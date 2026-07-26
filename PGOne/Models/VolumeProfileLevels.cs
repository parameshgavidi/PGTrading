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

  public bool IsAbovePoc(decimal price) =>
    HasData && price > Poc;

  public bool IsBelowPoc(decimal price) =>
    HasData && price < Poc;

  public bool ConfirmsBuy(decimal price) =>
    HasData ? IsAbovePoc(price) : PrevDayPoc > 0 && price > PrevDayPoc;

  public bool ConfirmsSell(decimal price) =>
    HasData ? IsBelowPoc(price) : PrevDayPoc > 0 && price < PrevDayPoc;

  public string TargetSummary(TrendDirection bias) => bias switch
  {
    TrendDirection.Buy when PrevDayVah > 0 || PrevDayPoc > 0 =>
      $"Prev VAH {PrevDayVah:N0} / Prev POC {PrevDayPoc:N0}",
    TrendDirection.Sell when PrevDayVal > 0 || PrevDayPoc > 0 =>
      $"Prev VAL {PrevDayVal:N0} / Prev POC {PrevDayPoc:N0}",
    _ => "Risk : Reward 1 : 2"
  };
}
