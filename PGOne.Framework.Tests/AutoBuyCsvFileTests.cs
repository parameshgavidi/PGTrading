using PGOne.Models;
using Xunit;

namespace PGOne.Framework.Tests;

public class AutoBuyCsvFileTests
{
    [Fact]
    public void Save_and_load_roundtrip_master_and_rows()
    {
        var path = Path.Combine(Path.GetTempPath(), $"auto_buy_test_{Guid.NewGuid():N}.csv");

        try
        {
            var rows = new List<AutoBuyRow>
            {
                new()
                {
                    Symbol = "RELIANCE",
                    Exchange = "NSE",
                    Timeframe = "5m",
                    Lots = 10,
                    AutomationEnabled = true,
                    MaxDeployAmount = 25000m
                },
                new()
                {
                    Symbol = "INFY",
                    Exchange = "NSE",
                    Timeframe = "1m",
                    Lots = 5,
                    AutomationEnabled = false
                }
            };

            AutoBuyCsvFile.Save(path, true, rows);
            var (master, loaded) = AutoBuyCsvFile.Load(path);

            Assert.True(master);
            Assert.Equal(2, loaded.Count);
            var reliance = loaded.First(r => r.Symbol == "RELIANCE");
            Assert.Equal(10, reliance.Lots);
            Assert.Equal(25000m, reliance.MaxDeployAmount);
            var infy = loaded.First(r => r.Symbol == "INFY");
            Assert.Equal("1m", infy.Timeframe);
            Assert.False(infy.AutomationEnabled);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void NormalizeTimeframe_defaults_invalid_to_5m()
    {
        Assert.Equal("5m", AutoBuyCsvFile.NormalizeTimeframe("bad"));
        Assert.Equal("15m", AutoBuyCsvFile.NormalizeTimeframe("15m"));
    }

    [Fact]
    public void MaxSymbols_is_one()
    {
        Assert.Equal(1, AutoBuyDefaults.MaxSymbols);
        Assert.Equal("CNC", AutoBuyDefaults.Product);
    }
}
