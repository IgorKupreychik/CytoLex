using CytoLex.Core.Semantics;
using static CytoLex.Core.Translation.Phrasing;

namespace CytoLex.Core.Translation;

/// <summary>
/// The summary line and the cell line headers (templates: summary, chromosome_count, ploidy, sex_chromosomes,
/// cell_count, composite, cell_line_separator, mosaic, chimera, transplant_chimera, endoreduplication).
/// </summary>
internal sealed class HeaderRenderer(RenderContext ctx)
{
    /// <summary>The summary line of a karyotype model.</summary>
    public string Summary(KaryotypeModel model)
    {
        if (model.CellLines.Length >= 2)
        {
            var relation = model.Relation switch
            {
                CellLineRelation.Mosaic => P("mosaic", "relation"),
                CellLineRelation.Chimera => P("chimera", null),
                CellLineRelation.Transplant => P("transplant_chimera", "relation"),
                _ => P("summary", "relation_none"),
            };
            return P("summary", "cell_lines", ("relation", relation), ("cell_lines", N(model.CellLines.Length)));
        }

        if (model.CellLines.IsEmpty)
        {
            return P("summary", "fragment", ("changes", P("summary", "no_change")));
        }

        var line = model.CellLines[0];
        string text;
        if (model.Relation == CellLineRelation.Transplant)
        {
            text = P(
                "transplant_chimera",
                "one_side",
                ("cells", line.CellCount?.Cells is { } c ? N(c) : "?"),
                ("side", P("transplant_chimera", line.SeparatorBefore == CellLineSeparator.DoubleSlash ? "donor" : "recipient")),
                ("count", Count(line)),
                ("sex", Sex(line)));
        }
        else if (line.Count is null && line.Sex is null)
        {
            text = line.Items switch
            {
                [] => P("summary", "fragment", ("changes", P("summary", "no_change"))),
                var items when items.All(i => i is ChromosomeRegion or AlternativeItem { Readings: [ChromosomeRegion, ..] }) => P("summary", "fragment_band"),
                var items when items.All(i => i is BandComposition) => P("summary", "fragment_composition"),
                _ => P("summary", "fragment", ("changes", Changes(line))),
            };
        }
        else
        {
            var sex = line.Sex is null ? SexInAbnormalities(line) : Sex(line);
            text = line.CellCount is { IsComposite: true } cp
                ? P(
                    "summary",
                    "karyotype_composite",
                    ("count", Count(line)),
                    ("sex", sex),
                    ("changes", Changes(line)),
                    ("composite", P("composite", "summary", ("cells", cp.Cells is { } n ? N(n) : "?"))))
                : P("summary", "karyotype", ("count", Count(line)), ("sex", sex), ("changes", Changes(line)));
        }

        return model.IsEndoreduplication ? P("endoreduplication", null, ("summary", text)) : text;
    }

    /// <summary>The sentence of a cell line's header (two or more cell lines): <c>Cell line 1, recipient (4 cells): 46 chromosomes; …</c>.</summary>
    public string Header(KaryotypeModel model, int index)
    {
        var line = model.CellLines[index];
        var header = line.Sex is null
            ? P("cell_line_separator", "header_count_only", ("count", Count(line)))
            : P("cell_line_separator", "header", ("count", Count(line)), ("sex", Sex(line)));
        var cells = line.CellCount switch
        {
            { IsComposite: true, Cells: { } n } => P("composite", "cells", ("cells", N(n))),
            { Cells: 1 } => P("cell_count", "one"),
            { Cells: { } n } => P("cell_count", "cells", ("cells", N(n))),
            _ => null,
        };
        var position = N(index + 1);
        if (model.Relation == CellLineRelation.Transplant)
        {
            var donor = model.CellLines.Take(index + 1).Any(l => l.SeparatorBefore == CellLineSeparator.DoubleSlash);
            var role = P("transplant_chimera", donor ? "donor" : "recipient");
            return cells is null
                ? P("cell_line_separator", "role_without_cells", ("index", position), ("role", role), ("header", header))
                : P("cell_line_separator", "role_with_cells", ("index", position), ("role", role), ("cells", cells), ("header", header));
        }

        return cells is null
            ? P("cell_line_separator", "without_cells", ("index", position), ("header", header))
            : P("cell_line_separator", "with_cells", ("index", position), ("cells", cells), ("header", header));
    }

    private string Count(CellLine line)
    {
        var count = line.Count switch
        {
            null or { IsMissing: true } => P("chromosome_count", "unknown"),
            { IsRange: true, From: { } from, To: { } to } => P("chromosome_count", "range", ("from", N(from)), ("to", N(to))),
            { IsRange: true, From: null, To: { } to } => P("chromosome_count", "approximate", ("count", N(to))),
            { From: { } n } => P("chromosome_count", "single", ("count", N(n))),
            _ => P("chromosome_count", "unknown"),
        };
        if (line.Ploidy is { Level: { } level and >= 1 and <= 8 })
        {
            var ploidy = P("ploidy", "text", ("level", P("ploidy", "level_" + N(level))));
            count = P("chromosome_count", "with_ploidy", ("count", count), ("ploidy", ploidy));
        }

        return count;
    }

    private string Sex(CellLine line)
    {
        if (line.Sex is not { } sex)
        {
            return P("sex_chromosomes", "none");
        }

        var number = sex.Chromosomes.Length == 1 ? "single" : "plural";
        var when = sex.Constitutional switch
        {
            null => number,
            { IsQuestioned: true } => "possibly_constitutional_" + number,
            _ => "constitutional_" + number,
        };
        return P("sex_chromosomes", when, ("sex", string.Concat(sex.Chromosomes)));
    }

    /// <summary>The sex chromosomes are only in the abnormalities (<c>46,t(X;Y)(…)</c>).</summary>
    private string SexInAbnormalities(CellLine line)
    {
        var mentions = line.Items
            .Select(i => i.DescendantsAndSelf().OfType<AbnormalityPart>().SelectMany(p => p.Chromosomes)
                .Concat(i is WholeChromosomeChange w ? [w.Chromosome] : [])
                .Count(c => c.IsSexChromosome))
            .Where(n => n > 0)
            .ToList();
        return mentions.Sum() <= 1
            ? P("summary", "sex_in_abnormality")
            : P("summary", mentions.Count >= 2 ? "sexes_in_abnormalities" : "sexes_in_abnormality");
    }

    private string Changes(CellLine line)
    {
        var changes = line.Items.Count(i => i is not OmittedItems);
        var counted = changes switch
        {
            0 => P("summary", "no_change"),
            1 => P("summary", "one_change"),
            _ => P("summary", "changes", ("n", N(changes))),
        };
        if (!line.Items.Any(i => i is OmittedItems))
        {
            return counted;
        }

        return changes == 0 ? P("summary", "omitted") : P("summary", "omitted_plus", ("changes", counted));
    }

    private string P(string construct, string? when, params (string Slot, string Value)[] slots) => ctx.P(construct, when, slots);
}
