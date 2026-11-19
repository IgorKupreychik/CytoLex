namespace CytoLex.Core.Rules.Checks;

/// <summary><c>DER.MULTIPLE_ABERRATIONS_ONE_CHROMOSOME</c> (§9.2.1, §9.2.3): several aberrations of one chromosome are written as a derivative (<see cref="Derivatives"/>).</summary>
[KbRule("DER.MULTIPLE_ABERRATIONS_ONE_CHROMOSOME")]
internal sealed class DerMultipleAberrationsCheck() : StructuralCheck(Derivatives.Analyze);
