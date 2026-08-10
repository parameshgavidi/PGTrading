using PgAiTrading.Models;
using Xunit;

namespace PgAiTrading.Framework.Tests;

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

    [Fact]
    public void Evaluate_reports_multiple_stocks_and_per_row_caps()
    {
        var rows = new List<AutoBuyRow>
        {
            new()
            {
                Symbol = "RELIANCE",
                Exchange = "NSE",
                Timeframe = "5m",
                AutomationEnabled = true,
                MaxDeployAmount = 50000m,
                DeployedAmount = 1000m,
                Status = "Waiting"
            },
            new()
            {
                Symbol = "IRFC",
                Exchange = "NSE",
                Timeframe = "1m",
                AutomationEnabled = false,
                MaxDeployAmount = 1000m,
                DeployedAmount = 0m,
                Status = "Disabled"
            }
        };

        var checks = AutoBuyReadiness.Evaluate(true, rows, true, true, true);

        Assert.Contains(checks, c => c.Label == "NSE stocks in list" && c.Passed && c.Detail.Contains("2"));
        Assert.Contains(checks, c => c.Label == "Row automation" && c.Passed);
        Assert.Contains(checks, c => c.Label == "Deploy capacity" && c.Passed);
        Assert.Contains(checks, c => c.Label == "Per-stock entry" && c.Passed);
    }

    [Fact]
    public void Evaluate_deploy_capacity_passes_when_some_rows_at_max()
    {
        var rows = new List<AutoBuyRow>
        {
            new()
            {
                Symbol = "RELIANCE",
                Exchange = "NSE",
                AutomationEnabled = true,
                MaxDeployAmount = 1000m,
                DeployedAmount = 1000m
            },
            new()
            {
                Symbol = "TCS",
                Exchange = "NSE",
                AutomationEnabled = true,
                MaxDeployAmount = 50000m,
                DeployedAmount = 1000m
            }
        };

        var checks = AutoBuyReadiness.Evaluate(true, rows, true, true, true);

        var deploy = checks.First(c => c.Label == "Deploy capacity");
        Assert.True(deploy.Passed);
        Assert.Contains("RELIANCE", deploy.Detail);
        Assert.Contains("can still enter", deploy.Detail);
    }

    [Fact]
    public void Evaluate_does_not_require_global_st_signal()
    {
        var rows = new List<AutoBuyRow>
        {
            new()
            {
                Symbol = "INFY",
                Exchange = "NSE",
                AutomationEnabled = true,
                Status = "Waiting"
            }
        };

        var checks = AutoBuyReadiness.Evaluate(true, rows, true, true, true);

        Assert.DoesNotContain(checks, c => c.Label == "ST(7,2.5) buy signal");
        Assert.Contains(checks, c => c.Label == "Per-stock entry" && c.Passed);
    }
}
