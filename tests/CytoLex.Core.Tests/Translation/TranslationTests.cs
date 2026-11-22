using System.Text.Json;
using CytoLex.Core.Context;
using CytoLex.Core.Translation;
using CytoLex.Core.Validation;
using KbCatalog = CytoLex.Core.Tests.KbExamples.KbCatalog;

namespace CytoLex.Core.Tests.Translation;

/// <summary>
/// T-058: the translation in the validation result: off by default, partial translation of strings with errors,
/// readings per context, KB links with titles, draft flags, the optional coordinates, and JSON round-trip.
/// </summary>
public class TranslationTests
{
    private static EditionResult Validate(string input, SampleContext? hint = null, bool coordinates = false, bool fragment = false) =>
        Validator.Default.Validate(input, new ValidationOptions
        {
            Editions = EditionSelection.One("2016"),
            ContextHint = hint,
            IncludeTranslation = true,
            IncludeCoordinates = coordinates,
            IsFragment = fragment,
        }).Results.Single();

    private static TranslationResult Translate(string input, SampleContext? hint = null, bool coordinates = false) =>
        Validate(input, hint, coordinates).Translation!;

    [Fact]
    public void Translation_IsOffByDefault()
    {
        var result = Validator.Default.Validate("46,XY,del(5)(q13q33)").Results.Single();
        Assert.Null(result.Translation);
    }

    [Fact]
    public void ValidString_HasSummaryItemsAndNoBanner()
    {
        var t = Translate("46,XX,del(5)(q13q33)");
        Assert.Equal("46 chromosomes; sex chromosomes XX; 1 abnormality.", t.Summary.Text);
        var item = Assert.Single(t.Items);
        Assert.Equal("del(5)(q13q33)", item.Text);
        Assert.Equal(6, item.Span.Start);
        Assert.Equal("Interstitial deletion of chromosome 5: the segment between 5q13 and 5q33 is lost.", item.Sentence);
        Assert.True(item.IsTranslated);
        Assert.Null(t.Banner);
        Assert.False(t.IsPartial);
        Assert.Equal("en", t.Language);
    }

    [Fact]
    public void Links_CarryTitles()
    {
        var item = Assert.Single(Translate("46,XX,del(5)(q13q33)").Items);
        var del = Assert.Single(item.Links, l => l.Ref.ToString() == "vocab:symbols/del");
        Assert.Equal("Deletion", del.Title);
        var format = Assert.Single(item.Links, l => l.Ref.ToString() == "rule:DEL.FORMAT");
        Assert.Equal(KbRuleTitle("DEL.FORMAT"), format.Title);
        var band = Assert.Single(item.Links, l => l.Ref.ToString() == "band:5q13");
        Assert.Equal("5q13", band.Title);
    }

    private static string KbRuleTitle(string id) => Kb.KnowledgeBase.LoadEmbedded().ForEdition("2016")!.Rule(id)!.Title;

    [Fact]
    public void Drafts_AreFlagged()
    {
        // Every template is a draft until T-082.
        var t = Translate("47,XY,+21");
        Assert.True(t.IsDraft);
        Assert.True(t.Summary.IsDraft);
        Assert.All(t.Items, i => Assert.True(i.IsDraft));
    }

    [Fact]
    public void UnknownContext_GivesNeutralSentenceAndBothReadings()
    {
        var t = Translate("47,XY,+21");
        var item = Assert.Single(t.Items);
        Assert.Equal("Gain of chromosome 21.", item.Sentence);
        Assert.NotNull(item.Readings);
        Assert.Equal("An extra chromosome 21 (constitutional).", item.Readings.Constitutional);
        Assert.Equal("Gain of chromosome 21 (acquired).", item.Readings.Acquired);
        Assert.Equal(ReadingBasis.Unknown, t.ReadingBasis);
    }

    [Fact]
    public void ContextHint_GivesOneReading()
    {
        var t = Translate("47,XY,+21", SampleContext.Constitutional);
        var item = Assert.Single(t.Items);
        Assert.Equal("An extra chromosome 21 (constitutional).", item.Sentence);
        Assert.Null(item.Readings);
        Assert.Equal(ReadingBasis.Hint, t.ReadingBasis);
    }

    [Fact]
    public void InferredContext_GivesOneReadingWithItsBasis()
    {
        // idem marks an acquired (neoplastic) karyotype.
        var t = Translate("46,XX,t(9;22)(q34;q11.2)[3]/47,idem,+8[17]");
        Assert.Equal(ReadingBasis.Inferred, t.ReadingBasis);
        var gain = Assert.Single(t.Items, i => i.Text == "+8");
        Assert.Equal("Gain of chromosome 8 (acquired).", gain.Sentence);
        Assert.Null(gain.Readings);
    }

    [Fact]
    public void SyntaxError_LeavesThePartUntranslated()
    {
        var result = Validate("46,XY,+8,del(5)(q13q33");
        var t = result.Translation!;
        Assert.Equal("Not valid ISCN 2016; partial translation.", t.Banner);
        Assert.True(t.IsPartial);
        var bad = Assert.Single(t.Items, i => !i.IsTranslated);
        Assert.StartsWith("Not translated", bad.Sentence, StringComparison.Ordinal);
        Assert.NotEmpty(bad.DiagnosticIndices);
        Assert.All(bad.DiagnosticIndices, i => Assert.InRange(i, 0, result.Diagnostics.Length - 1));
        Assert.Contains(t.Items, i => i.IsTranslated && i.Text == "+8" && i.Sentence == "Gain of chromosome 8.");
    }

    [Fact]
    public void RuleErrorOnly_IsTranslatedAsWrittenAndPointsToItsDiagnostics()
    {
        // Chromosome order in t(): a rule-check error, not a syntax error.
        var result = Validate("46,XY,t(5;2)(q31;q21)");
        Assert.False(result.Validity.IsValid);
        var t = result.Translation!;
        Assert.Equal("Not valid ISCN 2016; partial translation.", t.Banner);
        var item = Assert.Single(t.Items);
        Assert.True(item.IsTranslated);
        Assert.Equal(
            "Translocation between chromosomes 5 and 2, with breaks at 5q31 and 2q21; the segments distal to the breaks are exchanged.",
            item.Sentence);
        Assert.Contains(item.DiagnosticIndices, i => result.Diagnostics[i].RuleId == "STRUCT.CHROMOSOME_ORDER");
    }

    [Fact]
    public void NotImplementedSegment_IsAnUntranslatedPart()
    {
        var t = Translate("46,XX.ish del(22)(q11.2q11.2)(HIRA-)");
        var ish = Assert.Single(t.Items);
        Assert.False(ish.IsTranslated);
        Assert.Equal("Not translated: ish translation is not implemented.", ish.Sentence);
        Assert.Equal("46 chromosomes; sex chromosomes XX; no abnormality.", t.Summary.Text);
        Assert.True(t.IsPartial);
    }

    [Fact]
    public void Coordinates_AreOffUnlessRequested()
    {
        Assert.All(Translate("46,XX,del(5)(q13q33)").Items, i => Assert.Null(i.Coordinates));
    }

    [Fact]
    public void Coordinates_GiveBandsAndSegmentSize()
    {
        var kb = Kb.KnowledgeBase.LoadEmbedded().ForEdition("2016")!;
        var q13 = kb.Bands!.Find("5q13")!;
        var q33 = kb.Bands!.Find("5q33")!;
        var item = Assert.Single(Translate("46,XX,del(5)(q13q33)", coordinates: true).Items);
        var c = item.Coordinates!;
        Assert.Equal(["5q13", "5q33"], c.Bands.Select(b => b.Band));
        Assert.Equal(q13.Grch38, c.Bands[0].Grch38);
        var size = Assert.Single(c.Segments);
        Assert.Equal(q33.Grch38!.End - q13.Grch38!.Start + 1, size.Grch38Bases);
        Assert.Equal(q33.Grch37!.End - q13.Grch37!.Start + 1, size.Grch37Bases);
    }

    [Fact]
    public void Coordinates_BookOnlyBand_HasNoCoordinates()
    {
        var kb = Kb.KnowledgeBase.LoadEmbedded().ForEdition("2016")!;
        var bookOnly = kb.Bands!.Bands.FirstOrDefault(b => b.Book && b.Grch37 is null && b.Grch38 is null && b.Name.Contains('q', StringComparison.Ordinal));
        if (bookOnly is null)
        {
            Assert.Skip("The band data has no book-only band without coordinates.");
        }

        var arm = bookOnly.Name[bookOnly.Chromosome.Length..];
        var item = Assert.Single(Translate($"46,XX,del({bookOnly.Chromosome})({arm})", coordinates: true).Items);
        var band = Assert.Single(item.Coordinates!.Bands);
        Assert.True(band.IsBookOnly);
        Assert.Null(band.Grch37);
        Assert.Null(band.Grch38);
    }

    [Fact]
    public void Translation_RoundTripsThroughJson()
    {
        var t = Translate("mos 45,X[15]/46,XX,del(5)(q13q33)[10]", coordinates: true);
        var json = JsonSerializer.Serialize(t, Validator.JsonOptions);
        var back = JsonSerializer.Deserialize<TranslationResult>(json, Validator.JsonOptions)!;
        Assert.Equal(json, JsonSerializer.Serialize(back, Validator.JsonOptions));
        Assert.Contains("\"ReadingBasis\":\"Unknown\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void CloneReference_NamesItsCellLine()
    {
        var t = Translate("46,XX,t(9;22)(q34;q11.2)[3]/47,idem,+8[17]");
        Assert.Contains(t.Items, i => i.Text == "idem" && i.Sentence == "All abnormalities of the stemline (cell line 1).");
    }

    public static TheoryData<string> AllExampleKeys => [.. KbCatalog.Default.Cases.Select(c => c.Key)];

    /// <summary>The renderer never throws on what the parser accepts: every KB example (valid, invalid, corrected) is translated or marked.</summary>
    [Theory]
    [MemberData(nameof(AllExampleKeys))]
    public void EveryKbExample_IsTranslatedWithoutError(string key)
    {
        var c = KbCatalog.Default.Find(key);
        foreach (var text in new[] { c.Text, c.Corrected }.OfType<string>())
        {
            var result = Validate(text, fragment: c.IsFragment);
            var t = result.Translation;
            Assert.NotNull(t);
            Assert.False(string.IsNullOrWhiteSpace(t.Summary.Text));
            Assert.All(t.Items, i => Assert.False(string.IsNullOrWhiteSpace(i.Sentence)));
            Assert.All(t.Items, i => Assert.DoesNotContain("{", i.Sentence, StringComparison.Ordinal));
            Assert.All(t.Items, i => Assert.DoesNotContain("no wording", i.Sentence, StringComparison.Ordinal));
        }
    }
}
