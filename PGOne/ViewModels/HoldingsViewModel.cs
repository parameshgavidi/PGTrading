using System.ComponentModel;
using System.Runtime.CompilerServices;
using PGOne.Models;
using PGOne.Services;

namespace PGOne.ViewModels;

public class HoldingsViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IHoldingsService _holdings;
    private readonly ITrailingStopLossService _trailingStop;
    private readonly IZerodhaService _zerodha;

    public event PropertyChangedEventHandler? PropertyChanged;

    public List<HoldingRow> IntradayItems { get; private set; } = new();
    public List<HoldingRow> LongTermItems { get; private set; } = new();
    public List<TrailingStopRow> TrailingStopItems { get; private set; } = new();
    public bool IsLoading => _holdings.IsLoading || _trailingStop.IsLoading;
    public bool IsConnected => _zerodha.IsConnected;
    public string? ErrorMessage => _holdings.ErrorMessage;
    public string? TrailingStopStatusMessage => _trailingStop.StatusMessage;
    public bool IsTrailingStopMonitoring => _trailingStop.IsMonitoring;
    public IReadOnlyList<string> IntradayFrameworkConditions => _holdings.IntradayFrameworkConditions;
    public IReadOnlyList<string> LongTermFrameworkConditions => _holdings.LongTermFrameworkConditions;

    public int IntradaySatisfiedCount => IntradayItems.Count(i => !i.IsClosed && i.FrameworkSatisfied);
    public int IntradayReviewCount => IntradayItems.Count(i => !i.IsClosed && !i.FrameworkSatisfied);
    public int IntradayOpenCount => IntradayItems.Count(i => !i.IsClosed);
    public int IntradayClosedCount => IntradayItems.Count(i => i.IsClosed);
    public int LongTermSatisfiedCount => LongTermItems.Count(i => i.FrameworkSatisfied);
    public int LongTermReviewCount => LongTermItems.Count(i => !i.FrameworkSatisfied);
    public int TrailingStopTriggeredCount => TrailingStopItems.Count(i => i.IsTriggered && !i.ExitPlaced);
    public int TrailingStopMonitoringCount => TrailingStopItems.Count;

    public HoldingsViewModel(
        IHoldingsService holdings,
        ITrailingStopLossService trailingStop,
        IZerodhaService zerodha)
    {
        _holdings = holdings;
        _trailingStop = trailingStop;
        _zerodha = zerodha;

        _holdings.HoldingsUpdated += OnHoldingsUpdated;
        _trailingStop.Updated += OnTrailingStopUpdated;
    }

    public async Task RefreshAsync()
    {
        await Task.WhenAll(_holdings.RefreshAsync(), _trailingStop.RefreshAsync());
    }

    public async Task RefreshTrailingStopAsync() => await _trailingStop.RefreshAsync();

    public async Task SetTrailingStopMonitoringAsync(bool enabled)
        => await _trailingStop.SetMonitoringAsync(enabled);

    private void OnHoldingsUpdated()
    {
        IntradayItems = _holdings.IntradayItems;
        LongTermItems = _holdings.LongTermItems;
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

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IntradayItems)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LongTermItems)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TrailingStopItems)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLoading)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsConnected)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ErrorMessage)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TrailingStopStatusMessage)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsTrailingStopMonitoring)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IntradaySatisfiedCount)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IntradayReviewCount)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IntradayOpenCount)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IntradayClosedCount)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LongTermSatisfiedCount)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LongTermReviewCount)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TrailingStopTriggeredCount)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TrailingStopMonitoringCount)));
    }

    public void Dispose()
    {
        _holdings.HoldingsUpdated -= OnHoldingsUpdated;
        _trailingStop.Updated -= OnTrailingStopUpdated;
    }
}
