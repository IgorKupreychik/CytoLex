using CytoLex.Core.Semantics;

namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// <c>BAND.MUST_EXIST</c> (§2.4 ¶2, Fig. 5): a band name must be one of the edition's band set. Accepted without a band
/// entry: <c>p10</c>/<c>q10</c> (<c>BAND.CENTROMERE_10</c>) and a region alone when the arm has that region
/// (<c>1p1</c>, <c>BREAK.REGION_ONLY</c>). Skipped: landmarks, uncertain bands (<c>q2?3</c>), bands without a known
/// chromosome, bands of an invalid chromosome (<c>CHROM.IDENTIFIERS</c>) and recovered input. Each finding carries up
/// to three "did you mean" bands (<see cref="BandSuggestions"/>, T-041).
/// </summary>
[KbRule("BAND.MUST_EXIST")]
internal sealed class BandMustExistCheck : IRuleCheck
{
    public void Check(RuleContext context)
    {
        if (context.Kb.Bands is not { } bands)
        {
            return;
        }

        var positions = ArrPositions(context);
        var bookOnly = context.Arr is null;
        foreach (var bp in context.SegmentModel.Breakpoints())
        {
            if (bp.IsPartial || bp.KbBand is not null || bp.Name is not { } name || bp.Arm is not ("p" or "q")
                || bp.Chromosome is not { } chromosome || !chromosome.IsValidIdentifier())
            {
                continue;
            }

            var number = bp.BandNumber!;
            if (number == "10" || (number.Length == 1 && bands.ContainsRegion(name)))
            {
                continue;
            }

            var detail = number.Length == 1
                ? $"Chromosome {chromosome.Name} has no region {bp.Arm}{number}."
                : $"Chromosome {chromosome.Name} has no band {bp.Arm}{number} in the ISCN {context.Edition} idiograms.";
            (long, string)? position = positions.TryGetValue(bp, out var p) ? p : null;
            context.Report(bp.BandSpan(), detail, suggestions: BandSuggestions.For(context.Input, bands, bp, bookOnly, position));
        }
    }

    /// <summary>
    /// The position each band of an arr region stands for in a GRCh37/GRCh38 string: the first band the region's first
    /// position, the last band its last position (<c>ARR.POSITIONS_IN_BANDS</c> readings).
    /// </summary>
    private static Dictionary<Breakpoint, (long, string)> ArrPositions(RuleContext context)
    {
        var result = new Dictionary<Breakpoint, (long, string)>(ReferenceEqualityComparer.Instance);
        if (context.Arr is not { Build.Text: ArrPositionsInBandsCheck.Grch37 or ArrPositionsInBandsCheck.Grch38 } model)
        {
            return result;
        }

        var build = model.Build!.Text;
        foreach (var reading in ArrPositionsInBandsCheck.Readings(model))
        {
            if (reading.Region.First is { } first && reading.First.Start is { } start)
            {
                result.TryAdd(first, (start, build));
            }

            if (reading.Region.Last is { } last && (reading.Last.End ?? reading.Last.Start) is { } end)
            {
                result.TryAdd(last, (end, build));
            }
        }

        return result;
    }
}
