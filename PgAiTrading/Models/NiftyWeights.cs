namespace PgAiTrading.Models;

public static class NiftyWeights
{
    public static readonly IReadOnlyDictionary<string, decimal> Top10 = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
    {
        ["HDFCBANK"] = 13.2m,
        ["RELIANCE"] = 10.8m,
        ["ICICIBANK"] = 8.1m,
        ["INFY"] = 6.2m,
        ["ITC"] = 4.5m,
        ["LT"] = 4.2m,
        ["TCS"] = 3.8m,
        ["AXISBANK"] = 3.5m,
        ["KOTAKBANK"] = 3.2m,
        ["SBIN"] = 3.0m
    };

    public static readonly IReadOnlyDictionary<string, decimal> DemoChangePercent = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
    {
        ["NIFTY"] = 0.34m,
        ["BANKNIFTY"] = -0.30m,
        ["SENSEX"] = 0.12m,
        ["HDFCBANK"] = -0.45m,
        ["RELIANCE"] = 1.24m,
        ["ICICIBANK"] = 0.58m,
        ["INFY"] = 1.24m,
        ["ITC"] = -0.18m,
        ["LT"] = 0.72m,
        ["TCS"] = 0.31m,
        ["AXISBANK"] = -0.62m,
        ["KOTAKBANK"] = 0.15m,
        ["SBIN"] = 0.95m
    };

    public static decimal GetWeight(string symbol) =>
        Top10.TryGetValue(symbol, out var w) ? w : 0m;

    public static decimal GetDemoChangePercent(string symbol) =>
        DemoChangePercent.TryGetValue(symbol, out var c) ? c : 0m;
}
