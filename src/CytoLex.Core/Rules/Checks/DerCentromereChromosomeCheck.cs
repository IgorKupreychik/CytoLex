namespace CytoLex.Core.Rules.Checks;

/// <summary><c>DER.CENTROMERE_CHROMOSOME</c> (§4.4): <c>der(n)</c> names the chromosome with the centromere; checked on band compositions (<see cref="Composition"/>).</summary>
[KbRule("DER.CENTROMERE_CHROMOSOME")]
internal sealed class DerCentromereChromosomeCheck() : StructuralCheck(Composition.Analyze);
