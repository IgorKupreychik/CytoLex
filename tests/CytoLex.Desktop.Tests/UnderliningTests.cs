using CytoLex.Desktop.Editing;
using CytoLex.Desktop.ViewModels;

namespace CytoLex.Desktop.Tests;

/// <summary>T-042: the "Underline homologue" editor command (U+0332 after each underlined character) and underlined input in the check.</summary>
public sealed class UnderliningTests
{
    private static string Apply(string text, TextEdit? edit) =>
        edit is null ? text : text[..edit.Start] + edit.NewText + text[(edit.Start + edit.Length)..];

    [Fact]
    public void Selection_IsUnderlined()
    {
        const string text = "46,XX,der(10)t(10;12)";
        var edit = Underlining.Toggle(text, 10, 2);

        Assert.Equal("46,XX,der(1̲0̲)t(10;12)", Apply(text, edit));
        Assert.Equal((10, "1̲0̲"), (edit!.Start, edit.NewText));
    }

    [Theory]
    [InlineData(11)] // between the digits
    [InlineData(10)] // before the number, after "("
    [InlineData(12)] // after the number, before ")"
    public void Caret_UnderlinesTheNumberAtIt(int caret)
    {
        Assert.Equal("46,XX,der(1̲0̲)t(10;12)", Apply("46,XX,der(10)t(10;12)", Underlining.Toggle("46,XX,der(10)t(10;12)", caret, 0)));
    }

    [Fact]
    public void Caret_TakesOnlyTheDigitsBeforeAnArm()
    {
        const string text = "46,XX,t(2;7;7̲)(7p13→7pter)";
        var caret = text.IndexOf("7p13", StringComparison.Ordinal) + 1;

        Assert.Equal("46,XX,t(2;7;7̲)(7̲p13→7pter)", Apply(text, Underlining.Toggle(text, caret, 0)));
    }

    [Fact]
    public void Underlined_IsRemoved()
    {
        const string text = "46,XX,der(1̲0̲)t(10;12)";

        Assert.Equal("46,XX,der(10)t(10;12)", Apply(text, Underlining.Toggle(text, 11, 0)));
        Assert.Equal("46,XX,der(10)t(10;12)", Apply(text, Underlining.Toggle(text, 10, 3))); // selection ends before the last mark
    }

    [Fact]
    public void Mixed_IsUnderlinedThroughout_OtherCharactersUntouched()
    {
        const string text = "t(1̲;1)";

        Assert.Equal("t(1̲;1̲)", Apply(text, Underlining.Toggle(text, 2, 4)));
    }

    [Theory]
    [InlineData("46,XX;", 2, 1)] // only punctuation
    [InlineData("", 0, 0)]
    [InlineData("(;)", 1, 0)] // caret between punctuation
    public void NothingToUnderline_ReturnsNull(string text, int start, int length)
    {
        Assert.Null(Underlining.Toggle(text, start, length));
    }

    [Fact]
    public void UnderlinedHomologue_IsValidatedWithoutNote()
    {
        var vm = new MainWindowViewModel { Input = "46,XX,der(1)t(1;3)(p34.3;q21),der(1̲)t(1̲;3)(p34.3;q21)" };
        var panel = vm.SelectedPanel!;

        Assert.Equal("Valid in ISCN 2016", panel.ValidityText);
        Assert.All(panel.Segments, s => Assert.False(s.HasNote));
    }
}
