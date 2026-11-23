using System.Collections.Frozen;
using CytoLex.Core.Segmentation;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;
using static CytoLex.Core.Syntax.Parser;

namespace CytoLex.Core.Syntax;

/// <summary>
/// The in situ hybridization grammars (ISCN 2016 ch. 13, T-033): <c>ish</c>, <c>nuc ish</c>, <c>fib ish</c>, <c>rev ish</c>
/// and <c>ish cgh</c>/<c>cgh</c>. A result is <c>[//]keyword [mos|chi] item(,item)*[cells]</c> per cell line, cell lines
/// separated by <c>/</c> or <c>//</c>. An item is
/// <list type="bullet">
/// <item>ish: an abnormality with locus groups after its parentheses (<c>del(22)(q11.2q11.2)(D22S75–)</c>,
/// <c>ins(2)(wcp2+)</c>), or a chromosome or band with loci (<c>22q11.2(D22S75×2)</c>, <c>X(DXZ1×2)</c>);</item>
/// <item>nuc ish: a hybridization, short <c>(ABL1,BCR)×3(ABL1 con BCR×1)</c> or detailed <c>9q34(ABL1×2)</c>, with its cells;</item>
/// <item>fib ish: a band with its loci (<c>15q11.2(D15S11+,SNRPN+,GABRB3+)</c>);</item>
/// <item>rev ish: an abnormality or a region (<c>der(5)t(5;10)(p15;q22)</c>, <c>15q</c>);</item>
/// <item>cgh: abnormalities with <c>enh</c>/<c>dim</c> parts (<c>enh(1)(q32q43)</c>, <c>der(7)t(7;21)(q36;q22)enh(21)(q22)</c>).</item>
/// </list>
/// </summary>
/// <remarks>Permissive like the karyotype and arr grammars: the ISH, NUC_ISH, FIB_ISH, REV_ISH and CGH rules judge the form.</remarks>
internal sealed class FishGrammar : GrammarBase
{
    /// <summary>The words of the technique keywords (§13.1–13.6).</summary>
    internal static readonly FrozenSet<string> KeywordWords = Set("ish", "nuc", "fib", "rev", "cgh");

    private readonly NotationType _type;
    private readonly LocusGrammar _loci;
    private readonly KaryotypeGrammar _karyotype;

    public FishGrammar(TokenCursor cursor, NotationType type)
        : base(cursor)
    {
        _type = type;
        _loci = new LocusGrammar(cursor);
        _karyotype = new KaryotypeGrammar(cursor, _loci);
    }

    public SyntaxNode ParseRoot()
    {
        var children = new List<SyntaxElement>();
        var firstDiagnostic = Cursor.Diagnostics.Count;
        if (At(TokenKind.DoubleSlash))
        {
            children.Add(Node(SyntaxKind.CellLineSeparator, Eat())); // //nuc ish(…): donor cells only (§13.3.1 E19)
        }

        while (IsWord(Current, KeywordWords))
        {
            children.Add(Eat());
        }

        while (IsWord(Current, PrefixWords))
        {
            children.Add(Node(SyntaxKind.Prefix, Eat()));
        }

        while (true)
        {
            if (!At(TokenKind.Slash) && !At(TokenKind.DoubleSlash) && !AtEnd)
            {
                children.Add(ParseCellLine());
            }

            if (At(TokenKind.Slash) || At(TokenKind.DoubleSlash))
            {
                children.Add(Node(SyntaxKind.CellLineSeparator, Eat()));
                continue;
            }

            if (AtEnd)
            {
                break;
            }

            children.Add(Skip(t => t.Kind is TokenKind.Slash or TokenKind.DoubleSlash, CoreCodes.Unparsed, "Text not understood."));
        }

        children.Add(Eat()); // EndOfSegment
        if (_type == NotationType.Cgh)
        {
            RefileCghParts(firstDiagnostic);
        }

        return Node(SyntaxKind.FishResult, children);
    }

    private SyntaxNode ParseCellLine()
    {
        var children = new List<SyntaxElement> { ParseItem() };
        while (At(TokenKind.Comma))
        {
            children.Add(Eat());
            children.Add(ParseItem());
        }

        return Node(SyntaxKind.FishCellLine, children);
    }

    private SyntaxNode ParseItem()
    {
        var children = new List<SyntaxElement>();
        var regionLed = At(TokenKind.Number) || At(TokenKind.ChromosomeLetter) || At(TokenKind.OpenParen);
        var core = _type switch
        {
            NotationType.NucIsh or NotationType.FibIsh => ParseRegion(),
            NotationType.Ish or NotationType.RevIsh when regionLed => ParseRegion(),
            _ when AtItemEnd => Node(SyntaxKind.Skipped, MissingReported(TokenKind.Word, "", CoreCodes.Unparsed, "A result is expected here.")),
            _ => _karyotype.ParseItem(),
        };
        children.Add(core);

        if ((At(TokenKind.Plus) || At(TokenKind.Minus)) && Previous.Kind == TokenKind.CloseParen && Adjacent)
        {
            // del(22)(q11.2q11.2)(D22S75)–: the status follows the locus, inside its parentheses (ISH.LOCUS_STATUS).
            var close = Previous;
            var sign = Eat();
            Report(IshLocusStatus, sign.Span, SyntaxDiagnosticKind.Unexpected, "The status follows the locus inside the parentheses.",
                Delete(sign.Span), Insert(close.Span.Start, TextOf(sign.Span)));
            children.Add(sign);
        }

        if (At(TokenKind.OpenBracket))
        {
            children.Add(_loci.ParseCellCount());
        }

        return Node(SyntaxKind.FishItem, children);
    }

    private bool AtItemEnd => Current.Kind is TokenKind.Comma or TokenKind.Slash or TokenKind.DoubleSlash or TokenKind.OpenBracket or TokenKind.EndOfSegment;

    /// <summary>
    /// <c>[chromosome[band]](loci)[×n](positions)…[amp]</c>: <c>22q11.2(D22S75×2)</c>, <c>X(DXZ1×2)</c>, <c>(ABL1,BCR)×2(ABL1 con BCR×1)</c>,
    /// <c>15q</c>; a band in parentheses (<c>(22q11.2)(D22S75×2)</c>) is kept for ISH.NORMAL_CHROMOSOME_FORMAT.
    /// </summary>
    private SyntaxNode ParseRegion()
    {
        var children = new List<SyntaxElement>();
        if (At(TokenKind.Number) || At(TokenKind.ChromosomeLetter))
        {
            var chrom = Eat();
            children.Add(Adjacent && (IsWord(Current, ArmWords) || KaryotypeGrammar.IsArmSymbolTypo(Current)) ? _karyotype.ParseBand(chrom) : chrom);
        }
        else if (At(TokenKind.OpenParen) && Peek(1).Kind is TokenKind.Number or TokenKind.ChromosomeLetter && IsWord(Peek(2), ArmWords))
        {
            // (22q11.2)(D22S75×2): the band in parentheses (ISH.NORMAL_CHROMOSOME_FORMAT).
            children.Add(_karyotype.ParseBreakpointList());
        }

        while (true)
        {
            if (At(TokenKind.OpenParen))
            {
                children.Add(_loci.ParseGroup());
                continue;
            }

            if (At(TokenKind.Multiply))
            {
                children.Add(_loci.ParseCount());
                continue;
            }

            if (IsWord(Current, LocusGrammar.QualifierWords))
            {
                children.Add(Eat()); // (PAX7 con FOXO1)amp (§13.3.2 E12)
                continue;
            }

            break;
        }

        if (children.Count == 0)
        {
            children.Add(Skip(t => t.Kind is TokenKind.Comma or TokenKind.Slash or TokenKind.DoubleSlash or TokenKind.OpenBracket, CoreCodes.Unparsed, "Text not understood."));
        }

        return Node(SyntaxKind.FishRegion, children);
    }

    /// <summary>
    /// In a cgh result, <c>enh</c>/<c>dim</c> take the chromosome in parentheses like any symbol; the karyotype grammar reports
    /// <c>enh21</c> under KARYO.CHROMOSOME_IN_PARENTHESES, which in cgh is CGH.FORMAT's.
    /// </summary>
    private void RefileCghParts(int from)
    {
        for (var i = from; i < Cursor.Diagnostics.Count; i++)
        {
            if (Cursor.Diagnostics[i].RuleId == ChromosomeInParentheses)
            {
                Cursor.Diagnostics[i] = Cursor.Diagnostics[i] with { RuleId = CghFormat };
            }
        }
    }
}
