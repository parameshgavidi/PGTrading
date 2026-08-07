namespace PGOne.Models;

/// <summary>One CE/PE row shown in the Signals index-options panel.</summary>
public sealed class SignalOptionRow
{
    public string TradingSymbol { get; init; } = string.Empty;
    public decimal Strike { get; init; }
    public string OptionType { get; init; } = string.Empty;
    public DateTime Expiry { get; init; }
    public int LotSize { get; init; } = 1;
    public decimal Ltp { get; set; }
    public bool IsAtm { get; init; }

    public string ExpiryLabel => Expiry.ToString("dd-MMM");
    public string DisplayName => $"{Strike:0} {OptionType}";
}
