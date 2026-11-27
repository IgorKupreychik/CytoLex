using System.Text;
using CytoLex.Core.Batch;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Tests.Batch;

/// <summary>T-038: the JSON and CSV exports round-trip; an exported CSV reads back as batch input.</summary>
public class BatchExportTests
{
    private static readonly Lazy<BatchResult> _result = new(() =>
        BatchRunner.Run(Validator.Default, BatchReader.ReadFile(BatchSamples.PathOf("all.csv")).Items, new ValidationOptions { Editions = EditionSelection.All }));

    private static BatchResult Result => _result.Value;

    [Fact]
    public void Json_RoundTrips()
    {
        var json = Bytes(s => BatchExport.WriteJson(s, Result));
        using var input = new MemoryStream(json);

        var back = BatchExport.ReadJson(input);

        Assert.Equal(Result.Editions, back.Editions);
        Assert.Equal(Result.Items.Select(i => i.Item), back.Items.Select(i => i.Item));
        Assert.Equal(json, Bytes(s => BatchExport.WriteJson(s, back)));

        var text = Encoding.UTF8.GetString(json);
        Assert.Contains("\"Format\": \"cytolex-batch\"", text, StringComparison.Ordinal);
        Assert.Contains("×", text, StringComparison.Ordinal); // readable, not ×
    }

    [Fact]
    public void Json_RejectsOtherDocuments()
    {
        using var other = new MemoryStream(Encoding.UTF8.GetBytes("{\"Format\":\"something\",\"FormatVersion\":1}"));
        using var broken = new MemoryStream(Encoding.UTF8.GetBytes("not json"));

        Assert.Throws<InvalidDataException>(() => BatchExport.ReadJson(other));
        Assert.Throws<InvalidDataException>(() => BatchExport.ReadJson(broken));
    }

    [Fact]
    public void Csv_RoundTrips()
    {
        var csv = Bytes(s => BatchExport.WriteCsv(s, Result));
        Assert.Equal(Encoding.UTF8.GetPreamble(), csv[..3]);
        using var input = new MemoryStream(csv);

        var rows = BatchExport.ReadCsv(input);

        var expected = BatchExport.CsvRows(Result);
        Assert.Equal(Result.Items.Length * Result.Editions.Length, rows.Length);
        Assert.Equal(expected, rows);
        Assert.Contains(rows, r => r.Input.Contains('×', StringComparison.Ordinal));
        Assert.Contains(rows, r => r.Edition == "2016" && !r.Valid && r.Rules.Contains("Error:", StringComparison.Ordinal));
        Assert.Contains(rows, r => r.Status == EditionStatus.NotImplemented);
    }

    [Fact]
    public void Csv_ReadsBackAsBatchInput()
    {
        var csv = Bytes(s => BatchExport.WriteCsv(s, Result));

        var read = BatchReader.Read(csv, BatchFormat.Csv);

        Assert.Empty(read.Warnings);
        Assert.Equal(Result.Items.Select(i => i.Item), read.Items);
    }

    [Fact]
    public void Csv_RejectsOtherFiles()
    {
        using var other = new MemoryStream(Encoding.UTF8.GetBytes("id,iscn\n1,\"46,XX\"\n"));

        Assert.Throws<InvalidDataException>(() => BatchExport.ReadCsv(other));
    }

    [Fact]
    public void WriteFile_ByExtension()
    {
        var dir = Directory.CreateTempSubdirectory("cytolex-batch-");
        try
        {
            var json = Path.Combine(dir.FullName, "out.json");
            var csv = Path.Combine(dir.FullName, "out.csv");
            BatchExport.WriteFile(json, Result);
            BatchExport.WriteFile(csv, Result);

            using (var j = File.OpenRead(json))
            {
                Assert.Equal(Result.Items.Length, BatchExport.ReadJson(j).Items.Length);
            }

            Assert.Equal(Result.Items.Select(i => i.Item), BatchReader.ReadFile(csv).Items);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    // --- T-096: autocorrection ---

    private static readonly BatchItem[] _autoItems =
    [
        new(1, "a", "47,XY, +21"),
        new(2, null, "46,xy"),
        new(3, "c", "46, xy,inv(3)(q27p21)"),
        new(4, null, "46,XX"),
    ];

    private static readonly Lazy<BatchResult> _autoResult = new(() =>
        BatchRunner.Run(Validator.Default, _autoItems, new ValidationOptions { Editions = EditionSelection.All, AutoCorrect = true }));

    [Fact]
    public void AutoCorrection_Csv_CarriesOriginalCorrectedAndCorrections()
    {
        var rows = BatchExport.CsvRows(_autoResult.Value).Where(r => r.Edition == "2016").ToList();

        Assert.Equal(["47,XY, +21", "46,xy", "46, xy,inv(3)(q27p21)", "46,XX"], rows.Select(r => r.Input));
        Assert.Equal(
            [EditionVerdict.ValidAfterAutoCorrection, EditionVerdict.ValidAfterAutoCorrection, EditionVerdict.NotValid, EditionVerdict.Valid],
            rows.Select(r => r.Verdict));
        Assert.Equal(["47,XY,+21", "46,XY", "46,XY,inv(3)(q27p21)", string.Empty], rows.Select(r => r.Corrected));
        Assert.Equal("SYM.NO_OTHER_SPACE@6-7=", rows[0].AutoCorrections);
        Assert.Equal("SYM.LETTER_CASE@3-5=XY", rows[1].AutoCorrections);
        Assert.Equal(string.Empty, rows[3].AutoCorrections);
    }

    [Fact]
    public void AutoCorrection_Csv_RoundTrips_AndReadsBackAsBatchInput()
    {
        var csv = Bytes(s => BatchExport.WriteCsv(s, _autoResult.Value));
        using var input = new MemoryStream(csv);

        Assert.Equal(BatchExport.CsvRows(_autoResult.Value), BatchExport.ReadCsv(input));

        var read = BatchReader.Read(csv, BatchFormat.Csv);
        Assert.Empty(read.Warnings);
        Assert.Equal(_autoItems, read.Items);
    }

    [Fact]
    public void AutoCorrection_Json_RoundTrips()
    {
        var json = Bytes(s => BatchExport.WriteJson(s, _autoResult.Value));
        using var input = new MemoryStream(json);

        var back = BatchExport.ReadJson(input);

        Assert.Equal(json, Bytes(s => BatchExport.WriteJson(s, back)));
        var result = back.Items[0].Report.Results.Single(r => r.Edition == "2016");
        Assert.Equal(EditionVerdict.ValidAfterAutoCorrection, result.Verdict);
        Assert.Equal("47,XY,+21", result.AutoCorrection!.Corrected);
        Assert.Equal("47,XY, +21", back.Items[0].Report.Input);
        Assert.Contains("\"FormatVersion\": 5", Encoding.UTF8.GetString(json), StringComparison.Ordinal);
    }

    [Fact]
    public void Json_ReadsVersion1()
    {
        using var v1 = new MemoryStream(Encoding.UTF8.GetBytes("{\"Format\":\"cytolex-batch\",\"FormatVersion\":1,\"Editions\":[\"2016\"],\"Items\":[]}"));

        Assert.Empty(BatchExport.ReadJson(v1).Items);
    }

    // --- T-070: inherited changes and full forms of idem/sl/sdl lines ---

    [Fact]
    public void Json_CarriesCloneReferences()
    {
        var result = BatchRunner.Run(
            Validator.Default,
            [new BatchItem(1, null, "46,XX,t(9;22)(q34;q11.2)[7]/43,sl,-7,-9,-15[11]")],
            new ValidationOptions { Editions = EditionSelection.One("2016") });
        var json = Bytes(s => BatchExport.WriteJson(s, result));
        using var input = new MemoryStream(json);

        var back = BatchExport.ReadJson(input);

        Assert.Equal(json, Bytes(s => BatchExport.WriteJson(s, back)));
        var line = back.Items[0].Report.Results[0].CellLines[1];
        Assert.Equal("43,XX,-7,-9,t(9;22)(q34;q11.2),-15[11]", line.FullForm);
        Assert.Equal("sl", line.Reference!.Symbol);
        Assert.Equal(0, Assert.Single(line.Inherited).SourceCellLine);
        Assert.Empty(back.Items[0].Report.Results[0].CellLines[0].Inherited);
    }

    /// <summary>T-101: the loci go into the export and a version 3 file (no loci) still reads.</summary>
    [Fact]
    public void Json_CarriesLoci_ReadsVersion3()
    {
        var result = BatchRunner.Run(Validator.Default, [new BatchItem(1, null, "46,XX,del(5)(q13q33)")], new ValidationOptions { Editions = EditionSelection.One("2016") });
        var text = Encoding.UTF8.GetString(Bytes(s => BatchExport.WriteJson(s, result)));
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(text));
        Assert.Equal("5q13", Assert.Single(BatchExport.ReadJson(input).Items[0].Report.Results[0].Loci).From);

        var doc = System.Text.Json.Nodes.JsonNode.Parse(text)!;
        doc["FormatVersion"] = 3;
        foreach (var edition in doc["Items"]![0]!["Report"]!["Results"]!.AsArray())
        {
            edition!.AsObject().Remove("Loci");
        }

        var v3 = doc.ToJsonString();
        using var old = new MemoryStream(Encoding.UTF8.GetBytes(v3));
        Assert.Empty(BatchExport.ReadJson(old).Items[0].Report.Results[0].Loci);
    }

    [Fact]
    public void Json_ReadsVersion2()
    {
        var json = Encoding.UTF8.GetString(Bytes(s => BatchExport.WriteJson(s, _autoResult.Value)))
            .Replace("\"FormatVersion\": 5", "\"FormatVersion\": 2", StringComparison.Ordinal);
        using var v2 = new MemoryStream(Encoding.UTF8.GetBytes(json));

        Assert.Equal(_autoItems.Length, BatchExport.ReadJson(v2).Items.Length);
    }

    [Fact]
    public void Csv_ReadsPreAutoCorrectionExports()
    {
        var header = string.Join(',', BatchExport.CsvColumnsV1);
        var text = header + "\n1,,\"46,XX\",2016,Implemented,true,Constitutional;Acquired,true,0,0,0,0,\n2,,\"46,XX\",2020,NotImplemented,false,,false,0,0,0,0,\n";
        using var v1 = new MemoryStream(Encoding.UTF8.GetBytes(text));

        var rows = BatchExport.ReadCsv(v1);

        Assert.Equal([EditionVerdict.Valid, EditionVerdict.NotImplemented], rows.Select(r => r.Verdict));
        Assert.All(rows, r => Assert.Equal(string.Empty, r.Corrected));
        Assert.Equal(2, BatchReader.Read(Encoding.UTF8.GetBytes(text), BatchFormat.Csv).Items.Length);
    }

    [Fact]
    public void AutoCorrection_Summary_CountsValidAfterAutoCorrection()
    {
        var s2016 = BatchSummary.Of(_autoResult.Value).Single(s => s.Edition == "2016");

        Assert.Equal(1, s2016.Valid);
        Assert.Equal(2, s2016.ValidAfterAutoCorrection);
        Assert.Equal(1, s2016.NotValid);
    }

    private static byte[] Bytes(Action<Stream> write)
    {
        using var stream = new MemoryStream();
        write(stream);
        return stream.ToArray();
    }
}
