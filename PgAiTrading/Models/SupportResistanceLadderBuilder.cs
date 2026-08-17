namespace PgAiTrading.Models;

public sealed record SupportResistanceRow(string Label, decimal Price, string CssClass, string Group);

public static class SupportResistanceLadderBuilder
{
  public static IReadOnlyList<SupportResistanceRow> Build(MultiTimeframeAnalysis analysis)
  {
    var rows = new List<SupportResistanceRow>();
    var vp = analysis.VolumeProfile;
    var cam = analysis.Camarilla;

    if (vp.HasData)
    {
      rows.Add(new("VAH today", vp.Vah, "sell", "today"));
      rows.Add(new("POC today", vp.Poc, "neutral", "today"));
      rows.Add(new("VAL today", vp.Val, "buy", "today"));
    }

    if (vp.PrevDayPoc > 0)
    {
      rows.Add(new("VAH prev", vp.PrevDayVah, "sell", "prev"));
      rows.Add(new("POC prev", vp.PrevDayPoc, "neutral", "prev"));
      rows.Add(new("VAL prev", vp.PrevDayVal, "buy", "prev"));
    }

    if (vp.Pdh > 0)
      rows.Add(new("PDH", vp.Pdh, "sell", "prev"));
    if (vp.Pdl > 0)
      rows.Add(new("PDL", vp.Pdl, "buy", "prev"));

    if (analysis.CprPivot > 0)
    {
      rows.Add(new("Day TC", analysis.CprTc, "sell", "cpr"));
      rows.Add(new("Day CPR", analysis.CprPivot, "neutral", "cpr"));
      rows.Add(new("Day BC", analysis.CprBc, "buy", "cpr"));
    }

    if (cam.HasData)
    {
      rows.Add(new("R4", cam.H4, "sell", "cam"));
      rows.Add(new("R3", cam.H3, "sell", "cam"));
      rows.Add(new("R2", cam.H2, "sell", "cam"));
      rows.Add(new("PP", cam.Pivot, "neutral", "cam"));
      rows.Add(new("S2", cam.L2, "buy", "cam"));
      rows.Add(new("S3", cam.L3, "buy", "cam"));
      rows.Add(new("S4", cam.L4, "buy", "cam"));
    }

    return rows
      .OrderByDescending(r => r.Price)
      .ToList();
  }
}
