using System.Globalization;
using PgAiTrading.Models;

namespace PgAiTrading.Services;

public interface IFundamentalDataService
{
    StockFundamentals? GetFundamentals(string symbol);
    bool HasFundamentals(string symbol);
    IReadOnlyList<string> KnownSymbols { get; }
}

/// <summary>
/// Long-term Chartink fundamentals (ROE/ROCE/D-E/book value/mcap).
/// Loads bundled <c>Data/fundamentals.csv</c> (Nifty 500 coverage) with a small hardcoded fallback.
/// </summary>
public class FundamentalDataService : IFundamentalDataService
{
    private static readonly Dictionary<string, StockFundamentals> Fallback = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RELIANCE"] = new() { RoePercent = 8.91m, RocePercent = 10.3m, DebtEquityRatio = 0.42m, BookValuePerShare = 668m, PriceToBook = 2.3m, MarketCapCr = 1780882m },
        ["INFY"] = new() { RoePercent = 31.9m, RocePercent = 40.0m, DebtEquityRatio = 0.08m, BookValuePerShare = 225m, PriceToBook = 7.8m, MarketCapCr = 454908m },
        ["TCS"] = new() { RoePercent = 48.5m, RocePercent = 52.1m, DebtEquityRatio = 0.05m, BookValuePerShare = 250m, PriceToBook = 12.4m, MarketCapCr = 1450000m },
        ["HDFCBANK"] = new() { RoePercent = 16.8m, RocePercent = 7.2m, DebtEquityRatio = 0.95m, BookValuePerShare = 650m, PriceToBook = 2.9m, MarketCapCr = 1280000m },
        ["SBIN"] = new() { RoePercent = 17.4m, RocePercent = 6.1m, DebtEquityRatio = 1.35m, BookValuePerShare = 450m, PriceToBook = 1.6m, MarketCapCr = 680000m },
        ["HCLTECH"] = new() { RoePercent = 23.8m, RocePercent = 30.4m, DebtEquityRatio = 0.097m, BookValuePerShare = 277m, PriceToBook = 4.7m, MarketCapCr = 353455m },
        ["WIPRO"] = new() { RoePercent = 15.5m, RocePercent = 17.8m, DebtEquityRatio = 0.12m, BookValuePerShare = 83.9m, PriceToBook = 3.4m, MarketCapCr = 179064m },
        ["ICICIBANK"] = new() { RoePercent = 17.1m, RocePercent = 7.8m, DebtEquityRatio = 0.88m, BookValuePerShare = 400m, PriceToBook = 3.1m, MarketCapCr = 820000m },
        ["ITC"] = new() { RoePercent = 28.5m, RocePercent = 32.4m, DebtEquityRatio = 0.0m, BookValuePerShare = 55m, PriceToBook = 7.5m, MarketCapCr = 580000m },
        ["LT"] = new() { RoePercent = 14.2m, RocePercent = 12.8m, DebtEquityRatio = 1.12m, BookValuePerShare = 700m, PriceToBook = 5.6m, MarketCapCr = 460000m },
        ["AXISBANK"] = new() { RoePercent = 16.3m, RocePercent = 7.4m, DebtEquityRatio = 1.05m, BookValuePerShare = 550m, PriceToBook = 2.1m, MarketCapCr = 340000m },
        ["KOTAKBANK"] = new() { RoePercent = 14.8m, RocePercent = 8.1m, DebtEquityRatio = 0.72m, BookValuePerShare = 600m, PriceToBook = 2.8m, MarketCapCr = 390000m }
    };

    private readonly Dictionary<string, StockFundamentals> _data;

    public FundamentalDataService()
        : this(TryLoadBundledCsv())
    {
    }

    /// <summary>Test helper — inject CSV text directly.</summary>
    public FundamentalDataService(string? csvContent)
    {
        _data = new Dictionary<string, StockFundamentals>(Fallback, StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(csvContent))
            MergeCsv(csvContent, _data);
    }

    public IReadOnlyList<string> KnownSymbols => _data.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();

    public StockFundamentals? GetFundamentals(string symbol) =>
        _data.TryGetValue(symbol.Trim(), out var fundamentals) ? fundamentals : null;

    public bool HasFundamentals(string symbol) =>
        _data.ContainsKey(symbol.Trim());

    public static string? TryLoadBundledCsv()
    {
        foreach (var path in CandidateCsvPaths())
        {
            try
            {
                if (File.Exists(path))
                    return File.ReadAllText(path);
            }
            catch
            {
                // try next path
            }
        }

        return null;
    }

    public static IEnumerable<string> CandidateCsvPaths()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "Data", "fundamentals.csv");

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 6 && dir is not null; i++, dir = dir.Parent)
        {
            yield return Path.Combine(dir.FullName, "Data", "fundamentals.csv");
            yield return Path.Combine(dir.FullName, "PgAiTrading", "Data", "fundamentals.csv");
        }
    }

    public static void MergeCsv(string csv, Dictionary<string, StockFundamentals> target)
    {
        using var reader = new StringReader(csv);
        var header = reader.ReadLine();
        if (string.IsNullOrWhiteSpace(header))
            return;

        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var parts = line.Split(',');
            if (parts.Length < 6)
                continue;

            var symbol = parts[0].Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(symbol))
                continue;

            if (!TryParse(parts[1], out var roe)
                || !TryParse(parts[2], out var roce)
                || !TryParse(parts[3], out var de)
                || !TryParse(parts[4], out var book)
                || !TryParse(parts[5], out var mcap))
                continue;

            target[symbol] = new StockFundamentals
            {
                RoePercent = roe,
                RocePercent = roce,
                DebtEquityRatio = de,
                BookValuePerShare = book,
                // Live P/B is Close / BookValue at evaluation time (Chartink parity).
                PriceToBook = 0m,
                MarketCapCr = mcap
            };
        }
    }

    private static bool TryParse(string raw, out decimal value) =>
        decimal.TryParse(raw.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out value);
}
