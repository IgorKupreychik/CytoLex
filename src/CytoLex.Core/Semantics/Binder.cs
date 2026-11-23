using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using CytoLex.Core.Composing;
using CytoLex.Core.Kb;
using CytoLex.Core.Syntax;
using CytoLex.Core.Text;

namespace CytoLex.Core.Semantics;

/// <summary>
/// Builds the semantic model of a karyotype segment from its syntax tree (docs/design/core.md §5, T-065).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>The binder interprets; it does not judge. It reports no diagnostics: a band missing from the band set, a
/// breakpoint group without a chromosome and the like are facts in the model, reported by the rules (T-029+).</item>
/// <item>Short and detailed systems give the same <see cref="Breakpoint"/>s: in the detailed system they are the band
/// ends at the joins (<c>::</c>, <c>:</c>). The band composition (<see cref="DerivativeStructure"/>) is kept as written;
/// <see cref="Abnormality.Composition"/> gives it for both systems (computed from the short system by the
/// <see cref="Composer"/>, T-066).</item>
/// <item>Every node built from recovered or skipped input carries those <see cref="SyntaxDiagnostic"/>s as
/// <see cref="MeaningNode.Issues"/> (partial); its ancestors too.</item>
/// <item>KB refs: each symbol's vocab item and its format rule(s) (<see cref="SymbolRules"/>), each band of the band
/// set, and the rule that defines a construct (mosaic prefix, detailed system, <c>or</c>, <c>~</c>, <c>c</c>, …). Only
/// rule IDs the edition's KB has are attached.</item>
/// </list>
/// </remarks>
public sealed class Binder
{
    /// <summary>
    /// The rule(s) that define each abnormality symbol's format. Each rule's section is one of the symbol's vocab sections
    /// (checked by a test), so the mapping traces to the KB.
    /// </summary>
    internal static readonly FrozenDictionary<string, ImmutableArray<string>> SymbolRules =
        new Dictionary<string, ImmutableArray<string>>(StringComparer.Ordinal)
        {
            ["ace"] = ["ACE.NOT_IN_KARYOTYPE"],
            ["add"] = ["ADD.FORMAT"],
            ["del"] = ["DEL.FORMAT"],
            ["der"] = ["DER.DEFINITION"],
            ["dic"] = ["DIC.FORMAT"],
            ["dmin"] = ["DMIN.FORMAT"],
            ["dup"] = ["DUP.FORMAT"],
            ["fis"] = ["FIS.FORMAT"],
            ["fra"] = ["FRA.FORMAT"],
            ["hsr"] = ["HSR.FORMAT"],
            ["i"] = ["ISO.FORMAT"],
            ["idic"] = ["DIC.FORMAT", "ISO.FORMAT"],
            ["ider"] = ["DER.ISODERIVATIVE"],
            ["inc"] = ["UNCERTAIN.INC_AT_END"],
            ["ins"] = ["INS.ORIENTATION"],
            ["inv"] = ["INV.FORMAT"],
            ["mar"] = ["MAR.PLUS_SIGN"],
            ["neo"] = ["NEO.FORMAT"],
            ["psu"] = ["DIC.PSEUDODICENTRIC_ORDER"],
            ["qdp"] = ["QDP.FORMAT"],
            ["r"] = ["RING.FORMAT"],
            ["rec"] = ["REC.FORMAT"],
            ["rob"] = ["ROB.FORMAT"],
            ["t"] = ["TRANSLOC.FORMAT"],
            ["tas"] = ["TAS.FORMAT"],
            ["trc"] = ["TRC.FORMAT"],
            ["trp"] = ["TRP.FORMAT"],
            ["upd"] = ["UPD.FORMAT"],
        }.ToFrozenDictionary(StringComparer.Ordinal);

    // Rules that define constructs (not symbols); attached when the edition's KB has them.
    internal const string MosaicChimeraPrefix = "KARYO.MOSAIC_CHIMERA_PREFIX";
    internal const string Endoreduplication = "KARYO.ENDOREDUPLICATION";
    internal const string TransplantChimera = "KARYO.TRANSPLANT_CHIMERA";
    internal const string CountSexOrder = "KARYO.COUNT_SEX_ORDER";
    internal const string PloidyInAngleBrackets = "NUM.PLOIDY_IN_ANGLE_BRACKETS";
    internal const string ConstitutionalC = "KARYO.CONSTITUTIONAL_C";
    internal const string ParentalOrigin = "KARYO.PARENTAL_ORIGIN";
    internal const string MultipleCopies = "KARYO.MULTIPLY_SIGN_REARRANGED_ONLY";
    internal const string GainLossSign = "KARYO.GAIN_LOSS_SIGN";
    internal const string QuestionableItem = "UNCERTAIN.QUESTIONABLE_ITEM";
    internal const string QuestionMarkDesignation = "UNCERTAIN.QUESTION_MARK_DESIGNATION";
    internal const string ApproxInterval = "UNCERTAIN.APPROX_INTERVAL";
    internal const string OrAlternative = "UNCERTAIN.OR_ALTERNATIVE";
    internal const string DetailedSystem = "STRUCT.DETAILED_SYSTEM";
    internal const string BreakpointGroups = "STRUCT.BREAKPOINT_GROUPS";
    internal const string VarDefinition = "VAR.DEFINITION";
    internal const string VarLengthSign = "VAR.LENGTH_SIGN";

    /// <summary>Every construct rule ID the binder can attach (a test checks they exist in the KB).</summary>
    internal static readonly ImmutableArray<string> ConstructRuleIds =
    [
        MosaicChimeraPrefix, Endoreduplication, TransplantChimera, CountSexOrder, PloidyInAngleBrackets, ConstitutionalC,
        ParentalOrigin, MultipleCopies, GainLossSign, QuestionableItem, QuestionMarkDesignation, ApproxInterval, OrAlternative,
        DetailedSystem, BreakpointGroups, VarDefinition, VarLengthSign,
        GenomeBuildRequired, ArrRegionFormat, ArrNucleotideFormat, ArrCopyNumberRule, ArrComplex, ArrChromothripsis,
        ArrMosaicProportion, ArrInheritanceSpacing, ArrZygosity, ArrAberrationsOnly,
        IshStructuralFormat, IshLocusList, IshLocusDesignation, IshCellLines, IshNormalChromosomeFormat, NucIshSignalCountFormat,
        NucIshSignalPositionFormat, FibIshFormat, RevIshFormat, CghFormat,
    ];

    private readonly IEditionKb _kb;

    public Binder(IEditionKb kb)
    {
        ArgumentNullException.ThrowIfNull(kb);
        _kb = kb;
    }

    /// <summary>The meaning of <paramref name="tree"/>.</summary>
    public KaryotypeModel Bind(SyntaxTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        return new KaryotypeBinder(_kb, tree).BindRoot();
    }

    internal const string GenomeBuildRequired = "GEN.GENOME_BUILD_REQUIRED";
    internal const string ArrRegionFormat = "ARR.REGION_FORMAT";
    internal const string ArrNucleotideFormat = "ARR.NUCLEOTIDE_FORMAT";
    internal const string ArrCopyNumberRule = "ARR.COPY_NUMBER";
    internal const string ArrComplex = "ARR.COMPLEX_CX";
    internal const string ArrChromothripsis = "ARR.CHROMOTHRIPSIS";
    internal const string ArrMosaicProportion = "ARR.MOSAIC_PROPORTION";
    internal const string ArrInheritanceSpacing = "ARR.INHERITANCE_SPACING";
    internal const string ArrZygosity = "ARR.ZYGOSITY";
    internal const string ArrAberrationsOnly = "ARR.ABERRATIONS_ONLY";
    internal const string IshStructuralFormat = "ISH.STRUCTURAL_FORMAT";
    internal const string IshLocusList = "ISH.LOCUS_LIST";
    internal const string IshLocusDesignation = "ISH.LOCUS_DESIGNATION";
    internal const string IshCellLines = "ISH.CELL_LINES";
    internal const string IshNormalChromosomeFormat = "ISH.NORMAL_CHROMOSOME_FORMAT";
    internal const string NucIshSignalCountFormat = "NUC_ISH.SIGNAL_COUNT_FORMAT";
    internal const string NucIshSignalPositionFormat = "NUC_ISH.SIGNAL_POSITION_FORMAT";
    internal const string FibIshFormat = "FIB_ISH.FORMAT";
    internal const string RevIshFormat = "REV_ISH.FORMAT";
    internal const string CghFormat = "CGH.FORMAT";

    /// <summary>The meaning of an arr segment's <paramref name="tree"/> (root <see cref="SyntaxKind.ArrResult"/>).</summary>
    /// <exception cref="ArgumentException">The tree is not an arr tree.</exception>
    public ArrModel BindArr(SyntaxTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        if (tree.Root.Kind != SyntaxKind.ArrResult)
        {
            throw new ArgumentException($"Not an arr syntax tree (root {tree.Root.Kind}).", nameof(tree));
        }

        return new ArrBinder(_kb, tree).BindArrRoot();
    }

    /// <summary>The meaning of an in situ segment's <paramref name="tree"/> (root <see cref="SyntaxKind.FishResult"/>, T-033).</summary>
    /// <exception cref="ArgumentException">The tree is not an in situ tree.</exception>
    public FishModel BindFish(SyntaxTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        if (tree.Root.Kind != SyntaxKind.FishResult)
        {
            throw new ArgumentException($"Not an in situ syntax tree (root {tree.Root.Kind}).", nameof(tree));
        }

        return new FishBinder(_kb, tree, new KaryotypeBinder(_kb, tree)).BindRoot();
    }

    /// <summary>The meaning of <paramref name="tree"/>, by its root: an arr, in situ or karyotype model.</summary>
    public SegmentModel BindSegment(SyntaxTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        return tree.Root.Kind switch
        {
            SyntaxKind.ArrResult => BindArr(tree),
            SyntaxKind.FishResult => BindFish(tree),
            _ => Bind(tree),
        };
    }
}
