using PgAiTrading.Models;

namespace PgAiTrading.Services;

/// <summary>
/// Per-user Auto Buy persistence. Desktop uses local JSON; a future web backend
/// can implement the same contract keyed by authenticated <paramref name="userId"/>.
/// </summary>
public interface IAutoBuyStore
{
    /// <summary>Human-readable storage location for diagnostics (path or API label).</summary>
    string DescribeLocation(string userId);

    Task<AutoBuyDocument?> LoadAsync(string userId, CancellationToken cancellationToken = default);

    Task SaveAsync(string userId, AutoBuyDocument document, CancellationToken cancellationToken = default);
}

/// <summary>Desktop single-user (and multi-profile-ready) JSON files under AppData.</summary>
public sealed class LocalFileAutoBuyStore : IAutoBuyStore
{
    public const string JsonFileName = "auto_buy.json";
    public const string LegacyCsvFileName = "auto_buy.csv";

    private readonly string _appDataDirectory;
    private readonly string _localAppDataRoot;

    /// <summary>
    /// <paramref name="appDataDirectory"/> is the app sandbox (MAUI FileSystem.AppDataDirectory).
    /// <paramref name="localAppDataRoot"/> is used only to find pre-rebrand legacy folders.
    /// </summary>
    public LocalFileAutoBuyStore(string appDataDirectory, string localAppDataRoot)
    {
        _appDataDirectory = appDataDirectory;
        _localAppDataRoot = localAppDataRoot;
    }

    public string DescribeLocation(string userId) => GetJsonPath(userId);

    public Task<AutoBuyDocument?> LoadAsync(string userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var jsonPath = GetJsonPath(userId);
        if (File.Exists(jsonPath))
        {
            var fromJson = AutoBuyJsonFile.Load(jsonPath);
            if (fromJson is not null && fromJson.Rows.Count > 0)
                return Task.FromResult<AutoBuyDocument?>(fromJson);

            // Empty JSON — try to recover from CSV / legacy folders below.
            if (fromJson is not null && fromJson.Rows.Count == 0)
            {
                var recovered = TryRecoverDocument(userId, jsonPath);
                if (recovered is not null)
                {
                    AutoBuyJsonFile.Save(jsonPath, recovered);
                    return Task.FromResult<AutoBuyDocument?>(recovered);
                }

                return Task.FromResult<AutoBuyDocument?>(fromJson);
            }
        }

        var migrated = TryRecoverDocument(userId, jsonPath);
        if (migrated is not null)
        {
            AutoBuyJsonFile.Save(jsonPath, migrated);
            return Task.FromResult<AutoBuyDocument?>(migrated);
        }

        return Task.FromResult<AutoBuyDocument?>(null);
    }

    public Task SaveAsync(string userId, AutoBuyDocument document, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        document.Version = AutoBuyDocument.CurrentVersion;
        AutoBuyJsonFile.Save(GetJsonPath(userId), document);
        return Task.CompletedTask;
    }

    private AutoBuyDocument? TryRecoverDocument(string userId, string jsonPath)
    {
        foreach (var candidate in EnumerateRecoveryCandidates(userId, jsonPath))
        {
            if (!File.Exists(candidate))
                continue;

            AutoBuyDocument? doc = null;
            if (candidate.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                doc = AutoBuyJsonFile.Load(candidate);
            else if (candidate.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                doc = AutoBuyCsvFile.ToDocument(candidate);

            if (doc is not null && doc.Rows.Count > 0)
                return doc;
        }

        return null;
    }

    private IEnumerable<string> EnumerateRecoveryCandidates(string userId, string jsonPath)
    {
        // Same AppData folder: prior CSV format for this install.
        yield return Path.Combine(Path.GetDirectoryName(jsonPath) ?? _appDataDirectory, LegacyCsvFileName);

        // Pre-rebrand ApplicationTitle / ApplicationId folders.
        foreach (var (title, appId) in LegacyAppRoots)
        {
            var dataDir = Path.Combine(_localAppDataRoot, title, appId, "Data");
            yield return Path.Combine(dataDir, JsonFileName);
            yield return Path.Combine(dataDir, LegacyCsvFileName);
        }

        // Flat local user path if caller used a non-local user id later.
        if (!IsLocalUser(userId))
        {
            yield return Path.Combine(_appDataDirectory, JsonFileName);
            yield return Path.Combine(_appDataDirectory, LegacyCsvFileName);
        }
    }

    internal static readonly (string Title, string AppId)[] LegacyAppRoots =
    {
        ("PG One", "com.pgone.trading"),
        ("PG One Trade", "com.pgonetrade.trading"),
    };

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

/// <summary>Desktop default until auth exists; web should supply the signed-in user id.</summary>
public interface IUserContext
{
    string UserId { get; }
}

public sealed class LocalUserContext : IUserContext
{
    public string UserId => "local";
}
