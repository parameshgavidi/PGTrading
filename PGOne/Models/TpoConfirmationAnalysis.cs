namespace PGOne.Models;

/// <summary>
/// TPO / volume-profile confirmation for SuperTrend signals.
/// </summary>
public class TpoConfirmationAnalysis
{
  public bool BuyConfirmed { get; set; }
  public bool SellConfirmed { get; set; }
  public bool InsideValueArea { get; set; }
  public bool AboveValueArea { get; set; }
  public bool BelowValueArea { get; set; }
  public bool TrendDayOutsideVa { get; set; }
  public bool RotationInsideVa { get; set; }
  public bool CprNarrow { get; set; }
  public bool OpenOutsideValueArea { get; set; }
  public bool StrongTrendDay { get; set; }
  public string Summary { get; set; } = "No TPO data";

  public TrendDirection Bias =>
    BuyConfirmed ? TrendDirection.Buy
    : SellConfirmed ? TrendDirection.Sell
    : TrendDirection.Neutral;

  public bool Confirms(TrendDirection bias) => bias switch
  {
    TrendDirection.Buy => BuyConfirmed,
    TrendDirection.Sell => SellConfirmed,
    _ => false
  };
}
