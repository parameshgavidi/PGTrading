using FuzzySharp;

namespace PGOne.Services;

public sealed class NiftyStockDirectory
{
    private const int FuzzyMatchThreshold = 85;
    private readonly Dictionary<string, string> _nameToSymbol = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _companyNames = new();

    public IReadOnlyList<string> CompanyNames => _companyNames;

    public static async Task<NiftyStockDirectory> LoadAsync()
    {
        var directory = new NiftyStockDirectory();
        await directory.LoadFromPackageAsync();
        return directory;
    }

    public string? ResolveSymbol(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return null;

        var normalized = rawName.Trim().ToUpperInvariant();
        if (_nameToSymbol.TryGetValue(normalized, out var direct))
            return direct;

        if (normalized.Length < 3)
            return null;

        var match = Process.ExtractOne(normalized, _companyNames, scorer: Scorer.TokenSetRatio);
        if (match is null || match.Score <= FuzzyMatchThreshold)
            return null;

        return _nameToSymbol.GetValueOrDefault(match.Value);
    }

    public IEnumerable<string> ResolveSymbolsInText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var token in text.Split([' ', ',', '-', ':', ';', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries))
        {
            var symbol = ResolveSymbol(token);
            if (!string.IsNullOrEmpty(symbol) && seen.Add(symbol))
                yield return symbol;
        }

        foreach (var companyName in _companyNames)
        {
            if (!text.Contains(companyName, StringComparison.OrdinalIgnoreCase))
                continue;

            var symbol = _nameToSymbol[companyName];
            if (seen.Add(symbol))
                yield return symbol;
        }
    }

    private async Task LoadFromPackageAsync()
    {
        try
        {
            await using var stream = await FileSystem.OpenAppPackageFileAsync("ind_nifty500list.csv");
            using var reader = new StreamReader(stream);
            await LoadFromCsvAsync(reader);
        }
        catch
        {
            LoadFallbackMappings();
        }
    }

    private async Task LoadFromCsvAsync(TextReader reader)
    {
        var header = await reader.ReadLineAsync();
        if (header is null)
            return;

        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            var parts = ParseCsvLine(line);
            if (parts.Count < 3)
                continue;

            var companyName = parts[0].Trim();
            var symbol = parts[2].Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(companyName) || string.IsNullOrEmpty(symbol))
                continue;

            RegisterCompany(companyName, symbol);
        }
    }

    private void LoadFallbackMappings()
    {
        foreach (var symbol in Models.NiftyConstituents.ScanUniverse)
            RegisterCompany(symbol, symbol);
    }

    private void RegisterCompany(string companyName, string symbol)
    {
        var normalizedName = companyName.Trim().ToUpperInvariant();
        _nameToSymbol[normalizedName] = symbol;
        _companyNames.Add(normalizedName);

        foreach (var alias in BuildAliases(companyName, symbol))
        {
            if (!_nameToSymbol.ContainsKey(alias))
                _nameToSymbol[alias] = symbol;
        }
    }

    private static IEnumerable<string> BuildAliases(string companyName, string symbol)
    {
        yield return symbol;

        var withoutSuffix = companyName
            .Replace(" Ltd.", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" Limited", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" Ltd", "", StringComparison.OrdinalIgnoreCase)
            .Trim()
            .ToUpperInvariant();

        if (!string.IsNullOrWhiteSpace(withoutSuffix))
            yield return withoutSuffix;
    }

    private static List<string> ParseCsvLine(string line)
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
                parts.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        parts.Add(current.ToString());
        return parts;
    }
}
