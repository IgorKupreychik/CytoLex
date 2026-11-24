using System.Text.Json;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Tests.Validation;

/// <summary>T-096: <see cref="OffsetMap"/> maps spans of a corrected string back to the original, across rounds.</summary>
public class OffsetMapTests
{
    private static string Edit(string text, params (TextSpan Span, string NewText)[] edits) =>
        Fixer.Apply(text, edits.Select(e => new TextEdit(e.Span, e.NewText)));

    [Fact]
    public void Identity_MapsEveryOffsetToItself()
    {
        var map = OffsetMap.Identity(5);

        Assert.Equal(5, map.Length);
        Assert.Equal(new TextSpan(1, 3), map.ToOriginal(new TextSpan(1, 3)));
        Assert.Equal(new TextSpan(5, 0), map.ToOriginal(new TextSpan(5, 0)));
    }

    [Fact]
    public void Delete_ShiftsLaterOffsets()
    {
        const string original = "47,XY, +21";
        (TextSpan, string)[] edits = [(new TextSpan(6, 1), string.Empty)];
        var corrected = Edit(original, edits);
        var map = OffsetMap.Identity(original.Length).Apply(edits);

        Assert.Equal("47,XY,+21", corrected);
        Assert.Equal(corrected.Length, map.Length);
        var plus = new TextSpan(6, 3); // "+21" in the corrected string
        Assert.Equal("+21", map.ToOriginal(plus).Of(original));
        Assert.Equal("47,XY,", map.ToOriginal(new TextSpan(0, 6)).Of(original));
    }

    [Fact]
    public void Insert_MapsInsertedTextToThePoint()
    {
        const string original = "46XX";
        (TextSpan, string)[] edits = [(new TextSpan(2, 0), ",")];
        var map = OffsetMap.Identity(original.Length).Apply(edits);

        Assert.Equal(new TextSpan(2, 0), map.ToOriginal(new TextSpan(2, 1)));
        Assert.Equal("XX", map.ToOriginal(new TextSpan(3, 2)).Of(original));
        Assert.Equal(original, map.ToOriginal(new TextSpan(0, 5)).Of(original));
    }

    [Fact]
    public void Replace_OfDifferentLength()
    {
        const string original = "46,XX,Del(5)";
        (TextSpan, string)[] edits = [(new TextSpan(6, 3), "deletion")];
        var corrected = Edit(original, edits);
        var map = OffsetMap.Identity(original.Length).Apply(edits);

        Assert.Equal("46,XX,deletion(5)", corrected);
        Assert.Equal("Del", map.ToOriginal(new TextSpan(8, 2)).Of(original));
        Assert.Equal("(5)", map.ToOriginal(new TextSpan(14, 3)).Of(original));
    }

    [Fact]
    public void Rounds_Compose()
    {
        const string original = "46, xy,del (5)";
        (TextSpan, string)[] round1 = [(new TextSpan(3, 1), string.Empty), (new TextSpan(10, 1), string.Empty)];
        var text1 = Edit(original, round1);
        var map1 = OffsetMap.Identity(original.Length).Apply(round1);
        (TextSpan, string)[] round2 = [(new TextSpan(3, 2), "XY")];
        var text2 = Edit(text1, round2);
        var map2 = map1.Apply(round2);

        Assert.Equal("46,XY,del(5)", text2);
        Assert.Equal("xy", map2.ToOriginal(new TextSpan(3, 2)).Of(original));
        Assert.Equal("del", map2.ToOriginal(new TextSpan(6, 3)).Of(original));
        Assert.Equal("(5)", map2.ToOriginal(new TextSpan(9, 3)).Of(original));
    }

    [Fact]
    public void Overlapping_Throws() =>
        Assert.Throws<ArgumentException>(() => OffsetMap.Identity(5).Apply([(new TextSpan(0, 3), "a"), (new TextSpan(2, 1), "b")]));

    [Fact]
    public void Json_RoundTrips()
    {
        var map = OffsetMap.Identity(10).Apply([(new TextSpan(6, 1), string.Empty), (new TextSpan(2, 0), "!")]);

        var back = JsonSerializer.Deserialize<OffsetMap>(JsonSerializer.Serialize(map, Validator.JsonOptions), Validator.JsonOptions)!;

        Assert.Equal(map.OriginalLength, back.OriginalLength);
        Assert.Equal(map.Pieces, back.Pieces);
    }
}
