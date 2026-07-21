namespace PGOne.Models;

public sealed record AiCheck(string Label, string State);

public static class AiInsightHelper
{
    public static IReadOnlyList<AiCheck> BuildChecks(MultiTimeframeAnalysis analysis)
    {
        var trendStrong = analysis.Strength1H == TrendStrength.Strong;
        var adxRising = analysis.Adx >= 25;
        var rsiHealthy = analysis.Rsi >= 45 && analysis.Rsi <= 70;
        var superTrendBullish = analysis.Trend5M == TrendDirection.Buy;

        return
        [
            new($"Trend {(trendStrong ? "Strong" : analysis.Strength)}", trendStrong ? "pass" : analysis.Strength1H == TrendStrength.Moderate ? "warn" : "fail"),
            new(adxRising ? "ADX Rising" : $"ADX {analysis.Adx:N0}", adxRising ? "pass" : analysis.Adx >= 18 ? "warn" : "fail"),
            new($"RSI {analysis.Rsi:N0}", rsiHealthy ? "pass" : analysis.Rsi > 70 || analysis.Rsi < 30 ? "fail" : "warn"),
            new($"SuperTrend {TrendUi.GetSuperTrendLabel(analysis.Trend5M)}", superTrendBullish ? "pass" : analysis.Trend5M == TrendDirection.Sell ? "fail" : "warn")
        ];
    }

    public static string GetSuggestedAction(Signal signal, MultiTimeframeAnalysis analysis)
    {
        if (analysis.WaitForReversal)
            return "WAIT";

        return signal.Trend switch
        {
            TrendDirection.Buy when analysis.AboveVwap => "BUY ON DIP",
            TrendDirection.Buy => "BUY ON PULLBACK",
            TrendDirection.Sell when !analysis.AboveVwap => "SELL ON RALLY",
            TrendDirection.Sell => "SELL ON BOUNCE",
            _ when analysis.IsRangebound => "RANGE TRADE",
            _ => "HOLD / NEUTRAL"
        };
    }
}
