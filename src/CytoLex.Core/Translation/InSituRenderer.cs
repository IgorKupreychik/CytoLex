using CytoLex.Core.Segmentation;
using CytoLex.Core.Semantics;
using CytoLex.Core.Syntax;
using static CytoLex.Core.Translation.Phrasing;

namespace CytoLex.Core.Translation;

/// <summary>
/// The summary and the results of an in situ segment (T-093; templates: in_situ, in_situ_cell_line, probe_results,
/// normal_chromosome, fiber_ish, reverse_ish, subtelomeric). The technique is named in the summary only. An ish
/// abnormality is the karyotype sentence of its symbols (<see cref="ItemRenderer"/>) followed by its probe results.
/// </summary>
internal sealed class InSituRenderer(RenderContext ctx, BandPhrases bands, ItemRenderer items, LocusPhrases loci)
{
    /// <summary>The summary sentence of an in situ segment: technique, cell lines and the number of results.</summary>
    public string Summary(FishModel model)
    {
        var technique = P("in_situ", model.Type.ToKbName());
        var n = model.CellLines.Sum(l => l.Items.Length);
        var results = n switch
        {
            0 => P("in_situ", "no_result"),
            1 => P("in_situ", "one_result"),
            _ => P("in_situ", "results", ("n", N(n))),
        };

        if (model.CellLines.Length >= 2)
        {
            var relation = model.Relation switch
            {
                CellLineRelation.Mosaic => "relation_mosaic",
                CellLineRelation.Chimera => "relation_chimera",
                CellLineRelation.Transplant => "relation_transplant",
                _ => null,
            };
            var count = N(model.CellLines.Length);
            return relation is null
                ? P("in_situ", "lines", ("technique", technique), ("n", count), ("results", results))
                : P("in_situ", "lines_relation", ("technique", technique), ("relation", P("in_situ", relation)), ("n", count), ("results", results));
        }

        var side = model.StartsWithDoubleSlash ? "donor" : EndsWithDoubleSlash(model) ? "recipient" : null;
        return side is null
            ? P("in_situ", "summary", ("technique", technique), ("results", results))
            : P("in_situ", "summary_side", ("technique", technique), ("side", P("transplant_chimera", side)), ("results", results));
    }

    /// <summary>The sentence of one result of cell line <paramref name="lineIndex"/> (without the final full stop).</summary>
    public string Item(FishModel model, int lineIndex, FishItem item)
    {
        var text = item switch
        {
            { Abnormality: Abnormality { Kind: "subtel" } a } => Subtelomeric(a, item),
            { Abnormality: { } abnormality } => Abnormality(abnormality, item),
            { Region: { } region } => Region(model, region),
            _ => P("summary", "not_translated_unparsed"),
        };

        if (item.Cells is { } cells)
        {
            text += "; " + loci.Cells(cells);
        }

        return model.CellLines.Length >= 2 ? LinePrefix(model, lineIndex, text) : text;
    }

    /// <summary>An ish abnormality: the karyotype sentence and the probe results of its locus groups.</summary>
    private string Abnormality(KaryotypeItem abnormality, FishItem item)
    {
        ctx.BeginItem(abnormality);
        var text = items.Text(abnormality);
        var results = ProbeResults(abnormality as Abnormality, item);
        return results.Length == 0 ? text : text + "; " + results;
    }

    /// <summary>
    /// The probe results of an ish abnormality: one list (<c>probes SNRPN and D15S10 absent</c>); one list per chromosome
    /// for <c>;</c>-separated lists (<c>on the derivative chromosome 9: …; on the derivative chromosome 22: …</c>); one per
    /// breakpoint (<c>at 16p13.1: …; at 16q22: …</c>) or per part of a derivative (<c>for dup(4)(q31q34): …</c>).
    /// </summary>
    private string ProbeResults(Abnormality? a, FishItem item)
    {
        var groups = item.Groups.Select((g, i) => (Group: g, Index: i)).Where(x => x.Group.Loci is not null).ToList();
        if (groups.Count == 0)
        {
            return "";
        }

        var differentParts = groups.Select(x => x.Group.Part).Distinct().Count() > 1;
        var afterBreakpoints = groups.Count >= 2 && groups.Any(x => x.Index > 0 && item.Groups[x.Index - 1].Kind == FishGroupKind.Breakpoints);
        if (!differentParts && !afterBreakpoints)
        {
            // One part: its lists (subtelomeric bands, then clones: t(13;20)(q34–,p13+;…)(RP11-63L17–,…;…)) are read per chromosome.
            var part = PartOf(a, groups[0].Group);
            var sides = groups.Select(x => Sides(x.Group.Loci!)).ToList();
            if (part is not null && sides[0].Count >= 2 && sides.All(s => s.Count == sides[0].Count))
            {
                var form = part.Head is "t" or "ins" ? "on_derivative" : "for_chromosome";
                var shared = groups.Count == 1 ? groups[0].Group.Loci!.SharedCount : null;
                return string.Join("; ", Enumerable.Range(0, sides[0].Count).Select(i => P(
                    "probe_results",
                    form,
                    ("chromosome", i < part.Chromosomes.Length ? part.Chromosomes[i].Name : "?"),
                    ("loci", loci.Statuses([.. sides.SelectMany(s => s[i])], shared)))));
            }

            return List([.. groups.Select(x => x.Group.Loci!)]);
        }

        var phrases = new List<string>();
        foreach (var (g, index) in groups)
        {
            var list = loci.Statuses(loci.Entries(g.Loci!), g.Loci!.SharedCount);
            var part = PartOf(a, g);
            if (differentParts && part is not null)
            {
                var written = ctx.Written(part);
                var end = g.Span.Start - part.Span.Start;
                phrases.Add(P("probe_results", "for_part", ("part", end > 0 && end <= written.Length ? written[..end] : written), ("loci", list)));
            }
            else if (index > 0 && item.Groups[index - 1] is { Kind: FishGroupKind.Breakpoints } bp && part is not null)
            {
                var chromosome = part.Chromosomes.IsEmpty ? "" : part.Chromosomes[0].Name;
                phrases.Add(P("probe_results", "at_band", ("band", chromosome + Inner(bp.Span)), ("loci", list)));
            }
            else
            {
                phrases.Add(list);
            }
        }

        return string.Join("; ", phrases);
    }

    /// <summary>The abnormality part a parenthesis group follows.</summary>
    private static AbnormalityPart? PartOf(Abnormality? a, FishGroup g) =>
        a is null || a.Parts.IsEmpty ? null : a.Parts[Math.Clamp(g.Part, 0, a.Parts.Length - 1)];

    /// <summary><c>probe D22S75 absent</c> / <c>probes … </c>; a counted list is given as it is (<c>4 signals for RUNX1</c>).</summary>
    private string List(IReadOnlyList<LocusGroup> groups)
    {
        var entries = groups.SelectMany(loci.Entries).ToList();
        var shared = groups.Count == 1 ? groups[0].SharedCount : null;
        var list = loci.Statuses(entries, shared);
        if (groups.Any(g => g.SharedCount is not null) || entries.Any(e => e.Locus.Count is not null))
        {
            return list;
        }

        return P("probe_results", entries.Count == 1 ? "one" : "several", ("loci", list));
    }

    /// <summary>The loci of a group split at <c>;</c> (one list per chromosome), as named entries.</summary>
    private List<List<(string Name, Locus Locus)>> Sides(LocusGroup group)
    {
        var sides = new List<List<(string Name, Locus Locus)>>();
        var start = 0;
        for (var i = 0; i <= group.Loci.Length - 1; i++)
        {
            var last = i == group.Loci.Length - 1;
            if (last || group.Separators[i] == LocusSeparator.Semicolon)
            {
                var count = i - start + 1;
                sides.Add(loci.Entries([.. group.Loci.Skip(start).Take(count)], [.. group.Separators.Skip(start).Take(count - 1)]));
                start = i + 1;
            }
        }

        return sides;
    }

    /// <summary><c>subtel(41×2)</c>: the normal result of a subtelomeric probe panel.</summary>
    private string Subtelomeric(Abnormality a, FishItem item)
    {
        var group = item.LocusGroups.FirstOrDefault();
        if (group?.Loci is [{ Count: { } count } locus])
        {
            return P("subtelomeric", "panel", ("regions", locus.Name), ("count", loci.CountText(count)));
        }

        return Abnormality(a, item);
    }

    /// <summary>A region result: normal chromosome or band (ish), signal counts and positions (nuc ish), fib ish and rev ish forms.</summary>
    private string Region(FishModel model, FishRegion region)
    {
        var results = string.Join("; ", region.Groups.Select(loci.Group));
        if (region.Groups.IsEmpty)
        {
            return model.Type == NotationType.RevIsh
                ? P("reverse_ish", "region", ("region", ctx.Written(region)))
                : ctx.Written(region);
        }

        if (region.Band is { } band)
        {
            var name = bands.Band(band);
            return model.Type == NotationType.FibIsh
                ? P("fiber_ish", "band", ("band", name), ("results", results))
                : P("normal_chromosome", "band", ("band", name), ("results", results));
        }

        if (region.Chromosome is { } chromosome)
        {
            return P("normal_chromosome", "chromosome", ("chromosome", chromosome.Name), ("results", results));
        }

        return Cap(results);
    }

    /// <summary><c>Cell line 2: …</c>; after a transplant <c>Recipient: …</c> / <c>Donor: …</c>.</summary>
    private string LinePrefix(FishModel model, int lineIndex, string text)
    {
        if (model.Relation == CellLineRelation.Transplant)
        {
            var donor = model.StartsWithDoubleSlash || model.CellLines.Take(lineIndex + 1).Any(l => l.Separator == CellLineSeparator.DoubleSlash);
            return P("in_situ_cell_line", "role", ("role", Cap(P("transplant_chimera", donor ? "donor" : "recipient"))), ("text", text));
        }

        return P("in_situ_cell_line", "line", ("index", N(lineIndex + 1)), ("text", text));
    }

    /// <summary>A trailing <c>//</c> after the only cell line: recipient cells only (§13.3.1 E18).</summary>
    private static bool EndsWithDoubleSlash(FishModel model) =>
        model.CellLines.Length == 1
        && model.Syntax is SyntaxNode root
        && root.Children.OfType<SyntaxNode>().SkipWhile(n => n.Kind != SyntaxKind.FishCellLine).Skip(1).Any(n => n.Kind == SyntaxKind.CellLineSeparator);

    /// <summary>The text inside a parenthesis group.</summary>
    private string Inner(Text.TextSpan span)
    {
        var text = ctx.Written(span);
        return text.Length >= 2 && text[0] == '(' && text[^1] == ')' ? text[1..^1] : text;
    }

    private string P(string construct, string? when, params (string Slot, string Value)[] slots) => ctx.P(construct, when, slots);
}
