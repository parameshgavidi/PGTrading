using PgAiTrading.Models;
using PgAiTrading.Services;
using Xunit;

namespace PgAiTrading.Framework.Tests;

public class SuperTrendFlipHelperTests
{
    private readonly SuperTrendService _st = new();

    [Fact]
    public void GetLastClosedBarIndex_uses_last_candle_when_market_closed()
    {
        var candles = BuildRisingThenFlip(20, barMinutes: 5);
        var afterHours = new DateTime(2026, 8, 5, 8, 47, 0); // before open

        var index = SuperTrendFlipHelper.GetLastClosedBarIndex(candles, "5m", afterHours);

        Assert.Equal(candles.Count - 1, index);
    }

    [Fact]
    public void GetLastClosedBarIndex_skips_forming_5m_candle_during_market_hours()
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

        // Last bar 11:35–11:40 IST; 11:37 is still forming.
        var duringBar = new DateTime(2026, 8, 5, 11, 37, 0);
        var index = SuperTrendFlipHelper.GetLastClosedBarIndex(candles, "5m", duringBar);

        Assert.Equal(candles.Count - 2, index);
    }

    [Fact]
    public void GetLastClosedBarIndex_skips_forming_1m_candle_during_market_hours()
    {
        var start = new DateTime(2026, 8, 5, 12, 0, 0);
        var candles = new List<Candle>();
        for (var i = 0; i < 30; i++)
        {
            candles.Add(new Candle
            {
                Timestamp = start.AddMinutes(i),
                Open = 100 + i,
                High = 101 + i,
                Low = 99 + i,
                Close = 100.5m + i
            });
        }

        // Last bar 12:29–12:30 IST; 12:29:30 is still forming.
        var duringBar = new DateTime(2026, 8, 5, 12, 29, 30);
        var index = SuperTrendFlipHelper.GetLastClosedBarIndex(candles, "1m", duringBar);

        Assert.Equal(candles.Count - 2, index);

        var justClosed = new DateTime(2026, 8, 5, 12, 30, 0);
        Assert.Equal(candles.Count - 1, SuperTrendFlipHelper.GetLastClosedBarIndex(candles, "1m", justClosed));
    }

    [Fact]
    public void IsFormingBar_false_outside_market_hours_even_if_bar_end_in_future()
    {
        var candle = new Candle
        {
            Timestamp = new DateTime(2026, 8, 5, 15, 29, 0),
            Open = 1, High = 1, Low = 1, Close = 1
        };
        // After close — Zerodha last candle is already closed.
        var afterHours = new DateTime(2026, 8, 5, 16, 0, 0);

        Assert.False(SuperTrendFlipHelper.IsFormingBar(candle, "1m", afterHours));
    }

    [Fact]
    public void IsBuyTrigger_detects_sell_to_buy_on_last_closed_bar_after_hours()
    {
        var candles = BuildForcedSellThenBuyFlip(barMinutes: 5);
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
    public void IsBuyTrigger_works_on_1m_timeframe()
    {
        var candles = BuildForcedSellThenBuyFlip(barMinutes: 1);
        var afterHours = candles[^1].Timestamp.Date.AddHours(16);

        var trigger = SuperTrendFlipHelper.IsBuyTriggerOnLastClosedBar(
            candles,
            TrailingStopDefaults.Period,
            TrailingStopDefaults.Multiplier,
            _st.GetTrend,
            "1m",
            afterHours);

        Assert.True(trigger);
    }

    [Fact]
    public void IsBuyTrigger_false_when_flip_is_not_on_last_closed_bar()
    {
        var candles = BuildForcedSellThenBuyFlip(barMinutes: 5);

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
        var candles = BuildForcedSellThenBuyFlip(barMinutes: 5);
        var afterHours = new DateTime(2026, 8, 4, 16, 0, 0);

        Assert.True(SuperTrendFlipHelper.IsBuyTriggerOnLastClosedBar(
            candles,
            TrailingStopDefaults.Period,
            TrailingStopDefaults.Multiplier,
            _st.GetTrend,
            "5m",
            afterHours));
    }

    private static List<Candle> BuildRisingThenFlip(int count, int barMinutes)
    {
        var start = new DateTime(2026, 8, 4, 10, 0, 0);
        var candles = new List<Candle>();
        for (var i = 0; i < count; i++)
        {
            candles.Add(new Candle
            {
                Timestamp = start.AddMinutes(i * barMinutes),
                Open = 100 + i,
                High = 102 + i,
                Low = 99 + i,
                Close = 101 + i
            });
        }

        return candles;
    }

    /// <summary>Build a series that ends with a clear Sell→Buy SuperTrend flip on the last bar.</summary>
    private List<Candle> BuildForcedSellThenBuyFlip(int barMinutes)
    {
        var start = new DateTime(2026, 8, 4, 10, 0, 0);
        var candles = new List<Candle>();

        decimal price = 200m;
        for (var i = 0; i < 30; i++)
        {
            price -= 2m;
            candles.Add(new Candle
            {
                Timestamp = start.AddMinutes(i * barMinutes),
                Open = price + 1,
                High = price + 1.5m,
                Low = price - 1,
                Close = price
            });
        }

        for (var i = 0; i < 8; i++)
        {
            price += 5m;
            candles.Add(new Candle
            {
                Timestamp = start.AddMinutes((30 + i) * barMinutes),
                Open = price - 2,
                High = price + 2,
                Low = price - 3,
                Close = price
            });
        }

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
