namespace PGOne.Models;

/// <summary>
/// Volume footprint confirmation derived from 5m candle order-flow proxies.
/// Used as the final confirmation layer — not a standalone signal source.
/// </summary>
public class FootprintAnalysis
{
  public decimal Delta { get; set; }
  public bool PositiveDelta { get; set; }
  public bool NegativeDelta { get; set; }
  public bool StackedBuyImbalance { get; set; }
  public bool StackedSellImbalance { get; set; }
  public bool AbsorptionAgainstLong { get; set; }
  public bool AbsorptionAgainstShort { get; set; }
  public bool NearVolumeNode { get; set; }
  public bool HasUnfinishedAuction { get; set; }
  public string Summary { get; set; } = "No data";

  public bool ConfirmsLong => PositiveDelta && StackedBuyImbalance && !AbsorptionAgainstLong;
  public bool ConfirmsShort => NegativeDelta && StackedSellImbalance && !AbsorptionAgainstShort;

  public bool Confirms(TrendDirection bias) => bias switch
  {
    TrendDirection.Buy => ConfirmsLong,
    TrendDirection.Sell => ConfirmsShort,
    _ => false
  };
}
