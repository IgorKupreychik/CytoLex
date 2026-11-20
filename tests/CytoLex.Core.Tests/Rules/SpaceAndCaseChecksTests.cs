using System.Collections.Immutable;
using CytoLex.Core.Syntax;
using CytoLex.Core.Tests.KbExamples;
using CytoLex.Core.Tests.Parsing;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;
using Severity = CytoLex.Core.Validation.Severity;

namespace CytoLex.Core.Tests.Rules;

/// <summary>T-074: SYM.NO_OTHER_SPACE (stray spaces) and SYM.LETTER_CASE (letter case), with their level-1 fixes.</summary>
public class SpaceAndCaseChecksTests
{
    internal const string NoOtherSpace = "SYM.NO_OTHER_SPACE";
    internal const string LetterCase = "SYM.LETTER_CASE";

    private static ImmutableArray<Diagnostic> Diagnostics(string input, bool fragment = false) =>
        Validator.Default.Validate(input, new ValidationOptions { Editions = EditionSelection.One("2016"), IsFragment = fragment })
            .Results.Single().Diagnostics;

    // --- SYM.NO_OTHER_SPACE ---

    [Theory]
    [InlineData("47,XY, +21", "47,XY,+21")]
    [InlineData("46,X Y,dup(1)(q21q32)", "46,XY,dup(1)(q21q32)")]
    [InlineData("46 ,XX", "46,XX")]
    [InlineData("47,XX,+ 21", "47,XX,+21")]
    [InlineData("46,XY,del(5)(q13 q33)", "46,XY,del(5)(q13q33)")]
    [InlineData("47,XX,+21/ 46,XX", "47,XX,+21/46,XX")]
    [InlineData("46,XY,t(9;22)(q34; q11.2)", "46,XY,t(9;22)(q34;q11.2)")]
    [InlineData("47,XXY c,t(9;22)(q34;q11.2)[20]", "47,XXYc,t(9;22)(q34;q11.2)[20]")] // KARYO.CONSTITUTIONAL_C has no check yet
    [InlineData("arr[GRCh38] 18p11.32q23(102328_79093443)×3, 21q11.2(1_2)×1", "arr[GRCh38] 18p11.32q23(102328_79093443)×3,21q11.2(1_2)×1")]
    public void StraySpace_IsReported_WithDeleteFix(string input, string preview)
    {
        var d = Assert.Single(Diagnostics(input), d => d.RuleId == NoOtherSpace);

        Assert.Equal(Severity.Error, d.Severity);
        Assert.True(string.IsNullOrWhiteSpace(d.Span.Of(input)));
        var fix = Assert.Single(d.Fixes);
        Assert.Equal(FixKind.Delete, fix.Kind);
        Assert.Equal(preview, fix.Preview);
    }

    [Fact]
    public void EachSpaceRun_IsOneFinding()
    {
        const string input = "47,XY,  +21, del(5)(q13q33)";
        var found = Diagnostics(input).Where(d => d.RuleId == NoOtherSpace).Select(d => d.Span.Of(input)).ToList();
        Assert.Equal(["  ", " "], found);
    }

    [Theory]
    [InlineData("45,XX,psu dic(15;13)(q12;q12)")] // SYM.SPACE_BETWEEN_SYMBOLS
    [InlineData("mos 47,XY,+21/46,XY")] // SYM.SPACE_BEFORE_CHROMOSOME_COUNT
    [InlineData("chi 46,XX/46,XY")]
    [InlineData("47,XY,+mar c")] // MAR.CONSTITUTIONAL_C_SPACE
    [InlineData("47,XY,+mar dn[14]/46,XY[16]")] // KARYO.DE_NOVO_SPACE
    [InlineData("47,XX,+dic r(1;3)(p36.1q32;p24q26.2)")]
    [InlineData("46,XY,del(8)(q21.1) or i(8)(p10)")] // or
    [InlineData("46,XX,add(19)(p13 or q13)")]
    [InlineData("arr[GRCh38] 11p12(37741458_39209912)×2 hmz")] // build space, zygosity
    [InlineData("arr[GRCh38] 4q32.2q35.1(163146681_183022312)×1 dn")] // inheritance
    [InlineData(" 46,XX ")] // segment edges
    public void PrescribedSpace_IsNotReported(string input) =>
        Assert.DoesNotContain(Diagnostics(input), d => d.RuleId == NoOtherSpace);

    [Theory]
    [InlineData("46,XX,del (5)(q13q33)", "SYM.NO_SPACE_AT_PARENTHESIS", false)]
    [InlineData("arr(X,Y) ×1", "SYM.NO_SPACE_AT_PARENTHESIS", false)]
    [InlineData("1p 31", "BAND.DESIGNATION_FORMAT", true)]
    [InlineData("46,XX,t(1;3)(p 32;q21)", "BAND.DESIGNATION_FORMAT", false)]
    [InlineData("arr 6q21q25.1(113900000_149100000)×1", "GEN.GENOME_BUILD_REQUIRED", false)] // the build goes in the space
    [InlineData("arr[GRCh38] 4q32.2q35.1(163002425×2,163146681_183022312×1,184322231×2) dn", "ARR.INHERITANCE_SPACING", false)]
    public void SpaceOfAnotherRule_IsNotReportedTwice(string input, string ownRule, bool fragment)
    {
        var diagnostics = Diagnostics(input, fragment);
        Assert.Contains(diagnostics, d => d.RuleId == ownRule);
        Assert.DoesNotContain(diagnostics, d => d.RuleId == NoOtherSpace);
    }

    [Fact]
    public void Parser_ReadsAcrossTheSpace()
    {
        var tree = ParseHelper.Parse("46,X Y,dup(1)(q21q32)").Single();
        var sex = Assert.Single(tree.Root.DescendantNodesAndSelf(), n => n.Kind == SyntaxKind.SexChromosomes);
        Assert.Equal(["X", "Y"], sex.Tokens().Select(t => t.Text));
        Assert.Empty(tree.Diagnostics);
    }

    // --- SYM.LETTER_CASE ---

    [Theory]
    [InlineData("46,xy", "xy", "46,XY")]
    [InlineData("46,xx", "xx", "46,XX")]
    [InlineData("46,Xy", "y", "46,XY")] // only the wrong letter
    [InlineData("mos 45,x/46,XX", "x", "mos 45,X/46,XX")] // typed x alone in the sex field
    [InlineData("47,XXy", "y", "47,XXY")]
    [InlineData("46,XX,Del(5)(q13q33)", "Del", "46,XX,del(5)(q13q33)")]
    [InlineData("46,XX,DEL(5)(q13q33)", "DEL", "46,XX,del(5)(q13q33)")]
    [InlineData("46,XY,T(9;22)(q34;q11.2)", "T", "46,XY,t(9;22)(q34;q11.2)")]
    [InlineData("46,XX,INV(2)(p13q21)", "INV", "46,XX,inv(2)(p13q21)")]
    [InlineData("46,XX,I(17)(q10)", "I", "46,XX,i(17)(q10)")]
    [InlineData("MOS 47,XY,+21/46,XY", "MOS", "mos 47,XY,+21/46,XY")]
    public void WrongCase_IsReported_WithReplaceFix(string input, string spanText, string preview)
    {
        var diagnostics = Diagnostics(input);
        var d = Assert.Single(diagnostics, d => d.RuleId == LetterCase);

        Assert.Equal(spanText, d.Span.Of(input));
        Assert.Equal(Severity.Error, d.Severity);
        var fix = Assert.Single(d.Fixes);
        Assert.Equal(FixKind.Replace, fix.Kind);
        Assert.Equal(preview, fix.Preview);
        Assert.DoesNotContain(diagnostics, d => d.RuleId == "SYM.LISTED_ONLY");
        // No cascade: apart from the case, the same findings as the string in the listed case.
        Assert.Equal(
            Diagnostics(preview).Select(d => d.RuleId).Order(),
            diagnostics.Where(d => d.RuleId != LetterCase).Select(d => d.RuleId).Order());
    }

    [Theory]
    [InlineData("46,XX,dl(5)(q13q33)", "SYM.LISTED_ONLY")] // no listed token in any case
    [InlineData("1P31", "BAND.ARM_SYMBOL")] // arm letters stay BAND.ARM_SYMBOL
    [InlineData("46,XX,t(1;3)(P32;Q21)", "BAND.ARM_SYMBOL")]
    public void OtherForms_KeepTheirRule(string input, string ruleId)
    {
        var diagnostics = Diagnostics(input, fragment: !input.Contains(',', StringComparison.Ordinal));
        Assert.Contains(diagnostics, d => d.RuleId == ruleId);
        Assert.DoesNotContain(diagnostics, d => d.RuleId == LetterCase);
    }

    [Fact]
    public void TypedMultiplicationSign_IsNotASexChromosome()
    {
        var diagnostics = Diagnostics("46,XX,dmin x2");
        Assert.DoesNotContain(diagnostics, d => d.RuleId == LetterCase);
    }

    [Fact]
    public void FoldedToken_KeepsTheWrittenText_AndTheTreeStaysLossless()
    {
        const string input = "46,xy,Del(5)(q13q33)";
        var tree = ParseHelper.Parse(input).Single();
        var folded = tree.Root.Tokens().Where(t => t.IsCaseFolded).ToList();

        Assert.Equal(["X", "Y", "del"], folded.Select(t => t.Text));
        Assert.Equal(["x", "y", "Del"], folded.Select(t => t.WrittenText));
        Assert.Equal(input, tree.ToFullString());
    }

    // --- valid KB examples ---

    [Fact]
    public void NoValidKbExample_GetsEitherRule()
    {
        var hits = KbCatalog.Default.Cases
            .Where(c => c.Kind == ExampleKind.Valid)
            .SelectMany(c => Diagnostics(c.Text, c.IsFragment).Where(d => d.RuleId is NoOtherSpace or LetterCase).Select(d => $"{c.DisplayName}: {d.RuleId} '{d.Span.Of(c.Text)}'"))
            .ToList();
        Assert.Empty(hits);
    }
}
