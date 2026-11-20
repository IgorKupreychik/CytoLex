namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// <c>CLONE.SECOND_STEMLINE</c> (§11.1.4 ¶2, E7; a warning): once a second stemline is referred to as <c>sl2</c> (or a
/// higher number), <c>sdl</c> is avoided after it, since it could be a sideline of either stemline. Fires at every
/// <c>sdl</c> after the first <c>sl2</c>+. Level-3 fix (the KB hint).
/// </summary>
[KbRule("CLONE.SECOND_STEMLINE")]
internal sealed class SecondStemlineCheck : IRuleCheck
{
    public void Check(RuleContext context)
    {
        Semantics.CloneReference? second = null;
        foreach (var r in context.Model.CellLines.Select(l => l.BasedOn).OfType<Semantics.CloneReference>())
        {
            if (r.Symbol == "sl" && r.Number >= 2)
            {
                second ??= r;
            }
            else if (r.Symbol == "sdl" && second is not null)
            {
                context.Report(
                    r.Item.Span,
                    $"'{r.Item.Span.Of(context.Input)}' after a second stemline ('{second.Item.Span.Of(context.Input)}') is ambiguous: refer to the second stemline as sl2 (e.g. sl2×2,+8), or use idem.",
                    [second.Item.Span]);
            }
        }
    }
}
