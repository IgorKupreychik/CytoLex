namespace CytoLex.Core.Rules.Checks;

/// <summary><c>ROB.DICENTRIC_USE_DIC</c> (§9.2.17.3): a whole-arm derivative with breakpoints outside the centromere is a dicentric, written <c>dic</c> (<see cref="CentromericBreakpoints"/>).</summary>
[KbRule("ROB.DICENTRIC_USE_DIC")]
internal sealed class RobDicentricUseDicCheck() : StructuralCheck(CentromericBreakpoints.Analyze);
