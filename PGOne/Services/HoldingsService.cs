using PGOne.Models;

namespace PGOne.Services;

public interface IHoldingsService
{
    event Action? HoldingsUpdated;
    List<HoldingRow> Items { get; }
    bool IsLoading { get; }
    Task RefreshAsync();
}

public class HoldingsService : IHoldingsService
{
    private readonly IZerodhaService _zerodha;
    private readonly ISignalService _signal;
    private readonly IMarketDataService _marketData;
    private readonly ISuperTrendService _superTrend;
    private readonly ISettingsService _settings;

    public event Action? HoldingsUpdated;
    public List<HoldingRow> Items { get; private set; } = new();
    public bool IsLoading { get; private set; }

    public HoldingsService(
        IZerodhaService zerodha,
        ISignalService signal,
        IMarketDataService marketData,
        ISuperTrendService superTrend,
        ISettingsService settings)
    {
        _zerodha = zerodha;
        _signal = signal;
        _marketData = marketData;
        _superTrend = superTrend;
        _settings = settings;
    }

    public async Task RefreshAsync()
    {
        IsLoading = true;
        HoldingsUpdated?.Invoke();

        var holdings = await _zerodha.GetHoldingsAsync();
        var rows = new List<HoldingRow>();

        foreach (var holding in holdings)
            rows.Add(await BuildRowAsync(holding));

        Items = rows
            .OrderByDescending(r => r.FrameworkSatisfied)
            .ThenByDescending(r => r.FrameworkScore)
            .ThenBy(r => r.Symbol)
            .ToList();

        IsLoading = false;
        HoldingsUpdated?.Invoke();
    }

    private async Task<HoldingRow> BuildRowAsync(Holding holding)
    {
        var analysis = await _signal.AnalyzeAsync(holding.Symbol);
        var satisfied = IsFrameworkSatisfied(analysis);
        var overallPercent = holding.AveragePrice > 0
            ? Math.Round((holding.LastPrice - holding.AveragePrice) / holding.AveragePrice * 100, 2)
            : 0m;

        return new HoldingRow
        {
            Symbol = holding.Symbol,
            Exchange = holding.Exchange,
            Quantity = holding.Quantity,
            AveragePrice = holding.AveragePrice,
            LastPrice = holding.LastPrice,
            DayChangePercent = holding.DayChangePercent,
            OverallChangePercent = overallPercent,
            FrameworkSatisfied = satisfied,
            FrameworkStatus = GetFrameworkStatus(analysis, satisfied),
            FrameworkScore = analysis.OverallScore,
            StopLossRecommendation = satisfied
                ? null
                : await GetStopLossRecommendationAsync(holding.Symbol, analysis)
        };
    }

    private static bool IsFrameworkSatisfied(MultiTimeframeAnalysis analysis)
    {
        if (analysis.WaitForReversal)
            return false;

        if (analysis.RsiBias != TrendDirection.Buy)
            return false;

        if (analysis.Strength1H == TrendStrength.Weak)
            return false;

        if (!analysis.AboveVwap)
            return false;

        var superTrendAligned = analysis.Trend1H == TrendDirection.Buy
            && (analysis.Trend5M == TrendDirection.Buy || analysis.Trend15M == TrendDirection.Buy);

        return superTrendAligned;
    }

    private static string GetFrameworkStatus(MultiTimeframeAnalysis analysis, bool satisfied)
    {
        if (satisfied)
            return "Up";

        if (analysis.WaitForReversal)
            return "Reversal risk";

        if (analysis.RsiBias == TrendDirection.Sell)
            return "Bearish";

        if (analysis.IsRangebound)
            return "Range-bound";

        if (analysis.Strength1H == TrendStrength.Weak)
            return "Weak trend";

        if (!analysis.AboveVwap)
            return "Below VWAP";

        return "Wait for alignment";
    }

    private async Task<string> GetStopLossRecommendationAsync(string symbol, MultiTimeframeAnalysis analysis)
    {
        if (analysis.WaitForReversal)
            return $"Review — {analysis.ReversalReason}";

        var instrument = InstrumentMapper.ToZerodhaKey(symbol);
        var config = _settings.Strategy;
        var candles5M = await _marketData.GetCandlesAsync(instrument, "5m", 200);

        if (analysis.IsRangebound)
        {
            var last = candles5M.LastOrDefault();
            if (last?.KeltnerLowerOuter is decimal keltner)
                return $"₹{keltner:N2} (Keltner lower)";

            return "Beyond Keltner (20,2)";
        }

        var (_, superTrendValues) = _superTrend.Calculate(
            candles5M,
            config.SuperTrend5MPeriod,
            config.SuperTrend5MMultiplier);

        if (superTrendValues.Count > 0)
            return $"₹{superTrendValues[^1]:N2} (5m SuperTrend)";

        if (analysis.RsiBias == TrendDirection.Sell)
            return "Consider exit — bearish framework";

        return "Below 5m SuperTrend";
    }
}
