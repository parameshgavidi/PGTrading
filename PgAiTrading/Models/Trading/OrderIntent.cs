namespace PgAiTrading.Models.Trading;

/// <summary>Intent for a single broker order — UI layer builds this; execution service places it.</summary>
public sealed class OrderIntent
{
    public required string Exchange { get; init; }
    public required string TradingSymbol { get; init; }
    public required string Side { get; init; }
    public required int Quantity { get; init; }

    /// <summary>UI product (MIS or CNC). Mapped to broker product via <see cref="BrokerProductMapper"/>.</summary>
    public required string UiProduct { get; init; }

    public LimitPricingMode Pricing { get; init; } = LimitPricingMode.AtLtp;

    /// <summary>Optional known LTP; when null/≤0 the execution service fetches live LTP.</summary>
    public decimal? HintPrice { get; init; }

    /// <summary>When LIMIT is rejected, retry once as MARKET (used for NFO options).</summary>
    public bool FallbackToMarket { get; init; }

    public string InstrumentKey => ExchangeCodes.InstrumentKey(Exchange, TradingSymbol);

    public string BrokerProduct => BrokerProductMapper.ToBrokerProduct(UiProduct, Exchange);

    public string ProductLabel => BrokerProductMapper.DescribeUiProduct(UiProduct, Exchange);
}

/// <summary>Normalized result for UI / automation after attempting an order.</summary>
public sealed class OrderExecutionOutcome
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? OrderId { get; init; }
    public decimal? LimitPrice { get; init; }
    public bool UsedMarketFallback { get; init; }

    public static OrderExecutionOutcome Fail(string message) => new()
    {
        Success = false,
        Message = message
    };

    public static OrderExecutionOutcome Ok(
        string message,
        string orderId,
        decimal? limitPrice = null,
        bool usedMarketFallback = false) => new()
    {
        Success = true,
        Message = message,
        OrderId = orderId,
        LimitPrice = limitPrice,
        UsedMarketFallback = usedMarketFallback
    };
}
