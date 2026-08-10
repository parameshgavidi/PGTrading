using PGOneTrade.Services;
using Xunit;

namespace PGOneTrade.Framework.Tests;

public class NiftyIndexServiceTests
{
    [Fact]
    public void ParseIndexCsv_ExtractsEqSymbols()
    {
        const string csv = """
            Company Name,Industry,Symbol,Series,ISIN Code
            Reliance Industries Ltd.,Oil,RELIANCE,EQ,INE002A01018
            Some Pvt Ltd.,Finance,FOO-BE,BE,INE000000000
            Tata Consultancy Services Ltd.,IT,TCS,EQ,INE467B01029
            """;

        var symbols = NiftyIndexService.ParseIndexCsv(csv);

        Assert.Equal(2, symbols.Count);
        Assert.Contains("RELIANCE", symbols);
        Assert.Contains("TCS", symbols);
        Assert.DoesNotContain("FOO-BE", symbols);
    }

    [Fact]
    public void ParseIndexCsv_LoadsBundledNifty50File()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "nifty50.csv");
        if (!File.Exists(path))
            return;

        var symbols = NiftyIndexService.ParseIndexCsv(File.ReadAllText(path));

        Assert.InRange(symbols.Count, 48, 52);
        Assert.Contains("RELIANCE", symbols);
    }

    [Fact]
    public void ParseIndexCsv_LoadsBundledNifty500File()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "nifty500.csv");
        if (!File.Exists(path))
            return;

        var symbols = NiftyIndexService.ParseIndexCsv(File.ReadAllText(path));

        Assert.InRange(symbols.Count, 400, 510);
        Assert.Contains("RELIANCE", symbols);
    }
}
