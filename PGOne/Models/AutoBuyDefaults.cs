namespace PGOne.Models;

public static class AutoBuyDefaults
{
    /// <summary>NSE equity delivery — no MIS / F&amp;O.</summary>
    public const string Product = "CNC";

    /// <summary>Maximum NSE equity symbols in the Auto Buy list.</summary>
    public const int MaxSymbols = 50;

    /// <summary>Long-only automation — BUY CNC on ST downtrend→uptrend; never SELL.</summary>
    public const string EntrySide = "BUY";
}
