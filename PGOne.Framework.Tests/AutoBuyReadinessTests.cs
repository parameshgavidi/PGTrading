using PGOne.Models;
using Xunit;

namespace PGOne.Framework.Tests;

public class AutoBuyReadinessTests
{
    [Fact]
    public void CanPlaceOrder_requires_all_gates()
    {
        var row = new AutoBuyRow
        {
            Symbol = "RELIANCE",
            Exchange = "NSE",
            Lots = 10,
            AutomationEnabled = true,
            MaxDeployAmount = 50000m,
            DeployedAmount = 10000m
        };

        Assert.True(AutoBuyReadiness.CanPlaceOrder(row, true, true, true, 10, 100m));
        Assert.False(AutoBuyReadiness.CanPlaceOrder(row, false, true, true, 10, 100m));
        Assert.False(AutoBuyReadiness.CanPlaceOrder(row, true, false, true, 10, 100m));
        Assert.False(AutoBuyReadiness.CanPlaceOrder(row, true, true, false, 10, 100m));
    }

    [Fact]
    public void CanPlaceOrder_blocks_when_max_deploy_would_be_exceeded()
    {
        var row = new AutoBuyRow
        {
            Symbol = "RELIANCE",
            Exchange = "NSE",
            Lots = 100,
            AutomationEnabled = true,
            MaxDeployAmount = 10000m,
            DeployedAmount = 9500m
        };

        Assert.False(AutoBuyReadiness.CanPlaceOrder(row, true, true, true, 100, 100m));
    }
}
