using PgAiTrading.Models;
using PgAiTrading.Models.Trading;
using PgAiTrading.Models.Ui;

namespace PgAiTrading.Services;

public interface IAutoBuyService
{
    event Action? Updated;
    bool MasterAutomationEnabled { get; }
    IReadOnlyList<AutoBuyRow> Rows { get; }
    IReadOnlyList<AutoBuyFailedEntry> FailedEntries { get; }
    IReadOnlyList<string> NseSymbols { get; }
    bool IsLoadingSymbols { get; }
    bool IsMonitoring { get; }
    string? StatusMessage { get; }
    string StoragePath { get; }
    string? CurrentIpAddress { get; }
    bool IsRefreshingIp { get; }

    Task InitializeAsync();
    Task RefreshSymbolsAsync();
    Task RefreshIpAddressAsync();
    IReadOnlyList<string> SearchSymbols(string query, int limit = 20);
    Task AddSymbolAsync(string symbol);
    Task RemoveSymbolAsync(string symbol);
    Task UpdateRowAsync(AutoBuyRow row);
    Task SetRowAutomationAsync(string symbol, bool enabled);
    Task SetMasterAutomationAsync(bool enabled);
    Task ClearFailedEntriesAsync();
    Task SaveAsync();
    Task RefreshDeployedAmountsAsync();
    Task RefreshSettingsAsync();
    IReadOnlyList<AutoBuyReadiness.Check> GetReadinessChecks();
}

public class AutoBuyService : IAutoBuyService, IDisposable
{
    private const int MonitorIntervalSeconds = 20;
    private const int CandleFetchCount = 120;

    private readonly IZerodhaService _zerodha;
    private readonly IMarketDataService _marketData;
    private readonly ISuperTrendService _superTrend;
    private readonly ISettingsService _settings;
    private readonly IOrderExecutionService _orders;
    private readonly IAutoBuyStore _store;
    private readonly IUserContext _userContext;
    private readonly INseSymbolResolver _nseSymbolsResolver;
    private readonly IPublicIpAddressService _publicIp;

    private readonly List<AutoBuyRow> _rows = new();
    private readonly List<AutoBuyFailedEntry> _failedEntries = new();
    private List<string> _nseSymbols = new();
    private readonly HashSet<string> _orderedBarKeys = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>ST buy flips detected while orders could not place (e.g. market closed) — retry when ready.</summary>
    private readonly HashSet<string> _pendingBuyBarKeys = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _monitorCts;
    private readonly SemaphoreSlim _bootstrapLock = new(1, 1);
    private bool _bootstrapped;
    private bool _disposed;

    public event Action? Updated;

    public bool MasterAutomationEnabled { get; private set; }
    public IReadOnlyList<AutoBuyRow> Rows => _rows;
    public IReadOnlyList<AutoBuyFailedEntry> FailedEntries => _failedEntries;
    public IReadOnlyList<string> NseSymbols => _nseSymbols;
    public bool IsLoadingSymbols { get; private set; }
    public bool IsMonitoring => MasterAutomationEnabled && _monitorCts is not null;
    public string? StatusMessage { get; private set; }
    public string StoragePath { get; private set; } = LocalFileAutoBuyStore.JsonFileName;
    public string? CurrentIpAddress => _publicIp.CurrentIpAddress;
    public bool IsRefreshingIp { get; private set; }

    public AutoBuyService(
        IZerodhaService zerodha,
        IMarketDataService marketData,
        ISuperTrendService superTrend,
        ISettingsService settings,
        IOrderExecutionService orders,
        IAutoBuyStore store,
        IUserContext userContext,
        INseSymbolResolver nseSymbolsResolver,
        IPublicIpAddressService publicIp)
    {
        _zerodha = zerodha;
        _marketData = marketData;
        _superTrend = superTrend;
        _settings = settings;
        _orders = orders;
        _store = store;
        _userContext = userContext;
        _nseSymbolsResolver = nseSymbolsResolver;
        _publicIp = publicIp;
        _zerodha.ConnectionChanged += OnZerodhaConnectionChanged;
        _settings.SettingsChanged += OnSettingsChanged;
        _ = EnsureBootstrappedAsync();
    }

    private void OnSettingsChanged()
    {
        _ = RefreshSettingsAsync();
    }

    public async Task InitializeAsync()
    {
        await EnsureBootstrappedAsync();

        if (_zerodha.IsConnected)
            await RefreshDeployedAmountsAsync();

        EnsureMonitorRunning();
        Notify();
    }

    private async Task EnsureBootstrappedAsync()
    {
        if (_bootstrapped)
            return;

        await _bootstrapLock.WaitAsync();
        try
        {
            if (_bootstrapped)
                return;

            _settings.ReloadFromStorage();
            StoragePath = _store.DescribeLocation(_userContext.UserId);
            var trimmed = await LoadFromStoreAsync();
            if (trimmed)
                await SaveAsync();

            // Load NSE search universe even before Zerodha connects (public equity list).
            await RefreshSymbolsAsync();
            await RefreshIpAddressAsync();

            if (_zerodha.IsConnected)
                await RefreshDeployedAmountsAsync();

            _bootstrapped = true;
            EnsureMonitorRunning();
        }
        finally
        {
            _bootstrapLock.Release();
        }
    }

    public async Task RefreshIpAddressAsync()
    {
        IsRefreshingIp = true;
        Notify();
        try
        {
            await _publicIp.RefreshAsync();
        }
        finally
        {
            IsRefreshingIp = false;
            Notify();
        }
    }

    private void OnZerodhaConnectionChanged(bool connected)
    {
        if (!connected)
            return;

        _ = OnZerodhaConnectedAsync();
    }

    private async Task OnZerodhaConnectedAsync()
    {
        try
        {
            await EnsureBootstrappedAsync();
            await RefreshSymbolsAsync();
            await RefreshDeployedAmountsAsync();
            EnsureMonitorRunning();

            if (MasterAutomationEnabled)
                await EvaluateAllRowsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Auto Buy startup after connect failed: {ex.Message}";
            Notify();
        }
    }

    public async Task RefreshSymbolsAsync()
    {
        IsLoadingSymbols = true;
        Notify();

        try
        {
            await _nseSymbolsResolver.EnsureLoadedAsync();

            // Publish a searchable universe immediately (resolver + liquid fallback).
            _nseSymbols = NiftyConstituents.ScanUniverse
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();
            IsLoadingSymbols = false;
            Notify();

            // Enrich from Zerodha instruments when available (large download — don't block search).
            try
            {
                var fromZerodha = await _zerodha.GetNseEquitySymbolsAsync();
                _nseSymbols = fromZerodha
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.Trim().ToUpperInvariant())
                    .Concat(NiftyConstituents.ScanUniverse)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch
            {
                // Resolver + ScanUniverse already available for search.
            }
        }
        catch (Exception ex)
        {
            _nseSymbols = NiftyConstituents.ScanUniverse
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();
            StatusMessage = $"NSE list fallback loaded ({_nseSymbols.Count}): {ex.Message}";
        }
        finally
        {
            IsLoadingSymbols = false;
            Notify();
        }
    }

    public IReadOnlyList<string> SearchSymbols(string query, int limit = 20)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<string>();

        // Prefer resolver (symbol + company name). Fall back to cached tickers.
        var fromResolver = _nseSymbolsResolver.Search(query, limit);
        if (fromResolver.Count > 0)
            return fromResolver;

        var q = query.Trim().ToUpperInvariant();
        return _nseSymbols
            .Where(s => s.StartsWith(q, StringComparison.OrdinalIgnoreCase)
                        || s.Contains(q, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.StartsWith(q, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(s => s, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList();
    }

    public async Task AddSymbolAsync(string symbol)
    {
        var normalized = symbol.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(normalized))
            return;

        if (!_nseSymbolsResolver.ContainsSymbol(normalized)
            && !_nseSymbols.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            // Last chance: ensure list is loaded, then re-check.
            await _nseSymbolsResolver.EnsureLoadedAsync();
            if (!_nseSymbolsResolver.ContainsSymbol(normalized)
                && !_nseSymbols.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                StatusMessage = $"{normalized} is not in the NSE equity list. Click Refresh NSE list and try again.";
                Notify();
                return;
            }
        }

        if (_rows.Count >= AutoBuyDefaults.MaxSymbols)
        {
            StatusMessage = $"Auto Buy list limit reached ({AutoBuyDefaults.MaxSymbols} symbols) — remove a stock to add another.";
            Notify();
            return;
        }

        if (_rows.Any(r => r.Symbol.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
        {
            StatusMessage = $"{normalized} is already in the Auto Buy list.";
            Notify();
            return;
        }

        _rows.Add(new AutoBuyRow
        {
            Symbol = normalized,
            Exchange = "NSE",
            Timeframe = "5m",
            Lots = 1,
            AutomationEnabled = true,
            Status = "Idle"
        });

        await SaveAsync();
        StatusMessage = $"Added {normalized} to Auto Buy list.";
        Notify();
    }

    public async Task RemoveSymbolAsync(string symbol)
    {
        var removed = _rows.RemoveAll(r =>
            r.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));

        if (removed == 0)
            return;

        await SaveAsync();
        StatusMessage = $"Removed {symbol.ToUpperInvariant()} from Auto Buy list.";
        Notify();
    }

    public async Task UpdateRowAsync(AutoBuyRow row)
    {
        var existing = _rows.FirstOrDefault(r =>
            r.Symbol.Equals(row.Symbol, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
            return;

        if (_zerodha.IsConnected)
            await RefreshDeployedAmountsAsync();

        existing.Exchange = "NSE";
        existing.Timeframe = AutoBuyTimeframes.Normalize(row.Timeframe);
        existing.Lots = Math.Max(1, row.Lots);
        existing.MaxDeployAmount = Math.Max(0, row.MaxDeployAmount);

        if (existing.MaxDeployAmount > 0
            && _zerodha.IsConnected
            && AutoBuyDeployHelper.IsMaxDeployReached(existing.DeployedAmount, existing.MaxDeployAmount))
            existing.AutomationEnabled = false;
        else
            existing.AutomationEnabled = row.AutomationEnabled;

        await SaveAsync();
        StatusMessage = $"Updated {existing.Symbol}.";
        Notify();
    }

    public async Task SetRowAutomationAsync(string symbol, bool enabled)
    {
        var row = _rows.FirstOrDefault(r =>
            r.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));

        if (row is null || row.AutomationEnabled == enabled)
            return;

        if (enabled && row.MaxDeployAmount > 0)
        {
            if (_zerodha.IsConnected)
                await RefreshDeployedAmountsAsync();

            if (AutoBuyDeployHelper.IsMaxDeployReached(row.DeployedAmount, row.MaxDeployAmount))
            {
                StatusMessage = $"Cannot enable {row.Symbol} — max deploy ₹{row.MaxDeployAmount:N0} already reached.";
                Notify();
                return;
            }
        }

        row.AutomationEnabled = enabled;
        await SaveAsync();
        Notify();
    }

    public async Task SetMasterAutomationAsync(bool enabled)
    {
        if (MasterAutomationEnabled == enabled)
            return;

        if (enabled && !_settings.Settings.AutoTradingEnabled)
            StatusMessage = "Warning: Settings → Auto Trading is off — flips will not place orders until enabled.";

        MasterAutomationEnabled = enabled;
        _monitorCts?.Cancel();
        _monitorCts?.Dispose();
        _monitorCts = null;

        await SaveAsync();

        if (enabled)
        {
            _orderedBarKeys.Clear();
            StatusMessage = "Auto Buy master automation enabled — monitoring ST(7,2.5) flips.";
            StartMonitorLoop();
        }
        else
        {
            StatusMessage = "Auto Buy master automation disabled.";
        }

        Notify();
    }

    public async Task ClearFailedEntriesAsync()
    {
        if (_failedEntries.Count == 0)
            return;

        _failedEntries.Clear();
        await SaveAsync();
        StatusMessage = "Cleared Auto Buy failed entry list.";
        Notify();
    }

    public async Task SaveAsync()
    {
        var document = AutoBuyDocument.FromRuntime(MasterAutomationEnabled, _rows, _failedEntries);
        await _store.SaveAsync(_userContext.UserId, document);
    }

    public async Task RefreshDeployedAmountsAsync()
    {
        if (!_zerodha.IsConnected)
            return;

        var holdings = await _zerodha.GetHoldingsAsync();
        var cncPositions = await _zerodha.GetPositionsAsync(AutoBuyDefaults.Product);

        foreach (var row in _rows)
            row.DeployedAmount = AutoBuyDeployHelper.GetDeployedAmount(
                row.Symbol,
                holdings,
                cncPositions);

        Notify();
    }

    private async Task<bool> LoadFromStoreAsync()
    {
        var document = await _store.LoadAsync(_userContext.UserId);
        MasterAutomationEnabled = document?.MasterAutomationEnabled ?? false;
        _rows.Clear();
        _failedEntries.Clear();

        var runtime = document?.ToRuntime();
        var rows = runtime?.Rows ?? new List<AutoBuyRow>();
        var failed = runtime?.FailedEntries ?? new List<AutoBuyFailedEntry>();
        var trimmed = rows.Count > AutoBuyDefaults.MaxSymbols;

        foreach (var row in rows.Take(AutoBuyDefaults.MaxSymbols))
        {
            row.Exchange = "NSE";
            row.Timeframe = AutoBuyTimeframes.Normalize(row.Timeframe);
            row.Lots = Math.Max(1, row.Lots);
            row.MaxDeployAmount = Math.Max(0, row.MaxDeployAmount);
            _rows.Add(row);
        }

        foreach (var entry in failed.Take(AutoBuyDefaults.MaxFailedEntries))
            _failedEntries.Add(AutoBuyFailedEntry.Clone(entry));

        if (trimmed)
            StatusMessage = $"List had more than {AutoBuyDefaults.MaxSymbols} symbols — loaded first {AutoBuyDefaults.MaxSymbols}.";

        return trimmed;
    }

    private async Task RecordFailedEntryAsync(AutoBuyRow row, string status, string? detail, int quantity)
    {
        // Keep IP fresh enough for Zerodha whitelist diagnostics when an entry fails.
        if (string.IsNullOrWhiteSpace(_publicIp.CurrentIpAddress))
        {
            try
            {
                await _publicIp.RefreshAsync();
            }
            catch
            {
                // Best-effort — still record the failure without IP.
            }
        }

        _failedEntries.Insert(0, new AutoBuyFailedEntry
        {
            Symbol = row.Symbol,
            Exchange = row.Exchange,
            Timeframe = row.Timeframe,
            Quantity = Math.Max(0, quantity),
            Status = status,
            Detail = detail,
            IpAddress = _publicIp.CurrentIpAddress,
            FailedAt = DateTime.Now
        });

        while (_failedEntries.Count > AutoBuyDefaults.MaxFailedEntries)
            _failedEntries.RemoveAt(_failedEntries.Count - 1);

        await SaveAsync();
    }

    public async Task RefreshSettingsAsync()
    {
        _settings.ReloadFromStorage();
        Notify();
    }

    public IReadOnlyList<AutoBuyReadiness.Check> GetReadinessChecks()
    {
        _settings.ReloadFromStorage();
        return AutoBuyReadiness.Evaluate(
            MasterAutomationEnabled,
            _rows,
            _zerodha.IsConnected,
            _settings.Settings.AutoTradingEnabled,
            MarketHours.IsOpen());
    }

    private void StartMonitorLoop()
    {
        _monitorCts?.Cancel();
        _monitorCts?.Dispose();
        _monitorCts = new CancellationTokenSource();
        _ = MonitorLoopAsync(_monitorCts.Token);
    }

    private void EnsureMonitorRunning()
    {
        if (!MasterAutomationEnabled)
            return;

        if (_monitorCts is not null && !_monitorCts.IsCancellationRequested)
            return;

        StartMonitorLoop();
    }

    private async Task MonitorLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await EvaluateAllRowsAsync();
                await Task.Delay(TimeSpan.FromSeconds(MonitorIntervalSeconds), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Monitoring stopped.
        }
    }

    private async Task EvaluateAllRowsAsync()
    {
        if (!MasterAutomationEnabled)
            return;

        _settings.ReloadFromStorage();

        if (!_zerodha.IsConnected)
        {
            StatusMessage = "Connect to Zerodha to run Auto Buy automation.";
            Notify();
            return;
        }

        var holdings = await _zerodha.GetHoldingsAsync();
        var cncPositions = await _zerodha.GetPositionsAsync(AutoBuyDefaults.Product);

        foreach (var row in _rows)
        {
            row.DeployedAmount = AutoBuyDeployHelper.GetDeployedAmount(
                row.Symbol,
                holdings,
                cncPositions);

            if (await TryDisableAutomationForMaxAsync(row))
                continue;

            if (!row.AutomationEnabled)
            {
                row.Status = "Disabled";
                row.Detail = row.MaxDeployAmount > 0 && AutoBuyDeployHelper.IsMaxDeployReached(row.DeployedAmount, row.MaxDeployAmount)
                    ? $"Max deploy reached (₹{row.DeployedAmount:N0} / ₹{row.MaxDeployAmount:N0})"
                    : "Row automation off";
                continue;
            }

            await EvaluateRowAsync(row, holdings, cncPositions);
        }

        LastRefreshMessage();
        Notify();
    }

    private async Task<bool> TryDisableAutomationForMaxAsync(AutoBuyRow row)
    {
        if (!AutoBuyDeployHelper.IsMaxDeployReached(row.DeployedAmount, row.MaxDeployAmount))
            return false;

        row.Status = "Max reached";
        row.Detail = $"Deployed ₹{row.DeployedAmount:N0} / max ₹{row.MaxDeployAmount:N0} — automation disabled";

        if (!row.AutomationEnabled)
            return true;

        row.AutomationEnabled = false;
        await SaveAsync();
        return true;
    }

    private async Task EvaluateRowAsync(
        AutoBuyRow row,
        IReadOnlyList<Holding> holdings,
        IReadOnlyList<Position> cncPositions)
    {
        try
        {
            row.DeployedAmount = AutoBuyDeployHelper.GetDeployedAmount(
                row.Symbol,
                holdings,
                cncPositions);

            if (await TryDisableAutomationForMaxAsync(row))
                return;

            var instrument = InstrumentMapper.ToZerodhaKey(row.Symbol, row.Exchange);
            var candles = await _marketData.GetCandlesAsync(
                instrument,
                row.Timeframe,
                CandleFetchCount);

            if (candles.Count < TrailingStopDefaults.Period + 4)
            {
                row.Status = "No data";
                row.Detail = $"Need more {row.Timeframe} candles for ST(7,2.5)";
                return;
            }

            var stPeriod = TrailingStopDefaults.Period;
            var stMult = TrailingStopDefaults.Multiplier;
            var getTrend = _superTrend.GetTrend;
            var istNow = MarketHours.GetIstNow();

            // Only the last completed bar: Sell→Buy cross (e.g. 5m candle close).
            var lastBarTime = SuperTrendFlipHelper.GetLastClosedBarTime(candles, row.Timeframe, istNow);
            if (!lastBarTime.HasValue)
            {
                row.Status = "No data";
                row.Detail = "No closed candle for ST(7,2.5)";
                return;
            }

            var barKey = BuildBarKey(row.Symbol, row.Timeframe, lastBarTime.Value);
            var stBuyTrigger = SuperTrendFlipHelper.IsBuyTriggerOnLastClosedBar(
                candles, stPeriod, stMult, getTrend, row.Timeframe, istNow);

            // Drop pending from older bars once a new candle has closed.
            ClearStalePendingBuys(row.Symbol, row.Timeframe, barKey);

            if (_orderedBarKeys.Contains(barKey))
            {
                _pendingBuyBarKeys.Remove(barKey);
                row.Status = "Ordered";
                row.Detail = "BUY already sent for this ST(7,2.5) cross — wait for next Sell→Buy";
                return;
            }

            if (!stBuyTrigger)
            {
                _pendingBuyBarKeys.Remove(barKey);

                var stNow = SuperTrendFlipHelper.GetTrendOnLastClosedBar(
                    candles, stPeriod, stMult, getTrend, row.Timeframe, istNow);

                var deployNote = row.MaxDeployAmount > 0
                    ? $" · deployed ₹{row.DeployedAmount:N0} / ₹{row.MaxDeployAmount:N0}"
                    : string.Empty;

                row.Status = "Waiting";
                row.Detail = stNow == TrendDirection.Buy
                    ? $"{row.Timeframe} ST(7,2.5) already Buy — waiting for next Sell→Buy cross{deployNote}"
                    : $"{row.Timeframe} ST(7,2.5) is {TrendUi.GetBiasLabel(stNow)} — waiting for Buy cross{deployNote}";
                return;
            }

            // Fresh cross on this closed bar — queue until we can place (once only).
            _pendingBuyBarKeys.Add(barKey);

            var quantity = Math.Max(1, row.Lots);
            var limitPrice = await _zerodha.GetLtpAsync(instrument);
            if (limitPrice <= 0)
                limitPrice = SuperTrendFlipHelper.GetLastClosedBarClose(candles, row.Timeframe, istNow);

            if (!AutoBuyReadiness.CanPlaceOrder(
                    row,
                    _zerodha.IsConnected,
                    _settings.Settings.AutoTradingEnabled,
                    MarketHours.IsOpen(istNow),
                    quantity,
                    limitPrice))
            {
                if (!MarketHours.IsOpen(istNow))
                {
                    row.Status = "Buy signal (market closed)";
                    row.Detail = "ST(7,2.5) crossed Buy on last closed bar — will place once when market opens";
                    row.LastTriggeredAt = DateTime.Now;
                }
                else if (!_settings.Settings.AutoTradingEnabled)
                {
                    row.Status = "Buy signal";
                    row.Detail = "ST(7,2.5) crossed Buy — enable Auto Trading in Settings to place orders";
                    row.LastTriggeredAt = DateTime.Now;
                }
                else if (AutoBuyDeployHelper.WouldExceedMax(
                    row.DeployedAmount, row.MaxDeployAmount, quantity * limitPrice))
                {
                    row.Status = "Max reached";
                    row.Detail = $"Order ₹{quantity * limitPrice:N0} exceeds max ₹{row.MaxDeployAmount:N0}";
                    _orderedBarKeys.Add(barKey);
                    _pendingBuyBarKeys.Remove(barKey);
                    await TryDisableAutomationForMaxAsync(row);
                }
                else if (AutoBuyDeployHelper.IsMaxDeployReached(row.DeployedAmount, row.MaxDeployAmount))
                {
                    await TryDisableAutomationForMaxAsync(row);
                }
                else if (limitPrice <= 0)
                {
                    row.Status = "Buy signal";
                    row.Detail = "Could not fetch price for limit order";
                }

                return;
            }

            // Claim this bar BEFORE placing so the 20s monitor loop cannot fire again
            // while this cross is still the last closed bar (~until next 5m close).
            _orderedBarKeys.Add(barKey);
            _pendingBuyBarKeys.Remove(barKey);

            var outcome = await _orders.PlaceAsync(new OrderIntent
            {
                Exchange = row.Exchange,
                TradingSymbol = row.Symbol,
                Side = AutoBuyOrderPolicy.EntrySideOrThrow(AutoBuyDefaults.EntrySide),
                Quantity = quantity,
                UiProduct = AutoBuyDefaults.Product,
                // Raw LTP — same as pre-refactor Auto Buy (no tick rounding).
                Pricing = LimitPricingMode.RawLtp,
                HintPrice = limitPrice > 0 ? limitPrice : null
            });

            row.LastTriggeredAt = DateTime.Now;

            if (outcome.Success)
            {
                row.Status = "Order placed";
                row.Detail = $"BUY {quantity} CNC @ LIMIT {limitPrice:N2} — ST(7,2.5) cross · order {outcome.OrderId}";
                StatusMessage = $"Auto Buy: order placed for {row.Symbol}";

                row.DeployedAmount = AutoBuyDeployHelper.GetDeployedAmount(
                    row.Symbol,
                    holdings,
                    cncPositions);
                row.DeployedAmount += quantity * limitPrice;
                await TryDisableAutomationForMaxAsync(row);
            }
            else
            {
                // Match prior Zerodha OrderPlacementResult null-fallback wording.
                var error = outcome.Message == BrokerUiMessages.OrderPlacementFailed
                    ? BrokerUiMessages.OrderRejected
                    : outcome.Message;
                row.Status = "Order failed";
                row.Detail = $"{error} — will not retry this bar (wait for next Sell→Buy cross)";
                StatusMessage = $"Auto Buy: failed for {row.Symbol} — {row.Detail}";
                await RecordFailedEntryAsync(row, row.Status, row.Detail, quantity);
            }
        }
        catch (Exception ex)
        {
            row.Status = "Error";
            row.Detail = ex.Message;
            StatusMessage = $"Auto Buy: error for {row.Symbol} — {ex.Message}";
            await RecordFailedEntryAsync(row, row.Status, row.Detail, row.Lots);
        }
    }

    /// <summary>Stable bar id — date+time only so Kind/offset differences never re-fire the same candle.</summary>
    private static string BuildBarKey(string symbol, string timeframe, DateTime barTime)
    {
        var t = DateTime.SpecifyKind(barTime, DateTimeKind.Unspecified);
        return $"{symbol.ToUpperInvariant()}|{timeframe}|{t:yyyy-MM-ddTHH:mm:ss}";
    }

    private void ClearStalePendingBuys(string symbol, string timeframe, string currentBarKey)
    {
        var prefix = $"{symbol.ToUpperInvariant()}|{timeframe}|";
        _pendingBuyBarKeys.RemoveWhere(k =>
            k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(k, currentBarKey, StringComparison.OrdinalIgnoreCase));
    }

    private void LastRefreshMessage()
    {
        if (string.IsNullOrEmpty(StatusMessage) || StatusMessage.StartsWith("Auto Buy:", StringComparison.Ordinal))
            StatusMessage = $"Monitoring {_rows.Count(r => r.AutomationEnabled)} symbol(s) — BUY when ST(7,2.5) turns Buy.";
    }

    private void Notify() => Updated?.Invoke();

    public void Dispose()
    {
        if (_disposed)
            return;

        _zerodha.ConnectionChanged -= OnZerodhaConnectionChanged;
        _settings.SettingsChanged -= OnSettingsChanged;
        _monitorCts?.Cancel();
        _monitorCts?.Dispose();
        _disposed = true;
    }
}
