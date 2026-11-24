using System.Text.RegularExpressions;
using CytoLex.Core.Tests.KbExamples;
using CytoLex.Core.Tests.Parsing;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Tests.Validation;

/// <summary>
/// T-070: lines written with <c>idem</c>/<c>sl</c>/<c>sdl</c> list the changes they inherit (<see cref="CellLineSummary.Inherited"/>)
/// and their full form (<see cref="CellLineSummary.FullForm"/>).
/// </summary>
public partial class CloneExpansionTests
{
    /// <summary>The §6 order rules of <c>ItemOrder</c> (the full form is sorted by them; ORDER.SHORTEST_STRING_PREFERRED is a style hint about the written notation, not checked).</summary>
    private static readonly HashSet<string> _orderRules =
    [
        "KARYO.ABNORMALITY_ORDER", "ORDER.X_BEFORE_Y", "ORDER.NUMERICAL_BEFORE_STRUCTURAL", "ORDER.HOMOLOGUES_ALPHABETICAL",
        "ORDER.UNKNOWN_CENTROMERE_DER", "ORDER.UNIDENTIFIED_LAST", "RING.UNIDENTIFIED", "DMIN.FORMAT",
    ];

    private static EditionResult Result(string input) =>
        Assert.Single(Validator.Default.Validate(input, new ValidationOptions { Editions = EditionSelection.One("2016") }).Results);

    /// <summary>
    /// Hand-written expansions, reviewed against §11.1.4 (CLONE.SUBCLONE_REFERENCE, CLONE.POLYPLOID_SUBCLONE): the full form
    /// of every cell line (<c>null</c> for lines written in full), changes in §6 order.
    /// </summary>
    public static TheoryData<string, string?[]> FullForms() => new()
    {
        // Stakeholder example (T-070).
        { "46,XX,t(9;22)(q34;q11.2)[7]/43,sl,-7,-9,-15[11]", [null, "43,XX,-7,-9,t(9;22)(q34;q11.2),-15[11]"] },

        // E1: sdl1/sdl2 chain and the idem form.
        {
            "46,XX,t(9;22)(q34;q11.2)[3]/47,sl,+8[17]/48,sdl1,+9[3]/49,sdl2,+11[12]",
            [null, "47,XX,+8,t(9;22)(q34;q11.2)[17]", "48,XX,+8,+9,t(9;22)(q34;q11.2)[3]", "49,XX,+8,+9,t(9;22)(q34;q11.2),+11[12]"]
        },
        {
            "46,XX,t(9;22)(q34;q11.2)[3]/47,idem,+8[17]/48,idem,+8,+9[3]/49,idem,+8,+9,+11[12]",
            [null, "47,XX,+8,t(9;22)(q34;q11.2)[17]", "48,XX,+8,+9,t(9;22)(q34;q11.2)[3]", "49,XX,+8,+9,t(9;22)(q34;q11.2),+11[12]"]
        },

        // E2: a loss of X leaves one X in the field (SEX.ACQUIRED_GAIN_LOSS).
        {
            "46,XX,t(8;21)(q22;q22)[12]/45,sl,–X[19]/46,sdl1,+8[5]/47,sdl2,+9[8]",
            [null, "45,X,–X,t(8;21)(q22;q22)[19]", "46,X,–X,+8,t(8;21)(q22;q22)[5]", "47,X,–X,+8,t(8;21)(q22;q22),+9[8]"]
        },

        // E3: omitted changes of the stemline.
        {
            "48,XX,t(12;16)(q13;p11.1),…[23]/49,sl,+6[8]/50,sdl,+7,–8,+9[4]",
            [null, "49,XX,+6,t(12;16)(q13;p11.1),…[8]", "50,XX,+6,+7,–8,+9,t(12;16)(q13;p11.1),…[4]"]
        },

        // E4, E5, E8, E10: polyploid references.
        { "26,X,+4,+6,+21[3]/52,idem×2[13]", [null, "52,XX,+4,+4,+6,+6,+21,+21[13]"] },
        { "46,XY,t(9;22)(q34;q11.2)[3]/92,sl×2[5]/93,sdl,+8[2]", [null, "92,XXYY,t(9;22)(q34;q11.2)×2[5]", "93,XXYY,+8,t(9;22)(q34;q11.2)×2[2]"] },
        { "46,XY,t(9;22)(q34;q11.2)[3]/92,idem×2[5]/93,idem×2,+8[2]", [null, "92,XXYY,t(9;22)(q34;q11.2)×2[5]", "93,XXYY,+8,t(9;22)(q34;q11.2)×2[2]"] },
        { "48,XX,t(12;16)(q13;p11.1),…[31]/96,idem×2[6]", [null, "96,XXXX,t(12;16)(q13;p11.1)×2,…[6]"] },
        {
            "48,XX,t(12;16)(q13;p11.1),…[7]/96,sl×2,inv(3)(q21q27),t(3;6)(p25;q21)[19]",
            [null, "96,XXXX,inv(3)(q21q27),t(3;6)(p25;q21),t(12;16)(q13;p11.1)×2,…[19]"]
        },

        // E6: sl after a sideline goes back to the stemline.
        {
            "46,XY,t(9;22)(q34;q11.2)[3]/47,sl,+8[10]/48,sdl,+der(22)t(9;22)[4]/47,sl,+19[3]",
            [null, "47,XY,+8,t(9;22)(q34;q11.2)[10]", "48,XY,+8,t(9;22)(q34;q11.2),+der(22)t(9;22)[4]", "47,XY,t(9;22)(q34;q11.2),+19[3]"]
        },

        // E7: a second stemline.
        {
            "45,XY,–7[5]/46,sl,+8[6]/46,XY,t(9;22)(q34;q11.2)[3]/92,sl2×2[5]/93,sl2×2,+8[2]",
            [null, "46,XY,–7,+8[6]", null, "92,XXYY,t(9;22)(q34;q11.2)×2[5]", "93,XXYY,+8,t(9;22)(q34;q11.2)×2[2]"]
        },

        // E13: +3 undoes the stemline's –3; E14: –inv(6) removes the stemline's inv(6).
        { "53,XY,t(1;6)(p34;q22),–3,…[13]/57,sl,+3,+del(7)(q11.2),+8,+9[22]", [null, "57,XY,t(1;6)(p34;q22),+del(7)(q11.2),+8,+9,…[22]"] },
        { "49,XX,inv(6)(p21q25),…[17]/52,idem,–inv(6),+7,+8,+9,+mar[11]", [null, "52,XX,+7,+8,+9,+mar,…[11]"] },

        // Composite cell count and count range kept as written.
        {
            "46,XY,t(9;22)(q34;q11.2)[5]/51~52,sl,+1,+5,–7,+8,+11,+13,+19,+der(22)t(9;22)[cp4]",
            [null, "51~52,XY,+1,+5,–7,+8,t(9;22)(q34;q11.2),+11,+13,+19,+der(22)t(9;22)[cp4]"]
        },

        // Constitutional changes are inherited too.
        { "48,XY,+8,+21c[3]/49,idem,+9[5]/47,XY,+21c[12]", [null, "49,XY,+8,+9,+21c[5]", null] },

        // Synthetic: doubled losses, a partly cancelled doubled loss, a regained X.
        { "45,XY,–7[5]/90,idem×2[4]", [null, "90,XXYY,–7,–7[4]"] },
        { "45,XY,–7[5]/91,idem×2,+7[4]", [null, "91,XXYY,–7[4]"] },
        { "45,X,–X,+8[5]/46,idem,+X[4]", [null, "46,XX,+8[4]"] },
        { "47,XY,+3mar[5]/94,idem×2[4]", [null, "94,XXYY,+6mar[4]"] },
    };

    [Theory]
    [MemberData(nameof(FullForms))]
    public void FullForm_MatchesHandWrittenExpansion(string input, string?[] expected)
    {
        var result = Result(input);

        Assert.Equal(expected, result.CellLines.Select(l => l.FullForm));
    }

    [Fact]
    public void Stakeholder_InheritedEntriesPointToTheirSource()
    {
        const string input = "46,XX,t(9;22)(q34;q11.2)[7]/43,sl,-7,-9,-15[11]";
        var result = Result(input);
        var line = result.CellLines[1];

        Assert.Equal(new CloneReferenceSummary(new(31, 2), "sl", null, 1, 0), line.Reference);
        var inherited = Assert.Single(line.Inherited);
        var source = result.Abnormalities[inherited.Entry];
        Assert.Equal(0, inherited.SourceCellLine);
        Assert.Equal(0, source.CellLine);
        Assert.Equal("t(9;22)(q34;q11.2)", source.Span.Of(input));
        Assert.Equal((1, "t(9;22)(q34;q11.2)", (int?)null), (inherited.Copies, inherited.Text, inherited.RemovedBy));
        Assert.Null(result.CellLines[0].Reference);
        Assert.Empty(result.CellLines[0].Inherited);
    }

    [Fact]
    public void Chain_KeepsTheLineWhereTheChangeIsWritten()
    {
        var result = Result("46,XX,t(9;22)(q34;q11.2)[3]/47,sl,+8[17]/48,sdl1,+9[3]/49,sdl2,+11[12]");

        var last = result.CellLines[3];
        Assert.Equal(2, last.Reference!.TargetLine);
        Assert.Equal(
            [("t(9;22)(q34;q11.2)", 0), ("+8", 1), ("+9", 2)],
            last.Inherited.Select(i => (i.Text, i.SourceCellLine)));
    }

    [Fact]
    public void Polyploid_MultipliesInheritedCopies()
    {
        var result = Result("46,XY,t(9;22)(q34;q11.2)[3]/92,sl×2[5]/93,sdl,+8[2]");

        Assert.Equal(2, result.CellLines[1].Reference!.Multiplier);
        Assert.Equal((2, "t(9;22)(q34;q11.2)×2", 0), result.CellLines[2].Inherited.Select(i => (i.Copies, i.Text, i.SourceCellLine)).Single());
    }

    [Fact]
    public void MinusSignedStemlineAbnormality_RemovesTheInheritedOne()
    {
        const string input = "49,XX,inv(6)(p21q25),…[17]/52,sl,–inv(6),+7,+8,+9,+mar[11]";
        var result = Result(input);

        var removed = Assert.Single(result.CellLines[1].Inherited);
        Assert.Equal((0, "inv(6)(p21q25)"), (removed.Copies, removed.Text));
        Assert.Equal("–inv(6)", result.Abnormalities[removed.RemovedBy!.Value].Text);
    }

    [Fact]
    public void GainOfAStemlineLoss_RemovesTheInheritedLoss()
    {
        var result = Result("53,XY,t(1;6)(p34;q22),–3,…[13]/57,sl,+3,+del(7)(q11.2),+8,+9[22]");

        var inherited = result.CellLines[1].Inherited;
        Assert.Equal([("t(1;6)(p34;q22)", 1, false), ("–3", 0, true)], inherited.Select(i => (i.Text, i.Copies, i.RemovedBy is not null)));
        Assert.Equal("+3", result.Abnormalities[inherited[1].RemovedBy!.Value].Text);
    }

    /// <summary>
    /// <c>//</c>: the expansion follows <see cref="CellLineSummary.BasedOn"/> on both sides. The binder resolves references
    /// over the whole string; the meaning of a reference on the donor side is open (T-106).
    /// </summary>
    [Theory]
    [InlineData("46,XX,t(9;22)(q34;q11.2)[3]/47,idem,+8[17]//46,XY[20]")]
    [InlineData("46,XX[20]//46,XY,t(9;22)(q34;q11.2)[3]/47,sl,+8[5]")]
    [InlineData("46,XY,t(9;22)(q34;q11.2)[3]/47,sl,+8[17]//46,XX,del(5)(q13q33)[4]/47,sdl,+9[2]")]
    public void DoubleSlash_FollowsBasedOn(string input)
    {
        var result = Result(input);

        foreach (var line in result.CellLines.Where(l => l.Reference is not null))
        {
            Assert.Equal(line.BasedOn, line.Reference!.TargetLine);
            Assert.NotNull(line.FullForm);
            Assert.All(line.Inherited, i => Assert.True(i.SourceCellLine < line.Index));
        }

        Assert.Contains(result.CellLines, l => l.Reference is not null);
    }

    public static IEnumerable<TheoryDataRow<string>> KbReferenceExamples() =>
        ParseHelper.KbExamples()
            .Where(x => x.Kind == ExampleKind.Valid && !x.Fragment && ReferenceWord().IsMatch(x.Text))
            .Select(x => x.Text)
            .Distinct(StringComparer.Ordinal)
            .Select(t => new TheoryDataRow<string>(t) { TestDisplayName = t });

    /// <summary>
    /// Every valid KB example with idem/sl/sdl: each referenced line lists the referred clone's changes (copies × the
    /// <c>×n</c>), minus the ones its own items remove, each pointing to an entry of an earlier line; its full form is set
    /// and, written alone, keeps the §6 order and (without <c>…</c>) matches its count.
    /// </summary>
    [Theory]
    [MemberData(nameof(KbReferenceExamples))]
    public void KbExample_ReferencedLinesExpand(string input)
    {
        var result = Result(input);
        var referenced = result.CellLines.Where(l => l.Reference is not null).ToList();
        Assert.NotEmpty(referenced);

        foreach (var line in referenced)
        {
            var target = result.CellLines[line.Reference!.TargetLine!.Value];
            var expected = Changes(result, target).ToDictionary(kv => kv.Key, kv => kv.Value * line.Reference.Multiplier);
            Assert.Equal(expected.Keys.Order(), line.Inherited.Select(i => i.Entry).Order());
            foreach (var i in line.Inherited)
            {
                Assert.Equal(i.RemovedBy is null ? expected[i.Entry] : 0, i.Copies);
                Assert.Equal(i.SourceCellLine, result.Abnormalities[i.Entry].CellLine);
                Assert.True(i.SourceCellLine < line.Index);
            }

            Assert.NotNull(line.FullForm);
            var alone = Result(line.FullForm);
            var faults = alone.Diagnostics.Where(d => _orderRules.Contains(d.RuleId)
                || (!line.FullForm.Contains('…', StringComparison.Ordinal) && d.RuleId.StartsWith("NUM.", StringComparison.Ordinal)));
            Assert.Empty(faults.Select(d => $"{d.RuleId}: {d.Message}"));
        }
    }

    /// <summary>The changes a line contains, by entry, with their copies (kept inherited ones and own non-reference ones not used up by a removal).</summary>
    private static Dictionary<int, int> Changes(EditionResult result, CellLineSummary line)
    {
        var changes = line.Inherited.Where(i => i.RemovedBy is null).ToDictionary(i => i.Entry, i => i.Copies);
        var cancelling = line.Inherited.Select(i => i.RemovedBy).OfType<int>().ToHashSet();
        foreach (var e in result.Abnormalities.Where(a => a.CellLine == line.Index && a.Kind != AbnormalityKind.CloneReference && !cancelling.Contains(a.Index)))
        {
            changes[e.Index] = e.Flags.Copies ?? 1;
        }

        return changes;
    }

    [GeneratedRegex(@"\b(idem|sl|sdl)\d*\b")]
    private static partial Regex ReferenceWord();
}
