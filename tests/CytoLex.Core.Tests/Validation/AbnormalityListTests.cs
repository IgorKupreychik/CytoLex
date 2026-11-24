using System.Text.Json;
using CytoLex.Core.Semantics;
using CytoLex.Core.Tests.KbExamples;
using CytoLex.Core.Tests.Parsing;
using CytoLex.Core.Tests.Semantics;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Tests.Validation;

/// <summary>T-069: the abnormality list of an edition result (<see cref="EditionResult.Abnormalities"/>, <see cref="EditionResult.CellLines"/>).</summary>
public class AbnormalityListTests
{
    private static EditionResult Result(string input, bool fragment = false) =>
        Assert.Single(Validator.Default.Validate(input, new ValidationOptions { Editions = EditionSelection.One("2016"), IsFragment = fragment }).Results);

    private static AbnormalityEntry One(string input) => Assert.Single(Result(input).Abnormalities);

    private static string[] Texts(IEnumerable<BreakpointSummary> breakpoints) => [.. breakpoints.Select(b => b.Text)];

    /// <summary>The entries of the karyotype cell lines (T-072 adds arr groups to the same list).</summary>
    private static List<AbnormalityEntry> KaryotypeEntries(EditionResult result) =>
        [.. result.Abnormalities.Where(a => result.CellLines[a.CellLine].Technique == SegmentTechnique.Karyotype)];

    public static IEnumerable<TheoryDataRow<string, bool, bool>> KbCases() =>
        ParseHelper.KbExamples()
            .Select(x => new TheoryDataRow<string, bool, bool>(x.Text, x.Fragment, x.Kind == ExampleKind.Invalid) { TestDisplayName = $"{x.RuleId} {x.Kind}: {x.Text}" });

    /// <summary>Every item of every KB karyotype example is listed with its span, kind, chromosomes, breakpoints, parts, cell line and partial flag.</summary>
    [Theory]
    [MemberData(nameof(KbCases))]
    public void KbExample_EveryItemListed(string text, bool fragment, bool invalid)
    {
        var result = Result(text, fragment);
        var models = BindHelper.Bind(text, fragment);
        var lines = models.SelectMany(m => m.CellLines).ToList();
        var items = lines.SelectMany(l => l.Items).Where(i => i is not OmittedItems).ToList();
        var karyotypeLines = result.CellLines.Where(c => c.Technique == SegmentTechnique.Karyotype).ToList();
        var entries = KaryotypeEntries(result);

        Assert.Equal(lines.Select(l => l.Span), karyotypeLines.Select(c => c.Span));
        Assert.Equal(items.Select(i => i.Span), entries.Select(a => a.Span));
        Assert.Equal(Enumerable.Range(0, result.Abnormalities.Length), result.Abnormalities.Select(a => a.Index));
        Assert.Equal(result.Abnormalities.Length, result.CellLines.Sum(c => c.AbnormalityCount));

        for (var i = 0; i < items.Count; i++)
        {
            var (item, entry) = (items[i], entries[i]);
            var line = result.CellLines[entry.CellLine];
            Assert.True(line.Span.Start <= entry.Span.Start && entry.Span.End <= line.Span.End, $"'{entry.Text}' outside its cell line '{line.Text}'");
            Assert.Equal(item.IsPartial, entry.Flags.IsPartial);
            Assert.Equal(item.Span.Of(text), entry.Text);
            Assert.False(string.IsNullOrEmpty(entry.ShortForm), $"'{entry.Text}' has no short form");
            Assert.All(entry.Diagnostics, d => Assert.True(
                result.Diagnostics[d].Span.Start >= entry.Span.Start && result.Diagnostics[d].Span.End <= entry.Span.End));
            if (!invalid)
            {
                Assert.False(entry.Flags.IsPartial, $"'{entry.Text}' is partial");
            }

            switch (item)
            {
                case WholeChromosomeChange w:
                    Assert.Equal(w.Sign == GainLoss.Loss ? AbnormalityKind.Loss : w.Sign == GainLoss.Gain ? AbnormalityKind.Gain : AbnormalityKind.Unrecognised, entry.Kind);
                    Assert.Equal([w.Chromosome.Name], entry.Chromosomes);
                    break;
                case Abnormality a:
                    Assert.True(entry.Kind is AbnormalityKind.Structural or AbnormalityKind.CloneReference);
                    Assert.Equal(a.Kind, entry.Symbol);
                    Assert.Equal(a.Breakpoints.Select(b => b.Span), entry.Breakpoints.Select(b => b.Span));
                    Assert.Equal(
                        a.Parts.SelectMany(p => p.Chromosomes).Select(c => (c.IsQuestioned ? "?" : "") + c.Name + c.Arm).Distinct(),
                        entry.Chromosomes);
                    Assert.Equal(a.Parts.Length > 1 ? a.Parts.Select(p => p.Span) : [], entry.Parts.Select(p => p.Span));
                    break;
                case ChromosomeRegion:
                    Assert.Equal(AbnormalityKind.Variant, entry.Kind);
                    break;
                case AlternativeItem alt:
                    Assert.Equal(AbnormalityKind.Alternatives, entry.Kind);
                    Assert.Equal(alt.Readings.Select(r => r.Span), entry.Readings.Select(r => r.Span));
                    break;
                case UnboundItem:
                    Assert.Equal(AbnormalityKind.Unrecognised, entry.Kind);
                    break;
                default:
                    break;
            }
        }
    }

    [Fact]
    public void CloneLines_NameTheLineTheyReferTo()
    {
        var result = Result("46,XX,t(9;22)(q34;q11.2)[3]/47,sl,+8[17]/48,sdl,+9[3]");
        Assert.Equal([null, 0, 1], result.CellLines.Select(l => l.BasedOn));
    }

    [Fact]
    public void Translocation_ShortFormAndBreakpoints()
    {
        var e = One("46,XY,t(9;22)(q34;q11.2)");
        Assert.Equal(AbnormalityKind.Structural, e.Kind);
        Assert.Equal("t", e.Symbol);
        Assert.Equal("t(9;22)", e.ShortForm);
        Assert.Equal(["9", "22"], e.Chromosomes);
        Assert.Equal(["9q34", "22q11.2"], Texts(e.Breakpoints));
        Assert.Equal("q", e.Breakpoints[1].Arm);
        Assert.Equal("11.2", e.Breakpoints[1].Band);
        Assert.Empty(e.Parts);
        Assert.Equal(new(6, 18), e.Span);
        Assert.Contains(e.Kb, k => k.ToString() == "vocab:symbols/t");
    }

    [Fact]
    public void GainAndLoss()
    {
        var result = Result("47,XX,+8,–7,+21");
        Assert.Equal([AbnormalityKind.Gain, AbnormalityKind.Loss, AbnormalityKind.Gain], result.Abnormalities.Select(a => a.Kind));
        Assert.Equal(["+8", "–7", "+21"], result.Abnormalities.Select(a => a.ShortForm));
    }

    [Theory]
    [InlineData("46,XX,del(5)(q13q33)", "del(5q)")]
    [InlineData("46,XX,i(17)(q10)", "i(17q)")]
    [InlineData("46,XY,inv(9)(p12q13)", "inv(9)")]
    [InlineData("46,XX,dup(2p)", "dup(2p)")]
    [InlineData("47,XX,+3mar", "+3mar")]
    [InlineData("47,XY,+der(22)t(9;22)(q34;q11.2)", "+der(22)t(9;22)")]
    public void ShortForms(string input, string expected) => Assert.Equal(expected, One(input).ShortForm);

    [Fact]
    public void Derivative_ListsItsParts()
    {
        var e = One("46,XX,der(1)t(1;3)(p32;q21)t(1;11)(q25;q13)");
        Assert.Equal("der", e.Symbol);
        Assert.Equal(["der(1)", "t(1;3)(p32;q21)", "t(1;11)(q25;q13)"], e.Parts.Select(p => p.Text));
        Assert.Equal(["der(1)", "t(1;3)", "t(1;11)"], e.Parts.Select(p => p.ShortForm));
        Assert.Equal(["1p32", "3q21"], Texts(e.Parts[1].Breakpoints));
        Assert.Equal(["1", "3", "11"], e.Chromosomes);
        Assert.Equal("der(1)t(1;3)t(1;11)", e.ShortForm);
    }

    [Fact]
    public void Mosaic_CellLines()
    {
        var result = Result("mos 45,X[15]/47,XXX[5]");
        Assert.Equal(2, result.CellLines.Length);
        Assert.All(result.CellLines, c => Assert.Equal(CellLineRelation.Mosaic, c.Relation));
        Assert.Equal(["45", "47"], result.CellLines.Select(c => c.Count));
        Assert.Equal(["X", "XXX"], result.CellLines.Select(c => c.Sex));
        Assert.Equal([15, 5], result.CellLines.Select(c => c.Cells));
        Assert.Equal([CellLineSeparator.None, CellLineSeparator.Slash], result.CellLines.Select(c => c.SeparatorBefore));
        Assert.Empty(result.Abnormalities);
    }

    [Fact]
    public void Neoplastic_CellLinesAndItems()
    {
        var result = Result("46,XX,t(8;21)(q22;q22)[10]/45,X,–X,t(8;21)(q22;q22)[10]");
        Assert.Equal([1, 2], result.CellLines.Select(c => c.AbnormalityCount));
        Assert.Equal([0, 1, 1], result.Abnormalities.Select(a => a.CellLine));
    }

    [Fact]
    public void Flags()
    {
        var c = One("46,XY,inv(9)(p12q13)c");
        Assert.True(c.Flags.Constitutional);
        Assert.False(c.Flags.IsQuestioned);

        Assert.Equal("mat", One("46,XX,der(7)t(2;7)(q21;q22)mat").Flags.Inheritance);
        Assert.Equal(2, One("48,XX,+i(17)(q10)×2").Flags.Copies);
        Assert.Equal("3", One("47,XX,+3mar").Flags.LeadingCount);

        var q = One("46,XX,?del(1)(q21)");
        Assert.True(q.Flags.IsQuestioned);
        Assert.True(q.Flags.IsUncertain);
        Assert.True(One("46,XX,del(1)(q2?3)").Flags.IsUncertain);
    }

    [Fact]
    public void Alternatives_ListReadings()
    {
        var e = One("46,XX,del(1)(p13) or dup(1)(p13)");
        Assert.Equal(AbnormalityKind.Alternatives, e.Kind);
        Assert.Equal(["del(1p)", "dup(1p)"], e.Readings.Select(r => r.ShortForm));
        Assert.Equal("del(1p) or dup(1p)", e.ShortForm);
        Assert.True(e.Flags.IsUncertain);
    }

    [Fact]
    public void Variant()
    {
        var e = One("46,XY,16qh+");
        Assert.Equal(AbnormalityKind.Variant, e.Kind);
        Assert.Equal(["16"], e.Chromosomes);
    }

    [Fact]
    public void Partial_WithLinkedDiagnostics()
    {
        var result = Result("46,XX,del(5)(q13q33");
        var e = Assert.Single(result.Abnormalities);
        Assert.True(e.Flags.IsPartial);
        Assert.NotEmpty(e.Diagnostics);
        Assert.All(e.Diagnostics, i => Assert.InRange(result.Diagnostics[i].Span.Start, e.Span.Start, e.Span.End));
    }

    [Fact]
    public void IshSegment_OwnGroup()
    {
        // T-092: the ish result is listed in its own group after the karyotype cell line.
        var result = Result("46,XX.ish 5p15.2(D5S23×2)");
        Assert.Equal([SegmentTechnique.Karyotype, SegmentTechnique.InSitu], result.CellLines.Select(c => c.Technique));
        Assert.Equal(1, Assert.Single(result.Abnormalities).CellLine);
    }

    [Fact]
    public void Json_RoundTrip()
    {
        var report = Validator.Default.Validate("46,XX,der(1)t(1;3)(p32;q21)t(1;11)(q25;q13),+8 or +9");
        var json = JsonSerializer.Serialize(report, Validator.JsonOptions);
        Assert.Contains("\"Abnormalities\"", json, StringComparison.Ordinal);
        var back = JsonSerializer.Deserialize<ValidationReport>(json, Validator.JsonOptions)!;
        Assert.Equal(json, JsonSerializer.Serialize(back, Validator.JsonOptions));
    }

    /// <summary>T-066: the detailed-system form of an abnormality with a band composition.</summary>
    [Fact]
    public void Detailed_FromTheComposition()
    {
        Assert.Equal("t(9;22)(9pter→9q34::22q11.2→22qter;22pter→22q11.2::9q34→9qter)", One("46,XX,t(9;22)(q34;q11.2)").Detailed);
        Assert.Equal("der(9)(9pter→9q34::22q11.2→22qter)", One("46,XX,der(9)t(9;22)(q34;q11.2)").Detailed);
        Assert.Null(One("47,XX,+8").Detailed);
        Assert.Null(One("47,XX,+mar").Detailed);
    }

    public static IEnumerable<TheoryDataRow<string>> PairShortForms() =>
        Composition.CompositionPairTests.Pairs
            .Where(p => !Composition.PendingCompositions.Table.ContainsKey(p.Cite) && !Composition.UndefinedCompositions.Table.ContainsKey(p.Cite))
            .SelectMany(p => p.ShortForms)
            .Distinct(StringComparer.Ordinal)
            .Select(s => new TheoryDataRow<string>(s));

    /// <summary>T-066 acceptance: on every KB pair printed in both systems, each abnormality with a composition lists its detailed form.</summary>
    [Theory]
    [MemberData(nameof(PairShortForms))]
    public void KbPair_ShortForm_ListsTheDetailedForm(string text)
    {
        var fragment = KbCatalog.IsFragment(text);
        var result = Result(text, fragment);
        var items = BindHelper.Bind(text, fragment).SelectMany(m => m.CellLines).SelectMany(l => l.Items).Where(i => i is not OmittedItems).ToList();
        var composed = 0;
        var entries = KaryotypeEntries(result);
        for (var i = 0; i < items.Count; i++)
        {
            var expected = (items[i] as Abnormality)?.Composition?.Detailed;
            Assert.Equal(expected, entries[i].Detailed);
            composed += expected is null ? 0 : 1;
        }

        Assert.True(composed > 0, "no abnormality with a composition");
    }
}
