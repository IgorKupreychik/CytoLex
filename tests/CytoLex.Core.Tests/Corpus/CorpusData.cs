using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using CytoLex.Core.Tests.KbExamples;
using YamlDotNet.RepresentationModel;

namespace CytoLex.Core.Tests.Corpus;

/// <summary>Expected verdict of a corpus entry in one edition (T-062).</summary>
internal enum CorpusVerdict
{
    /// <summary>Parse-only: it must split and parse, no claim about validity.</summary>
    Unreviewed,

    Valid,

    Invalid,
}

/// <summary>The expected result of a corpus entry in one edition: verdict, optional context hint, rules that must fire.</summary>
internal sealed record CorpusExpectation(CorpusVerdict Verdict, SampleContext? Context, ImmutableArray<string> Fires);

/// <summary>One entry of a corpus file (<c>curated/corpus/&lt;type&gt;.yaml</c>; schema <c>kb/schema/corpus.schema.json</c>).</summary>
internal sealed record CorpusEntry(
    string File,
    string Id,
    string Iscn,
    ImmutableArray<string> Types,
    ImmutableArray<string> Features,
    ImmutableArray<string> Rules,
    string Source,
    ImmutableDictionary<string, CorpusExpectation> Expected,
    bool ReviewChecked)
{
    public override string ToString() => $"{Id}: {Iscn}";
}

/// <summary>
/// The test corpus (T-062): every <c>*.yaml</c> file in <c>data/corpus/</c> (copied from <c>curated/corpus/</c>).
/// The schema is checked by <c>kb check</c>; this loader reads the fields the harness needs and rejects what it can't read.
/// </summary>
internal static class CorpusData
{
    public const string Format = "cytolex-corpus";

    /// <summary>Core codes (<c>CORE.*</c>) are not KB rules but may be named in <c>fires</c>.</summary>
    public const string CorePrefix = "CORE.";

    private static readonly Lazy<ImmutableArray<CorpusEntry>> _entries = new(() => LoadDirectory(Directory));

    public static string Directory => Path.Combine(AppContext.BaseDirectory, "data", "corpus");

    public static ImmutableArray<CorpusEntry> Entries => _entries.Value;

    public static CorpusEntry Find(string id) => Entries.Single(e => e.Id == id);

    public static ImmutableArray<CorpusEntry> LoadDirectory(string directory) =>
        [.. System.IO.Directory.GetFiles(directory, "*.yaml").Order(StringComparer.Ordinal).SelectMany(LoadFile)];

    public static IEnumerable<CorpusEntry> LoadFile(string path) =>
        Parse(File.ReadAllText(path, Encoding.UTF8), Path.GetFileName(path));

    public static IReadOnlyList<CorpusEntry> Parse(string yaml, string file)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        var root = (YamlMappingNode)stream.Documents[0].RootNode;
        if (Scalar(root, "format") != Format)
        {
            throw new InvalidDataException($"{file}: format must be '{Format}'.");
        }

        var entries = new List<CorpusEntry>();
        foreach (var node in ((YamlSequenceNode)root["corpus"]).Children.Cast<YamlMappingNode>())
        {
            var expected = ImmutableDictionary.CreateBuilder<string, CorpusExpectation>(StringComparer.Ordinal);
            foreach (var (edition, value) in ((YamlMappingNode)node["expected"]).Children)
            {
                var map = (YamlMappingNode)value;
                expected[((YamlScalarNode)edition).Value!] = new CorpusExpectation(
                    Scalar(map, "verdict") switch
                    {
                        "valid" => CorpusVerdict.Valid,
                        "invalid" => CorpusVerdict.Invalid,
                        "unreviewed" => CorpusVerdict.Unreviewed,
                        var v => throw new InvalidDataException($"{file}:{map.Start.Line}: unknown verdict '{v}'."),
                    },
                    Scalar(map, "context") switch
                    {
                        null => null,
                        "constitutional" => SampleContext.Constitutional,
                        "acquired" => SampleContext.Acquired,
                        var c => throw new InvalidDataException($"{file}:{map.Start.Line}: unknown context '{c}'."),
                    },
                    Strings(map, "fires"));
            }

            var review = (YamlMappingNode)node["review"];
            entries.Add(new CorpusEntry(
                file,
                Scalar(node, "id")!,
                Scalar(node, "iscn")!,
                Strings(node, "types"),
                Strings(node, "features"),
                Strings(node, "rules"),
                Scalar(node, "source")!,
                expected.ToImmutable(),
                bool.Parse(Scalar(review, "checked")!)));
        }

        return entries;
    }

    /// <summary>
    /// Whether the expected result of <paramref name="entry"/> in <paramref name="edition"/> is checked (task rule T-062):
    /// a verdict is given and a person checked the entry, or every rule it names is a rule of that edition in the KB.
    /// Otherwise the entry is parse-only. <paramref name="reason"/> says why a label isn't enforced.
    /// </summary>
    public static bool IsEnforced(CorpusEntry entry, string edition, IReadOnlySet<string> kbRules, out string reason)
    {
        if (!entry.Expected.TryGetValue(edition, out var expected) || expected.Verdict == CorpusVerdict.Unreviewed)
        {
            reason = "unreviewed (parse-only)";
            return false;
        }

        if (entry.ReviewChecked)
        {
            reason = "";
            return true;
        }

        var missing = entry.Rules.Concat(expected.Fires)
            .Where(r => !r.StartsWith(CorePrefix, StringComparison.Ordinal) && !kbRules.Contains(r))
            .ToList();
        reason = missing.Count == 0
            ? ""
            : string.Create(CultureInfo.InvariantCulture, $"not reviewed and rules not in the ISCN {edition} KB: {string.Join(", ", missing)} (parse-only)");
        return missing.Count == 0;
    }

    /// <summary>The rule IDs of one edition in the embedded KB.</summary>
    public static IReadOnlySet<string> KbRules(string edition) =>
        KbCatalog.Default.Rules.Where(r => r.Edition == edition).Select(r => r.Id).ToHashSet(StringComparer.Ordinal);

    private static string? Scalar(YamlMappingNode map, string key) =>
        map.Children.TryGetValue(new YamlScalarNode(key), out var node) ? ((YamlScalarNode)node).Value : null;

    private static ImmutableArray<string> Strings(YamlMappingNode map, string key) =>
        map.Children.TryGetValue(new YamlScalarNode(key), out var node)
            ? [.. ((YamlSequenceNode)node).Children.Select(c => ((YamlScalarNode)c).Value!)]
            : [];
}
