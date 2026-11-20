namespace CytoLex.Core.Rules.Checks;

/// <summary><c>RING.UNIDENTIFIED</c> (§9.2.15): <c>+r</c> for a ring of unknown origin, before any marker; a ring of known origin in number order (<see cref="Unidentified"/>).</summary>
[KbRule("RING.UNIDENTIFIED")]
internal sealed class RingUnidentifiedCheck() : StructuralCheck(Unidentified.Analyze);
