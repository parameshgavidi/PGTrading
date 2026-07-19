using System.ComponentModel;
using System.Runtime.CompilerServices;
using PGOne.Models;
using PGOne.Services;

namespace PGOne.ViewModels;

public class StrategyViewModel : INotifyPropertyChanged
{
    private readonly IStrategyService _strategy;

    public event PropertyChangedEventHandler? PropertyChanged;

    public StrategyConfig Config { get; private set; } = new();
    public string SaveMessage { get; private set; } = string.Empty;

    public StrategyViewModel(IStrategyService strategy)
    {
        _strategy = strategy;
        Config = _strategy.Config;
    }

    public async Task SaveAsync()
    {
        await _strategy.SaveAsync(Config);
        SaveMessage = "Strategy saved successfully!";
        Notify(nameof(SaveMessage));
    }

    private void Notify([CallerMemberName] string? property = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}
