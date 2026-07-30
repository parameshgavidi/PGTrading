using PGOne.Services;

namespace PGOne.Framework.Tests;

public class NiftyIndexServiceTests
{
    [Fact]
    public void ParseNifty500Csv_ExtractsEqSymbols()
    {
        const string csv = """
            Company Name,Industry,Symbol,Series,ISIN Code
            Reliance Industries Ltd.,Oil,RELIANCE,EQ,INE002A01018
            Some Pvt Ltd.,Finance,FOO-BE,BE,INE000000000
            Tata Consultancy Services Ltd.,IT,TCS,EQ,INE467B01029
            """;

        var symbols = NiftyIndexService.ParseNifty500Csv(csv);

        Assert.Equal(2, symbols.Count);
        Assert.Contains("RELIANCE", symbols);
        Assert.Contains("TCS", symbols);
        Assert.DoesNotContain("FOO-BE", symbols);
    }

    [Fact]
    public void ParseNifty500Csv_LoadsBundledFile()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "nifty500.csv");
        if (!File.Exists(path))
            return;

        var symbols = NiftyIndexService.ParseNifty500Csv(File.ReadAllText(path));

        Assert.InRange(symbols.Count, 400, 510);
        Assert.Contains("RELIANCE", symbols);
    }
}
