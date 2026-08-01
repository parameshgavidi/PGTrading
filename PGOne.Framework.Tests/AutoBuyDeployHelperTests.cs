using PGOne.Models;
using Xunit;

namespace PGOne.Framework.Tests;

public class AutoBuyDeployHelperTests
{
    [Fact]
    public void GetDeployedAmount_sums_holdings_and_cnc_positions()
    {
        var holdings = new List<Holding>
        {
            new() { Symbol = "RELIANCE", Quantity = 10, LastPrice = 100m },
            new() { Symbol = "INFY", Quantity = 5, LastPrice = 50m }
        };

        var positions = new List<Position>
        {
            new() { Symbol = "RELIANCE", Quantity = 2, LastPrice = 100m }
        };

        var deployed = AutoBuyDeployHelper.GetDeployedAmount("RELIANCE", holdings, positions);

        Assert.Equal(1200m, deployed);
    }

    [Fact]
    public void IsMaxDeployReached_when_deployed_meets_max()
    {
        Assert.True(AutoBuyDeployHelper.IsMaxDeployReached(5000m, 5000m));
        Assert.False(AutoBuyDeployHelper.IsMaxDeployReached(4000m, 5000m));
        Assert.False(AutoBuyDeployHelper.IsMaxDeployReached(6000m, 0m));
    }
}
