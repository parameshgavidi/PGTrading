using System.ComponentModel;
using System.Runtime.CompilerServices;
using PgAiTrading.Models;
using PgAiTrading.Services;

namespace PgAiTrading.ViewModels;

public class WatchlistViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IWatchlistService _watchlist;
    private readonly IIntradayScannerService _intradayScanner;
    private readonly ILongTermScannerService _longTermScanner;
    private readonly ITrailingStopLossService _trailingStop;
    private readonly ILongTermExitMonitorService _longTermExit;
    private readonly IZerodhaService _zerodha;

    public event PropertyChangedEventHandler? PropertyChanged;

    public List<WatchItem> TopWeightageItems { get; private set; } = new();
    public List<StockScanRow> IntradayScanItems { get; private set; } = new();
    public List<TrailingStopRow> TrailingStopItems { get; private set; } = new();
    public List<StockScanRow> LongTermScanItems { get; private set; } = new();
    public List<LongTermExitRow> LongTermExitItems { get; private set; } = new();

    public bool IsLoading =>
        _watchlist.IsLoading
        || _intradayScanner.IsScanning
        || _longTermScanner.IsScanning
        || _trailingStop.IsLoading
        || _longTermExit.IsLoading;

    public bool IsIntradayScanning => _intradayScanner.IsScanning;
    public bool IsLongTermScanning => _longTermScanner.IsScanning;
    public bool IsConnected => _zerodha.IsConnected;

    public string? IntradayScanProgressMessage => _intradayScanner.ProgressMessage;
    public string? LongTermScanProgressMessage => _longTermScanner.ProgressMessage;
    public string? TrailingStopStatusMessage => _trailingStop.StatusMessage;
    public string? LongTermExitStatusMessage => _longTermExit.StatusMessage;

    public bool IsTrailingStopMonitoring => _trailingStop.IsMonitoring;
    public bool IsLongTermExitMonitoring => _longTermExit.IsMonitoring;

    public IReadOnlyList<string> IntradayFrameworkConditions => IntradayFrameworkEvaluator.Conditions;
    public IReadOnlyList<string> LongTermFrameworkConditions { get; }

    public int TrailingStopTriggeredCount => TrailingStopItems.Count(i => i.IsTriggered && !i.ExitPlaced);
    public int TrailingStopMonitoringCount => TrailingStopItems.Count;
    public int LongTermExitAlertCount => LongTermExitItems.Count(i => i.IsExitSignal);

    public WatchlistViewModel(
        IWatchlistService watchlist,
        IIntradayScannerService intradayScanner,
        ILongTermScannerService longTermScanner,
        ITrailingStopLossService trailingStop,
        ILongTermExitMonitorService longTermExit,
        IZerodhaService zerodha,
        ILongTermFrameworkService longTermFramework)
    {
        _watchlist = watchlist;
        _intradayScanner = intradayScanner;
        _longTermScanner = longTermScanner;
        _trailingStop = trailingStop;
        _longTermExit = longTermExit;
        _zerodha = zerodha;
        LongTermFrameworkConditions = longTermFramework.FrameworkConditions;

        _watchlist.WatchlistUpdated += OnWatchlistUpdated;
        _intradayScanner.Updated += OnIntradayScannerUpdated;
        _longTermScanner.Updated += OnLongTermScannerUpdated;
        _trailingStop.Updated += OnTrailingStopUpdated;
        _longTermExit.Updated += OnLongTermExitUpdated;
    }

    public async Task RefreshTopWeightageAsync() =>
        await _watchlist.RefreshTopWeightageAsync(waitForFullList: true);

    public async Task ScanIntradayAsync() => await _intradayScanner.ScanAsync();

    public async Task ScanLongTermAsync() => await _longTermScanner.ScanAsync();

    public async Task RefreshTrailingStopAsync() => await _trailingStop.RefreshAsync();

    public async Task RefreshLongTermExitAsync() => await _longTermExit.RefreshAsync();

    public async Task RefreshTabAsync(WatchlistTab tab, bool rescan = false)
    {
        switch (tab)
        {
            case WatchlistTab.TopWeight:
                await RefreshTopWeightageAsync();
                break;

            case WatchlistTab.IntradayScan:
                if (rescan)
                    await ScanIntradayAsync();
                break;

            case WatchlistTab.TrailingStop:
                await RefreshTrailingStopAsync();
                break;

            case WatchlistTab.LongTermScan:
                if (rescan)
                    await ScanLongTermAsync();
                break;

            case WatchlistTab.LongTermExitMonitor:
                await RefreshLongTermExitAsync();
                break;
        }
    }

    public async Task SetTrailingStopMonitoringAsync(bool enabled)
        => await _trailingStop.SetMonitoringAsync(enabled);

    public async Task SetLongTermExitMonitoringAsync(bool enabled)
        => await _longTermExit.SetMonitoringAsync(enabled);

    public async Task<string?> PlaceMisMarketOrderAsync(StockScanRow row)
    {
        var result = await _intradayScanner.PlaceOrderAsync(row);

        row.OrderMessage = result.IsSuccess
            ? $"MIS LIMIT BUY placed @ {row.LastPrice:N2} — order {result.OrderId}"
            : result.ErrorMessage ?? "Order failed — check Zerodha connection";

        Notify(nameof(IntradayScanItems));
        return result.OrderId;
    }

    public async Task<string?> PlaceCncMarketOrderAsync(StockScanRow row)
    {
        var result = await _longTermScanner.PlaceOrderAsync(row);

        row.OrderMessage = result.IsSuccess
            ? $"CNC LIMIT BUY placed @ {row.LastPrice:N2} — order {result.OrderId}"
            : result.ErrorMessage ?? "Order failed — check Zerodha connection";

        Notify(nameof(LongTermScanItems));
        return result.OrderId;
    }

    private void OnWatchlistUpdated()
    {
        TopWeightageItems = _watchlist.TopWeightageItems;
        Notify();
    }

    private void OnIntradayScannerUpdated()
    {
        IntradayScanItems = _intradayScanner.Items.ToList();
        Notify();
    }

    private void OnLongTermScannerUpdated()
    {
        LongTermScanItems = _longTermScanner.Items.ToList();
        Notify();
    }

    private void OnTrailingStopUpdated()
    {
        TrailingStopItems = _trailingStop.Items.ToList();
        Notify();
    }

    private void OnLongTermExitUpdated()
    {
        LongTermExitItems = _longTermExit.Items.ToList();
        Notify();
    }

    private void Notify([CallerMemberName] string? property = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        if (property != null)
            return;

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TopWeightageItems)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IntradayScanItems)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TrailingStopItems)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LongTermScanItems)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LongTermExitItems)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLoading)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsIntradayScanning)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLongTermScanning)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsConnected)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IntradayScanProgressMessage)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LongTermScanProgressMessage)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TrailingStopStatusMessage)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LongTermExitStatusMessage)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsTrailingStopMonitoring)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLongTermExitMonitoring)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TrailingStopTriggeredCount)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TrailingStopMonitoringCount)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LongTermExitAlertCount)));
    }

    public void Dispose()
    {
        _watchlist.WatchlistUpdated -= OnWatchlistUpdated;
        _intradayScanner.Updated -= OnIntradayScannerUpdated;
        _longTermScanner.Updated -= OnLongTermScannerUpdated;
        _trailingStop.Updated -= OnTrailingStopUpdated;
        _longTermExit.Updated -= OnLongTermExitUpdated;
    }
}
