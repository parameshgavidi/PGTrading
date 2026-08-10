using PgAiTrading.Models;
using Xunit;

namespace PgAiTrading.Framework.Tests;

public class AppThemesTests
{
    [Theory]
    [InlineData(null, AppThemes.Black)]
    [InlineData("", AppThemes.Black)]
    [InlineData("black", AppThemes.Black)]
    [InlineData("BLACK", AppThemes.Black)]
    [InlineData("classic", AppThemes.Classic)]
    [InlineData("Classic", AppThemes.Classic)]
    [InlineData("white", AppThemes.Black)]
    public void Normalize_maps_known_and_unknown_themes(string? input, string expected)
    {
        Assert.Equal(expected, AppThemes.Normalize(input));
    }

    [Fact]
    public void DisplayName_describes_classic_clearly()
    {
        Assert.Equal("Black", AppThemes.DisplayName(AppThemes.Black));
        Assert.Equal("White & Blue (Classic)", AppThemes.DisplayName(AppThemes.Classic));
    }
}
