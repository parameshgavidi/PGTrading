using System.ComponentModel;
using System.Runtime.CompilerServices;
using PGOneTrade.Models;
using PGOneTrade.Services;

namespace PGOneTrade.ViewModels;

public class StrategyViewModel : INotifyPropertyChanged
{
    private readonly IStrategyService _strategy;

    public event PropertyChangedEventHandler? PropertyChanged;

    public StrategyConfig IntradayConfig { get; private set; } = new();
    public LongTermStrategyConfig LongTermConfig { get; private set; } = new();
    public string SaveMessage { get; private set; } = string.Empty;

    public StrategyViewModel(IStrategyService strategy)
    {
        _strategy = strategy;
        IntradayConfig = _strategy.IntradayConfig;
        LongTermConfig = _strategy.LongTermConfig;
    }

    public async Task SaveIntradayAsync()
    {
        await _strategy.SaveIntradayAsync(IntradayConfig);
        SaveMessage = "Intraday strategy saved.";
        Notify(nameof(SaveMessage));
    }

    public async Task SaveLongTermAsync()
    {
        await _strategy.SaveLongTermAsync(LongTermConfig);
        SaveMessage = "Long term strategy saved.";
        Notify(nameof(SaveMessage));
    }

    public void ClearSaveMessage()
    {
        SaveMessage = string.Empty;
        Notify(nameof(SaveMessage));
    }

    private void Notify([CallerMemberName] string? property = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}
