using PgAiTrading.Models;
using PgAiTrading.Services;
using Xunit;

namespace PgAiTrading.Framework.Tests;

public class AppDataFileMigrationTests
{
    [Fact]
    public void TryMigrateAutoBuyCsv_copies_from_pg_one_folder_when_new_is_empty()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pg_migrate_{Guid.NewGuid():N}");
        var legacyDir = Path.Combine(root, "PG One", "com.pgone.trading", "Data");
        var newDir = Path.Combine(root, "PG AI Trading", "com.pgaitrading.trading", "Data");
        Directory.CreateDirectory(legacyDir);
        Directory.CreateDirectory(newDir);

        var legacyPath = Path.Combine(legacyDir, "auto_buy.csv");
        var newPath = Path.Combine(newDir, "auto_buy.csv");

        try
        {
            AutoBuyCsvFile.Save(legacyPath, true, new List<AutoBuyRow>
            {
                new()
                {
                    Symbol = "TCS",
                    Exchange = "NSE",
                    Timeframe = "5m",
                    Lots = 2,
                    AutomationEnabled = true,
                    MaxDeployAmount = 10000m
                }
            });

            Assert.True(AppDataFileMigration.TryMigrateAutoBuyCsv(newPath, root));

            var (master, rows) = AutoBuyCsvFile.Load(newPath);
            Assert.True(master);
            Assert.Single(rows);
            Assert.Equal("TCS", rows[0].Symbol);
            Assert.Equal(10000m, rows[0].MaxDeployAmount);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TryMigrateAutoBuyCsv_does_not_overwrite_existing_rows()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pg_migrate_{Guid.NewGuid():N}");
        var legacyDir = Path.Combine(root, "PG One", "com.pgone.trading", "Data");
        var newDir = Path.Combine(root, "PG AI Trading", "com.pgaitrading.trading", "Data");
        Directory.CreateDirectory(legacyDir);
        Directory.CreateDirectory(newDir);

        var legacyPath = Path.Combine(legacyDir, "auto_buy.csv");
        var newPath = Path.Combine(newDir, "auto_buy.csv");

        try
        {
            AutoBuyCsvFile.Save(legacyPath, true, new List<AutoBuyRow>
            {
                new() { Symbol = "OLD", Exchange = "NSE", Timeframe = "5m", Lots = 1, AutomationEnabled = true }
            });
            AutoBuyCsvFile.Save(newPath, false, new List<AutoBuyRow>
            {
                new() { Symbol = "NEW", Exchange = "NSE", Timeframe = "15m", Lots = 3, AutomationEnabled = false }
            });

            Assert.False(AppDataFileMigration.TryMigrateAutoBuyCsv(newPath, root));

            var (_, rows) = AutoBuyCsvFile.Load(newPath);
            Assert.Single(rows);
            Assert.Equal("NEW", rows[0].Symbol);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void EnumerateLegacyFileCandidates_includes_known_renames()
    {
        var paths = AppDataFileMigration.EnumerateLegacyFileCandidates("auto_buy.csv", @"C:\Users\x\AppData\Local")
            .ToList();

        Assert.Contains(paths, p => p.Contains("PG One") && p.Contains("com.pgone.trading"));
        Assert.Contains(paths, p => p.Contains("PG One Trade") && p.Contains("com.pgonetrade.trading"));
    }
}
