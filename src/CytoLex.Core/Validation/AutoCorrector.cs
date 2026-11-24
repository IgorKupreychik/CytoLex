using System.Collections.Immutable;
using CytoLex.Core.Kb;
using CytoLex.Core.Text;

namespace CytoLex.Core.Validation;

/// <summary>
/// Autocorrection of one edition (T-096): applies the level-1 fixes of the rules the edition's KB flags with
/// <c>fix.auto</c>, re-validates, and repeats until nothing flagged is left to fix. Only fixes that don't depend on the
/// sample context are applied (<see cref="Fixer.ApplyAll"/> with no context).
/// </summary>
internal static class AutoCorrector
{
    /// <summary>The most rounds; overlapping fixes need a further round each, real strings settle in one or two.</summary>
    internal const int MaxRounds = 8;

    /// <summary>
    /// Autocorrects <paramref name="input"/>, whose first-round result is <paramref name="first"/>; <paramref name="run"/>
    /// validates a corrected string in the same edition. Returns the final round's result with
    /// <see cref="EditionResult.AutoCorrection"/> set.
    /// </summary>
    public static EditionResult Correct(IEditionKb kb, string input, EditionResult first, Func<string, EditionResult> run)
    {
        var text = input;
        var result = first;
        var map = OffsetMap.Identity(input.Length);
        var corrections = ImmutableArray.CreateBuilder<AppliedCorrection>();
        bool IsAuto(Diagnostic d) => kb.Rule(d.RuleId)?.Fix?.Auto == true;

        for (var round = 1; round <= MaxRounds; round++)
        {
            var all = Fixer.ApplyAll(text, result, context: null, include: IsAuto);
            if (all.Applied.IsEmpty || all.Text == text)
            {
                break;
            }

            foreach (var fix in all.Applied)
            {
                var ruleId = result.Diagnostics.First(d => d.Fixes.Any(f => ReferenceEquals(f, fix))).RuleId;
                foreach (var edit in fix.Edits.OrderBy(e => e.Span.Start))
                {
                    var original = map.ToOriginal(edit.Span);
                    corrections.Add(new AppliedCorrection(ruleId, original, original.Of(input), edit.NewText, fix.Description, fix.Citations, round));
                }
            }

            map = map.Apply(all.Applied.SelectMany(f => f.Edits).Select(e => (e.Span, e.NewText)));
            text = all.Text;
            result = run(text);
        }

        return result with { AutoCorrection = new AutoCorrection(text, corrections.ToImmutable(), map) };
    }
}
