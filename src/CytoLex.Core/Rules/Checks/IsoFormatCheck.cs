namespace CytoLex.Core.Rules.Checks;

/// <summary><c>ISO.FORMAT</c> (§9.2.11): <c>i(chromosome)(p10|q10)</c>: no arm in the chromosome list (<see cref="SymbolForms"/>), a centromeric breakpoint (<see cref="CentromericBreakpoints"/>); <c>iso</c> is the parser's.</summary>
[KbRule("ISO.FORMAT")]
internal sealed class IsoFormatCheck() : StructuralCheck((m, i) => SymbolForms.Analyze(m, i).Concat(CentromericBreakpoints.Analyze(m, i)));
