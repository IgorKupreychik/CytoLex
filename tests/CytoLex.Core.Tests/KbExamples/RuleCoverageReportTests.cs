using CytoLex.Core.Rules;

namespace CytoLex.Core.Tests.KbExamples;

public class RuleCoverageReportTests
{
    [Fact]
    public void Report_IsWrittenToBuildReview()
    {
        var path = RuleCoverageReport.Write(KbCatalog.Default, RuleImplementationScan.Default);

        Assert.True(File.Exists(path));
        Assert.Contains("# Rule coverage", File.ReadAllText(path), StringComparison.Ordinal);
    }

    [Fact]
    public void EveryKbRuleAttribute_NamesAKbRule()
    {
        // core.md §7.2 startup check: an implementation bound to an ID the KB doesn't have is a bug.
        Assert.Empty(RuleCoverageReport.OrphanRuleIds(KbCatalog.Default, RuleImplementationScan.Default));
    }

    [Fact]
    public void Report_ListsImplementationsAndGaps()
    {
        // The test assembly holds one fake implementation (below) and one orphan.
        var scan = new RuleImplementationScan(typeof(RuleCoverageReportTests).Assembly);
        var report = RuleCoverageReport.Build(KbCatalog.Default, scan);

        Assert.Contains("| `CHROM.IDENTIFIERS` |", report, StringComparison.Ordinal);
        Assert.Contains("✔ FakeChromIdentifiersCheck", report, StringComparison.Ordinal);
        Assert.Contains("- `STRUCT.CHROMOSOME_ORDER` (", report, StringComparison.Ordinal); // no implementation
        Assert.DoesNotContain("- `CHROM.IDENTIFIERS` (", report, StringComparison.Ordinal);
        Assert.Contains("- `CHROM.GROUPS` (", report, StringComparison.Ordinal);              // no examples
        Assert.Contains("- `TEST.NOT_IN_KB`", report, StringComparison.Ordinal);              // orphan
        Assert.Equal(["TEST.NOT_IN_KB"], RuleCoverageReport.OrphanRuleIds(KbCatalog.Default, scan));
    }

    [Fact]
    public void Report_IsDeterministic() =>
        Assert.Equal(
            RuleCoverageReport.Build(KbCatalog.Default, RuleImplementationScan.Default),
            RuleCoverageReport.Build(KbCatalog.Default, RuleImplementationScan.Default));

    [Theory]
    [InlineData(null, "2016", true)]
    [InlineData("2016", "2016", true)]
    [InlineData("2016,2020", "2020", true)]
    [InlineData("2020+", "2016", false)]
    [InlineData("2020+", "2024", true)]
    [InlineData("2020", "2016", false)]
    public void KbRuleAttribute_EditionFilter(string? editions, string edition, bool expected) =>
        Assert.Equal(expected, new KbRuleAttribute("X.Y") { Editions = editions }.AppliesTo(edition));

    [KbRule("CHROM.IDENTIFIERS")]
    private sealed class FakeChromIdentifiersCheck;

    [KbRule("TEST.NOT_IN_KB", Editions = "2016")]
    private sealed class FakeOrphanCheck;
}
