namespace PGOne.Models;

public static class AutoBuyDefaults
{
    /// <summary>NSE equity delivery — no MIS / F&amp;O.</summary>
    public const string Product = "CNC";

    /// <summary>Auto Buy tracks a single NSE equity symbol.</summary>
    public const int MaxSymbols = 1;
}
