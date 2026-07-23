using System.ComponentModel;
using System.Runtime.CompilerServices;
using PGOne.Models;
using PGOne.Services;

namespace PGOne.ViewModels;

public class SignalViewModel : INotifyPropertyChanged
{
    private readonly ISignalService _signal;
    private readonly IZerodhaService _zerodha;
    private readonly ISettingsService _settings;

    public event PropertyChangedEventHandler? PropertyChanged;

    public Signal CurrentSignal { get; private set; } = new();
    public string PlaceOrderMessage { get; private set; } = string.Empty;

    public SignalViewModel(ISignalService signal, IZerodhaService zerodha, ISettingsService settings)
    {
        _signal = signal;
        _zerodha = zerodha;
        _settings = settings;
    }

    public async Task RefreshAsync(string instrument = "NIFTY")
    {
        CurrentSignal = await _signal.GenerateSignalAsync(instrument);
        Notify(nameof(CurrentSignal));
    }

    public async Task PlaceTradeAsync()
    {
        await _settings.LoadAsync();

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

        if (CurrentSignal.Strike <= 0 || string.IsNullOrWhiteSpace(CurrentSignal.OptionType))
        {
            PlaceOrderMessage = "Signal does not include a valid option entry.";
            Notify(nameof(PlaceOrderMessage));
            return;
        }

        var option = await _zerodha.ResolveOptionSymbolAsync(
            CurrentSignal.Instrument,
            CurrentSignal.Strike,
            CurrentSignal.OptionType);

        if (option is null)
        {
            PlaceOrderMessage = $"Could not resolve NFO symbol for {CurrentSignal.Entry}.";
            Notify(nameof(PlaceOrderMessage));
            return;
        }

        var ltp = await _zerodha.GetLtpAsync($"NFO:{option.TradingSymbol}");
        if (ltp <= 0)
        {
            PlaceOrderMessage = "Could not fetch option price for limit order.";
            Notify(nameof(PlaceOrderMessage));
            return;
        }

        var lots = Math.Max(1, _settings.Settings.LotSize);
        var quantity = option.LotSize * lots;
        var transactionType = CurrentSignal.Trend == TrendDirection.Buy ? "BUY" : "SELL";

        var result = await _zerodha.PlaceOrderAsync(
            "NFO",
            option.TradingSymbol,
            transactionType,
            quantity,
            "LIMIT",
            ltp,
            "MIS");

        if (!result.IsSuccess)
        {
            PlaceOrderMessage = result.ErrorMessage ?? "Order placement failed.";
            Notify(nameof(PlaceOrderMessage));
            return;
        }

        var stopLossNote = string.IsNullOrWhiteSpace(CurrentSignal.StopLoss)
            ? string.Empty
            : $" SL: {CurrentSignal.StopLoss}.";

        PlaceOrderMessage =
            $"{transactionType} {quantity} x {option.TradingSymbol} @ ₹{ltp:N2}. Order ID: {result.OrderId}.{stopLossNote}";
        Notify(nameof(PlaceOrderMessage));
    }

    private void Notify([CallerMemberName] string? property = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}
