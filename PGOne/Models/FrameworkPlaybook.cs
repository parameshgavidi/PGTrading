namespace PGOne.Models;

/// <summary>
/// Static playbook content for the Framework page — kept in sync with TradeFrameworkEvaluator.
/// </summary>
public static class FrameworkPlaybook
{
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
      "1H ST bullish but price below session VWAP (or bearish ST but above session VWAP)",
      "WAIT — Step 1 incomplete; FrameworkStatus names the conflict",
      "AI checklist: 1H ST can pass while VWAP fails. Both must align for market bias.")
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
      "Step 5 exit levels", "Not current-day POC for targets"),
    Rule("POC-8", "Dashboard — current VA label",
      "Price > POC AND > VAH → Bullish | Price < POC AND < VAL → Bearish | else Neutral",
      "Shown as label only (no numbers)", "Multi-timeframe table row"),
    Rule("POC-9", "Dashboard — prev VA label",
      "Price > prev POC AND > prev VAH → Bullish | Price < prev POC AND < prev VAL → Bearish",
      "Shown as label only", "Uses previous session levels")
  ];

  private static readonly IReadOnlyList<FrameworkRule> VaBiasRules =
  [
    Rule("VA-1", "Current session — Bullish",
      "Price > current POC AND price > current VAH",
      "Bullish", "Both must be true"),
    Rule("VA-2", "Current session — Bearish",
      "Price < current POC AND price < current VAL",
      "Bearish", "Both must be true"),
    Rule("VA-3", "Current session — Neutral",
      "Inside value area OR above POC but below VAH OR below POC but above VAL",
      "Neutral", "No directional VA label"),
    Rule("VA-4", "Previous day — Bullish",
      "Price > prev POC AND price > prev VAH",
      "Bullish", "Prev POC / VAH / VAL row"),
    Rule("VA-5", "Previous day — Bearish",
      "Price < prev POC AND price < prev VAL",
      "Bearish", null),
    Rule("VA-6", "Display",
      "Dashboard shows Bullish / Bearish / Neutral only — levels hidden",
      "Multi-timeframe table", null)
  ];

  private static readonly IReadOnlyList<FrameworkRule> CamarillaRules =
  [
    Rule("CAM-0", "Levels (prev day H, L, C)",
      "H4/H3/H2 / PP / L2/L3/L4 — H1 and L1 hidden (mild S/R, not in UI)",
      "Context layer alongside CPR and POC/VA", "Range = H − L"),
    Rule("CAM-1", "H4", "C + range × 1.1 / 2", "Extension / blow-off", null),
    Rule("CAM-2", "H3", "C + range × 1.1 / 4", "Strong resistance / breakout band", null),
    Rule("CAM-3", "H2", "C + range × 1.1 / 6", "Resistance", null),
    Rule("CAM-4", "PP", "(H + L + C) / 3", "Pivot", null),
    Rule("CAM-5", "L2", "C − range × 1.1 / 6", "Support", null),
    Rule("CAM-6", "L3", "C − range × 1.1 / 4", "Strong support / breakdown band", null),
    Rule("CAM-7", "L4", "C − range × 1.1 / 2", "Extension / breakdown", null),
    Rule("CAM-8", "Camarilla bias (label rule)",
      "Bullish: price > PP AND > H2 | Bearish: price < PP AND < L2",
      "Supports Step 1 market bias", "No H1/L1 in rules"),
    Rule("CAM-9", "Camarilla band (label rule)",
      "Bullish: price > H3 | Bearish: price < L3 | else inside L3–H3",
      "Breakout / trend context", "Pairs with ADX>25 outside VA"),
    Rule("CAM-10", "Step 2 filter",
      "LONG: POC/VA bullish AND not trapped below L3 | SHORT: POC/VA bearish AND not trapped above H3",
      "Trade direction filter", null),
    Rule("CAM-11", "Step 3 entry zones",
      "LONG pullback to VWAP or PP | SHORT rally to VWAP or PP + 5M ST (7,2.5)",
      "No chase at H4/L4", null),
    Rule("CAM-12", "Step 5 targets",
      "LONG: H2 → H3 → H4 | SHORT: L2 → L3 → L4",
      "Alongside prev POC/VAH/VAL", "Stop: 5M ST reversal or back through PP")
  ];

  private static readonly IReadOnlyList<FrameworkRule> FootprintDetailRules =
  [
    Rule("FP-N1", "Data source",
      "Last 8 × 5m candles; bias from trade direction (or market bias if direction neutral)",
      "Final confirmation only", null),
    Rule("FP-N2", "Delta calculation",
      "Sum buy-volume proxy − sell-volume proxy per bar",
      "Positive = Delta + | Negative = Delta − | Zero = Delta flat", null),
    Rule("FP-N3", "Volume split (normal)",
      "Buy vol = volume × (Close−Low)/(High−Low) | Sell vol = volume × (High−Close)/(High−Low)",
      "Classic candle position proxy", "Requires non-zero volume"),
    Rule("FP-N4", "Volume split (zero volume / indices)",
      "When volume = 0: use bar range (or |body|) as activity proxy, then same split",
      "Shows \"range proxy (no volume)\" in summary", "NIFTY index candles"),
    Rule("FP-N5", "Delta flat — when correct",
      "Balanced buy/sell over 8 bars OR doji-heavy session with net zero",
      "Not confirmed for long or short", "Common on indices before proxy fix"),
    Rule("FP-N6", "Stacked imbalance",
      "≥ 3 consecutive bars where buy > sell × 1.4 (or sell > buy × 1.4)",
      "Required for confirmation", null),
    Rule("FP-N7", "Absorption against long",
      "Last bar: vol ≥ 1.5× avg, near recent high, red close, close near low of bar",
      "Blocks long confirmation", null),
    Rule("FP-N8", "Absorption against short",
      "Last bar: vol ≥ 1.5× avg, near recent low, green close, close near high of bar",
      "Blocks short confirmation", null),
    Rule("FP-N9", "Long confirmed",
      "Delta + AND stacked buy imbalances AND NOT absorption vs long",
      "ConfirmsLong", null),
    Rule("FP-N10", "Short confirmed",
      "Delta − AND stacked sell imbalances AND NOT absorption vs short",
      "ConfirmsShort", null),
    Rule("FP-N11", "Flow opposes bias",
      "Footprint delta/flow opposes trade direction (or market bias when direction neutral)",
      "WAIT — AI Analysis \"Flow Conflict\"; setup score capped at 68%",
      "Checklist shows fail when flow opposes; does not confirm Step 4")
  ];

  private static readonly IReadOnlyList<FrameworkRule> DashboardRules =
  [
    Rule("DB-1", "Market bias row", "1H ST (10,3) + VWAP alignment", "Bullish / Bearish label", null),
    Rule("DB-2", "Trade direction row", "Step 2 passed or blocking reason", "Long / Short / Wait", null),
    Rule("DB-3", "POC bias row", "Price vs POC (current or prev POC fallback)", "Tpo.Summary + Bullish/Bearish", null),
    Rule("DB-4", "POC / VAH / VAL row", "Price > POC AND > VAH → Bullish; < POC AND < VAL → Bearish", "Labels only — numbers hidden", null),
    Rule("DB-5", "Prev POC / VAH / VAL row", "Same rules on prev-day POC / VAH / VAL", "Labels only", null),
    Rule("DB-6", "RSI(28) bias", "> 55 Bullish | < 45 Bearish | 45–55 range", "1H only", null),
    Rule("DB-7", "Footprint row", "Step 4 summary (delta, imbalances, absorption)", "Not confirmed until all pass", null),
    Rule("DB-8", "Framework row", "FrameworkReady + FrameworkStatus", "Master gate", null),
    Rule("DB-9", "CPR row", "Narrow CPR + open outside VA → strong trend day bonus", "Separate from Camarilla", null),
    Rule("DB-10", "AI Analysis — checklist",
      "1H ST pass when directional (independent of VWAP); VWAP pass only when Step 1 aligned; footprint fail when flow opposes bias",
      "8 checks mirror framework steps", null),
    Rule("DB-11", "AI Analysis — probability label",
      "Setup Score when not ready; Entry Probability when FrameworkReady",
      "Footprint conflict caps score ≤ 68%", null),
    Rule("DB-12", "AI Analysis — Suggested",
      "WAIT until FrameworkReady (or RANGE ONLY for rotation/range-bound); structured BUY/SELL only when all 5 steps align",
      "Step 1 conflict example: bullish ST below VWAP → wait for reclaim", null),
    Rule("DB-13", "Step 1 status text",
      "Bullish ST below VWAP → \"Step 1 — 1H ST bullish but price below session VWAP\"",
      "Bearish mirror for above VWAP", "Shown in Framework row and AI Suggested detail")
  ];

  private static readonly IReadOnlyList<FrameworkRule> CprRules =
  [
    Rule("CPR-1", "Pivot", "(Prev High + Prev Low + Prev Close) / 3", "Central pivot", null),
    Rule("CPR-2", "TC / BC", "Top and bottom central pivot lines", "Narrow width < 0.35% of pivot = narrow CPR", null),
    Rule("CPR-3", "Bias", "Price above pivot → Bullish CPR | below → Bearish", "Dashboard CPR row", null),
    Rule("CPR-4", "Strong trend day",
      "Narrow CPR AND session open outside VAH/VAL",
      "Score bonus + Tpo.StrongTrendDay", "Works with POC/VA rules")
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
    Rule("SC-1", "Universe", "Nifty 50 only (~50 stocks from bundled list)", "Fast scan vs full NSE", null),
    Rule("SC-2", "Phase 1 screen", "Parallel quick screen: 1H ST + session VWAP + 5m RSI/ADX/rotation gates", "Candidates only pass Step 1 bias", null),
    Rule("SC-3", "Phase 2 framework", "Full 5-step framework on phase-1 candidates (15m/5m/footprint)", "FrameworkReady required for match", null),
    Rule("SC-4", "Direction filter", "LONG MIS buy orders only (no auto short stock orders)", "BUY MIS ~₹5,000 notional", null),
    Rule("SC-5", "Scanner page", "Nifty top-weight watchlist — shows framework + footprint columns", "Alignment = FrameworkReady", null)
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

  public static readonly IReadOnlyList<(string Band, string Range, string Meaning, string Css)> VaBiasBands =
  [
    ("Bullish", "> POC and > VAH", "Above value area — acceptance higher", "buy"),
    ("Bearish", "< POC and < VAL", "Below value area — acceptance lower", "sell"),
    ("Neutral", "Between levels", "Inside VA or mixed (e.g. above POC below VAH)", "neutral")
  ];

  public static readonly IReadOnlyList<(string Level, string Formula, string Role)> CamarillaLevels =
  [
    ("H4", "C + range × 1.1 / 2", "Extension up"),
    ("H3", "C + range × 1.1 / 4", "Strong resistance / band"),
    ("H2", "C + range × 1.1 / 6", "Resistance"),
    ("PP", "(H + L + C) / 3", "Pivot"),
    ("L2", "C − range × 1.1 / 6", "Support"),
    ("L3", "C − range × 1.1 / 4", "Strong support / band"),
    ("L4", "C − range × 1.1 / 2", "Extension down")
  ];

  public const string LastValidated = "30 Jul 2026";

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
    "Step 1 — 1H ST (10,3) + session VWAP on 5m must align; conflict → WAIT with explicit Step 1 status",
    "AI Analysis — 1H ST checklist pass independent of VWAP; VWAP fail when ST/VWAP conflict",
    "AI Analysis — footprint opposes bias → WAIT, Flow Conflict, score capped at 68%",
    "AI Analysis — Setup Score vs Entry Probability label based on FrameworkReady",
    "Step 2 — 15M ST (10,3) must match market bias; ADX(1H) ≥ 18 (not choppy) and ≥ 20 (minimum trade)",
    "Step 2 — POC: price above POC = bull, below = bear; prev-day POC fallback when no session profile",
    "Step 2 — ADX(1H) > 25: long needs price above VAH, short needs price below VAL",
    "Step 3 — 5M entry ST (7, 2.5) via TrailingStopDefaults; must match trade direction",
    "Step 4 — Footprint delta + stacked imbalances + no opposing absorption on 5m",
    "Footprint on indices: range proxy when candle volume is zero (see Footprint detail)",
    "Dashboard POC/VA rows: Bullish/Bearish labels only (above POC+VAH / below POC+VAL)",
    "Camarilla: PP + H2–H4 / L2–L4 context (H1/L1 hidden); pairs with POC/VA and CPR",
    "Step 5 — Targets: prev-day POC / VAH / VAL; stop on 5M ST (7, 2.5) reversal",
    "Framework READY — IsFrameworkReady blocks reversal, rotation, range-bound, and missing entry/footprint",
    "Intraday scan — Nifty 50 two-phase (1H+VWAP screen → full framework on candidates); FrameworkReady; long MIS only",
    "Long-term scan — separate daily/weekly ST + fundamentals playbook (Watchlist tab)"
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
      "GetTradeDirection returns Neutral when ADX < 18 before minimum-ADX check"),
    ("Step 1 AI checklist marked 1H ST warn when only VWAP conflicted",
      "1H ST pass when directional; VWAP fail when opposing ST; clearer Step 1 FrameworkStatus text"),
    ("Intraday scan evaluated all NSE equities (slow)",
      "Nifty 50 two-phase scan: parallel Step 1 screen then full framework on candidates only"),
    ("AI Suggested action unstructured",
      "Structured WAIT/BUY/SELL with ActionDetail; footprint conflict and Step 1 blocking reasons")
  ];

  public static readonly IReadOnlyList<string> KnownLimitations =
  [
    "NIFTY index candles have zero volume — footprint uses range proxy; POC uses OHLCV proxies",
    "Footprint \"Delta flat\" = net buy/sell proxy balanced over 8 bars (or before range proxy on zero volume)",
    "Chart overlay 5m SuperTrend display still uses (10, 3) in MarketDataService.AttachSuperTrend; signals use (7, 2.5) for entry",
    "StrategyConfig.EntryMode is not wired into evaluator logic",
    "Unit tests require local dotnet SDK — run command above before session if you changed framework code"
  ];

  // Declared after all rule lists — static init order would leave Rules null if placed at top.
  public static readonly IReadOnlyList<FrameworkRuleGroup> Groups =
  [
    new("global", "Global gates — check first", "These run before any directional trade.", GlobalGates),
    new("rotation", "Rotation regime", "When ADX is choppy and price is inside the value area.", RotationRules),
    new("range", "Range-bound regime", "When 1H RSI(28) is between 45 and 55.", RangeBoundRules),
    new("step1", "Step 1 — Market bias", "1H SuperTrend (10,3) + current-day VWAP must align.", Step1Rules),
    new("step2", "Step 2 — Trade direction", "All must pass after Step 1.", Step2Rules),
    new("poc", "POC & value area", "Volume profile rules for bias and regime.", PocRules),
    new("va", "Value area labels (dashboard)", "Bullish/Bearish labels — no raw numbers on dashboard.", VaBiasRules),
    new("camarilla", "Camarilla pivot (prev day)", "Context layer — PP + H2–H4 / L2–L4. H1/L1 hidden (mild S/R).", CamarillaRules),
    new("cpr", "CPR (central pivot range)", "Prev-day pivot + TC/BC — narrow CPR trend-day context.", CprRules),
    new("footprint", "Footprint logic (5m)", "How delta, imbalances, and absorption are computed.", FootprintDetailRules),
    new("dashboard", "Dashboard multi-timeframe table", "What each sidebar row means.", DashboardRules),
    new("step3", "Step 3 — Entry trigger", "5M SuperTrend (7, 2.5).", Step3Rules),
    new("step4", "Step 4 — Footprint confirmation (5m)", "Final layer only — never trade footprint alone.", Step4LongRules, "Long — all required"),
    new("step4s", "Step 4 — Footprint confirmation (5m)", null, Step4ShortRules, "Short — all required"),
    new("step5", "Step 5 — Exit & targets", "How to manage the trade after entry.", Step5Rules),
    new("ready", "Framework READY — master checklist", "Every item must be true for a live directional signal.", ReadyRules),
    new("scan", "Intraday stock scan", "Watchlist → Intraday scan tab. Nifty 50 two-phase for speed.", ScanRules),
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
