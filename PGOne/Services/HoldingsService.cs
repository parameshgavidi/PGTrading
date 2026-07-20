using PGOne.Models;

namespace PGOne.Services;

public interface IHoldingsService
{
    event Action? HoldingsUpdated;
    List<HoldingRow> IntradayItems { get; }
    List<HoldingRow> LongTermItems { get; }
    bool IsLoading { get; }
    IReadOnlyList<string> IntradayFrameworkConditions { get; }
    IReadOnlyList<string> LongTermFrameworkConditions { get; }
    Task RefreshAsync();
}

public class HoldingsService : IHoldingsService
{
    private readonly IZerodhaService _zerodha;
    private readonly ISignalService _signal;
    private readonly IMarketDataService _marketData;
    private readonly ISuperTrendService _superTrend;
    private readonly ILongTermFrameworkService _longTermFramework;
    private readonly ISettingsService _settings;

    public event Action? HoldingsUpdated;
    public List<HoldingRow> IntradayItems { get; private set; } = new();
    public List<HoldingRow> LongTermItems { get; private set; } = new();
    public bool IsLoading { get; private set; }

    public IReadOnlyList<string> IntradayFrameworkConditions { get; } =
    [
        "1H RSI(28) bias bullish (> 55)",
        "No reversal guard — RSI < 30 on any timeframe",
        "ADX(14) on 1H not weak (≥ 18)",
        "Price above 5m VWAP",
        "1H SuperTrend bullish + 5m or 15m SuperTrend aligned",
        "Stop Loss: 5m SuperTrend (or Keltner lower when range-bound)"
    ];

    public IReadOnlyList<string> LongTermFrameworkConditions =>
        _longTermFramework.FrameworkConditions;

    public HoldingsService(
        IZerodhaService zerodha,
        ISignalService signal,
        IMarketDataService marketData,
        ISuperTrendService superTrend,
        ILongTermFrameworkService longTermFramework,
        ISettingsService settings)
    {
        _zerodha = zerodha;
        _signal = signal;
        _marketData = marketData;
        _superTrend = superTrend;
        _longTermFramework = longTermFramework;
        _settings = settings;
    }

    public async Task RefreshAsync()
    {
        IsLoading = true;
        HoldingsUpdated?.Invoke();

        var holdings = await _zerodha.GetHoldingsAsync();
        var intradayRows = new List<HoldingRow>();
        var longTermRows = new List<HoldingRow>();

        foreach (var holding in holdings)
        {
            intradayRows.Add(await BuildIntradayRowAsync(holding));
            longTermRows.Add(await BuildLongTermRowAsync(holding));
        }

        IntradayItems = SortRows(intradayRows);
        LongTermItems = SortRows(longTermRows);

        IsLoading = false;
        HoldingsUpdated?.Invoke();
    }

    private static List<HoldingRow> SortRows(List<HoldingRow> rows) =>
        rows
            .OrderByDescending(r => r.FrameworkSatisfied)
            .ThenByDescending(r => r.FrameworkScore)
            .ThenBy(r => r.Symbol)
            .ToList();

    private async Task<HoldingRow> BuildIntradayRowAsync(Holding holding)
    {
        var analysis = await _signal.AnalyzeAsync(holding.Symbol);
        var satisfied = IsIntradaySatisfied(analysis);

        return CreateRow(
            holding,
            satisfied,
            GetIntradayStatus(analysis, satisfied),
            analysis.OverallScore,
            satisfied ? null : await GetIntradayStopLossAsync(holding.Symbol, analysis));
    }

    private async Task<HoldingRow> BuildLongTermRowAsync(Holding holding)
    {
        var evaluation = await _longTermFramework.EvaluateAsync(holding.Symbol, holding.LastPrice);

        return CreateRow(
            holding,
            evaluation.Satisfied,
            evaluation.Status,
            evaluation.Score,
            evaluation.StopLoss);
    }

    private static HoldingRow CreateRow(
        Holding holding,
        bool satisfied,
        string status,
        int score,
        string? stopLoss)
    {
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
            FrameworkStatus = status,
            FrameworkScore = score,
            StopLossRecommendation = stopLoss
        };
    }

    private static bool IsIntradaySatisfied(MultiTimeframeAnalysis analysis)
    {
        if (analysis.WaitForReversal)
            return false;

        if (analysis.RsiBias != TrendDirection.Buy)
            return false;

        if (analysis.Strength1H == TrendStrength.Weak)
            return false;

        if (!analysis.AboveVwap)
            return false;

        return analysis.Trend1H == TrendDirection.Buy
            && (analysis.Trend5M == TrendDirection.Buy || analysis.Trend15M == TrendDirection.Buy);
    }

    private static string GetIntradayStatus(MultiTimeframeAnalysis analysis, bool satisfied)
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

    private async Task<string> GetIntradayStopLossAsync(string symbol, MultiTimeframeAnalysis analysis)
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
