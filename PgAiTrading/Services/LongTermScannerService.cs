using PgAiTrading.Models;
using PgAiTrading.Models.Trading;

namespace PgAiTrading.Services;

public interface ILongTermScannerService
{
    event Action? Updated;
    IReadOnlyList<StockScanRow> Items { get; }
    bool IsScanning { get; }
    bool HasCachedResults { get; }
    DateTime? LastScannedAtUtc { get; }
    string? ProgressMessage { get; }

    /// <summary>Load last JSON snapshot instantly (no network).</summary>
    Task EnsureLoadedAsync();

    /// <summary>Start a background scan; returns immediately. Keeps prior results visible until done.</summary>
    Task StartScanAsync();

    /// <summary>Run scan to completion (tests / callers that need to await).</summary>
    Task ScanAsync();

    Task<OrderPlacementResult> PlaceOrderAsync(StockScanRow row);
}

public class LongTermScannerService : ILongTermScannerService
{
    private const int QuoteBatchSize = 400;
    private const int MaxParallel = 8;
    private const int ProgressEvery = 40;

    private readonly IZerodhaService _zerodha;
    private readonly ILongTermFrameworkService _longTermFramework;
    private readonly IFundamentalDataService _fundamentals;
    private readonly IOrderExecutionService _orders;
    private readonly ILongTermScanStore _store;
    private readonly IUserContext _user;

    private readonly object _gate = new();
    private bool _cacheLoaded;
    private Task? _runningScan;
    private DateTime _lastProgressNotifyUtc = DateTime.MinValue;

    public event Action? Updated;
    public IReadOnlyList<StockScanRow> Items { get; private set; } = Array.Empty<StockScanRow>();
    public bool IsScanning { get; private set; }
    public bool HasCachedResults => Items.Count > 0 || LastScannedAtUtc.HasValue;
    public DateTime? LastScannedAtUtc { get; private set; }
    public string? ProgressMessage { get; private set; }

    public LongTermScannerService(
        IZerodhaService zerodha,
        ILongTermFrameworkService longTermFramework,
        IFundamentalDataService fundamentals,
        IOrderExecutionService orders,
        ILongTermScanStore store,
        IUserContext user)
    {
        _zerodha = zerodha;
        _longTermFramework = longTermFramework;
        _fundamentals = fundamentals;
        _orders = orders;
        _store = store;
        _user = user;
    }

    public async Task EnsureLoadedAsync()
    {
        if (_cacheLoaded)
            return;

        try
        {
            var doc = await _store.LoadAsync(_user.UserId);
            if (doc is not null)
            {
                Items = doc.Items
                    .OrderByDescending(r => r.FrameworkScore)
                    .ThenBy(r => r.Symbol)
                    .ToList();
                LastScannedAtUtc = doc.ScannedAtUtc;
                ProgressMessage = BuildCacheMessage(doc);
            }
        }
        catch
        {
            // Non-fatal — empty table until a scan runs.
        }
        finally
        {
            _cacheLoaded = true;
            Notify();
        }
    }

    public Task StartScanAsync()
    {
        _ = GetOrStartScanTask();
        return Task.CompletedTask;
    }

    public async Task ScanAsync()
    {
        await GetOrStartScanTask();
    }

    private Task GetOrStartScanTask()
    {
        lock (_gate)
        {
            if (_runningScan is { IsCompleted: false })
                return _runningScan;

            _runningScan = RunScanCoreAsync();
            return _runningScan;
        }
    }

    private async Task RunScanCoreAsync()
    {
        IsScanning = true;
        // Keep prior Items visible while refreshing — do not clear the table.
        ProgressMessage = Items.Count > 0
            ? "Refreshing long-term scan in background (showing last results)..."
            : "Loading NSE equity symbols...";
        Notify();

        try
        {
            var universe = (await _zerodha.GetNseEquitySymbolsAsync()).ToList();

            // Only symbols with fundamental coverage can ever pass — skip the rest before quotes/candles.
            var candidates = universe
                .Where(_fundamentals.HasFundamentals)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s)
                .ToList();

            if (candidates.Count == 0)
                candidates = universe;

            ProgressMessage = $"Fetching quotes for {candidates.Count} candidates (of {universe.Count} NSE)..."
                + (Items.Count > 0 ? " — prior results still shown." : string.Empty);
            Notify();

            var quotes = await FetchQuotesBatchedAsync(candidates);

            var satisfied = new List<StockScanRow>();
            var scanned = 0;
            using var parallelGate = new SemaphoreSlim(MaxParallel);

            var tasks = candidates.Select(async symbol =>
            {
                await parallelGate.WaitAsync();
                try
                {
                    var lastPrice = quotes.GetValueOrDefault($"NSE:{symbol}", 0m);
                    if (lastPrice <= 0)
                        return;

                    var n = Interlocked.Increment(ref scanned);
                    MaybeReportProgress(symbol, n, candidates.Count, universe.Count);

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
                    parallelGate.Release();
                }
            });

            await Task.WhenAll(tasks);

            var ordered = satisfied
                .OrderByDescending(r => r.FrameworkScore)
                .ThenBy(r => r.Symbol)
                .ToList();

            var scannedAt = DateTime.UtcNow;
            var status = ordered.Count > 0
                ? $"Found {ordered.Count} match(es) — scanned {scanned}/{candidates.Count} candidates ({universe.Count} NSE)."
                : $"No matches — scanned {scanned}/{candidates.Count} candidates ({universe.Count} NSE).";

            Items = ordered;
            LastScannedAtUtc = scannedAt;
            ProgressMessage = status;

            await PersistAsync(ordered, scannedAt, universe.Count, scanned, status);
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
            Pricing = LimitPricingMode.RawLtp,
            HintPrice = row.LastPrice > 0 ? row.LastPrice : null
        });

        return outcome.Success
            ? OrderPlacementResult.Ok(outcome.OrderId!)
            : OrderPlacementResult.Fail(outcome.Message);
    }

    private async Task PersistAsync(
        IReadOnlyList<StockScanRow> items,
        DateTime scannedAtUtc,
        int universeCount,
        int evaluatedCount,
        string statusMessage)
    {
        try
        {
            var doc = LongTermScanDocument.FromResults(
                items, scannedAtUtc, universeCount, evaluatedCount, statusMessage);
            await _store.SaveAsync(_user.UserId, doc);
        }
        catch
        {
            // Results still available in memory this session.
        }
    }

    private void MaybeReportProgress(string symbol, int scanned, int candidateCount, int universeCount)
    {
        var now = DateTime.UtcNow;
        if (scanned % ProgressEvery != 0 && scanned != candidateCount
            && (now - _lastProgressNotifyUtc).TotalMilliseconds < 400)
            return;

        _lastProgressNotifyUtc = now;
        ProgressMessage =
            $"Long-term scan {symbol} ({scanned}/{candidateCount} candidates, {universeCount} NSE)..."
            + (Items.Count > 0 ? " — prior results still shown." : string.Empty);
        Notify();
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

    private static string BuildCacheMessage(LongTermScanDocument doc)
    {
        var when = doc.ScannedAtUtc.HasValue
            ? doc.ScannedAtUtc.Value.ToLocalTime().ToString("dd MMM yyyy HH:mm")
            : "previous session";

        if (doc.Items.Count > 0)
            return $"Showing last scan ({when}) — {doc.Items.Count} match(es). Tap Rescan to refresh in background.";

        return doc.StatusMessage
            ?? $"Last scan ({when}) found no matches. Tap Rescan to refresh in background.";
    }

    private void Notify() => Updated?.Invoke();
}
