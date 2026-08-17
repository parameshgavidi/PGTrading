namespace PgAiTrading.Models;

/// <summary>
/// Static playbook content for the Framework page — kept in sync with TradeFrameworkEvaluator.
/// Chart-only overlays (Camarilla, CPR, TPO display) are documented as display-only, not gates.
/// </summary>
public static class FrameworkPlaybook
{
  private static readonly IReadOnlyList<FrameworkRule> GlobalGates =
  [
    Rule("G0", "5m RSI oversold — expect reversal",
      "5m RSI(14) < 30",
      "EXPECT REVERSAL — watch for bounce; not a hard block alone",
      "Oversold on entry timeframe."),
    Rule("G0b", "5m RSI + bullish pattern WAIT",
      "5m RSI(14) < 30 AND any bullish candlestick pattern on 5m",
      "WAIT — no new entry",
      "Reversal likely starting — stand aside."),
    Rule("R1", "Strong chop regime",
      "1H RSI(28) between 45–55 AND ADX(1H) < 18",
      "Strong NO-TRADE for breakouts — switch to liquidity-sweep mean-reversion at VA/PDH/PDL",
      "RSI mid alone is not enough to call chop."),
    Rule("R2", "Developing / soft neutral",
      "1H RSI(28) 45–55 AND ADX > 22 → developing (wait 1H structure + 15M BOS). ADX 18–22 → soft neutral stand aside.",
      "Do NOT auto-label RSI 45–55 as chop when ADX is developing.",
      null)
  ];

  private static readonly IReadOnlyList<FrameworkRule> StructureRules =
  [
    Rule("MS-1", "1H = major market structure (direction)",
      "HH + HL → bullish | LH + LL → bearish | Mixed → consolidation/chop",
      "Top priority for direction — never decide overall structure from 5M",
      "1H alone if you pick only one TF for structure."),
    Rule("MS-2", "15M = trading structure (setup)",
      "Look for BOS in the same direction as 1H (e.g. 1H bullish → 15M breaks prior swing high)",
      "Setup confirmation", null),
    Rule("MS-3", "5M = entry / execution only",
      "Pullback → HL/LH → 5M BOS/CHOCH back with 1H/15M trend",
      "Entry trigger (alongside ST 7,2.5 as stop)",
      "5M HH/HL noise must not override 1H direction."),
    Rule("MS-4", "Alignment with SuperTrend + VWAP",
      "1H structure preferred; block when ST/VWAP hard-opposes structure",
      "Soft alignment layer", "Existing 1H ST → 15M → 5M stack still used as confirmation.")
  ];

  private static readonly IReadOnlyList<FrameworkRule> RegimeRules =
  [
    Rule("RG-1", "Trending bullish", "1H RSI(28) > 55", "Long setups / bullish spreads with structure"),
    Rule("RG-2", "Trending bearish", "1H RSI(28) < 45", "Short setups / bearish spreads with structure"),
    Rule("RG-3", "Strong chop", "RSI 45–55 + ADX < 18", "Liquidity sweeps at profile extremes — not breakout chase"),
    Rule("RG-4", "Developing", "RSI 45–55 + ADX > 22", "Wait 1H structure + 15M confirmation — not auto-chop"),
    Rule("RG-5", "Soft neutral", "RSI 45–55 + ADX 18–22", "Stand aside"),
    Rule("RG-6", "Confirm chop with",
      "1H structure mixed + ADX < 18 + 15M BOS failures + price oscillating around VWAP",
      "Higher-confidence range call", null)
  ];

  private static readonly IReadOnlyList<FrameworkRule> VolumeProfileRules =
  [
    Rule("VP-1", "Primary location tool", "Session Volume Profile — POC, VAH, VAL, HVN/LVN", "Where volume accumulated", "Preferred over TPO for liquidity levels."),
    Rule("VP-2", "Session references", "PDH / PDL + session high/low + major 15M swing highs/lows", "Liquidity magnets", null),
    Rule("VP-3", "POC bias", "Price > POC → bull | Price < POC → bear", "Location filter for trend trades", null),
    Rule("VP-4", "Trend day", "ADX ≥ 25: long prefers > VAH; short prefers < VAL (or confirmed sweep)", "Acceptance outside value", null),
    Rule("VP-5", "Targets", "Prev VAH / Prev POC (long) | Prev VAL / Prev POC (short)", "Step exit levels", null)
  ];

  private static readonly IReadOnlyList<FrameworkRule> SweepRules =
  [
    Rule("LS-1", "Definition",
      "Price takes PDH/PDL/VAH/VAL/POC/15M swing then rejects",
      "Price-action event — profile marks the level", null),
    Rule("LS-2", "Bullish sweep sequence",
      "Sweep sell-side liquidity (below PDL/VAL/swing low) → rejection → reclaim → 5M BOS/CHOCH",
      "Mean-reversion long / continuation long after flush", null),
    Rule("LS-3", "Bearish sweep sequence",
      "Sweep buy-side liquidity (above PDH/VAH/swing high) → rejection → reclaim → 5M BOS/CHOCH",
      "Mean-reversion short", null),
    Rule("LS-4", "Do not enter on sweep alone",
      "Require reclaim + 5M structure shift",
      "WAIT until sequence completes", null),
    Rule("LS-5", "Strong-chop playbook",
      "RSI 45–55 + ADX < 18 → identify VAH/VAL/PDH/PDL → wait sweep → reclaim → 5M BOS → footprint",
      "Mean-reversion mode", "More robust than treating every RSI mid period as no-trade.")
  ];

  private static readonly IReadOnlyList<FrameworkRule> FootprintRules =
  [
    Rule("FP-1", "Role", "Confirm reaction at the swept level — not the entry signal itself", "Final confirmation", null),
    Rule("FP-2", "Bullish sweep footprint",
      "Heavy selling / negative delta into the low, but price fails to continue → absorption → reclaim",
      "Sellers absorbed", "Large negative delta alone ≠ bullish."),
    Rule("FP-3", "Bearish sweep footprint",
      "Heavy buying into the high that fails to continue → absorption → reclaim",
      "Buyers absorbed", null),
    Rule("FP-4", "Trend confirmation",
      "Delta + stacked imbalances + no opposing absorption in trade direction",
      "Required for FrameworkReady", null),
    Rule("FP-5", "Combo",
      "Liquidity sweep + absorption + reclaim + structure break",
      "Highest-quality entry", null)
  ];

  private static readonly IReadOnlyList<FrameworkRule> PipelineRules =
  [
    Rule("P1", "Market Structure", "1H direction → 15M BOS setup", "Top priority", null),
    Rule("P2", "Regime", "RSI(28) + ADX on 1H", "Trend vs strong chop vs developing", null),
    Rule("P3", "Volume Profile", "POC / VAH / VAL / PDH / PDL", "Important location", null),
    Rule("P4", "Liquidity Sweep", "Sweep at reference level", "Setup", null),
    Rule("P5", "Footprint", "Delta / imbalance / absorption", "Confirmation", null),
    Rule("P6", "5M BOS", "Break of structure / CHOCH (+ ST 7,2.5 stop)", "Entry", null)
  ];

  private static readonly IReadOnlyList<FrameworkRule> TrendingRules =
  [
    Rule("T1", "Bias", "1H HH/HL (or LH/LL) + RSI >55 / <45", "Trade with structure", null),
    Rule("T2", "Setup", "15M BOS with 1H", "Required", null),
    Rule("T3", "Location", "POC confirms direction; ADX≥25 prefers outside VA", "Required", null),
    Rule("T4", "Entry", "5M pullback → BOS (+ footprint)", "Execute", null),
    Rule("T5", "Stop", "5M SuperTrend (7, 2.5) candle close through", "Trailing", null)
  ];

  private static readonly IReadOnlyList<FrameworkRule> ChartOnlyRules =
  [
    Rule("CH-1", "Camarilla pivot", "Show/hide on chart only — not a framework gate", "Display", "Do not use for FrameworkReady."),
    Rule("CH-2", "CPR (day / 1m)", "Show/hide on chart only — not a framework gate", "Display", null),
    Rule("CH-3", "TPO display", "Existing chart/POC overlay — do not remove; not a separate framework gate", "Display", "Volume Profile is the primary location tool.")
  ];

  private static readonly IReadOnlyList<FrameworkRule> ReadyRules =
  [
    Rule("OK-1", "No WAIT guard", "NOT (5m RSI < 30 AND bullish pattern)", "✓", null),
    Rule("OK-2", "Regime allows trade", "Trending, developing (with structure), or strong-chop with confirmed sweep", "✓", null),
    Rule("OK-3", "1H structure directional (or sweep MR)", "HH/HL or LH/LL — or confirmed sweep path in strong chop", "✓", null),
    Rule("OK-4", "15M setup / sweep location", "BOS aligned or sweep at profile/session level", "✓", null),
    Rule("OK-5", "Volume profile location OK", "POC / VA rules for regime", "✓", null),
    Rule("OK-6", "5M BOS (or entry ST) triggered", "Entry", "✓", null),
    Rule("OK-7", "Footprint confirmed", "Direction or sweep absorption path", "✓", null),
    Rule("OK-8", "Signal", "Dashboard / Signals show entry + targets", "TRADE", null)
  ];

  private static readonly IReadOnlyList<FrameworkRule> DoNotRules =
  [
    Rule("N1", "Decide structure from 5M", "5M swings are noise for overall bias", null, null),
    Rule("N2", "Treat RSI 45–55 alone as chop", "Require ADX < 18 for strong chop; ADX > 22 = developing", null, null),
    Rule("N3", "Enter on sweep wick alone", "Need reclaim + 5M BOS + footprint", null, null),
    Rule("N4", "Enter on footprint delta alone", "Never without structure/regime/location", null, null),
    Rule("N5", "Use Camarilla/CPR/TPO as gates", "Chart display only for now", null, null)
  ];

  public static readonly IReadOnlyList<(string Band, string Range, string Meaning, string Css)> AdxBands =
  [
    ("Choppy", "< 18", "With RSI mid → strong chop / sweep MR", "sell"),
    ("Moderate", "18 – 22", "With RSI mid → soft neutral", "neutral"),
    ("Developing", "> 22", "With RSI mid → wait structure (not auto-chop)", "neutral"),
    ("Strong", "> 25", "Trend day — prefer outside value area", "buy")
  ];

  public static readonly IReadOnlyList<(string Band, string Range, string Meaning, string Css)> RsiBands =
  [
    ("Long", "> 55", "Bullish momentum on 1H", "buy"),
    ("Short", "< 45", "Bearish momentum on 1H", "sell"),
    ("Mid", "45 – 55", "Neutral momentum — combine with ADX + structure", "neutral")
  ];

  public static readonly IReadOnlyList<(string Band, string Range, string Meaning, string Css)> VaBiasBands =
  [
    ("Bullish", "> POC and > VAH", "Above value area — acceptance higher", "buy"),
    ("Bearish", "< POC and < VAL", "Below value area — acceptance lower", "sell"),
    ("Neutral", "Between levels", "Inside VA or mixed", "neutral")
  ];

  public static readonly IReadOnlyList<(string Band, string Range, string Meaning, string Css)> StructureBands =
  [
    ("Bullish", "HH + HL", "Major bullish structure (1H)", "buy"),
    ("Bearish", "LH + LL", "Major bearish structure (1H)", "sell"),
    ("Mixed", "Conflicting swings", "Consolidation / chop", "neutral")
  ];

  public static readonly IReadOnlyList<FrameworkRuleGroup> Groups =
  [
    new("pipeline", "Decision pipeline (top → bottom)", "Enforce in this order every session.", PipelineRules),
    new("global", "Global gates", "Check before directional or sweep trades.", GlobalGates),
    new("structure", "1. Market structure (top priority)", "1H direction · 15M setup · 5M entry only.", StructureRules),
    new("regime", "2. RSI(28) + ADX — regime", "Momentum + trend strength together.", RegimeRules),
    new("vp", "3. Session Volume Profile — location", "Primary liquidity/reference levels.", VolumeProfileRules),
    new("sweep", "4. Liquidity sweep — setup", "Price-action event at profile/session levels.", SweepRules),
    new("footprint", "5. Footprint — confirmation", "What happened inside the level.", FootprintRules),
    new("trend", "Trending regime playbook", "When RSI is directional.", TrendingRules),
    new("ready", "Framework READY — master checklist", "Every item must be true for a live signal.", ReadyRules),
    new("chart", "Chart-only overlays (not framework)", "Show/hide buttons on the chart. Do not use as gates.", ChartOnlyRules),
    new("not", "Do NOT", "Avoid these mistakes.", DoNotRules)
  ];

  private static FrameworkRule Rule(
    string id,
    string title,
    string condition,
    string? action = null,
    string? note = null) =>
    new(id, title, condition, action, note);
}

public sealed record FrameworkRule(string Id, string Title, string Condition, string? Action, string? Note);

public sealed record FrameworkRuleGroup(
  string Key,
  string Title,
  string? Intro,
  IReadOnlyList<FrameworkRule> Rules,
  string? Subtitle = null);
