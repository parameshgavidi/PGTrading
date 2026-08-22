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
        Assert.True(svc.HasFundamentals("HCLTECH"));
        Assert.False(svc.HasFundamentals("GUJAPOLLO"));
        Assert.Contains("INFY", svc.KnownSymbols);
    }

    [Fact]
    public void FundamentalDataService_csv_overrides_book_value_for_hcltech()
    {
        const string csv = """
            Symbol,RoePercent,RocePercent,DebtEquityRatio,BookValuePerShare,MarketCapCr
            HCLTECH,23.8,30.4,0.097,277.0,353455.0
            """;

        var svc = new FundamentalDataService(csv);
        var f = svc.GetFundamentals("HCLTECH");
        Assert.NotNull(f);
        Assert.Equal(277m, f!.BookValuePerShare);
        Assert.Equal(23.8m, f.RoePercent);
        Assert.Equal(30.4m, f.RocePercent);

        // Chartink: Close / Book Value — was failing with stale static P/B 6.2
        var pb = f.ResolvePriceToBook(1302.5m);
        Assert.True(pb < 5m, $"Expected P/B < 5 for HCLTECH @ 1302.5, got {pb}");
        Assert.InRange(pb, 4.5m, 4.8m);
    }

    [Fact]
    public void StockFundamentals_resolve_price_to_book_prefers_book_value()
    {
        var f = new StockFundamentals { BookValuePerShare = 100m, PriceToBook = 9m };
        Assert.Equal(4.5m, f.ResolvePriceToBook(450m));
    }

    [Fact]
    public void StockFundamentals_resolve_price_to_book_falls_back_to_stored_pb()
    {
        var f = new StockFundamentals { BookValuePerShare = 0m, PriceToBook = 3.2m };
        Assert.Equal(3.2m, f.ResolvePriceToBook(450m));
    }

    [Fact]
    public void LongTermFramework_yearly_high_uses_lookback_window()
    {
        var daily = new List<Candle>();
        for (var i = 0; i < 300; i++)
        {
            daily.Add(new Candle
            {
                Timestamp = DateTime.UtcNow.Date.AddDays(-300 + i),
                Open = 100,
                High = i == 0 ? 999m : 110m, // old spike outside 252-day window
                Low = 90,
                Close = 105,
                Volume = 1_000_000
            });
        }

        // Place a recent yearly high inside the window
        daily[^10].High = 200m;

        var high = LongTermChartinkMath.YearlyHigh(daily, lookbackDays: 252);
        Assert.Equal(200m, high);
        Assert.NotEqual(999m, high);
    }
}
