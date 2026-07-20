namespace PGOne.Services;

public static class InstrumentMapper
{
    public static string ToZerodhaKey(string symbol) => symbol.ToUpper() switch
    {
        "NIFTY" => "NSE:NIFTY 50",
        "BANKNIFTY" => "NSE:NIFTY BANK",
        "RELIANCE" => "NSE:RELIANCE",
        "INFY" => "NSE:INFY",
        "TCS" => "NSE:TCS",
        "SBIN" => "NSE:SBIN",
        "HDFCBANK" => "NSE:HDFCBANK",
        _ => symbol.Contains(':') ? symbol : $"NSE:{symbol}"
    };

    public static string ToDisplayName(string symbol) => symbol.ToUpper() switch
    {
        "NIFTY" => "NIFTY 50",
        "BANKNIFTY" => "NIFTY BANK",
        _ => symbol
    };
}
