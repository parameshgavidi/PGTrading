using PGOne.Services;
using Xunit;

namespace PGOne.Framework.Tests;

public class MarketHoursTests
{
    [Fact]
    public void GetChartSessionDate_on_sunday_returns_friday()
    {
        var sundayEvening = new DateTime(2026, 8, 9, 21, 30, 0);
        Assert.Equal(new DateTime(2026, 8, 7), MarketHours.GetChartSessionDate(sundayEvening));
    }

    [Fact]
    public void GetChartSessionDate_on_saturday_returns_friday()
    {
        var saturday = new DateTime(2026, 8, 8, 12, 0, 0);
        Assert.Equal(new DateTime(2026, 8, 7), MarketHours.GetChartSessionDate(saturday));
    }

    [Fact]
    public void GetChartSessionDate_monday_before_open_returns_friday()
    {
        var mondayMorning = new DateTime(2026, 8, 10, 8, 0, 0);
        Assert.Equal(new DateTime(2026, 8, 7), MarketHours.GetChartSessionDate(mondayMorning));
    }

    [Fact]
    public void GetChartSessionDate_weekday_during_session_returns_today()
    {
        var fridaySession = new DateTime(2026, 8, 7, 11, 0, 0);
        Assert.Equal(new DateTime(2026, 8, 7), MarketHours.GetChartSessionDate(fridaySession));
    }

    [Fact]
    public void GetChartSessionDate_weekday_after_close_returns_today()
    {
        var fridayEvening = new DateTime(2026, 8, 7, 18, 0, 0);
        Assert.Equal(new DateTime(2026, 8, 7), MarketHours.GetChartSessionDate(fridayEvening));
    }
}
