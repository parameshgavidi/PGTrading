namespace PgAiTrading.Models;

/// <summary>
/// Camarilla levels active for one pivot period (TradingView Auto-style stepped history).
/// Levels come from the previous completed period H/L/C.
/// </summary>
public sealed record CamarillaSegment(
    DateTime Start,
    DateTime End,
    decimal R4,
    decimal R3,
    decimal Pivot,
    decimal S3,
    decimal S4);
