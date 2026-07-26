using PGOne.Models;

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
    private const int MaxParallel = 4;

    private readonly IZerodhaService _zerodha;
    private readonly ISignalService _signal;

    public event Action? Updated;
    public IReadOnlyList<StockScanRow> Items { get; private set; } = Array.Empty<StockScanRow>();
    public bool IsScanning { get; private set; }
    public string? ProgressMessage { get; private set; }

    public IntradayScannerService(IZerodhaService zerodha, ISignalService signal)
    {
        _zerodha = zerodha;
        _signal = signal;
    }

    public async Task ScanAsync()
    {
        IsScanning = true;
        ProgressMessage = "Loading NSE equity symbols...";
        Items = Array.Empty<StockScanRow>();
        Notify();

        try
        {
            var universe = (await _zerodha.GetNseEquitySymbolsAsync()).ToList();
            var quotes = await FetchQuotesBatchedAsync(universe);
            var satisfied = new List<StockScanRow>();
            var scanned = 0;
            using var gate = new SemaphoreSlim(MaxParallel);

            var tasks = universe.Select(async symbol =>
            {
                await gate.WaitAsync();
                try
                {
                    var lastPrice = quotes.GetValueOrDefault($"NSE:{symbol}", 0m);
                    if (lastPrice <= 0)
                        return;

                    Interlocked.Increment(ref scanned);
                    ProgressMessage = $"Intraday scan {symbol} ({scanned}/{universe.Count})...";
                    Notify();

                    var analysis = await _signal.AnalyzeForFrameworkAsync(symbol);
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

            Items = satisfied
                .OrderByDescending(r => r.FrameworkScore)
                .ThenBy(r => r.Symbol)
                .ToList();

            ProgressMessage = Items.Count > 0
                ? $"Found {Items.Count} NSE stocks matching intraday framework."
                : "No NSE stocks matched the intraday framework right now.";
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

    public async Task<OrderPlacementResult> PlaceOrderAsync(StockScanRow row)
    {
        var limitPrice = row.LastPrice;
        if (limitPrice <= 0)
            limitPrice = await _zerodha.GetLtpAsync($"{row.Exchange}:{row.Symbol}");

        if (limitPrice <= 0)
            return OrderPlacementResult.Fail("Could not fetch price for limit order.");

        return await _zerodha.PlaceOrderAsync(
            row.Exchange, row.Symbol, "BUY", row.Quantity, "LIMIT", limitPrice, "MIS");
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
