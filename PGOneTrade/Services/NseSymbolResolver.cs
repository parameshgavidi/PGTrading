using System.Text;
using System.Text.RegularExpressions;

namespace PGOneTrade.Services;

public interface INseSymbolResolver
{
    int SymbolCount { get; }
    Task EnsureLoadedAsync(CancellationToken cancellationToken = default);
    IEnumerable<string> ResolveSymbolsInText(string text);
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
            await MergeZerodhaSymbolsAsync(cancellationToken);
            _companies.Sort((a, b) => b.Name.Length.CompareTo(a.Name.Length));
            _isLoaded = true;
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
        _symbols.Add(symbol);
        _companies.Add(new CompanyEntry(companyName, symbol));
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
