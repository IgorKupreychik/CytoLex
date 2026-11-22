using CytoLex.Core.Semantics;
using static CytoLex.Core.Translation.Phrasing;

namespace CytoLex.Core.Translation;

/// <summary>Chromosomes in running text (templates: band, unknown_designation).</summary>
internal sealed class ChromosomePhrases(RenderContext ctx)
{
    /// <summary>A chromosome: <c>chromosome 5</c>, <c>the underlined chromosome 7</c>, <c>an unidentified chromosome</c>.</summary>
    public string Chromosome(ChromosomeRef c)
    {
        if (c.IsUnknown)
        {
            return ctx.P("unknown_designation", "chromosome");
        }

        if (ctx.Homologues.Contains(c.Name))
        {
            return ctx.P("band", c.IsUnderlined ? "underlined_chromosome" : "first_chromosome", ("chromosome", c.Name));
        }

        return ctx.P("band", "chromosome", ("chromosome", c.Name));
    }

    /// <summary>
    /// A chromosome list: <c>chromosomes 2 and 5</c>, <c>two chromosomes 13</c>, <c>an unidentified chromosome and chromosome 15</c>,
    /// <c>chromosome 2 and both chromosomes 7 (the second one underlined)</c>.
    /// </summary>
    public string Chromosomes(IReadOnlyList<ChromosomeRef> list)
    {
        if (list.Count == 1)
        {
            return Chromosome(list[0]);
        }

        if (list.Count == 2 && !list[0].IsUnknown && list[0].Name == list[1].Name)
        {
            return ctx.P("band", "two_chromosomes", ("chromosome", list[0].Name));
        }

        // Runs: consecutive known chromosomes (collapsed), a homologue pair, or an unknown chromosome.
        var runs = new List<string>();
        var known = new List<string>();
        for (var i = 0; i < list.Count; i++)
        {
            var c = list[i];
            if (c.IsUnknown)
            {
                Flush(runs, known);
                runs.Add(ctx.P("unknown_designation", "chromosome"));
            }
            else if (i + 1 < list.Count && list[i + 1].Name == c.Name && list[i + 1].IsUnderlined && !c.IsUnderlined)
            {
                Flush(runs, known);
                runs.Add(ctx.P("band", "both_chromosomes", ("chromosome", c.Name)));
                i++;
            }
            else
            {
                known.Add(c.Name);
            }
        }

        Flush(runs, known);
        return And(runs);
    }

    private void Flush(List<string> runs, List<string> known)
    {
        if (known.Count == 1)
        {
            runs.Add(ctx.P("band", "chromosome", ("chromosome", known[0])));
        }
        else if (known.Count > 1)
        {
            runs.Add(ctx.P("band", "chromosomes", ("chromosomes", And(known))));
        }

        known.Clear();
    }
}
