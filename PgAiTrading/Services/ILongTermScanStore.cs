using PgAiTrading.Models;

namespace PgAiTrading.Services;

/// <summary>
/// Persists the latest long-term scan so the Watchlist tab can show results instantly
/// while a fresh scan runs in the background.
/// </summary>
public interface ILongTermScanStore
{
    string DescribeLocation(string userId);

    Task<LongTermScanDocument?> LoadAsync(string userId, CancellationToken cancellationToken = default);

    Task SaveAsync(string userId, LongTermScanDocument document, CancellationToken cancellationToken = default);
}

/// <summary>AppData JSON file — same layout as Auto Buy store.</summary>
public sealed class LocalFileLongTermScanStore : ILongTermScanStore
{
    public const string JsonFileName = "long_term_scan.json";

    private readonly string _appDataDirectory;

    public LocalFileLongTermScanStore(string appDataDirectory)
    {
        _appDataDirectory = appDataDirectory;
    }

    public string DescribeLocation(string userId) => GetJsonPath(userId);

    public Task<LongTermScanDocument?> LoadAsync(string userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(LongTermScanJsonFile.Load(GetJsonPath(userId)));
    }

    public Task SaveAsync(string userId, LongTermScanDocument document, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        document.Version = LongTermScanDocument.CurrentVersion;
        LongTermScanJsonFile.Save(GetJsonPath(userId), document);
        return Task.CompletedTask;
    }

    private string GetJsonPath(string userId)
    {
        if (IsLocalUser(userId))
            return Path.Combine(_appDataDirectory, JsonFileName);

        var safe = SanitizeUserId(userId);
        return Path.Combine(_appDataDirectory, "users", safe, JsonFileName);
    }

    private static bool IsLocalUser(string userId) =>
        string.IsNullOrWhiteSpace(userId)
        || userId.Equals("local", StringComparison.OrdinalIgnoreCase);

    private static string SanitizeUserId(string userId)
    {
        var chars = userId.Trim().Select(c =>
            char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_').ToArray();
        var safe = new string(chars);
        return string.IsNullOrEmpty(safe) ? "user" : safe;
    }
}
