using PgAiTrading.Models.Trading;

namespace PgAiTrading.Models;

public static class AutoBuyDefaults
{
    /// <summary>NSE equity delivery — no MIS / F&amp;O.</summary>
    public const string Product = ProductTypes.Cnc;

    /// <summary>Maximum NSE equity symbols in the Auto Buy list.</summary>
    public const int MaxSymbols = 50;

    /// <summary>Maximum persisted failed entry records shown under Add company.</summary>
    public const int MaxFailedEntries = 100;

    /// <summary>Long-only automation — BUY CNC on ST downtrend→uptrend; never SELL.</summary>
    public const string EntrySide = OrderSides.Buy;
}
