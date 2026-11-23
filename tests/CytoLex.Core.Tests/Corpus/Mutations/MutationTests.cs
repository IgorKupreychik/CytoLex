using System.Text;
using CytoLex.Core.Segmentation;
using CytoLex.Core.Tests.KbExamples;

namespace CytoLex.Core.Tests.Corpus.Mutations;

/// <summary>
/// The mutation generator (T-062): each operator is grounded in a book pair of its rule, behaves deterministically and
/// only where it applies, and every mutant of a trusted valid corpus entry makes its rule fire.
/// </summary>
public class MutationTests
{
    public static TheoryData<string> MutationIds => [.. MutationCatalog.All.Select(m => m.Id)];

    public static IEnumerable<TheoryDataRow<string>> MutantKeys() =>
        MutationGenerator.Mutants.Select(m => new TheoryDataRow<string>(m.Key) { TestDisplayName = $"{m.Key} [{m.RuleId}]: {m.Text}" });

    private static Mutation Get(string id) => MutationCatalog.All.Single(m => m.Id == id);

    [Fact]
    public void Ids_AreUnique()
    {
        Assert.Equal(MutationCatalog.All.Count, MutationCatalog.All.Select(m => m.Id).Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>The pair is in the KB: an invalid example of the operator's rule, whose corrected form (if any) is the source.</summary>
    [Theory]
    [MemberData(nameof(MutationIds))]
    public void Grounding_IsABookExampleOfTheRule(string id)
    {
        var m = Get(id);
        var book = m.Grounding.BookExample ?? m.Grounding.Invalid;
        var example = KbCatalog.Default.Cases.FirstOrDefault(c =>
            c.Rule.Edition == MutationGenerator.Edition && c.Rule.Id == m.RuleId && c.Kind == ExampleKind.Invalid && c.Text == book);
        Assert.True(example is not null, $"'{book}' is not an invalid KB example of {m.RuleId}.");

        var corrected = KbCatalog.Default.Cases.FirstOrDefault(c =>
            c.Key == KbCatalog.MakeKey(MutationGenerator.Edition, m.RuleId, ExampleKind.Corrected, example.Index));
        if (m.Grounding.BookExample is null && corrected is not null)
        {
            Assert.Equal(corrected.Corrected, m.Grounding.Source);
        }
    }

    [Theory]
    [MemberData(nameof(MutationIds))]
    public void Grounding_IsReproduced(string id)
    {
        var m = Get(id);
        Assert.Equal(m.Grounding.Invalid, MutationGenerator.Apply(m, m.Grounding.Source));
    }

    [Theory]
    [MemberData(nameof(MutationIds))]
    public void Grounding_SourceIsValid_AndMutantFiresTheRule(string id)
    {
        var m = Get(id);
        var source = CorpusExpectations.Run(MutationGenerator.Edition, m.Grounding.Source, m.RequiredContext);
        Assert.True(source.Validity.IsValid, $"'{m.Grounding.Source}': {CorpusExpectations.Describe(source)}");
        var mutant = CorpusExpectations.Run(MutationGenerator.Edition, m.Grounding.Invalid, m.RequiredContext);
        Assert.Contains(mutant.Diagnostics, d => d.RuleId == m.RuleId);
    }

    [Theory]
    [MemberData(nameof(MutationIds))]
    public void Operator_AppliesToTheCorpus(string id)
    {
        Assert.Contains(MutationGenerator.Mutants, x => x.MutationId == id);
    }

    [Theory]
    [InlineData("swap-chromosomes", "46,XX,inv(2)(p21q31)")]
    [InlineData("swap-chromosomes", "46,XY,t(9;9)(p13;q22)")]
    [InlineData("chromosome-list-comma", "47,XX,+21")]
    [InlineData("nonexistent-band", "47,XX,+21")]
    [InlineData("count-plus-one", "92,XXYY")]
    [InlineData("count-plus-one", "45~48,XY,+8[cp10]")]
    [InlineData("swap-items", "47,XY,+21,+21")]
    [InlineData("swap-items", "47,XXY")]
    [InlineData("drop-comma-before-sign", "47,XY,+8,+21")]
    [InlineData("sign-after", "45,XX,-7")]
    [InlineData("drop-breakpoint-parentheses", "46,XY,t(9;22)(q34;q11.2)")]
    [InlineData("space-after-comma", "46,XY")]
    [InlineData("space-before-parenthesis", "47,XY,+21")]
    [InlineData("lower-case-sex", "46,XX")]
    [InlineData("capitalized-symbol", "46,XY,t(9;22)(q34;q11.2)")]
    [InlineData("drop-cell-count", "46,XY,t(8;21)(q22;q22)[20]")]
    public void Operator_DoesNotApply(string id, string text)
    {
        Assert.Null(MutationGenerator.Apply(Get(id), text));
    }

    [Theory]
    [InlineData("swap-chromosomes", "46,XY,del(5)(q13q33),t(9;22)(q34;q11.2)", "46,XY,del(5)(q13q33),t(22;9)(q11.2;q34)")]
    [InlineData("nonexistent-band", "46,XX,t(1;3)(p36.1;q21)", "46,XX,t(1;3)(p37;q21)")]
    [InlineData("count-plus-one", "45,XX,-7[10]/46,XX[10]", "46,XX,-7[10]/46,XX[10]")]
    [InlineData("swap-items", "48,XY,+8,+21,t(9;22)(q34;q11.2)", "48,XY,+21,+8,t(9;22)(q34;q11.2)")]
    [InlineData("space-before-parenthesis", "46,XX,t(1;3)(p36.1;q21)", "46,XX,t (1;3)(p36.1;q21)")]
    [InlineData("drop-cell-count", "45,XX,-7[5]/46,XX[15]", "45,XX,-7[5]/46,XX")]
    public void Operator_FirstSite(string id, string text, string expected)
    {
        Assert.Equal(expected, MutationGenerator.Apply(Get(id), text));
    }

    [Fact]
    public void Generation_IsDeterministic()
    {
        var again = MutationGenerator.Generate(CorpusData.Entries);
        Assert.Equal(MutationGenerator.Mutants, again);
        Assert.Equal(MutationGenerator.Mutants.Count, MutationGenerator.Mutants.Select(m => m.Key).Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [MemberData(nameof(MutantKeys))]
    public void Mutant_FiresItsRule(string key)
    {
        var m = MutationGenerator.Find(key);
        var result = CorpusExpectations.Run(MutationGenerator.Edition, m.Text, m.Context);
        var passes = result.Diagnostics.Any(d => d.RuleId == m.RuleId) && !result.Validity.IsValid;
        PendingCorpus.SkipOrFailIfFixed(key, passes);
        Assert.True(passes, $"{key}: '{m.Text}' (hint {m.Context?.ToString() ?? "none"}): expected {m.RuleId} and not valid; got {CorpusExpectations.Describe(result)}.");
    }

    /// <summary>
    /// Opt-in (env <c>CYTOLEX_CORPUS_EXPORT=1</c>): the corpus and its mutants as a batch file
    /// (<c>build/review/corpus/corpus-2016.csv</c>; columns <c>id,iscn,type,expected,fires,source</c>) for the desktop
    /// Batch tab and the CytoGPS comparison.
    /// </summary>
    [Fact]
    public void WriteCorpusExport()
    {
        const string RelativePath = "build/review/corpus/corpus-2016.csv";
        if (Environment.GetEnvironmentVariable("CYTOLEX_CORPUS_EXPORT") != "1")
        {
            Assert.Skip("opt-in: set CYTOLEX_CORPUS_EXPORT=1 to write " + RelativePath);
        }

        var sb = new StringBuilder("id,iscn,type,expected,fires,source\n");
        foreach (var e in CorpusData.Entries)
        {
            var x = e.Expected.GetValueOrDefault(MutationGenerator.Edition);
            Row(sb, e.Id, e.Iscn, string.Join('+', e.Types), x?.Verdict.ToString().ToLowerInvariant() ?? "", string.Join(' ', x?.Fires ?? []), e.Source);
        }

        foreach (var m in MutationGenerator.Mutants)
        {
            var type = string.Join('+', Segmenter.Split(m.Text).Select(s => s.Type.ToKbName()));
            Row(sb, m.Key, m.Text, type, "invalid", m.RuleId, "mutation");
        }

        var path = Path.Combine(RuleCoverageReport.RepoRoot(), RelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    private static void Row(StringBuilder sb, params string[] fields) =>
        sb.Append(string.Join(',', fields.Select(Quote))).Append('\n');

    private static string Quote(string f) =>
        f.Contains(',', StringComparison.Ordinal) || f.Contains('"', StringComparison.Ordinal)
            ? $"\"{f.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : f;
}
