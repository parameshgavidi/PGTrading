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

    public List<HoldingRow> IntradayItems { get; private set; } = new();
    public List<HoldingRow> LongTermItems { get; private set; } = new();
    public bool IsLoading => _holdings.IsLoading;
    public bool IsConnected => _zerodha.IsConnected;
    public IReadOnlyList<string> IntradayFrameworkConditions => _holdings.IntradayFrameworkConditions;
    public IReadOnlyList<string> LongTermFrameworkConditions => _holdings.LongTermFrameworkConditions;

    public int IntradaySatisfiedCount => IntradayItems.Count(i => i.FrameworkSatisfied);
    public int IntradayReviewCount => IntradayItems.Count(i => !i.FrameworkSatisfied);
    public int LongTermSatisfiedCount => LongTermItems.Count(i => i.FrameworkSatisfied);
    public int LongTermReviewCount => LongTermItems.Count(i => !i.FrameworkSatisfied);

    public HoldingsViewModel(IHoldingsService holdings, IZerodhaService zerodha)
    {
        _holdings = holdings;
        _zerodha = zerodha;
        _holdings.HoldingsUpdated += () =>
        {
            IntradayItems = _holdings.IntradayItems;
            LongTermItems = _holdings.LongTermItems;
            Notify();
        };
    }

    public async Task RefreshAsync() => await _holdings.RefreshAsync();

    private void Notify([CallerMemberName] string? property = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        if (property != null)
            return;

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IntradayItems)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LongTermItems)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLoading)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsConnected)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IntradaySatisfiedCount)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IntradayReviewCount)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LongTermSatisfiedCount)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LongTermReviewCount)));
    }
}
