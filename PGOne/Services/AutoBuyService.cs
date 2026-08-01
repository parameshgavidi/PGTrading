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
        LoadFromCsv();
        await RefreshSymbolsAsync();

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

        existing.Exchange = string.IsNullOrWhiteSpace(row.Exchange) ? "NSE" : row.Exchange.ToUpperInvariant();
        existing.Timeframe = AutoBuyCsvFile.NormalizeTimeframe(row.Timeframe);
        existing.Lots = Math.Max(1, row.Lots);
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

        row.AutomationEnabled = enabled;
        await SaveAsync();
        Notify();
    }

    public async Task SetMasterAutomationAsync(bool enabled)
    {
        if (MasterAutomationEnabled == enabled)
            return;

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

    private void LoadFromCsv()
    {
        var (master, rows) = AutoBuyCsvFile.Load(CsvPath);
        MasterAutomationEnabled = master;
        _rows.Clear();
        _rows.AddRange(rows);
    }

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

        var openMis = await _zerodha.GetMisPositionsAsync(includeClosed: false);
        var openSymbols = openMis
            .Where(p => p.Quantity > 0)
            .Select(p => p.Symbol.ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var row in _rows)
        {
            if (!row.AutomationEnabled)
            {
                row.Status = "Disabled";
                row.Detail = "Row automation off";
                continue;
            }

            await EvaluateRowAsync(row, openSymbols);
        }

        LastRefreshMessage();
        Notify();
    }

    private async Task EvaluateRowAsync(AutoBuyRow row, HashSet<string> openSymbols)
    {
        try
        {
            if (openSymbols.Contains(row.Symbol))
            {
                row.Status = "In position";
                row.Detail = "Open MIS long — skip new entry";
                return;
            }

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

            var flipped = SuperTrendFlipHelper.DetectBullishFlipOnLastClosedBar(
                candles,
                TrailingStopDefaults.Period,
                TrailingStopDefaults.Multiplier,
                _superTrend.GetTrend);

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

                row.Status = "Watching";
                row.Detail = $"{row.Timeframe} ST(7,2.5) is {TrendUi.GetBiasLabel(currentTrend)} — waiting for Sell→Buy flip";
                return;
            }

            if (barKey is not null && _orderedBarKeys.Contains(barKey))
            {
                row.Status = "Ordered";
                row.Detail = "Entry already placed for this candle flip";
                return;
            }

            if (!_settings.Settings.AutoTradingEnabled)
            {
                row.Status = "Flip detected";
                row.Detail = "ST flip BUY — enable Auto Trading in Settings to place orders";
                row.LastTriggeredAt = DateTime.Now;
                return;
            }

            var quantity = Math.Max(1, row.Lots);
            var limitPrice = await _zerodha.GetLtpAsync(instrument);
            if (limitPrice <= 0)
                limitPrice = candles[^2].Close;

            if (limitPrice <= 0)
            {
                row.Status = "Flip detected";
                row.Detail = "Could not fetch price for limit order";
                return;
            }

            var result = await _zerodha.PlaceOrderAsync(
                row.Exchange,
                row.Symbol,
                "BUY",
                quantity,
                "LIMIT",
                limitPrice,
                "MIS");

            row.LastTriggeredAt = DateTime.Now;

            if (result.IsSuccess)
            {
                if (barKey is not null)
                    _orderedBarKeys.Add(barKey);

                row.Status = "Order placed";
                row.Detail = $"BUY {quantity} @ LIMIT {limitPrice:N2} — order {result.OrderId}";
                StatusMessage = $"Auto Buy: order placed for {row.Symbol}";
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
            StatusMessage = $"Monitoring {_rows.Count(r => r.AutomationEnabled)} symbols — ST(7,2.5) Sell→Buy flip.";
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
