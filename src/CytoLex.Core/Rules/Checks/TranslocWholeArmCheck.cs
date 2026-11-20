namespace CytoLex.Core.Rules.Checks;

/// <summary><c>TRANSLOC.WHOLE_ARM</c> (§9.2.17.2): whole-arm breakpoints are p10/q10, the first chromosome's p10 in a balanced exchange (<see cref="CentromericBreakpoints"/>).</summary>
[KbRule("TRANSLOC.WHOLE_ARM")]
internal sealed class TranslocWholeArmCheck() : StructuralCheck(CentromericBreakpoints.Analyze);
