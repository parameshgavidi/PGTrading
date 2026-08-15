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
}
