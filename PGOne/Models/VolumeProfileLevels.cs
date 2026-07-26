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

  public string TargetSummary(TrendDirection bias) => bias switch
  {
    TrendDirection.Buy when HasData =>
      $"VAH {Vah:N0} / Prev POC {PrevDayPoc:N0}",
    TrendDirection.Sell when HasData =>
      $"VAL {Val:N0} / Prev POC {PrevDayPoc:N0}",
    _ => "Risk : Reward 1 : 2"
  };
}
