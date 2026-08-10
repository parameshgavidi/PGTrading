using System.Text;
using System.Text.RegularExpressions;

namespace PgAiTrading.Services;

public interface INseSymbolResolver
{
    int SymbolCount { get; }
    Task EnsureLoadedAsync(CancellationToken cancellationToken = default);
    IEnumerable<string> ResolveSymbolsInText(string text);
    /// <summary>Search NSE equities by tradingsymbol or company name.</summary>
    IReadOnlyList<string> Search(string query, int limit = 20);
    bool ContainsSymbol(string symbol);
}

public sealed partial class NseSymbolResolver : INseSymbolResolver
{
    private const string NseEquityListUrl = "https://archives.nseindia.com/content/equities/EQUITY_L.csv";
    private const int MinSymbolLength = 3;

    private readonly IZerodhaService _zerodha;
    private readonly HttpClient _http;
    private readonly HashSet<string> _symbols = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<CompanyEntry> _companies = new();
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private bool _isLoaded;

    public int SymbolCount => _symbols.Count;

    public NseSymbolResolver(IZerodhaService zerodha)
    {
        _zerodha = zerodha;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
    }

    public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (_isLoaded)
            return;

        await _loadLock.WaitAsync(cancellationToken);
        try
        {
            if (_isLoaded)
                return;

            await LoadFromNseAsync(cancellationToken);

            // Always keep a liquid fallback universe so search never stays empty.
            foreach (var symbol in Models.NiftyConstituents.ScanUniverse)
                Register(symbol, symbol);

            _companies.Sort((a, b) => b.Name.Length.CompareTo(a.Name.Length));
            _isLoaded = true;

            // Zerodha instruments dump is large — enrich after search is already usable.
            try
            {
                await MergeZerodhaSymbolsAsync(cancellationToken);
            }
            catch
            {
                // Search already works from NSE CSV + ScanUniverse.
            }
        }
        finally
        {
            _loadLock.Release();
        }
    }

    public IEnumerable<string> ResolveSymbolsInText(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || _symbols.Count == 0)
            yield break;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var company in _companies)
        {
            if (!text.Contains(company.Name, StringComparison.OrdinalIgnoreCase))
                continue;

            if (seen.Add(company.Symbol))
                yield return company.Symbol;
        }

        foreach (var token in Tokenize(text))
        {
            if (token.Length < MinSymbolLength)
                continue;

            if (_symbols.Contains(token) && seen.Add(token))
                yield return token;
        }
    }

    public IReadOnlyList<string> Search(string query, int limit = 20)
    {
        if (string.IsNullOrWhiteSpace(query) || limit <= 0)
            return Array.Empty<string>();

        var q = query.Trim().ToUpperInvariant();
        var startsWith = new List<string>();
        var containsSymbol = new List<string>();
        var containsName = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var symbol in _symbols)
        {
            if (symbol.StartsWith(q, StringComparison.OrdinalIgnoreCase))
            {
                if (seen.Add(symbol))
                    startsWith.Add(symbol);
            }
            else if (symbol.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                if (seen.Add(symbol))
                    containsSymbol.Add(symbol);
            }

            if (startsWith.Count >= limit)
                break;
        }

        if (startsWith.Count + containsSymbol.Count < limit)
        {
            foreach (var company in _companies)
            {
                if (seen.Contains(company.Symbol))
                    continue;

                if (!company.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                    && !company.Symbol.Contains(q, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (seen.Add(company.Symbol))
                    containsName.Add(company.Symbol);

                if (startsWith.Count + containsSymbol.Count + containsName.Count >= limit)
                    break;
            }
        }

        return startsWith
            .Concat(containsSymbol.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
            .Concat(containsName.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
            .Take(limit)
            .ToList();
    }

    public bool ContainsSymbol(string symbol) =>
        !string.IsNullOrWhiteSpace(symbol)
        && _symbols.Contains(symbol.Trim());

    private async Task LoadFromNseAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.GetAsync(NseEquityListUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            var csv = await response.Content.ReadAsStringAsync(cancellationToken);
            using var reader = new StringReader(csv);
            _ = reader.ReadLine();

            while (reader.ReadLine() is { } line)
            {
                var parts = ParseCsvLine(line);
                if (parts.Count < 3)
                    continue;

                if (!parts[2].Equals("EQ", StringComparison.OrdinalIgnoreCase))
                    continue;

                var symbol = parts[0].Trim().ToUpperInvariant();
                var companyName = parts[1].Trim();
                if (string.IsNullOrWhiteSpace(symbol) || string.IsNullOrWhiteSpace(companyName))
                    continue;

                Register(symbol, companyName);
            }
        }
        catch
        {
            foreach (var symbol in Models.NiftyConstituents.ScanUniverse)
                Register(symbol, symbol);
        }
    }

    private async Task MergeZerodhaSymbolsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var zerodhaSymbols = await _zerodha.GetNseEquitySymbolsAsync();
        foreach (var symbol in zerodhaSymbols)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                continue;

            _symbols.Add(symbol.ToUpperInvariant());
        }
    }

    private void Register(string symbol, string companyName)
    {
        var normalized = symbol.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(normalized))
            return;

        var added = _symbols.Add(normalized);
        if (!added)
            return;

        var name = string.IsNullOrWhiteSpace(companyName) ? normalized : companyName.Trim();
        _companies.Add(new CompanyEntry(name, normalized));
    }

    private static IEnumerable<string> Tokenize(string text)
    {
        foreach (Match match in SymbolTokenRegex().Matches(text.ToUpperInvariant()))
        {
            if (!string.IsNullOrWhiteSpace(match.Value))
                yield return match.Value;
        }
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
                parts.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        parts.Add(current.ToString().Trim());
        return parts;
    }

    [GeneratedRegex(@"\b[A-Z][A-Z0-9&.-]{2,}\b")]
    private static partial Regex SymbolTokenRegex();

    private sealed record CompanyEntry(string Name, string Symbol);
}
