namespace PGOne.Models;

/// <summary>
/// Copies exchange volume from a liquid proxy series (e.g. index futures) onto index candles.
/// </summary>
public static class CandleVolumeMerger
{
    public static List<Candle> CopyWithVolumeFrom(List<Candle> priceCandles, IReadOnlyList<Candle> volumeCandles)
    {
        if (priceCandles.Count == 0)
            return priceCandles;

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

    private static List<Candle> CloneCandles(IReadOnlyList<Candle> candles) =>
        candles.Select(c => new Candle
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
            Vwap = c.Vwap
        }).ToList();

    private static DateTime AlignBar(DateTime timestamp) =>
        new(timestamp.Year, timestamp.Month, timestamp.Day, timestamp.Hour, timestamp.Minute, 0);
}
