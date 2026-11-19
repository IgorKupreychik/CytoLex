namespace CytoLex.Core.Rules.Checks;

/// <summary><c>RING.DIC_TRC_ORDER</c> (§9.2.15): <c>dic r</c>/<c>trc r</c> list the sex chromosome or the lowest-numbered one first, and the triplet and <c>r</c> are written apart (<see cref="ChromosomeOrder"/>).</summary>
[KbRule("RING.DIC_TRC_ORDER")]
internal sealed class RingDicTrcOrderCheck() : StructuralCheck(ChromosomeOrder.Analyze);
