namespace CytoLex.Core.Rules.Checks;

/// <summary><c>STRUCT.INSERTION_RECEPTOR_FIRST</c> (§4.3.1.2, §9.2.9): in an insertion the receiving chromosome is listed first (<see cref="ChromosomeOrder"/>).</summary>
[KbRule("STRUCT.INSERTION_RECEPTOR_FIRST")]
internal sealed class InsertionReceptorFirstCheck() : StructuralCheck(ChromosomeOrder.Analyze);
