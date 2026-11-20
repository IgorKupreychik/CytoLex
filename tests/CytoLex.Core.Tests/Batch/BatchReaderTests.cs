using System.Text;
using CytoLex.Core.Batch;

namespace CytoLex.Core.Tests.Batch;

/// <summary>T-038: reading batch files (text, CSV, TSV; quoting, BOM, header detection, warnings).</summary>
public class BatchReaderTests
{
    [Fact]
    public void Text_OneStringPerLine_SkipsBlankLines()
    {
        var result = BatchReader.Read("46,XX\r\n\n  47,XY,+21  \n\n", BatchFormat.Text);

        Assert.Equal([new BatchItem(1, null, "46,XX"), new BatchItem(3, null, "47,XY,+21")], result.Items);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Csv_QuotedCommasQuotesAndLineBreaks()
    {
        const string text = "id,iscn,note\n"
            + "a1,\"46,XX\",plain\n"
            + "a2,\"47,XY,+21\",\"says \"\"hi\"\"\"\n"
            + "a3,\"46,XY,\n"
            + "del(5)(q13q33)\",split\n"
            + "a4,\"46,XX\",last";
        var result = BatchReader.Read(text, BatchFormat.Csv);

        Assert.Equal(
            [
                new BatchItem(2, "a1", "46,XX"),
                new BatchItem(3, "a2", "47,XY,+21"),
                new BatchItem(4, "a3", "46,XY,\ndel(5)(q13q33)"),
                new BatchItem(6, "a4", "46,XX"),
            ],
            result.Items);
        Assert.Empty(result.Warnings);
    }

    [Theory]
    [InlineData("karyotype")]
    [InlineData("ISCN")]
    [InlineData("Input")]
    [InlineData("string")]
    public void Csv_StringColumnByHeaderName(string header)
    {
        var result = BatchReader.Read($"n,{header}\n1,\"46,XX\"\n", BatchFormat.Csv);

        Assert.Equal("46,XX", Assert.Single(result.Items).Input);
        Assert.Null(result.Items[0].Id);
    }

    [Fact]
    public void Csv_WithoutKnownHeader_TakesTheFirstColumn()
    {
        var result = BatchReader.Read("sample,lab\n\"46,XX\",x\n", BatchFormat.Csv);

        Assert.Equal("46,XX", Assert.Single(result.Items).Input);
    }

    [Fact]
    public void Csv_EmptyStringCell_IsSkippedWithWarning()
    {
        var result = BatchReader.Read("id,iscn\nx1,\nx2,\"46,XX\"\nx3\n", BatchFormat.Csv);

        Assert.Equal("x2", Assert.Single(result.Items).Id);
        Assert.Equal(2, result.Warnings.Length);
        Assert.Contains("line 2", result.Warnings[0], StringComparison.Ordinal);
        Assert.Contains("line 4", result.Warnings[1], StringComparison.Ordinal);
    }

    [Fact]
    public void Csv_UnclosedQuote_IsAWarning()
    {
        var result = BatchReader.Read("iscn\n\"46,XX\n", BatchFormat.Csv);

        Assert.Equal("46,XX", Assert.Single(result.Items).Input);
        Assert.Contains(result.Warnings, w => w.Contains("not closed", StringComparison.Ordinal));
    }

    [Fact]
    public void Csv_TextAfterClosingQuote_IsAWarning()
    {
        var result = BatchReader.Read("iscn\n\"46,XX\"x\n", BatchFormat.Csv);

        Assert.Equal("46,XXx", Assert.Single(result.Items).Input);
        Assert.Contains(result.Warnings, w => w.Contains("line 2", StringComparison.Ordinal));
    }

    [Fact]
    public void Tsv_TabSeparated()
    {
        var result = BatchReader.Read("id\tiscn\nt1\t46,XX,t(2;5)(p23;q35)\n", BatchFormat.Tsv);

        Assert.Equal(new BatchItem(2, "t1", "46,XX,t(2;5)(p23;q35)"), Assert.Single(result.Items));
    }

    [Fact]
    public void Bytes_Utf8Bom_AndSymbols()
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("iscn\n\"arr[GRCh38] 7q11.23(73330452_74769740)×1\"\n")).ToArray();

        var result = BatchReader.Read(bytes, BatchFormat.Csv);

        Assert.Equal("arr[GRCh38] 7q11.23(73330452_74769740)×1", Assert.Single(result.Items).Input);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Bytes_Utf16Bom()
    {
        var bytes = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("46,XX\n47,XXX\n")).ToArray();

        Assert.Equal(["46,XX", "47,XXX"], BatchReader.Read(bytes, BatchFormat.Text).Items.Select(i => i.Input));
    }

    [Fact]
    public void Bytes_NotUtf8_ReadAsLatin1WithWarning()
    {
        byte[] bytes = [.. Encoding.ASCII.GetBytes("47,XY,+21"), 0xD7, (byte)'\n']; // 0xD7 = × in Latin-1

        var result = BatchReader.Read(bytes, BatchFormat.Text);

        Assert.Equal("47,XY,+21×", Assert.Single(result.Items).Input);
        Assert.Single(result.Warnings);
    }

    [Theory]
    [InlineData("a.csv", BatchFormat.Csv)]
    [InlineData("a.TSV", BatchFormat.Tsv)]
    [InlineData("a.txt", BatchFormat.Text)]
    [InlineData("a", BatchFormat.Text)]
    public void FormatOf_ByExtension(string path, BatchFormat expected) => Assert.Equal(expected, BatchReader.FormatOf(path));
}
