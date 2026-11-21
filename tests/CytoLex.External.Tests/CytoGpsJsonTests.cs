using CytoLex.External.CytoGps;

namespace CytoLex.External.Tests;

/// <summary>The CytoGPS JSON reader, on hand-written output in the CytoGPS_Result.json schema (book strings only).</summary>
public sealed class CytoGpsJsonTests
{
    private const string _sample = """
        {"producer":"CytoGPS","date":"2026-11-21","iscn2016_bands":["9q34.1","9q34.2","9q34.3"],
         "output":[
          {"karyotype":"46,XY,t(9;22)(q34;q11.2)[20]/46,XY[5]","status":"Success","parsing_result":[
            {"cell_number":20,"loss_gain_fusion_computing":{"loss":[0,0,0],"gain":[0,0,0],"fusion":[1,1,1]}},
            {"cell_number":5,"relationship":"normal","uncertain_events":["?"],"derivative_chromosome_detailed_systems":["der(9)(…)"],
             "loss_gain_fusion_computing":{"loss":[0,0,0],"gain":[0,0,0],"fusion":[0,0,0]}}]},
          {"karyotype":"46,XY,t(9;22)(q34-q11.2)","status":"Fixable grammar error but containing validation error",
           "grammar_error":["-: Unrecognized token"],"revised_karyotype":"46,XY,t(9;22)(q34q11.2)","validation_error":["bad"]},
          {"karyotype":"46,XY,t(9;22)(q34;q11.2)[20","status":"Fixable grammar error and success","grammar_error":["missing ]"],
           "revised_karyotype":"46,XY,t(9;22)(q34;q11.2)[20]","parsing_result":[
            {"cell_number":20,"loss_gain_fusion_computing":{"loss":[0,0,0],"gain":[0,0,0],"fusion":[1,1,1]}}]},
          {"karyotype":"46,XY.ish del(22)(q11.2q11.2)(TUPLE1-)","status":"Nonfixable grammar error","grammar_error":["x"]},
          {"karyotype":"46,XY,del(5)(q33q13)","status":"Validation error","validation_error":["proximal first"]}
         ]}
        """;

    [Fact]
    public void Parse_ReadsAllFiveStatuses()
    {
        var output = CytoGpsJson.Parse(_sample);

        Assert.Equal(["9q34.1", "9q34.2", "9q34.3"], output.Bands);
        Assert.Equal(
            CytoGpsJson.Statuses.Order(StringComparer.Ordinal),
            output.Entries.Select(e => e.Status).Order(StringComparer.Ordinal));
        Assert.Equal(
            [ExternalVerdict.Valid, ExternalVerdict.Invalid, ExternalVerdict.Valid, ExternalVerdict.Invalid, ExternalVerdict.Invalid],
            output.Entries.Select(e => CytoGpsJson.VerdictOf(e.Status)));

        var clones = output.Entries[0].Clones;
        Assert.Equal(2, clones.Length);
        Assert.Equal(20, clones[0].CellCount);
        Assert.Equal([1, 1, 1], clones[0].Fusion);
        Assert.Equal("normal", clones[1].Relationship);
        Assert.Equal(["?"], clones[1].UncertainEvents);
        Assert.Equal(["der(9)(…)"], clones[1].DetailedSystems);

        var fixable = output.Entries[1];
        Assert.Equal("46,XY,t(9;22)(q34q11.2)", fixable.RevisedKaryotype);
        Assert.Equal([ExternalMessageKind.Grammar, ExternalMessageKind.Validation], fixable.Messages.Select(m => m.Kind));
        Assert.Empty(fixable.Clones);
    }

    [Fact]
    public void VerdictOf_UnknownStatus_IsFailed() =>
        Assert.Equal(ExternalVerdict.Failed, CytoGpsJson.VerdictOf("Something new"));

    [Fact]
    public void Parse_LgfLengthNotMatchingBands_Throws()
    {
        const string Json = """
            {"iscn2016_bands":["1p36.33","1p36.32"],"output":[{"karyotype":"46,XY","status":"Success","parsing_result":[
              {"loss_gain_fusion_computing":{"loss":[0],"gain":[0,0],"fusion":[0,0]}}]}]}
            """;
        var ex = Assert.Throws<FormatException>(() => CytoGpsJson.Parse(Json));
        Assert.Contains("'loss' has 1 values, expected 2", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"output":[]}""")]
    [InlineData("""{"iscn2016_bands":[],"output":[{"status":"Success"}]}""")]
    public void Parse_Malformed_ThrowsFormatException(string json) =>
        Assert.Throws<FormatException>(() => CytoGpsJson.Parse(json));
}
