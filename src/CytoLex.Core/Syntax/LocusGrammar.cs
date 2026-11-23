using System.Collections.Frozen;
using System.Collections.Immutable;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;
using static CytoLex.Core.Syntax.Parser;

namespace CytoLex.Core.Syntax;

/// <summary>What a parenthesis group in a FISH result holds (decided by looking ahead to its closing parenthesis).</summary>
internal enum GroupContent
{
    /// <summary>Not at a <c>(</c>.</summary>
    None,

    /// <summary>Loci (<c>(D22S75–)</c>, <c>(wcp2+)</c>, <c>(41×2)</c>, subtelomeric bands with status <c>(q34–,p13+)</c>).</summary>
    Loci,

    /// <summary>Breakpoints with loci written inside the same parentheses (<c>(q11.2q11.2D22S75–)</c>).</summary>
    Mixed,

    /// <summary>Bands only (<c>(q11.2q11.2)</c>, <c>(p or q)</c>).</summary>
    Breakpoints,

    /// <summary>Anything else (a chromosome list such as <c>(14;21)</c>, <c>(22q11.2)</c>).</summary>
    Other,
}

/// <summary>
/// Locus groups of the FISH grammars (ISCN 2016 §13.2–13.4, T-033): loci in parentheses separated by <c>,</c>, contig
/// <c>/</c>, <c>;</c> between chromosomes or <c>con</c>/<c>sep</c> between signals, each with its status (<c>+</c>, <c>–</c>,
/// <c>++</c>), signal count (<c>×2</c>, <c>×4~5</c>, <c>×12~&gt;50</c>) and qualifiers (<c>amp</c>, <c>dim</c>, <c>enh</c>).
/// </summary>
/// <remarks>
/// <para>
/// Probe names (<c>D22S75</c>, <c>RP11-1143I12</c>, <c>5′CBFB</c>, <c>wcp11</c>, <c>pcp18p11.2</c>) are not listed
/// symbols: the lexer splits them into pieces, which <see cref="ReadProbe"/> joins into one <see cref="TokenKind.Probe"/>
/// token. A name is a run of adjacent pieces (no whitespace between); a typed hyphen followed by a letter or digit stays
/// inside the name (<c>RP11-453F20</c>), the printed dash is the status (<c>D22S75–</c>). A lower-case <c>x</c> ending the
/// name before a closing number is the typed multiplication sign (<c>ATMx1</c> = <c>ATM×1</c>, SYM.TYPED_SYMBOL_FORMS).
/// </para>
/// <para>
/// Permissive like the other grammars: the order of status and count, spacing, separators and the like stay in the tree
/// for the ISH/NUC_ISH rules; the grammar reports only text it cannot place.
/// </para>
/// </remarks>
internal sealed class LocusGrammar(TokenCursor cursor) : GrammarBase(cursor), IAbnormalityLoci
{
    /// <summary>Words that end a probe name: qualifiers, signal positions and <c>or</c>.</summary>
    internal static readonly FrozenSet<string> QualifierWords = Set("amp", "dim", "enh");

    /// <summary>Words between the loci of a signal position group (§13.3.2).</summary>
    internal static readonly FrozenSet<string> PositionWords = Set("con", "sep");

    private static readonly FrozenSet<string> _probeStopWords = Set("amp", "dim", "enh", "con", "sep", "or");

    /// <summary>Listed words that start a probe name: whole and partial chromosome paints.</summary>
    private static readonly FrozenSet<string> _probeWords = Set("wcp", "pcp");

    // ── IAbnormalityLoci ─────────────────────────────────────────────────────────────────────────────

    public bool AtLocusGroup() => Classify() is GroupContent.Loci or GroupContent.Mixed
        || (Classify() == GroupContent.Breakpoints && Classify(GroupEnd(0) + 1) is GroupContent.Loci);

    public bool ParseLoci(List<SyntaxElement> children, KaryotypeGrammar karyotype)
    {
        var any = false;
        while (At(TokenKind.OpenParen))
        {
            switch (Classify())
            {
                case GroupContent.Loci:
                    children.Add(ParseGroup());
                    any = true;
                    continue;
                case GroupContent.Mixed:
                    children.Add(ParseMixedGroup(karyotype));
                    any = true;
                    continue;
                case GroupContent.Breakpoints when any || Classify(GroupEnd(0) + 1) is GroupContent.Loci:
                    // inv(16)(p13.1)(5′CBFB+)(q22)(3′CBFB+): breakpoints and loci alternate (§13.2 E29).
                    children.Add(karyotype.ParseBreakpointList());
                    continue;
                default:
                    return any;
            }
        }

        return any;
    }

    // ── Classification ───────────────────────────────────────────────────────────────────────────────

    /// <summary>The offset of the <c>)</c> that closes the group opening at offset <paramref name="start"/> (or of the end).</summary>
    private int GroupEnd(int start)
    {
        var depth = 0;
        for (var k = start + 1; ; k++)
        {
            var t = Peek(k);
            if (t.Kind == TokenKind.EndOfSegment || (t.Kind == TokenKind.CloseParen && depth == 0))
            {
                return k;
            }

            depth += t.Kind == TokenKind.OpenParen ? 1 : t.Kind == TokenKind.CloseParen ? -1 : 0;
        }
    }

    /// <summary>What the parenthesis group at offset <paramref name="start"/> (default: the current token) holds.</summary>
    public GroupContent Classify(int start = 0)
    {
        if (Peek(start).Kind != TokenKind.OpenParen)
        {
            return GroupContent.None;
        }

        var inside = new List<int>();
        var depth = 0;
        for (var k = start + 1; ; k++)
        {
            var t = Peek(k);
            if (t.Kind == TokenKind.EndOfSegment)
            {
                break;
            }

            if (t.Kind == TokenKind.OpenParen)
            {
                depth++;
            }
            else if (t.Kind == TokenKind.CloseParen)
            {
                if (depth == 0)
                {
                    break;
                }

                depth--;
            }

            inside.Add(k);
        }

        if (inside.Count == 0)
        {
            return GroupContent.Other;
        }

        var first = Peek(inside[0]);
        var startsBand = IsWord(first, ArmWords) || KaryotypeGrammar.IsArmSymbolTypo(first);
        var hasProbe = inside.Any(StartsProbe);
        var hasMark = inside.Select(Peek).Any(t => t.Kind is TokenKind.Plus or TokenKind.Multiply
            || IsWord(t, QualifierWords) || IsWord(t, PositionWords));
        if (startsBand)
        {
            return hasProbe ? GroupContent.Mixed : hasMark ? GroupContent.Loci : GroupContent.Breakpoints;
        }

        return hasProbe || hasMark ? GroupContent.Loci : GroupContent.Other;
    }

    /// <summary>The token at offset <paramref name="k"/> starts a probe name.</summary>
    private bool StartsProbe(int k)
    {
        var t = Peek(k);
        return t.Kind switch
        {
            TokenKind.UnknownWord => !KaryotypeGrammar.IsArmSymbolTypo(t),
            // wcp11, pcp18p11.2; a listed word glued to a number (RB1: "RB" is a banding code) other than an arm (q11).
            TokenKind.Word => _probeWords.Contains(t.Text)
                || (!t.HasTrailingTrivia && Peek(k + 1).Kind is TokenKind.Number && !IsWord(t, ArmWords) && !_probeStopWords.Contains(t.Text)),
            TokenKind.Number => IsPrime(Peek(k + 1)) && !t.HasTrailingTrivia,
            _ => false,
        };
    }

    private static bool IsPrime(SyntaxToken t) => t.Kind == TokenKind.BadCharacter && t.Text is "′" or "'";

    // ── Groups ───────────────────────────────────────────────────────────────────────────────────────

    /// <summary><c>(locus, locus/locus; locus con locus)</c>.</summary>
    public SyntaxNode ParseGroup()
    {
        var children = new List<SyntaxElement> { Eat() };
        ParseGroupContent(children);
        return Node(SyntaxKind.LocusGroup, children);
    }

    /// <summary>
    /// <c>(q11.2q11.2D22S75–)</c>: breakpoints and loci in one pair of parentheses. Reported (ISH.STRUCTURAL_FORMAT) with a
    /// fix that closes the breakpoints and opens the loci.
    /// </summary>
    private SyntaxNode ParseMixedGroup(KaryotypeGrammar karyotype)
    {
        var children = new List<SyntaxElement> { Eat() };
        while (IsWord(Current, ArmWords) || KaryotypeGrammar.IsArmSymbolTypo(Current))
        {
            children.Add(karyotype.ParseBand(null));
        }

        Report(IshStructuralFormat, Current.Span, SyntaxDiagnosticKind.Unexpected,
            "The breakpoints and the loci go in separate parentheses.", Insert(Current.Span.Start, ")("));
        ParseGroupContent(children);
        return Node(SyntaxKind.LocusGroup, children);
    }

    private void ParseGroupContent(List<SyntaxElement> children)
    {
        while (true)
        {
            if (At(TokenKind.CloseParen))
            {
                children.Add(Eat());
                return;
            }

            if (AtEnd || At(TokenKind.OpenBracket) || At(TokenKind.OpenParen))
            {
                children.Add(MissingReported(TokenKind.CloseParen, ")", CoreCodes.Unparsed, "'(' needs a closing ')'."));
                return;
            }

            children.Add(ParseLocus());
            if (At(TokenKind.Comma) || At(TokenKind.Semicolon) || At(TokenKind.Slash) || IsWord(Current, PositionWords))
            {
                children.Add(Eat());
            }
            else if (!At(TokenKind.CloseParen) && !AtLocusStart && !AtEnd && !At(TokenKind.OpenBracket) && !At(TokenKind.OpenParen))
            {
                children.Add(Skip(t => t.Kind is TokenKind.Comma or TokenKind.Semicolon or TokenKind.Slash or TokenKind.CloseParen or TokenKind.OpenBracket,
                    CoreCodes.Unparsed, "Text not understood in the locus list."));
                if (At(TokenKind.Comma) || At(TokenKind.Semicolon) || At(TokenKind.Slash))
                {
                    children.Add(Eat());
                }
            }
        }
    }

    /// <summary>A locus starts here: a probe name, a number (<c>subtel(41×2)</c>), a band (<c>q34–</c>) or a sign before a name.</summary>
    private bool AtLocusStart =>
        StartsProbe(0) || At(TokenKind.Number) || At(TokenKind.ChromosomeLetter) || IsWord(Current, ArmWords)
        || At(TokenKind.Plus) || At(TokenKind.Minus) || (At(TokenKind.Word) && !_probeStopWords.Contains(Current.Text));

    /// <summary>One locus: <c>[signs]name[status][×n][ amp|dim|enh]…</c>.</summary>
    private SyntaxNode ParseLocus()
    {
        var children = new List<SyntaxElement>();
        if (At(TokenKind.Plus) || At(TokenKind.Minus))
        {
            var signs = new List<SyntaxElement>();
            while (At(TokenKind.Plus) || At(TokenKind.Minus))
            {
                signs.Add(Eat());
            }

            children.Add(Node(SyntaxKind.Signs, signs));
        }

        if (AtLocusStart && !At(TokenKind.Plus) && !At(TokenKind.Minus))
        {
            children.AddRange(ReadProbe());
        }
        else
        {
            children.Add(MissingReported(TokenKind.Probe, "", CoreCodes.Unparsed, "A locus is expected here."));
        }

        while (true)
        {
            if (At(TokenKind.Plus) || At(TokenKind.Minus))
            {
                children.Add(Eat());
                continue;
            }

            if (At(TokenKind.Multiply))
            {
                children.Add(ParseCount());
                continue;
            }

            if (IsWord(Current, QualifierWords))
            {
                children.Add(Eat());
                continue;
            }

            break;
        }

        return Node(SyntaxKind.Locus, children);
    }

    /// <summary><c>×2</c>, <c>×0</c>, <c>×4~5</c>, <c>×12~&gt;50</c>.</summary>
    public SyntaxNode ParseCount()
    {
        var children = new List<SyntaxElement> { Eat() };
        children.Add(At(TokenKind.Number) ? Eat() : MissingReported(TokenKind.Number, "1", CoreCodes.Unparsed, "A number follows the multiplication sign."));
        if (At(TokenKind.Tilde) && Adjacent)
        {
            children.Add(Eat());
            if (At(TokenKind.CloseAngle) && Adjacent)
            {
                children.Add(Eat());
            }

            children.Add(At(TokenKind.Number) ? Eat() : MissingReported(TokenKind.Number, "1", CoreCodes.Unparsed, "A range ends with a number."));
        }

        return Node(SyntaxKind.LocusCount, children);
    }

    /// <summary>
    /// The probe name at the current position as one <see cref="TokenKind.Probe"/> token, and a count read from a typed
    /// <c>x</c> glued to the name (<c>ATMx1</c>).
    /// </summary>
    private IEnumerable<SyntaxElement> ReadProbe()
    {
        var pieces = new List<SyntaxToken> { Eat() };
        while (!pieces[^1].HasTrailingTrivia && ContinuesProbe(Current, Peek(1)))
        {
            pieces.Add(Eat());
        }

        // ATMx1: the lexer read "ATMx" as one word; its x is the typed multiplication sign before a closing number.
        if (pieces.Count >= 2 && pieces[^1].Kind == TokenKind.Number && pieces[^2] is { Kind: TokenKind.UnknownWord or TokenKind.Word } word
            && word.Text.Length > 1 && word.WrittenText.EndsWith('x') && !At(TokenKind.Multiply))
        {
            var number = pieces[^1];
            var x = new TextSpan(word.Span.End - 1, 1);
            var nameEnd = new TextSpan(word.Span.Start, word.Span.Length - 1);
            var name = Join([.. pieces.Take(pieces.Count - 2)], nameEnd, ImmutableArray<Trivia>.Empty, word.Leading);
            var times = new SyntaxToken(TokenKind.Multiply, x, "x", [], [], isTypedForm: true);
            var count = Node(SyntaxKind.LocusCount, times, number);
            return [name, count];
        }

        return [Join(pieces, null, pieces[^1].Trailing, pieces[0].Leading)];
    }

    /// <summary>One probe token over <paramref name="pieces"/> (and <paramref name="extra"/>, the start of a split piece).</summary>
    private SyntaxToken Join(List<SyntaxToken> pieces, TextSpan? extra, ImmutableArray<Trivia> trailing, ImmutableArray<Trivia> leadingIfEmpty)
    {
        var start = pieces.Count > 0 ? pieces[0].Span.Start : extra!.Value.Start;
        var end = extra?.End ?? pieces[^1].Span.End;
        var leading = pieces.Count > 0 ? pieces[0].Leading : leadingIfEmpty;
        var span = TextSpan.FromBounds(start, end);
        return new SyntaxToken(TokenKind.Probe, span, TextOf(span), leading, trailing);
    }

    private static bool ContinuesProbe(SyntaxToken t, SyntaxToken next) => t.Kind switch
    {
        TokenKind.Number or TokenKind.UnknownWord or TokenKind.ChromosomeLetter => true,
        TokenKind.Word => !_probeStopWords.Contains(t.Text),
        TokenKind.BadCharacter => IsPrime(t),
        TokenKind.Dot => next.Kind == TokenKind.Number && !t.HasTrailingTrivia,
        TokenKind.Minus => t is { IsTypedForm: true, Text: "-", HasTrailingTrivia: false }
            && next.Kind is TokenKind.Number or TokenKind.UnknownWord or TokenKind.Word or TokenKind.ChromosomeLetter,
        _ => false,
    };

    /// <summary>The cells of a result: <c>[20]</c>, <c>[100/400]</c> (kept as written; the rules read them).</summary>
    public SyntaxNode ParseCellCount()
    {
        var children = new List<SyntaxElement> { Eat() };
        while (!At(TokenKind.CloseBracket) && !AtEnd && !At(TokenKind.OpenBracket))
        {
            children.Add(Eat());
        }

        children.Add(At(TokenKind.CloseBracket) ? Eat() : MissingReported(TokenKind.CloseBracket, "]", CoreCodes.Unparsed, "'[' needs a closing ']'."));
        return Node(SyntaxKind.FishCellCount, children);
    }
}
