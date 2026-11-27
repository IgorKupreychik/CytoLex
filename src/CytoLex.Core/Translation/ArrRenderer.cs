using CytoLex.Core.Semantics;
using static CytoLex.Core.Translation.Phrasing;

namespace CytoLex.Core.Translation;

/// <summary>
/// The summary and the results of a microarray segment (T-105; templates: microarray, arr_result, arr_copy_number,
/// nucleotide_positions, zygosity, complex_cx, chromothripsis, cell_proportion). The technique and the genome build are
/// named in the summary only; each comma-separated result is one sentence. Copy numbers are given as written: no gain or
/// loss reading, which needs the sex and ploidy (decisions → T-105, as T-072/T-101).
/// </summary>
internal sealed class ArrRenderer(RenderContext ctx, BandPhrases bands)
{
    /// <summary>The summary sentence: <c>Microarray (arr, GRCh38): 2 results.</c></summary>
    public string Summary(ArrModel model)
    {
        var technique = model.Build is { Text.Length: > 0 } build
            ? P("microarray", "technique_build", ("build", build.Text))
            : P("microarray", "technique");
        var n = model.Aberrations.Length;
        var results = n switch
        {
            0 => P("microarray", "no_result"),
            1 => P("microarray", "one_result"),
            _ => P("microarray", "results", ("n", N(n))),
        };
        return P("microarray", "summary", ("technique", technique), ("results", results));
    }

    /// <summary>The sentence of one result (without the final full stop).</summary>
    public string Item(ArrAberration a)
    {
        var what = a.IsWholeChromosomes ? Chromosomes(a) : Regions(a);
        var text = a.Copy switch
        {
            { Kind: ArrResultKind.Amplification } => P("amplified", "arr", ("locus", what)),
            { Kind: ArrResultKind.Complex } => P("complex_cx", null, ("what", what)),
            { Kind: ArrResultKind.Chromothripsis } => P("chromothripsis", null, ("what", what)),
            { } copy => P("arr_result", "copies_of", ("copies", Copies(copy)), ("what", what)),
            null when a.IsDetailed => P("arr_result", "detailed", ("region", what), ("details", Details(a))),
            null => what,
        };

        if (a.Proportion is { } proportion)
        {
            text += "; " + (proportion.IsFraction
                ? P("cell_proportion", "fraction", ("proportion", proportion.Text))
                : P("cell_proportion", "written", ("proportion", proportion.Text)));
        }

        var qualifiers = a.Qualifiers.Select(Qualifier).ToList();
        if (qualifiers.Count > 0)
        {
            // After a detailed list (…, 2 copies) the words are set off by a semicolon so they don't read as the last position's.
            text += (a.Copy is null && a.IsDetailed ? "; " : ", ") + string.Join(", ", qualifiers);
        }

        return Cap(text);
    }

    /// <summary><c>1 copy</c>, <c>3 copies</c>, <c>no copy</c>, <c>2 to 4 copies</c>.</summary>
    private string Copies(ArrCopyNumber copy)
    {
        if (copy.IsRange && copy.Min is { } low && copy.Max is { } high)
        {
            return P("arr_copy_number", "range", ("low", N(low)), ("high", N(high)));
        }

        return copy.Min switch
        {
            0 => P("arr_copy_number", "none"),
            1 => P("arr_copy_number", "one"),
            { } n => P("arr_copy_number", "n", ("n", N(n))),
            null => ctx.Written(copy),
        };
    }

    /// <summary>
    /// Whole chromosomes and arms: <c>chromosome 8</c>, <c>chromosomes 1 to 22 and X</c>, <c>the long arm of chromosome 16</c>;
    /// runs of chromosomes are kept together between arms (<c>chromosomes 3 and 7, the long arm of chromosome 9 and …</c>).
    /// </summary>
    private string Chromosomes(ArrAberration a)
    {
        var phrases = new List<string>();
        var run = new List<ArrChromosomeEntry>();
        foreach (var entry in a.Chromosomes)
        {
            if (entry.Last is null && entry.First.Arm is { } arm)
            {
                Flush();
                phrases.Add(P("arr_result", "arm", ("arm", bands.Arm(arm)), ("chromosome", entry.First.Name)));
            }
            else
            {
                run.Add(entry);
            }
        }

        Flush();
        return phrases.Count > 2 ? SerialAnd(phrases) : And(phrases);

        void Flush()
        {
            if (run.Count == 0)
            {
                return;
            }

            var names = run.Select(e => e.Last is { } last ? P("arr_result", "range", ("from", e.First.Name), ("to", last.Name)) : e.First.Name).ToList();
            phrases.Add(run is [{ Last: null }]
                ? P("arr_result", "chromosome", ("chromosome", names[0]))
                : P("arr_result", "chromosomes", ("chromosomes", And(names))));
            run.Clear();
        }
    }

    /// <summary>The regions of a result (grouped regions joined with <c>and</c>), each with its positions.</summary>
    private string Regions(ArrAberration a) => And([.. a.Regions.Select(r => Region(r, a.IsDetailed))]);

    /// <summary>
    /// A region: <c>band 11p12 (positions 37741458 to 39209912)</c>, <c>the segment from 6q21 to 6q25.1 (…)</c>; other readings
    /// (<c>Xq28 or Yq12</c>) are given with their own positions. In the detailed system the positions follow with their copy numbers.
    /// </summary>
    private string Region(ArrRegion region, bool detailed)
    {
        var readings = new List<ArrRegion> { region };
        readings.AddRange(region.Alternatives);
        var phrases = new List<string>();
        for (var i = 0; i < readings.Count; i++)
        {
            var phrase = Bands(readings[i]);
            var positions = detailed ? null : Positions(region.Positions, i);
            phrases.Add(positions is null ? phrase : P("arr_result", "with_positions", ("phrase", phrase), ("positions", positions)));
        }

        return phrases.Count == 1 ? phrases[0] : P("alternative", "phrase", ("first", phrases[0]), ("second", string.Join(" or ", phrases.Skip(1))));
    }

    /// <summary>The bands of one reading: one band, or the segment between the first and the last (an arm end or the centromere by name).</summary>
    private string Bands(ArrRegion region)
    {
        if (region.Bands is [var only])
        {
            return P("arr_result", "band", ("band", Band(only)));
        }

        if (region.First is { } first && region.Last is { } last)
        {
            return P("arr_result", "segment", ("from", Band(first)), ("to", Band(last)));
        }

        return ctx.Written(region.BandSpan);
    }

    /// <summary>A band in a region; <c>pter</c>/<c>qter</c>/<c>cen</c> as the arm end or the centromere of the chromosome.</summary>
    private string Band(Breakpoint b)
    {
        var chromosome = b.Chromosome is { IsUnknown: false } c ? P("band", "chromosome", ("chromosome", c.Name)) : null;
        return (b.Landmark, chromosome) switch
        {
            ("pter", { } of) => P("short_arm_end", "of_chromosome", ("chromosome", of)),
            ("qter", { } of) => P("long_arm_end", "of_chromosome", ("chromosome", of)),
            ("cen", { } of) => P("centromere", "of_chromosome", ("chromosome", of)),
            _ => bands.Band(b),
        };
    }

    /// <summary>The positions of reading <paramref name="reading"/>: <c>positions 113900000 to 149100000</c>, as written.</summary>
    private string? Positions(ArrPositions? positions, int reading)
    {
        if (positions is null)
        {
            return null;
        }

        var items = positions.Items
            .Select(item => reading == 0 ? item : reading - 1 < item.Alternatives.Length ? item.Alternatives[reading - 1] : null)
            .OfType<ArrPositionItem>()
            .Select(Position)
            .ToList();
        return items.Count == 0 ? null : string.Join(", ", items);
    }

    /// <summary>The detailed system: each position with its copy number (<c>position 163002425, 2 copies; …</c>).</summary>
    private string Details(ArrAberration a) => string.Join("; ", a.Regions
        .Where(r => r.Positions is not null)
        .SelectMany(r => r.Positions!.Items)
        .Select(item => item.Copy is { } copy
            ? P("nucleotide_positions", "with_copies", ("position", Position(item)), ("copies", Copies(copy)))
            : Position(item)));

    /// <summary><c>position 163002425</c> / <c>positions 163146681 to 183022312</c>, the numbers as written.</summary>
    private string Position(ArrPositionItem item) => item.EndSpan is { } end
        ? P("nucleotide_positions", "span", ("from", ctx.Written(item.StartSpan)), ("to", ctx.Written(end)))
        : P("nucleotide_positions", "position", ("position", ctx.Written(item.StartSpan)));

    /// <summary>A word after the result: zygosity, parental origin, <c>mos</c>, <c>c</c>.</summary>
    private string Qualifier(ArrQualifier q) => q.Word switch
    {
        "hmz" or "htz" => P("zygosity", q.Word),
        "mat" => P("maternal", null),
        "pat" => P("paternal", null),
        "inh" => P("inherited", null),
        "dn" => P("de_novo", null),
        "mos" => P("mosaic", "in_abnormality"),
        "c" => P("constitutional_c", null),
        _ => q.Word,
    };

    private string P(string construct, string? when, params (string Slot, string Value)[] slots) => ctx.P(construct, when, slots);
}
