using PgAiTrading.Models;

namespace PgAiTrading.Services;

public interface IChartPatternService
{
    /// <summary>
    /// Annotates each candle with at most one high-priority pattern
    /// (TradingView-style important candlestick patterns).
    /// </summary>
    void ApplyPatterns(IList<Candle> candles);

    /// <summary>
    /// True when the latest candle has a bullish pattern bias (Buy).
    /// </summary>
    bool TryGetLatestBullishPattern(IReadOnlyList<Candle> candles, out string? label);
}

/// <summary>
/// Detects high-signal candlestick patterns for quick chart reading.
/// One pattern per bar (highest priority wins) to avoid clutter.
/// </summary>
public sealed class ChartPatternService : IChartPatternService
{
    private const decimal DojiBodyRatio = 0.12m;
    private const decimal LongWickRatio = 2.0m;
    private const decimal ShortWickRatio = 0.35m;
    private const decimal MarubozuWickRatio = 0.12m;

    public void ApplyPatterns(IList<Candle> candles)
    {
        if (candles.Count == 0)
            return;

        for (var i = 0; i < candles.Count; i++)
        {
            candles[i].PatternCode = null;
            candles[i].PatternLabel = null;
            candles[i].PatternBias = null;
        }

        for (var i = 0; i < candles.Count; i++)
        {
            var match = DetectAt(candles, i);
            if (match is null)
                continue;

            candles[i].PatternCode = match.Value.Code;
            candles[i].PatternLabel = match.Value.Label;
            candles[i].PatternBias = match.Value.Bias;
        }
    }

    public bool TryGetLatestBullishPattern(IReadOnlyList<Candle> candles, out string? label)
    {
        label = null;
        if (candles.Count == 0)
            return false;

        var latest = candles[^1];
        if (!string.Equals(latest.PatternBias, ChartPatternBias.Buy, StringComparison.OrdinalIgnoreCase))
            return false;

        label = !string.IsNullOrWhiteSpace(latest.PatternLabel)
            ? latest.PatternLabel
            : latest.PatternCode;
        return true;
    }

    private static (string Code, string Label, string Bias)? DetectAt(IList<Candle> candles, int i)
    {
        var c = candles[i];
        var range = Range(c);
        if (range <= 0)
            return null;

        var body = Body(c);
        var upper = UpperWick(c);
        var lower = LowerWick(c);
        var bull = IsBull(c);
        var bear = IsBear(c);

        // 3-bar patterns (highest priority)
        if (i >= 2)
        {
            var a = candles[i - 2];
            var b = candles[i - 1];
            if (IsEveningStar(a, b, c))
                return ("ES", "Evening Star", ChartPatternBias.Sell);
            if (IsMorningStar(a, b, c))
                return ("MS", "Morning Star", ChartPatternBias.Buy);
            if (IsThreeBlackCrows(a, b, c))
                return ("TBC", "3 Black Crows", ChartPatternBias.Sell);
            if (IsThreeWhiteSoldiers(a, b, c))
                return ("TWS", "3 White Soldiers", ChartPatternBias.Buy);
        }

        // 2-bar patterns
        if (i >= 1)
        {
            var prev = candles[i - 1];
            if (IsBearishEngulfing(prev, c))
                return ("BE", "Bear Engulf", ChartPatternBias.Sell);
            if (IsBullishEngulfing(prev, c))
                return ("BU", "Bull Engulf", ChartPatternBias.Buy);
            if (IsDarkCloudCover(prev, c))
                return ("DC", "Dark Cloud", ChartPatternBias.Sell);
            if (IsPiercingLine(prev, c))
                return ("PL", "Piercing", ChartPatternBias.Buy);
            if (IsInsideBar(prev, c))
                return ("IB", "Inside Bar", ChartPatternBias.Neutral);
        }

        // 1-bar patterns
        if (IsShootingStar(c, body, upper, lower, range, bear))
            return ("SS", "Shooting Star", ChartPatternBias.Sell);
        if (IsHammer(c, body, upper, lower, range, bull))
            return ("H", "Hammer", ChartPatternBias.Buy);
        if (IsMarubozu(body, upper, lower, range, bull, bear))
            return bull
                ? ("MB", "Bull Marubozu", ChartPatternBias.Buy)
                : ("MSZ", "Bear Marubozu", ChartPatternBias.Sell);
        if (IsDoji(body, range))
            return ("D", "Doji", ChartPatternBias.Neutral);

        return null;
    }

    private static bool IsBullishEngulfing(Candle prev, Candle cur) =>
        IsBear(prev) && IsBull(cur)
        && cur.Open <= prev.Close
        && cur.Close >= prev.Open
        && Body(cur) > Body(prev) * 1.05m;

    private static bool IsBearishEngulfing(Candle prev, Candle cur) =>
        IsBull(prev) && IsBear(cur)
        && cur.Open >= prev.Close
        && cur.Close <= prev.Open
        && Body(cur) > Body(prev) * 1.05m;

    private static bool IsPiercingLine(Candle prev, Candle cur)
    {
        if (!IsBear(prev) || !IsBull(cur) || Body(prev) <= 0)
            return false;
        var mid = (prev.Open + prev.Close) / 2m;
        return cur.Open < prev.Low && cur.Close > mid && cur.Close < prev.Open;
    }

    private static bool IsDarkCloudCover(Candle prev, Candle cur)
    {
        if (!IsBull(prev) || !IsBear(cur) || Body(prev) <= 0)
            return false;
        var mid = (prev.Open + prev.Close) / 2m;
        return cur.Open > prev.High && cur.Close < mid && cur.Close > prev.Open;
    }

    private static bool IsInsideBar(Candle prev, Candle cur) =>
        cur.High <= prev.High && cur.Low >= prev.Low
        && (cur.High < prev.High || cur.Low > prev.Low);

    private static bool IsMorningStar(Candle a, Candle b, Candle c)
    {
        if (!IsBear(a) || Body(a) <= 0)
            return false;
        if (Body(b) > Body(a) * 0.45m)
            return false;
        if (!IsBull(c) || Body(c) <= Body(a) * 0.5m)
            return false;
        var aMid = (a.Open + a.Close) / 2m;
        return c.Close >= aMid;
    }

    private static bool IsEveningStar(Candle a, Candle b, Candle c)
    {
        if (!IsBull(a) || Body(a) <= 0)
            return false;
        if (Body(b) > Body(a) * 0.45m)
            return false;
        if (!IsBear(c) || Body(c) <= Body(a) * 0.5m)
            return false;
        var aMid = (a.Open + a.Close) / 2m;
        return c.Close <= aMid;
    }

    private static bool IsThreeWhiteSoldiers(Candle a, Candle b, Candle c) =>
        IsBull(a) && IsBull(b) && IsBull(c)
        && b.Close > a.Close && c.Close > b.Close
        && b.Open > a.Open && b.Open < a.Close
        && c.Open > b.Open && c.Open < b.Close
        && UpperWick(a) <= Body(a) * 0.4m
        && UpperWick(b) <= Body(b) * 0.4m
        && UpperWick(c) <= Body(c) * 0.4m;

    private static bool IsThreeBlackCrows(Candle a, Candle b, Candle c) =>
        IsBear(a) && IsBear(b) && IsBear(c)
        && b.Close < a.Close && c.Close < b.Close
        && b.Open < a.Open && b.Open > a.Close
        && c.Open < b.Open && c.Open > b.Close
        && LowerWick(a) <= Body(a) * 0.4m
        && LowerWick(b) <= Body(b) * 0.4m
        && LowerWick(c) <= Body(c) * 0.4m;

    private static bool IsHammer(Candle c, decimal body, decimal upper, decimal lower, decimal range, bool bull) =>
        lower >= body * LongWickRatio
        && upper <= Math.Max(body, range * 0.12m) * ShortWickRatio * 2
        && body <= range * 0.4m
        && (bull || body <= range * 0.25m);

    private static bool IsShootingStar(Candle c, decimal body, decimal upper, decimal lower, decimal range, bool bear) =>
        upper >= body * LongWickRatio
        && lower <= Math.Max(body, range * 0.12m) * ShortWickRatio * 2
        && body <= range * 0.4m
        && (bear || body <= range * 0.25m);

    private static bool IsDoji(decimal body, decimal range) =>
        range > 0 && body / range <= DojiBodyRatio;

    private static bool IsMarubozu(decimal body, decimal upper, decimal lower, decimal range, bool bull, bool bear) =>
        (bull || bear)
        && body >= range * 0.82m
        && upper <= range * MarubozuWickRatio
        && lower <= range * MarubozuWickRatio;

    private static bool IsBull(Candle c) => c.Close > c.Open;
    private static bool IsBear(Candle c) => c.Close < c.Open;
    private static decimal Body(Candle c) => Math.Abs(c.Close - c.Open);
    private static decimal Range(Candle c) => Math.Max(0m, c.High - c.Low);
    private static decimal UpperWick(Candle c) => c.High - Math.Max(c.Open, c.Close);
    private static decimal LowerWick(Candle c) => Math.Min(c.Open, c.Close) - c.Low;
}
