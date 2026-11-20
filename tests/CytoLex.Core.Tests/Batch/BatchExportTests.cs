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

    private static byte[] Bytes(Action<Stream> write)
    {
        using var stream = new MemoryStream();
        write(stream);
        return stream.ToArray();
    }
}
