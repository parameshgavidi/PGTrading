namespace PGOne.Models.Trading;

/// <summary>How to derive a LIMIT price from LTP before sending the order.</summary>
public enum LimitPricingMode
{
    /// <summary>Round LTP to exchange tick (equity / delivery).</summary>
    AtLtp = 0,

    /// <summary>BUY one tick above LTP, SELL one tick below (options fill reliability).</summary>
    AggressiveOffset = 1
}
