using PgAiTrading.Models;
using PgAiTrading.Services;
using Xunit;

namespace PgAiTrading.Framework.Tests;

public class AutoBuyJsonStoreTests
{
    [Fact]
    public void Json_roundtrip_preserves_master_and_rows()
    {
        var path = Path.Combine(Path.GetTempPath(), $"auto_buy_{Guid.NewGuid():N}.json");
        try
        {
            var doc = AutoBuyDocument.FromRuntime(true, new List<AutoBuyRow>
            {
                new()
                {
                    Symbol = "RELIANCE",
                    Exchange = "NSE",
                    Timeframe = "5m",
                    Lots = 10,
                    AutomationEnabled = true,
                    MaxDeployAmount = 25000m,
                    Status = "should-not-persist",
                    DeployedAmount = 999m
                }
            });

            AutoBuyJsonFile.Save(path, doc);
            var loaded = AutoBuyJsonFile.Load(path);

            Assert.NotNull(loaded);
            Assert.Equal(AutoBuyDocument.CurrentVersion, loaded!.Version);
            Assert.True(loaded.MasterAutomationEnabled);
            Assert.Single(loaded.Rows);
            Assert.Equal("RELIANCE", loaded.Rows[0].Symbol);
            Assert.Equal(25000m, loaded.Rows[0].MaxDeployAmount);

            var runtime = loaded.ToRuntime().Rows[0];
            Assert.Equal(0m, runtime.DeployedAmount);
            Assert.Equal("Idle", runtime.Status);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void Json_roundtrip_preserves_failed_entries_and_ip()
    {
        var path = Path.Combine(Path.GetTempPath(), $"auto_buy_failed_{Guid.NewGuid():N}.json");
        try
        {
            var failedAt = new DateTime(2026, 8, 17, 10, 15, 30);
            var doc = AutoBuyDocument.FromRuntime(
                true,
                new List<AutoBuyRow>
                {
                    new() { Symbol = "INFY", Exchange = "NSE", Timeframe = "5m", Lots = 2, AutomationEnabled = true }
                },
                new List<AutoBuyFailedEntry>
                {
                    new()
                    {
                        Symbol = "INFY",
                        Exchange = "NSE",
                        Timeframe = "5m",
                        Quantity = 2,
                        Status = "Order failed",
                        Detail = "Rejected by exchange",
                        IpAddress = "203.0.113.10",
                        FailedAt = failedAt
                    }
                });

            AutoBuyJsonFile.Save(path, doc);
            var loaded = AutoBuyJsonFile.Load(path);

            Assert.NotNull(loaded);
            Assert.Equal(AutoBuyDocument.CurrentVersion, loaded!.Version);
            Assert.Single(loaded.FailedEntries);
            Assert.Equal("INFY", loaded.FailedEntries[0].Symbol);
            Assert.Equal("203.0.113.10", loaded.FailedEntries[0].IpAddress);
            Assert.Equal(failedAt, loaded.FailedEntries[0].FailedAt);
            Assert.Equal("Rejected by exchange", loaded.FailedEntries[0].Detail);

            var runtimeFailed = loaded.ToRuntime().FailedEntries[0];
            Assert.Equal("INFY", runtimeFailed.Symbol);
            Assert.Equal("203.0.113.10", runtimeFailed.IpAddress);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void FromRuntime_caps_failed_entries_at_max()
    {
        var failed = Enumerable.Range(0, AutoBuyDefaults.MaxFailedEntries + 25)
            .Select(i => new AutoBuyFailedEntry
            {
                Symbol = "TCS",
                Detail = $"fail-{i}",
                IpAddress = "198.51.100.1",
                FailedAt = DateTime.Now.AddMinutes(-i)
            })
            .ToList();

        var doc = AutoBuyDocument.FromRuntime(false, Array.Empty<AutoBuyRow>(), failed);
        Assert.Equal(AutoBuyDefaults.MaxFailedEntries, doc.FailedEntries.Count);
        Assert.Equal("fail-0", doc.FailedEntries[0].Detail);
    }

    [Fact]
    public async Task LocalStore_migrates_same_folder_csv_to_json()
    {
        var appData = Path.Combine(Path.GetTempPath(), $"pg_app_{Guid.NewGuid():N}");
        var localRoot = Path.Combine(Path.GetTempPath(), $"pg_local_{Guid.NewGuid():N}");
        Directory.CreateDirectory(appData);

        try
        {
            AutoBuyCsvFile.Save(
                Path.Combine(appData, "auto_buy.csv"),
                true,
                new List<AutoBuyRow>
                {
                    new() { Symbol = "INFY", Exchange = "NSE", Timeframe = "15m", Lots = 4, AutomationEnabled = true }
                });

            var store = new LocalFileAutoBuyStore(appData, localRoot);
            var loaded = await store.LoadAsync("local");

            Assert.NotNull(loaded);
            Assert.True(loaded!.MasterAutomationEnabled);
            Assert.Equal("INFY", loaded.Rows[0].Symbol);
            Assert.True(File.Exists(Path.Combine(appData, "auto_buy.json")));
        }
        finally
        {
            if (Directory.Exists(appData))
                Directory.Delete(appData, true);
            if (Directory.Exists(localRoot))
                Directory.Delete(localRoot, true);
        }
    }

    [Fact]
    public async Task LocalStore_migrates_legacy_pg_one_csv()
    {
        var appData = Path.Combine(Path.GetTempPath(), $"pg_app_{Guid.NewGuid():N}");
        var localRoot = Path.Combine(Path.GetTempPath(), $"pg_local_{Guid.NewGuid():N}");
        var legacyDir = Path.Combine(localRoot, "PG One", "com.pgone.trading", "Data");
        Directory.CreateDirectory(appData);
        Directory.CreateDirectory(legacyDir);

        try
        {
            AutoBuyCsvFile.Save(
                Path.Combine(legacyDir, "auto_buy.csv"),
                false,
                new List<AutoBuyRow>
                {
                    new() { Symbol = "TCS", Exchange = "NSE", Timeframe = "5m", Lots = 2, AutomationEnabled = true, MaxDeployAmount = 8000m }
                });

            var store = new LocalFileAutoBuyStore(appData, localRoot);
            var loaded = await store.LoadAsync("local");

            Assert.NotNull(loaded);
            Assert.False(loaded!.MasterAutomationEnabled);
            Assert.Equal("TCS", loaded.Rows[0].Symbol);
            Assert.Equal(8000m, loaded.Rows[0].MaxDeployAmount);
        }
        finally
        {
            if (Directory.Exists(appData))
                Directory.Delete(appData, true);
            if (Directory.Exists(localRoot))
                Directory.Delete(localRoot, true);
        }
    }

    [Fact]
    public async Task LocalStore_does_not_overwrite_existing_json_rows()
    {
        var appData = Path.Combine(Path.GetTempPath(), $"pg_app_{Guid.NewGuid():N}");
        var localRoot = Path.Combine(Path.GetTempPath(), $"pg_local_{Guid.NewGuid():N}");
        var legacyDir = Path.Combine(localRoot, "PG One", "com.pgone.trading", "Data");
        Directory.CreateDirectory(appData);
        Directory.CreateDirectory(legacyDir);

        try
        {
            AutoBuyJsonFile.Save(
                Path.Combine(appData, "auto_buy.json"),
                AutoBuyDocument.FromRuntime(false, new List<AutoBuyRow>
                {
                    new() { Symbol = "NEW", Exchange = "NSE", Timeframe = "1m", Lots = 1, AutomationEnabled = false }
                }));

            AutoBuyCsvFile.Save(
                Path.Combine(legacyDir, "auto_buy.csv"),
                true,
                new List<AutoBuyRow>
                {
                    new() { Symbol = "OLD", Exchange = "NSE", Timeframe = "5m", Lots = 9, AutomationEnabled = true }
                });

            var store = new LocalFileAutoBuyStore(appData, localRoot);
            var loaded = await store.LoadAsync("local");

            Assert.NotNull(loaded);
            Assert.Single(loaded!.Rows);
            Assert.Equal("NEW", loaded.Rows[0].Symbol);
        }
        finally
        {
            if (Directory.Exists(appData))
                Directory.Delete(appData, true);
            if (Directory.Exists(localRoot))
                Directory.Delete(localRoot, true);
        }
    }

    [Fact]
    public void NormalizeTimeframe_defaults_invalid_to_5m()
    {
        Assert.Equal("5m", AutoBuyTimeframes.Normalize("bad"));
        Assert.Equal("15m", AutoBuyTimeframes.Normalize("15m"));
    }
}
