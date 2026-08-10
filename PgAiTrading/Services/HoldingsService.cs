using PgAiTrading.Models;

namespace PgAiTrading.Services;

public interface IHoldingsService
{
    event Action? HoldingsUpdated;
    List<HoldingRow> IntradayItems { get; }
    List<HoldingRow> LongTermItems { get; }
    bool IsLoading { get; }
    string? ErrorMessage { get; }
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

    public event Action? HoldingsUpdated;
    public List<HoldingRow> IntradayItems { get; private set; } = new();
    public List<HoldingRow> LongTermItems { get; private set; } = new();
    public bool IsLoading { get; private set; }
    public string? ErrorMessage { get; private set; }

    public IReadOnlyList<string> IntradayFrameworkConditions =>
        IntradayFrameworkEvaluator.Conditions;

    public IReadOnlyList<string> LongTermFrameworkConditions =>
        _longTermFramework.FrameworkConditions;

    public HoldingsService(
        IZerodhaService zerodha,
        ISignalService signal,
        IMarketDataService marketData,
        ISuperTrendService superTrend,
        ILongTermFrameworkService longTermFramework)
    {
        _zerodha = zerodha;
        _signal = signal;
        _marketData = marketData;
        _superTrend = superTrend;
        _longTermFramework = longTermFramework;
    }

    public async Task RefreshAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        HoldingsUpdated?.Invoke();

        try
        {
            var intradayRows = new List<HoldingRow>();
            var longTermRows = new List<HoldingRow>();

            var misPositions = await _zerodha.GetMisPositionsAsync(includeClosed: true);
            foreach (var position in misPositions)
            {
                intradayRows.Add(await BuildIntradayRowAsync(position));
            }

            var holdings = await _zerodha.GetHoldingsAsync();
            foreach (var holding in holdings)
            {
                longTermRows.Add(await BuildLongTermRowAsync(holding));
            }

            IntradayItems = SortIntradayRows(intradayRows);
            LongTermItems = SortRows(longTermRows);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
            HoldingsUpdated?.Invoke();
        }
    }

    private static Holding ToHolding(Position position)
    {
        var quantity = position.IsClosed ? 0 : Math.Abs(position.Quantity);

        return new Holding
        {
            Symbol = position.Symbol,
            Exchange = position.Exchange,
            Quantity = quantity,
            AveragePrice = position.AveragePrice,
            LastPrice = position.LastPrice,
            DayChangePercent = 0m,
            PnL = position.PnL
        };
    }

    private static List<HoldingRow> SortIntradayRows(List<HoldingRow> rows) =>
        rows
            .OrderBy(r => r.IsClosed)
            .ThenByDescending(r => r.FrameworkSatisfied)
            .ThenByDescending(r => r.FrameworkScore)
            .ThenBy(r => r.Symbol)
            .ToList();

    private static List<HoldingRow> SortRows(List<HoldingRow> rows) =>
        rows
            .OrderByDescending(r => r.FrameworkSatisfied)
            .ThenByDescending(r => r.FrameworkScore)
            .ThenBy(r => r.Symbol)
            .ToList();

    private async Task<HoldingRow> BuildIntradayRowAsync(Position position)
    {
        var holding = ToHolding(position);
        if (position.IsClosed)
            return CreateClosedRow(holding);

        var instrument = InstrumentMapper.ToZerodhaKey(holding.Symbol, holding.Exchange);
        var analysis = await _signal.AnalyzeForFrameworkAsync(instrument);
        var satisfied = IsIntradaySatisfied(analysis);

        return CreateRow(
            holding,
            satisfied,
            GetIntradayStatus(analysis, satisfied),
            analysis.OverallScore,
            satisfied ? null : await GetIntradayStopLossAsync(holding, analysis));
    }

    private static HoldingRow CreateClosedRow(Holding holding) =>
        new()
        {
            Symbol = holding.Symbol,
            Exchange = holding.Exchange,
            Quantity = 0,
            AveragePrice = holding.AveragePrice,
            LastPrice = holding.LastPrice,
            DayChangePercent = 0m,
            OverallChangePercent = 0m,
            FrameworkSatisfied = holding.PnL >= 0,
            FrameworkStatus = "Closed",
            FrameworkScore = 0,
            StopLossRecommendation = null,
            IsClosed = true,
            PnL = holding.PnL
        };

    private async Task<HoldingRow> BuildLongTermRowAsync(Holding holding)
    {
        var evaluation = await _longTermFramework.EvaluateAsync(
            holding.Symbol,
            holding.LastPrice,
            holding.Exchange);

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
            StopLossRecommendation = stopLoss,
            PnL = holding.PnL
        };
    }

    private static bool IsIntradaySatisfied(MultiTimeframeAnalysis analysis) =>
        IntradayFrameworkEvaluator.IsSatisfied(analysis);

    private static string GetIntradayStatus(MultiTimeframeAnalysis analysis, bool satisfied) =>
        IntradayFrameworkEvaluator.GetStatus(analysis, satisfied);

    private async Task<string> GetIntradayStopLossAsync(Holding holding, MultiTimeframeAnalysis analysis)
    {
        if (analysis.WaitForReversal)
            return $"Review — {analysis.ReversalReason}";

        var instrument = InstrumentMapper.ToZerodhaKey(holding.Symbol, holding.Exchange);
        var config = FrameworkDefaults.Intraday;
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

        if (analysis.TradeDirection == TrendDirection.Sell)
            return "Consider exit — bearish framework";

        return "Below 5m SuperTrend";
    }
}
