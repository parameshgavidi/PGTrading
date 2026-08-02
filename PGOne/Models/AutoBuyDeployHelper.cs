namespace PGOne.Models;

public static class AutoBuyDeployHelper
{
    public static decimal GetDeployedAmount(
        string symbol,
        IReadOnlyList<Holding> holdings,
        IReadOnlyList<Position> cncPositions)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return 0m;

        decimal total = 0m;

        foreach (var h in holdings.Where(h =>
                     h.Quantity > 0 &&
                     h.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase)))
        {
            var price = h.LastPrice > 0 ? h.LastPrice : h.AveragePrice;
            total += h.Quantity * price;
        }

        foreach (var p in cncPositions.Where(p =>
                     p.Quantity > 0 &&
                     p.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase)))
        {
            var price = p.LastPrice > 0 ? p.LastPrice : p.AveragePrice;
            total += Math.Abs(p.Quantity) * price;
        }

        return total;
    }

    public static bool IsMaxDeployReached(decimal deployedAmount, decimal maxDeployAmount) =>
        maxDeployAmount > 0 && deployedAmount >= maxDeployAmount;

    public static bool WouldExceedMax(decimal deployedAmount, decimal maxDeployAmount, decimal orderValue) =>
        maxDeployAmount > 0 && deployedAmount + orderValue > maxDeployAmount;
}
