namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// <c>BAND.MUST_EXIST</c> (§2.4 ¶2, Fig. 5): a band name must be one of the edition's band set. Accepted without a band
/// entry: <c>p10</c>/<c>q10</c> (<c>BAND.CENTROMERE_10</c>) and a region alone when the arm has that region
/// (<c>1p1</c>, <c>BREAK.REGION_ONLY</c>). Skipped: landmarks, uncertain bands (<c>q2?3</c>), bands without a known
/// chromosome, bands of an invalid chromosome (<c>CHROM.IDENTIFIERS</c>) and recovered input.
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
            context.Report(bp.BandSpan(), detail);
        }
    }
}
