using CytoLex.Core.Semantics;
using static CytoLex.Core.Translation.Phrasing;

namespace CytoLex.Core.Translation;

/// <summary>
/// Loci, locus groups, signal counts and cells of in situ results in running text (T-093; templates: locus_status,
/// signal_count, connected, separated, amplified, diminished, enhanced, subtelomeric, whole_chromosome_paint,
/// partial_chromosome_paint, cells_scored).
/// </summary>
internal sealed class LocusPhrases(RenderContext ctx)
{
    /// <summary>
    /// The loci of an abnormality's group as present/absent results: runs of loci with the same status are listed together
    /// (<c>D15S11 present, SNRPN and D15S10 absent, GABRB3 present</c>); counted loci give their signals.
    /// </summary>
    public string Statuses(IReadOnlyList<(string Name, Locus Locus)> entries, LocusCount? shared)
    {
        var phrases = new List<string>();
        var run = new List<string>();
        string? runStatus = null;
        foreach (var (name, locus) in entries)
        {
            var status = Status(locus);
            var simple = status.Length > 0 && locus.Count is null && locus.Qualifiers.IsEmpty;
            if (simple && status == runStatus)
            {
                run.Add(name);
                continue;
            }

            Flush();
            if (simple)
            {
                runStatus = status;
                run.Add(name);
            }
            else
            {
                phrases.Add(Result(name, locus, shared));
            }
        }

        Flush();
        return string.Join(", ", phrases);

        void Flush()
        {
            if (run.Count > 0)
            {
                phrases.Add(StatusPhrase(runStatus!, And(run)));
            }

            run.Clear();
            runStatus = null;
        }
    }

    /// <summary>The result of one locus: its status, signal count and qualifiers (<c>RUNX1 amplified</c>, <c>4 signals for RUNX1</c>).</summary>
    public string Result(string name, Locus locus, LocusCount? shared)
    {
        var count = locus.Count ?? shared;
        var status = Status(locus);
        string text;
        if (count is not null)
        {
            text = Signals(count, name, each: false);
            text = Qualify(locus.Qualifiers, text, "signals");
        }
        else if (!locus.Qualifiers.IsEmpty)
        {
            text = Qualify(locus.Qualifiers, name, "locus");
        }
        else if (status.Length > 0)
        {
            text = StatusPhrase(status, name);
        }
        else
        {
            text = name;
        }

        return text;
    }

    /// <summary>The signal results of a region's locus group (nuc ish, normal chromosomes, fib ish): counts, positions, qualifiers.</summary>
    public string Group(LocusGroup group)
    {
        if (group.IsPosition)
        {
            return Position(group);
        }

        // (A,B)×3: one shared count for loci written without their own.
        if (group.SharedCount is { } shared && group.Loci.All(l => l.Count is null && l.Qualifiers.IsEmpty && l.Status.Length == 0))
        {
            var names = Names(group);
            var text = group.Loci.Length == 1 ? Signals(shared, names, each: false) : Signals(shared, names, each: true);
            return Qualify(group.Qualifiers, text, "group");
        }

        var entries = Entries(group);
        if (group.Loci.Any(l => Status(l).Length > 0) && group.Loci.All(l => l.Count is null) && group.SharedCount is null)
        {
            return Qualify(group.Qualifiers, Statuses(entries, null), "group");
        }

        return Qualify(group.Qualifiers, And([.. entries.Select(e => Result(e.Name, e.Locus, group.SharedCount))]), "group");
    }

    /// <summary><c>(ABL1 con BCR×2)</c>: connected or separated signals, with their count (written on the last locus or after the group).</summary>
    private string Position(LocusGroup group)
    {
        var count = group.SharedCount ?? group.Loci.LastOrDefault(l => l.Count is not null)?.Count;
        var names = group.Loci.Select(l => l.Qualifiers.IsEmpty ? Name(l) : Qualify(l.Qualifiers, Name(l), "name")).ToList();
        var construct = group.Separators.Contains(LocusSeparator.Sep) ? "separated" : "connected";
        var loci = And(names);
        var text = count switch
        {
            null => ctx.P(construct, "uncounted", ("loci", loci)),
            { Low: 1, High: null, IsOpenEnded: false } => ctx.P(construct, "one", ("loci", loci)),
            _ => ctx.P(construct, "many", ("count", CountText(count)), ("loci", loci)),
        };
        return Qualify(group.Qualifiers, text, "group");
    }

    /// <summary>The names of a group's loci: <c>ABL1 and BCR</c>; contig loci joined with <c>/</c> count as one.</summary>
    private string Names(LocusGroup group) => And([.. Entries(group).Select(e => e.Name)]);

    /// <summary>
    /// The loci of a group as named results: contig loci joined with <c>/</c> (<c>D14Z1/D22Z1+</c>) are one entry with
    /// the status, count and qualifiers of the last one.
    /// </summary>
    public List<(string Name, Locus Locus)> Entries(LocusGroup group) => Entries(group.Loci, group.Separators);

    /// <inheritdoc cref="Entries(LocusGroup)"/>
    public List<(string Name, Locus Locus)> Entries(IReadOnlyList<Locus> loci, IReadOnlyList<LocusSeparator> separators)
    {
        var entries = new List<(string Name, Locus Locus)>();
        for (var i = 0; i < loci.Count; i++)
        {
            var name = Name(loci[i]);
            if (i > 0 && i - 1 < separators.Count && separators[i - 1] == LocusSeparator.Slash && entries.Count > 0)
            {
                entries[^1] = (entries[^1].Name + "/" + name, loci[i]);
            }
            else
            {
                entries.Add((name, loci[i]));
            }
        }

        return entries;
    }

    private static string Status(Locus locus) => locus.Status.Length > 0 ? locus.Status : locus.LeadingSigns;

    /// <summary><c>2 signals for D21S65</c>, <c>no signal for SRY</c>, <c>4 to 5 signals each for D17Z1 and ERBB2</c>.</summary>
    public string Signals(LocusCount count, string loci, bool each)
    {
        if (count is { Low: 0, High: null })
        {
            return ctx.P("signal_count", each ? "none_each" : "none", ("locus", loci), ("loci", loci));
        }

        if (count is { Low: 1, High: null, IsOpenEnded: false })
        {
            return ctx.P("signal_count", each ? "one_each" : "one", ("locus", loci), ("loci", loci));
        }

        return ctx.P("signal_count", each ? "signals_each" : "signals", ("count", CountText(count)), ("locus", loci), ("loci", loci));
    }

    /// <summary>A count: <c>3</c>, <c>4 to 5</c>, <c>12 to more than 50</c>; as written when it can't be read.</summary>
    public string CountText(LocusCount count) => count switch
    {
        { Low: { } low, High: { } high, IsOpenEnded: true } => ctx.P("signal_count", "open_range", ("low", N(low)), ("high", N(high))),
        { Low: { } low, High: { } high } => ctx.P("signal_count", "range", ("low", N(low)), ("high", N(high))),
        { Low: { } low } => N(low),
        _ => count.Text,
    };

    /// <summary>A locus name: as written, paints spelled out (<c>whole chromosome paint 11</c>).</summary>
    public string Name(Locus locus)
    {
        if (locus.PaintedChromosome is { } chromosome)
        {
            return locus.Name.StartsWith("wcp", StringComparison.Ordinal)
                ? ctx.P("whole_chromosome_paint", null, ("chromosome", chromosome))
                : ctx.P("partial_chromosome_paint", null, ("region", locus.Name[3..].Trim()));
        }

        return locus.IsBand ? ctx.P("subtelomeric", "band", ("band", locus.Name)) : locus.Name;
    }

    /// <summary>The cells of a result: <c>in 20 cells</c>, <c>in 100 of 200 cells</c>.</summary>
    public string Cells(FishCells cells) => cells switch
    {
        { IsPercentage: true } => ctx.P("cells_scored", "percent", ("text", cells.Text)),
        { Count: { } n, Total: { } total } => ctx.P("cells_scored", "of_total", ("cells", N(n)), ("total", N(total))),
        { Count: 1 } => ctx.P("cells_scored", "one"),
        { Count: { } n } => ctx.P("cells_scored", "cells", ("cells", N(n))),
        _ => ctx.P("cells_scored", "written", ("text", cells.Text)),
    };

    private string StatusPhrase(string status, string loci) => status switch
    {
        "+" => ctx.P("locus_status", "present", ("loci", loci)),
        "++" => ctx.P("locus_status", "twice", ("loci", loci)),
        _ when status.All(c => c == '+') => ctx.P("locus_status", "times", ("loci", loci), ("n", N(status.Length))),
        _ => ctx.P("locus_status", "absent", ("loci", loci)),
    };

    /// <summary>Applies <c>amp</c>/<c>dim</c>/<c>enh</c> to a phrase (form: locus, name, signals or group).</summary>
    private string Qualify(IEnumerable<LocusQualifier> qualifiers, string text, string form)
    {
        foreach (var q in qualifiers)
        {
            var construct = q.Word switch
            {
                "amp" => "amplified",
                "dim" => "diminished",
                "enh" => "enhanced",
                _ => null,
            };
            if (construct is null)
            {
                continue;
            }

            // amp has no "signals" form: an amplified signal count reads "…, amplified".
            var when = form == "signals" && construct == "amplified" ? "group" : form;
            text = ctx.P(construct, when, ("locus", text), ("phrase", text));
        }

        return text;
    }
}
