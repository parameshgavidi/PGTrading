using PGOneTrade.Models;

namespace PGOneTrade.Services;

public interface IWatchlistService
{
    List<WatchItem> IndexItems { get; }
    List<WatchItem> Top10WeightItems { get; }
    List<WatchItem> TopWeightageItems { get; }
    bool IsLoading { get; }
    event Action? WatchlistUpdated;
    Task RefreshTopWeightageAsync(bool waitForFullList = false);
}
