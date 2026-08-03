using PGOne.Models;
using PGOne.Services;
using Xunit;

namespace PGOne.Framework.Tests;

public class ChartOverlayIndicatorTests
{
    [Fact]
    public void Five_minute_indicators_populate_super_trend_ema_and_vwap()
    {
        var candles = BuildTrendingCandles(150);
        var superTrend = new SuperTrendService();
        var indicators = new IndicatorService();

        var (_, st103) = superTrend.Calculate(candles, 10, 3.0);
        Assert.NotEmpty(st103);

        var startIndex = candles.Count - st103.Count;
        for (var i = 0; i < st103.Count; i++)
            candles[startIndex + i].SuperTrend = st103[i];

        var (_, st725) = superTrend.Calculate(
            candles,
            TrailingStopDefaults.Period,
            TrailingStopDefaults.Multiplier);
        Assert.NotEmpty(st725);

        startIndex = candles.Count - st725.Count;
        for (var i = 0; i < st725.Count; i++)
            candles[startIndex + i].SuperTrendEntry = st725[i];

        indicators.ApplyVwap(candles);
        indicators.ApplyEma20(candles, 20);

        var visible = candles.TakeLast(50).ToList();
        Assert.All(visible, c => Assert.NotNull(c.SuperTrend));
        Assert.All(visible, c => Assert.NotNull(c.SuperTrendEntry));
        Assert.All(visible, c => Assert.NotNull(c.Vwap));
        Assert.All(visible.Skip(19), c => Assert.NotNull(c.Ema20));
    }

    private static List<Candle> BuildTrendingCandles(int count)
    {
        var candles = new List<Candle>();
        var price = 24000m;
        var time = new DateTime(2026, 7, 30, 9, 15, 0);

        for (var i = 0; i < count; i++)
        {
            var open = price;
            var close = price + (i % 5 == 0 ? -8m : 6m);
            var high = Math.Max(open, close) + 4m;
            var low = Math.Min(open, close) - 4m;
            candles.Add(new Candle
            {
                Timestamp = time.AddMinutes(i * 5),
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = 10000 + i * 50
            });
            price = close;
        }

        return candles;
    }
}
