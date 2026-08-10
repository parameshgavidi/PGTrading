using System.Text;
using PGOneTrade.Models;

namespace PGOneTrade.Services;

public interface INiftyIndexService
{
    Task<IReadOnlyList<string>> GetNifty50SymbolsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetNifty500SymbolsAsync(CancellationToken cancellationToken = default);
}

public class NiftyIndexService : INiftyIndexService
{
    private const string NseNifty50Url = "https://nsearchives.nseindia.com/content/indices/ind_nifty50list.csv";
    private const string NseNifty500Url = "https://nsearchives.nseindia.com/content/indices/ind_nifty500list.csv";
    private static readonly string BundledNifty50Path = Path.Combine(AppContext.BaseDirectory, "Data", "nifty50.csv");
    private static readonly string BundledNifty500Path = Path.Combine(AppContext.BaseDirectory, "Data", "nifty500.csv");

    private readonly HttpClient _http;
    private IReadOnlyList<string>? _cached50;
    private IReadOnlyList<string>? _cached500;

    public NiftyIndexService()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
    }

    public async Task<IReadOnlyList<string>> GetNifty50SymbolsAsync(CancellationToken cancellationToken = default)
    {
        if (_cached50 is { Count: > 0 })
            return _cached50;

        var bundled = LoadFromCsvFile(BundledNifty50Path);
        if (bundled.Count >= 45)
        {
            _cached50 = bundled;
            return _cached50;
        }

        var online = await TryFetchFromNseAsync(NseNifty50Url, cancellationToken);
        if (online.Count >= 45)
        {
            _cached50 = online;
            return _cached50;
        }

        _cached50 = NiftyConstituents.TopWeightage.ToList();
        return _cached50;
    }

    public async Task<IReadOnlyList<string>> GetNifty500SymbolsAsync(CancellationToken cancellationToken = default)
    {
        if (_cached500 is { Count: > 0 })
            return _cached500;

        var online = await TryFetchFromNseAsync(NseNifty500Url, cancellationToken);
        if (online.Count > 0)
        {
            _cached500 = online;
            return _cached500;
        }

        var bundled = LoadFromCsvFile(BundledNifty500Path);
        _cached500 = bundled.Count > 0 ? bundled : NiftyConstituents.ScanUniverse.ToList();
        return _cached500;
    }

    private async Task<List<string>> TryFetchFromNseAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return [];

            var csv = await response.Content.ReadAsStringAsync(cancellationToken);
            return ParseIndexCsv(csv);
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

            return ParseIndexCsv(File.ReadAllText(path));
        }
        catch
        {
            return [];
        }
    }

    public static List<string> ParseIndexCsv(string csv)
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
