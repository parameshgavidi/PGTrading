using System.ComponentModel;
using System.Runtime.CompilerServices;
using PGOne.Models;
using PGOne.Services;

namespace PGOne.ViewModels;

public class HoldingsViewModel : INotifyPropertyChanged
{
    private readonly IHoldingsService _holdings;
    private readonly IZerodhaService _zerodha;

    public event PropertyChangedEventHandler? PropertyChanged;

    public List<HoldingRow> Items { get; private set; } = new();
    public bool IsLoading => _holdings.IsLoading;
    public bool IsConnected => _zerodha.IsConnected;
    public int SatisfiedCount => Items.Count(i => i.FrameworkSatisfied);
    public int ReviewCount => Items.Count(i => !i.FrameworkSatisfied);

    public HoldingsViewModel(IHoldingsService holdings, IZerodhaService zerodha)
    {
        _holdings = holdings;
        _zerodha = zerodha;
        _holdings.HoldingsUpdated += () =>
        {
            Items = _holdings.Items;
            Notify();
        };
    }

    public async Task RefreshAsync() => await _holdings.RefreshAsync();

    private void Notify([CallerMemberName] string? property = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        if (property != null)
            return;

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Items)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLoading)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsConnected)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SatisfiedCount)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ReviewCount)));
    }
}
