namespace CytoLex.Core.Rules.Checks;

/// <summary><c>DER.ISODERIVATIVE</c> (§9.2.3): the breakpoint of <c>ider</c> is p10 or q10 (<see cref="CentromericBreakpoints"/>).</summary>
[KbRule("DER.ISODERIVATIVE")]
internal sealed class DerIsoderivativeCheck() : StructuralCheck(CentromericBreakpoints.Analyze);
