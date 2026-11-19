namespace CytoLex.Core.Rules.Checks;

/// <summary><c>DER.DICENTRIC_FORMAT</c> (§9.2.3): a dicentric chromosome with further aberrations is <c>der(a;b)</c> (<see cref="Derivatives"/>).</summary>
[KbRule("DER.DICENTRIC_FORMAT")]
internal sealed class DerDicentricFormatCheck() : StructuralCheck(Derivatives.Analyze);
