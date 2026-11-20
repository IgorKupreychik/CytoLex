namespace CytoLex.Core.Rules.Checks;

/// <summary><c>HSR.INTERFACE_TWO_CHROMOSOMES</c> (§9.2.8): an hsr at the interface of two chromosomes names both (<see cref="Hsr"/>).</summary>
[KbRule("HSR.INTERFACE_TWO_CHROMOSOMES")]
internal sealed class HsrInterfaceTwoChromosomesCheck() : StructuralCheck((m, _) => Hsr.Analyze(m));
