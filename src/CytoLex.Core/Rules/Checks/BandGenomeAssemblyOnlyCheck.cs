namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// <c>BAND.GENOME_ASSEMBLY_ONLY</c> (T-029; policy T-022): a band of the genome assemblies that the book's idiograms do not
/// show (<c>book: false</c>, e.g. <c>9q34.11</c>) is accepted with an info note naming the source. The KB rule applies
/// to karyotype and in situ hybridization strings only, so array and sequence strings get no note.
/// </summary>
[KbRule("BAND.GENOME_ASSEMBLY_ONLY")]
internal sealed class BandGenomeAssemblyOnlyCheck : IRuleCheck
{
    public void Check(RuleContext context)
    {
        foreach (var bp in context.SegmentModel.Breakpoints().Where(b => !b.IsPartial && b.KbBand is { Book: false }))
        {
            context.Report(
                bp.BandSpan(),
                $"{bp.KbBand!.Name} is a band of the GRCh37/GRCh38 genome assemblies (NCBI) but not of the ISCN {context.Edition} idiograms.");
        }
    }
}
