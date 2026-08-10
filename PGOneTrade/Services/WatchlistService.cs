using PGOneTrade.Models;

namespace PGOneTrade.Services;

public class WatchlistService : IWatchlistService
{
  private const int QuickTrendPeriod = 10;
  private const double QuickTrendMultiplier = 3.0;
  private const int SparklineCandleCount = 24;

  private readonly IZerodhaService _zerodha;
  private readonly ISuperTrendService _superTrend;

  public List<WatchItem> IndexItems { get; private set; } = new();
  public List<WatchItem> Top10WeightItems { get; private set; } = new();
  public List<WatchItem> TopWeightageItems { get; private set; } = new();
  public bool IsLoading { get; private set; }
  public event Action? WatchlistUpdated;

  public WatchlistService(IZerodhaService zerodha, ISuperTrendService superTrend)
  {
    _zerodha = zerodha;
    _superTrend = superTrend;
  }

  public async Task RefreshTopWeightageAsync(bool waitForFullList = false)
  {
    IsLoading = true;
    WatchlistUpdated?.Invoke();

    try
    {
      var indexSymbols = NiftyConstituents.DashboardIndices;
      var top10Symbols = NiftyConstituents.Top10Weightage;
      var allStockSymbols = NiftyConstituents.FullTopWeightageWatchlist;

      var uniqueSymbols = indexSymbols
          .Concat(allStockSymbols)
          .Distinct(StringComparer.OrdinalIgnoreCase)
          .ToList();

      var instruments = new string[uniqueSymbols.Count];
      for (var i = 0; i < uniqueSymbols.Count; i++)
        instruments[i] = InstrumentMapper.ToZerodhaKey(uniqueSymbols[i]);

      var quotes = await _zerodha.GetInstrumentQuotesAsync(instruments);

      var dashboardSymbols = indexSymbols
          .Concat(top10Symbols)
          .Distinct(StringComparer.OrdinalIgnoreCase)
          .ToList();

      var dashboardSnapshots = await BuildMarketSnapshotsAsync(dashboardSymbols, maxConcurrency: 6);

      IndexItems = BuildWatchItems(indexSymbols, quotes, dashboardSnapshots, markFavorites: true);
      Top10WeightItems = BuildWatchItems(top10Symbols, quotes, dashboardSnapshots);
      WatchlistUpdated?.Invoke();

      var fullTask = RefreshFullTopWeightageInBackgroundAsync(allStockSymbols, quotes);
      if (waitForFullList)
        await fullTask;
    }
    finally
    {
      IsLoading = false;
      WatchlistUpdated?.Invoke();
    }
  }

  private async Task RefreshFullTopWeightageInBackgroundAsync(
      IReadOnlyList<string> allStockSymbols,
      Dictionary<string, InstrumentQuote> quotes)
  {
    try
    {
      var snapshots = await BuildMarketSnapshotsAsync(allStockSymbols, maxConcurrency: 4);
      TopWeightageItems = BuildWatchItems(allStockSymbols, quotes, snapshots);
      WatchlistUpdated?.Invoke();
    }
    catch
    {
      // Watchlist page can retry; dashboard top 10 is already shown.
    }
  }

  private async Task<IReadOnlyDictionary<string, WatchlistMarketSnapshot>> BuildMarketSnapshotsAsync(
      IReadOnlyList<string> symbols,
      int maxConcurrency)
  {
    var snapshots = new Dictionary<string, WatchlistMarketSnapshot>(StringComparer.OrdinalIgnoreCase);
    using var gate = new SemaphoreSlim(maxConcurrency);
    var lockObj = new object();

    var tasks = symbols.Select(async symbol =>
    {
      await gate.WaitAsync();
      try
      {
        var snapshot = await BuildMarketSnapshotAsync(symbol);
        lock (lockObj)
        {
          snapshots[symbol] = snapshot;
        }
      }
      catch
      {
        lock (lockObj)
        {
          snapshots[symbol] = WatchlistMarketSnapshot.Empty;
        }
      }
      finally
      {
        gate.Release();
      }
    });

    await Task.WhenAll(tasks);
    return snapshots;
  }

  private async Task<WatchlistMarketSnapshot> BuildMarketSnapshotAsync(string symbol)
  {
    var instrument = InstrumentMapper.ToZerodhaKey(symbol);
    var candles = await _zerodha.GetHistoricalCandlesAsync(instrument, "5m", SparklineCandleCount);
    if (candles.Count == 0)
      return WatchlistMarketSnapshot.Empty;

    var closes = candles.Select(c => c.Close).ToList();
    var sparkline = SparklineHelper.GenerateFromCloses(closes);
    var trend = candles.Count >= QuickTrendPeriod + 1
        ? _superTrend.GetTrend(candles, QuickTrendPeriod, QuickTrendMultiplier)
        : TrendDirection.Neutral;

    return new WatchlistMarketSnapshot(sparkline, trend);
  }

  private static List<WatchItem> BuildWatchItems(
      IReadOnlyList<string> symbols,
      Dictionary<string, InstrumentQuote> quotes,
      IReadOnlyDictionary<string, WatchlistMarketSnapshot> snapshots,
      bool markFavorites = false)
  {
    var items = new List<WatchItem>();

    for (var i = 0; i < symbols.Count; i++)
    {
      var symbol = symbols[i];
      var instrument = InstrumentMapper.ToZerodhaKey(symbol);
      quotes.TryGetValue(instrument, out var quote);
      var price = quote?.LastPrice ?? 0m;
      var changePct = quote?.ChangePercent ?? NiftyWeights.GetDemoChangePercent(symbol);

      snapshots.TryGetValue(symbol, out var snapshot);
      var trend = snapshot?.Trend ?? TrendDirection.Neutral;
      var sparkline = snapshot?.Sparkline ?? string.Empty;
      if (string.IsNullOrEmpty(sparkline))
        sparkline = SparklineHelper.Generate(trend, changePct);

      items.Add(new WatchItem
      {
        Symbol = symbol,
        Name = InstrumentMapper.ToDisplayName(symbol),
        Rank = i + 1,
        LastPrice = price,
        Change = quote?.Change ?? 0m,
        ChangePercent = changePct,
        Trend = trend,
        IsFavorite = markFavorites,
        Weight = NiftyWeights.GetWeight(symbol),
        Sparkline = sparkline
      });
    }

    return items;
  }

  private sealed record WatchlistMarketSnapshot(string Sparkline, TrendDirection Trend)
  {
    public static WatchlistMarketSnapshot Empty { get; } =
        new(string.Empty, TrendDirection.Neutral);
  }
}
