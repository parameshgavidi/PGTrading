using System.ComponentModel;
using System.Runtime.CompilerServices;
using PGOne.Models;
using PGOne.Models.Trading;
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
    private readonly IOrderExecutionService _orders;
    private SentimentFilter _filter = SentimentFilter.All;

    public event PropertyChangedEventHandler? PropertyChanged;

    public SentimentViewModel(
        ISentimentService sentiment,
        IZerodhaService zerodha,
        IOrderExecutionService orders)
    {
        _sentiment = sentiment;
        _zerodha = zerodha;
        _orders = orders;
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
        // Sentiment orders are long CNC only (delivery buy) — no MIS, no shorts.
        if (row.Prediction != SentimentPrediction.Bullish)
            return (false, "Sentiment Place Order is long CNC only — available on Bullish stocks.");

        var outcome = await _orders.PlaceAsync(new OrderIntent
        {
            Exchange = ExchangeCodes.Nse,
            TradingSymbol = row.Symbol,
            Side = OrderSides.Buy,
            Quantity = quantity,
            UiProduct = ProductTypes.Cnc,
            Pricing = LimitPricingMode.AtLtp
        });

        // Keep success message concise for the row label (matches prior format).
        if (outcome.Success && outcome.LimitPrice is decimal px)
        {
            row.OrderMessage = $"BUY CNC {quantity} @ ₹{px:N2} — order {outcome.OrderId}";
        }
        else
        {
            row.OrderMessage = outcome.Message;
        }

        OnPropertyChanged(nameof(Results));
        return (outcome.Success, row.OrderMessage);
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
