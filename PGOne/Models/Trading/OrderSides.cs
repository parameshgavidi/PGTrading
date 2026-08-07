namespace PGOne.Models.Trading;

/// <summary>Buy/Sell transaction types sent to the broker.</summary>
public static class OrderSides
{
    public const string Buy = "BUY";
    public const string Sell = "SELL";

    public static bool IsValid(string? side) =>
        side is Buy or Sell
        || string.Equals(side, Buy, StringComparison.OrdinalIgnoreCase)
        || string.Equals(side, Sell, StringComparison.OrdinalIgnoreCase);

    public static string Normalize(string side)
    {
        if (string.Equals(side, Buy, StringComparison.OrdinalIgnoreCase))
            return Buy;
        if (string.Equals(side, Sell, StringComparison.OrdinalIgnoreCase))
            return Sell;

        throw new ArgumentOutOfRangeException(nameof(side), side, "Side must be BUY or SELL.");
    }
}
