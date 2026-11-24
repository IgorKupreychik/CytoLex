using CytoLex.External.Comparison;

namespace CytoLex.External.Tests.Comparison;

public sealed class TriageFileTests
{
    private const string _header = "format: cytolex-triage\nversion: 1\nchecker: \"CytoGPS\"\nedition: \"2016\"\n";

    [Fact]
    public void Parse_ReadsEntries()
    {
        var file = TriageFile.Parse(_header + """
            entries:
              - iscn: "47,XY,+21,del(5)(q13q33)"
                label: checker-limitation
                confirmed: true
                note: "order"
              - iscn: "46,XY,t(9;22)(q34;q11.2)×2"
                label: our-bug
                expected: valid
                kb_rule: KARYO.MULTIPLY_SIGN_AFTER_ABNORMALITY
            """);

        Assert.Equal("CytoGPS", file.Checker);
        Assert.Equal("2016", file.Edition);
        Assert.Equal(2, file.Entries.Length);
        var first = file.Find("47,XY,+21,del(5)(q13q33)")!;
        Assert.Equal(TriageLabel.CheckerLimitation, first.Label);
        Assert.True(first.Confirmed);
        Assert.Equal("order", first.Note);
        Assert.Equal(6, first.Line);
        var second = file.Entries[1];
        Assert.False(second.Confirmed);
        Assert.Equal(ExpectedVerdict.Valid, second.Expected);
        Assert.Equal("KARYO.MULTIPLY_SIGN_AFTER_ABNORMALITY", second.KbRule);
        Assert.Null(file.Find("46,XY"));
    }

    [Theory]
    [InlineData("entries:\n  - iscn: \"46,XY\"\n    label: x\n", 6, "is not one of")]
    [InlineData("entries:\n  - iscn: \"46,XY\"\n    label: our-bug\n  - iscn: \"46,XY\"\n    label: our-bug\n", 8, "duplicate iscn")]
    [InlineData("entries:\n  - iscn: \"46,XY\"\n    label: our-bug\n    expected: valid\n", 6, "go together")]
    [InlineData("entries:\n  - iscn: \"46,XY\"\n    label: our-bug\n    kb_rule: X.Y\n", 6, "go together")]
    [InlineData("entries:\n  - iscn: \"46,XY\"\n    label: our-bug\n    colour: red\n", 8, "unknown key")]
    [InlineData("entries:\n  - iscn: \"46,XY\"\n    label: our-bug\n    confirmed: yes\n", 6, "true or false")]
    public void Parse_RejectsBadEntries_NamingTheLine(string body, int line, string message)
    {
        var ex = Assert.Throws<TriageFileException>(() => TriageFile.Parse(_header + body, "t.yaml"));
        Assert.Contains($"t.yaml:{line}:", ex.Message, StringComparison.Ordinal);
        Assert.Contains(message, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_RejectsWrongFormat() =>
        Assert.Throws<TriageFileException>(() => TriageFile.Parse("format: other\nversion: 1\nchecker: c\nedition: e\n"));

    [Fact]
    public void ToYaml_RoundTrips()
    {
        var file = new TriageFile("CytoGPS", "2016",
        [
            new TriageEntry("46,XY,del(5)(q13q33)\"x\\", TriageLabel.VersionDifference, true, "a \"note\"", ExpectedVerdict.Invalid, "DEL.FORMAT"),
            new TriageEntry("46,xy", TriageLabel.CheckerLimitation, false, null, null, null),
            new TriageEntry("47,XX,+min mar", TriageLabel.SeverityPolicy, true, null, null, null),
        ]);

        var yaml = file.ToYaml();
        var back = TriageFile.Parse(yaml);

        Assert.DoesNotContain('\r', yaml);
        Assert.Equal(file.Entries.Select(e => e with { Line = 0 }), back.Entries.Select(e => e with { Line = 0 }));
        Assert.Equal(yaml, back.ToYaml());
    }

    [Fact]
    public void CommittedTriageFile_Loads()
    {
        var file = TriageFile.Load(Path.Combine(AppContext.BaseDirectory, "data", "comparison", "cytogps-2016-triage.yaml"));
        Assert.Equal("CytoGPS", file.Checker);
        Assert.Equal("2016", file.Edition);
    }

    [Fact]
    public void CommittedTriageFile_RoundTripsByteForByte_WithItsHeader()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "data", "comparison", "cytogps-2016-triage.yaml");
        var text = File.ReadAllText(path);
        var file = TriageFile.Load(path);

        Assert.NotEmpty(file.Header);
        Assert.All(file.Header, l => Assert.StartsWith("#", l, StringComparison.Ordinal));
        Assert.Equal(text, file.ToYaml());
    }

    [Fact]
    public void With_ReplacesInPlaceOrAppends_AndWithoutRemoves()
    {
        var file = TriageFile.Parse("# head\nformat: cytolex-triage\nversion: 1\nchecker: \"CytoGPS\"\nedition: \"2016\"\nentries:\n"
            + "  - iscn: \"a\"\n    label: our-bug\n  - iscn: \"b\"\n    label: our-bug\n");

        var replaced = file.With(new TriageEntry("a", TriageLabel.SeverityPolicy, true, "n", null, null));
        Assert.Equal(["a", "b"], replaced.Entries.Select(e => e.Iscn));
        Assert.Equal(TriageLabel.SeverityPolicy, replaced.Find("a")?.Label);
        Assert.Equal(["# head"], replaced.Header);

        var appended = replaced.With(new TriageEntry("c", TriageLabel.OurBug, false, null, null, null));
        Assert.Equal(["a", "b", "c"], appended.Entries.Select(e => e.Iscn));

        var removed = appended.Without("b");
        Assert.Equal(["a", "c"], removed.Entries.Select(e => e.Iscn));
        Assert.Null(removed.Find("b"));
        Assert.Same(removed, removed.Without("zzz"));
        Assert.StartsWith("# head\nformat:", removed.ToYaml(), StringComparison.Ordinal);
    }

    [Fact]
    public void Save_WritesLfWithoutBom()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cytolex-triage-{Guid.NewGuid():N}.yaml");
        try
        {
            var file = TriageFile.Empty("CytoGPS", "2016").With(new TriageEntry("46,XY", TriageLabel.OurBug, true, "x", null, null));
            file.Save(path);
            var bytes = File.ReadAllBytes(path);
            Assert.NotEqual(0xEF, bytes[0]);
            Assert.DoesNotContain((byte)'\r', bytes);
            Assert.Equal(file.Entries.Select(e => e with { Line = 0 }), TriageFile.Load(path).Entries.Select(e => e with { Line = 0 }));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
