using Microsoft.Maui.Storage;
using PGOne.Models;

namespace PGOne.Services;

public interface IAutoBuyService
{
    event Action? Updated;
    bool MasterAutomationEnabled { get; }
    IReadOnlyList<AutoBuyRow> Rows { get; }
    IReadOnlyList<string> NseSymbols { get; }
    bool IsLoadingSymbols { get; }
    bool IsMonitoring { get; }
    string? StatusMessage { get; }
    string CsvPath { get; }

    Task InitializeAsync();
    Task RefreshSymbolsAsync();
    IReadOnlyList<string> SearchSymbols(string query, int limit = 20);
    Task AddSymbolAsync(string symbol);
    Task RemoveSymbolAsync(string symbol);
    Task UpdateRowAsync(AutoBuyRow row);
    Task SetRowAutomationAsync(string symbol, bool enabled);
    Task SetMasterAutomationAsync(bool enabled);
    Task SaveAsync();
    Task RefreshDeployedAmountsAsync();
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

    private readonly List<AutoBuyRow> _rows = new();
    private List<string> _nseSymbols = new();
    private readonly HashSet<string> _orderedBarKeys = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _monitorCts;
    private bool _disposed;

    public event Action? Updated;

    public bool MasterAutomationEnabled { get; private set; }
    public IReadOnlyList<AutoBuyRow> Rows => _rows;
    public IReadOnlyList<string> NseSymbols => _nseSymbols;
    public bool IsLoadingSymbols { get; private set; }
    public bool IsMonitoring => MasterAutomationEnabled && _monitorCts is not null;
    public string? StatusMessage { get; private set; }
    public string CsvPath { get; private set; } =
        Path.Combine(FileSystem.AppDataDirectory, "auto_buy.csv");

    public AutoBuyService(
        IZerodhaService zerodha,
        IMarketDataService marketData,
        ISuperTrendService superTrend,
        ISettingsService settings)
    {
        _zerodha = zerodha;
        _marketData = marketData;
        _superTrend = superTrend;
        _settings = settings;
    }

    public async Task InitializeAsync()
    {
        CsvPath = Path.Combine(FileSystem.AppDataDirectory, "auto_buy.csv");
        var trimmed = LoadFromCsv();
        if (trimmed)
            await SaveAsync();

        await RefreshSymbolsAsync();

        if (_zerodha.IsConnected)
            await RefreshDeployedAmountsAsync();

        if (MasterAutomationEnabled)
            StartMonitorLoop();
    }

    public async Task RefreshSymbolsAsync()
    {
        IsLoadingSymbols = true;
        Notify();

        try
        {
            var symbols = await _zerodha.GetNseEquitySymbolsAsync();
            _nseSymbols = symbols
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();
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

        var q = query.Trim().ToUpperInvariant();
        return _nseSymbols
            .Where(s => s.Contains(q, StringComparison.OrdinalIgnoreCase))
            .Take(limit)
            .ToList();
    }

    public async Task AddSymbolAsync(string symbol)
    {
        var normalized = symbol.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(normalized))
            return;

        if (!_nseSymbols.Contains(normalized))
        {
            StatusMessage = $"{normalized} is not in the NSE equity list.";
            Notify();
            return;
        }

        if (_rows.Count >= AutoBuyDefaults.MaxSymbols)
        {
            StatusMessage = "Auto Buy supports one NSE equity only — remove the current stock first.";
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
        existing.Timeframe = AutoBuyCsvFile.NormalizeTimeframe(row.Timeframe);
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

    public Task SaveAsync()
    {
        AutoBuyCsvFile.Save(CsvPath, MasterAutomationEnabled, _rows);
        return Task.CompletedTask;
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

    private bool LoadFromCsv()
    {
        var (master, rows) = AutoBuyCsvFile.Load(CsvPath);
        MasterAutomationEnabled = master;
        _rows.Clear();

        var trimmed = rows.Count > AutoBuyDefaults.MaxSymbols;

        foreach (var row in rows.Take(AutoBuyDefaults.MaxSymbols))
        {
            row.Exchange = "NSE";
            row.Timeframe = AutoBuyCsvFile.NormalizeTimeframe(row.Timeframe);
            row.Lots = Math.Max(1, row.Lots);
            row.MaxDeployAmount = Math.Max(0, row.MaxDeployAmount);
            _rows.Add(row);
        }

        return trimmed;
    }

    public IReadOnlyList<AutoBuyReadiness.Check> GetReadinessChecks() =>
        AutoBuyReadiness.Evaluate(
            MasterAutomationEnabled,
            _rows.FirstOrDefault(),
            _zerodha.IsConnected,
            _settings.Settings.AutoTradingEnabled,
            MarketHours.IsOpen());

    private void StartMonitorLoop()
    {
        _monitorCts?.Cancel();
        _monitorCts?.Dispose();
        _monitorCts = new CancellationTokenSource();
        _ = MonitorLoopAsync(_monitorCts.Token);
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

            if (SuperTrendFlipHelper.DetectBearishFlipOnLastClosedBar(
                    candles, stPeriod, stMult, getTrend))
            {
                row.Status = "Long hold";
                row.Detail = $"{row.Timeframe} Buy→Sell signal — ignored (buy only, never sell)";
                return;
            }

            var flipped = SuperTrendFlipHelper.DetectBullishFlipOnLastClosedBar(
                candles, stPeriod, stMult, getTrend);

            var lastBarTime = SuperTrendFlipHelper.GetLastClosedBarTime(candles);
            var barKey = lastBarTime.HasValue
                ? $"{row.Symbol}|{row.Timeframe}|{lastBarTime.Value:O}"
                : null;

            if (!flipped)
            {
                var currentTrend = _superTrend.GetTrend(
                    candles,
                    TrailingStopDefaults.Period,
                    TrailingStopDefaults.Multiplier);

                var deployNote = row.MaxDeployAmount > 0
                    ? $" · deployed ₹{row.DeployedAmount:N0} / ₹{row.MaxDeployAmount:N0}"
                    : string.Empty;

                row.Status = "Watching";
                row.Detail = $"{row.Timeframe} — buy only when Sell→Buy flip fires (no sell on other signals){deployNote}";
                return;
            }

            if (barKey is not null && _orderedBarKeys.Contains(barKey))
            {
                row.Status = "Ordered";
                row.Detail = "BUY already sent for this flip — waits for next Sell→Buy signal";
                return;
            }

            var quantity = Math.Max(1, row.Lots);
            var limitPrice = await _zerodha.GetLtpAsync(instrument);
            if (limitPrice <= 0)
                limitPrice = candles[^2].Close;

            if (!AutoBuyReadiness.CanPlaceOrder(
                    row,
                    _zerodha.IsConnected,
                    _settings.Settings.AutoTradingEnabled,
                    MarketHours.IsOpen(),
                    quantity,
                    limitPrice))
            {
                if (!MarketHours.IsOpen())
                {
                    row.Status = flipped ? "Flip (market closed)" : "Market closed";
                    row.Detail = flipped
                        ? "ST flip detected — orders only during market hours"
                        : "Monitoring resumes when market opens";
                }
                else if (!_settings.Settings.AutoTradingEnabled)
                {
                    row.Status = "Flip detected";
                    row.Detail = "ST flip BUY — enable Auto Trading in Settings to place orders";
                    row.LastTriggeredAt = DateTime.Now;
                }
                else if (AutoBuyDeployHelper.WouldExceedMax(
                    row.DeployedAmount, row.MaxDeployAmount, quantity * limitPrice))
                {
                    row.Status = "Max reached";
                    row.Detail = $"Order ₹{quantity * limitPrice:N0} exceeds max ₹{row.MaxDeployAmount:N0}";
                    if (barKey is not null)
                        _orderedBarKeys.Add(barKey);
                    await TryDisableAutomationForMaxAsync(row);
                }
                else if (AutoBuyDeployHelper.IsMaxDeployReached(row.DeployedAmount, row.MaxDeployAmount))
                {
                    await TryDisableAutomationForMaxAsync(row);
                }
                else if (limitPrice <= 0)
                {
                    row.Status = "Flip detected";
                    row.Detail = "Could not fetch price for limit order";
                }

                return;
            }

            var result = await _zerodha.PlaceOrderAsync(
                row.Exchange,
                row.Symbol,
                AutoBuyOrderPolicy.EntrySideOrThrow(AutoBuyDefaults.EntrySide),
                quantity,
                "LIMIT",
                limitPrice,
                AutoBuyDefaults.Product);

            row.LastTriggeredAt = DateTime.Now;

            if (result.IsSuccess)
            {
                if (barKey is not null)
                    _orderedBarKeys.Add(barKey);

                row.Status = "Order placed";
                row.Detail = $"BUY {quantity} CNC @ LIMIT {limitPrice:N2} — long entry · order {result.OrderId}";
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
                row.Status = "Order failed";
                row.Detail = result.ErrorMessage ?? "Order rejected";
                StatusMessage = $"Auto Buy: failed for {row.Symbol} — {row.Detail}";
            }
        }
        catch (Exception ex)
        {
            row.Status = "Error";
            row.Detail = ex.Message;
        }
    }

    private void LastRefreshMessage()
    {
        if (string.IsNullOrEmpty(StatusMessage) || StatusMessage.StartsWith("Auto Buy:", StringComparison.Ordinal))
            StatusMessage = $"Monitoring {_rows.Count(r => r.AutomationEnabled)} symbol(s) — BUY only on each Sell→Buy flip.";
    }

    private void Notify() => Updated?.Invoke();

    public void Dispose()
    {
        if (_disposed)
            return;

        _monitorCts?.Cancel();
        _monitorCts?.Dispose();
        _disposed = true;
    }
}
