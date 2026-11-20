namespace CytoLex.Core.Rules.Checks;

/// <summary><c>DER.WHOLE_ARM_TWO_CHROMOSOMES</c> (§9.2.17.2): <c>der(a;b)</c> for an unbalanced whole-arm translocation, replacing both chromosomes (<see cref="CentromericBreakpoints"/>, <see cref="ReplacedLosses"/>).</summary>
[KbRule("DER.WHOLE_ARM_TWO_CHROMOSOMES")]
internal sealed class DerWholeArmTwoChromosomesCheck() : StructuralCheck((m, i) => CentromericBreakpoints.Analyze(m, i).Concat(ReplacedLosses.SymbolFindings(m, i)));
