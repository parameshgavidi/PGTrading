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

        var tradingsymbol = CurrentSignal.Entry.Replace(" ", "");
        var ltp = await _zerodha.GetLtpAsync($"NFO:{tradingsymbol}");
        if (ltp <= 0)
        {
            PlaceOrderMessage = "Could not fetch price for limit order.";
            Notify(nameof(PlaceOrderMessage));
            return;
        }

        var result = await _zerodha.PlaceOrderAsync(
            "NFO",
            tradingsymbol,
            CurrentSignal.Trend == TrendDirection.Buy ? "BUY" : "SELL",
            1,
            "LIMIT",
            ltp);

        PlaceOrderMessage = result.IsSuccess
            ? $"Order placed! ID: {result.OrderId}"
            : result.ErrorMessage ?? "Order placement failed.";
        Notify(nameof(PlaceOrderMessage));
    }

    private void Notify([CallerMemberName] string? property = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}
