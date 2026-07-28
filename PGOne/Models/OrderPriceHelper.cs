namespace PGOne.Models;

public static class OrderPriceHelper
{
    public static decimal GetTickSize(string exchange) =>
        exchange is "NFO" or "BFO" or "CDS" or "MCX" ? 0.05m : 0.01m;

    public static decimal RoundToTick(decimal price, string exchange)
    {
        if (price <= 0)
            return price;

        var tick = GetTickSize(exchange);
        return Math.Round(price / tick, MidpointRounding.AwayFromZero) * tick;
    }

    public static string BuildInstrumentKey(Position position) =>
        $"{position.Exchange}:{position.Symbol}";
}
