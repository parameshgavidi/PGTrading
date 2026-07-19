using System.ComponentModel;
using System.Runtime.CompilerServices;
using PGOne.Models;
using PGOne.Services;

namespace PGOne.ViewModels;

public class SignalViewModel : INotifyPropertyChanged
{
    private readonly ISignalService _signal;
    private readonly IZerodhaService _zerodha;

    public event PropertyChangedEventHandler? PropertyChanged;

    public Signal CurrentSignal { get; private set; } = new();
    public string PlaceOrderMessage { get; private set; } = string.Empty;

    public SignalViewModel(ISignalService signal, IZerodhaService zerodha)
    {
        _signal = signal;
        _zerodha = zerodha;
    }

    public async Task RefreshAsync(string instrument = "NIFTY")
    {
        CurrentSignal = await _signal.GenerateSignalAsync(instrument);
        Notify(nameof(CurrentSignal));
    }

    public async Task PlaceTradeAsync()
    {
        if (!_zerodha.IsConnected)
        {
            PlaceOrderMessage = "Please connect to Zerodha first.";
            Notify(nameof(PlaceOrderMessage));
            return;
        }

        if (CurrentSignal.Trend == TrendDirection.Neutral)
        {
            PlaceOrderMessage = "No valid trade signal.";
            Notify(nameof(PlaceOrderMessage));
            return;
        }

        var orderId = await _zerodha.PlaceOrderAsync(
            "NFO",
            CurrentSignal.Entry.Replace(" ", ""),
            CurrentSignal.Trend == TrendDirection.Buy ? "BUY" : "SELL",
            1,
            "MARKET");

        PlaceOrderMessage = orderId != null
            ? $"Order placed! ID: {orderId}"
            : "Order placement failed.";
        Notify(nameof(PlaceOrderMessage));
    }

    private void Notify([CallerMemberName] string? property = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}
