using System.ComponentModel;
using System.Runtime.CompilerServices;
using PGOne.Models;
using PGOne.Services;

namespace PGOne.ViewModels;

public class WatchlistViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IWatchlistService _watchlist;
    private readonly IIntradayScannerService _scanner;
    private readonly ITrailingStopLossService _trailingStop;
    private readonly IZerodhaService _zerodha;

    public event PropertyChangedEventHandler? PropertyChanged;

    public List<WatchItem> TopWeightageItems { get; private set; } = new();
    public List<IntradayScanRow> IntradayScanItems { get; private set; } = new();
    public List<TrailingStopRow> TrailingStopItems { get; private set; } = new();

    public bool IsLoading => _watchlist.IsLoading || _scanner.IsScanning || _trailingStop.IsLoading;
    public bool IsScanning => _scanner.IsScanning;
    public bool IsConnected => _zerodha.IsConnected;
    public string? ScanProgressMessage => _scanner.ProgressMessage;
    public string? TrailingStopStatusMessage => _trailingStop.StatusMessage;
    public bool IsTrailingStopMonitoring => _trailingStop.IsMonitoring;
    public IReadOnlyList<string> IntradayFrameworkConditions => IntradayFrameworkEvaluator.Conditions;

    public int TrailingStopTriggeredCount => TrailingStopItems.Count(i => i.IsTriggered && !i.ExitPlaced);
    public int TrailingStopMonitoringCount => TrailingStopItems.Count;

    public WatchlistViewModel(
        IWatchlistService watchlist,
        IIntradayScannerService scanner,
        ITrailingStopLossService trailingStop,
        IZerodhaService zerodha)
    {
        _watchlist = watchlist;
        _scanner = scanner;
        _trailingStop = trailingStop;
        _zerodha = zerodha;

        _watchlist.WatchlistUpdated += OnWatchlistUpdated;
        _scanner.Updated += OnScannerUpdated;
        _trailingStop.Updated += OnTrailingStopUpdated;
    }

    public async Task RefreshTopWeightageAsync() => await _watchlist.RefreshTopWeightageAsync();

    public async Task ScanIntradayAsync() => await _scanner.ScanAsync();

    public async Task RefreshTrailingStopAsync() => await _trailingStop.RefreshAsync();

    public async Task RefreshTabAsync(WatchlistTab tab, bool rescanIntraday = false)
    {
        switch (tab)
        {
            case WatchlistTab.TopWeight:
                await RefreshTopWeightageAsync();
                break;

            case WatchlistTab.IntradayScan:
                if (rescanIntraday)
                    await ScanIntradayAsync();
                break;

            case WatchlistTab.TrailingStop:
                await RefreshTrailingStopAsync();
                break;
        }
    }

    public async Task SetTrailingStopMonitoringAsync(bool enabled)
        => await _trailingStop.SetMonitoringAsync(enabled);

    public async Task<string?> PlaceMisMarketOrderAsync(IntradayScanRow row)
    {
        var orderId = await _scanner.PlaceMisMarketOrderAsync(
            row.Exchange,
            row.Symbol,
            row.Quantity,
            "BUY");

        row.OrderMessage = orderId is not null
            ? $"MIS MARKET BUY placed — order {orderId}"
            : "Order failed — check Zerodha connection";

        Notify(nameof(IntradayScanItems));
        return orderId;
    }

    private void OnWatchlistUpdated()
    {
        TopWeightageItems = _watchlist.TopWeightageItems;
        Notify();
    }

    private void OnScannerUpdated()
    {
        IntradayScanItems = _scanner.Items;
        Notify();
    }

    private void OnTrailingStopUpdated()
    {
        TrailingStopItems = _trailingStop.Items;
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
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLoading)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsScanning)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsConnected)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ScanProgressMessage)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TrailingStopStatusMessage)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsTrailingStopMonitoring)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TrailingStopTriggeredCount)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TrailingStopMonitoringCount)));
    }

    public void Dispose()
    {
        _watchlist.WatchlistUpdated -= OnWatchlistUpdated;
        _scanner.Updated -= OnScannerUpdated;
        _trailingStop.Updated -= OnTrailingStopUpdated;
    }
}
