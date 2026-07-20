namespace PGOne.Models;

/// <summary>
/// Fixed framework parameters for Holdings intraday/long-term checks.
/// Strategy page settings do not override these values.
/// </summary>
public static class FrameworkDefaults
{
    public static StrategyConfig Intraday { get; } = new();

    public static LongTermStrategyConfig LongTerm { get; } = new();
}
