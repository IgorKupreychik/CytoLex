using CytoLex.Core.Composing;
using CytoLex.Core.Semantics;
using CytoLex.Core.Tests.Semantics;

namespace CytoLex.Core.Tests.Composition;

/// <summary>
/// T-066: the band composition of each rearrangement type, on synthetic strings (the book's own pairs are in
/// <see cref="CompositionPairTests"/>). Expected forms follow the type's KB rule and <c>STRUCT.COMPOSITION_ORDER</c>.
/// </summary>
public class CompositionTypeTests
{
    private static Abnormality First(string text) =>
        BindHelper.Bind(text).SelectMany(m => m.CellLines).SelectMany(l => l.Items).OfType<Abnormality>().First();

    private static AbnormalityComposition Composed(string text) => Assert.IsType<AbnormalityComposition>(First(text).Composition);

    [Theory]
    // DEL.FORMAT: terminal (p and q arm) and interstitial
    [InlineData("46,XX,del(1)(p31)", "del(1)(:p31→qter)")]
    [InlineData("46,XX,del(1)(q32)", "del(1)(pter→q32:)")]
    [InlineData("46,XX,del(2)(q21q31)", "del(2)(pter→q21::q31→qter)")]
    // ADD.FORMAT
    [InlineData("46,XX,add(1)(q32)", "add(1)(pter→q32::?)")]
    // INV.FORMAT: paracentric, pericentric
    [InlineData("46,XX,inv(1)(p13p31)", "inv(1)(pter→p31::p13→p31::p13→qter)")]
    [InlineData("46,XX,inv(9)(p12q13)", "inv(9)(pter→p12::q13→p12::q13→qter)")]
    // DUP/TRP/QDP.FORMAT: direct (proximal band first) and inverted, p and q arm
    [InlineData("46,XX,dup(1)(p13p22)", "dup(1)(pter→p13::p22→qter)")]
    [InlineData("46,XX,dup(2)(q33q21)", "dup(2)(pter→q33::q33→q21::q33→qter)")]
    [InlineData("46,XX,trp(3)(q21q26)", "trp(3)(pter→q26::q21→q26::q21→qter)")]
    [InlineData("46,XX,qdp(1)(q32q23)", "qdp(1)(pter→q32::q32→q23::q23→q32::q32→q23::q32→qter)")]
    // INS.ORIENTATION: within one chromosome, between two
    [InlineData("46,XX,ins(3)(q27p21p24)", "ins(3)(pter→p24::p21→q27::p21→p24::q27→qter)")]
    [InlineData("46,XX,ins(4;9)(q31;p13p22)", "ins(4;9)(4pter→4q31::9p13→9p22::4q31→4qter;9pter→9p22::9p13→9qter)")]
    [InlineData("46,XX,ins(4;?)(q31;?)", "ins(4;?)(4pter→4q31::?::4q31→4qter)")]
    // ISO.FORMAT, DIC.FORMAT (idic, dic)
    [InlineData("46,XX,i(5)(p10)", "i(5)(pter→p10::p10→pter)")]
    [InlineData("46,X,idic(X)(p11.2)", "idic(X)(qter→p11.2::p11.2→qter)")]
    [InlineData("45,XX,dic(5;10)(q31;p12)", "dic(5;10)(5pter→5q31::10p12→10qter)")]
    // RING.FORMAT
    [InlineData("46,XX,r(4)(p16q35)", "r(4)(::p16→q35::)")]
    // TRANSLOC.FORMAT: two chromosomes, underlined homologue; TRANSLOC.WHOLE_ARM via rob
    [InlineData("46,XX,t(1;7)(q21;p15)", "t(1;7)(1pter→1q21::7p15→7pter;1qter→1q21::7p15→7qter)")]
    [InlineData("46,XX,t(9;9̲)(p13;q22)", "t(9;9̲)(9̲qter→9̲q22::9p13→9qter;9̲pter→9̲q22::9p13→9pter)")]
    [InlineData("45,XX,rob(13;14)(q10;q10)", "rob(13;14)(13qter→13q10::14q10→14qter)")]
    // TAS.FORMAT, TRC.FORMAT, FIS.FORMAT, HSR.FORMAT
    [InlineData("46,XX,tas(2;6)(p25;q27)", "tas(2;6)(2qter→2pter→6qter→6pter)")]
    [InlineData("44,XX,trc(1;4;9)(q42;p16q35;p24)", "trc(1;4;9)(1pter→1q42::4p16→4q35::9p24→9qter)")]
    [InlineData("47,XY,–3,+fis(3)(p10),+fis(3)(q10)", "+fis(3)(pter→p10:)")]
    [InlineData("46,XX,hsr(2)(q24)", "hsr(2)(pter→q24::hsr::q24→qter)")]
    // DER.CENTROMERE_CHROMOSOME: aberrations in turn; DER.ISODERIVATIVE
    [InlineData("46,XX,der(2)t(2;11)(q35;p13)", "der(2)(2pter→2q35::11p13→11pter)")]
    [InlineData("46,XX,der(3)del(3)(p21)dup(3)(q21q26)", "der(3)(:p21→q26::q21→qter)")]
    [InlineData("46,XX,ider(17)(q10)t(15;17)(q22;q21)", "ider(17)(15qter→15q22::17q21→17q10::17q10→17q21::15q22→15qter)")]
    // REC.DEFINITION (§4.5 ¶4): both recombinants of a pericentric inversion
    [InlineData("46,XX,rec(2)dup(2p)inv(2)(p21q31)mat", "rec(2)(pter→q31::p21→pter)")]
    [InlineData("46,XX,rec(2)dup(2q)inv(2)(p21q31)mat", "rec(2)(qter→q31::p21→qter)")]
    public void ShortForm_ComposesTo(string text, string detailed)
    {
        var composition = Composed(text);
        Assert.Equal(CompositionSource.Computed, composition.Source);
        Assert.Equal(detailed, composition.Detailed);
    }

    [Fact]
    public void Fission_SecondProduct_StartsAtTheLongArmEnd()
    {
        var items = BindHelper.Bind("47,XY,–3,+fis(3)(p10),+fis(3)(q10)").SelectMany(m => m.CellLines).SelectMany(l => l.Items).OfType<Abnormality>().ToList();
        Assert.Equal("+fis(3)(qter→q10:)", items[^1].Composition!.Detailed);
    }

    [Fact]
    public void Translocation_ThreeChromosomes_EachReceivesFromTheOneBefore()
    {
        // §9.2.17.1 ¶1: the second chromosome receives from the first, the third from the second, the first from the last
        var composition = Composed("46,XX,t(1;4;6)(p32;q21;q23)");
        Assert.Equal(
            ["6qter→6q23::1p32→1qter", "4pter→4q21::1p32→1pter", "6pter→6q23::4q21→4qter"],
            composition.Products.Select(p => p.Format(true)));
    }

    [Fact]
    public void DetailedSystem_IsReadAsWritten()
    {
        var composition = Composed("46,XX,der(1)(3qter→3q13.1::1p22→1qter)");
        Assert.Equal(CompositionSource.Written, composition.Source);
        Assert.Equal("der(1)(3qter→3q13.1::1p22→1qter)", composition.Detailed);
        Assert.Contains(KbRef.Rule("STRUCT.DETAILED_SYSTEM"), composition.Kb);
    }

    [Fact]
    public void ShortAndDetailedForms_HaveEqualCompositions()
    {
        var computed = Composed("46,XX,der(2)t(2;11)(q35;p13)");
        var written = Composed("46,XX,der(2)(2pter→2q35::11p13→11pter)");
        Assert.Equal(written.Products, computed.Products);
    }

    [Theory]
    [InlineData("46,XX,del(5)(q13q33)", "DEL.FORMAT")]
    [InlineData("46,XX,t(1;7)(q21;p15)", "TRANSLOC.FORMAT")]
    [InlineData("45,XX,rob(13;14)(q10;q10)", "ROB.FORMAT")]
    [InlineData("46,XX,der(2)t(2;11)(q35;p13)", "DER.CENTROMERE_CHROMOSOME")]
    [InlineData("46,XX,i(5)(p10)", "ISO.FORMAT")]
    public void Composition_TracesToItsRules(string text, string rule)
    {
        var kb = Composed(text).Kb;
        Assert.Contains(KbRef.Rule(rule), kb);
        Assert.Contains(KbRef.Rule("STRUCT.SHORT_AND_DETAILED_SYSTEMS"), kb);
        Assert.Contains(KbRef.Rule("STRUCT.COMPOSITION_ORDER"), kb);
        Assert.All(kb, k => Assert.NotNull(BindHelper.Kb2016.Rule(k.Id)));
    }

    [Theory]
    [InlineData("47,XX,+mar")] // no composition: markers, minutes, fragile sites, uniparental disomy
    [InlineData("47,XX,+mar1")]
    [InlineData("46,XX,4dmin")]
    [InlineData("46,XX,fra(10)(q25.2)")]
    [InlineData("46,XX,del(5)(q2?3)")] // uncertain breakpoints
    [InlineData("46,XX,del(5)(q13~q21)")]
    [InlineData("46,XX,t(1;3)(p32 or p34;q21)")]
    [InlineData("46,XX,der(1)t(1;3)(p32;q21)t(5;7)(q11;p13)")] // an aberration that doesn't touch the derivative
    [InlineData("46,XX,rec(21)del(21)ins(21)(p13q22.2q22.3)pat")] // insertion recombinants: no rule defines the crossover product
    [InlineData("47,XX,+der(?)t(?;9)(?;p13)ins(?;7)(?;q11.2q32)")] // insertion into the unknown chromosome
    public void NoComposition(string text) => Assert.Null(First(text).Composition);

    [Fact]
    public void PartialAbnormality_HasNoComposition()
    {
        var a = First("46,XX,del(5)(q13");
        Assert.True(a.IsPartial);
        Assert.Null(a.Composition);
    }

    /// <summary>The symbols without a composition are listed (and documented) in one place.</summary>
    [Fact]
    public void SymbolsWithoutComposition_AreListed() =>
        Assert.Equal(["ace", "dmin", "fra", "idem", "inc", "mar", "neo", "sdl", "sl", "upd"], Composer.WithoutComposition.Order(StringComparer.Ordinal));
}
