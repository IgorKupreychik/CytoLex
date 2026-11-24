using System.Collections.Frozen;
using System.Collections.Immutable;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;
using static CytoLex.Core.Syntax.Parser;

namespace CytoLex.Core.Syntax;

/// <summary>
/// The karyotype grammar (docs/design/core.md §4, T-027): cell lines, counts, sex chromosomes and abnormalities, over a shared
/// <see cref="TokenCursor"/>. Other grammars reuse its bands, chromosome lists and abnormalities. With
/// <paramref name="loci"/> (the ish grammars, T-033), an abnormality's parenthesis groups may be followed by locus groups.
/// </summary>
internal sealed class KaryotypeGrammar(TokenCursor cursor, IAbnormalityLoci? loci = null) : GrammarBase(cursor)
{
    private bool AtCellLineEnd => Current.Kind is TokenKind.Slash or TokenKind.DoubleSlash or TokenKind.EndOfSegment;

    /// <summary>Tokens that end an item: <c>,</c> <c>/</c> <c>//</c> <c>[</c> or the end.</summary>
    private bool AtItemEnd => Current.Kind is TokenKind.Comma or TokenKind.Slash or TokenKind.DoubleSlash
        or TokenKind.OpenBracket or TokenKind.EndOfSegment;

    // ── Root and cell lines ──────────────────────────────────────────────────────────────────────

    public SyntaxNode ParseRoot(bool fragment)
    {
        var children = new List<SyntaxElement>();
        if (AtEnd && !fragment)
        {
            Report(CountSexOrder, new TextSpan(Current.Span.Start, 0), SyntaxDiagnosticKind.Missing, "The karyotype is empty; it starts with the chromosome count.");
        }

        while (IsWord(Current, PrefixWords))
        {
            children.Add(Node(SyntaxKind.Prefix, Eat()));
        }

        while (true)
        {
            if (!AtCellLineEnd)
            {
                children.Add(ParseCellLine(fragment));
            }

            if (At(TokenKind.Slash) || At(TokenKind.DoubleSlash))
            {
                children.Add(Node(SyntaxKind.CellLineSeparator, Eat()));
                continue;
            }

            if (At(TokenKind.Comma) && children[^1] is SyntaxNode { Kind: SyntaxKind.CellLine } line
                && line.Children[^1] is SyntaxNode { Kind: SyntaxKind.CellCount })
            {
                Report(CloneSeparator, Current.Span, SyntaxDiagnosticKind.Unexpected, "Cell lines are separated by a slant line (/), not a comma.", "/");
                children.Add(Node(SyntaxKind.CellLineSeparator, Eat()));
                continue;
            }

            if (IsWord(Current, PrefixWords))
            {
                var word = Current;
                TextEdit[] move = [Insert(Cursor.TokenAt(0).Span.Start, word.Text + " "), Delete(TextSpan.FromBounds(Previous.Span.End, word.Span.End))];
                if (word.Text == "end")
                {
                    Report(Endoreduplication, word.Span, SyntaxDiagnosticKind.Unexpected, "'end' goes before the karyotype designation.", move);
                }
                else
                {
                    Report(MosaicChimeraPrefix, word.Span, SyntaxDiagnosticKind.Unexpected, $"'{word.Text}' goes before the karyotype designations.", move);
                }

                children.Add(Node(SyntaxKind.MisplacedPrefix, Eat()));
                continue;
            }

            if (AtEnd)
            {
                break;
            }

            if (IsWord(Current, FishGrammar.KeywordWords) && Cursor.Position > 0 && Previous.HasTrailingTrivia)
            {
                // "46,XY ish 22q11.2(D22S75×2)": a period separates the ish result from the karyotype (ISH.COMBINED_WITH_KARYOTYPE).
                var gap = TextSpan.FromBounds(Previous.Span.End, Current.Span.Start);
                var keyword = Current.Span;
                var rest = new List<SyntaxElement>();
                while (!AtEnd)
                {
                    rest.Add(Eat());
                }

                Report(IshCombinedWithKaryotype, keyword, SyntaxDiagnosticKind.Unexpected, "A period separates the in situ result from the karyotype.", new TextEdit(gap, "."));
                children.Add(Node(SyntaxKind.Skipped, rest));
                break;
            }

            children.Add(Skip(t => t.Kind is TokenKind.Slash or TokenKind.DoubleSlash, CoreCodes.Unparsed, "Text not understood."));
        }

        children.Add(Eat()); // EndOfSegment: carries the whitespace of an empty segment
        return Node(fragment ? SyntaxKind.Fragment : SyntaxKind.Karyotype, children);
    }

    private SyntaxNode ParseCellLine(bool fragment)
    {
        var children = new List<SyntaxElement>();
        var needComma = true;

        if (At(TokenKind.ChromosomeLetter) && SexThenCount())
        {
            // "XX,46": sex chromosomes before the count.
            var sex = ParseSex();
            var comma = Eat();
            var count = ParseCount();
            Report(
                CountSexOrder,
                Cover(sex, count),
                SyntaxDiagnosticKind.Unexpected,
                "The chromosome count comes before the sex chromosomes.",
                new TextEdit(Cover(sex, count), TextOf(count.Span) + "," + TextOf(sex.Span)));
            children.AddRange([sex, comma, count]);
        }
        else if (!fragment || LooksLikeHeader())
        {
            needComma = ParseHeader(children);
        }
        else
        {
            children.Add(ParseItem());
        }

        while (true)
        {
            if (At(TokenKind.Comma) && Peek(1).Kind == TokenKind.Multiply && children.Count > 0
                && children[^1] is SyntaxNode { Kind: SyntaxKind.Item } clone && IsCloneReference(clone))
            {
                // "idem,×2": the multiplication sign follows the clone reference directly (§11.1.4 ¶3).
                var comma = Eat();
                Report(PolyploidSubclone, comma.Span, SyntaxDiagnosticKind.Unexpected, "The multiplication sign follows idem, sl or sdl directly, without a comma.", Delete(comma.Span));
                var core = (SyntaxNode)clone.Children[0];
                var extended = new List<SyntaxElement>(core.Children) { comma };
                ParseSuffixes(extended);
                children[^1] = Node(SyntaxKind.Item, [Node(core.Kind, extended), .. clone.Children.Skip(1)]);
                continue;
            }

            if (At(TokenKind.Comma))
            {
                children.Add(Eat());
                children.Add(ParseItem());
                continue;
            }

            if (At(TokenKind.OpenBracket) && Peek(1).Kind == TokenKind.Number && Peek(2).Kind == TokenKind.CloseBracket
                && (Peek(3).Kind == TokenKind.OpenBracket
                    || (Peek(3).Kind == TokenKind.Comma && !(Peek(4).Kind == TokenKind.Number && Peek(5).Kind is TokenKind.Comma or TokenKind.Tilde or TokenKind.OpenAngle)))
                && children.Count > 0 && children[^1] is SyntaxNode { Kind: SyntaxKind.Item } counted)
            {
                // "del(3)(p12)[2],": the number of cells with this abnormality (§11.1.5 ¶2, E2).
                var count = Node(SyntaxKind.ItemCellCount, Eat(), Eat(), Eat());
                children[^1] = Node(SyntaxKind.Item, [.. counted.Children, count]);
                continue;
            }

            if (!needComma && !IsWord(Current, PrefixWords) && !At(TokenKind.OpenBracket) && !AtCellLineEnd)
            {
                // The header's comma was already read (no sex chromosomes): the first item follows.
                needComma = true;
                children.Add(ParseItem());
                continue;
            }

            if (At(TokenKind.OpenBracket))
            {
                children.Add(ParseCellCount());
                break;
            }

            if (At(TokenKind.OpenParen) && Peek(1).Kind == TokenKind.Number && Peek(2).Kind == TokenKind.CloseParen)
            {
                var open = Eat();
                var n = Eat();
                var close = Eat();
                Report(CloneSeparator, Cover(open, close), SyntaxDiagnosticKind.Unexpected, "Cell counts are written in square brackets.", "[" + n.Text + "]");
                children.Add(Node(SyntaxKind.CellCount, open, n, close));
                break;
            }

            if (At(TokenKind.Plus) || At(TokenKind.Minus))
            {
                var missing = Missing(TokenKind.Comma, ",");
                Report(AbnormalityOrder, missing.Span, SyntaxDiagnosticKind.Missing, "Abnormalities are separated by commas.", ",");
                children.Add(missing);
                children.Add(ParseItem());
                continue;
            }

            break;
        }

        return Node(SyntaxKind.CellLine, children);
    }

    /// <summary>An item whose abnormality is a clone reference: <c>idem</c>, <c>sl</c>, <c>sdl1</c> (§11.1.4).</summary>
    private static bool IsCloneReference(SyntaxNode item) =>
        item.Children[0] is SyntaxNode { Kind: SyntaxKind.Abnormality } core
        && core.ChildNodes(SyntaxKind.AbnormalityPart).FirstOrDefault() is { } part
        && part.ChildNodes(SyntaxKind.Symbols).SelectMany(s => s.Children.OfType<SyntaxToken>()).LastOrDefault() is { Text: "idem" or "sl" or "sdl" };

    /// <summary><c>XX,46</c>: X/Y letters, a comma, then a number.</summary>
    private bool SexThenCount()
    {
        var k = 0;
        while (Peek(k).Kind == TokenKind.ChromosomeLetter)
        {
            k++;
        }

        return Peek(k).Kind == TokenKind.Comma && Peek(k + 1).Kind == TokenKind.Number;
    }

    /// <summary>A fragment starts with a header: <c>count[~count][&lt;…&gt;],</c>.</summary>
    private bool LooksLikeHeader()
    {
        var k = 0;
        if (Peek(k).Kind != TokenKind.Number)
        {
            return false;
        }

        k++;
        if (Peek(k).Kind == TokenKind.Tilde && Peek(k + 1).Kind == TokenKind.Number)
        {
            k += 2;
        }

        if (Peek(k).Kind == TokenKind.OpenAngle)
        {
            while (Peek(k).Kind is not (TokenKind.CloseAngle or TokenKind.EndOfSegment))
            {
                k++;
            }

            k++;
        }

        return Peek(k).Kind == TokenKind.Comma;
    }

    /// <summary>Count, ploidy, comma, sex chromosomes. Returns whether the next item needs a comma first (false when no sex field follows the comma).</summary>
    private bool ParseHeader(List<SyntaxElement> children)
    {
        if (!At(TokenKind.Number) && !At(TokenKind.Tilde))
        {
            var missing = Missing(TokenKind.Number, "46");
            Report(CountSexOrder, missing.Span, SyntaxDiagnosticKind.Missing, "The karyotype starts with the chromosome count.");
            children.Add(Node(SyntaxKind.ChromosomeCount, missing));
            if (!AtItemEnd)
            {
                children.Add(ParseItem());
            }

            return true;
        }

        children.Add(ParseCount());

        if (At(TokenKind.OpenAngle))
        {
            children.Add(ParsePloidy());
        }
        else if (At(TokenKind.OpenParen) && Peek(1).Kind == TokenKind.Number && IsPloidyN(Peek(2)) && Peek(3).Kind == TokenKind.CloseParen)
        {
            var ploidy = Node(SyntaxKind.Ploidy, Eat(), Eat(), Eat(), Eat());
            var level = TextSpan.FromBounds(ploidy.Children[1].Span.Start, ploidy.Children[2].Span.End);
            Report(PloidyInAngleBrackets, ploidy.Span, SyntaxDiagnosticKind.Unexpected, "The ploidy level is written in angle brackets.", new TextEdit(ploidy.Span, "<" + TextOf(level) + ">"));
            children.Add(ploidy);
        }

        if (At(TokenKind.Comma) && Peek(1).Kind == TokenKind.OpenAngle)
        {
            Report(PloidyInAngleBrackets, Current.Span, SyntaxDiagnosticKind.Unexpected, "The ploidy level follows the count directly, without a comma.", Delete(TextSpan.FromBounds(Current.Span.Start, Peek(1).Span.Start)));
            children.Add(Eat());
            children.Add(ParsePloidy());
        }

        if (At(TokenKind.Comma))
        {
            children.Add(Eat());
        }
        else if (AtCellLineEnd || At(TokenKind.OpenBracket))
        {
            return true;
        }
        else
        {
            var missing = Missing(TokenKind.Comma, ",");
            Report(CountSexOrder, missing.Span, SyntaxDiagnosticKind.Missing, "A comma follows the chromosome count.", ",");
            children.Add(missing);
        }

        if (At(TokenKind.ChromosomeLetter) && IsSexField())
        {
            children.Add(ParseSex());
            return true;
        }

        if ((At(TokenKind.UnknownWord) || At(TokenKind.Word)) && Current.Text.All(char.IsAsciiLetterUpper)
            && Current.Text.Length <= 2 && Peek(1).Kind is TokenKind.Comma or TokenKind.OpenBracket or TokenKind.Slash or TokenKind.DoubleSlash or TokenKind.EndOfSegment)
        {
            Report(ChromIdentifiers, Current.Span, SyntaxDiagnosticKind.Unexpected, $"'{Current.Text}' is not a sex chromosome; they are X and Y.");
            children.Add(Node(SyntaxKind.SexChromosomes, Eat()));
            return true;
        }

        return false;
    }

    private static bool IsPloidyN(SyntaxToken t) => t.Kind is TokenKind.UnknownWord or TokenKind.Word && t.Text == "n";

    private SyntaxNode ParseCount()
    {
        var children = new List<SyntaxElement>();
        if (At(TokenKind.Number))
        {
            children.Add(Eat());
        }

        if (At(TokenKind.Tilde))
        {
            children.Add(Eat());
            children.Add(At(TokenKind.Number) ? Eat() : MissingReported(TokenKind.Number, "48", CoreCodes.Unparsed, "A number follows '~'."));
        }

        return Node(SyntaxKind.ChromosomeCount, children);
    }

    private SyntaxNode ParsePloidy()
    {
        var children = new List<SyntaxElement> { Eat() };
        while (!At(TokenKind.CloseAngle) && !AtItemEnd)
        {
            children.Add(Eat());
        }

        children.Add(At(TokenKind.CloseAngle) ? Eat() : MissingReported(TokenKind.CloseAngle, ">", CoreCodes.Unparsed, "'<' needs a closing '>'."));
        return Node(SyntaxKind.Ploidy, children);
    }

    /// <summary>X/Y letters (then <c>c</c>, <c>?c</c>) not followed by an arm or a number: the sex chromosome field.</summary>
    private bool IsSexField()
    {
        var k = 0;
        while (Peek(k).Kind == TokenKind.ChromosomeLetter)
        {
            k++;
        }

        var next = Peek(k);
        return !(next.Kind == TokenKind.Number || IsWord(next, ArmWords) || IsWord(next, FeatureWords) || IsWord(next, LandmarkWords));
    }

    private SyntaxNode ParseSex()
    {
        var children = new List<SyntaxElement>();
        while (At(TokenKind.ChromosomeLetter))
        {
            children.Add(Eat());
        }

        if (AtWord("c") || (At(TokenKind.Question) && Peek(1) is { Kind: TokenKind.Word, Text: "c" }))
        {
            var suffix = new List<SyntaxElement>();
            if (At(TokenKind.Question))
            {
                suffix.Add(Eat());
            }

            suffix.Add(Eat());
            if (At(TokenKind.Question) && Adjacent)
            {
                suffix.Add(Eat()); // "c?": order judged by KARYO.UNCERTAIN_CONSTITUTIONAL
            }

            children.Add(Node(SyntaxKind.Suffix, suffix));
        }

        return Node(SyntaxKind.SexChromosomes, children);
    }

    private SyntaxNode ParseCellCount()
    {
        var children = new List<SyntaxElement> { Eat() };
        while (!At(TokenKind.CloseBracket) && !AtCellLineEnd && !At(TokenKind.Comma))
        {
            children.Add(Eat());
        }

        children.Add(At(TokenKind.CloseBracket) ? Eat() : MissingReported(TokenKind.CloseBracket, "]", CoreCodes.Unparsed, "'[' needs a closing ']'."));
        return Node(SyntaxKind.CellCount, children);
    }

    // ── Items ────────────────────────────────────────────────────────────────────────────────────

    internal SyntaxNode ParseItem()
    {
        var first = ParseItemCore();
        if (!AtWord("or"))
        {
            return Node(SyntaxKind.Item, first);
        }

        var alternatives = new List<SyntaxElement> { first };
        while (AtWord("or"))
        {
            alternatives.Add(Eat());
            alternatives.Add(ParseItemCore());
        }

        return Node(SyntaxKind.Item, Node(SyntaxKind.Alternative, alternatives));
    }

    private SyntaxNode ParseItemCore()
    {
        if (At(TokenKind.Ellipsis))
        {
            return Node(SyntaxKind.Ellipsis, Eat());
        }

        if (At(TokenKind.OpenParen))
        {
            return ParseBreakpointList(); // a band composition on its own (book fragment)
        }

        if (IsWord(Current, InheritanceWords))
        {
            var from = Previous.Kind == TokenKind.Comma && Cursor.Position > 1 ? Cursor.TokenAt(Cursor.Position - 2).Span.End : Previous.Span.End;
            Report(ParentalOrigin, Current.Span, SyntaxDiagnosticKind.Unexpected, $"'{Current.Text}' follows the abnormality directly, not as a separate item.", Delete(TextSpan.FromBounds(from, Current.Span.Start)));
            return Node(SyntaxKind.Suffix, Eat());
        }

        SyntaxNode? signs = null;
        if (Current.Kind is TokenKind.Plus or TokenKind.Minus or TokenKind.Question)
        {
            var s = new List<SyntaxElement>();
            while (Current.Kind is TokenKind.Plus or TokenKind.Minus or TokenKind.Question)
            {
                s.Add(Eat());
            }

            signs = Node(SyntaxKind.Signs, s);
        }

        if ((At(TokenKind.Number) || At(TokenKind.Tilde)) && LeadingCountAhead())
        {
            return ParseAbnormality(signs, ParseLeadingCount());
        }

        if (At(TokenKind.Number) || At(TokenKind.ChromosomeLetter))
        {
            return ParseChromosomeItem(signs);
        }

        if (IsWord(Current, ArmWords) || IsArmSymbolTypo(Current))
        {
            // A band without its chromosome, e.g. the second reading of "1q23 or q24".
            var children = new List<SyntaxElement>();
            AddIfNotNull(children, signs);
            children.Add(ParseBand(null));
            return Node(SyntaxKind.ChromosomeRegion, children);
        }

        if ((At(TokenKind.Word) && !NonSymbolWords.Contains(Current.Text)) || At(TokenKind.UnknownWord))
        {
            return ParseAbnormality(signs, null);
        }

        if (signs is not null)
        {
            Report(GainLossSign, signs.Span, SyntaxDiagnosticKind.Unexpected, "A sign must be followed by a chromosome or an abnormality.");
            return signs;
        }

        if (AtItemEnd)
        {
            var missing = MissingReported(TokenKind.Word, "", CoreCodes.Unparsed, "An abnormality is expected here.");
            return Node(SyntaxKind.Skipped, missing);
        }

        return Skip(t => t.Kind is TokenKind.Comma or TokenKind.Slash or TokenKind.DoubleSlash or TokenKind.OpenBracket, CoreCodes.Unparsed, "Text not understood.");
    }

    /// <summary>A zero-width '(' at <paramref name="at"/>, reported under SYM.PARENTHESES_PAIRED (T-085).</summary>
    private SyntaxToken MissingOpenParen(int at, string message)
    {
        var open = new SyntaxToken(TokenKind.OpenParen, new TextSpan(at, 0), "(", [], [], isMissing: true);
        Report(ParenthesesPaired, open.Span, SyntaxDiagnosticKind.Missing, message, "(");
        return open;
    }

    private static void AddIfNotNull(List<SyntaxElement> list, SyntaxElement? element)
    {
        if (element is not null)
        {
            list.Add(element);
        }
    }

    /// <summary><c>3mar</c>, <c>12~20dmin</c>, <c>~14dmin</c>, <c>5r</c>: a number (range) directly followed by a symbol.</summary>
    private bool LeadingCountAhead()
    {
        var k = 0;
        if (Peek(k).Kind == TokenKind.Number)
        {
            k++;
        }

        if (Peek(k).Kind == TokenKind.Tilde)
        {
            if (Peek(k + 1).Kind != TokenKind.Number)
            {
                return false;
            }

            k += 2;
        }

        if (k == 0 || Peek(k - 1).HasTrailingTrivia)
        {
            return false;
        }

        var next = Peek(k);
        return next.Kind == TokenKind.UnknownWord ? !IsArmSymbolTypo(next)
            : next.Kind == TokenKind.Word && !NonSymbolWords.Contains(next.Text) && !IsArmSymbolTypo(next);
    }

    private SyntaxNode ParseLeadingCount()
    {
        var children = new List<SyntaxElement>();
        if (At(TokenKind.Number))
        {
            children.Add(Eat());
        }

        if (At(TokenKind.Tilde))
        {
            children.Add(Eat());
            children.Add(Eat());
        }

        return Node(SyntaxKind.LeadingCount, children);
    }

    /// <summary><c>P</c>/<c>Q</c> where an arm symbol belongs (BAND.ARM_SYMBOL).</summary>
    internal static bool IsArmSymbolTypo(SyntaxToken t) => t.Kind is TokenKind.UnknownWord or TokenKind.Word && t.Text is "P" or "Q";

    /// <summary>An item led by a chromosome: a whole-chromosome change (<c>+21</c>, <c>–X</c>) or a region (<c>16qh+</c>, <c>1p31</c>).</summary>
    private SyntaxNode ParseChromosomeItem(SyntaxNode? signs)
    {
        var children = new List<SyntaxElement>();
        AddIfNotNull(children, signs);
        var chrom = Eat();
        children.Add(chrom);

        if (IsRegionStart(Current))
        {
            ParseRegionTail(children, chrom);
            ParseSuffixes(children);
            return Node(SyntaxKind.ChromosomeRegion, children);
        }

        if ((At(TokenKind.Plus) || At(TokenKind.Minus)) && Adjacent)
        {
            var sign = Eat();
            children.Add(sign);
            Report(GainLossSign, Cover(chrom, sign), SyntaxDiagnosticKind.Unexpected, "The plus or minus sign goes before the chromosome.", new TextEdit(Cover(chrom, sign), sign.Text + TextOf(chrom.Span)));
        }
        else if (At(TokenKind.Question) && Adjacent)
        {
            var q = Eat();
            children.Add(q);
            Report(QuestionableItem, q.Span, SyntaxDiagnosticKind.Unexpected, "The question mark goes before the questionable chromosome or abnormality.", Insert(chrom.Span.Start, q.Text), Delete(q.Span));
        }
        else if (signs is null)
        {
            Report(GainLossSign, chrom.Span, SyntaxDiagnosticKind.Unexpected, "A gained or lost chromosome is written with a plus or minus sign before it.");
        }

        ParseSuffixes(children);
        return Node(SyntaxKind.WholeChromosome, children);
    }

    private static bool IsRegionStart(SyntaxToken t) =>
        IsWord(t, ArmWords) || IsWord(t, FeatureWords) || IsWord(t, LandmarkWords) || IsArmSymbolTypo(t);

    /// <summary>
    /// Arms, bands and variant features after a chromosome: <c>q</c>, <c>p31.1</c>, <c>cenh+</c>, <c>pstk+</c>, <c>qh–</c>.
    /// Reports arm-length signs (<c>5q–</c>) and features written before the arm (<c>16hq+</c>).
    /// </summary>
    private void ParseRegionTail(List<SyntaxElement> children, SyntaxToken chrom)
    {
        SyntaxToken? featureBeforeArm = null;
        var sawArm = false;
        var featureSinceArm = false;
        var first = true;
        while (true)
        {
            var isFirst = first;
            first = false;
            if (IsWord(Current, ArmWords) || IsArmSymbolTypo(Current))
            {
                var band = ParseBand(null, allowCommaSubband: true);
                children.Add(band);
                if (featureBeforeArm is not null)
                {
                    Report(VarLengthSign, Cover(featureBeforeArm, band.Children[0]), SyntaxDiagnosticKind.Unexpected, "The variant symbol follows the arm (e.g. 16qh+).");
                    featureBeforeArm = null;
                    sawArm = true;
                    featureSinceArm = true; // reported once, not again as an arm-length sign
                    continue;
                }

                sawArm = true;
                featureSinceArm = false;
                continue;
            }

            if (IsWord(Current, FeatureWords) || IsWord(Current, LandmarkWords))
            {
                var word = Eat();
                children.Add(word);
                if (isFirst && word.Text is "h" or "s" or "stk" or "var")
                {
                    featureBeforeArm = word; // "16hq+": the feature is written before the arm
                }

                featureSinceArm = true;
                continue;
            }

            if ((At(TokenKind.Plus) || At(TokenKind.Minus)) && Adjacent)
            {
                var sign = Eat();
                children.Add(sign);
                if (sawArm && !featureSinceArm)
                {
                    Report(NoArmLengthSign, Cover(chrom, sign), SyntaxDiagnosticKind.Unexpected, "Arm-length notation such as 5q– or 4p+ is not used in karyotypes.");
                }

                continue;
            }

            break;
        }
    }

    /// <summary>
    /// A band or region: <c>[chromosome]arm[digits[.digits]]</c> with <c>?</c> and <c>~</c> (<c>q2?3</c>, <c>q21~24</c>).
    /// Reports a second decimal point (<c>36.3.3</c>) and, in an item, a comma used as the decimal point (<c>1p31,1</c>).
    /// </summary>
    internal SyntaxNode ParseBand(SyntaxToken? chrom, bool allowCommaSubband = false)
    {
        var children = new List<SyntaxElement>();
        AddIfNotNull(children, chrom);
        var arm = Eat();
        children.Add(arm);
        if (IsArmSymbolTypo(arm))
        {
            Report(ArmSymbol, arm.Span, SyntaxDiagnosticKind.Unexpected, $"The arm symbol is lower-case: {arm.Text.ToLowerInvariant()}.", arm.Text.ToLowerInvariant());
        }

        var dots = 0;
        var digits = false;
        while (true)
        {
            if (At(TokenKind.Number) && (Adjacent || !digits))
            {
                children.Add(Eat());
                digits = true;
                continue;
            }

            if (At(TokenKind.Dot) && Adjacent && Peek(1).Kind == TokenKind.Number)
            {
                var dot = Eat();
                children.Add(dot);
                dots++;
                if (dots > 1)
                {
                    Report(SubbandFormat, TextSpan.FromBounds(dot.Span.Start, Current.Span.End), SyntaxDiagnosticKind.Unexpected, "A sub-band has one decimal point; further digits follow it directly.", Delete(dot.Span));
                }

                continue;
            }

            if (At(TokenKind.Question) && Adjacent)
            {
                children.Add(Eat());
                continue;
            }

            if (At(TokenKind.Tilde) && Adjacent && Peek(1).Kind == TokenKind.Number)
            {
                children.Add(Eat());
                children.Add(Eat());
                continue;
            }

            break;
        }

        if (allowCommaSubband && digits && dots == 0 && At(TokenKind.Comma) && Adjacent
            && Peek(1).Kind == TokenKind.Number && !Current.HasTrailingTrivia
            && Peek(2).Kind is TokenKind.EndOfSegment or TokenKind.Comma)
        {
            var comma = Eat();
            Report(SubbandFormat, comma.Span, SyntaxDiagnosticKind.Unexpected, "Sub-bands follow a decimal point, not a comma.", ".");
            children.Add(comma);
            children.Add(Eat());
        }

        return Node(SyntaxKind.Band, children);
    }

    // ── Abnormalities ────────────────────────────────────────────────────────────────────────────

    private bool AtSymbolWord => (At(TokenKind.Word) && !NonSymbolWords.Contains(Current.Text)) || At(TokenKind.UnknownWord);

    private SyntaxNode ParseAbnormality(SyntaxNode? signs, SyntaxNode? leadingCount)
    {
        var children = new List<SyntaxElement>();
        AddIfNotNull(children, signs);
        AddIfNotNull(children, leadingCount);

        var hadGroup = true;
        // Further parts (der(1)t(1;3)(…)inv(1)(…)) follow a part with parentheses directly, without a space.
        while (hadGroup && (children.Count == (signs is null ? 0 : 1) + (leadingCount is null ? 0 : 1) || Adjacent)
            && (AtSymbolWord || (At(TokenKind.Question) && IsSymbolWordToken(Peek(1)))))
        {
            var part = ParseAbnormalityPart(out hadGroup);
            children.Add(part);
        }

        while (true)
        {
            var before = children.Count;
            ParseSuffixes(children);
            if (children.Count > before && children[^1] is SyntaxNode { Kind: SyntaxKind.Suffix } last
                && last.Children[0] is SyntaxToken { Kind: TokenKind.Multiply } && At(TokenKind.OpenParen))
            {
                var group = ParseBreakpointList();
                Report(MultiplySignAfter, last.Span, SyntaxDiagnosticKind.Unexpected, "×n goes after the whole abnormality, including its breakpoints.", Delete(last.Span), Insert(group.Span.End, TextOf(last.Span)));
                children.Add(group);
                continue;
            }

            // Loci after a suffix: der(4)t(4;11)(p16.3;p15)mat(wcp11+,…) (§13.2 E24).
            if (loci is not null && loci.AtLocusGroup() && loci.ParseLoci(children, this))
            {
                continue;
            }

            break;
        }

        return Node(SyntaxKind.Abnormality, children);
    }

    private static bool IsSymbolWordToken(SyntaxToken t) =>
        (t.Kind == TokenKind.Word && !NonSymbolWords.Contains(t.Text)) || t.Kind == TokenKind.UnknownWord;

    /// <summary>One symbol group with its parentheses: <c>[?]symbols[number](chromosomes)(breakpoints)</c>.</summary>
    private SyntaxNode ParseAbnormalityPart(out bool hadGroup)
    {
        var children = new List<SyntaxElement>();
        if (At(TokenKind.Question))
        {
            children.Add(Eat());
        }

        var symbols = new List<SyntaxElement>();
        while (AtSymbolWord)
        {
            if (At(TokenKind.UnknownWord))
            {
                var (rule, message) = UnlistedWordRules.TryGetValue(Current.Text, out var named) ? named
                    : Peek(1) is { Kind: TokenKind.Word, Text: "mar" } ? (MarNoSizeMorphology, $"The size or shape of a marker ('{Current.Text}') is not described in the karyotype.")
                    : (ListedOnly, $"'{Current.Text}' is not a listed symbol or abbreviation.");
                if (rule == IsoFormat)
                {
                    Report(rule, Current.Span, SyntaxDiagnosticKind.Unexpected, message, new TextEdit(Current.Span, "i"));
                }
                else if (rule == MarNoSizeMorphology)
                {
                    Report(rule, Current.Span, SyntaxDiagnosticKind.Unexpected, message, Delete(TextSpan.FromBounds(Current.Span.Start, Peek(1).Span.Start)));
                }
                else
                {
                    Report(rule, Current.Span, SyntaxDiagnosticKind.Unexpected, message);
                }
            }

            symbols.Add(Eat());
        }

        children.Add(Node(SyntaxKind.Symbols, symbols));
        var lastSymbol = (SyntaxToken)symbols[^1];

        var groups = 0;
        if (At(TokenKind.Number) && Adjacent)
        {
            var number = Eat();
            if (NumberedSymbols.Contains(lastSymbol.Text))
            {
                children.Add(Node(SyntaxKind.SymbolNumber, number));
            }
            else if (lastSymbol.Text != "dmin" && At(TokenKind.CloseParen) && Adjacent)
            {
                // del5)(q13): the chromosome's '(' is missing.
                var open = MissingOpenParen(number.Span.Start, "The chromosome has a closing ')' but no opening '('.");
                children.Add(Node(SyntaxKind.ChromosomeList, open, Node(SyntaxKind.ChromosomeSpec, number), Eat()));
                groups = 1;
            }
            else
            {
                var (rule, message) = lastSymbol.Text switch
                {
                    "dmin" => (DminFormat, "The number of double minutes goes before dmin."),
                    "upd" => (UpdFormat, "The chromosome of upd goes in parentheses."),
                    _ => (ChromosomeInParentheses, $"The chromosome goes in parentheses after {lastSymbol.Text}."),
                };
                if (lastSymbol.Text == "dmin")
                {
                    Report(rule, number.Span, SyntaxDiagnosticKind.Unexpected, message, Insert(lastSymbol.Span.Start, number.Text), Delete(number.Span));
                }
                else
                {
                    Report(rule, number.Span, SyntaxDiagnosticKind.Unexpected, message, "(" + number.Text + ")");
                }
                children.Add(Node(lastSymbol.Text == "dmin" ? SyntaxKind.SymbolNumber : SyntaxKind.BareChromosome, number));
                groups = lastSymbol.Text == "dmin" ? 0 : 1;
            }
        }

        while (At(TokenKind.OpenParen) && groups < 2 && !(loci?.AtLocusGroup() ?? false))
        {
            children.Add(groups == 0 ? ParseChromosomeList() : ParseBreakpointList());
            groups++;
        }

        if (groups == 1 && Adjacent && (IsWord(Current, ArmWords) || IsArmSymbolTypo(Current)))
        {
            var bare = ParseBreakpointList(withParens: false);
            if (At(TokenKind.CloseParen) && Adjacent)
            {
                // del(5)q13): the breakpoints' '(' is missing.
                var open = MissingOpenParen(bare.Span.Start, "The breakpoints have a closing ')' but no opening '('.");
                children.Add(Node(SyntaxKind.BreakpointList, [open, .. bare.Children, Eat()]));
            }
            else
            {
                Report(BreakpointGroups, bare.Span, SyntaxDiagnosticKind.Unexpected, "Breakpoints are written in parentheses.", Insert(bare.Span.Start, "("), Insert(bare.Span.End, ")"));
                children.Add(bare);
            }

            groups++;
        }

        var hadLoci = loci?.ParseLoci(children, this) ?? false;
        hadGroup = groups > 0 || hadLoci;
        return Node(SyntaxKind.AbnormalityPart, children);
    }

    /// <summary>Suffixes: <c>×n</c>, <c>c</c>, <c>?c</c>, <c>mat</c>/<c>pat</c>/<c>inh</c>/<c>dn</c>; <c>X2</c> recovered as <c>×2</c>.</summary>
    private void ParseSuffixes(List<SyntaxElement> children)
    {
        while (true)
        {
            if (At(TokenKind.Multiply))
            {
                var times = Eat();
                var n = At(TokenKind.Number) ? Eat() : MissingReported(TokenKind.Number, "2", CoreCodes.Unparsed, "A number follows the multiplication sign.");
                children.Add(Node(SyntaxKind.Suffix, times, n));
                continue;
            }

            if (At(TokenKind.ChromosomeLetter) && Current.Text == "X" && Adjacent && !Current.HasTrailingTrivia && Peek(1).Kind == TokenKind.Number)
            {
                var x = Eat();
                Report(TypedSymbolForms, x.Span, SyntaxDiagnosticKind.Unexpected, "Upper-case X is the sex chromosome; the multiplication sign is × (or x).", "×");
                children.Add(Node(SyntaxKind.Suffix, x, Eat()));
                continue;
            }

            if (AtWord("c") || (At(TokenKind.Question) && Peek(1) is { Kind: TokenKind.Word, Text: "c" }))
            {
                var suffix = new List<SyntaxElement>();
                if (At(TokenKind.Question))
                {
                    suffix.Add(Eat());
                }

                suffix.Add(Eat());
                children.Add(Node(SyntaxKind.Suffix, suffix));
                continue;
            }

            if (IsWord(Current, InheritanceWords))
            {
                children.Add(Node(SyntaxKind.Suffix, Eat()));
                continue;
            }

            break;
        }
    }

    // ── Parenthesised groups ─────────────────────────────────────────────────────────────────────

    /// <summary><c>(1;3)</c>, <c>(X;18)</c>, <c>(?;6)</c>, <c>(2p)</c>: chromosomes separated by semicolons.</summary>
    internal SyntaxNode ParseChromosomeList()
    {
        var children = new List<SyntaxElement> { Eat() };
        while (true)
        {
            if (At(TokenKind.CloseParen))
            {
                children.Add(Eat());
                break;
            }

            if ((AtItemEnd && !At(TokenKind.Comma)) || (children.Count > 1 && (At(TokenKind.OpenParen) || CommaEndsGroup)))
            {
                children.Add(MissingReported(TokenKind.CloseParen, ")", ParenthesesPaired, "The chromosomes need a closing ')'."));
                break;
            }

            if (At(TokenKind.Number) || At(TokenKind.ChromosomeLetter) || At(TokenKind.Question))
            {
                var spec = new List<SyntaxElement>();
                var chrom = Eat();
                spec.Add(chrom);
                if (chrom.Kind == TokenKind.Question && (At(TokenKind.Number) || At(TokenKind.ChromosomeLetter)) && Adjacent)
                {
                    spec.Add(Eat());
                }

                if (IsRegionStart(Current) && Adjacent)
                {
                    ParseRegionTail(spec, chrom);
                }

                children.Add(Node(SyntaxKind.ChromosomeSpec, spec));
            }
            else
            {
                children.Add(Skip(t => t.Kind is TokenKind.Semicolon or TokenKind.Comma or TokenKind.CloseParen or TokenKind.Slash or TokenKind.DoubleSlash or TokenKind.OpenBracket,
                    CoreCodes.Unparsed, "A chromosome is expected here."));
            }

            if (At(TokenKind.Semicolon))
            {
                children.Add(Eat());
            }
            else if (At(TokenKind.Comma) && !CommaEndsGroup)
            {
                Report(ChromosomeSeparator, Current.Span, SyntaxDiagnosticKind.Unexpected, "The chromosomes of a rearrangement are separated by a semicolon.", ";");
                children.Add(Eat());
            }
            else if (!At(TokenKind.CloseParen) && !AtItemEnd && !At(TokenKind.OpenParen))
            {
                children.Add(Skip(t => t.Kind is TokenKind.Semicolon or TokenKind.CloseParen or TokenKind.Comma or TokenKind.Slash or TokenKind.DoubleSlash,
                    CoreCodes.Unparsed, "Text not understood in the chromosome list."));
            }
        }

        return Node(SyntaxKind.ChromosomeList, children);
    }

    /// <summary>
    /// A comma inside a parenthesis group that starts the next item rather than separating chromosomes or breakpoints
    /// (<c>del(5)(q13,+8</c>): the group lacks its closing parenthesis (SYM.PARENTHESES_PAIRED, T-085).
    /// </summary>
    private bool CommaEndsGroup => At(TokenKind.Comma) && !StartsGroupContent(Peek(1));

    private static bool StartsGroupContent(SyntaxToken t) =>
        t.Kind is TokenKind.Number or TokenKind.ChromosomeLetter or TokenKind.Question or TokenKind.Arrow or TokenKind.Colon or TokenKind.DoubleColon
        || IsWord(t, ArmWords) || IsWord(t, LandmarkWords) || IsArmSymbolTypo(t);

    /// <summary>
    /// Breakpoints in parentheses, short or detailed system: groups separated by semicolons, each a sequence of bands,
    /// landmarks (<c>pter</c>, <c>cen</c>, <c>hsr</c>, <c>?</c>), <c>→</c>, <c>:</c>, <c>::</c>, <c>or</c>.
    /// Without parentheses (recovery for <c>inv(2)p21q31</c>) it ends at an item end.
    /// </summary>
    internal SyntaxNode ParseBreakpointList(bool withParens = true)
    {
        var children = new List<SyntaxElement>();
        if (withParens)
        {
            children.Add(Eat());
        }

        while (true)
        {
            children.Add(ParseBreakpointGroup(withParens));
            if (At(TokenKind.Semicolon))
            {
                children.Add(Eat());
                continue;
            }

            if (withParens)
            {
                children.Add(At(TokenKind.CloseParen) ? Eat() : MissingReported(TokenKind.CloseParen, ")", ParenthesesPaired, "The breakpoints need a closing ')'."));
            }

            break;
        }

        return Node(SyntaxKind.BreakpointList, children);
    }

    private bool AtGroupEnd(bool withParens) =>
        Current.Kind is TokenKind.Semicolon or TokenKind.EndOfSegment or TokenKind.Slash or TokenKind.DoubleSlash or TokenKind.OpenBracket
        || (withParens ? Current.Kind == TokenKind.CloseParen || CommaEndsGroup : Current.Kind is TokenKind.Comma or TokenKind.CloseParen);

    private SyntaxNode ParseBreakpointGroup(bool withParens)
    {
        var children = new List<SyntaxElement>();
        while (!AtGroupEnd(withParens))
        {
            var t = Current;
            switch (t.Kind)
            {
                case TokenKind.Arrow or TokenKind.Colon or TokenKind.DoubleColon or TokenKind.Tilde:
                    children.Add(Eat());
                    continue;
                case TokenKind.Word when t.Text == "or":
                    children.Add(Eat());
                    continue;
                case TokenKind.Word when LandmarkWords.Contains(t.Text):
                    children.Add(Node(SyntaxKind.Landmark, Eat()));
                    continue;
                case TokenKind.Word when ArmWords.Contains(t.Text):
                    children.Add(ParseBand(null));
                    continue;
                case TokenKind.Number or TokenKind.ChromosomeLetter or TokenKind.Question:
                    children.Add(ParseChromosomePoint());
                    continue;
                case TokenKind.Comma when withParens:
                    Report(BreakpointGroups, t.Span, SyntaxDiagnosticKind.Unexpected, "Breakpoint groups are separated by a semicolon.", ";");
                    children.Add(Eat());
                    continue;
                case TokenKind.UnknownWord or TokenKind.Word when IsArmSymbolTypo(t):
                    children.Add(ParseBand(null));
                    continue;
                case TokenKind.UnknownWord:
                    Report(ListedOnly, t.Span, SyntaxDiagnosticKind.Unexpected, $"'{t.Text}' is not a listed symbol or abbreviation.");
                    children.Add(Eat());
                    continue;
                default:
                    children.Add(Skip(_ => true, CoreCodes.Unparsed, "Text not understood in the breakpoints."));
                    continue;
            }
        }

        if (children.Count == 0)
        {
            children.Add(MissingReported(TokenKind.Word, "", CoreCodes.Unparsed, "A breakpoint is expected here."));
        }

        return Node(SyntaxKind.BreakpointGroup, children);
    }

    /// <summary><c>3q13.1</c>, <c>Xpter</c>, <c>9̲q34</c>, <c>?cen</c>, or a lone <c>?</c>/number.</summary>
    private SyntaxNode ParseChromosomePoint()
    {
        var chrom = Eat();
        if (Adjacent && (IsWord(Current, ArmWords) || IsArmSymbolTypo(Current)))
        {
            return ParseBand(chrom);
        }

        if (Adjacent && IsWord(Current, LandmarkWords))
        {
            return Node(SyntaxKind.Landmark, chrom, Eat());
        }

        if (chrom.Kind == TokenKind.Question)
        {
            return Node(SyntaxKind.Landmark, chrom);
        }

        Report(CoreCodes.Unparsed, chrom.Span, SyntaxDiagnosticKind.Unexpected, "A chromosome in a breakpoint is followed by its arm or band.");
        return Node(SyntaxKind.Skipped, chrom);
    }
}
