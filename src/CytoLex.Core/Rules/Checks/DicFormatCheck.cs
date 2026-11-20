namespace CytoLex.Core.Rules.Checks;

/// <summary><c>DIC.FORMAT</c> (§9.2.4): the chromosomes a dicentric replaces are not written as missing (<see cref="ReplacedLosses"/>).</summary>
[KbRule("DIC.FORMAT")]
internal sealed class DicFormatCheck() : StructuralCheck(ReplacedLosses.SymbolFindings);
