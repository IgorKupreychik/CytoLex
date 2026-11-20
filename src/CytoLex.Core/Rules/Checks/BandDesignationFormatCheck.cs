using System.Collections.Immutable;
using CytoLex.Core.Syntax;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// <c>BAND.DESIGNATION_FORMAT</c> (§2.3.2 ¶2): chromosome, arm, region and band are written together. Reports whitespace
/// inside a band (<c>1p 31</c>, <c>t(1;3)(p 32;q21)</c>) and between a written chromosome and its band (<c>1 p31</c>).
/// Whitespace between two bands (<c>(q13 q33)</c>) is not this rule's concern.
/// </summary>
[KbRule("BAND.DESIGNATION_FORMAT")]
internal sealed class BandDesignationFormatCheck : IRuleCheck
{
    public void Check(RuleContext context)
    {
        var withChromosome = new HashSet<SyntaxNode>();
        foreach (var region in context.Syntax.Root.DescendantNodesAndSelf().Where(n => n.Kind == SyntaxKind.ChromosomeRegion))
        {
            // A region's chromosome token and the band right after it form one designation (1p31).
            for (var i = 0; i + 1 < region.Children.Length; i++)
            {
                if (region.Children[i] is SyntaxToken { Kind: TokenKind.Number or TokenKind.ChromosomeLetter } chromosome
                    && region.Children[i + 1] is SyntaxNode { Kind: SyntaxKind.Band } band)
                {
                    withChromosome.Add(band);
                    CheckTokens(context, [chromosome, .. band.PresentTokens()]);
                }
            }
        }

        foreach (var band in context.Syntax.Root.DescendantNodesAndSelf().Where(n => n.Kind == SyntaxKind.Band && !withChromosome.Contains(n)))
        {
            CheckTokens(context, band.PresentTokens());
        }
    }

    /// <summary>Reports the designation when whitespace follows any of its tokens but the last.</summary>
    private static void CheckTokens(RuleContext context, List<SyntaxToken> tokens)
    {
        if (tokens.Count > 1 && tokens.Take(tokens.Count - 1).Any(t => t.HasTrailingTrivia))
        {
            var span = TextSpan.FromBounds(tokens[0].Span.Start, tokens[^1].Span.End);
            var gaps = tokens.Zip(tokens.Skip(1))
                .Where(p => p.First.HasTrailingTrivia)
                .Select(p => new TextEdit(TextSpan.FromBounds(p.First.Span.End, p.Second.Span.Start), string.Empty))
                .ToImmutableArray();
            context.Report(span, $"Space inside the band designation '{span.Of(context.Input)}'.", fixes: [new FixProposal(gaps)]);
        }
    }
}
