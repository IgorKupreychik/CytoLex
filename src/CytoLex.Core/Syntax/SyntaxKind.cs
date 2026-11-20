namespace CytoLex.Core.Syntax;

/// <summary>Node kinds of the karyotype syntax tree (docs/design/core.md §4).</summary>
public enum SyntaxKind
{
    /// <summary>Root of a whole karyotype segment.</summary>
    Karyotype,

    /// <summary>Root of a book fragment (<c>inv(2)</c>, <c>1p31</c>, <c>43~47,XX,…</c>): the header is optional.</summary>
    Fragment,

    /// <summary><c>mos</c>, <c>chi</c> or <c>end</c> before the cell lines.</summary>
    Prefix,

    /// <summary>A <c>mos</c>/<c>chi</c>/<c>end</c> written after the karyotype (recovered).</summary>
    MisplacedPrefix,

    /// <summary>One cell line: count, sex chromosomes, abnormalities, cell count.</summary>
    CellLine,

    /// <summary><c>/</c> or <c>//</c> between cell lines (or a comma recovered as one).</summary>
    CellLineSeparator,

    /// <summary>The chromosome count, incl. ranges (<c>45~48</c>).</summary>
    ChromosomeCount,

    /// <summary>A ploidy level <c>&lt;2n&gt;</c>.</summary>
    Ploidy,

    /// <summary>The sex chromosome field (<c>XXY</c>, <c>Xc</c>, <c>XXX?c</c>).</summary>
    SexChromosomes,

    /// <summary>One comma-separated item; holds one <see cref="Alternative"/> per <c>or</c> branch, or a single core.</summary>
    Item,

    /// <summary>Two or more readings joined by <c>or</c> (§5.3).</summary>
    Alternative,

    /// <summary>A rearrangement or other symbol-led abnormality (<c>del(5)(q13q33)</c>, <c>der(1)t(1;3)(p32;q21)…</c>, <c>+mar</c>).</summary>
    Abnormality,

    /// <summary>One symbol group of an abnormality with its parentheses (<c>t(1;3)(p32;q21)</c> inside a <c>der</c>).</summary>
    AbnormalityPart,

    /// <summary>The symbol words of a part (<c>psu dic</c>, <c>dic r</c>, <c>upd der</c>).</summary>
    Symbols,

    /// <summary>A number after the symbol (<c>mar1</c>, <c>r2</c>).</summary>
    SymbolNumber,

    /// <summary>A copy count before the symbol (<c>3mar</c>, <c>12~20dmin</c>, <c>5r</c>).</summary>
    LeadingCount,

    /// <summary>Gain, loss or questioned whole chromosome (<c>+21</c>, <c>–X</c>, <c>–?21</c>).</summary>
    WholeChromosome,

    /// <summary>A chromosome region with variant features or a band (<c>16qh+</c>, <c>14cenh+pstk+ps+</c>, <c>1p31</c>).</summary>
    ChromosomeRegion,

    /// <summary>Signs before an item: <c>+</c>, <c>–</c>, <c>?</c>.</summary>
    Signs,

    /// <summary>Parenthesised chromosome list (<c>(1;3)</c>, <c>(5q)</c>, <c>(?)</c>).</summary>
    ChromosomeList,

    /// <summary>A chromosome without parentheses where they are required (recovered).</summary>
    BareChromosome,

    /// <summary>One chromosome in a list, optionally with an arm or band (<c>2p</c> in <c>dup(2p)</c>).</summary>
    ChromosomeSpec,

    /// <summary>Parenthesised breakpoints, short or detailed system (<c>(q13q33)</c>, <c>(pter→q13::q33→qter)</c>).</summary>
    BreakpointList,

    /// <summary>The breakpoints of one chromosome, between semicolons.</summary>
    BreakpointGroup,

    /// <summary>A band or region (<c>q13</c>, <c>3q13.1</c>, <c>q2?3</c>, <c>q21~24</c>).</summary>
    Band,

    /// <summary>A named point in a band composition: <c>pter</c>, <c>qter</c>, <c>cen</c>, <c>ter</c>, <c>hsr</c>, <c>neo</c>, <c>?</c>.</summary>
    Landmark,

    /// <summary>A suffix: copies <c>×2</c>, constitutional <c>c</c>/<c>?c</c>, inheritance <c>mat</c>/<c>pat</c>/<c>inh</c>/<c>dn</c>.</summary>
    Suffix,

    /// <summary>A cell count <c>[20]</c> or <c>[cp10]</c>.</summary>
    CellCount,

    /// <summary>The number of cells with one abnormality, after it: <c>del(3)(p12)[2]</c> (§11.1.5 ¶2; inside the <see cref="Item"/>).</summary>
    ItemCellCount,

    /// <summary><c>…</c>: abnormalities left out.</summary>
    Ellipsis,

    // ── Microarray (ch. 14, T-034) ──────────────────────────────────────────────────────────────────

    /// <summary>Root of an arr segment: <c>arr</c>, the genome build, the aberrations separated by commas.</summary>
    ArrResult,

    /// <summary>The genome build in square brackets after <c>arr</c> (<c>[GRCh38]</c>).</summary>
    GenomeBuild,

    /// <summary>One comma-separated array result: a chromosome group or region(s), then the copy number, proportion and qualifiers.</summary>
    ArrAberration,

    /// <summary>Whole chromosomes or arms in parentheses (<c>(1–22,X)</c>, <c>(16q)</c>, <c>(8,9p)</c>); entries are <see cref="ChromosomeSpec"/>s.</summary>
    ArrChromosomeGroup,

    /// <summary>Regions with their positions grouped in parentheses before a shared result (<c>(15q11.2q26.3(…),21q11.21q22.3(…))×2</c>).</summary>
    ArrRegionGroup,

    /// <summary>A chromosome with its bands (<c>6q21q25.1</c>, <c>13q14.2qter</c>, <c>Xq28 or Yq12</c>) and the positions.</summary>
    ArrRegion,

    /// <summary>Nucleotide positions in parentheses: short <c>(a_b)</c>, detailed <c>(a×2,b_c×1,d×2)</c>, <c>(a_b or c_d)</c>.</summary>
    Positions,

    /// <summary>One position or span of positions, with its copy number in the detailed system.</summary>
    PositionSpan,

    /// <summary>The copy number: <c>×n</c>, <c>×n~m</c>, or <c>amp</c>, <c>cx</c>, <c>cth</c>.</summary>
    CopyNumber,

    /// <summary>The proportion of abnormal cells in square brackets (<c>[0.6]</c>).</summary>
    Proportion,

    /// <summary>A word after the copy number: <c>mos</c>, <c>htz</c>/<c>hmz</c>, <c>mat</c>/<c>pat</c>/<c>inh</c>/<c>dn</c>, <c>c</c>.</summary>
    ArrQualifier,

    /// <summary>Tokens skipped by error recovery (kept for losslessness).</summary>
    Skipped,
}
