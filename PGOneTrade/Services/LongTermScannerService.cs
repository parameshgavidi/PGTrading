using PGOneTrade.Models;
using PGOneTrade.Models.Trading;

namespace PGOneTrade.Services;

public interface ILongTermScannerService
{
    event Action? Updated;
    IReadOnlyList<StockScanRow> Items { get; }
    bool IsScanning { get; }
    string? ProgressMessage { get; }
    Task ScanAsync();
    Task<OrderPlacementResult> PlaceOrderAsync(StockScanRow row);
}

public class LongTermScannerService : ILongTermScannerService
{
    private const int QuoteBatchSize = 400;
    private const int MaxParallel = 3;

    private readonly IZerodhaService _zerodha;
    private readonly ILongTermFrameworkService _longTermFramework;
    private readonly IOrderExecutionService _orders;

    public event Action? Updated;
    public IReadOnlyList<StockScanRow> Items { get; private set; } = Array.Empty<StockScanRow>();
    public bool IsScanning { get; private set; }
    public string? ProgressMessage { get; private set; }

    public LongTermScannerService(
        IZerodhaService zerodha,
        ILongTermFrameworkService longTermFramework,
        IOrderExecutionService orders)
    {
        _zerodha = zerodha;
        _longTermFramework = longTermFramework;
        _orders = orders;
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
                    ProgressMessage = $"Long-term scan {symbol} ({scanned}/{universe.Count})...";
                    Notify();

                    var evaluation = await _longTermFramework.EvaluateAsync(symbol, lastPrice);
                    if (!evaluation.Satisfied)
                        return;

                    var qty = IntradayFrameworkEvaluator.QuantityForNotional(lastPrice, ScanNotional.LongTerm);
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
                            FrameworkStatus = evaluation.Status,
                            FrameworkScore = evaluation.Score
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
                ? $"Found {Items.Count} NSE stocks matching long-term framework."
                : "No NSE stocks matched the long-term framework right now.";
        }
        catch (Exception ex)
        {
            ProgressMessage = $"Long-term scan failed: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
            Notify();
        }
    }

    public async Task<OrderPlacementResult> PlaceOrderAsync(StockScanRow row)
    {
        var outcome = await _orders.PlaceAsync(new OrderIntent
        {
            Exchange = row.Exchange,
            TradingSymbol = row.Symbol,
            Side = OrderSides.Buy,
            Quantity = row.Quantity,
            UiProduct = ProductTypes.Cnc,
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
