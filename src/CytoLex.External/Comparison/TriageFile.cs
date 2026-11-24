using System.Collections.Immutable;
using System.Text;
using YamlDotNet.RepresentationModel;

namespace CytoLex.External.Comparison;

/// <summary>Why CytoLex and an external checker disagree on a string (docs/decisions.md "External comparison").</summary>
public enum TriageLabel
{
    /// <summary>CytoLex is wrong: a bug to fix (and, once confirmed, a new KB example).</summary>
    OurBug,

    /// <summary>The checker doesn't check or can't read what the book prescribes.</summary>
    CheckerLimitation,

    /// <summary>The two follow different ISCN editions or readings of the edition.</summary>
    VersionDifference,

    /// <summary>
    /// CytoLex reports the fault, but below error by its severity policy (decisions T-022/T-056), so its verdict is
    /// valid while the checker rejects the string. Neither a bug nor a checker limitation (see T-075, T-080).
    /// </summary>
    SeverityPolicy,
}

/// <summary>The expected verdict of a triaged string, for the KB-example export.</summary>
public enum ExpectedVerdict
{
    Valid,
    Invalid,
}

/// <summary>One triaged string.</summary>
/// <param name="Iscn">The string as in the batch (the key).</param>
/// <param name="Label">Why the tools disagree.</param>
/// <param name="Confirmed">Whether a person checked the label (suggested labels are not confirmed).</param>
/// <param name="Note">A short reason.</param>
/// <param name="Expected">The verdict the string should get; with <paramref name="KbRule"/>, it is exported as a KB example.</param>
/// <param name="KbRule">The KB rule the string is an example of.</param>
/// <param name="Line">The 1-based line of the entry in its file (0 when built in code).</param>
public sealed record TriageEntry(
    string Iscn,
    TriageLabel Label,
    bool Confirmed,
    string? Note,
    ExpectedVerdict? Expected,
    string? KbRule,
    int Line = 0);

/// <summary>A triage file: the labels of one checker and edition, keyed by string (T-051).</summary>
public sealed class TriageFile
{
    private readonly Dictionary<string, TriageEntry> _byIscn;

    /// <summary>Creates a triage file; the first entry wins for a repeated string.</summary>
    /// <param name="checker">The checker the labels are for.</param>
    /// <param name="edition">The ISCN edition.</param>
    /// <param name="entries">The entries, in file order.</param>
    /// <param name="header">The comment lines above the YAML (each starting with <c>#</c>), kept when the file is saved.</param>
    public TriageFile(string checker, string edition, ImmutableArray<TriageEntry> entries, ImmutableArray<string> header = default)
    {
        Checker = checker;
        Edition = edition;
        Entries = entries;
        Header = header.IsDefault ? [] : header;
        _byIscn = entries.GroupBy(e => e.Iscn, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
    }

    /// <summary>The checker the labels are for, e.g. <c>CytoGPS</c>.</summary>
    public string Checker { get; }

    /// <summary>The ISCN edition of the comparison, e.g. <c>2016</c>.</summary>
    public string Edition { get; }

    /// <summary>The entries, in file order.</summary>
    public ImmutableArray<TriageEntry> Entries { get; }

    /// <summary>The comment lines at the top of the file (each starting with <c>#</c>), written back by <see cref="ToYaml"/>.</summary>
    public ImmutableArray<string> Header { get; }

    /// <summary>The <c>format</c> value of a triage file.</summary>
    public const string Format = "cytolex-triage";

    /// <summary>The <c>version</c> value written and read.</summary>
    public const int Version = 1;

    /// <summary>An empty triage file for a checker and edition.</summary>
    public static TriageFile Empty(string checker, string edition) => new(checker, edition, []);

    /// <summary>The entry for a string, if any.</summary>
    public TriageEntry? Find(string iscn) => _byIscn.GetValueOrDefault(iscn);

    /// <summary>
    /// This file with <paramref name="entry"/> in place of the entry for the same string (same position), or appended
    /// when the string has no entry yet.
    /// </summary>
    public TriageFile With(TriageEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var index = -1;
        for (var i = 0; i < Entries.Length && index < 0; i++)
        {
            if (string.Equals(Entries[i].Iscn, entry.Iscn, StringComparison.Ordinal))
            {
                index = i;
            }
        }

        var entries = index >= 0 ? Entries.SetItem(index, entry) : Entries.Add(entry);
        return new TriageFile(Checker, Edition, entries, Header);
    }

    /// <summary>This file without the entry for <paramref name="iscn"/> (the same file when there is none).</summary>
    public TriageFile Without(string iscn) =>
        Find(iscn) is null
            ? this
            : new TriageFile(Checker, Edition, [.. Entries.Where(e => !string.Equals(e.Iscn, iscn, StringComparison.Ordinal))], Header);

    /// <summary>Writes the file to <paramref name="path"/> (UTF-8 without BOM, LF).</summary>
    public void Save(string path) => File.WriteAllText(path, ToYaml(), new UTF8Encoding(false));

    /// <summary>The YAML name of a label.</summary>
    public static string LabelName(TriageLabel label) => label switch
    {
        TriageLabel.OurBug => "our-bug",
        TriageLabel.CheckerLimitation => "checker-limitation",
        TriageLabel.VersionDifference => "version-difference",
        TriageLabel.SeverityPolicy => "severity-policy",
        _ => throw new ArgumentOutOfRangeException(nameof(label)),
    };

    /// <summary>Reads a triage file.</summary>
    /// <exception cref="TriageFileException">The file is malformed; the message names the line.</exception>
    public static TriageFile Load(string path) => Parse(File.ReadAllText(path, Encoding.UTF8), path);

    /// <summary>Reads triage YAML; <paramref name="source"/> names it in errors.</summary>
    /// <exception cref="TriageFileException">The YAML is malformed; the message names the line.</exception>
    public static TriageFile Parse(string yaml, string source = "triage")
    {
        var stream = new YamlStream();
        try
        {
            stream.Load(new StringReader(yaml));
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            throw new TriageFileException($"{source}:{ex.Start.Line}: malformed YAML ({ex.Message}).", ex);
        }

        if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode root)
        {
            throw new TriageFileException($"{source}: expected a mapping with format, version, checker, edition and entries.");
        }

        TriageFileException Error(YamlNode node, string message) => new($"{source}:{node.Start.Line}: {message}");

        string Scalar(YamlMappingNode map, string key, bool required)
        {
            if (!map.Children.TryGetValue(new YamlScalarNode(key), out var node))
            {
                return required ? throw Error(map, $"missing '{key}'.") : "";
            }

            return node is YamlScalarNode { Value: { } v } ? v : throw Error(node, $"'{key}' must be a string.");
        }

        if (Scalar(root, "format", true) != Format)
        {
            throw Error(root, $"format must be '{Format}'.");
        }

        if (Scalar(root, "version", true) != Version.ToString(System.Globalization.CultureInfo.InvariantCulture))
        {
            throw Error(root, $"version must be {Version}.");
        }

        var checker = Scalar(root, "checker", true);
        var edition = Scalar(root, "edition", true);
        var entries = ImmutableArray.CreateBuilder<TriageEntry>();
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        if (root.Children.TryGetValue(new YamlScalarNode("entries"), out var entriesNode)
            && !(entriesNode is YamlScalarNode { Value: null or "" }))
        {
            if (entriesNode is not YamlSequenceNode list)
            {
                throw Error(entriesNode, "'entries' must be a list.");
            }

            foreach (var item in list.Children)
            {
                if (item is not YamlMappingNode entry)
                {
                    throw Error(item, "an entry must be a mapping.");
                }

                var line = (int)entry.Start.Line;
                var known = new HashSet<string>(["iscn", "label", "confirmed", "note", "expected", "kb_rule"], StringComparer.Ordinal);
                foreach (var key in entry.Children.Keys)
                {
                    if (key is not YamlScalarNode { Value: { } k } || !known.Contains(k))
                    {
                        throw Error(key, $"unknown key '{key}'.");
                    }
                }

                var iscn = Scalar(entry, "iscn", true);
                if (iscn.Length == 0)
                {
                    throw Error(entry, "'iscn' is empty.");
                }

                if (seen.TryGetValue(iscn, out var first))
                {
                    throw Error(entry, $"duplicate iscn \"{iscn}\" (first on line {first}).");
                }

                seen[iscn] = line;
                var labelText = Scalar(entry, "label", true);
                var label = labelText switch
                {
                    "our-bug" => TriageLabel.OurBug,
                    "checker-limitation" => TriageLabel.CheckerLimitation,
                    "version-difference" => TriageLabel.VersionDifference,
                    "severity-policy" => TriageLabel.SeverityPolicy,
                    _ => throw Error(entry, $"label \"{labelText}\" is not one of our-bug, checker-limitation, version-difference, severity-policy."),
                };

                var confirmedText = Scalar(entry, "confirmed", false);
                var confirmed = confirmedText switch
                {
                    "" or "false" => false,
                    "true" => true,
                    _ => throw Error(entry, "'confirmed' must be true or false."),
                };
                var note = Scalar(entry, "note", false);
                var expectedText = Scalar(entry, "expected", false);
                ExpectedVerdict? expected = expectedText switch
                {
                    "" => null,
                    "valid" => ExpectedVerdict.Valid,
                    "invalid" => ExpectedVerdict.Invalid,
                    _ => throw Error(entry, "'expected' must be valid or invalid."),
                };
                var kbRule = Scalar(entry, "kb_rule", false);
                if ((expected is null) != (kbRule.Length == 0))
                {
                    throw Error(entry, "'expected' and 'kb_rule' go together (both or neither).");
                }

                entries.Add(new TriageEntry(iscn, label, confirmed, note.Length == 0 ? null : note, expected,
                    kbRule.Length == 0 ? null : kbRule, line));
            }
        }

        return new TriageFile(checker, edition, entries.ToImmutable(), LeadingComments(yaml));
    }

    private static ImmutableArray<string> LeadingComments(string yaml) =>
    [
        .. yaml.Split('\n')
            .TakeWhile(l => l.StartsWith('#'))
            .Select(l => l.TrimEnd()),
    ];

    /// <summary>Writes the file as YAML (LF, stable key order), after its <see cref="Header"/> comments.</summary>
    public string ToYaml()
    {
        var sb = new StringBuilder();
        foreach (var line in Header)
        {
            sb.Append(line).Append('\n');
        }

        sb.Append("format: ").Append(Format).Append('\n');
        sb.Append("version: ").Append(Version.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
        sb.Append("checker: ").Append(Yaml.Quote(Checker)).Append('\n');
        sb.Append("edition: ").Append(Yaml.Quote(Edition)).Append('\n');
        if (Entries.IsEmpty)
        {
            sb.Append("entries: []\n");
            return sb.ToString();
        }

        sb.Append("entries:\n");
        foreach (var e in Entries)
        {
            AppendEntry(sb, e.Iscn, LabelName(e.Label), e.Confirmed, e.Note, e.Expected, e.KbRule);
        }

        return sb.ToString();
    }

    internal static void AppendEntry(StringBuilder sb, string iscn, string label, bool confirmed, string? note, ExpectedVerdict? expected, string? kbRule)
    {
        sb.Append("  - iscn: ").Append(Yaml.Quote(iscn)).Append('\n');
        sb.Append("    label: ").Append(label).Append('\n');
        sb.Append("    confirmed: ").Append(confirmed ? "true" : "false").Append('\n');
        if (note is not null)
        {
            sb.Append("    note: ").Append(Yaml.Quote(note)).Append('\n');
        }

        if (expected is not null && kbRule is not null)
        {
            sb.Append("    expected: ").Append(expected == ExpectedVerdict.Valid ? "valid" : "invalid").Append('\n');
            sb.Append("    kb_rule: ").Append(kbRule).Append('\n');
        }
    }
}

/// <summary>A malformed triage file.</summary>
public sealed class TriageFileException : Exception
{
    public TriageFileException()
    {
    }

    public TriageFileException(string message)
        : base(message)
    {
    }

    public TriageFileException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Minimal YAML scalar quoting for the files the comparison writes.</summary>
internal static class Yaml
{
    /// <summary>A double-quoted YAML scalar.</summary>
    public static string Quote(string value)
    {
        var sb = new StringBuilder(value.Length + 2).Append('"');
        foreach (var c in value)
        {
            _ = c switch
            {
                '"' => sb.Append("\\\""),
                '\\' => sb.Append("\\\\"),
                '\n' => sb.Append("\\n"),
                '\t' => sb.Append("\\t"),
                _ when char.IsControl(c) => sb.Append("\\u").Append(((int)c).ToString("x4", System.Globalization.CultureInfo.InvariantCulture)),
                _ => sb.Append(c),
            };
        }

        return sb.Append('"').ToString();
    }
}
