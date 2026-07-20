using PGOne.Models;

namespace PGOne.Services;

public interface IFundamentalDataService
{
    StockFundamentals? GetFundamentals(string symbol);
}

public class FundamentalDataService : IFundamentalDataService
{
    private static readonly Dictionary<string, StockFundamentals> Data = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RELIANCE"] = new() { RoePercent = 9.5m, RocePercent = 10.2m, DebtEquityRatio = 0.42m, PriceToBook = 2.3m, MarketCapCr = 1950000m },
        ["INFY"] = new() { RoePercent = 31.2m, RocePercent = 34.5m, DebtEquityRatio = 0.08m, PriceToBook = 7.8m, MarketCapCr = 760000m },
        ["TCS"] = new() { RoePercent = 48.5m, RocePercent = 52.1m, DebtEquityRatio = 0.05m, PriceToBook = 12.4m, MarketCapCr = 1450000m },
        ["HDFCBANK"] = new() { RoePercent = 16.8m, RocePercent = 7.2m, DebtEquityRatio = 0.95m, PriceToBook = 2.9m, MarketCapCr = 1280000m },
        ["SBIN"] = new() { RoePercent = 17.4m, RocePercent = 6.1m, DebtEquityRatio = 1.35m, PriceToBook = 1.6m, MarketCapCr = 680000m },
        ["HCLTECH"] = new() { RoePercent = 24.6m, RocePercent = 28.3m, DebtEquityRatio = 0.09m, PriceToBook = 6.2m, MarketCapCr = 420000m },
        ["WIPRO"] = new() { RoePercent = 17.8m, RocePercent = 20.4m, DebtEquityRatio = 0.12m, PriceToBook = 3.4m, MarketCapCr = 250000m },
        ["ICICIBANK"] = new() { RoePercent = 17.1m, RocePercent = 7.8m, DebtEquityRatio = 0.88m, PriceToBook = 3.1m, MarketCapCr = 820000m },
        ["ITC"] = new() { RoePercent = 28.5m, RocePercent = 32.4m, DebtEquityRatio = 0.0m, PriceToBook = 7.5m, MarketCapCr = 580000m },
        ["LT"] = new() { RoePercent = 14.2m, RocePercent = 12.8m, DebtEquityRatio = 1.12m, PriceToBook = 5.6m, MarketCapCr = 460000m },
        ["AXISBANK"] = new() { RoePercent = 16.3m, RocePercent = 7.4m, DebtEquityRatio = 1.05m, PriceToBook = 2.1m, MarketCapCr = 340000m },
        ["KOTAKBANK"] = new() { RoePercent = 14.8m, RocePercent = 8.1m, DebtEquityRatio = 0.72m, PriceToBook = 2.8m, MarketCapCr = 390000m }
    };

    public StockFundamentals? GetFundamentals(string symbol) =>
        Data.TryGetValue(symbol.Trim(), out var fundamentals) ? fundamentals : null;
}
