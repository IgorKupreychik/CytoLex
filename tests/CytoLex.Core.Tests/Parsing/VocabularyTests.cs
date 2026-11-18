using System.Text;
using CytoLex.Core.Kb;

namespace CytoLex.Core.Tests.Parsing;

public class VocabularyTests
{
    [Fact]
    public void Embedded2016_HasSymbolsAndBandingCodes()
    {
        var vocab = Vocabulary.LoadEmbedded("2016");
        Assert.Equal("2016", vocab.Edition);
        Assert.Equal("vocab:symbols/del", vocab.Find("del")?.KbRef);
        Assert.Equal("vocab:banding_codes/GTG", vocab.Find("GTG")?.KbRef);
        Assert.Contains("9.2.2", vocab.Find("del")!.Sections);
    }

    [Fact]
    public void SharedToken_HasNoSingleEntry()
    {
        var vocab = Vocabulary.LoadEmbedded("2016");
        Assert.Equal(2, vocab.Lookup(".").Length);
        Assert.Null(vocab.Find("."));
    }

    [Fact]
    public void TypedForms_AreMarked()
    {
        var minus = Vocabulary.LoadEmbedded("2016").Find("-")!;
        Assert.True(minus.IsTypedForm("-"));
        Assert.False(minus.IsTypedForm("–"));
    }

    [Fact]
    public void UnknownEdition_Throws() =>
        Assert.Throws<KeyNotFoundException>(() => Vocabulary.LoadEmbedded("1995"));

    [Fact]
    public void UnsupportedFormatVersion_Throws()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("""{"format_version": 99, "editions": {}}"""));
        Assert.Throws<InvalidDataException>(() => Vocabulary.Load(stream, "2016"));
    }
}
