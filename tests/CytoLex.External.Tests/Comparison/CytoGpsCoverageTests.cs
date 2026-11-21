using CytoLex.External.Comparison;

namespace CytoLex.External.Tests.Comparison;

public sealed class CytoGpsCoverageTests
{
    [Theory]
    [InlineData("arr[GRCh38] 9p24.3(1310386_1709409)×1 mat", "arr segment")]
    [InlineData("46,XX,del(20)(q11.2q13.3)[1]/46,XX[19].nuc ish(D20S108×1)[40/200]", "nuc ish segment")]
    [InlineData("47,XY,+mar.ish r(1)(p13.1q11?)(D1Z1+)", "ish segment")]
    [InlineData("46,XX,add(19)(?::p13.3→qter)", "detailed system")]
    [InlineData("46,XX,add(19)(?::p13.3->qter)", "detailed system")]
    [InlineData("46,XX,rec(2)dup(2p)inv(2)(p21q31)mat", "symbol rec")]
    [InlineData("46,X,fra(X)(q27.3)", "symbol fra")]
    [InlineData("46,XY,upd(15)mat", "symbol upd")]
    [InlineData("46,XY[3]//46,XX[17]", "chimera separator //")]
    [InlineData("45~48,XX,del(3)(p12)[2],–5[4],+8[2],+11[3][cp7]", "per-abnormality cell count")]
    public void Unsupported_NamesTheConstruct(string input, string reason) =>
        Assert.Equal(reason, CytoGpsCoverage.Instance.Unsupported(input));

    [Theory]
    [InlineData("46,XY")]
    [InlineData("46,XY,t(9;22)(q34;q11.2)")]
    [InlineData("46,XX,del(6)(q13q23)×2")]
    [InlineData("45,XY,–7[5]/46,idem,+8[6]")]
    [InlineData("47~51,XY,t(11;22)(q24;q12),+1~5mar[cp10]")]
    [InlineData("46,xy")]
    [InlineData("47,XY, +21")]
    public void Supported_ReturnsNull(string input) => Assert.Null(CytoGpsCoverage.Instance.Unsupported(input));

    [Theory]
    [InlineData("KARYO.ABNORMALITY_ORDER", true)]
    [InlineData("CLONE.MIN_CELLS", true)]
    [InlineData("SYM.LETTER_CASE", true)]
    [InlineData("SYM.LISTED_ONLY", false)]
    [InlineData("BAND.MUST_EXIST", false)]
    [InlineData("KARYO.ABNORMALITY_ORDER_X", false)]
    public void DoesNotCheck_MatchesIdsAndFamilies(string ruleId, bool expected) =>
        Assert.Equal(expected, CytoGpsCoverage.Instance.DoesNotCheck(ruleId));
}
