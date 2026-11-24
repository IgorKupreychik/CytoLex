using System.Text.RegularExpressions;
using CytoLex.Core.Tests.Composition;
using CytoLex.Core.Translation;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Tests.Translation;

/// <summary>
/// T-086: the composition line of a translated part (<see cref="TranslationItem.Composition"/>): the band composition the
/// short system defines, in the words of the detailed system's sentences. Sentences themselves are unchanged (the golden
/// file pins them, <see cref="RendererGoldenTests"/>).
/// </summary>
public partial class CompositionLineTests
{
    /// <summary>
    /// Blocks of homologues or an unknown partner that the book prints without chromosome numbers, where another such
    /// block has them (§9.2.4 E1 dic(13;13)); <c>STRUCT.COMPOSITION_CHROMOSOME_NUMBERS</c> leaves both alone, and the line
    /// numbers them as <c>Composer</c> writes the detailed form. Compared without the numbers.
    /// </summary>
    private static readonly HashSet<string> _unnumberedInBook = ["9.2.1 p.60 E4", "9.2.4 p.66 E9"];

    private static TranslationResult Translate(string input) =>
        Validator.Default.Validate(input, new ValidationOptions { Editions = EditionSelection.One("2016"), IncludeTranslation = true })
            .Results.Single().Translation!;

    private static TranslationItem Item(string input, string text) => Translate(input).Items.Single(i => i.Text == text);

    [Theory]
    [InlineData(
        "46,XX,der(9)t(9;22)(q34;q11.2)",
        "der(9)t(9;22)(q34;q11.2)",
        "Band composition: the segment from the end of the short arm of chromosome 9 to 9q34, joined to the segment from 22q11.2 to the end of the long arm of chromosome 22.")]
    [InlineData(
        "46,XX,del(5)(q13q33)",
        "del(5)(q13q33)",
        "Band composition: the segment from the end of the short arm to 5q13, joined to the segment from 5q33 to the end of the long arm.")]
    [InlineData(
        "46,XX,t(2;5)(q21;q31)",
        "t(2;5)(q21;q31)",
        "Band compositions: chromosome 2 is made up of the segment from the end of its short arm to 2q21, joined to the segment from 5q31 to the end of the long arm of chromosome 5; chromosome 5 is made up of the segment from the end of its short arm to 5q31, joined to the segment from 2q21 to the end of the long arm of chromosome 2.")]
    public void ShortForm_HasTheLine(string input, string part, string expected) =>
        Assert.Equal(expected, Item(input, part).Composition);

    [Fact]
    public void Ring_IsARing() =>
        Assert.StartsWith("Band composition: a ring made up of the segment from 4p16 to 4q35", Item("46,XX,r(4)(p16q35)", "r(4)(p16q35)").Composition, StringComparison.Ordinal);

    [Theory]
    [InlineData("46,XX,del(5)(pter→q13::q33→qter)", "del(5)(pter→q13::q33→qter)")] // detailed: the sentence gives it
    [InlineData("46,XX,del(5)(q2?3)", "del(5)(q2?3)")] // uncertain breakpoint: no composition
    [InlineData("47,XX,+21", "+21")]
    [InlineData("47,XX,+mar", "+mar")]
    public void NoLine(string input, string part) => Assert.Null(Item(input, part).Composition);

    /// <summary>
    /// For every book block printed in both systems, the short form's line gives the same segments as the detailed
    /// form's sentence: one phrase path for both systems.
    /// </summary>
    [Theory]
    [MemberData(nameof(CompositionPairTests.Cases), MemberType = typeof(CompositionPairTests))]
    public void ShortFormLine_UsesTheSegmentsOfTheDetailedSentence(string cite)
    {
        var pair = CompositionPairTests.Pairs.Single(p => p.Cite == cite);
        if (PendingCompositions.Table.TryGetValue(cite, out var pending))
        {
            Assert.Skip(pending);
        }

        var sentences = pair.DetailedForms.Where(d => !KbExamples.KbCatalog.IsFragment(d)).SelectMany(d => Translate(d).Items).Select(i => i.Sentence).ToList();
        if (sentences.Count == 0)
        {
            return; // fragments only: no sentence to compare with
        }

        foreach (var shortForm in pair.ShortForms.Where(s => !KbExamples.KbCatalog.IsFragment(s)))
        {
            foreach (var line in Translate(shortForm).Items.Select(i => i.Composition).OfType<string>())
            {
                foreach (var segments in Segments(line))
                {
                    var relaxed = _unnumberedInBook.Contains(cite);
                    Assert.True(
                        sentences.Any(s => relaxed ? Unnumbered(s).Contains(Unnumbered(segments), StringComparison.Ordinal) : s.Contains(segments, StringComparison.Ordinal)),
                        $"{shortForm}: '{segments}' is in no detailed sentence of {cite}:\n{string.Join("\n", sentences)}");
                }
            }
        }
    }

    private static string Unnumbered(string text) => ChromosomeNumber().Replace(text, string.Empty);

    /// <summary>" of chromosome 5", the chromosome before a band (<c>5</c> in <c>5q13</c>), and "back" (said only of unnumbered arm ends).</summary>
    [GeneratedRegex(@" of chromosome (\d+|X|Y)|\b(\d+|X|Y)(?=[pq]\d)| back(?= to)")]
    private static partial Regex ChromosomeNumber();

    /// <summary>
    /// The piece phrases of a line (<c>the segment from … to …</c>), per product after its head. Pieces are compared one
    /// by one: a deletion's sentence joins them with its own words ("is joined to").
    /// </summary>
    private static IEnumerable<string> Segments(string line)
    {
        var body = line[(line.IndexOf(':', StringComparison.Ordinal) + 2)..].TrimEnd('.');
        foreach (var product in body.Split("; "))
        {
            var i = product.IndexOf(" is made up of ", StringComparison.Ordinal);
            var segments = i < 0 ? product : product[(i + " is made up of ".Length)..];
            segments = segments.StartsWith("a ring made up of ", StringComparison.Ordinal) ? segments["a ring made up of ".Length..] : segments;
            foreach (var piece in segments.Split(", joined to "))
            {
                yield return piece;
            }
        }
    }
}
