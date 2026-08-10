using PgAiTrading.Models;

namespace PgAiTrading.Services;

/// <summary>
/// After ApplicationTitle / ApplicationId renames, MAUI
/// <see cref="Microsoft.Maui.Storage.FileSystem.AppDataDirectory"/> moves.
/// Copy file-backed state (e.g. Auto Buy CSV) from known legacy folders once.
/// </summary>
public static class AppDataFileMigration
{
    /// <summary>Known pre-rebrand (ApplicationTitle, ApplicationId) pairs.</summary>
    public static readonly (string Title, string AppId)[] LegacyAppRoots =
    {
        ("PG One", "com.pgone.trading"),
        ("PG One Trade", "com.pgonetrade.trading"),
    };

    public const string AutoBuyFileName = "auto_buy.csv";

    public static IEnumerable<string> EnumerateLegacyFileCandidates(
        string fileName,
        string localAppDataRoot)
    {
        foreach (var (title, appId) in LegacyAppRoots)
            yield return Path.Combine(localAppDataRoot, title, appId, "Data", fileName);
    }

    /// <summary>
    /// If <paramref name="destinationPath"/> is missing or has no Auto Buy rows,
    /// copy from the first legacy CSV that still has symbols.
    /// </summary>
    /// <returns>True when a copy was written to the destination.</returns>
    public static bool TryMigrateAutoBuyCsv(
        string destinationPath,
        string? localAppDataRoot = null)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
            return false;

        var (_, existingRows) = AutoBuyCsvFile.Load(destinationPath);
        if (existingRows.Count > 0)
            return false;

        localAppDataRoot ??= Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppDataRoot))
            return false;

        string? destFull = null;
        try
        {
            destFull = Path.GetFullPath(destinationPath);
        }
        catch
        {
            // Ignore path resolution issues; still attempt legacy lookup.
        }

        foreach (var legacyPath in EnumerateLegacyFileCandidates(AutoBuyFileName, localAppDataRoot))
        {
            if (!File.Exists(legacyPath))
                continue;

            try
            {
                if (destFull is not null
                    && Path.GetFullPath(legacyPath)
                        .Equals(destFull, StringComparison.OrdinalIgnoreCase))
                    continue;
            }
            catch
            {
                // Continue with load attempt.
            }

            var (master, rows) = AutoBuyCsvFile.Load(legacyPath);
            if (rows.Count == 0)
                continue;

            AutoBuyCsvFile.Save(destinationPath, master, rows);
            return true;
        }

        return false;
    }
}
