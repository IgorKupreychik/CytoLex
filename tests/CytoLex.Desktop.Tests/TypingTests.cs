using CytoLex.Desktop.ViewModels;

namespace CytoLex.Desktop.Tests;

/// <summary>T-059: typing a string character by character never throws (every prefix is validated with the translation).</summary>
public sealed class TypingTests
{
    public static TheoryData<string> Strings => new(
        [
            .. MainWindowViewModel.ExampleStrings,
            "47,XY,+8,t(9;22)(q34;q11.2)",
            "mos 47,XX,+21[12]/46,XX[8]",
            "46,XX,der(1)t(1;3)(p32;q21)t(1;11)(q25;q13)",
            "46,XX,del(5)(q13q33)t(1;3)(p32;q21",
            "46,XY,t(9;22)(q34;q11.2)[10]/47,idem,+8[5]",
            "arr[GRCh38] 7q11.23(72726578_74139390)x1",
        ]);

    [Theory]
    [MemberData(nameof(Strings))]
    public void EveryPrefix_Validates(string text)
    {
        var vm = new MainWindowViewModel();
        foreach (var coordinates in new[] { false, true })
        {
            vm.ShowCoordinates = coordinates;
            for (var i = 1; i <= text.Length; i++)
            {
                var prefix = text[..i];
                var ex = Record.Exception(() => vm.Input = prefix);
                Assert.True(ex is null, $"'{prefix}' (coordinates {coordinates}): {ex}");
                for (var offset = 0; offset < prefix.Length; offset++)
                {
                    vm.HoverOffset = offset;
                }

                vm.HoverOffset = null;
            }
        }
    }
}
