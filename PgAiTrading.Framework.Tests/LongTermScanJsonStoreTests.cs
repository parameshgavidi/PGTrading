using PgAiTrading.Models;
using PgAiTrading.Services;
using Xunit;

namespace PgAiTrading.Framework.Tests;

public class LongTermScanJsonStoreTests
{
    [Fact]
    public async Task Store_round_trips_scan_document()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pg-lt-scan-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        try
        {
            var store = new LocalFileLongTermScanStore(dir);
            var doc = LongTermScanDocument.FromResults(
                new[]
                {
                    new StockScanRow
                    {
                        Symbol = "INFY",
                        Exchange = "NSE",
                        LastPrice = 1500m,
                        Quantity = 6,
                        OrderValue = 9000m,
                        FrameworkSatisfied = true,
                        FrameworkStatus = "Up",
                        FrameworkScore = 100
                    }
                },
                scannedAtUtc: new DateTime(2026, 8, 15, 10, 30, 0, DateTimeKind.Utc),
                universeCount: 3100,
                evaluatedCount: 12,
                statusMessage: "Found 1 match");

            await store.SaveAsync("local", doc);
            Assert.True(File.Exists(Path.Combine(dir, LocalFileLongTermScanStore.JsonFileName)));

            var loaded = await store.LoadAsync("local");
            Assert.NotNull(loaded);
            Assert.Equal(LongTermScanDocument.CurrentVersion, loaded!.Version);
            Assert.Single(loaded.Items);
            Assert.Equal("INFY", loaded.Items[0].Symbol);
            Assert.Equal(100, loaded.Items[0].FrameworkScore);
            Assert.Equal(3100, loaded.UniverseCount);
            Assert.Equal(12, loaded.EvaluatedCount);
            Assert.Equal(doc.ScannedAtUtc, loaded.ScannedAtUtc);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void JsonFile_returns_null_for_missing_path()
    {
        var path = Path.Combine(Path.GetTempPath(), "missing-lt-scan-" + Guid.NewGuid().ToString("N") + ".json");
        Assert.Null(LongTermScanJsonFile.Load(path));
    }

    [Fact]
    public void FundamentalDataService_exposes_known_symbols_for_prefilter()
    {
        var svc = new FundamentalDataService();
        Assert.True(svc.HasFundamentals("INFY"));
        Assert.False(svc.HasFundamentals("GUJAPOLLO"));
        Assert.Contains("INFY", svc.KnownSymbols);
    }
}
