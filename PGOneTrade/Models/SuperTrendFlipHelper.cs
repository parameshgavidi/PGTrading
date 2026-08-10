namespace PGOneTrade.Models;

/// <summary>SuperTrend ST(7, 2.5) flip detection on completed candles.</summary>
public static class SuperTrendFlipHelper
{
    public static TimeSpan GetBarDuration(string timeframe) => timeframe switch
    {
        "1m" => TimeSpan.FromMinutes(1),
        "5m" => TimeSpan.FromMinutes(5),
        "15m" => TimeSpan.FromMinutes(15),
        "1H" => TimeSpan.FromHours(1),
        "1D" => TimeSpan.FromDays(1),
        _ => TimeSpan.FromMinutes(5)
    };

    /// <summary>
    /// Index of the last fully closed candle. When the final candle is still forming
    /// (market open and bar end &gt; now), excludes it. Outside market hours the last
    /// candle from Zerodha is already closed — do not skip it, or overnight flips are missed.
    /// </summary>
    public static int GetLastClosedBarIndex(
        IReadOnlyList<Candle> candles,
        string timeframe,
        DateTime? istNow = null)
    {
        if (candles.Count == 0)
            return -1;

        var lastIndex = candles.Count - 1;
        if (IsFormingBar(candles[lastIndex], timeframe, istNow))
            return lastIndex - 1;

        return lastIndex;
    }

    public static bool IsFormingBar(Candle candle, string timeframe, DateTime? istNow = null)
    {
        var now = istNow ?? GetIstNow();
        if (!IsMarketOpen(now))
            return false;

        var barEnd = candle.Timestamp + GetBarDuration(timeframe);
        return now < barEnd;
    }

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
    /// Bullish flip on the last completed candle.
    /// Long entry only: previous closed bar Sell, last closed bar Buy.
    /// </summary>
    public static bool DetectBullishFlipOnLastClosedBar(
        IReadOnlyList<Candle> candles,
        int period,
        double multiplier,
        Func<List<Candle>, int, double, TrendDirection> getTrend,
        string timeframe = "5m",
        DateTime? istNow = null)
    {
        if (candles.Count < period + 3)
            return false;

        var lastClosedIndex = GetLastClosedBarIndex(candles, timeframe, istNow);
        var prevClosedIndex = lastClosedIndex - 1;
        if (prevClosedIndex < 0)
            return false;

        var prevTrend = GetTrendAtBarClose(candles, prevClosedIndex, period, multiplier, getTrend);
        var lastTrend = GetTrendAtBarClose(candles, lastClosedIndex, period, multiplier, getTrend);

        return prevTrend == TrendDirection.Sell && lastTrend == TrendDirection.Buy;
    }

    public static bool DetectBearishFlipOnLastClosedBar(
        IReadOnlyList<Candle> candles,
        int period,
        double multiplier,
        Func<List<Candle>, int, double, TrendDirection> getTrend,
        string timeframe = "5m",
        DateTime? istNow = null)
    {
        if (candles.Count < period + 3)
            return false;

        var lastClosedIndex = GetLastClosedBarIndex(candles, timeframe, istNow);
        var prevClosedIndex = lastClosedIndex - 1;
        if (prevClosedIndex < 0)
            return false;

        var prevTrend = GetTrendAtBarClose(candles, prevClosedIndex, period, multiplier, getTrend);
        var lastTrend = GetTrendAtBarClose(candles, lastClosedIndex, period, multiplier, getTrend);

        return prevTrend == TrendDirection.Buy && lastTrend == TrendDirection.Sell;
    }

    public static bool IsBuyTriggerOnLastClosedBar(
        IReadOnlyList<Candle> candles,
        int period,
        double multiplier,
        Func<List<Candle>, int, double, TrendDirection> getTrend,
        string timeframe = "5m",
        DateTime? istNow = null) =>
        DetectBullishFlipOnLastClosedBar(candles, period, multiplier, getTrend, timeframe, istNow);

    public static TrendDirection GetTrendOnLastClosedBar(
        IReadOnlyList<Candle> candles,
        int period,
        double multiplier,
        Func<List<Candle>, int, double, TrendDirection> getTrend,
        string timeframe = "5m",
        DateTime? istNow = null)
    {
        var lastClosedIndex = GetLastClosedBarIndex(candles, timeframe, istNow);
        if (lastClosedIndex < 0)
            return TrendDirection.Neutral;

        return GetTrendAtBarClose(candles, lastClosedIndex, period, multiplier, getTrend);
    }

    public static DateTime? GetLastClosedBarTime(
        IReadOnlyList<Candle> candles,
        string timeframe = "5m",
        DateTime? istNow = null)
    {
        var lastClosedIndex = GetLastClosedBarIndex(candles, timeframe, istNow);
        return lastClosedIndex >= 0 ? candles[lastClosedIndex].Timestamp : null;
    }

    public static decimal GetLastClosedBarClose(
        IReadOnlyList<Candle> candles,
        string timeframe = "5m",
        DateTime? istNow = null)
    {
        var lastClosedIndex = GetLastClosedBarIndex(candles, timeframe, istNow);
        return lastClosedIndex >= 0 ? candles[lastClosedIndex].Close : 0m;
    }

    private static DateTime GetIstNow()
    {
        try
        {
            return TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"));
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"));
        }
    }

    private static bool IsMarketOpen(DateTime istNow)
    {
        if (istNow.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            return false;

        var tod = istNow.TimeOfDay;
        return tod >= new TimeSpan(9, 15, 0) && tod <= new TimeSpan(15, 30, 0);
    }
}
