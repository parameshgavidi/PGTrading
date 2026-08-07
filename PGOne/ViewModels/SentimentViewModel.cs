using System.ComponentModel;
using System.Runtime.CompilerServices;
using PGOne.Models;
using PGOne.Services;

namespace PGOne.ViewModels;

public enum SentimentFilter
{
    All,
    Bullish,
    Bearish,
    Neutral
}

public class SentimentViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly ISentimentService _sentiment;
    private readonly IZerodhaService _zerodha;
    private SentimentFilter _filter = SentimentFilter.All;

    public event PropertyChangedEventHandler? PropertyChanged;

    public SentimentViewModel(ISentimentService sentiment, IZerodhaService zerodha)
    {
        _sentiment = sentiment;
        _zerodha = zerodha;
        _sentiment.Updated += OnSentimentUpdated;
    }

    public bool IsScanning => _sentiment.IsScanning;
    public string? ProgressMessage => _sentiment.ProgressMessage;
    public IReadOnlyList<StockSentimentResult> Results => _sentiment.Results;
    public bool IsConnected => _zerodha.IsConnected;

    public SentimentFilter Filter
    {
        get => _filter;
        set
        {
            if (_filter == value)
                return;

            _filter = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FilteredResults));
        }
    }

    public IEnumerable<StockSentimentResult> FilteredResults => Filter switch
    {
        SentimentFilter.Bullish => Results.Where(r => r.Prediction == SentimentPrediction.Bullish),
        SentimentFilter.Bearish => Results.Where(r => r.Prediction == SentimentPrediction.Bearish),
        SentimentFilter.Neutral => Results.Where(r => r.Prediction == SentimentPrediction.Neutral),
        _ => Results
    };

    public int BullishCount => Results.Count(r => r.Prediction == SentimentPrediction.Bullish);
    public int BearishCount => Results.Count(r => r.Prediction == SentimentPrediction.Bearish);
    public int NeutralCount => Results.Count(r => r.Prediction == SentimentPrediction.Neutral);

    public async Task ScanNewsFeedsAsync() => await _sentiment.ScanNewsFeedsAsync();

    public async Task ScanSymbolsAsync() => await _sentiment.ScanSymbolsAsync();

    public async Task ScanTopTenAsync() =>
        await _sentiment.ScanSymbolsAsync(NiftyConstituents.Top10Weightage);

    public async Task<(bool Success, string Message)> PlaceOrderAsync(StockSentimentResult row, int quantity)
    {
        if (!_zerodha.IsConnected)
            return (false, "Please connect to Zerodha first.");

        if (quantity <= 0)
            return (false, "Quantity must be at least 1.");

        // Sentiment orders are long CNC only (delivery buy) — no MIS, no shorts.
        if (row.Prediction != SentimentPrediction.Bullish)
            return (false, "Sentiment Place Order is long CNC only — available on Bullish stocks.");

        var ltp = await _zerodha.GetLtpAsync($"NSE:{row.Symbol}");
        if (ltp <= 0)
            return (false, "Could not fetch price for limit order.");

        var limitPrice = OrderPriceHelper.RoundToTick(ltp, "NSE");
        var result = await _zerodha.PlaceOrderAsync(
            "NSE",
            row.Symbol,
            "BUY",
            quantity,
            "LIMIT",
            limitPrice,
            "CNC");

        row.OrderMessage = result.IsSuccess
            ? $"BUY CNC {quantity} @ ₹{limitPrice:N2} — order {result.OrderId}"
            : result.ErrorMessage ?? "Order placement failed.";

        OnPropertyChanged(nameof(Results));
        return (result.IsSuccess, row.OrderMessage);
    }

    private void OnSentimentUpdated()
    {
        OnPropertyChanged(nameof(IsScanning));
        OnPropertyChanged(nameof(ProgressMessage));
        OnPropertyChanged(nameof(Results));
        OnPropertyChanged(nameof(FilteredResults));
        OnPropertyChanged(nameof(BullishCount));
        OnPropertyChanged(nameof(BearishCount));
        OnPropertyChanged(nameof(NeutralCount));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public void Dispose() => _sentiment.Updated -= OnSentimentUpdated;
}
