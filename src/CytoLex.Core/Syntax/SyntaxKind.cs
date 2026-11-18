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

    /// <summary><c>…</c>: abnormalities left out.</summary>
    Ellipsis,

    /// <summary>Tokens skipped by error recovery (kept for losslessness).</summary>
    Skipped,
}
