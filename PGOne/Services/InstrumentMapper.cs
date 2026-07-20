namespace PGOne.Services;

public static class InstrumentMapper
{
    public static string ToZerodhaKey(string symbol, string exchange = "NSE")
    {
        if (symbol.Contains(':'))
            return symbol;

        return symbol.ToUpper() switch
        {
            "NIFTY" => "NSE:NIFTY 50",
            "BANKNIFTY" => "NSE:NIFTY BANK",
            "RELIANCE" => "NSE:RELIANCE",
            "INFY" => "NSE:INFY",
            "TCS" => "NSE:TCS",
            "SBIN" => "NSE:SBIN",
            "HDFCBANK" => "NSE:HDFCBANK",
            _ => $"{exchange.ToUpperInvariant()}:{symbol.ToUpperInvariant()}"
        };
    }

    public static string ToDisplayName(string symbol) => symbol.ToUpper() switch
    {
        "NIFTY" => "NIFTY 50",
        "BANKNIFTY" => "NIFTY BANK",
        _ => symbol
    };
}
