using System.Collections.Frozen;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;
using static CytoLex.Core.Syntax.Parser;

namespace CytoLex.Core.Syntax;

/// <summary>
/// The microarray grammar (ISCN 2016 ch. 14, T-034):
/// <c>arr[build] aberration(,aberration)*</c>, where an aberration is a chromosome group <c>(1–22,X)</c>, a region with
/// positions <c>6q21q25.1(113900000_149100000)</c>, or regions grouped in parentheses, followed by the copy number
/// (<c>×1</c>, <c>×2~4</c>, <c>amp</c>, <c>cx</c>, <c>cth</c>; per position in the detailed system), an optional
/// proportion <c>[0.6]</c> and qualifiers (<c>mos</c>, <c>htz</c>/<c>hmz</c>, <c>mat</c>/<c>pat</c>/<c>inh</c>/<c>dn</c>, <c>c</c>).
/// </summary>
/// <remarks>
/// Permissive like the karyotype grammar: what the ARR rules judge stays in the tree for the checks. That covers a hyphen as the span
/// joiner, thousands separators, a hyphen between bands, a hyphen in a copy-number range, a proportion before the copy
/// number, qualifier order and spacing. The parser reports only what breaks the surface form: unclosed brackets,
/// unlisted words, text it cannot place.
/// </remarks>
internal sealed class ArrGrammar(TokenCursor cursor, KaryotypeGrammar karyotype) : GrammarBase(cursor)
{
    private bool AtChromosome => At(TokenKind.Number) || At(TokenKind.ChromosomeLetter);

    /// <summary>
    /// A chromosome with its arm after it: the start of a region (<c>6q21</c>, <c>Xp22.33</c>; a space between them is
    /// BAND.DESIGNATION_FORMAT's).
    /// </summary>
    private bool AtRegionStart => AtChromosome && (IsWord(Peek(1), ArmWords) || KaryotypeGrammar.IsArmSymbolTypo(Peek(1)));

    private bool AtArrItemEnd => At(TokenKind.Comma) || AtEnd || AtTrailingKaryotype;

    /// <summary>
    /// A period followed by a number after a result: a karyotype written after the array result (<c>…×3 dn.47,XY,+mar</c>).
    /// The segmenter splits only before a letter (§3.1), so it stays in the arr segment.
    /// </summary>
    private bool AtTrailingKaryotype => At(TokenKind.Dot) && Peek(1).Kind == TokenKind.Number;

    public SyntaxNode ParseArrRoot()
    {
        var children = new List<SyntaxElement> { Eat() }; // the arr keyword (the segment type says it is there)
        if (At(TokenKind.OpenBracket))
        {
            children.Add(ParseGenomeBuild());
        }

        if (AtEnd)
        {
            Report(ArrNormalResult, new TextSpan(Current.Span.Start, 0), SyntaxDiagnosticKind.Missing, "An array result follows arr: the chromosomes or regions and their copy number.");
        }

        while (!AtEnd)
        {
            children.Add(ParseArrAberration());
            if (At(TokenKind.Comma))
            {
                children.Add(Eat());
                continue;
            }

            if (AtTrailingKaryotype)
            {
                children.Add(ParseTrailingKaryotype(children[0].Span.Start));
                break;
            }

            if (!AtEnd)
            {
                children.Add(Skip(t => t.Kind == TokenKind.Comma, CoreCodes.Unparsed, "Text not understood in the array result."));
                if (At(TokenKind.Comma))
                {
                    children.Add(Eat());
                }
            }
        }

        children.Add(Eat()); // EndOfSegment
        return Node(SyntaxKind.ArrResult, children);
    }

    /// <summary>
    /// <c>.47,XY,+mar</c> after the array result: the karyotype comes first (ARR.COMBINED_WITH_OTHER_METHODS). Kept as
    /// skipped text, with a fix that moves it before <c>arr</c>; it is not validated here.
    /// </summary>
    private SyntaxNode ParseTrailingKaryotype(int segmentStart)
    {
        var dot = Current;
        var skipped = new List<SyntaxElement>();
        while (!AtEnd)
        {
            skipped.Add(Eat());
        }

        var node = Node(SyntaxKind.Skipped, skipped);
        var karyotype = TextOf(TextSpan.FromBounds(skipped[1].Span.Start, node.Span.End)); // after the period (a number, see AtTrailingKaryotype)
        Report(
            ArrCombinedWithOtherMethods,
            node.Span,
            SyntaxDiagnosticKind.Unexpected,
            "The karyotype comes before the array result.",
            Insert(segmentStart, karyotype + "."),
            Delete(TextSpan.FromBounds(dot.Span.Start, node.Span.End)));
        return node;
    }

    /// <summary><c>[GRCh38]</c>: kept as written; unlisted words in it are reported.</summary>
    private SyntaxNode ParseGenomeBuild()
    {
        var children = new List<SyntaxElement> { Eat() };
        while (!At(TokenKind.CloseBracket) && !AtEnd && !At(TokenKind.OpenParen) && !At(TokenKind.Comma))
        {
            if (At(TokenKind.UnknownWord))
            {
                Report(ListedOnly, Current.Span, SyntaxDiagnosticKind.Unexpected, $"'{Current.Text}' is not a listed symbol or abbreviation.");
            }

            children.Add(Eat());
        }

        children.Add(At(TokenKind.CloseBracket)
            ? Eat()
            : MissingReported(TokenKind.CloseBracket, "]", GenomeBuildRequired, "The genome build is closed by a square bracket."));
        return Node(SyntaxKind.GenomeBuild, children);
    }

    private SyntaxNode ParseArrAberration()
    {
        var children = new List<SyntaxElement>();
        if (At(TokenKind.OpenParen))
        {
            children.Add(LooksLikeRegionGroup() ? ParseArrRegionGroup() : ParseArrChromosomeGroup());
        }
        else if (AtRegionStart)
        {
            children.Add(ParseArrRegion());
        }
        else
        {
            children.Add(Skip(t => t.Kind is TokenKind.Comma or TokenKind.Multiply or TokenKind.OpenBracket, CoreCodes.Unparsed, "An array result starts with a chromosome or region."));
        }

        ParseArrResultParts(children);
        if (!AtArrItemEnd)
        {
            children.Add(Skip(t => t.Kind == TokenKind.Comma, CoreCodes.Unparsed, "Text not understood after the copy number."));
        }

        return Node(SyntaxKind.ArrAberration, children);
    }

    /// <summary><c>(15q11.2q26.3(</c>: a region (chromosome, arm, band digits) right after the parenthesis.</summary>
    private bool LooksLikeRegionGroup() =>
        Peek(1).Kind is TokenKind.Number or TokenKind.ChromosomeLetter && !Peek(1).HasTrailingTrivia
        && IsWord(Peek(2), ArmWords) && !Peek(2).HasTrailingTrivia && Peek(3).Kind == TokenKind.Number;

    /// <summary><c>(1–22,X)</c>, <c>(16q)</c>, <c>(8,9p)</c>: chromosomes, arms and ranges separated by commas.</summary>
    private SyntaxNode ParseArrChromosomeGroup()
    {
        var children = new List<SyntaxElement> { Eat() };
        while (true)
        {
            if (AtChromosome || At(TokenKind.Question))
            {
                var spec = new List<SyntaxElement> { Eat() };
                if (Adjacent && IsWord(Current, ArmWords))
                {
                    spec.Add(Eat());
                }

                if (At(TokenKind.Minus) && Peek(1).Kind is TokenKind.Number or TokenKind.ChromosomeLetter)
                {
                    spec.Add(Eat());
                    spec.Add(Eat());
                }

                children.Add(Node(SyntaxKind.ChromosomeSpec, spec));
                continue;
            }

            if (At(TokenKind.Comma))
            {
                children.Add(Eat());
                continue;
            }

            if (At(TokenKind.CloseParen))
            {
                children.Add(Eat());
                break;
            }

            if (AtEnd || At(TokenKind.Multiply) || At(TokenKind.OpenBracket))
            {
                children.Add(MissingReported(TokenKind.CloseParen, ")", ParenthesesPaired, "The chromosomes are closed by a parenthesis."));
                break;
            }

            children.Add(Skip(_ => true, CoreCodes.Unparsed, "A chromosome, an arm or a range is expected in the parentheses."));
        }

        return Node(SyntaxKind.ArrChromosomeGroup, children);
    }

    /// <summary><c>(15q11.2q26.3(…),21q11.21q22.3(…))</c>: regions with positions sharing one result (§14.2.1 E5).</summary>
    private SyntaxNode ParseArrRegionGroup()
    {
        var children = new List<SyntaxElement> { Eat() };
        while (true)
        {
            if (AtRegionStart)
            {
                children.Add(ParseArrRegion());
                continue;
            }

            if (At(TokenKind.Comma))
            {
                children.Add(Eat());
                continue;
            }

            if (At(TokenKind.CloseParen))
            {
                children.Add(Eat());
                break;
            }

            if (AtEnd || At(TokenKind.Multiply) || At(TokenKind.OpenBracket))
            {
                children.Add(MissingReported(TokenKind.CloseParen, ")", ParenthesesPaired, "The grouped regions are closed by a parenthesis."));
                break;
            }

            children.Add(Skip(_ => true, CoreCodes.Unparsed, "A region with its positions is expected in the parentheses."));
        }

        return Node(SyntaxKind.ArrRegionGroup, children);
    }

    /// <summary>
    /// <c>6q21q25.1(…)</c>, <c>Xq25(…)</c>, <c>13q14.2qter(…)</c>, <c>Xq28 or Yq12(…)</c>: bands (or a landmark) after the
    /// chromosome, as written (a hyphen between them and further bands are kept for ARR.REGION_FORMAT), then the positions.
    /// </summary>
    private SyntaxNode ParseArrRegion()
    {
        var children = new List<SyntaxElement> { karyotype.ParseBand(Eat()) };
        while (true)
        {
            if (Adjacent && (IsWord(Current, ArmWords) || KaryotypeGrammar.IsArmSymbolTypo(Current)))
            {
                children.Add(karyotype.ParseBand(null));
                continue;
            }

            if (Adjacent && IsWord(Current, ArrLandmarkWords))
            {
                children.Add(Node(SyntaxKind.Landmark, Eat()));
                continue;
            }

            if (At(TokenKind.Minus) && IsWord(Peek(1), ArmWords))
            {
                children.Add(Eat());
                continue;
            }

            if (AtWord("or") && Peek(1).Kind is TokenKind.Number or TokenKind.ChromosomeLetter && IsWord(Peek(2), ArmWords))
            {
                children.Add(Eat());
                children.Add(karyotype.ParseBand(Eat()));
                continue;
            }

            break;
        }

        if (At(TokenKind.OpenParen))
        {
            children.Add(ParsePositions());
        }

        return Node(SyntaxKind.ArrRegion, children);
    }

    /// <summary>
    /// <c>(113900000_149100000)</c>, <c>(163002425×2,163146681_183022312×1,184322231×2)</c>, <c>(a_b or c_d)</c>. A
    /// comma inside a number (<c>163,146,681</c>) is kept in its <see cref="SyntaxKind.PositionSpan"/> for ARR.NUCLEOTIDE_FORMAT.
    /// </summary>
    private SyntaxNode ParsePositions()
    {
        var children = new List<SyntaxElement> { Eat() };
        while (true)
        {
            if (At(TokenKind.Number))
            {
                children.Add(ParsePositionSpan());
                continue;
            }

            if (At(TokenKind.Comma) || AtWord("or"))
            {
                children.Add(Eat());
                continue;
            }

            if (At(TokenKind.CloseParen))
            {
                children.Add(Eat());
                break;
            }

            if (AtEnd || At(TokenKind.OpenBracket))
            {
                children.Add(MissingReported(TokenKind.CloseParen, ")", ParenthesesPaired, "The nucleotide positions are closed by a parenthesis."));
                break;
            }

            children.Add(Skip(_ => true, CoreCodes.Unparsed, "A nucleotide position is expected in the parentheses."));
        }

        return Node(SyntaxKind.Positions, children);
    }

    private SyntaxNode ParsePositionSpan()
    {
        var children = new List<SyntaxElement>();
        ParsePositionNumber(children);
        if (At(TokenKind.Underscore) || (At(TokenKind.Minus) && Peek(1).Kind == TokenKind.Number))
        {
            children.Add(Eat());
            ParsePositionNumber(children);
        }

        if (At(TokenKind.Multiply) || AtTypedX)
        {
            children.Add(ParseCopyNumber());
        }

        return Node(SyntaxKind.PositionSpan, children);
    }

    /// <summary>A number, with thousands groups (<c>,146</c>: a comma directly followed by three digits) taken into it.</summary>
    private void ParsePositionNumber(List<SyntaxElement> children)
    {
        children.Add(Eat());
        while (At(TokenKind.Comma) && Adjacent && !Current.HasTrailingTrivia
            && Peek(1) is { Kind: TokenKind.Number, Text.Length: 3 } && Peek(2).Kind is not TokenKind.Multiply)
        {
            children.Add(Eat());
            children.Add(Eat());
        }
    }

    /// <summary>An upper-case X right after the result, followed by a number: a mistyped multiplication sign (SYM.TYPED_SYMBOL_FORMS).</summary>
    private bool AtTypedX => At(TokenKind.ChromosomeLetter) && Current.Text == "X" && Adjacent && !Current.HasTrailingTrivia && Peek(1).Kind == TokenKind.Number;

    /// <summary>The copy number, proportion and qualifiers after a chromosome group, a region or a region group.</summary>
    private void ParseArrResultParts(List<SyntaxElement> children)
    {
        while (true)
        {
            if (At(TokenKind.Multiply) || AtTypedX || IsWord(Current, ArrResultWords))
            {
                children.Add(ParseCopyNumber());
                continue;
            }

            if (At(TokenKind.OpenBracket))
            {
                children.Add(ParseProportion());
                continue;
            }

            if (IsWord(Current, ArrQualifierWords))
            {
                children.Add(Node(SyntaxKind.ArrQualifier, Eat()));
                continue;
            }

            if (At(TokenKind.UnknownWord))
            {
                Report(ListedOnly, Current.Span, SyntaxDiagnosticKind.Unexpected, $"'{Current.Text}' is not a listed symbol or abbreviation.");
                children.Add(Node(SyntaxKind.Skipped, Eat()));
                continue;
            }

            break;
        }
    }

    /// <summary><c>×1</c>, <c>×2~4</c> (and <c>×2-4</c>, kept for ARR.COPY_NUMBER), <c>amp</c>, <c>cx</c>, <c>cth</c>.</summary>
    private SyntaxNode ParseCopyNumber()
    {
        if (At(TokenKind.Word))
        {
            return Node(SyntaxKind.CopyNumber, Eat());
        }

        var children = new List<SyntaxElement>();
        if (AtTypedX)
        {
            var x = Eat();
            Report(TypedSymbolForms, x.Span, SyntaxDiagnosticKind.Unexpected, "Upper-case X is the sex chromosome; the multiplication sign is × (or x).", "×");
            children.Add(x);
        }
        else
        {
            children.Add(Eat());
        }

        if (!At(TokenKind.Number))
        {
            children.Add(MissingReported(TokenKind.Number, "1", CoreCodes.Unparsed, "A number follows the multiplication sign."));
            return Node(SyntaxKind.CopyNumber, children);
        }

        children.Add(Eat());
        if ((At(TokenKind.Tilde) || At(TokenKind.Minus)) && Adjacent && Peek(1).Kind == TokenKind.Number)
        {
            children.Add(Eat());
            children.Add(Eat());
        }

        return Node(SyntaxKind.CopyNumber, children);
    }

    /// <summary><c>[0.6]</c>: kept as written up to the closing bracket (its form is ARR.MOSAIC_PROPORTION's).</summary>
    private SyntaxNode ParseProportion()
    {
        var children = new List<SyntaxElement> { Eat() };
        while (!At(TokenKind.CloseBracket) && !At(TokenKind.Comma) && !AtEnd)
        {
            children.Add(Eat());
        }

        children.Add(At(TokenKind.CloseBracket)
            ? Eat()
            : MissingReported(TokenKind.CloseBracket, "]", ArrMosaicProportion, "The proportion of cells is closed by a square bracket."));
        return Node(SyntaxKind.Proportion, children);
    }
}
