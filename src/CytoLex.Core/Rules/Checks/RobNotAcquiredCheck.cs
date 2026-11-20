namespace CytoLex.Core.Rules.Checks;

/// <summary><c>ROB.NOT_ACQUIRED</c> (§9.2.17.3): <c>rob</c> is not used for acquired abnormalities; conditional by the KB context (<see cref="CentromericBreakpoints"/>).</summary>
[KbRule("ROB.NOT_ACQUIRED")]
internal sealed class RobNotAcquiredCheck() : StructuralCheck(CentromericBreakpoints.Analyze);
