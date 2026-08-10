namespace PGOneTrade.Models;

public sealed class NfoOptionInstrument
{
    public string TradingSymbol { get; init; } = string.Empty;
    public int LotSize { get; init; }
    public DateTime Expiry { get; init; }
    public decimal Strike { get; init; }
    public string OptionType { get; init; } = string.Empty;
}
