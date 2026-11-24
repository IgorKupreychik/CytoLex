using System.Collections.Frozen;
using System.Collections.Immutable;
using CytoLex.Core.Kb;
using CytoLex.Core.Segmentation;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Syntax;

/// <summary>
/// Hand-written recursive-descent parser for karyotype segments (docs/design/core.md §4, T-027). It builds a
/// lossless tree, never stops at the first error and names the KB rule of the expected form at every recovery (§8.4).
/// </summary>
/// <remarks>
/// The grammar is deliberately permissive where the tree can represent the text: symbol combinations
/// (<c>der dic</c>, <c>min mar</c>), group counts (<c>t(2;5)(q21)</c>), order, spacing (kept as trivia), band
/// existence, arms inside chromosome lists (<c>del(5q)</c>, needed for <c>rec(2)dup(2p)…</c>) and the like are judged by
/// the rules (T-029+). The parser reports what breaks the surface form: missing or misplaced punctuation, signs on the
/// wrong side, unlisted words, chromosomes without parentheses, malformed sub-bands, misplaced prefixes.
/// </remarks>
public sealed class Parser
{
    internal const string GenomeBuildRequired = "GEN.GENOME_BUILD_REQUIRED";
    internal const string ArrNormalResult = "ARR.NORMAL_RESULT";
    internal const string ArrRegionFormat = "ARR.REGION_FORMAT";
    internal const string ArrMosaicProportion = "ARR.MOSAIC_PROPORTION";
    internal const string ArrCombinedWithOtherMethods = "ARR.COMBINED_WITH_OTHER_METHODS";

    /// <summary>Words that replace a copy number (§14.2 ¶4, §14.2.2): amplification, complex, chromothripsis.</summary>
    internal static readonly FrozenSet<string> ArrResultWords = Set("amp", "cx", "cth");

    /// <summary>Words after the copy number (§14.2 ¶4, §14.2.1): mosaic, zygosity, origin, constitutional.</summary>
    internal static readonly FrozenSet<string> ArrQualifierWords = Set("mos", "htz", "hmz", "mat", "pat", "inh", "dn", "c");

    /// <summary>Landmarks that may end an arr band range (<c>13q14.2qter</c>).</summary>
    internal static readonly FrozenSet<string> ArrLandmarkWords = Set("pter", "qter", "cen");

    // Word classes. Each word is a ch. 3 symbol; a test checks them against the vocab (KB-traced).
    internal static readonly FrozenSet<string> PrefixWords = Set("mos", "chi", "end");
    internal static readonly FrozenSet<string> InheritanceWords = Set("mat", "pat", "inh", "dn");
    internal static readonly FrozenSet<string> ArmWords = Set("p", "q");
    internal static readonly FrozenSet<string> LandmarkWords = Set("pter", "qter", "cen", "ter", "hsr", "neo");

    /// <summary>Variant features after a chromosome (§7.1): <c>h</c>, <c>s</c>, <c>stk</c>, <c>var</c>, satellited arms <c>ps</c>/<c>qs</c>, <c>cen</c>.</summary>
    internal static readonly FrozenSet<string> FeatureWords = Set("h", "s", "stk", "var", "ps", "qs", "cen");

    /// <summary>Symbols that take a number right after them (<c>mar1</c>, <c>r2</c>: MAR.NUMBERED, RING.UNIDENTIFIED; <c>sl1</c>, <c>sdl2</c>: §11.1.4).</summary>
    internal static readonly FrozenSet<string> NumberedSymbols = Set("mar", "r", "sl", "sdl");

    /// <summary>Words that never start or continue an abnormality's symbols.</summary>
    internal static readonly FrozenSet<string> NonSymbolWords =
        Set("c", "or", "mat", "pat", "inh", "dn", "p", "q", "pter", "qter", "cen", "ter", "h", "s", "stk", "var", "ps", "qs");

    // KB rule IDs used at recovery points (docs/design/core.md §8.4).
    internal const string CountSexOrder = "KARYO.COUNT_SEX_ORDER";
    internal const string AbnormalityOrder = "KARYO.ABNORMALITY_ORDER";
    internal const string ChromosomeInParentheses = "KARYO.CHROMOSOME_IN_PARENTHESES";
    internal const string ChromosomeSeparator = "STRUCT.CHROMOSOME_SEPARATOR";
    internal const string BreakpointGroups = "STRUCT.BREAKPOINT_GROUPS";
    internal const string GainLossSign = "KARYO.GAIN_LOSS_SIGN";
    internal const string NoArmLengthSign = "KARYO.NO_ARM_LENGTH_SIGN";
    internal const string VarLengthSign = "VAR.LENGTH_SIGN";
    internal const string ListedOnly = "SYM.LISTED_ONLY";
    internal const string TypedSymbolForms = "SYM.TYPED_SYMBOL_FORMS";
    internal const string ArmSymbol = "BAND.ARM_SYMBOL";
    internal const string SubbandFormat = "BAND.SUBBAND_FORMAT";
    internal const string ChromIdentifiers = "CHROM.IDENTIFIERS";
    internal const string CloneSeparator = "KARYO.CLONE_SEPARATOR";
    internal const string MosaicChimeraPrefix = "KARYO.MOSAIC_CHIMERA_PREFIX";
    internal const string Endoreduplication = "KARYO.ENDOREDUPLICATION";
    internal const string ParentalOrigin = "KARYO.PARENTAL_ORIGIN";
    internal const string MultiplySignAfter = "KARYO.MULTIPLY_SIGN_AFTER_ABNORMALITY";
    internal const string PloidyInAngleBrackets = "NUM.PLOIDY_IN_ANGLE_BRACKETS";
    internal const string QuestionableItem = "UNCERTAIN.QUESTIONABLE_ITEM";
    internal const string DminFormat = "DMIN.FORMAT";
    internal const string UpdFormat = "UPD.FORMAT";
    internal const string IsoFormat = "ISO.FORMAT";
    internal const string HsrNoAbrSymbol = "HSR.NO_ABR_SYMBOL";
    internal const string MarNoSizeMorphology = "MAR.NO_SIZE_MORPHOLOGY";
    internal const string PolyploidSubclone = "CLONE.POLYPLOID_SUBCLONE";
    internal const string IshCombinedWithKaryotype = "ISH.COMBINED_WITH_KARYOTYPE";
    internal const string IshStructuralFormat = "ISH.STRUCTURAL_FORMAT";
    internal const string IshLocusStatus = "ISH.LOCUS_STATUS";
    internal const string CghFormat = "CGH.FORMAT";

    /// <summary>
    /// Unlisted words that a symbol rule names (one fault, one finding; T-068), reported by that rule instead of
    /// <c>SYM.LISTED_ONLY</c>: <c>iso</c> (ISO.FORMAT: "i, never iso") and <c>abr</c> (HSR.NO_ABR_SYMBOL: no symbol for
    /// abnormally banded regions). An unlisted word directly before <c>mar</c> (<c>acro mar</c>) is MAR.NO_SIZE_MORPHOLOGY's.
    /// </summary>
    internal static readonly FrozenDictionary<string, (string Rule, string Message)> UnlistedWordRules =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["iso"] = (IsoFormat, "The symbol for an isochromosome is i, not iso."),
            ["abr"] = (HsrNoAbrSymbol, "There is no symbol for an abnormally banded region; abr is not used in karyotypes."),
        }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Every KB rule ID the parser can report (a test checks they exist in the KB).</summary>
    internal static readonly ImmutableArray<string> RuleIds =
    [
        CountSexOrder, AbnormalityOrder, ChromosomeInParentheses, ChromosomeSeparator, BreakpointGroups, GainLossSign,
        NoArmLengthSign, VarLengthSign, ListedOnly, TypedSymbolForms, ArmSymbol, SubbandFormat, ChromIdentifiers,
        CloneSeparator, MosaicChimeraPrefix, Endoreduplication, ParentalOrigin, MultiplySignAfter, PloidyInAngleBrackets,
        QuestionableItem, DminFormat, UpdFormat, IsoFormat, HsrNoAbrSymbol, MarNoSizeMorphology, PolyploidSubclone,
        GenomeBuildRequired, ArrNormalResult, ArrRegionFormat, ArrMosaicProportion, ArrCombinedWithOtherMethods,
        IshCombinedWithKaryotype, IshStructuralFormat, IshLocusStatus, CghFormat,
    ];

    private static readonly Dictionary<string, Parser> _byEdition = new(StringComparer.Ordinal);
    private static readonly Lock _byEditionLock = new();

    private readonly Vocabulary _vocab;
    private readonly Lexer _lexer;

    public Parser(Vocabulary vocab)
    {
        ArgumentNullException.ThrowIfNull(vocab);
        _vocab = vocab;
        _lexer = new Lexer(vocab);
    }

    /// <summary>A parser for <paramref name="edition"/> using the embedded KB vocab (cached; thread-safe).</summary>
    public static Parser ForEdition(string edition)
    {
        lock (_byEditionLock)
        {
            if (!_byEdition.TryGetValue(edition, out var parser))
            {
                parser = new Parser(Vocabulary.LoadEmbedded(edition));
                _byEdition[edition] = parser;
            }

            return parser;
        }
    }

    /// <summary>The vocabularies the parser was built from.</summary>
    public Vocabulary Vocabulary => _vocab;

    /// <summary>Parses a whole karyotype segment: <c>[mos|chi|end] count[,sex][,abnormalities][cells] (/|// …)*</c>.</summary>
    public SyntaxTree ParseKaryotype(string input, Segment segment) => Parse(input, segment, fragment: false);

    /// <summary>
    /// Parses a book fragment (<c>inv(2)</c>, <c>+21</c>, <c>1p31</c>, <c>16qh+</c>, <c>psu dic</c>, <c>43~47,XX,…</c>):
    /// the same grammar with the count and sex chromosomes optional.
    /// </summary>
    public SyntaxTree ParseFragment(string input, Segment segment) => Parse(input, segment, fragment: true);

    /// <summary>
    /// Parses <paramref name="segment"/> of <paramref name="input"/>: an arr segment with the arr grammar (T-034), the in situ
    /// types with the FISH grammar (T-033), any other as a whole karyotype or a fragment.
    /// </summary>
    public SyntaxTree Parse(string input, Segment segment, bool fragment)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(segment);
        var tokens = _lexer.Lex(input, segment.Span);
        if (segment.Type != NotationType.Arr)
        {
            tokens = FoldByPosition(tokens);
        }

        var cursor = new TokenCursor(tokens, input);
        var karyotype = new KaryotypeGrammar(cursor);
        var root = segment.Type switch
        {
            NotationType.Arr => new ArrGrammar(cursor, karyotype).ParseArrRoot(),
            NotationType.Ish or NotationType.NucIsh or NotationType.FibIsh or NotationType.RevIsh or NotationType.Cgh => new FishGrammar(cursor, segment.Type).ParseRoot(),
            _ => karyotype.ParseRoot(fragment),
        };
        return new SyntaxTree(
            input,
            segment,
            root,
            [.. cursor.Diagnostics.OrderBy(d => d.Span.Start)],
            FindConstructs(tokens));
    }

    /// <summary>
    /// Reads the two case-folded clashes by their position (SYM.LETTER_CASE, T-074): a typed <c>x</c> alone in the sex
    /// chromosome field (<c>45,x</c>) is <c>X</c>; the meiosis numeral <c>I</c> directly before <c>(</c> in an abnormality
    /// (<c>I(17)(q10)</c>) is <c>i</c>. An <c>x</c> elsewhere stays the multiplication sign (SYM.TYPED_SYMBOL_FORMS).
    /// </summary>
    private ImmutableArray<SyntaxToken> FoldByPosition(ImmutableArray<SyntaxToken> tokens)
    {
        ImmutableArray<SyntaxToken>.Builder? folded = null;
        for (var i = 0; i < tokens.Length - 1; i++)
        {
            var t = tokens[i];
            SyntaxToken? replacement = null;
            if (t is { Kind: TokenKind.Multiply, IsTypedForm: true, Text: "x" } && InSexField(tokens, i))
            {
                replacement = t.AsFolded(TokenKind.ChromosomeLetter, "X", null);
            }
            else if (t is { Kind: TokenKind.Word, Text: "I", HasTrailingTrivia: false } && tokens[i + 1].Kind == TokenKind.OpenParen
                && (i == 0 || tokens[i - 1].Kind is TokenKind.Comma or TokenKind.Plus or TokenKind.Minus or TokenKind.Question or TokenKind.CloseParen))
            {
                replacement = t.AsFolded(TokenKind.Word, "i", _vocab.Find("i")?.KbRef);
            }

            if (replacement is not null)
            {
                folded ??= tokens.ToBuilder();
                folded[i] = replacement;
            }
        }

        return folded?.ToImmutable() ?? tokens;
    }

    /// <summary>Token <paramref name="i"/> stands alone right after the comma that follows a cell line's chromosome count.</summary>
    private static bool InSexField(ImmutableArray<SyntaxToken> tokens, int i)
    {
        if (i < 2 || tokens[i - 1].Kind != TokenKind.Comma || tokens[i].HasTrailingTrivia
            || tokens[i + 1].Kind is not (TokenKind.Comma or TokenKind.OpenBracket or TokenKind.Slash or TokenKind.DoubleSlash or TokenKind.EndOfSegment))
        {
            return false;
        }

        var k = i - 2;
        if (tokens[k].Kind != TokenKind.Number)
        {
            return false;
        }

        if (k >= 2 && tokens[k - 1].Kind == TokenKind.Tilde && tokens[k - 2].Kind == TokenKind.Number)
        {
            k -= 2; // 45~46,x
        }

        return k == 0 || tokens[k - 1].Kind is TokenKind.Slash or TokenKind.DoubleSlash or TokenKind.Word;
    }

    private ImmutableArray<NotImplementedConstruct> FindConstructs(ImmutableArray<SyntaxToken> tokens)
    {
        var result = ImmutableArray.CreateBuilder<NotImplementedConstruct>();
        foreach (var t in tokens)
        {
            if (t.Kind == TokenKind.Word && _vocab.Find(t.Text) is { Sections.Length: > 0 } entry)
            {
                if (entry.Sections.All(s => s.StartsWith("10.", StringComparison.Ordinal)))
                {
                    result.Add(new(ConstructKind.Breakage, t.Span, $"breakage symbol '{t.Text}' (ch. 10): not implemented in the pilot"));
                }
                else if (entry.Sections.All(s => s.StartsWith("12.", StringComparison.Ordinal)))
                {
                    result.Add(new(ConstructKind.Meiotic, t.Span, $"meiotic symbol '{t.Text}' (ch. 12): not implemented in the pilot"));
                }
            }

            if (t.IsUnderlined)
            {
                result.Add(new(ConstructKind.Underlining, t.Span, "underlining outside a chromosome number (T-042): not implemented in the pilot"));
            }
        }

        return result.ToImmutable();
    }

    internal static FrozenSet<string> Set(params string[] words) => words.ToFrozenSet(StringComparer.Ordinal);
}
