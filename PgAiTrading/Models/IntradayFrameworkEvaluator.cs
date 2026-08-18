namespace PgAiTrading.Models;

public static class IntradayFrameworkEvaluator
{
    public static IReadOnlyList<string> Conditions { get; } =
    [
        "1H market structure (HH/HL or LH/LL) — major direction",
        "RSI(28) + ADX(1H) — regime (trend / strong chop / developing)",
        "15M structure BOS aligned with 1H direction (setup)",
        "Session Volume Profile — POC / VAH / VAL / PDH / PDL location",
        "Liquidity sweep → reclaim → 5M BOS (esp. strong chop)",
        "Footprint: delta + imbalances + absorption on sweep/entry",
        "5M BOS entry (ST 7,2.5 remains trailing stop)",
        "5m RSI < 30 = EXPECT REVERSAL; RSI < 30 + bullish 5m pattern = WAIT",
        "MIS quantity sized to ~₹5,000 notional per stock",
        "Chart-only (not gates): Camarilla, CPR, TPO display"
    ];

    public static bool IsSatisfied(MultiTimeframeAnalysis analysis) =>
        analysis.FrameworkReady;

    public static string GetStatus(MultiTimeframeAnalysis analysis, bool satisfied)
    {
        if (satisfied)
            return analysis.TradeDirection == TrendDirection.Sell ? "Short" : "Long";

        if (analysis.WaitForReversal)
            return "Reversal wait";

        if (analysis.ExpectReversal)
            return "Expect reversal";

        if (analysis.Regime == MarketRegime.StrongChop)
            return analysis.LiquiditySweep.IsConfirmedSetup ? "Sweep setup" : "Strong chop";

        if (analysis.Regime == MarketRegime.DevelopingTrend)
            return "Developing — await 15M BOS";

        if (analysis.Regime == MarketRegime.SoftNeutral)
            return "Soft neutral";

        if (analysis.IsRotationRegime)
            return "Rotation inside VA";

        if (analysis.MarketBias == TrendDirection.Neutral)
            return "No market structure bias";

        if (analysis.TradeDirection == TrendDirection.Neutral)
            return analysis.FrameworkStatus;

        if (!analysis.TpoConfirmed)
            return "Await volume profile";

        if (!analysis.EntryTriggered)
            return "Await 5M BOS";

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
