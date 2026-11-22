using CytoLex.Core.Semantics;
using static CytoLex.Core.Translation.Phrasing;

namespace CytoLex.Core.Translation;

/// <summary>Bands, breaks, breakpoint lists and arms in running text (templates: band, approximate_interval, alternative, short_arm, long_arm).</summary>
internal sealed class BandPhrases(RenderContext ctx)
{
    /// <summary>The chromosome label of a breakpoint: written, or from its group.</summary>
    public static string? ChromosomeName(Breakpoint b) => b.Chromosome is { IsUnknown: false } c ? c.Name : null;

    /// <summary>A known breakpoint (not <c>?</c>).</summary>
    public static bool IsKnown(Breakpoint b) => b.Landmark != "?";

    /// <summary>Two breakpoints in band order (proximal first on one arm; short arm first across arms).</summary>
    public static (Breakpoint From, Breakpoint To) Ordered(Breakpoint a, Breakpoint b) => IsAscending(a, b) ? (a, b) : (b, a);

    /// <summary>Whether <paramref name="a"/> → <paramref name="b"/> is written in ascending band order (a direct segment).</summary>
    public static bool IsAscending(Breakpoint a, Breakpoint b)
    {
        if (a.Arm != b.Arm)
        {
            return a.Arm == "p";
        }

        return string.CompareOrdinal(Sortable(a.BandNumber), Sortable(b.BandNumber)) <= 0;

        static string Sortable(string? digits)
        {
            if (digits is null)
            {
                return "";
            }

            var dot = digits.IndexOf('.', StringComparison.Ordinal);
            var whole = dot < 0 ? digits : digits[..dot];
            return whole.PadLeft(3, '0') + (dot < 0 ? "" : digits[dot..]);
        }
    }

    /// <summary>The opposite arm.</summary>
    public static string Opposite(string? arm) => arm == "p" ? "q" : "p";

    /// <summary>The words of an arm: <c>short arm</c> / <c>long arm</c> (forms: noun, hyphenated, plural).</summary>
    public string Arm(string? arm, string form = "noun") => ctx.P(arm == "p" ? "short_arm" : "long_arm", form);

    /// <summary>A band in running text: <c>5q13</c>, <c>q22</c> (no chromosome known), <c>1p31 of the underlined homologue</c> in lists.</summary>
    public string Band(Breakpoint b, bool homologue = false)
    {
        var name = b.Name ?? (ChromosomeName(b) ?? "") + b.Text;
        return homologue && b.Chromosome is { IsUnderlined: true } c && ctx.Homologues.Contains(c.Name)
            ? ctx.P("band", "of_underlined", ("band", name))
            : name;
    }

    /// <summary>Where a single break is: <c>at 5q13</c>, <c>somewhere from 6q21 to 6q23</c>, <c>in region 1p3, band unknown</c>.</summary>
    public string BreakAt(Breakpoint b)
    {
        if (b.Landmark == "?")
        {
            return ctx.P("band", "unknown_position");
        }

        if (b.RangeEnd is { } end)
        {
            return ctx.P("approximate_interval", null, ("from", Band(b)), ("to", Band(end)));
        }

        if (b.IsQuestioned && b.BandNumber is null && b.Arm is not null && b.Text.Length > 2)
        {
            return ctx.P("band", "region_unknown_band", ("region", (ChromosomeName(b) ?? "") + b.Text.TrimEnd('?')));
        }

        if (!b.Alternatives.IsEmpty)
        {
            return ctx.P("band", "at", ("band", ctx.P("alternative", "phrase", ("first", Band(b)), ("second", Band(b.Alternatives[0])))));
        }

        return ctx.P("band", "at", ("band", Band(b)));
    }

    /// <summary>
    /// The breakpoints of the groups: <c>2q21 and 5q31</c>; with a group of two, groups are joined with a serial comma
    /// (<c>1p36.1 and 1q32, 3q26.3 and 3p24, and 12p12 and 12q23</c>). Unknown breakpoints are left out.
    /// </summary>
    public string BreakpointList(IReadOnlyList<BreakpointGroup> groups, bool homologue = true)
    {
        var known = groups.Select(g => g.Breakpoints.Where(IsKnown).Select(b => Band(b, homologue)).ToList()).Where(g => g.Count > 0).ToList();
        return known.Any(g => g.Count > 1) ? SerialAnd([.. known.Select(g => And(g))]) : And([.. known.SelectMany(g => g)]);
    }

    /// <summary><c>with breaks at …</c> / <c>with a break at … (the other break unknown)</c>.</summary>
    public string WithBreaks(IReadOnlyList<BreakpointGroup> groups)
    {
        var all = groups.SelectMany(g => g.Breakpoints).ToList();
        var known = all.Count(IsKnown);
        var list = BreakpointList(groups);
        var phrase = ctx.P("band", known == 1 ? "with_break" : "with_breaks", ("breakpoints", list));
        return known < all.Count ? ctx.P("band", known == 1 ? "other_unknown" : "others_unknown", ("phrase", phrase)) : phrase;
    }
}
