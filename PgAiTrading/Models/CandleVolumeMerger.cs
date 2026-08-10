namespace PgAiTrading.Models;

/// <summary>
/// Aligns index futures candles with index bar times for footprint order-flow analysis.
/// </summary>
public static class CandleVolumeMerger
{
    /// <summary>
    /// Returns futures 5m bars at the same timestamps as the index series (full OHLCV from futures).
    /// </summary>
    public static List<Candle> SelectFuturesBarsMatchingIndex(
        IReadOnlyList<Candle> indexCandles,
        IReadOnlyList<Candle> futureCandles)
    {
        if (indexCandles.Count == 0 || futureCandles.Count == 0)
            return [];

        var futureByBar = new Dictionary<DateTime, Candle>();
        foreach (var candle in futureCandles)
        {
            var key = AlignBar(candle.Timestamp);
            futureByBar[key] = candle;
        }

        var matched = new List<Candle>();
        foreach (var indexCandle in indexCandles)
        {
            var key = AlignBar(indexCandle.Timestamp);
            if (futureByBar.TryGetValue(key, out var future))
                matched.Add(CloneCandle(future));
        }

        return matched;
    }

    /// <summary>
    /// Copies exchange volume from a liquid proxy series onto index candles.
    /// </summary>
    public static List<Candle> CopyWithVolumeFrom(IReadOnlyList<Candle> priceCandles, IReadOnlyList<Candle> volumeCandles)
    {
        if (priceCandles.Count == 0)
            return [];

        if (volumeCandles.Count == 0)
            return CloneCandles(priceCandles);

        var volumeByBar = new Dictionary<DateTime, long>();
        foreach (var candle in volumeCandles)
        {
            if (candle.Volume <= 0)
                continue;

            volumeByBar[AlignBar(candle.Timestamp)] = candle.Volume;
        }

        var merged = CloneCandles(priceCandles);
        foreach (var candle in merged)
        {
            var key = AlignBar(candle.Timestamp);
            if (volumeByBar.TryGetValue(key, out var volume))
                candle.Volume = volume;
        }

        return merged;
    }

    public static bool HasTradeableVolume(IReadOnlyList<Candle> candles) =>
        candles.Any(c => c.Volume > 0);

    private static Candle CloneCandle(Candle c) => new()
    {
        Timestamp = c.Timestamp,
        Open = c.Open,
        High = c.High,
        Low = c.Low,
        Close = c.Close,
        Volume = c.Volume,
        SuperTrend = c.SuperTrend,
        KeltnerMid = c.KeltnerMid,
        KeltnerUpperInner = c.KeltnerUpperInner,
        KeltnerLowerInner = c.KeltnerLowerInner,
        KeltnerUpperOuter = c.KeltnerUpperOuter,
        KeltnerLowerOuter = c.KeltnerLowerOuter,
        Vwap = c.Vwap,
        Ema9 = c.Ema9,
        Ema20 = c.Ema20,
        Ema50 = c.Ema50,
        Ema200 = c.Ema200,
        PatternCode = c.PatternCode,
        PatternLabel = c.PatternLabel,
        PatternBias = c.PatternBias
    };

    private static List<Candle> CloneCandles(IReadOnlyList<Candle> candles) =>
        candles.Select(CloneCandle).ToList();

    private static DateTime AlignBar(DateTime timestamp) =>
        new(timestamp.Year, timestamp.Month, timestamp.Day, timestamp.Hour, timestamp.Minute, 0);
}
