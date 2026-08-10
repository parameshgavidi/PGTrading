using PGOneTrade.Models;
using PGOneTrade.Services;
using Xunit;

namespace PGOneTrade.Framework.Tests;

public class SuperTrendFlipHelperTests
{
    private readonly SuperTrendService _st = new();

    [Fact]
    public void GetLastClosedBarIndex_uses_last_candle_when_market_closed()
    {
        var candles = BuildRisingThenFlip(20);
        var afterHours = new DateTime(2026, 8, 5, 8, 47, 0); // before open

        var index = SuperTrendFlipHelper.GetLastClosedBarIndex(candles, "5m", afterHours);

        Assert.Equal(candles.Count - 1, index);
    }

    [Fact]
    public void GetLastClosedBarIndex_skips_forming_candle_during_market_hours()
    {
        var start = new DateTime(2026, 8, 5, 10, 0, 0);
        var candles = new List<Candle>();
        for (var i = 0; i < 20; i++)
        {
            var t = start.AddMinutes(i * 5);
            candles.Add(new Candle
            {
                Timestamp = t,
                Open = 100 + i,
                High = 101 + i,
                Low = 99 + i,
                Close = 100.5m + i
            });
        }

        // Last bar starts 10:95 → 11:35; now is 11:37 so still forming until 11:40
        var duringBar = new DateTime(2026, 8, 5, 11, 37, 0);
        var index = SuperTrendFlipHelper.GetLastClosedBarIndex(candles, "5m", duringBar);

        Assert.Equal(candles.Count - 2, index);
    }

    [Fact]
    public void IsBuyTrigger_detects_sell_to_buy_on_last_closed_bar_after_hours()
    {
        var candles = BuildForcedSellThenBuyFlip();
        var afterHours = new DateTime(2026, 8, 5, 16, 0, 0);

        var trigger = SuperTrendFlipHelper.IsBuyTriggerOnLastClosedBar(
            candles,
            TrailingStopDefaults.Period,
            TrailingStopDefaults.Multiplier,
            _st.GetTrend,
            "5m",
            afterHours);

        Assert.True(trigger);
    }

    [Fact]
    public void IsBuyTrigger_false_when_flip_is_not_on_last_closed_bar()
    {
        var candles = BuildForcedSellThenBuyFlip();

        for (var i = 1; i <= 3; i++)
        {
            var last = candles[^1];
            candles.Add(new Candle
            {
                Timestamp = last.Timestamp.AddMinutes(5),
                Open = last.Close,
                High = last.Close + 2,
                Low = last.Close - 0.5m,
                Close = last.Close + 1
            });
        }

        var afterHours = new DateTime(2026, 8, 4, 16, 0, 0);
        var trigger = SuperTrendFlipHelper.IsBuyTriggerOnLastClosedBar(
            candles,
            TrailingStopDefaults.Period,
            TrailingStopDefaults.Multiplier,
            _st.GetTrend,
            "5m",
            afterHours);

        Assert.False(trigger);
    }

    [Fact]
    public void IsBuyTrigger_true_only_on_exact_cross_bar()
    {
        var candles = BuildForcedSellThenBuyFlip();
        var afterHours = new DateTime(2026, 8, 4, 16, 0, 0);

        Assert.True(SuperTrendFlipHelper.IsBuyTriggerOnLastClosedBar(
            candles,
            TrailingStopDefaults.Period,
            TrailingStopDefaults.Multiplier,
            _st.GetTrend,
            "5m",
            afterHours));
    }

    private static List<Candle> BuildRisingThenFlip(int count)
    {
        var start = new DateTime(2026, 8, 4, 10, 0, 0);
        var candles = new List<Candle>();
        for (var i = 0; i < count; i++)
        {
            candles.Add(new Candle
            {
                Timestamp = start.AddMinutes(i * 5),
                Open = 100 + i,
                High = 102 + i,
                Low = 99 + i,
                Close = 101 + i
            });
        }

        return candles;
    }

    /// <summary>Build a series that ends with a clear Sell→Buy SuperTrend flip on the last bar.</summary>
    private List<Candle> BuildForcedSellThenBuyFlip()
    {
        var start = new DateTime(2026, 8, 4, 10, 0, 0);
        var candles = new List<Candle>();

        // Strong downtrend first
        decimal price = 200m;
        for (var i = 0; i < 30; i++)
        {
            price -= 2m;
            candles.Add(new Candle
            {
                Timestamp = start.AddMinutes(i * 5),
                Open = price + 1,
                High = price + 1.5m,
                Low = price - 1,
                Close = price
            });
        }

        // Sharp reversal up to force ST flip to Buy
        for (var i = 0; i < 8; i++)
        {
            price += 5m;
            candles.Add(new Candle
            {
                Timestamp = start.AddMinutes((30 + i) * 5),
                Open = price - 2,
                High = price + 2,
                Low = price - 3,
                Close = price
            });
        }

        // Ensure the last closed bar (after hours = last candle) is the flip or Buy after Sell
        var flipFound = false;
        for (var i = candles.Count - 1; i >= TrailingStopDefaults.Period + 2; i--)
        {
            var prev = SuperTrendFlipHelper.GetTrendAtBarClose(
                candles, i - 1, TrailingStopDefaults.Period, TrailingStopDefaults.Multiplier, _st.GetTrend);
            var cur = SuperTrendFlipHelper.GetTrendAtBarClose(
                candles, i, TrailingStopDefaults.Period, TrailingStopDefaults.Multiplier, _st.GetTrend);
            if (prev == TrendDirection.Sell && cur == TrendDirection.Buy)
            {
                candles = candles.Take(i + 1).ToList();
                flipFound = true;
                break;
            }
        }

        Assert.True(flipFound, "Test fixture must produce a Sell→Buy SuperTrend flip");
        return candles;
    }
}
