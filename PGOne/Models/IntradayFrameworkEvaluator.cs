namespace PGOne.Models;

public static class IntradayFrameworkEvaluator
{
    public static IReadOnlyList<string> Conditions { get; } =
    [
        "Step 1 — 1H SuperTrend (10,3) + current-day VWAP aligned",
        "Step 2 — 15M SuperTrend (10,3) + ADX(15m) ≥ 20 + RSI(15m) confirmation",
        "TPO — Buy above POC+VAH; Sell below POC+VAL; ADX<18 inside VA = rotation",
        "Step 3 — 5M SuperTrend (7,2.5) entry trigger",
        "Step 4 — Footprint: Delta + imbalances, no opposing absorption",
        "Step 5 — Targets: Prev POC / VAH / VAL; stop: 5M ST reversal",
        "No new entry when 5m RSI < 30",
        "MIS quantity sized to ~₹5,000 notional per stock"
    ];

    public static bool IsSatisfied(MultiTimeframeAnalysis analysis) =>
        analysis.FrameworkReady;

    public static string GetStatus(MultiTimeframeAnalysis analysis, bool satisfied)
    {
        if (satisfied)
            return analysis.TradeDirection == TrendDirection.Sell ? "Short" : "Long";

        if (analysis.WaitForReversal)
            return "Reversal risk";

        if (analysis.IsRotationRegime)
            return "Rotation inside VA";

        if (analysis.IsRangebound)
            return "Range-bound";

        if (analysis.MarketBias == TrendDirection.Neutral)
            return "No market bias";

        if (analysis.TradeDirection == TrendDirection.Neutral)
            return analysis.FrameworkStatus;

        if (!analysis.TpoConfirmed)
            return "Await TPO";

        if (!analysis.EntryTriggered)
            return "Await entry ST";

        if (!analysis.FootprintConfirmed)
            return "Await footprint";

        return analysis.FrameworkStatus;
    }

    public static int QuantityForNotional(decimal lastPrice, decimal notional = 5000m)
    {
        if (lastPrice <= 0)
            return 0;

        return Math.Max(1, (int)Math.Floor(notional / lastPrice));
    }
}
