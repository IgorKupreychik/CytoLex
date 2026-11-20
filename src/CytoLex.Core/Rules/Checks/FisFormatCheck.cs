namespace CytoLex.Core.Rules.Checks;

/// <summary><c>FIS.FORMAT</c> (§9.2.6): the breakpoint of a centric fission is p10 or q10 (<see cref="CentromericBreakpoints"/>).</summary>
[KbRule("FIS.FORMAT")]
internal sealed class FisFormatCheck() : StructuralCheck(CentromericBreakpoints.Analyze);
