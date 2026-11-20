using System.Globalization;
using CytoLex.Core.Semantics;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// <c>CLONE.SUBCLONE_REFERENCE</c> (§11.1.4 ¶1–2): a subclone is written relative to an earlier clone with <c>idem</c>
/// (the first karyotype), <c>sl</c> (the stemline) or <c>sdl</c> (the sideline before, or the N-th with <c>sdlN</c>).
/// Fires on (a) a reference with nothing to refer to (on the first line, <c>sdl</c> before any sideline, <c>sl2</c> with no
/// second clone written in full), and (b) a count that does not match the referenced clone's count (× the
/// <c>×n</c> of a polyploid subclone) plus the subclone's own changes, all signs being relative to the clone referred to.
/// (b) is how a missing repeat with <c>idem</c> shows (<c>…/47,idem,+8/48,idem,+9</c>); it is reported here, not as
/// <c>NUM.COUNT_MATCHES_CHANGES</c>, which skips reference lines (one fault, one finding). Skipped for count ranges,
/// composite (<c>[cp]</c>) lines and partial input. Level-3 fix (the KB hint).
/// </summary>
[KbRule("CLONE.SUBCLONE_REFERENCE")]
internal sealed class SubcloneReferenceCheck : IRuleCheck
{
    public void Check(RuleContext context)
    {
        var lines = context.Model.CellLines;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.BasedOn is not { } reference)
            {
                continue;
            }

            var word = reference.Item.Span.Of(context.Input);
            if (reference.TargetIndex is not { } t)
            {
                var detail = i == 0
                    ? $"'{word}' refers to an earlier clone, but this is the first one: the stemline is listed first and written in full."
                    : reference.Symbol == "sdl"
                        ? $"'{word}' refers to a sideline listed before it, but there is none."
                        : $"'{word}' refers to stemline {reference.Number}, but there are not that many clones written in full before it.";
                context.Report(reference.Item.Span, detail);
                continue;
            }

            CheckCount(context, line, reference, lines[t]);
        }
    }

    private static void CheckCount(RuleContext context, CellLine line, CloneReference reference, CellLine target)
    {
        if (line.DescendantsAndSelf().Any(n => n.IsPartial) || target.IsPartial
            || line.Count is not { From: { } count, IsRange: false, IsMissing: false }
            || target.Count is not { From: { } targetCount, IsRange: false, IsMissing: false }
            || line.CellCount is { IsComposite: true } || target.CellCount is { IsComposite: true }
            || reference.Multiplier is not { } multiplier)
        {
            return;
        }

        var own = line.Items.Where(item => !ReferenceEquals(item, reference.Item));
        if (ChromosomeCount.RelativeDelta(own) is not { } delta)
        {
            return;
        }

        var basis = targetCount * multiplier;
        if (delta.Values.Contains(count - basis))
        {
            return;
        }

        var times = multiplier == 1 ? string.Empty : string.Create(CultureInfo.InvariantCulture, $" × {multiplier}");
        var changes = string.Join(" or ", delta.Values.Select(v => v.ToString("+0;−0;±0", CultureInfo.InvariantCulture)));
        var hint = reference.Symbol == "idem"
            ? "with idem, every change beyond the first karyotype is repeated in each subclone"
            : "the signs are relative to the clone referred to";
        context.Report(
            line.Count.Span,
            string.Create(CultureInfo.InvariantCulture, $"{count} chromosomes do not match the clone referred to ({targetCount}{times}) with the changes listed ({changes}): {hint}."),
            [reference.Item.Span, target.Count.Span],
            delta.Certain ? Certainty.Certain : Certainty.Uncertain);
    }
}
