namespace PGOne.Models;

/// <summary>
/// CPR (TC / Pivot / BC) levels active for a 15-minute window on a 1-minute chart.
/// Levels are derived from the previous completed 15-minute bar.
/// </summary>
public sealed record IntradayCprSegment(
    DateTime Start,
    DateTime End,
    decimal Pivot,
    decimal Tc,
    decimal Bc);
