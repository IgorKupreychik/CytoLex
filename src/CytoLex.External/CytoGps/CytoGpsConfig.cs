using System.Globalization;
using YamlDotNet.RepresentationModel;

namespace CytoLex.External.CytoGps;

/// <summary>
/// The local CytoGPS setup (T-049): the <c>cytogps</c> section of <c>config/local.yaml</c>; each key can be
/// overridden by the environment variable <c>CYTOLEX_CYTOGPS_&lt;KEY&gt;</c> (e.g. <c>CYTOLEX_CYTOGPS_JAVA_HOME</c>).
/// Template: <c>config/local.example.yaml</c>.
/// </summary>
/// <param name="RepoRoot">The CytoLex repo root (the build and the opt-in disk cache go to <c>build/cache/cytogps/</c>).</param>
/// <param name="SourceDir">The unpacked CytoGPS source (<c>source_dir</c>).</param>
/// <param name="JavaHome">The JDK (<c>java_home</c>; needs <c>bin/java</c> and <c>bin/javac</c>).</param>
/// <param name="JavaxJsonJar">The <c>javax.json-1.1.4.jar</c> from Maven Central (<c>javax_json_jar</c>).</param>
/// <param name="DiskCache">Whether results are also cached on disk (<c>disk_cache</c>; synthetic/book corpora only, T-048).</param>
/// <param name="Timeout">The time limit of one CytoGPS run (<c>timeout_seconds</c>, default 600).</param>
public sealed record CytoGpsConfig(
    string RepoRoot,
    string? SourceDir,
    string? JavaHome,
    string? JavaxJsonJar,
    bool DiskCache,
    TimeSpan Timeout)
{
    /// <summary>The config section name in <c>config/local.yaml</c>.</summary>
    public const string Section = "cytogps";

    /// <summary>The default time limit of one CytoGPS run.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(600);

    /// <summary>The cache directory of the compiled build and the opt-in result cache.</summary>
    public string CacheDir => Path.Combine(RepoRoot, "build", "cache", "cytogps");

    /// <summary>
    /// Reads the setup from <c>config/local.yaml</c> under <paramref name="repoRoot"/> (found from the app
    /// directory when omitted) and the environment. A missing file or section gives empty settings (not configured).
    /// </summary>
    /// <exception cref="InvalidOperationException">The repo root cannot be found or the YAML is malformed.</exception>
    public static CytoGpsConfig Load(string? repoRoot = null, Func<string, string?>? environment = null)
    {
        repoRoot ??= RepoLocator.Find()
            ?? throw new InvalidOperationException("CytoLex repo root not found (no CytoLex.slnx above " + AppContext.BaseDirectory + ").");
        environment ??= Environment.GetEnvironmentVariable;

        var values = ReadSection(Path.Combine(repoRoot, "config", "local.yaml"));
        string? Get(string key)
        {
            var env = environment("CYTOLEX_CYTOGPS_" + key.ToUpperInvariant());
            var value = !string.IsNullOrWhiteSpace(env) ? env : values.GetValueOrDefault(key);
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        var diskCache = Get("disk_cache") is { } dc && bool.TryParse(dc, out var b) && b;
        var timeout = Get("timeout_seconds") is { } ts
            && int.TryParse(ts, NumberStyles.Integer, CultureInfo.InvariantCulture, out var secs) && secs > 0
                ? TimeSpan.FromSeconds(secs)
                : DefaultTimeout;
        return new CytoGpsConfig(repoRoot, Get("source_dir"), Get("java_home"), Get("javax_json_jar"), diskCache, timeout);
    }

    private static Dictionary<string, string> ReadSection(string path)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(path))
        {
            return result;
        }

        var stream = new YamlStream();
        try
        {
            using var reader = new StreamReader(path);
            stream.Load(reader);
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            throw new InvalidOperationException($"{path}: malformed YAML ({ex.Message}).", ex);
        }

        if (stream.Documents.Count == 0
            || stream.Documents[0].RootNode is not YamlMappingNode root
            || !root.Children.TryGetValue(new YamlScalarNode(Section), out var node)
            || node is not YamlMappingNode section)
        {
            return result;
        }

        foreach (var (key, value) in section.Children)
        {
            if (key is YamlScalarNode { Value: { } k } && value is YamlScalarNode { Value: { } v })
            {
                result[k] = v;
            }
        }

        return result;
    }
}

/// <summary>Finds the CytoLex repo root: the nearest directory above the app with <c>CytoLex.slnx</c>.</summary>
internal static class RepoLocator
{
    public static string? Find(string? start = null)
    {
        for (var dir = new DirectoryInfo(start ?? AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "CytoLex.slnx")))
            {
                return dir.FullName;
            }
        }

        return null;
    }
}
