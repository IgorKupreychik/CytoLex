using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace CytoLex.External.CytoGps;

/// <summary>Whether one path setting of the CytoGPS setup works (T-089).</summary>
/// <param name="Key">The <c>config/local.yaml</c> key, e.g. <c>java_home</c>.</param>
/// <param name="IsSet">Whether the key has a value (file or environment).</param>
/// <param name="IsOk">Whether the value passes the adapter's check.</param>
/// <param name="Message">What is wrong, or what was found.</param>
public sealed record CytoGpsFieldCheck(string Key, bool IsSet, bool IsOk, string Message);

/// <summary>The per-field checks of a CytoGPS setup (T-089), in key order.</summary>
public sealed record CytoGpsSetupReport(ImmutableArray<CytoGpsFieldCheck> Fields)
{
    /// <summary>Whether every path setting passes (the adapter can run).</summary>
    public bool IsConfigured => Fields.All(f => f.IsOk);

    /// <summary>The check of <paramref name="key"/>.</summary>
    public CytoGpsFieldCheck this[string key] => Fields.First(f => f.Key == key);
}

/// <summary>
/// The adapter's own checks of the path settings, one result per field (T-089; the same checks <see cref="CytoGpsBuild.TryResolve"/>
/// runs, which stops at the first problem). Reads files only (it hashes the javax.json jar); never starts a process.
/// </summary>
public static class CytoGpsSetupCheck
{
    /// <summary>Checks <c>source_dir</c>, <c>java_home</c> and <c>javax_json_jar</c> of <paramref name="config"/>.</summary>
    public static CytoGpsSetupReport Check(CytoGpsConfig config) => Check(config, CytoGpsBuild.JavaxJsonSha1);

    /// <summary>As <see cref="Check(CytoGpsConfig)"/> with another expected jar SHA-1 (tests).</summary>
    internal static CytoGpsSetupReport Check(CytoGpsConfig config, string expectedJarSha1)
    {
        ArgumentNullException.ThrowIfNull(config);
        return new CytoGpsSetupReport(
        [
            Field(CytoGpsConfigFile.SourceDirKey, config.SourceDir, v => SourceDirProblem(v, out _), "CytoGPS source tree found."),
            Field(CytoGpsConfigFile.JavaHomeKey, config.JavaHome, v => JavaHomeProblem(v, out _, out _), "JDK found (bin/java and bin/javac)."),
            Field(CytoGpsConfigFile.JavaxJsonJarKey, config.JavaxJsonJar, v => JarProblem(v, expectedJarSha1), "javax.json 1.1.4 (SHA-1 matches)."),
        ]);
    }

    private static CytoGpsFieldCheck Field(string key, string? value, Func<string, string?> problem, string ok)
    {
        if (value is null)
        {
            return new CytoGpsFieldCheck(key, false, false, "Not set.");
        }

        try
        {
            return problem(value) is { } message
                ? new CytoGpsFieldCheck(key, true, false, message)
                : new CytoGpsFieldCheck(key, true, true, ok);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return new CytoGpsFieldCheck(key, true, false, $"{key} '{value}' cannot be read: {ex.Message}");
        }
    }

    /// <summary>What is wrong with the CytoGPS source directory, or <c>null</c>; <paramref name="antlr"/> = its ANTLR jar.</summary>
    internal static string? SourceDirProblem(string sourceDir, out string? antlr)
    {
        antlr = null;
        if (!Directory.Exists(sourceDir))
        {
            return $"source_dir '{sourceDir}' does not exist.";
        }

        var src = Path.Combine(sourceDir, "src");
        var generated = Path.Combine(sourceDir, "target", "generated-sources", "antlr4");
        if (!File.Exists(Path.Combine(src, "toolkit", "AggregateJsonForBatchFile.java")) || !Directory.Exists(generated))
        {
            return $"source_dir '{sourceDir}' is not a CytoGPS source tree (expected src/toolkit/AggregateJsonForBatchFile.java and target/generated-sources/antlr4/).";
        }

        antlr = Path.GetFullPath(Path.Combine(sourceDir, CytoGpsBuild.AntlrJarPath));
        return File.Exists(antlr) ? null : $"the ANTLR runtime '{antlr}' is missing.";
    }

    /// <summary>What is wrong with the JDK directory, or <c>null</c>; the paths of <c>java</c> and <c>javac</c>.</summary>
    internal static string? JavaHomeProblem(string javaHome, out string java, out string javac)
    {
        var exe = OperatingSystem.IsWindows() ? ".exe" : "";
        java = Path.GetFullPath(Path.Combine(javaHome, "bin", "java" + exe));
        javac = Path.GetFullPath(Path.Combine(javaHome, "bin", "javac" + exe));
        return File.Exists(java) && File.Exists(javac)
            ? null
            : $"java_home '{javaHome}' is not a JDK (expected bin/java{exe} and bin/javac{exe}).";
    }

    /// <summary>What is wrong with the javax.json jar, or <c>null</c>.</summary>
    internal static string? JarProblem(string jar, string expectedSha1)
    {
        if (!File.Exists(jar))
        {
            return $"javax_json_jar '{jar}' does not exist (download org.glassfish:javax.json:1.1.4 from Maven Central).";
        }

        var sha1 = Sha1Hex(jar);
        return string.Equals(sha1, expectedSha1, StringComparison.Ordinal)
            ? null
            : $"javax_json_jar '{jar}' has SHA-1 {sha1}, expected {expectedSha1} (org.glassfish:javax.json:1.1.4).";
    }

    [SuppressMessage("Security", "CA5350:Do Not Use Weak Cryptographic Algorithms",
        Justification = "Integrity check against the SHA-1 Maven Central publishes for the jar; not a security boundary.")]
    private static string Sha1Hex(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA1.HashData(stream));
    }
}
