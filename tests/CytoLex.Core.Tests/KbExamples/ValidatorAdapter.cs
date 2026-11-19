using CytoLex.Core.Validation;
using CoreContext = CytoLex.Core.Context.SampleContext;
using CoreSeverity = CytoLex.Core.Validation.Severity;

namespace CytoLex.Core.Tests.KbExamples;

/// <summary>
/// The validator the KB-example harness runs against (T-028): <see cref="Validator.Default"/> for one edition, its
/// <see cref="EditionResult"/> mapped onto <see cref="ExampleRun"/>. T-035 sets <see cref="IExampleValidator.SupportsFixes"/>.
/// </summary>
internal static class ValidatorAdapter
{
    private static readonly Lazy<IExampleValidator> _current = new(() => new CoreValidatorAdapter(Validator.Default));

    public static IExampleValidator? Current => _current.Value;

    private sealed class CoreValidatorAdapter(Validator validator) : IExampleValidator
    {
        public bool SupportsFixes => false;

        public ExampleRun Run(string edition, string text, SampleContext? hint, bool isFragment)
        {
            var report = validator.Validate(text, new ValidationOptions
            {
                Editions = EditionSelection.One(edition),
                ContextHint = hint switch
                {
                    SampleContext.Constitutional => CoreContext.Constitutional,
                    SampleContext.Acquired => CoreContext.Acquired,
                    _ => null,
                },
                IsFragment = isFragment,
            });

            var result = report.Results.Single();
            return new ExampleRun(
                result.Validity.IsValid,
                [.. result.Validity.ValidContexts.Select(Map)],
                [.. result.Diagnostics.Select(d => new ExampleDiagnostic(
                    d.RuleId,
                    Map(d.Severity),
                    d.IsConditional,
                    d.Certainty == Certainty.Uncertain,
                    [.. d.Fixes.Select(f => f.Preview).OfType<string>()],
                    d.CannotVerify))],
                report.Context.Inferred is { } inferred ? Map(inferred) : null);
        }

        private static SampleContext Map(CoreContext c) => c switch
        {
            CoreContext.Constitutional => SampleContext.Constitutional,
            _ => SampleContext.Acquired,
        };

        private static Severity Map(CoreSeverity s) => s switch
        {
            CoreSeverity.Error => Severity.Error,
            CoreSeverity.Warning => Severity.Warning,
            _ => Severity.Info,
        };
    }
}
