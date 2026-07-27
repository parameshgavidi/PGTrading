namespace PGOne.Models;

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

    if (cam.HasData)
    {
      rows.Add(new("H4", cam.H4, "sell", "cam"));
      rows.Add(new("H3", cam.H3, "sell", "cam"));
      rows.Add(new("H2", cam.H2, "sell", "cam"));
      rows.Add(new("PP", cam.Pivot, "neutral", "cam"));
      rows.Add(new("L2", cam.L2, "buy", "cam"));
      rows.Add(new("L3", cam.L3, "buy", "cam"));
      rows.Add(new("L4", cam.L4, "buy", "cam"));
    }

    return rows
      .OrderByDescending(r => r.Price)
      .ToList();
  }
}
