using PgAiTrading.Models;
using PgAiTrading.Models.Trading;
using PgAiTrading.Models.Ui;

namespace PgAiTrading.Services;

public interface IOrderExecutionService
{
    Task<OrderExecutionOutcome> PlaceAsync(OrderIntent intent);
}

/// <summary>
/// Single path for limit (and optional market-fallback) order placement.
/// ViewModels/services build an <see cref="OrderIntent"/>; this class owns LTP, tick, product mapping, and broker call.
/// </summary>
public sealed class OrderExecutionService : IOrderExecutionService
{
    private readonly IZerodhaService _zerodha;

    public OrderExecutionService(IZerodhaService zerodha)
    {
        _zerodha = zerodha;
    }

    public async Task<OrderExecutionOutcome> PlaceAsync(OrderIntent intent)
    {
        // Keep wording identical to ZerodhaService.PlaceOrderAsync for call-site parity.
        if (!_zerodha.IsConnected)
            return OrderExecutionOutcome.Fail(BrokerUiMessages.BrokerNotConnected);

        if (intent.Quantity <= 0)
            return OrderExecutionOutcome.Fail(BrokerUiMessages.OrderQuantityInvalid);

        string side;
        string uiProduct;
        try
        {
            side = OrderSides.Normalize(intent.Side);
            uiProduct = ProductTypes.NormalizeUi(intent.UiProduct);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return OrderExecutionOutcome.Fail(ex.Message);
        }

        var exchange = intent.Exchange.Trim().ToUpperInvariant();
        var symbol = intent.TradingSymbol.Trim();
        if (string.IsNullOrEmpty(exchange) || string.IsNullOrEmpty(symbol))
            return OrderExecutionOutcome.Fail("Exchange and trading symbol are required.");

        var brokerProduct = BrokerProductMapper.ToBrokerProduct(uiProduct, exchange);
        var productLabel = BrokerProductMapper.DescribeUiProduct(uiProduct, exchange);

        var ltp = intent.HintPrice.GetValueOrDefault();
        if (ltp <= 0)
            ltp = await _zerodha.GetLtpAsync(ExchangeCodes.InstrumentKey(exchange, symbol));

        if (ltp <= 0)
            return OrderExecutionOutcome.Fail(BrokerUiMessages.CouldNotFetchPrice);

        var limitPrice = LimitPriceCalculator.Compute(exchange, side, ltp, intent.Pricing);
        if (limitPrice <= 0)
            return OrderExecutionOutcome.Fail(BrokerUiMessages.CouldNotFetchPrice);

        var result = await _zerodha.PlaceOrderAsync(
            exchange,
            symbol,
            side,
            intent.Quantity,
            "LIMIT",
            limitPrice,
            brokerProduct);

        if (result.IsSuccess)
        {
            return OrderExecutionOutcome.Ok(
                $"{side} {intent.Quantity} x {symbol} @ ₹{limitPrice:N2} ({productLabel}). Order ID: {result.OrderId}.",
                result.OrderId!,
                limitPrice);
        }

        if (!intent.FallbackToMarket)
        {
            return OrderExecutionOutcome.Fail(
                result.ErrorMessage ?? BrokerUiMessages.OrderPlacementFailed);
        }

        var marketResult = await _zerodha.PlaceOrderAsync(
            exchange,
            symbol,
            side,
            intent.Quantity,
            "MARKET",
            price: null,
            brokerProduct);

        if (!marketResult.IsSuccess)
        {
            return OrderExecutionOutcome.Fail(
                $"{side} failed — Limit: {result.ErrorMessage ?? "rejected"}; Market: {marketResult.ErrorMessage ?? "rejected"}.");
        }

        return OrderExecutionOutcome.Ok(
            $"{side} {intent.Quantity} x {symbol} MARKET ({productLabel}). Order ID: {marketResult.OrderId}.",
            marketResult.OrderId!,
            limitPrice,
            usedMarketFallback: true);
    }
}
