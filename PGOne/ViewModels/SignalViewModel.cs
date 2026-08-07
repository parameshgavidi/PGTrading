using System.ComponentModel;
using System.Runtime.CompilerServices;
using PGOne.Models;
using PGOne.Services;

namespace PGOne.ViewModels;

public class SignalViewModel : INotifyPropertyChanged
{
    private static readonly string[] IndexUnderlyings = ["NIFTY", "BANKNIFTY", "FINNIFTY"];

    private readonly ISignalService _signal;
    private readonly IZerodhaService _zerodha;
    private readonly ISettingsService _settings;

    public event PropertyChangedEventHandler? PropertyChanged;

    public Signal CurrentSignal { get; private set; } = new();
    public string PlaceOrderMessage { get; private set; } = string.Empty;

    public bool IsOptionPanelOpen { get; private set; }
    public bool IsLoadingOptions { get; private set; }
    public bool IsPlacingOrder { get; private set; }
    public string OptionUnderlying { get; private set; } = "NIFTY";
    public IReadOnlyList<string> AvailableUnderlyings => IndexUnderlyings;
    public IReadOnlyList<SignalOptionRow> OptionRows { get; private set; } = Array.Empty<SignalOptionRow>();
    public SignalOptionRow? SelectedOption { get; private set; }
    public string OrderSide { get; private set; } = "BUY";
    /// <summary>UI product: MIS (intraday) or CNC (mapped to NRML for NFO overnight).</summary>
    public string OrderProduct { get; private set; } = "MIS";
    public int OrderLots { get; private set; } = 1;
    public DateTime? OptionExpiry { get; private set; }
    public decimal SpotPrice { get; private set; }

    public IReadOnlyList<string> AvailableProducts { get; } = ["MIS", "CNC"];

    public int OrderQuantity =>
        SelectedOption is null ? 0 : Math.Max(1, SelectedOption.LotSize) * Math.Max(1, OrderLots);

    public decimal EstimatedOrderValue =>
        SelectedOption is null || SelectedOption.Ltp <= 0
            ? 0m
            : SelectedOption.Ltp * OrderQuantity;

    /// <summary>Zerodha product sent on the wire — CNC on NFO becomes NRML (F&amp;O carry).</summary>
    public string BrokerProduct =>
        OrderProduct.Equals("CNC", StringComparison.OrdinalIgnoreCase) ? "NRML" : "MIS";

    public string ProductHint =>
        OrderProduct.Equals("CNC", StringComparison.OrdinalIgnoreCase)
            ? "CNC → NRML on NFO (carry overnight)"
            : "MIS intraday (auto square-off)";

    public bool CanPlaceSelectedOrder =>
        _zerodha.IsConnected
        && SelectedOption is not null
        && OrderQuantity > 0
        && !IsPlacingOrder
        && !IsLoadingOptions
        && (OrderSide is "BUY" or "SELL")
        && (OrderProduct is "MIS" or "CNC");

    public SignalViewModel(ISignalService signal, IZerodhaService zerodha, ISettingsService settings)
    {
        _signal = signal;
        _zerodha = zerodha;
        _settings = settings;
    }

    public async Task RefreshAsync(string instrument = "NIFTY")
    {
        CurrentSignal = await _signal.GenerateSignalAsync(instrument);
        OptionUnderlying = string.IsNullOrWhiteSpace(CurrentSignal.Instrument)
            ? "NIFTY"
            : CurrentSignal.Instrument.Trim().ToUpperInvariant();
        Notify(nameof(CurrentSignal));
        Notify(nameof(OptionUnderlying));
    }

    /// <summary>Opens the right-side index options panel and loads the chain.</summary>
    public async Task OpenOptionPanelAsync()
    {
        PlaceOrderMessage = string.Empty;
        IsOptionPanelOpen = true;
        Notify(nameof(IsOptionPanelOpen));
        Notify(nameof(PlaceOrderMessage));

        await _settings.LoadAsync();
        OrderLots = Math.Max(1, _settings.Settings.LotSize);

        // Prefill side from signal when available; user can still change Buy/Sell.
        OrderSide = CurrentSignal.Trend == TrendDirection.Sell ? "SELL" : "BUY";
        OrderProduct = "MIS";
        Notify(nameof(OrderLots));
        Notify(nameof(OrderSide));
        Notify(nameof(OrderProduct));
        Notify(nameof(BrokerProduct));
        Notify(nameof(ProductHint));
        Notify(nameof(CanPlaceSelectedOrder));

        if (string.IsNullOrWhiteSpace(OptionUnderlying))
            OptionUnderlying = string.IsNullOrWhiteSpace(CurrentSignal.Instrument)
                ? "NIFTY"
                : CurrentSignal.Instrument.Trim().ToUpperInvariant();

        await LoadOptionChainAsync();
    }

    public void CloseOptionPanel()
    {
        IsOptionPanelOpen = false;
        SelectedOption = null;
        Notify(nameof(IsOptionPanelOpen));
        Notify(nameof(SelectedOption));
        Notify(nameof(CanPlaceSelectedOrder));
        Notify(nameof(OrderQuantity));
        Notify(nameof(EstimatedOrderValue));
    }

    public async Task SetOptionUnderlyingAsync(string underlying)
    {
        var next = underlying.Trim().ToUpperInvariant();
        if (string.Equals(OptionUnderlying, next, StringComparison.OrdinalIgnoreCase))
            return;

        OptionUnderlying = next;
        SelectedOption = null;
        Notify(nameof(OptionUnderlying));
        Notify(nameof(SelectedOption));
        await LoadOptionChainAsync();
    }

    public void SelectOption(SignalOptionRow row)
    {
        SelectedOption = row;
        Notify(nameof(SelectedOption));
        Notify(nameof(OrderQuantity));
        Notify(nameof(EstimatedOrderValue));
        Notify(nameof(CanPlaceSelectedOrder));
    }

    public void SetOrderSide(string side)
    {
        var next = side.Trim().ToUpperInvariant();
        if (next is not ("BUY" or "SELL"))
            return;

        OrderSide = next;
        PlaceOrderMessage = string.Empty;
        Notify(nameof(OrderSide));
        Notify(nameof(PlaceOrderMessage));
        Notify(nameof(CanPlaceSelectedOrder));
    }

    public void SetOrderProduct(string product)
    {
        var next = product.Trim().ToUpperInvariant();
        if (next is not ("MIS" or "CNC"))
            return;

        OrderProduct = next;
        PlaceOrderMessage = string.Empty;
        Notify(nameof(OrderProduct));
        Notify(nameof(BrokerProduct));
        Notify(nameof(ProductHint));
        Notify(nameof(PlaceOrderMessage));
        Notify(nameof(CanPlaceSelectedOrder));
    }

    public void SetOrderLots(int lots)
    {
        OrderLots = Math.Max(1, lots);
        Notify(nameof(OrderLots));
        Notify(nameof(OrderQuantity));
        Notify(nameof(EstimatedOrderValue));
        Notify(nameof(CanPlaceSelectedOrder));
    }

    public async Task LoadOptionChainAsync()
    {
        if (!_zerodha.IsConnected)
        {
            OptionRows = Array.Empty<SignalOptionRow>();
            PlaceOrderMessage = "Connect to Zerodha in Settings to load index options.";
            Notify(nameof(OptionRows));
            Notify(nameof(PlaceOrderMessage));
            return;
        }

        IsLoadingOptions = true;
        PlaceOrderMessage = string.Empty;
        Notify(nameof(IsLoadingOptions));
        Notify(nameof(PlaceOrderMessage));

        try
        {
            SpotPrice = await _zerodha.GetLtpAsync(InstrumentMapper.ToZerodhaKey(OptionUnderlying));
            var chain = await _zerodha.GetIndexOptionChainAsync(OptionUnderlying, strikeCountEachSide: 7);
            OptionExpiry = chain.Count > 0 ? chain[0].Expiry.Date : null;

            var strikeStep = OptionUnderlying switch
            {
                "BANKNIFTY" => 100m,
                "FINNIFTY" => 50m,
                "MIDCPNIFTY" => 25m,
                _ => 50m
            };
            var atm = SpotPrice > 0
                ? Math.Round(SpotPrice / strikeStep, MidpointRounding.AwayFromZero) * strikeStep
                : 0m;

            var keys = chain.Select(o => $"NFO:{o.TradingSymbol}").ToArray();
            var quotes = keys.Length > 0
                ? await _zerodha.GetQuotesAsync(keys)
                : new Dictionary<string, decimal>();

            OptionRows = chain.Select(o =>
            {
                quotes.TryGetValue($"NFO:{o.TradingSymbol}", out var ltp);
                return new SignalOptionRow
                {
                    TradingSymbol = o.TradingSymbol,
                    Strike = o.Strike,
                    OptionType = o.OptionType,
                    Expiry = o.Expiry,
                    LotSize = Math.Max(1, o.LotSize),
                    Ltp = ltp,
                    IsAtm = atm > 0 && o.Strike == atm
                };
            }).ToList();

            // Prefer signal strike/type when present in the loaded chain.
            if (CurrentSignal.Strike > 0 && !string.IsNullOrWhiteSpace(CurrentSignal.OptionType))
            {
                var preferred = OptionRows.FirstOrDefault(r =>
                    r.Strike == CurrentSignal.Strike
                    && r.OptionType.Equals(CurrentSignal.OptionType, StringComparison.OrdinalIgnoreCase));
                if (preferred is not null)
                    SelectedOption = preferred;
            }

            if (SelectedOption is null)
                SelectedOption = OptionRows.FirstOrDefault(r => r.IsAtm && r.OptionType == "CE")
                    ?? OptionRows.FirstOrDefault(r => r.IsAtm)
                    ?? OptionRows.FirstOrDefault();

            if (OptionRows.Count == 0)
                PlaceOrderMessage = $"No NFO options found for {OptionUnderlying}. Check Zerodha connection.";
        }
        catch (Exception ex)
        {
            OptionRows = Array.Empty<SignalOptionRow>();
            PlaceOrderMessage = $"Failed to load options: {ex.Message}";
        }
        finally
        {
            IsLoadingOptions = false;
            Notify(nameof(IsLoadingOptions));
            Notify(nameof(OptionRows));
            Notify(nameof(OptionExpiry));
            Notify(nameof(SpotPrice));
            Notify(nameof(SelectedOption));
            Notify(nameof(OrderQuantity));
            Notify(nameof(EstimatedOrderValue));
            Notify(nameof(CanPlaceSelectedOrder));
            Notify(nameof(PlaceOrderMessage));
        }
    }

    public async Task PlaceSelectedOptionOrderAsync()
    {
        if (SelectedOption is null)
        {
            PlaceOrderMessage = "Select an option first.";
            Notify(nameof(PlaceOrderMessage));
            return;
        }

        if (!_zerodha.IsConnected)
        {
            PlaceOrderMessage = "Connect to Zerodha in Settings before placing an order.";
            Notify(nameof(PlaceOrderMessage));
            return;
        }

        if (OrderSide is not ("BUY" or "SELL"))
        {
            PlaceOrderMessage = "Choose Buy or Sell.";
            Notify(nameof(PlaceOrderMessage));
            return;
        }

        if (OrderQuantity <= 0)
        {
            PlaceOrderMessage = "Lots / quantity must be at least 1.";
            Notify(nameof(PlaceOrderMessage));
            return;
        }

        var side = OrderSide;
        var product = BrokerProduct;
        var productLabel = OrderProduct.Equals("CNC", StringComparison.OrdinalIgnoreCase)
            ? "CNC (NRML)"
            : "MIS";
        var option = SelectedOption;
        var quantity = OrderQuantity;

        IsPlacingOrder = true;
        PlaceOrderMessage = $"Placing {side} {productLabel}…";
        Notify(nameof(IsPlacingOrder));
        Notify(nameof(CanPlaceSelectedOrder));
        Notify(nameof(PlaceOrderMessage));

        try
        {
            await _settings.LoadAsync();

            var liveLtp = await _zerodha.GetLtpAsync($"NFO:{option.TradingSymbol}");
            if (liveLtp <= 0)
                liveLtp = option.Ltp;

            if (liveLtp <= 0)
            {
                PlaceOrderMessage = "Could not fetch option LTP. Refresh the chain and try again.";
                return;
            }

            option.Ltp = liveLtp;
            Notify(nameof(SelectedOption));
            Notify(nameof(EstimatedOrderValue));

            // SELL limits fill more reliably slightly below LTP; BUY slightly above.
            var tick = OrderPriceHelper.GetTickSize("NFO");
            var rawPrice = side == "SELL" ? liveLtp - tick : liveLtp + tick;
            if (rawPrice <= 0)
                rawPrice = liveLtp;

            var limitPrice = OrderPriceHelper.RoundToTick(rawPrice, "NFO");
            if (limitPrice <= 0)
                limitPrice = OrderPriceHelper.RoundToTick(liveLtp, "NFO");

            var result = await _zerodha.PlaceOrderAsync(
                "NFO",
                option.TradingSymbol,
                side,
                quantity,
                "LIMIT",
                limitPrice,
                product);

            if (!result.IsSuccess)
            {
                var marketResult = await _zerodha.PlaceOrderAsync(
                    "NFO",
                    option.TradingSymbol,
                    side,
                    quantity,
                    "MARKET",
                    price: null,
                    product);

                if (!marketResult.IsSuccess)
                {
                    PlaceOrderMessage =
                        $"{side} failed — Limit: {result.ErrorMessage ?? "rejected"}; Market: {marketResult.ErrorMessage ?? "rejected"}.";
                    return;
                }

                PlaceOrderMessage =
                    $"{side} {quantity} x {option.TradingSymbol} MARKET ({productLabel}). Order ID: {marketResult.OrderId}.";
                return;
            }

            PlaceOrderMessage =
                $"{side} {quantity} x {option.TradingSymbol} @ ₹{limitPrice:N2} ({productLabel}). Order ID: {result.OrderId}.";
        }
        catch (Exception ex)
        {
            PlaceOrderMessage = $"Order failed: {ex.Message}";
        }
        finally
        {
            IsPlacingOrder = false;
            Notify(nameof(IsPlacingOrder));
            Notify(nameof(CanPlaceSelectedOrder));
            Notify(nameof(PlaceOrderMessage));
        }
    }

    /// <summary>Legacy one-click path kept for compatibility — opens the options panel instead.</summary>
    public Task PlaceTradeAsync() => OpenOptionPanelAsync();

    private void Notify([CallerMemberName] string? property = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}
