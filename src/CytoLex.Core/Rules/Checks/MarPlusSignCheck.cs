namespace CytoLex.Core.Rules.Checks;

/// <summary><c>MAR.PLUS_SIGN</c> (§9.2.12): a marker chromosome is written with a plus sign (<see cref="Unidentified"/>).</summary>
[KbRule("MAR.PLUS_SIGN")]
internal sealed class MarPlusSignCheck() : StructuralCheck(Unidentified.Analyze);
