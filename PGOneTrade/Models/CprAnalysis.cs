namespace PGOneTrade.Models;

public sealed record CprAnalysis(
  string Bias,
  bool IsNarrow,
  decimal WidthPercent,
  decimal Pivot,
  decimal Top,
  decimal Bottom);
