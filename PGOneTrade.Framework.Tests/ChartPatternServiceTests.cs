using PGOneTrade.Models;
using PGOneTrade.Services;
using Xunit;

namespace PGOneTrade.Framework.Tests;

public class ChartPatternServiceTests
{
    private readonly ChartPatternService _sut = new();

    [Fact]
    public void Detects_bullish_engulfing()
    {
        var candles = new List<Candle>
        {
            Bar(100, 110, 99, 102),   // prior green-ish but treated as bear if close < open
            Bar(108, 108, 95, 96),    // bear
            Bar(95, 112, 94, 111)     // bull engulf
        };
        // Make first irrelevant; ensure second is bear and third engulfs
        candles[0] = Bar(100, 105, 99, 104);
        candles[1] = Bar(110, 111, 100, 101);
        candles[2] = Bar(100, 115, 99, 114);

        _sut.ApplyPatterns(candles);

        Assert.Equal("BU", candles[2].PatternCode);
        Assert.Equal(ChartPatternBias.Buy, candles[2].PatternBias);
        Assert.Contains("Bull", candles[2].PatternLabel);
    }

    [Fact]
    public void Detects_bearish_engulfing()
    {
        var candles = new List<Candle>
        {
            Bar(100, 105, 99, 104),
            Bar(101, 112, 100, 111), // bull
            Bar(112, 113, 98, 99)    // bear engulf
        };

        _sut.ApplyPatterns(candles);

        Assert.Equal("BE", candles[2].PatternCode);
        Assert.Equal(ChartPatternBias.Sell, candles[2].PatternBias);
    }

    [Fact]
    public void Detects_hammer()
    {
        var candles = new List<Candle>
        {
            Bar(100, 101, 90, 100.5m) // long lower wick, small body near top
        };

        _sut.ApplyPatterns(candles);

        Assert.Equal("H", candles[0].PatternCode);
        Assert.Equal(ChartPatternBias.Buy, candles[0].PatternBias);
    }

    [Fact]
    public void Detects_shooting_star()
    {
        var candles = new List<Candle>
        {
            Bar(100, 112, 99.5m, 100.5m) // long upper wick
        };

        _sut.ApplyPatterns(candles);

        Assert.Equal("SS", candles[0].PatternCode);
        Assert.Equal(ChartPatternBias.Sell, candles[0].PatternBias);
    }

    [Fact]
    public void Detects_doji()
    {
        var candles = new List<Candle>
        {
            Bar(100, 105, 95, 100.2m)
        };

        _sut.ApplyPatterns(candles);

        Assert.Equal("D", candles[0].PatternCode);
        Assert.Equal(ChartPatternBias.Neutral, candles[0].PatternBias);
    }

    [Fact]
    public void Detects_morning_star()
    {
        var candles = new List<Candle>
        {
            Bar(120, 121, 100, 101), // long bear
            Bar(101, 103, 98, 100),  // small body
            Bar(100, 118, 99, 117)   // strong bull reclaiming mid
        };

        _sut.ApplyPatterns(candles);

        Assert.Equal("MS", candles[2].PatternCode);
        Assert.Equal(ChartPatternBias.Buy, candles[2].PatternBias);
    }

    [Fact]
    public void Detects_inside_bar()
    {
        var candles = new List<Candle>
        {
            Bar(100, 120, 90, 110),
            Bar(105, 115, 95, 108)
        };

        _sut.ApplyPatterns(candles);

        Assert.Equal("IB", candles[1].PatternCode);
        Assert.Equal(ChartPatternBias.Neutral, candles[1].PatternBias);
    }

    [Fact]
    public void One_pattern_per_bar_prefers_higher_priority()
    {
        // Engulfing should win over a doji-like body on the signal bar.
        var candles = new List<Candle>
        {
            Bar(110, 111, 100, 101), // bear
            Bar(100, 115, 99, 114)   // bull engulf
        };

        _sut.ApplyPatterns(candles);

        Assert.Equal("BU", candles[1].PatternCode);
        Assert.NotEqual("D", candles[1].PatternCode);
    }

    private static Candle Bar(decimal open, decimal high, decimal low, decimal close) => new()
    {
        Timestamp = DateTime.UtcNow,
        Open = open,
        High = high,
        Low = low,
        Close = close,
        Volume = 1000
    };
}
