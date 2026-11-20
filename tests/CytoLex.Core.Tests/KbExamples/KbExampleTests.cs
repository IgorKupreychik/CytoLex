namespace CytoLex.Core.Tests.KbExamples;

/// <summary>
/// One test per KB example (T-025): valid, invalid, and corrected forms from <c>build/kb.json</c>.
/// Invalid cases skip with a reason while their rule is not implemented (T-029+); corrected cases check fix previews (T-035).
/// </summary>
public class KbExampleTests
{
    public static IEnumerable<TheoryDataRow<string>> Cases() =>
        KbCatalog.Default.Cases.Select(c => new TheoryDataRow<string>(c.Key) { TestDisplayName = c.DisplayName });

    [Theory]
    [MemberData(nameof(Cases))]
    public void KbExample(string key)
    {
        var c = KbCatalog.Default.Find(key);
        var outcome = ExampleExpectations.Evaluate(
            c,
            ValidatorAdapter.Current,
            RuleImplementationScan.Default.IsImplemented(c.Rule.Edition, c.Rule.Id));

        if (outcome.SkipReason is { } reason)
        {
            Assert.Skip(reason);
        }

        Assert.True(outcome.Failures.Count == 0, $"{c.DisplayName}\n  - {string.Join("\n  - ", outcome.Failures)}");
    }
}
