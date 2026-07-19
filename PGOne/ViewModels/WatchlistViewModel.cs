using System.ComponentModel;
using System.Runtime.CompilerServices;
using PGOne.Models;
using PGOne.Services;

namespace PGOne.ViewModels;

public class WatchlistViewModel : INotifyPropertyChanged
{
    private readonly IWatchlistService _watchlist;

    public event PropertyChangedEventHandler? PropertyChanged;
    public List<WatchItem> Items { get; private set; } = new();

    public WatchlistViewModel(IWatchlistService watchlist)
    {
        _watchlist = watchlist;
        _watchlist.WatchlistUpdated += () => { Items = _watchlist.Items; Notify(); };
    }

    public async Task RefreshAsync() => await _watchlist.RefreshAsync();

    private void Notify([CallerMemberName] string? property = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}
