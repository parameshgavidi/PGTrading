namespace PGOne.Models;

/// <summary>SuperTrend ST(7, 2.5) flip detection on completed candles.</summary>
public static class SuperTrendFlipHelper
{
    public static TrendDirection GetTrendAtBarClose(
        IReadOnlyList<Candle> candles,
        int closedBarIndex,
        int period,
        double multiplier,
        Func<List<Candle>, int, double, TrendDirection> getTrend)
    {
        if (closedBarIndex < 0 || closedBarIndex >= candles.Count)
            return TrendDirection.Neutral;

        var subset = candles.Take(closedBarIndex + 1).ToList();
        return getTrend(subset, period, multiplier);
    }

    /// <summary>
    /// Bullish flip on the last completed candle (excludes the live forming bar).
    /// Long entry only: previous bar downtrend (Sell), last bar uptrend (Buy).
    /// Bearish (Buy→Sell) flips are ignored — no sell automation.
    /// </summary>
    public static bool DetectBullishFlipOnLastClosedBar(
        IReadOnlyList<Candle> candles,
        int period,
        double multiplier,
        Func<List<Candle>, int, double, TrendDirection> getTrend)
    {
        if (candles.Count < period + 4)
            return false;

        var lastClosedIndex = candles.Count - 2;
        var prevClosedIndex = candles.Count - 3;
        if (prevClosedIndex < 0)
            return false;

        var prevTrend = GetTrendAtBarClose(candles, prevClosedIndex, period, multiplier, getTrend);
        var lastTrend = GetTrendAtBarClose(candles, lastClosedIndex, period, multiplier, getTrend);

        return prevTrend == TrendDirection.Sell && lastTrend == TrendDirection.Buy;
    }

    public static DateTime? GetLastClosedBarTime(IReadOnlyList<Candle> candles) =>
        candles.Count >= 2 ? candles[^2].Timestamp : null;
}
