using PGOne.Models;
using Xunit;

namespace PGOne.Framework.Tests;

public class Top10WeightProbabilityHelperTests
{
    [Fact]
    public void Calculate_sums_sell_and_buy_weights_near_100_percent()
    {
        var items = BuildTop10DemoItems();

        var result = Top10WeightProbabilityHelper.Calculate(items);

        Assert.Equal(35.4m, result.SellWeightPercent);
        Assert.Equal(22.1m, result.BuyWeightPercent);
        Assert.Equal(57.5m, result.TotalWeightPercent);
        Assert.Equal("Bear", result.DirectionLabel);
        Assert.Equal(35, result.Probability);
    }

    [Fact]
    public void Calculate_returns_wait_when_buy_and_sell_weights_are_close()
    {
        var items = new List<WatchItem>
        {
            Item("A", TrendDirection.Buy, 50m),
            Item("B", TrendDirection.Sell, 48m)
        };

        var result = Top10WeightProbabilityHelper.Calculate(items);

        Assert.Equal("Wait", result.DirectionLabel);
        Assert.Equal("wait", result.CssClass);
    }

    [Fact]
    public void Calculate_returns_bull_when_buy_weight_dominates()
    {
        var items = new List<WatchItem>
        {
            Item("A", TrendDirection.Buy, 40m),
            Item("B", TrendDirection.Sell, 10m)
        };

        var result = Top10WeightProbabilityHelper.Calculate(items);

        Assert.Equal("Bull", result.DirectionLabel);
        Assert.Equal(40, result.Probability);
        Assert.Equal("bull", result.CssClass);
    }

    private static List<WatchItem> BuildTop10DemoItems() =>
        new()
        {
            Item("HDFCBANK", TrendDirection.Sell, 13.2m),
            Item("RELIANCE", TrendDirection.Buy, 10.8m),
            Item("ICICIBANK", TrendDirection.Buy, 8.1m),
            Item("INFY", TrendDirection.Sell, 6.2m),
            Item("ITC", TrendDirection.Sell, 4.5m),
            Item("LT", TrendDirection.Sell, 4.2m),
            Item("TCS", TrendDirection.Sell, 3.8m),
            Item("AXISBANK", TrendDirection.Sell, 3.5m),
            Item("KOTAKBANK", TrendDirection.Buy, 3.2m)
        };

    private static WatchItem Item(string symbol, TrendDirection trend, decimal weight) =>
        new()
        {
            Symbol = symbol,
            Trend = trend,
            Weight = weight
        };
}
