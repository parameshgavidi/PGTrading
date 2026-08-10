namespace PgAiTrading.Models;

public sealed class NfoFutureInstrument
{
    public string TradingSymbol { get; set; } = string.Empty;
    public DateTime Expiry { get; set; }
    public int InstrumentToken { get; set; }
}
