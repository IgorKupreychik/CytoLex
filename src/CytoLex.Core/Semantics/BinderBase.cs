using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using CytoLex.Core.Composing;
using CytoLex.Core.Kb;
using CytoLex.Core.Syntax;
using CytoLex.Core.Text;
using static CytoLex.Core.Semantics.Binder;

namespace CytoLex.Core.Semantics;

/// <summary>
/// The edition KB, the syntax tree and the helpers every binder uses (T-083): issues and spans of syntax elements, KB refs,
/// chromosomes, bands and landmarks (shared by the karyotype, arr and FISH binders).
/// </summary>
internal abstract class BinderBase(IEditionKb kb, SyntaxTree tree)
{
    protected IEditionKb Kb { get; } = kb;

    protected SyntaxTree Tree { get; } = tree;

    /// <summary>The syntax diagnostics inside <paramref name="elements"/>: their missing tokens, or text overlapping them.</summary>
    protected ImmutableArray<SyntaxDiagnostic> IssuesOf(params SyntaxElement?[] elements)
    {
        if (Tree.Diagnostics.IsEmpty)
        {
            return [];
        }

        var present = elements.OfType<SyntaxElement>().ToList();
        var tokens = present.SelectMany(e => e.Tokens()).ToList();
        var missing = tokens.Where(t => t.IsMissing).Select(t => t.Span).ToHashSet();
        var spans = present.Select(e => e.Span).Where(s => s.Length > 0).ToList();
        return
        [
            .. Tree.Diagnostics.Where(d => d.Kind == SyntaxDiagnosticKind.Missing && d.Span.Length == 0
                ? missing.Contains(d.Span)
                : spans.Any(s => Overlaps(s, d.Span))),
        ];
    }

    protected static bool Overlaps(TextSpan a, TextSpan b) =>
        b.Length == 0 ? a.Start <= b.Start && b.Start <= a.End : a.Start < b.End && b.Start < a.End;

    protected static TextSpan SpanOf(params SyntaxElement?[] elements)
    {
        var present = elements.OfType<SyntaxElement>().Select(e => e.Span).ToList();
        return present.Count == 0 ? default : TextSpan.FromBounds(present.Min(s => s.Start), present.Max(s => s.End));
    }

    /// <summary>Refs to the rules with <paramref name="ids"/> that the edition's KB has.</summary>
    protected IEnumerable<KbRef> Rules(params IEnumerable<string> ids) =>
        ids.Where(id => Kb.Rule(id) is not null).Select(KbRef.Rule);

    protected static IEnumerable<KbRef> VocabOf(params IEnumerable<SyntaxToken?> tokens) =>
        tokens.OfType<SyntaxToken>().Where(t => t.VocabRef is not null).Select(t => KbRef.Parse(t.VocabRef!));

    protected static ImmutableArray<KbRef> Refs(params IEnumerable<KbRef>[] parts) => [.. parts.SelectMany(p => p).Distinct()];

    protected static int? Int(SyntaxToken? t) =>
        t is { IsMissing: false, Kind: TokenKind.Number } && int.TryParse(t.ValueText, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : null;

    protected static bool IsWord(SyntaxElement e, string text) => e is SyntaxToken { Kind: TokenKind.Word } t && t.Text == text;

    protected UnboundItem Unbound(SyntaxElement element) => new()
    {
        Span = element.Span,
        Syntax = element,
        Text = Tree.Input.Substring(element.Span.Start, element.Span.Length),
        Issues = IssuesOf(element),
    };

    /// <summary>The spans of the underlined tokens bound as chromosome identifiers so far (T-042), for <see cref="SegmentModel.UnderlinedChromosomes"/>.</summary>
    internal HashSet<TextSpan> UnderlinedChromosomeSpans { get; } = [];

    protected ChromosomeRef Chromosome(SyntaxToken token, SyntaxToken? questionMark = null, string? arm = null, SyntaxNode? node = null)
    {
        if (token.IsUnderlined)
        {
            UnderlinedChromosomeSpans.Add(token.Span);
        }

        var name = token.Kind == TokenKind.Question ? "?" : token.ValueText;
        return new ChromosomeRef
        {
            Span = node?.Span ?? token.Span,
            Syntax = (SyntaxElement?)node ?? token,
            Name = name,
            Number = Int(token),
            IsQuestioned = questionMark is not null,
            IsUnderlined = token.IsUnderlined,
            Arm = arm,
            Issues = node is null ? IssuesOf(token) : IssuesOf(node),
        };
    }

    /// <summary>A band: <c>[chromosome]arm[digits[.digits]]</c> with <c>?</c> and <c>~</c>.</summary>
    protected Breakpoint BindBand(SyntaxNode band, ChromosomeRef? implied)
    {
        var tokens = band.Children.OfType<SyntaxToken>().ToList();
        var i = 0;
        ChromosomeRef? chromosome = implied;
        var written = false;
        if (tokens[0].Kind is TokenKind.Number or TokenKind.ChromosomeLetter or TokenKind.Question)
        {
            chromosome = Chromosome(tokens[0]);
            written = true;
            i = 1;
        }

        var arm = tokens[i];
        var tilde = tokens.FindIndex(i, t => t.Kind == TokenKind.Tilde);
        var main = tokens.Skip(i).Take((tilde < 0 ? tokens.Count : tilde) - i).ToList();
        var text = Text(main);
        var questioned = main.Any(t => t.Kind == TokenKind.Question);
        var digits = Text(main.Skip(1));
        var bandNumber = questioned || digits.Length == 0 ? null : digits.Replace(',', '.');

        Breakpoint? rangeEnd = null;
        if (tilde >= 0 && tilde + 1 < tokens.Count)
        {
            var endTokens = tokens.Skip(tilde + 1).ToList();
            var endDigits = Text(endTokens);
            rangeEnd = Resolve(new Breakpoint
            {
                Span = SpanOf([.. endTokens]),
                Syntax = endTokens[0],
                Chromosome = chromosome,
                ChromosomeWritten = written,
                Text = arm.Text + endDigits,
                Arm = arm.Text,
                BandNumber = endDigits.Contains('?', StringComparison.Ordinal) ? null : endDigits,
                IsQuestioned = endDigits.Contains('?', StringComparison.Ordinal),
                Issues = IssuesOf([.. endTokens]),
            });
        }

        return Resolve(new Breakpoint
        {
            Span = band.Span,
            Syntax = band,
            Chromosome = chromosome,
            ChromosomeWritten = written,
            Text = tilde < 0 ? text : Text(tokens.Skip(i)),
            Arm = arm.Text,
            BandNumber = bandNumber,
            IsQuestioned = questioned,
            RangeEnd = rangeEnd,
            Issues = IssuesOf(band),
            Kb = Refs(VocabOf(arm), questioned ? Rules(QuestionMarkDesignation) : [], rangeEnd is not null ? Rules(ApproxInterval) : []),
        });
    }

    /// <summary><c>pter</c>, <c>3qter</c>, <c>cen</c>, <c>?cen</c>, <c>hsr</c>, <c>neo</c>, <c>?</c>.</summary>
    protected Breakpoint BindLandmark(SyntaxNode landmark, ChromosomeRef? implied)
    {
        var tokens = landmark.Children.OfType<SyntaxToken>().ToList();
        var word = tokens[^1];
        var chromosome = tokens.Count > 1 ? Chromosome(tokens[0]) : implied;
        var name = word.Kind == TokenKind.Question ? "?" : word.Text;
        return new Breakpoint
        {
            Span = landmark.Span,
            Syntax = landmark,
            Chromosome = chromosome,
            ChromosomeWritten = tokens.Count > 1,
            Text = name,
            Landmark = name,
            IsQuestioned = name == "?",
            Issues = IssuesOf(landmark),
            Kb = Refs(VocabOf(word)),
        };
    }

    /// <summary>Adds the band set's entry (and its <c>band:</c> ref) when the full name is a band of the edition.</summary>
    protected Breakpoint Resolve(Breakpoint bp)
    {
        if (bp.Name is not { } name || Kb.Bands?.Find(name) is not { } band)
        {
            return bp;
        }

        return bp with { KbBand = band, Kb = Refs(bp.Kb, [KbRef.Band(band.Name)]) };
    }

    protected static string Text(IEnumerable<SyntaxToken> tokens)
    {
        var sb = new StringBuilder();
        foreach (var t in tokens.Where(t => !t.IsMissing))
        {
            sb.Append(t.ValueText);
        }

        return sb.ToString();
    }
}
