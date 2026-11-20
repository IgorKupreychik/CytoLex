using System.Globalization;
using CytoLex.Core.Syntax;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// <c>CP.FORMAT</c> (§11.1.5 ¶1): the total cell count of a composite karyotype is written <c>[cpN]</c>, cp first. Fires
/// on a cell count with <c>cp</c> after the number (<c>[7cp]</c>); fix: <c>[cp7]</c>.
/// </summary>
[KbRule("CP.FORMAT")]
internal sealed class CpFormatCheck : IRuleCheck
{
    public void Check(RuleContext context)
    {
        foreach (var cells in context.Model.CellLines.Select(l => l.CellCount).OfType<Semantics.CellCount>().Where(c => c.IsComposite && !c.IsPartial))
        {
            var inner = cells.Syntax!.PresentTokens().Where(t => t.Kind is not (TokenKind.OpenBracket or TokenKind.CloseBracket)).ToList();
            if (inner.Count == 0 || inner[0] is { Kind: TokenKind.Word, Text: "cp" })
            {
                continue;
            }

            var fix = cells.Cells is { } n ? FixProposal.Replace(cells.Span, string.Create(CultureInfo.InvariantCulture, $"[cp{n}]")) : null;
            context.ReportWithFix(cells.Span, $"'{cells.Span.Of(context.Input)}': the total of a composite karyotype is written with cp before the number{(cells.Cells is { } c ? string.Create(CultureInfo.InvariantCulture, $", [cp{c}]") : string.Empty)}.", fix);
        }
    }
}
