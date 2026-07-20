namespace PGOne.Models;

public class IntradayScanRow
{
    public string Symbol { get; set; } = string.Empty;
    public string Exchange { get; set; } = "NSE";
    public decimal LastPrice { get; set; }
    public int Quantity { get; set; }
    public decimal OrderValue { get; set; }
    public bool FrameworkSatisfied { get; set; }
    public string FrameworkStatus { get; set; } = string.Empty;
    public int FrameworkScore { get; set; }
    public string? OrderMessage { get; set; }
}

public static class NiftyConstituents
{
    /// <summary>Top Nifty 50 stocks by approximate index weight (highest first).</summary>
    public static IReadOnlyList<string> TopWeightage { get; } =
    [
        "HDFCBANK", "RELIANCE", "ICICIBANK", "INFY", "ITC", "LT", "TCS",
        "AXISBANK", "KOTAKBANK", "SBIN", "BHARTIARTL", "BAJFINANCE",
        "HINDUNILVR", "ASIANPAINT", "MARUTI", "HCLTECH", "SUNPHARMA",
        "TITAN", "NTPC", "M&M", "ULTRACEMCO", "WIPRO", "POWERGRID",
        "BAJAJFINSV", "NESTLEIND", "ADANIENT", "JSWSTEEL", "TATAMOTORS",
        "ONGC", "COALINDIA", "TECHM", "TATASTEEL", "INDUSINDBK", "DIVISLAB",
        "CIPLA", "DRREDDY", "APOLLOHOSP", "EICHERMOT", "GRASIM", "HEROMOTOCO",
        "BRITANNIA", "HINDALCO", "BPCL", "SBILIFE", "ADANIPORTS", "TRENT",
        "SHRIRAMFIN", "BEL", "JIOFIN", "ETERNAL"
    ];

    /// <summary>Liquid NSE equities used for intraday framework scan (Nifty-heavy universe).</summary>
    public static IReadOnlyList<string> ScanUniverse { get; } =
    [
        "RELIANCE", "TCS", "HDFCBANK", "ICICIBANK", "INFY", "HINDUNILVR", "ITC",
        "SBIN", "BHARTIARTL", "KOTAKBANK", "LT", "AXISBANK", "BAJFINANCE",
        "ASIANPAINT", "MARUTI", "HCLTECH", "SUNPHARMA", "TITAN", "NTPC", "M&M",
        "ULTRACEMCO", "WIPRO", "POWERGRID", "BAJAJFINSV", "NESTLEIND", "ADANIENT",
        "JSWSTEEL", "TATAMOTORS", "ONGC", "COALINDIA", "TECHM", "TATASTEEL",
        "INDUSINDBK", "DIVISLAB", "CIPLA", "DRREDDY", "APOLLOHOSP", "EICHERMOT",
        "GRASIM", "HEROMOTOCO", "BRITANNIA", "HINDALCO", "BPCL", "SBILIFE",
        "ADANIPORTS", "TRENT", "SHRIRAMFIN", "BEL", "JIOFIN", "ETERNAL",
        "VEDL", "DLF", "GAIL", "IOC", "PIDILITIND", "SIEMENS", "AMBUJACEM",
        "HAL", "BANKBARODA", "PNB", "CANBK", "UNIONBANK", "IDFCFIRSTB",
        "FEDERALBNK", "AUBANK", "BANDHANBNK", "CHOLAFIN", "MUTHOOTFIN",
        "PFC", "RECLTD", "IRFC", "LODHA", "VBL", "DABUR", "GODREJCP",
        "MARICO", "COLPAL", "TORNTPHARM", "LUPIN", "AUROPHARMA", "BIOCON",
        "ALKEM", "ZYDUSLIFE", "MAXHEALTH", "POLYCAB", "HAVELLS", "VOLTAS",
        "CROMPTON", "ABB", "BOSCHLTD", "MOTHERSON", "BHARATFORG", "TVSMOTOR",
        "ASHOKLEY", "ESCORTS", "EXIDEIND", "MRF", "PAGEIND", "PIIND", "SRF",
        "UPL", "DEEPAKNTR", "ATGL", "IGL", "MGL", "PETRONET", "CONCOR",
        "IRCTC", "INDIGO", "NAUKRI", "PAYTM", "ZOMATO", "POLICYBZR"
    ];
}
