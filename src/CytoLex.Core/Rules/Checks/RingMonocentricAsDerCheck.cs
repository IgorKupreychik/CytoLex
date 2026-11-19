namespace CytoLex.Core.Rules.Checks;

/// <summary><c>RING.MONOCENTRIC_AS_DER</c> (§9.2.15): a monocentric ring of several chromosomes is <c>der(n)r(n;…)</c> (<see cref="ChromosomeOrder"/>).</summary>
[KbRule("RING.MONOCENTRIC_AS_DER")]
internal sealed class RingMonocentricAsDerCheck() : StructuralCheck(ChromosomeOrder.Analyze);
