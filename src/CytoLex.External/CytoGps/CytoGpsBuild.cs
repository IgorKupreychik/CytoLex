using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace CytoLex.External.CytoGps;

/// <summary>The resolved paths of a configured CytoGPS setup.</summary>
internal sealed record CytoGpsSetup(
    string Java,
    string Javac,
    string AntlrJar,
    string JavaxJsonJar,
    ImmutableArray<string> Sources,
    string SourceDir,
    string CacheDir)
{
    public string ClassesDir => Path.Combine(CacheDir, "classes");

    public string ClassPath => string.Join(Path.PathSeparator, ClassesDir, AntlrJar, JavaxJsonJar);
}

/// <summary>
/// Checks the CytoGPS setup and compiles it on demand into <c>build/cache/cytogps/classes/</c> (T-049). The build is
/// reused while its fingerprint (sources, wrapper, jars, JDK path) is unchanged. Nothing here uses the network.
/// </summary>
internal static class CytoGpsBuild
{
    /// <summary>SHA-1 of <c>org.glassfish:javax.json:1.1.4</c> as published on Maven Central (<c>.jar.sha1</c>).</summary>
    public const string JavaxJsonSha1 = "943f240a509d3c70b448a55c6735591ecbd37c88";

    /// <summary>The ANTLR runtime CytoGPS ships, relative to its source directory.</summary>
    public const string AntlrJarPath = "resource/antlr-4.7-complete.jar";

    private const string _wrapperResource = "CytoLex.External.CytoGpsCli.java";
    private static readonly SemaphoreSlim _buildLock = new(1, 1);

    /// <summary>Resolves the setup, or says what is missing ("CytoGPS not configured: …").</summary>
    /// <param name="config">The setup to check.</param>
    /// <param name="setup">The resolved paths when configured.</param>
    /// <param name="reason">What is missing when not configured.</param>
    /// <param name="expectedJarSha1">The expected SHA-1 of the javax.json jar (tests only; default <see cref="JavaxJsonSha1"/>).</param>
    public static bool TryResolve(
        CytoGpsConfig config,
        [NotNullWhen(true)] out CytoGpsSetup? setup,
        [NotNullWhen(false)] out string? reason,
        string expectedJarSha1 = JavaxJsonSha1)
    {
        setup = null;
        reason = Check(config, expectedJarSha1, out var java, out var javac, out var antlr, out var sources);
        if (reason is not null)
        {
            reason = "CytoGPS not configured: " + reason;
            return false;
        }

        setup = new CytoGpsSetup(java!, javac!, antlr!, Path.GetFullPath(config.JavaxJsonJar!), sources, Path.GetFullPath(config.SourceDir!), config.CacheDir);
        return true;
    }

    private static string? Check(CytoGpsConfig c, string expectedSha1, out string? java, out string? javac, out string? antlr, out ImmutableArray<string> sources)
    {
        java = javac = antlr = null;
        sources = [];
        const string Where = " in config/local.yaml (section 'cytogps', see config/local.example.yaml) or the environment variable ";
        if (c.SourceDir is null)
        {
            return "set 'source_dir'" + Where + "CYTOLEX_CYTOGPS_SOURCE_DIR.";
        }

        if (c.JavaHome is null)
        {
            return "set 'java_home'" + Where + "CYTOLEX_CYTOGPS_JAVA_HOME.";
        }

        if (c.JavaxJsonJar is null)
        {
            return "set 'javax_json_jar'" + Where + "CYTOLEX_CYTOGPS_JAVAX_JSON_JAR.";
        }

        // The per-field checks are shared with the settings view (T-089); this order gives the first problem.
        if (CytoGpsSetupCheck.SourceDirProblem(c.SourceDir, out antlr) is { } sourceProblem)
        {
            return sourceProblem;
        }

        if (CytoGpsSetupCheck.JavaHomeProblem(c.JavaHome, out var javaPath, out var javacPath) is { } jdkProblem)
        {
            return jdkProblem;
        }

        (java, javac) = (javaPath, javacPath);
        if (CytoGpsSetupCheck.JarProblem(c.JavaxJsonJar, expectedSha1) is { } jarProblem)
        {
            return jarProblem;
        }

        var src = Path.Combine(c.SourceDir, "src");
        var generated = Path.Combine(c.SourceDir, "target", "generated-sources", "antlr4");
        sources = [.. Directory.EnumerateFiles(src, "*.java", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(generated, "*.java", SearchOption.AllDirectories))
            .Select(Path.GetFullPath)
            .Order(StringComparer.Ordinal)];
        return null;
    }

    /// <summary>The build fingerprint: sources, wrapper, jars and JDK path.</summary>
    public static string Fingerprint(CytoGpsSetup setup)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        void Add(string s) => hash.AppendData(Encoding.UTF8.GetBytes(s + "\n"));
        Add("cytolex-cytogps-build-1");
        Add(setup.Javac);
        Add(WrapperSource());
        foreach (var file in setup.Sources.Append(setup.AntlrJar).Append(setup.JavaxJsonJar))
        {
            Add(Path.GetRelativePath(setup.SourceDir, file).Replace('\\', '/'));
            hash.AppendData(File.ReadAllBytes(file));
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    /// <summary>
    /// Compiles the build unless it is current. Returns the fingerprint, or throws <see cref="InvalidOperationException"/>
    /// with the javac output when compiling fails.
    /// </summary>
    public static async Task<string> EnsureBuiltAsync(CytoGpsSetup setup, IProcessRunner runner, IProgress<string>? progress, CancellationToken ct)
    {
        await _buildLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var fingerprint = Fingerprint(setup);
            var stamp = Path.Combine(setup.CacheDir, "fingerprint.txt");
            if (Directory.Exists(setup.ClassesDir) && File.Exists(stamp)
                && string.Equals(File.ReadAllText(stamp).Trim(), fingerprint, StringComparison.Ordinal))
            {
                return fingerprint;
            }

            progress?.Report("Compiling CytoGPS into " + setup.ClassesDir);
            if (File.Exists(stamp))
            {
                File.Delete(stamp);
            }

            if (Directory.Exists(setup.ClassesDir))
            {
                Directory.Delete(setup.ClassesDir, recursive: true);
            }

            Directory.CreateDirectory(setup.ClassesDir);
            var wrapperDir = Path.Combine(setup.CacheDir, "src");
            Directory.CreateDirectory(wrapperDir);
            var wrapper = Path.Combine(wrapperDir, "CytoGpsCli.java");
            await File.WriteAllTextAsync(wrapper, WrapperSource(), ct).ConfigureAwait(false);

            // javac argument file: one quoted path per line, forward slashes (backslashes are escapes there).
            var argFile = Path.Combine(setup.CacheDir, "sources.txt");
            var lines = setup.Sources.Append(wrapper).Select(p => "\"" + p.Replace('\\', '/') + "\"");
            await File.WriteAllTextAsync(argFile, string.Join("\n", lines) + "\n", ct).ConfigureAwait(false);

            string[] args =
            [
                "-nowarn", "-encoding", "UTF-8", "-d", setup.ClassesDir,
                "-cp", string.Join(Path.PathSeparator, setup.AntlrJar, setup.JavaxJsonJar),
                "@" + argFile,
            ];
            var outcome = await runner.RunAsync(setup.Javac, args, setup.CacheDir, TimeSpan.FromMinutes(5), ct).ConfigureAwait(false);
            if (outcome.TimedOut || outcome.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    "Compiling CytoGPS failed" + (outcome.TimedOut ? " (timed out)" : $" (javac exit {outcome.ExitCode})") + ":\n"
                    + Tail(outcome.StdErr + outcome.StdOut));
            }

            await File.WriteAllTextAsync(stamp, fingerprint + "\n", ct).ConfigureAwait(false);
            return fingerprint;
        }
        finally
        {
            _buildLock.Release();
        }
    }

    /// <summary>Our wrapper's Java source (embedded resource).</summary>
    public static string WrapperSource()
    {
        using var stream = typeof(CytoGpsBuild).Assembly.GetManifestResourceStream(_wrapperResource)
            ?? throw new InvalidOperationException("Embedded resource missing: " + _wrapperResource);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd().ReplaceLineEndings("\n");
    }

    /// <summary>The last part of a long tool output.</summary>
    public static string Tail(string text, int max = 4000) =>
        text.Length <= max ? text.Trim() : "…" + text[^max..].Trim();
}
