namespace PGOneTrade.Models;

/// <summary>UI theme identifiers persisted in <see cref="AppSettings.Theme"/>.</summary>
public static class AppThemes
{
    public const string Black = "black";
    public const string Classic = "classic";

    public static string Normalize(string? theme) =>
        string.Equals(theme, Classic, StringComparison.OrdinalIgnoreCase)
            ? Classic
            : Black;

    public static bool IsClassic(string? theme) =>
        string.Equals(Normalize(theme), Classic, StringComparison.OrdinalIgnoreCase);

    public static string DisplayName(string? theme) =>
        IsClassic(theme) ? "White & Blue (Classic)" : "Black";
}
