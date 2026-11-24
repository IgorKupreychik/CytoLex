using System.Collections.Immutable;
using CytoLex.Core.Composing;
using CytoLex.Core.Semantics;
using CytoLex.Core.Text;

namespace CytoLex.Core.Translation;

/// <summary>
/// The composition line of a translated part (T-086): the band composition the short system defines
/// (<see cref="Abnormality.Composition"/>, computed), in the words the detailed system's sentences use —
/// <c>Band composition: the segment from the end of the short arm of chromosome 9 to 9q34, joined to …</c>. A composition
/// is read through <see cref="CompositionRenderer"/> as if it were written, with chromosome numbers where
/// <c>STRUCT.COMPOSITION_CHROMOSOME_NUMBERS</c> puts them, so both systems share one phrase path. Parts written in the
/// detailed system get none: their sentence already gives the composition.
/// </summary>
internal sealed class CompositionLine(RenderContext ctx, ChromosomePhrases chromosomes, CompositionRenderer composition)
{
    /// <summary>The line of <paramref name="item"/>, or <c>null</c> when it has no computed composition.</summary>
    public string? Line(KaryotypeItem item)
    {
        if (item is not Abnormality { Composition: { Source: CompositionSource.Computed } c } a)
        {
            return null;
        }

        // Chromosome numbers as Composer.Format writes them (STRUCT.COMPOSITION_CHROMOSOME_NUMBERS): with several chromosomes involved.
        var numbered = a.Parts.Any(p => p.Chromosomes.Length > 1)
            || c.Products.SelectMany(p => p.Pieces).SelectMany(p => p.Points).Select(p => p.Chromosome).OfType<ChromosomeId>().Where(x => !x.IsUnknown).Distinct().Count() > 1;
        if (a.Kind == "tas")
        {
            return ctx.P("band_composition", "line", ("segments", composition.TasChain(Structure(c.Products[0], numbered))));
        }

        if (c.Products is [var one])
        {
            return ctx.P("band_composition", "line", ("segments", Segments(one, numbered, null, ring: true)));
        }

        // One product per chromosome of the list (t, ins(a;b), whole-arm t): described for it, as the detailed sentence does.
        var head = a.Parts[^1];
        var known = head.Chromosomes.Where(x => !x.IsUnknown).ToList();
        var named = known.Count == c.Products.Length;
        var products = c.Products.Select((p, i) => named
            ? ctx.P("band_composition", "group", ("chromosome", chromosomes.Chromosome(known[i])), ("segments", Segments(p, numbered, known[i], ring: false)))
            : Segments(p, numbered, null, ring: false));
        return ctx.P("band_composition", "line_products", ("segments", string.Join("; ", products)));
    }

    /// <summary>The segments of a product; <paramref name="group"/> names the chromosome it is described for (<c>its short arm</c>), as in a multi-group detailed sentence.</summary>
    private string Segments(ComposedChromosome product, bool numbered, ChromosomeRef? group, bool ring)
    {
        var segments = composition.Composition(Structure(product, numbered), group, out var isRing);
        return ring && isRing ? ctx.P("band_composition", "ring", ("segments", segments)) : segments;
    }

    /// <summary>A composition as the written structure the renderer reads (no spans: nothing in it is from the input).</summary>
    internal static DerivativeStructure Structure(ComposedChromosome c, bool numbered)
    {
        return new DerivativeStructure
        {
            Span = default,
            LeadingJoin = c.LeadingJoin,
            TrailingJoin = c.TrailingJoin,
            Joins = c.Joins,
            Pieces = [.. c.Pieces.Select(p => new SegmentPiece { Span = default, Points = [.. p.Points.Select(pt => Point(pt, numbered))] })],
        };
    }

    private static Breakpoint Point(BandPoint p, bool numbered)
    {
        var chromosome = p.Chromosome is { } id
            ? new ChromosomeRef { Span = default, Name = id.Name, IsUnderlined = id.IsUnderlined, Number = int.TryParse(id.Name, out var n) ? n : null }
            : null;
        var (landmark, text) = p.Kind switch
        {
            PointKind.Band => ((string?)null, p.Arm + p.Band),
            PointKind.Pter => ("pter", "pter"),
            PointKind.Qter => ("qter", "qter"),
            PointKind.Cen => ("cen", "cen"),
            PointKind.Hsr => ("hsr", "hsr"),
            _ => ("?", "?"),
        };
        return new Breakpoint
        {
            Span = new TextSpan(0, 0),
            Chromosome = chromosome,
            ChromosomeWritten = numbered && chromosome is not null && p.Kind is not (PointKind.Hsr or PointKind.Unknown),
            Text = text,
            Arm = p.Kind == PointKind.Band ? p.Arm : null,
            BandNumber = p.Kind == PointKind.Band ? p.Band : null,
            Landmark = landmark,
            Alternatives = ImmutableArray<Breakpoint>.Empty,
        };
    }
}
