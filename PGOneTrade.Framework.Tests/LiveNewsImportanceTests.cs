using PGOneTrade.Services;
using Xunit;

namespace PGOneTrade.Framework.Tests;

public class LiveNewsImportanceTests
{
    [Fact]
    public void Scores_higher_when_more_symbols_and_market_keywords()
    {
        var weak = LiveNewsImportance.Score("Company updates website theme", "Google News", []);
        var strong = LiveNewsImportance.Score(
            "Nifty surges as Reliance and HDFC Bank rally after RBI decision",
            "MoneyControl",
            ["RELIANCE", "HDFCBANK"]);

        Assert.True(strong > weak);
    }

    [Fact]
    public void NormalizeTitleKey_dedupes_whitespace_and_case()
    {
        var a = LiveNewsImportance.NormalizeTitleKey("  Nifty  Surges  ");
        var b = LiveNewsImportance.NormalizeTitleKey("nifty surges");
        Assert.Equal(a, b);
    }
}
