using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using YamlDotNet.RepresentationModel;

namespace CytoLex.External.CytoGps;

/// <summary>
/// The <c>cytogps</c> section of <c>config/local.yaml</c> as a file (T-089): its keys, their environment overrides, the raw
/// values and a save that edits only the section's lines. YamlDotNet's representation model drops comments, so the save
/// changes the key lines in place instead of re-serializing: every other line (<c>iscn_pdf</c>, which the Python tools read,
/// other sections, comments) stays as it is. A missing file is created from <c>config/local.example.yaml</c>.
/// </summary>
public static partial class CytoGpsConfigFile
{
    /// <summary>The CytoGPS source directory.</summary>
    public const string SourceDirKey = "source_dir";

    /// <summary>The JDK directory.</summary>
    public const string JavaHomeKey = "java_home";

    /// <summary>The javax.json jar.</summary>
    public const string JavaxJsonJarKey = "javax_json_jar";

    /// <summary>Whether results are also cached on disk.</summary>
    public const string DiskCacheKey = "disk_cache";

    /// <summary>The time limit of one run in seconds.</summary>
    public const string TimeoutSecondsKey = "timeout_seconds";

    /// <summary>The keys of the section, in the template's order.</summary>
    public static ImmutableArray<string> Keys { get; } = [SourceDirKey, JavaHomeKey, JavaxJsonJarKey, DiskCacheKey, TimeoutSecondsKey];

    /// <summary>The keys whose values are paths (written as quoted strings).</summary>
    public static ImmutableArray<string> PathKeys { get; } = [SourceDirKey, JavaHomeKey, JavaxJsonJarKey];

    /// <summary>The environment variable that overrides <paramref name="key"/>, e.g. <c>CYTOLEX_CYTOGPS_JAVA_HOME</c>.</summary>
    public static string EnvironmentVariable(string key) => "CYTOLEX_CYTOGPS_" + key.ToUpperInvariant();

    /// <summary>The config file under <paramref name="repoRoot"/>.</summary>
    public static string PathOf(string repoRoot) => Path.Combine(repoRoot, "config", "local.yaml");

    /// <summary>The template the file is created from.</summary>
    public static string TemplatePathOf(string repoRoot) => Path.Combine(repoRoot, "config", "local.example.yaml");

    /// <summary>The values of the section as written in the file (no environment); empty when the file or section is missing.</summary>
    /// <exception cref="InvalidOperationException">The YAML is malformed.</exception>
    public static ImmutableDictionary<string, string> ReadValues(string repoRoot) => ReadSection(PathOf(repoRoot));

    /// <summary>The section's scalar values in <paramref name="path"/>.</summary>
    /// <exception cref="InvalidOperationException">The YAML is malformed.</exception>
    internal static ImmutableDictionary<string, string> ReadSection(string path)
    {
        if (!File.Exists(path))
        {
            return ImmutableDictionary<string, string>.Empty;
        }

        using var reader = new StreamReader(path);
        return ReadSection(reader, path);
    }

    private static ImmutableDictionary<string, string> ReadSection(TextReader reader, string name)
    {
        var stream = new YamlStream();
        try
        {
            stream.Load(reader);
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            throw new InvalidOperationException($"{name}: malformed YAML ({ex.Message}).", ex);
        }

        if (stream.Documents.Count == 0
            || stream.Documents[0].RootNode is not YamlMappingNode root
            || !root.Children.TryGetValue(new YamlScalarNode(CytoGpsConfig.Section), out var node)
            || node is not YamlMappingNode section)
        {
            return ImmutableDictionary<string, string>.Empty;
        }

        var result = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in section.Children)
        {
            if (key is YamlScalarNode { Value: { } k } && value is YamlScalarNode { Value: { } v })
            {
                result[k] = v;
            }
        }

        return result.ToImmutable();
    }

    /// <summary>
    /// Writes <paramref name="values"/> into the section of <c>config/local.yaml</c> (atomically, LF line endings). A key with
    /// a <c>null</c> or empty value is commented out; keys not in <paramref name="values"/> are left alone. The written file is
    /// read back before it replaces the old one; on any problem the old file stays.
    /// </summary>
    /// <returns>The path written.</returns>
    /// <exception cref="InvalidOperationException">The new text does not read back as the given values, or the template/file is malformed.</exception>
    /// <exception cref="IOException">The file cannot be written.</exception>
    public static string Save(string repoRoot, IReadOnlyDictionary<string, string?> values)
    {
        ArgumentNullException.ThrowIfNull(repoRoot);
        ArgumentNullException.ThrowIfNull(values);
        var path = PathOf(repoRoot);
        var template = TemplatePathOf(repoRoot);
        var text = File.Exists(path) ? File.ReadAllText(path)
            : File.Exists(template) ? File.ReadAllText(template)
            : string.Empty;
        var updated = Edit(text, values);

        // Read back before replacing anything: a save must never leave a file that says something else.
        var readBack = ReadSection(new StringReader(updated), path);
        foreach (var (key, value) in values)
        {
            var expected = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            var actual = readBack.GetValueOrDefault(key) is { } v && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"{path}: the edited file would read '{key}' as '{actual ?? "(not set)"}' instead of '{expected ?? "(not set)"}'; nothing was saved.");
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        try
        {
            File.WriteAllBytes(temp, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(updated));
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }

        return path;
    }

    /// <summary>
    /// The text with the section's key lines set to <paramref name="values"/> (LF line endings). Exposed for tests; see
    /// <see cref="Save"/>.
    /// </summary>
    internal static string Edit(string text, IReadOnlyDictionary<string, string?> values)
    {
        var lines = text.ReplaceLineEndings("\n").Split('\n').ToList();
        var endsWithNewline = lines.Count > 0 && lines[^1].Length == 0;
        if (endsWithNewline)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        var anySet = values.Values.Any(v => !string.IsNullOrWhiteSpace(v));
        var header = FindHeader(lines);
        if (header is null)
        {
            if (!anySet)
            {
                return Join(lines, endsWithNewline || lines.Count == 0);
            }

            if (lines.Count > 0 && lines[^1].Trim().Length > 0)
            {
                lines.Add(string.Empty);
            }

            lines.Add(CytoGpsConfig.Section + ":");
            header = (lines.Count - 1, false);
        }

        var (headerIndex, commented) = header.Value;
        if (commented && anySet)
        {
            lines[headerIndex] = CytoGpsConfig.Section + ":" + TrailingComment(lines[headerIndex]);
            commented = false;
        }

        foreach (var key in Keys.Where(values.ContainsKey))
        {
            var value = values[key];
            var block = BlockKeyLines(lines, headerIndex, commented);
            var real = block.FirstOrDefault(l => l.Key == key && !l.Commented);
            var disabled = block.FirstOrDefault(l => l.Key == key && l.Commented);
            if (string.IsNullOrWhiteSpace(value))
            {
                if (real is not null)
                {
                    lines[real.Index] = "# " + lines[real.Index];
                }

                continue;
            }

            var formatted = Format(key, value.Trim());
            if (real is not null)
            {
                lines[real.Index] = WithValue(lines[real.Index], formatted);
            }
            else if (disabled is not null)
            {
                lines[disabled.Index] = WithValue(Uncomment(lines[disabled.Index]), formatted);
            }
            else
            {
                var after = block.Count > 0 ? block.Max(l => l.Index) : headerIndex;
                lines.Insert(after + 1, $"  {key}: {formatted}");
            }
        }

        return Join(lines, endsWithNewline || lines.Count > 0);
    }

    private static string Join(List<string> lines, bool trailingNewline) =>
        string.Join("\n", lines) + (trailingNewline && lines.Count > 0 ? "\n" : string.Empty);

    /// <summary>The section header: an active <c>cytogps:</c>, else the template's commented <c># cytogps:</c>.</summary>
    private static (int Index, bool Commented)? FindHeader(List<string> lines)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            if (ActiveHeader().IsMatch(lines[i]))
            {
                return (i, false);
            }
        }

        for (var i = 0; i < lines.Count; i++)
        {
            if (CommentedHeader().IsMatch(lines[i]))
            {
                return (i, true);
            }
        }

        return null;
    }

    private sealed record KeyLine(int Index, string Key, bool Commented);

    /// <summary>
    /// The key lines of the section: after an active header, every line up to the next top-level key (indented keys and
    /// commented-out keys); after a commented header, the run of commented indented lines.
    /// </summary>
    private static List<KeyLine> BlockKeyLines(List<string> lines, int header, bool commentedHeader)
    {
        var result = new List<KeyLine>();
        for (var i = header + 1; i < lines.Count; i++)
        {
            var line = lines[i];
            if (commentedHeader)
            {
                if (!CommentedIndented().IsMatch(line))
                {
                    break;
                }
            }
            else if (line.Length > 0 && !char.IsWhiteSpace(line[0]) && line[0] != '#')
            {
                break;
            }

            if (KeyLinePattern().Match(line) is { Success: true } m && Keys.Contains(m.Groups["key"].Value))
            {
                var isCommented = m.Groups["hash"].Success;
                if (!isCommented && m.Groups["indent"].Value.Length == 0)
                {
                    continue;
                }

                result.Add(new KeyLine(i, m.Groups["key"].Value, isCommented));
            }
        }

        return result;
    }

    /// <summary>The line with its value replaced; the inline comment keeps its column where it fits.</summary>
    private static string WithValue(string line, string value)
    {
        var colon = line.IndexOf(':', StringComparison.Ordinal);
        var start = colon + 1;
        while (start < line.Length && line[start] == ' ')
        {
            start++;
        }

        var prefix = line[..(colon + 1)] + " ";
        var comment = CommentStart(line, start);
        if (comment < 0)
        {
            return prefix + value;
        }

        var head = prefix + value;
        return head.Length < comment ? head.PadRight(comment) + line[comment..] : head + " " + line[comment..];
    }

    /// <summary>Where the inline comment of a value starting at <paramref name="start"/> begins, or −1.</summary>
    private static int CommentStart(string line, int start)
    {
        var i = start;
        if (i < line.Length && line[i] == '"')
        {
            for (i++; i < line.Length && line[i] != '"'; i++)
            {
                if (line[i] == '\\')
                {
                    i++;
                }
            }

            i++;
        }
        else if (i < line.Length && line[i] == '\'')
        {
            for (i++; i < line.Length; i++)
            {
                if (line[i] == '\'')
                {
                    if (i + 1 < line.Length && line[i + 1] == '\'')
                    {
                        i++;
                        continue;
                    }

                    break;
                }
            }

            i++;
        }

        for (; i < line.Length; i++)
        {
            if (line[i] == '#' && (i == 0 || line[i - 1] == ' ' || line[i - 1] == '\t'))
            {
                return i;
            }
        }

        return -1;
    }

    private static string TrailingComment(string headerLine)
    {
        var uncommented = Uncomment(headerLine).TrimStart();
        var colon = uncommented.IndexOf(':', StringComparison.Ordinal);
        var rest = uncommented[(colon + 1)..];
        var hash = rest.IndexOf('#', StringComparison.Ordinal);
        return hash < 0 ? string.Empty : " " + rest[hash..];
    }

    /// <summary><c>#   key: …</c> → <c>  key: …</c> (the <c>#</c> and one space go).</summary>
    private static string Uncomment(string line)
    {
        var hash = line.IndexOf('#', StringComparison.Ordinal);
        var rest = line[(hash + 1)..];
        return rest.StartsWith(' ') ? rest[1..] : rest;
    }

    /// <summary>The value as YAML: booleans and integers bare, paths (and anything else) as a double-quoted string.</summary>
    private static string Format(string key, string value)
    {
        if (key == DiskCacheKey && bool.TryParse(value, out var b))
        {
            return b ? "true" : "false";
        }

        if (key == TimeoutSecondsKey && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
        {
            return n.ToString(CultureInfo.InvariantCulture);
        }

        var sb = new StringBuilder("\"");
        foreach (var ch in value)
        {
            _ = ch switch
            {
                '\\' => sb.Append(@"\\"),
                '"' => sb.Append("\\\""),
                '\t' => sb.Append(@"\t"),
                _ when char.IsControl(ch) => sb.Append(CultureInfo.InvariantCulture, $"\\u{(int)ch:x4}"),
                _ => sb.Append(ch),
            };
        }

        return sb.Append('"').ToString();
    }

    [GeneratedRegex(@"^cytogps\s*:\s*(#.*)?$")]
    private static partial Regex ActiveHeader();

    [GeneratedRegex(@"^#\s*cytogps\s*:\s*(#.*)?$")]
    private static partial Regex CommentedHeader();

    [GeneratedRegex(@"^#\s{2,}\S")]
    private static partial Regex CommentedIndented();

    [GeneratedRegex(@"^(?<hash>#)?(?<indent>\s*)(?<key>[A-Za-z_][A-Za-z0-9_]*)\s*:")]
    private static partial Regex KeyLinePattern();
}
