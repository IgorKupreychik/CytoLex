namespace CytoLex.Core.Tests.KbExamples;

/// <summary>
/// The validator the KB-example harness runs against. <c>null</c> until the rule engine exists:
/// T-028 returns an <see cref="IExampleValidator"/> that calls <c>Validator.Validate</c> for one edition
/// and maps its result onto <see cref="ExampleRun"/>; T-035 sets <see cref="IExampleValidator.SupportsFixes"/>.
/// While it is <c>null</c>, every KB-example case is skipped with "no validator yet (T-028)".
/// </summary>
internal static class ValidatorAdapter
{
    public static IExampleValidator? Current => null;
}
