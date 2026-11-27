using CytoLex.Core.Context;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Tests.Translation;

/// <summary>
/// T-057: the translation golden file stays consistent with the parser and binder, so the renderer (T-058) can be
/// tested against it: every input binds to karyotype and in situ segments (T-093), its items are exactly the translated items of the
/// model (same spans, same order), and each item's KB refs are refs of that part of the model.
/// </summary>
public class GoldenFileTests
{
    public static TheoryData<string> Inputs2016 => [.. GoldenFile.Entries2016.Keys];

    [Fact]
    public void GoldenFile_HasEntries()
    {
        Assert.NotEmpty(GoldenFile.Entries2016);
    }

    [Theory]
    [MemberData(nameof(Inputs2016))]
    public void Entry_MatchesTheModel(string input)
    {
        var entry = GoldenFile.Entries2016[input];
        var models = GoldenFile.Models(input, entry.IsFragment);
        Assert.True(models is not null, $"'{input}' has a segment that is neither a karyotype nor in situ.");
        foreach (var model in models)
        {
            Assert.False(model.IsPartial, $"'{input}' has syntax errors: {string.Join("; ", model.Issues.Select(i => i.Message))}");
        }

        var expected = GoldenFile.Items(models).ToList();
        var expectedSpans = expected.Select(e => $"{e.Span.Start}:{input.Substring(e.Span.Start, e.Span.Length)}");
        var actualSpans = entry.Items.Select(i => $"{i.Start}:{i.Text}");
        Assert.Equal(expectedSpans, actualSpans);

        foreach (var (item, (_, nodes)) in entry.Items.Zip(expected))
        {
            var refs = GoldenFile.Refs(nodes);
            var unknown = item.Kb.Where(k => !refs.Contains(k)).ToList();
            Assert.True(unknown.Count == 0, $"'{item.Text}': KB refs not in the model: {string.Join(", ", unknown)}");
        }
    }

    /// <summary>Non-book entries must be valid ISCN 2016 in their context (book examples are checked by the KB-example harness).</summary>
    [Theory]
    [MemberData(nameof(Inputs2016))]
    public void SyntheticEntry_IsValid(string input)
    {
        var entry = GoldenFile.Entries2016[input];
        if (!entry.IsSynthetic)
        {
            return;
        }

        var report = Validator.Default.Validate(input, new ValidationOptions
        {
            Editions = EditionSelection.One("2016"),
            ContextHint = entry.Context switch
            {
                "constitutional" => SampleContext.Constitutional,
                "neoplastic" => SampleContext.Acquired,
                _ => null,
            },
        });
        var result = report.Results.Single();
        Assert.True(
            result.Validity.IsValid,
            $"'{input}' is not valid: {string.Join("; ", result.Diagnostics.Select(d => $"{d.RuleId}: {d.Message}"))}");
    }
}
