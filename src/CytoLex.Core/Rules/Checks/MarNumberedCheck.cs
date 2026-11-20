namespace CytoLex.Core.Rules.Checks;

/// <summary><c>MAR.NUMBERED</c> (§9.2.12): copies of a numbered marker are written <c>marN×n</c> (<see cref="Unidentified"/>).</summary>
[KbRule("MAR.NUMBERED")]
internal sealed class MarNumberedCheck() : StructuralCheck(Unidentified.Analyze);
