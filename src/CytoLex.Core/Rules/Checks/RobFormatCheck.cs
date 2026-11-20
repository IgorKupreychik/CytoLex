namespace CytoLex.Core.Rules.Checks;

/// <summary><c>ROB.FORMAT</c> (§9.2.17.3): <c>rob</c> joins acrocentric chromosomes and replaces both (<see cref="CentromericBreakpoints"/>, <see cref="ReplacedLosses"/>).</summary>
[KbRule("ROB.FORMAT")]
internal sealed class RobFormatCheck() : StructuralCheck((m, i) => CentromericBreakpoints.Analyze(m, i).Concat(ReplacedLosses.SymbolFindings(m, i)));
