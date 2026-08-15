using PgAiTrading.Models;
using Xunit;

namespace PgAiTrading.Framework.Tests;

public class CamarillaCalculatorTests
{
    [Fact]
    public void FromPreviousDay_matches_standard_camarilla_formula()
    {
        var prev = new Candle { High = 1220m, Low = 1200m, Close = 1217m };
        var cam = CamarillaCalculator.FromPreviousDay(prev);

        Assert.True(cam.HasData);
        Assert.Equal(1228.00m, cam.H4);
        Assert.Equal(1222.50m, cam.H3);
        Assert.Equal(1220.67m, cam.H2);
        Assert.Equal(1212.33m, cam.Pivot);
        Assert.Equal(1213.33m, cam.L2);
        Assert.Equal(1211.50m, cam.L3);
        Assert.Equal(1206.00m, cam.L4);
    }

    [Fact]
    public void FromAvailableSessions_prefers_completed_daily_bar_near_live_price()
    {
        var today = new DateTime(2026, 8, 15);
        var daily = new List<Candle>
        {
            new() { Timestamp = today.AddDays(-2), High = 1210m, Low = 1190m, Close = 1200m },
            new() { Timestamp = today.AddDays(-1), High = 1230m, Low = 1205m, Close = 1220m },
            new() { Timestamp = today, High = 1240m, Low = 1215m, Close = 1225m }
        };

        var cam = CamarillaCalculator.FromAvailableSessions(daily, null, referencePrice: 1225m, asOfDate: today);

        Assert.True(cam.HasData);
        Assert.Equal(1220m, cam.PrevClose);
        Assert.Equal(1230m, cam.PrevHigh);
        Assert.Equal(1205m, cam.PrevLow);
    }

    [Fact]
    public void FromAvailableSessions_rejects_mismatched_demo_daily_and_uses_intraday_session()
    {
        var today = new DateTime(2026, 8, 15);
        // Demo/fallback daily around 1000 while live price is ~1220.
        var badDaily = new List<Candle>
        {
            new() { Timestamp = today.AddDays(-1), High = 1010m, Low = 990m, Close = 1000m },
            new() { Timestamp = today, High = 1005m, Low = 995m, Close = 1002m }
        };

        var prevSession = new List<Candle>
        {
            new() { Timestamp = today.AddDays(-1).AddHours(9).AddMinutes(15), Open = 1200m, High = 1210m, Low = 1195m, Close = 1205m },
            new() { Timestamp = today.AddDays(-1).AddHours(15).AddMinutes(15), Open = 1205m, High = 1225m, Low = 1200m, Close = 1220m }
        };

        var cam = CamarillaCalculator.FromAvailableSessions(badDaily, prevSession, referencePrice: 1222m, asOfDate: today);

        Assert.True(cam.HasData);
        Assert.Equal(1220m, cam.PrevClose);
        Assert.Equal(1225m, cam.PrevHigh);
        Assert.Equal(1195m, cam.PrevLow);
        Assert.InRange(cam.Pivot, 1180m, 1250m);
    }

    [Fact]
    public void IsPlausibleSessionBar_rejects_far_demo_prices()
    {
        var demo = new Candle { High = 1010m, Low = 990m, Close = 1000m };
        Assert.False(CamarillaCalculator.IsPlausibleSessionBar(demo, 1220m));
        Assert.True(CamarillaCalculator.IsPlausibleSessionBar(demo, 1005m));
    }

    [Theory]
    [InlineData("1m", "1D")]
    [InlineData("5m", "1D")]
    [InlineData("15m", "1D")]
    [InlineData("1H", "1D")]
    [InlineData("1D", "1W")]
    [InlineData("1W", "1M")]
    public void ResolvePivotTimeframe_follows_tradingview_auto(string chartTf, string expectedPivot)
    {
        Assert.Equal(expectedPivot, CamarillaCalculator.ResolvePivotTimeframe(chartTf));
    }

    [Fact]
    public void ForChartTimeframe_daily_chart_uses_previous_week()
    {
        var asOf = new DateTime(2026, 8, 15); // Saturday
        var daily = new List<Candle>();
        // Two prior weeks of flat-ish daily bars
        for (var d = -20; d <= 0; d++)
        {
            var day = asOf.AddDays(d);
            if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                continue;
            daily.Add(new Candle
            {
                Timestamp = day,
                Open = 1200m + d,
                High = 1210m + d,
                Low = 1190m + d,
                Close = 1205m + d
            });
        }

        var cam = CamarillaCalculator.ForChartTimeframe(
            "1D",
            daily,
            daily,
            previousIntradaySession: null,
            referencePrice: 1205m,
            asOfDate: asOf);

        Assert.True(cam.HasData);
        Assert.Equal("1W", cam.PivotTimeframe);
        Assert.InRange(cam.Pivot, 1100m, 1300m);
    }
}
