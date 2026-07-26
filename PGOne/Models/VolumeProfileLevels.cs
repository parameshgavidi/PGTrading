namespace PGOne.Models;

/// <summary>
/// Volume profile levels (POC / Value Area) for targets and S/R.
/// </summary>
public class VolumeProfileLevels
{
  public decimal Poc { get; set; }
  public decimal Vah { get; set; }
  public decimal Val { get; set; }
  public decimal PrevDayPoc { get; set; }
  public decimal PrevDayVah { get; set; }
  public decimal PrevDayVal { get; set; }
  public bool HasData { get; set; }

  public bool IsInsideValueArea(decimal price) =>
    HasData && price >= Val && price <= Vah;

  public bool IsAboveValueArea(decimal price) =>
    HasData && price > Vah;

  public bool IsBelowValueArea(decimal price) =>
    HasData && price < Val;

  public bool ConfirmsBuy(decimal price) =>
    HasData && price > Poc && price > Vah;

  public bool ConfirmsSell(decimal price) =>
    HasData && price < Poc && price < Val;

  public string TargetSummary(TrendDirection bias) => bias switch
  {
    TrendDirection.Buy when PrevDayPoc > 0 || HasData =>
      $"Prev VAH {PrevDayVah:N0} / Prev POC {PrevDayPoc:N0}",
    TrendDirection.Sell when PrevDayPoc > 0 || HasData =>
      $"Prev VAL {PrevDayVal:N0} / Prev POC {PrevDayPoc:N0}",
    _ => "Risk : Reward 1 : 2"
  };
}
