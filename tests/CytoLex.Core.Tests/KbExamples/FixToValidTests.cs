using CoreContext = CytoLex.Core.Context.SampleContext;

namespace CytoLex.Core.Tests.KbExamples;

/// <summary>
/// T-040 acceptance: applying the fixes of the KB's invalid examples yields valid strings (<see cref="FixToValid"/>).
/// Examples whose remaining errors have only an explanation are counted in <c>build/review/fix-coverage.md</c>, not failed.
/// Known gaps are listed in <see cref="Pending"/> with their task; an entry fails once its example passes.
/// </summary>
public class FixToValidTests
{
    /// <summary>Invalid examples whose fixes don't yet end valid: case key → reason (task).</summary>
    internal static readonly IReadOnlyDictionary<string, string> Pending =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public static IEnumerable<TheoryDataRow<string>> Cases() =>
        KbCatalog.Default.Cases.Where(c => c.Kind == ExampleKind.Invalid)
            .Select(c => new TheoryDataRow<string>(c.Key) { TestDisplayName = "fix → valid: " + c.DisplayName });

    [Theory]
    [MemberData(nameof(Cases))]
    public void InvalidExample_FixesToValid(string key)
    {
        var c = KbCatalog.Default.Find(key);
        var outcome = FixToValid.Run(c);
        if (outcome.Kind == FixToValidKind.Skipped)
        {
            Assert.Skip(outcome.Message!);
        }

        var failed = outcome.Kind == FixToValidKind.Failed;
        if (Pending.TryGetValue(key, out var reason))
        {
            Assert.True(failed, $"'{c.Text}' now passes: remove it from {nameof(FixToValidTests)}.{nameof(Pending)}.");
            Assert.Skip("pending - " + reason);
        }

        Assert.False(failed, $"{c.DisplayName}\n  → '{outcome.Text}' after {outcome.Rounds} round(s): {outcome.Message} (errors left: {string.Join(", ", outcome.Remaining)})");
    }

    /// <summary>Strings that are not in the book, each with several faults the checks fix with certainty.</summary>
    public static TheoryData<string, CoreContext?, string> NonBookStrings() => new()
    {
        // Missing comma after the count; chromosome order in t (breakpoints follow).
        { "46XX,t(7;3)(q22;p21)", null, "46,XX,t(3;7)(p21;q22)" },
        // Space before a parenthesis; chromosome order in t; then the order of abnormalities (t(2;5) before del(5)).
        { "46,XX,del (5)(q13q33),t(5;2)(q13;p11)", null, "46,XX,t(2;5)(p11;q13),del(5)(q13q33)" },
        // Arrow in the short system; chromosome order; abnormality order.
        { "46,XY,del(5)(q13→q33),t(5;2)(q13;p11)", null, "46,XY,t(2;5)(p11;q13),del(5)(q13q33)" },
        // Structural before numerical of a higher chromosome; chromosome order in t.
        { "47,XY,+21,t(9;4)(q34;q21)", null, "47,XY,t(4;9)(q21;q34),+21" },
        // Breakpoint order of an inversion (p before q).
        { "46,XY,inv(3)(q27p21)", null, "46,XY,inv(3)(p21q27)" },
        // Derivative: chromosome order of its translocation; abnormality order.
        { "46,XY,der(7)t(7;1)(q36;p36.3),del(4)(p16)", null, "46,XY,del(4)(p16),der(7)t(1;7)(p36.3;q36)" },
        // Loss of 7 written before del(5).
        { "45,XX,–7,del(5)(q13q33)", null, "45,XX,del(5)(q13q33),–7" },
        // Order and chromosome order inside the first clone of a mosaic with cell counts.
        { "47,XX,+21,t(11;8)(q23;q24)[10]/46,XX[10]", null, "47,XX,t(8;11)(q24;q23),+21[10]/46,XX[10]" },
        // Upper-case arm symbols.
        { "46,XX,t(1;3)(P32;Q21)", null, "46,XX,t(1;3)(p32;q21)" },
        // Abnormality order by chromosome number.
        { "46,XY,add(19)(p13.3),del(6)(q21)", null, "46,XY,del(6)(q21),add(19)(p13.3)" },
        // arr: aberration order (chromosome 1 first) and the space before the origin.
        { "arr[GRCh38] 7q11.23(73330452_74989354)×1dn,1p36.33(1_2000000)×3", null, "arr[GRCh38] 1p36.33(1_2000000)×3,7q11.23(73330452_74989354)×1 dn" },
        // Stray spaces (T-074): after a comma, inside the sex chromosome field.
        { "47,XY, +21", null, "47,XY,+21" },
        { "46,X Y,dup(1)(q21q32)", null, "46,XY,dup(1)(q21q32)" },
        // Letter case (T-074): sex chromosomes, a symbol.
        { "46,xy", null, "46,XY" },
        { "46,XY,T(9;22)(q34;q11.2)", null, "46,XY,t(9;22)(q34;q11.2)" },
        // Case, a stray space and the order of abnormalities together.
        { "47,xy, +21,Del(5)(q13q33)", null, "47,XY,del(5)(q13q33),+21" },
    };

    [Theory]
    [MemberData(nameof(NonBookStrings))]
    public void NonBookString_FixesToValid(string input, CoreContext? hint, string expected)
    {
        var outcome = FixToValid.Run("2016", input, hint);
        Assert.True(outcome.Kind == FixToValidKind.Fixed, $"'{input}' → '{outcome.Text}': {outcome.Kind} {outcome.Message} (errors left: {string.Join(", ", outcome.Remaining)})");
        Assert.Equal(expected, outcome.Text);
    }

    [Fact]
    public void Report_IsWrittenToBuildReview()
    {
        var path = FixToValid.WriteReport(KbCatalog.Default);
        var text = File.ReadAllText(path);
        Assert.StartsWith("# Fix coverage (T-040)", text, StringComparison.Ordinal);
        Assert.Contains("| Fixed to valid |", text, StringComparison.Ordinal);
    }
}
