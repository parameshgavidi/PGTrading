using PGOne.Models;
using PGOne.Models.Trading;

namespace PGOne.Services;

public interface IIntradayScannerService
{
    event Action? Updated;
    IReadOnlyList<StockScanRow> Items { get; }
    bool IsScanning { get; }
    string? ProgressMessage { get; }
    Task ScanAsync();
    Task<OrderPlacementResult> PlaceOrderAsync(StockScanRow row);
}

public class IntradayScannerService : IIntradayScannerService
{
    private const int QuoteBatchSize = 400;
    private const int Phase1Parallel = 15;
    private const int Phase2Parallel = 10;

    private readonly IZerodhaService _zerodha;
    private readonly ISignalService _signal;
    private readonly INiftyIndexService _niftyIndex;
    private readonly IOrderExecutionService _orders;

    public event Action? Updated;
    public IReadOnlyList<StockScanRow> Items { get; private set; } = Array.Empty<StockScanRow>();
    public bool IsScanning { get; private set; }
    public string? ProgressMessage { get; private set; }

    public IntradayScannerService(
        IZerodhaService zerodha,
        ISignalService signal,
        INiftyIndexService niftyIndex,
        IOrderExecutionService orders)
    {
        _zerodha = zerodha;
        _signal = signal;
        _niftyIndex = niftyIndex;
        _orders = orders;
    }

    public async Task ScanAsync()
    {
        IsScanning = true;
        ProgressMessage = "Loading Nifty 50 symbols...";
        Items = Array.Empty<StockScanRow>();
        Notify();

        try
        {
            var universe = (await _niftyIndex.GetNifty50SymbolsAsync()).ToList();
            if (universe.Count == 0)
            {
                ProgressMessage = "Could not load Nifty 50 symbol list.";
                return;
            }

            var tradable = universe
                .Where(s => s.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            ProgressMessage = $"Fetching live quotes for {tradable.Count} Nifty 50 stocks...";
            Notify();

            var quotes = await FetchQuotesBatchedAsync(tradable);
            var withPrice = tradable
                .Where(s => quotes.GetValueOrDefault($"NSE:{s}", 0m) > 0)
                .ToList();

            ProgressMessage = $"Phase 1: screening {withPrice.Count} stocks (1H ST + VWAP + RSI filters)...";
            Notify();

            var candidates = await RunPhase1ScreenAsync(withPrice);

            if (candidates.Count == 0)
            {
                Items = Array.Empty<StockScanRow>();
                ProgressMessage = "No Nifty 500 stocks passed Step 1 (1H SuperTrend + VWAP bullish).";
                return;
            }

            ProgressMessage = $"Phase 2: full framework on {candidates.Count} candidates (15m/5m/footprint)...";
            Notify();

            var satisfied = await RunPhase2FrameworkAsync(candidates, quotes);

            Items = satisfied
                .OrderByDescending(r => r.FrameworkScore)
                .ThenBy(r => r.Symbol)
                .ToList();

            ProgressMessage = Items.Count > 0
                ? $"Found {Items.Count} Nifty 50 stocks matching full intraday framework (screened {withPrice.Count} → {candidates.Count} → {Items.Count})."
                : $"No matches after full framework ({candidates.Count} passed Step 1).";
        }
        catch (Exception ex)
        {
            ProgressMessage = $"Intraday scan failed: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
            Notify();
        }
    }

    private async Task<List<IntradayPrefetch>> RunPhase1ScreenAsync(IReadOnlyList<string> symbols)
    {
        var candidates = new List<IntradayPrefetch>();
        var screened = 0;
        using var gate = new SemaphoreSlim(Phase1Parallel);

        var tasks = symbols.Select(async symbol =>
        {
            await gate.WaitAsync();
            try
            {
                var prefetch = await _signal.TryScreenIntradayPhase1Async(symbol);
                Interlocked.Increment(ref screened);
                if (screened % 25 == 0 || screened == symbols.Count)
                {
                    ProgressMessage = $"Phase 1: screened {screened}/{symbols.Count} — {candidates.Count} bullish candidates so far...";
                    Notify();
                }

                if (prefetch is not null)
                {
                    lock (candidates)
                        candidates.Add(prefetch);
                }
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks);
        return candidates;
    }

    private async Task<List<StockScanRow>> RunPhase2FrameworkAsync(
        IReadOnlyList<IntradayPrefetch> candidates,
        Dictionary<string, decimal> quotes)
    {
        var satisfied = new List<StockScanRow>();
        var completed = 0;
        using var gate = new SemaphoreSlim(Phase2Parallel);

        var tasks = candidates.Select(async prefetch =>
        {
            await gate.WaitAsync();
            try
            {
                var symbol = prefetch.Symbol;
                var lastPrice = quotes.GetValueOrDefault($"NSE:{symbol}", 0m);
                if (lastPrice <= 0)
                    return;

                var analysis = await _signal.AnalyzeForFrameworkAsync(symbol, prefetch);

                Interlocked.Increment(ref completed);
                ProgressMessage = $"Phase 2: {completed}/{candidates.Count} — checking full framework...";
                Notify();

                if (!IntradayFrameworkEvaluator.IsSatisfied(analysis)
                    || analysis.TradeDirection != TrendDirection.Buy)
                    return;

                var qty = IntradayFrameworkEvaluator.QuantityForNotional(lastPrice, ScanNotional.Intraday);
                lock (satisfied)
                {
                    satisfied.Add(new StockScanRow
                    {
                        Symbol = symbol,
                        Exchange = "NSE",
                        LastPrice = lastPrice,
                        Quantity = qty,
                        OrderValue = qty * lastPrice,
                        FrameworkSatisfied = true,
                        FrameworkStatus = IntradayFrameworkEvaluator.GetStatus(analysis, true),
                        FrameworkScore = analysis.OverallScore
                    });
                }
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks);
        return satisfied;
    }

    public async Task<OrderPlacementResult> PlaceOrderAsync(StockScanRow row)
    {
        var outcome = await _orders.PlaceAsync(new OrderIntent
        {
            Exchange = row.Exchange,
            TradingSymbol = row.Symbol,
            Side = OrderSides.Buy,
            Quantity = row.Quantity,
            UiProduct = ProductTypes.Mis,
            // Raw LTP — same as pre-refactor (no tick rounding).
            Pricing = LimitPricingMode.RawLtp,
            HintPrice = row.LastPrice > 0 ? row.LastPrice : null
        });

        return outcome.Success
            ? OrderPlacementResult.Ok(outcome.OrderId!)
            : OrderPlacementResult.Fail(outcome.Message);
    }

    private async Task<Dictionary<string, decimal>> FetchQuotesBatchedAsync(IReadOnlyList<string> symbols)
    {
        var quotes = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < symbols.Count; i += QuoteBatchSize)
        {
            var batch = symbols.Skip(i).Take(QuoteBatchSize).Select(s => $"NSE:{s}").ToArray();
            var batchQuotes = await _zerodha.GetQuotesAsync(batch);
            foreach (var (key, value) in batchQuotes)
                quotes[key] = value;
        }

        return quotes;
    }

    private void Notify() => Updated?.Invoke();
}
