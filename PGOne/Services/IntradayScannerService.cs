using PGOne.Models;

namespace PGOne.Services;

public interface IIntradayScannerService
{
    event Action? Updated;
    List<IntradayScanRow> Items { get; }
    bool IsScanning { get; }
    string? ProgressMessage { get; }
    Task ScanAsync();
    Task<string?> PlaceMisMarketOrderAsync(string exchange, string symbol, int quantity, string transactionType = "BUY");
}

public class IntradayScannerService : IIntradayScannerService
{
    private const decimal DefaultNotional = 5000m;

    private readonly IZerodhaService _zerodha;
    private readonly ISignalService _signal;

    public event Action? Updated;
    public List<IntradayScanRow> Items { get; private set; } = new();
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
        ProgressMessage = "Starting intraday framework scan...";
        Items = new List<IntradayScanRow>();
        Notify();

        try
        {
            var universe = NiftyConstituents.ScanUniverse;
            var instruments = universe.Select(s => $"NSE:{s}").ToArray();
            var quotes = await _zerodha.GetQuotesAsync(instruments);
            var satisfied = new List<IntradayScanRow>();
            var scanned = 0;

            foreach (var symbol in universe)
            {
                scanned++;
                ProgressMessage = $"Scanning {symbol} ({scanned}/{universe.Count})...";
                Notify();

                var instrument = $"NSE:{symbol}";
                var lastPrice = quotes.GetValueOrDefault(instrument, 0m);
                if (lastPrice <= 0)
                    continue;

                var analysis = await _signal.AnalyzeForFrameworkAsync(symbol);
                var isSatisfied = IntradayFrameworkEvaluator.IsSatisfied(analysis);
                if (!isSatisfied)
                    continue;

                var qty = IntradayFrameworkEvaluator.QuantityForNotional(lastPrice, DefaultNotional);
                satisfied.Add(new IntradayScanRow
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

            Items = satisfied
                .OrderByDescending(r => r.FrameworkScore)
                .ThenBy(r => r.Symbol)
                .ToList();

            ProgressMessage = Items.Count > 0
                ? $"Found {Items.Count} stocks matching intraday framework."
                : "No stocks matched the intraday framework right now.";
        }
        catch (Exception ex)
        {
            ProgressMessage = $"Scan failed: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
            Notify();
        }
    }

    public async Task<string?> PlaceMisMarketOrderAsync(
        string exchange,
        string symbol,
        int quantity,
        string transactionType = "BUY")
    {
        if (!_zerodha.IsConnected)
            return null;

        if (quantity <= 0)
            return null;

        return await _zerodha.PlaceOrderAsync(
            exchange,
            symbol,
            transactionType,
            quantity,
            "MARKET");
    }

    private void Notify() => Updated?.Invoke();
}
