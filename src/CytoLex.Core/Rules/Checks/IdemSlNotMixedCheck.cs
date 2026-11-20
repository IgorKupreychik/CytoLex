namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// <c>CLONE.IDEM_SL_NOT_MIXED</c> (§11.1.2 E3 caption): one sample uses either <c>idem</c> or <c>sl</c>/<c>sdl</c>. Fires
/// once, at the first reference written in the style that appears second. Level-3 fix (the KB hint).
/// </summary>
[KbRule("CLONE.IDEM_SL_NOT_MIXED")]
internal sealed class IdemSlNotMixedCheck : IRuleCheck
{
    public void Check(RuleContext context)
    {
        var refs = context.Model.CellLines.Select(l => l.BasedOn).OfType<Semantics.CloneReference>().ToList();
        if (refs.Count == 0)
        {
            return;
        }

        var firstIsIdem = refs[0].Symbol == "idem";
        if (refs.FirstOrDefault(r => (r.Symbol == "idem") != firstIsIdem) is { } other)
        {
            context.Report(
                other.Item.Span,
                $"'{other.Item.Span.Of(context.Input)}' mixes {(firstIsIdem ? "sl/sdl" : "idem")} into a karyotype written with {(firstIsIdem ? "idem" : "sl/sdl")} ('{refs[0].Item.Span.Of(context.Input)}').",
                [refs[0].Item.Span]);
        }
    }
}
