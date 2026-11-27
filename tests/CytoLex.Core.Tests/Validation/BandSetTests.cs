using CytoLex.Core.Kb;

namespace CytoLex.Core.Tests.Validation;

/// <summary>T-101: stain in the runtime bundle and the chromosome/arm helpers of <see cref="BandSet"/>.</summary>
public class BandSetTests
{
    private static readonly BandSet _bands = KnowledgeBase.LoadEmbedded().ForEdition("2016")!.Bands!;

    [Fact]
    public void Stain_InTheBundle()
    {
        Assert.Equal("acen", _bands.Find("1p11.1")!.Stain);
        Assert.Equal("gvar", _bands.Find("1q12")!.Stain);
        Assert.Null(_bands.Find("22q11")!.Stain); // a derived nesting ancestor
        var at850 = _bands.Bands.Where(b => b.Levels.Contains(850)).ToList();
        Assert.All(at850.Where(b => b.Grch38 is not null), b => Assert.NotNull(b.Stain));
    }

    [Fact]
    public void Chromosomes_InKaryotypeOrder()
    {
        Assert.Equal([.. Enumerable.Range(1, 22).Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture)), "X", "Y"], _bands.Chromosomes);
        Assert.All(_bands.OfChromosome("21"), b => Assert.Equal("21", b.Chromosome));
        Assert.Empty(_bands.OfChromosome("23"));
    }

    [Fact]
    public void Ranges_ChromosomeAndArms()
    {
        var whole = _bands.ChromosomeRange("1", ReferenceBuild.Grch38)!;
        var p = _bands.ArmRange("1", "p", ReferenceBuild.Grch38)!;
        var q = _bands.ArmRange("1", "q", ReferenceBuild.Grch38)!;
        Assert.Equal(new GenomicRange(1, 248_956_422), whole); // GRCh38 chr1 length
        Assert.Equal(whole, p.Union(q));
        Assert.True(p.End < q.Start);
        Assert.Equal(249_250_621, _bands.ChromosomeRange("1", ReferenceBuild.Grch37)!.End);
        Assert.Null(_bands.ChromosomeRange("23", ReferenceBuild.Grch38));
    }
}
