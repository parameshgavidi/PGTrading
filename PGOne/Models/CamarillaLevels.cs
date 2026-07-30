namespace PGOne.Models;

/// <summary>
/// Camarilla pivot levels from previous session H/L/C. H1/L1 omitted (mild S/R).
/// </summary>
public class CamarillaLevels
{
  public decimal H4 { get; set; }
  public decimal H3 { get; set; }
  public decimal H2 { get; set; }
  public decimal Pivot { get; set; }
  public decimal L2 { get; set; }
  public decimal L3 { get; set; }
  public decimal L4 { get; set; }
  public decimal PrevHigh { get; set; }
  public decimal PrevLow { get; set; }
  public decimal PrevClose { get; set; }
  public bool HasData { get; set; }

  public TrendDirection GetBias(decimal price)
  {
    if (!HasData || price <= 0)
      return TrendDirection.Neutral;

    if (price > Pivot && price > H2)
      return TrendDirection.Buy;

    if (price < Pivot && price < L2)
      return TrendDirection.Sell;

    return TrendDirection.Neutral;
  }

  public TrendDirection GetBandBias(decimal price)
  {
    if (!HasData || price <= 0)
      return TrendDirection.Neutral;

    if (price > H3)
      return TrendDirection.Buy;

    if (price < L3)
      return TrendDirection.Sell;

    return TrendDirection.Neutral;
  }
}

public static class CamarillaCalculator
{
  public static CamarillaLevels FromPreviousDay(Candle prevDay)
  {
    var range = prevDay.High - prevDay.Low;
    var close = prevDay.Close;

    if (range <= 0 || close <= 0)
      return new CamarillaLevels();

    var scaled = range * 1.1m;

    return new CamarillaLevels
    {
      HasData = true,
      H4 = Round(close + scaled / 2m),
      H3 = Round(close + scaled / 4m),
      H2 = Round(close + scaled / 6m),
      Pivot = Round((prevDay.High + prevDay.Low + close) / 3m),
      L2 = Round(close - scaled / 6m),
      L3 = Round(close - scaled / 4m),
      L4 = Round(close - scaled / 2m),
      PrevHigh = prevDay.High,
      PrevLow = prevDay.Low,
      PrevClose = close
    };
  }

  private static decimal Round(decimal value) => Math.Round(value, 2);
}
