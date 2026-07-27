using PGOne.Models;
using PGOne.Services;
using Xunit;

namespace PGOne.Framework.Tests;

public class IntradayCprServiceTests
{
    private readonly IntradayCprService _service = new();

    [Fact]
    public void BuildSegments_returns_empty_when_no_15m_candles()
    {
        var segments = _service.BuildSegments(new List<Candle>(), DateTime.Today);
        Assert.Empty(segments);
    }

    [Fact]
    public void GetActiveSegment_returns_null_for_empty_segments()
    {
        var segment = _service.GetActiveSegment(Array.Empty<IntradayCprSegment>(), DateTime.Now);
        Assert.Null(segment);
    }

    [Fact]
    public void BuildSegments_creates_intraday_windows_from_prior_session_bar()
    {
        var sessionDate = new DateTime(2026, 7, 24);
        var sessionOpen = sessionDate.Add(MarketHours.OpenTime);
        var prevBar = new Candle
        {
            Timestamp = sessionOpen.AddMinutes(-15),
            High = 100m,
            Low = 90m,
            Close = 95m
        };
        var bar915 = new Candle
        {
            Timestamp = sessionOpen.AddMinutes(15),
            High = 102m,
            Low = 94m,
            Close = 99m
        };

        var segments = _service.BuildSegments([prevBar, bar915], sessionDate);
        Assert.NotEmpty(segments);
        Assert.Equal(sessionOpen.AddMinutes(15), segments[0].Start);
        Assert.Equal(95m, segments[0].Pivot);
    }
}
