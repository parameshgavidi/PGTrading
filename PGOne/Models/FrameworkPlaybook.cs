namespace PGOne.Models;

/// <summary>
/// Static playbook content for the Framework page — kept in sync with TradeFrameworkEvaluator.
/// </summary>
public static class FrameworkPlaybook
{
  public static readonly IReadOnlyList<FrameworkRuleGroup> Groups =
  [
    new("global", "Global gates — check first", "These run before any directional trade.", GlobalGates),
    new("rotation", "Rotation regime", "When ADX is choppy and price is inside the value area.", RotationRules),
    new("range", "Range-bound regime", "When 1H RSI(28) is between 45 and 55.", RangeBoundRules),
    new("step1", "Step 1 — Market bias", "1H SuperTrend (10,3) + current-day VWAP must align.", Step1Rules),
    new("step2", "Step 2 — Trade direction", "All must pass after Step 1.", Step2Rules),
    new("poc", "POC & value area", "Volume profile rules for bias and regime.", PocRules),
    new("step3", "Step 3 — Entry trigger", "5M SuperTrend (7, 2.5).", Step3Rules),
    new("step4", "Step 4 — Footprint confirmation (5m)", "Final layer only — never trade footprint alone.", Step4LongRules, "Long — all required"),
    new("step4s", "Step 4 — Footprint confirmation (5m)", null, Step4ShortRules, "Short — all required"),
    new("step5", "Step 5 — Exit & targets", "How to manage the trade after entry.", Step5Rules),
    new("ready", "Framework READY — master checklist", "Every item must be true for a live directional signal.", ReadyRules),
    new("scan", "Intraday stock scan", "Full NSE scan tab rules.", ScanRules),
    new("not", "Do NOT", "Avoid these mistakes.", DoNotRules)
  ];

  private static readonly IReadOnlyList<FrameworkRule> GlobalGates =
  [
    Rule("G0", "5m RSI reversal guard",
      "5m RSI(14) < 30",
      "WAIT — no new entry",
      "Possible reversal / oversold on entry timeframe."),
    Rule("G1", "Rotation inside value area",
      "ADX(1H) < 18 AND price inside current-day Value Area (between VAL and VAH)",
      "RANGE TRADE only — Keltner (20,1.5)/(20,2) fade + VWAP. Do NOT take breakout trades.",
      "Choppy trend + price accepting inside VA."),
    Rule("G2", "RSI range-bound",
      "1H RSI(28) between 45 and 55 (inclusive)",
      "RANGE TRADE only — Keltner fade + VWAP.",
      "No directional bias from RSI.")
  ];

  private static readonly IReadOnlyList<FrameworkRule> RotationRules =
  [
    Rule("R1", "ADX choppy", "ADX(14) on 1H < 18", "Weak / choppy — avoid trend breakouts"),
    Rule("R2", "Inside value area", "Price between current-day VAL and VAH", "Rotation day"),
    Rule("R3", "Playbook", "Fade extremes toward VWAP / Keltner mid", "Straddle or iron condor style on indices"),
    Rule("R4", "Stop", "Beyond Keltner outer band (20, 2)", null)
  ];

  private static readonly IReadOnlyList<FrameworkRule> RangeBoundRules =
  [
    Rule("RB1", "RSI neutral", "1H RSI(28) ≥ 45 AND ≤ 55", "No long/short momentum"),
    Rule("RB2", "Playbook", "Keltner (20,1.5)/(20,2) mean-reversion on 5m + VWAP", null),
    Rule("RB3", "Target", "Mid-range / VWAP", null),
    Rule("RB4", "Stop", "Beyond Keltner (20, 2)", null)
  ];

  private static readonly IReadOnlyList<FrameworkRule> Step1Rules =
  [
    Rule("S1-L", "Bullish market bias (LONG setup)",
      "1H SuperTrend (10,3) = Buy AND price ≥ current-day VWAP",
      "Market bias = Bullish",
      "VWAP is session-anchored on 5m candles."),
    Rule("S1-S", "Bearish market bias (SHORT setup)",
      "1H SuperTrend (10,3) = Sell AND price < current-day VWAP",
      "Market bias = Bearish", null),
    Rule("S1-F", "Fails when",
      "1H ST bullish but price below VWAP (or bearish ST but above VWAP)",
      "WAIT — bias not aligned", null)
  ];

  private static readonly IReadOnlyList<FrameworkRule> Step2Rules =
  [
    Rule("S2-1", "15M SuperTrend aligned",
      "15M SuperTrend (10,3) same direction as market bias",
      "Required", null),
    Rule("S2-2", "ADX not choppy",
      "ADX(14) on 1H ≥ 18",
      "Blocks if choppy", "< 18 = choppy / weak"),
    Rule("S2-3", "ADX minimum for trade",
      "ADX(14) on 1H ≥ 20 (Minimum ADX setting)",
      "Required for trend trade", "18–19 = moderate but below trade threshold"),
    Rule("S2-4", "RSI long momentum",
      "For LONG: 1H RSI(28) strictly > 55 (55 = range)",
      "Required for long direction", null),
    Rule("S2-5", "RSI short momentum",
      "For SHORT: 1H RSI(28) strictly < 45 (45 = range)",
      "Required for short direction", null),
    Rule("S2-6", "POC bull confirm",
      "For LONG: price > current-day POC (or > prev-day POC if no session profile)",
      "POC bullish", "Above POC = bull"),
    Rule("S2-7", "POC bear confirm",
      "For SHORT: price < current-day POC (or < prev-day POC if no session profile)",
      "POC bearish", "Below POC = bear"),
    Rule("S2-8", "Trend day — long",
      "When ADX(1H) > 25: LONG requires price above VAH (outside value area)",
      "Strong trend day acceptance", null),
    Rule("S2-9", "Trend day — short",
      "When ADX(1H) > 25: SHORT requires price below VAL (outside value area)",
      "Strong trend day acceptance", null)
  ];

  private static readonly IReadOnlyList<FrameworkRule> PocRules =
  [
    Rule("POC-1", "Above POC", "Price > POC", "Bullish bias", "Uses current session POC; falls back to prev day"),
    Rule("POC-2", "Below POC", "Price < POC", "Bearish bias", null),
    Rule("POC-3", "Inside VA", "VAL ≤ price ≤ VAH", "Rotation context when ADX < 18"),
    Rule("POC-4", "Above VAH", "Price > VAH", "Outside VA — trend day context when ADX > 25"),
    Rule("POC-5", "Below VAL", "Price < VAL", "Outside VA — trend day context when ADX > 25"),
    Rule("POC-6", "CPR narrow + open outside VA",
      "Narrow CPR (width < 0.35% of pivot) AND session open outside VAH/VAL",
      "Strong trend day probability ↑", "Score bonus in app"),
    Rule("POC-7", "Targets (always prev day)",
      "LONG targets: Prev VAH, Prev POC | SHORT targets: Prev VAL, Prev POC",
      "Step 5 exit levels", "Not current-day POC for targets")
  ];

  private static readonly IReadOnlyList<FrameworkRule> Step3Rules =
  [
    Rule("S3-1", "Entry SuperTrend",
      "5M SuperTrend (7, 2.5) = same direction as trade (Buy for long, Sell for short)",
      "Entry triggered", "Separate from display 5m ST on chart"),
    Rule("S3-2", "Preferred entry",
      "Pullback to VWAP in direction of bias",
      "BUY ON PULLBACK / SELL ON RALLY", "Continuation entry, not chase"),
    Rule("S3-3", "Fails when",
      "5M entry ST not flipped yet",
      "WAIT — await entry ST", null)
  ];

  private static readonly IReadOnlyList<FrameworkRule> Step4LongRules =
  [
    Rule("FP-L1", "Positive delta", "Net aggressive buying on 5m (volume proxy)", "Required", null),
    Rule("FP-L2", "Stacked buy imbalances", "≥ 3 consecutive 5m bars with buy-side imbalance (1.4× ratio)", "Required", null),
    Rule("FP-L3", "No absorption against long",
      "No heavy selling absorbed at resistance failing to break higher",
      "Required", "Avoid entering into opposing liquidity"),
    Rule("FP-L4", "Fails when", "Any of the above missing", "WAIT — footprint not confirmed", null)
  ];

  private static readonly IReadOnlyList<FrameworkRule> Step4ShortRules =
  [
    Rule("FP-S1", "Negative delta", "Net aggressive selling on 5m", "Required", null),
    Rule("FP-S2", "Stacked sell imbalances", "≥ 3 consecutive 5m bars with sell-side imbalance", "Required", null),
    Rule("FP-S3", "No absorption against short",
      "No heavy buying absorbed at support failing to break lower",
      "Required", null),
    Rule("FP-S4", "Fails when", "Any of the above missing", "WAIT — footprint not confirmed", null)
  ];

  private static readonly IReadOnlyList<FrameworkRule> Step5Rules =
  [
    Rule("E1", "Profit targets — long", "Previous-day VAH and Previous-day POC", "Primary targets", null),
    Rule("E2", "Profit targets — short", "Previous-day VAL and Previous-day POC", "Primary targets", null),
    Rule("E3", "Risk reward", "1 : 2 if no profile levels", "Fallback target", null),
    Rule("E4", "Stop / exit — directional",
      "5M SuperTrend (7, 2.5) — exit on candle close through ST level",
      "Trailing stop on 5m", null),
    Rule("E5", "Stop — range trades", "Beyond Keltner (20, 2) outer band", "Rotation / range-bound only", null)
  ];

  private static readonly IReadOnlyList<FrameworkRule> ReadyRules =
  [
    Rule("OK-1", "No reversal guard", "5m RSI(14) ≥ 30", "✓", null),
    Rule("OK-2", "Not rotation regime", "NOT (ADX < 18 inside VA)", "✓", null),
    Rule("OK-3", "Not range-bound", "1H RSI(28) NOT between 45–55", "✓", null),
    Rule("OK-4", "Market bias", "Step 1 passed", "✓", null),
    Rule("OK-5", "Trade direction", "Step 2 passed (15M ST + ADX + RSI + POC + trend-day VA if ADX>25)", "✓", null),
    Rule("OK-6", "Entry triggered", "5M ST (7,2.5) aligned", "✓", null),
    Rule("OK-7", "Footprint confirmed", "Step 4 passed for direction", "✓", null),
    Rule("OK-8", "Signal", "Dashboard / Signals show entry + prev-day targets + confidence %", "TRADE", null)
  ];

  private static readonly IReadOnlyList<FrameworkRule> ScanRules =
  [
    Rule("SC-1", "Same as Framework READY", "All master checklist items above", "Stock appears in intraday scan", null),
    Rule("SC-2", "Direction filter", "LONG MIS buy orders only (no auto short stock orders)", "BUY MIS ~₹5,000 notional", null),
    Rule("SC-3", "Scanner page", "Nifty top-weight watchlist — shows framework + footprint columns", "Alignment = FrameworkReady", null)
  ];

  private static readonly IReadOnlyList<FrameworkRule> DoNotRules =
  [
    Rule("N1", "Footprint alone", "Never enter on delta/imbalance without full framework", null, null),
    Rule("N2", "Breakout in rotation", "ADX < 18 inside VA — no breakout trades", null, null),
    Rule("N3", "Chase", "Do not enter without 5M entry ST trigger", null, null),
    Rule("N4", "Ignore 5m RSI", "Below 30 = stand aside for new entries", null, null),
    Rule("N5", "Long-term scan", "Uses separate fundamentals + daily/weekly ST — not this playbook", null, null)
  ];

  public static readonly IReadOnlyList<(string Band, string Range, string Meaning, string Css)> AdxBands =
  [
    ("Choppy", "< 18", "Weak trend — no directional trades", "sell"),
    ("Moderate", "18 – 25", "Trade allowed if ≥ 20 minimum", "neutral"),
    ("Strong", "> 25", "Trend day — price should be outside value area", "buy")
  ];

  public static readonly IReadOnlyList<(string Band, string Range, string Meaning, string Css)> RsiBands =
  [
    ("Long", "> 55", "Bullish momentum on 1H", "buy"),
    ("Short", "< 45", "Bearish momentum on 1H", "sell"),
    ("Range", "45 – 55", "Range-bound — Keltner fade", "neutral")
  ];

  public const string LastValidated = "26 Jul 2026";

  public static readonly string UnitTestCommand =
    "dotnet test PGOne.Framework.Tests/PGOne.Framework.Tests.csproj";

  /// <summary>Code ↔ playbook alignment verified in TradeFrameworkEvaluatorTests + manual audit.</summary>
  public static readonly IReadOnlyList<string> RevalidationChecklist =
  [
    "G0 — 5m RSI(14) < 30 sets WaitForReversal; IsFrameworkReady false; no new directional entry",
    "G1 — ADX(1H) < 18 AND price inside current-day VA → IsRotationRegime; FrameworkReady false; score capped at 45",
    "G2 — 1H RSI(28) 45–55 inclusive → IsRangebound; GetTradeDirection returns Neutral; FrameworkReady false",
    "RSI long requires strictly > 55 (RsiConfirmsLong); RSI 55 is range, not long",
    "RSI short requires strictly < 45 (RsiConfirmsShort); RSI 45 is range, not short",
    "Step 1 — 1H ST (10,3) + current-day VWAP must align for market bias",
    "Step 2 — 15M ST (10,3) must match market bias; ADX(1H) ≥ 18 (not choppy) and ≥ 20 (minimum trade)",
    "Step 2 — POC: price above POC = bull, below = bear; prev-day POC fallback when no session profile",
    "Step 2 — ADX(1H) > 25: long needs price above VAH, short needs price below VAL",
    "Step 3 — 5M entry ST (7, 2.5) via TrailingStopDefaults; must match trade direction",
    "Step 4 — Footprint delta + stacked imbalances + no opposing absorption on 5m",
    "Step 5 — Targets: prev-day POC / VAH / VAL; stop on 5M ST (7, 2.5) reversal",
    "Framework READY — IsFrameworkReady blocks reversal, rotation, range-bound, and missing entry/footprint",
    "Intraday scan — IntradayFrameworkEvaluator.IsSatisfied uses FrameworkReady; long MIS only",
    "Long-term scan — unchanged (LongTermFrameworkService); not this playbook"
  ];

  public static readonly IReadOnlyList<(string Issue, string Fix)> BugsFixed =
  [
    ("RSI 55 allowed long / RSI 45 allowed short",
      "Strict boundaries: long > 55, short < 45; 45–55 inclusive is range via IsRangebound()"),
    ("FrameworkReady did not block range-bound regime",
      "IsFrameworkReady now requires !isRangebound; score 45 when range-bound or rotation"),
    ("Range-bound status unclear in UI",
      "GetBlockingReason returns \"Range-bound — 1H RSI(28) between 45–55\"; FrameworkStatus surfaced on Dashboard"),
    ("POC missing on thin session data",
      "TpoConfirmationEvaluator falls back to prev-day POC when session profile has no data"),
    ("ADX choppy not explicit in trade-direction gate",
      "GetTradeDirection returns Neutral when ADX < 18 before minimum-ADX check")
  ];

  public static readonly IReadOnlyList<string> KnownLimitations =
  [
    "NIFTY index candles have zero volume — footprint and POC use OHLCV proxies on indices",
    "Chart overlay 5m SuperTrend display still uses (10, 3) in MarketDataService.AttachSuperTrend; signals use (7, 2.5) for entry",
    "StrategyConfig.EntryMode is not wired into evaluator logic",
    "Unit tests require local dotnet SDK — run command above before session if you changed framework code"
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
