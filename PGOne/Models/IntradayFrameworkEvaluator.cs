namespace PGOne.Models;

public static class IntradayFrameworkEvaluator
{
    public static IReadOnlyList<string> Conditions { get; } =
    [
        "1H RSI(28) bias bullish (> 55)",
        "No reversal guard — RSI < 30 on any timeframe",
        "ADX(14) on 1H not weak (≥ 18)",
        "Price above 5m VWAP",
        "1H SuperTrend bullish + 5m or 15m SuperTrend aligned",
        "MIS quantity sized to ~₹5,000 notional per stock"
    ];

    public static bool IsSatisfied(MultiTimeframeAnalysis analysis)
    {
        if (analysis.WaitForReversal)
            return false;

        if (analysis.RsiBias != TrendDirection.Buy)
            return false;

        if (analysis.Strength1H == TrendStrength.Weak)
            return false;

        if (!analysis.AboveVwap)
            return false;

        return analysis.Trend1H == TrendDirection.Buy
            && (analysis.Trend5M == TrendDirection.Buy || analysis.Trend15M == TrendDirection.Buy);
    }

    public static string GetStatus(MultiTimeframeAnalysis analysis, bool satisfied)
    {
        if (satisfied)
            return "Up";

        if (analysis.WaitForReversal)
            return "Reversal risk";

        if (analysis.RsiBias == TrendDirection.Sell)
            return "Bearish";

        if (analysis.IsRangebound)
            return "Range-bound";

        if (analysis.Strength1H == TrendStrength.Weak)
            return "Weak trend";

        if (!analysis.AboveVwap)
            return "Below VWAP";

        return "Wait for alignment";
    }

    public static int QuantityForNotional(decimal lastPrice, decimal notional = 5000m)
    {
        if (lastPrice <= 0)
            return 0;

        return Math.Max(1, (int)Math.Floor(notional / lastPrice));
    }
}
