namespace CytoLex.Core.Rules.Checks;

/// <summary><c>DMIN.FORMAT</c> (§9.2.12): no sign before <c>dmin</c>, and double minutes after the markers (<see cref="Unidentified"/>); <c>dmin14</c> is the parser's.</summary>
[KbRule("DMIN.FORMAT")]
internal sealed class DminFormatCheck() : StructuralCheck(Unidentified.Analyze);
