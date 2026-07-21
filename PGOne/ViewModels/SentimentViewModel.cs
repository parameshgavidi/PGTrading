using System.ComponentModel;
using System.Runtime.CompilerServices;
using PGOne.Models;
using PGOne.Services;

namespace PGOne.ViewModels;

public class SentimentViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly ISentimentService _sentiment;

    public event PropertyChangedEventHandler? PropertyChanged;

    public SentimentViewModel(ISentimentService sentiment)
    {
        _sentiment = sentiment;
        _sentiment.Updated += OnSentimentUpdated;
    }

    public bool IsScanning => _sentiment.IsScanning;
    public string? ProgressMessage => _sentiment.ProgressMessage;
    public IReadOnlyList<StockSentimentResult> Results => _sentiment.Results;

    public int BullishCount => Results.Count(r => r.Prediction == SentimentPrediction.Bullish);
    public int BearishCount => Results.Count(r => r.Prediction == SentimentPrediction.Bearish);
    public int NeutralCount => Results.Count(r => r.Prediction == SentimentPrediction.Neutral);

    public async Task ScanNewsFeedsAsync() => await _sentiment.ScanNewsFeedsAsync();

    public async Task ScanSymbolsAsync() => await _sentiment.ScanSymbolsAsync();

    public async Task ScanTopTenAsync() =>
        await _sentiment.ScanSymbolsAsync(NiftyConstituents.Top10Weightage);

    private void OnSentimentUpdated()
    {
        OnPropertyChanged(nameof(IsScanning));
        OnPropertyChanged(nameof(ProgressMessage));
        OnPropertyChanged(nameof(Results));
        OnPropertyChanged(nameof(BullishCount));
        OnPropertyChanged(nameof(BearishCount));
        OnPropertyChanged(nameof(NeutralCount));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public void Dispose() => _sentiment.Updated -= OnSentimentUpdated;
}
