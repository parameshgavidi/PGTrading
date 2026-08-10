namespace PGOneTrade.Models.Trading;

/// <summary>How to derive a LIMIT price from LTP before sending the order.</summary>
public enum LimitPricingMode
{
    /// <summary>Round LTP to exchange tick (equity / delivery).</summary>
    AtLtp = 0,

    /// <summary>BUY one tick above LTP, SELL one tick below (options fill reliability).</summary>
    AggressiveOffset = 1,

    /// <summary>
    /// Use LTP as-is with no tick rounding — preserves legacy scanner / Auto Buy pricing.
    /// </summary>
    RawLtp = 2
}
