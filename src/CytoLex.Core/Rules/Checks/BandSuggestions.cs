using System.Collections.Immutable;
using System.Globalization;
using CytoLex.Core.Kb;
using CytoLex.Core.Semantics;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// "Did you mean" options for a band name the edition doesn't have (<c>BAND.MUST_EXIST</c>, level 2, T-041), all on the
/// written chromosome and taken from the edition's band set, best first:
/// <list type="number">
/// <item>arr only: the band of the written form that holds the region's position in the stated build (<c>18q22.33</c> → <c>18q21.33</c>);</item>
/// <item>the nearest bands of the same form within the same band or arm (<c>9q35</c> → <c>9q34</c>, <c>9q34.4</c> → <c>9q34.3</c>);</item>
/// <item>the nearest enclosing band (<c>21q11.21</c> → <c>21q11.2</c>);</item>
/// <item>the same number on the other arm (<c>9p34</c> → <c>9q34</c>).</item>
/// </list>
/// Karyotype and in situ strings get only bands of the book's idiograms; arr strings any band of the set.
/// </summary>
internal static class BandSuggestions
{
    /// <summary>The options for <paramref name="bp"/>; <paramref name="position"/> is its arr position with its build, if any.</summary>
    public static ImmutableArray<SuggestionProposal> For(
        string input, BandSet bands, Breakpoint bp, bool bookOnly, (long Position, string Build)? position)
    {
        if (bp.Chromosome is not { } chromosome || bp.Arm is not { } arm || bp.BandNumber is not { Length: >= 2 } number
            || bp.Span.End > input.Length)
        {
            return [];
        }

        // The band text as written, with or without its chromosome (3q13 / q13); anything else (q21~24) is left alone.
        var c = chromosome.Name;
        var written = input.Substring(bp.Span.Start, bp.Span.Length);
        var offset = written == arm + number ? 0 : written == c + arm + number ? c.Length : -1;
        if (offset < 0)
        {
            return [];
        }

        var target = new TextSpan(bp.Span.Start + offset, bp.Span.Length - offset);
        var names = new List<(string Name, string Description)>();
        if (position is var (pos, build) && ArrPositionFit.HoldingBand(bands, c, number, pos, build) is { } holding)
        {
            names.Add((holding.Name, $"Did you mean {holding.Name}? It holds {pos.ToString(CultureInfo.InvariantCulture)} in {build}."));
        }

        var usable = bands.Bands.Where(b => b.Chromosome == c && (!bookOnly || b.Book)).ToList();
        var dot = number.IndexOf('.', StringComparison.Ordinal);
        var parent = dot < 0 ? null : number[..dot];
        var value = Value(number);
        var siblings = usable
            .Where(b => b.Arm == arm && Shape(Number(b)) == Shape(number) && (parent is null || Number(b).StartsWith(parent + ".", StringComparison.Ordinal)))
            .Select(b => (Band: b, Distance: Math.Abs(Value(Number(b)) - value)))
            .ToList();
        var lower = siblings.Where(s => Value(Number(s.Band)) < value).OrderBy(s => s.Distance).FirstOrDefault();
        var higher = siblings.Where(s => Value(Number(s.Band)) > value).OrderBy(s => s.Distance).FirstOrDefault();
        foreach (var s in new[] { lower, higher }.Where(s => s.Band is not null).OrderBy(s => s.Distance))
        {
            names.Add((s.Band.Name, $"Did you mean {s.Band.Name}?"));
        }

        for (var n = number[..^1].TrimEnd('.'); n.Length >= 2; n = n[..^1].TrimEnd('.'))
        {
            if (usable.Find(b => b.Name == c + arm + n) is { } ancestor)
            {
                names.Add((ancestor.Name, $"Did you mean {ancestor.Name}?"));
                break;
            }
        }

        var otherArm = arm == "p" ? "q" : "p";
        if (usable.Find(b => b.Name == c + otherArm + number) is { } other)
        {
            names.Add((other.Name, $"Did you mean {other.Name} (the {(otherArm == "p" ? "short" : "long")} arm)?"));
        }

        return
        [
            .. names
                .DistinctBy(n => n.Name, StringComparer.Ordinal)
                .Take(DiagnosticFactory.MaxSuggestions)
                .Select(n => new SuggestionProposal(new TextEdit(target, n.Name[c.Length..]), n.Description)),
        ];
    }

    private static string Number(Band b) => b.Name[(b.Chromosome.Length + 1)..];

    private static string Shape(string number) => new([.. number.Select(ch => ch == '.' ? '.' : '9')]);

    /// <summary>A band number as a fraction along the arm: <c>34</c> → 0.34, <c>34.3</c> → 0.343.</summary>
    private static decimal Value(string number) =>
        decimal.Parse("0." + number.Replace(".", string.Empty, StringComparison.Ordinal), CultureInfo.InvariantCulture);
}
