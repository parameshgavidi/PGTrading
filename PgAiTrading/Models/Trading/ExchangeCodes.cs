namespace PgAiTrading.Models.Trading;

/// <summary>Broker exchange codes used across order and market-data calls.</summary>
public static class ExchangeCodes
{
    public const string Nse = "NSE";
    public const string Nfo = "NFO";
    public const string Bse = "BSE";
    public const string Bfo = "BFO";

    public static bool IsDerivatives(string? exchange) =>
        exchange is Nfo or Bfo or "CDS" or "MCX";

    public static string InstrumentKey(string exchange, string symbol) =>
        $"{exchange}:{symbol}";
}
