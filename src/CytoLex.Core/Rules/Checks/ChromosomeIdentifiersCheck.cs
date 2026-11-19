namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// <c>CHROM.IDENTIFIERS</c> (§2.2.1): a chromosome is 1–22, X or Y. The parser reports wrong letters in the sex chromosome
/// field (<c>45,Z</c>); this check reports numbers outside 1–22 anywhere else (<c>+23</c>, <c>t(23;1)</c>, <c>23q11</c>).
/// </summary>
[KbRule("CHROM.IDENTIFIERS")]
internal sealed class ChromosomeIdentifiersCheck : IRuleCheck
{
    public void Check(RuleContext context)
    {
        foreach (var chromosome in context.Model.Chromosomes().Where(c => !c.IsPartial && !c.IsValidIdentifier()))
        {
            context.Report(chromosome.Span, $"There is no chromosome {chromosome.Name}; chromosomes are 1–22, X and Y.");
        }
    }
}
