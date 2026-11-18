using System.Collections.Immutable;
using System.Text.RegularExpressions;
using CytoLex.Core.Segmentation;
using CytoLex.Core.Text;

namespace CytoLex.Core.Context;

/// <summary>
/// Infers the sample context from clear markers only (decisions T-021, T-026; docs/design/core.md §3.3).
/// </summary>
/// <remarks>
/// A lexical scan, since it runs before the parser: words are maximal runs of ASCII letters matched exactly,
/// so <c>cen</c>, <c>cp</c> or <c>idic</c> never count as the marker <c>c</c>. Only karyotype, in situ
/// hybridization and cgh segments are scanned; arr, rsa and seq segments are skipped (HGVS <c>c.</c>, free
/// probe and gene names). The parser (T-027) may later back the same API with tokens.
/// </remarks>
public static partial class ContextInference
{
    /// <summary>Infers the context of <paramref name="input"/>, split into <paramref name="segments"/> by <see cref="Segmenter.Split"/>.</summary>
    /// <param name="input">The whole input string.</param>
    /// <param name="segments">Its segments.</param>
    /// <param name="hint">An explicit context; overrides the inferred one.</param>
    public static ContextInfo Infer(string input, ImmutableArray<Segment> segments, SampleContext? hint = null)
    {
        ArgumentNullException.ThrowIfNull(input);

        var markers = new List<ContextMarker>();
        foreach (var segment in segments)
        {
            if (segment.Type is NotationType.Arr or NotationType.Rsa or NotationType.Seq)
            {
                continue;
            }

            ScanSegment(input, segment, markers);
        }

        markers.Sort((a, b) => a.Span.Start.CompareTo(b.Span.Start));

        var acquired = markers.Exists(m => m.Indicates == SampleContext.Acquired);
        var constitutional = markers.Exists(m => m.Indicates == SampleContext.Constitutional);
        var contradictory = acquired && constitutional;
        SampleContext? inferred = contradictory ? null
            : acquired ? SampleContext.Acquired
            : constitutional ? SampleContext.Constitutional
            : null;

        return new ContextInfo(hint, inferred, hint ?? inferred, [.. markers], contradictory);
    }

    private static void ScanSegment(string input, Segment segment, List<ContextMarker> markers)
    {
        var text = input.AsSpan(segment.Span.Start, segment.Span.Length);
        var offset = segment.Span.Start;
        var hasMosOrChi = false;

        foreach (var m in Word().EnumerateMatches(text))
        {
            var span = new TextSpan(offset + m.Index, m.Length);
            switch (text.Slice(m.Index, m.Length))
            {
                case "idem" or "sl" or "sdl":
                    markers.Add(new ContextMarker(ContextMarkerKind.StemlineReference, SampleContext.Acquired, span));
                    break;
                case "c":
                    markers.Add(new ContextMarker(ContextMarkerKind.ConstitutionalAnomaly, SampleContext.Acquired, span));
                    break;
                case "rec":
                    markers.Add(new ContextMarker(ContextMarkerKind.Recombinant, SampleContext.Constitutional, span));
                    break;
                case "mos" or "chi":
                    hasMosOrChi = true;
                    break;
            }
        }

        foreach (var m in PloidyLevel().EnumerateMatches(text))
        {
            markers.Add(new ContextMarker(ContextMarkerKind.PloidyLevel, SampleContext.Acquired, new TextSpan(offset + m.Index, m.Length)));
        }

        if (segment.Type == NotationType.Karyotype && !hasMosOrChi && CellCount().IsMatch(text)
            && FirstTopLevelSlash(text) is { } slash)
        {
            markers.Add(new ContextMarker(ContextMarkerKind.CloneStructure, SampleContext.Acquired, new TextSpan(offset + slash.Start, slash.Length)));
        }
    }

    /// <summary>The first cell-line separator (<c>/</c> or <c>//</c>) outside brackets, relative to <paramref name="text"/>.</summary>
    private static TextSpan? FirstTopLevelSlash(ReadOnlySpan<char> text)
    {
        var depth = 0;
        for (var i = 0; i < text.Length; i++)
        {
            switch (text[i])
            {
                case '(' or '[' or '{':
                    depth++;
                    break;
                case ')' or ']' or '}':
                    depth = Math.Max(0, depth - 1);
                    break;
                case '/' when depth == 0:
                    return new TextSpan(i, i + 1 < text.Length && text[i + 1] == '/' ? 2 : 1);
            }
        }

        return null;
    }

    [GeneratedRegex("[A-Za-z]+")]
    private static partial Regex Word();

    /// <summary>A ploidy level such as <c>&lt;2n&gt;</c>, <c>&lt;3n&gt;</c> or <c>&lt;4n±&gt;</c>.</summary>
    [GeneratedRegex("<[^<>]*n[^<>]*>")]
    private static partial Regex PloidyLevel();

    /// <summary>A cell count <c>[n]</c>.</summary>
    [GeneratedRegex(@"\[\d+\]")]
    private static partial Regex CellCount();
}
