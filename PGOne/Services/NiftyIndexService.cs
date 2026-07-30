using System.Text;
using PGOne.Models;

namespace PGOne.Services;

public interface INiftyIndexService
{
    Task<IReadOnlyList<string>> GetNifty500SymbolsAsync(CancellationToken cancellationToken = default);
}

public class NiftyIndexService : INiftyIndexService
{
    private const string NseNifty500Url = "https://nsearchives.nseindia.com/content/indices/ind_nifty500list.csv";
    private static readonly string BundledCsvPath = Path.Combine(AppContext.BaseDirectory, "Data", "nifty500.csv");

    private readonly HttpClient _http;
    private IReadOnlyList<string>? _cached;

    public NiftyIndexService()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
    }

    public async Task<IReadOnlyList<string>> GetNifty500SymbolsAsync(CancellationToken cancellationToken = default)
    {
        if (_cached is { Count: > 0 })
            return _cached;

        var online = await TryFetchFromNseAsync(cancellationToken);
        if (online.Count > 0)
        {
            _cached = online;
            return _cached;
        }

        var bundled = LoadFromCsvFile(BundledCsvPath);
        _cached = bundled.Count > 0 ? bundled : NiftyConstituents.ScanUniverse.ToList();
        return _cached;
    }

    private async Task<List<string>> TryFetchFromNseAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.GetAsync(NseNifty500Url, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return [];

            var csv = await response.Content.ReadAsStringAsync(cancellationToken);
            return ParseNifty500Csv(csv);
        }
        catch
        {
            return [];
        }
    }

    private static List<string> LoadFromCsvFile(string path)
    {
        try
        {
            if (!File.Exists(path))
                return [];

            return ParseNifty500Csv(File.ReadAllText(path));
        }
        catch
        {
            return [];
        }
    }

    internal static List<string> ParseNifty500Csv(string csv)
    {
        var symbols = new List<string>();
        using var reader = new StringReader(csv);
        _ = reader.ReadLine();

        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var parts = ParseCsvLine(line);
            if (parts.Length < 3)
                continue;

            var symbol = parts[2].Trim();
            if (string.IsNullOrWhiteSpace(symbol))
                continue;

            if (parts.Length > 3 && !parts[3].Equals("EQ", StringComparison.OrdinalIgnoreCase))
                continue;

            symbols.Add(symbol.ToUpperInvariant());
        }

        return symbols
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string[] ParseCsvLine(string line)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        foreach (var ch in line)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                parts.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        parts.Add(current.ToString().Trim());
        return parts.ToArray();
    }
}
