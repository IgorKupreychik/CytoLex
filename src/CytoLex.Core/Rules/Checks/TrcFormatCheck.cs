namespace CytoLex.Core.Rules.Checks;

/// <summary><c>TRC.FORMAT</c> (§9.2.18): a tricentric chromosome starts with the end chromosome that is a sex chromosome or has the lowest number (<see cref="ChromosomeOrder"/>); only the order is checked.</summary>
[KbRule("TRC.FORMAT")]
internal sealed class TrcFormatCheck() : StructuralCheck(ChromosomeOrder.Analyze);
