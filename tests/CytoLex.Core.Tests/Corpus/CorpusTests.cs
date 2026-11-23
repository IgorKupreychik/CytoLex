using CytoLex.Core.Segmentation;
using CytoLex.Core.Tests.KbExamples;

namespace CytoLex.Core.Tests.Corpus;

/// <summary>
/// The test corpus (T-062): file integrity, then one test per entry × edition (<see cref="CorpusExpectations"/>).
/// Entries in <see cref="PendingCorpus"/> skip with their reason until they pass.
/// </summary>
public class CorpusTests
{
    private static readonly Dictionary<string, char> _prefixes = new(StringComparer.Ordinal)
    {
        ["karyotype.yaml"] = 'K',
        ["arr.yaml"] = 'A',
        ["ish.yaml"] = 'I',
        ["nuc_ish.yaml"] = 'N',
        ["combined.yaml"] = 'C',
        ["robustness.yaml"] = 'R',
    };

    public static IEnumerable<TheoryDataRow<string, string>> Cases() =>
        CorpusData.Entries.SelectMany(e => e.Expected.Keys.Order(StringComparer.Ordinal).Select(ed =>
            new TheoryDataRow<string, string>(e.Id, ed) { TestDisplayName = $"[{ed}] {e.Id}: {e.Iscn}" }));

    [Fact]
    public void Corpus_HasEveryFile()
    {
        var files = CorpusData.Entries.Select(e => e.File).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
        Assert.Equal(_prefixes.Keys.Order(StringComparer.Ordinal), files);
    }

    [Fact]
    public void Karyotype_HasAtLeast100Entries()
    {
        Assert.True(CorpusData.Entries.Count(e => e.File == "karyotype.yaml") >= 100);
    }

    [Fact]
    public void Ids_AreUniqueAndMatchTheirFile()
    {
        var duplicates = CorpusData.Entries.GroupBy(e => e.Id, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key);
        Assert.Empty(duplicates);
        Assert.All(CorpusData.Entries, e => Assert.Equal(_prefixes[e.File], e.Id[0]));
    }

    [Fact]
    public void Strings_AreUnique()
    {
        var duplicates = CorpusData.Entries.GroupBy(e => e.Iscn, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => string.Join(", ", g.Select(e => e.Id)));
        Assert.Empty(duplicates);
    }

    [Fact]
    public void Types_MatchSegmenter()
    {
        var wrong = CorpusData.Entries
            .Select(e => (e.Id, Declared: string.Join('+', e.Types), Actual: string.Join('+', Segmenter.Split(e.Iscn).Select(s => s.Type.ToKbName()))))
            .Where(x => x.Declared != x.Actual)
            .Select(x => $"{x.Id}: declared {x.Declared}, split as {x.Actual}");
        Assert.Empty(wrong);
    }

    [Fact]
    public void RuleIds_ExistInTheKb()
    {
        var unknown = new List<string>();
        foreach (var e in CorpusData.Entries)
        {
            foreach (var (edition, expected) in e.Expected)
            {
                var kb = CorpusData.KbRules(edition);
                if (kb.Count == 0)
                {
                    continue; // edition not in the KB yet (2020/2024): the label stays parse-only
                }

                unknown.AddRange(e.Rules.Concat(expected.Fires)
                    .Where(r => !r.StartsWith(CorpusData.CorePrefix, StringComparison.Ordinal) && !kb.Contains(r))
                    .Select(r => $"{e.Id} [{edition}]: {r}"));
            }
        }

        Assert.Empty(unknown);
    }

    [Fact]
    public void PendingEntries_Exist()
    {
        var ids = CorpusData.Entries.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        Assert.All(PendingCorpus.Table.Keys, k => Assert.Contains(k.Split('~')[0], ids));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Entry(string id, string edition)
    {
        var entry = CorpusData.Find(id);
        var outcome = CorpusExpectations.Evaluate(entry, edition, CorpusData.KbRules(edition));
        if (outcome.SkipReason is { } reason)
        {
            Assert.Skip(reason);
        }

        PendingCorpus.SkipOrFailIfFixed(id, outcome.Passed);
        Assert.True(outcome.Failures.Count == 0, $"[{edition}] {entry}\n  - {string.Join("\n  - ", outcome.Failures)}");
    }
}
