namespace CytoLex.Core.Syntax;

/// <summary>
/// Locus groups after an abnormality's parentheses (ish, §13.2, T-033): <c>del(22)(q11.2q11.2)(D22S75–)</c>,
/// <c>ins(2)(wcp2+)</c> (breakpoints left out), <c>inv(16)(p13.1)(5′CBFB+)(q22)(3′CBFB+)</c>,
/// <c>der(4)t(4;11)(p16.3;p15)mat(wcp11+,…)</c>. The <see cref="KaryotypeGrammar"/> asks before each parenthesis group and
/// after each abnormality part and suffix.
/// </summary>
internal interface IAbnormalityLoci
{
    /// <summary>The current <c>(</c> opens a locus group (or a breakpoint group with loci written inside it).</summary>
    bool AtLocusGroup();

    /// <summary>Parses the locus groups at the current position (and breakpoint groups between them); true when it parsed any.</summary>
    bool ParseLoci(List<SyntaxElement> children, KaryotypeGrammar karyotype);
}
