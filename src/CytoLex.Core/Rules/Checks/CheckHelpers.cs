using System.Globalization;
using CytoLex.Core.Semantics;
using CytoLex.Core.Syntax;
using CytoLex.Core.Text;

namespace CytoLex.Core.Rules.Checks;

/// <summary>Model and token queries shared by the rule checks.</summary>
internal static class CheckHelpers
{
    /// <summary>Every breakpoint and band of the model (interval ends and <c>or</c> readings included), in input order.</summary>
    public static IEnumerable<Breakpoint> Breakpoints(this MeaningNode model) =>
        model.DescendantsAndSelf().OfType<Breakpoint>();

    /// <summary>
    /// Every chromosome named in the model: chromosome lists, gained/lost chromosomes, regions, and chromosomes written with
    /// a band (<c>3q13</c>); each once (by span).
    /// </summary>
    public static IEnumerable<ChromosomeRef> Chromosomes(this MeaningNode model) =>
        model.DescendantsAndSelf()
            .OfType<ChromosomeRef>()
            .Concat(model.Breakpoints().Where(b => b.ChromosomeWritten).Select(b => b.Chromosome).OfType<ChromosomeRef>())
            .DistinctBy(c => c.Span);

    /// <summary>Whether <paramref name="chromosome"/> is <c>1</c>–<c>22</c>, <c>X</c>, <c>Y</c> or unknown (<c>?</c>) (§2.2.1, <c>CHROM.IDENTIFIERS</c>).</summary>
    public static bool IsValidIdentifier(this ChromosomeRef chromosome) =>
        chromosome.IsUnknown
        || chromosome.IsSexChromosome
        || (chromosome.Number is >= 1 and <= 22 && chromosome.Name == chromosome.Number.Value.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// The span of a band as the reader sees it: with its chromosome when that is written directly before it (<c>9q35</c>
    /// as a region item), else the band alone (<c>q35</c> in <c>del(9)(q35)</c>).
    /// </summary>
    public static TextSpan BandSpan(this Breakpoint bp) =>
        bp.Chromosome is { } c && c.Span.End == bp.Span.Start ? TextSpan.FromBounds(c.Span.Start, bp.Span.End) : bp.Span;

    /// <summary>The tokens of <paramref name="element"/> that are in the input (no missing tokens).</summary>
    public static List<SyntaxToken> PresentTokens(this SyntaxElement element) =>
        [.. element.Tokens().Where(t => !t.IsMissing)];

    /// <summary>
    /// Where an in situ result written after a karyotype without its period starts (<c>46,XY ish 22q11.2(D22S75×2)</c>,
    /// ISH.COMBINED_WITH_KARYOTYPE, T-033): the karyotype parser keeps it as skipped text, which the spacing and letter-case
    /// checks leave alone (it is in situ notation, not karyotype); <c>null</c> when there is none.
    /// </summary>
    public static int? MisplacedInSituStart(this SyntaxNode root) =>
        root.Children.OfType<SyntaxNode>().LastOrDefault(n => n.Kind == SyntaxKind.Skipped
            && n.Children[0] is SyntaxToken { Kind: TokenKind.Word } w && FishGrammar.KeywordWords.Contains(w.Text))?.Span.Start;
}
