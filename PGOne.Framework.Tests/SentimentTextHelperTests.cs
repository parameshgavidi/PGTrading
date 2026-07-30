using PGOne.Services;

namespace PGOne.Framework.Tests;

public class SentimentTextHelperTests
{
    [Fact]
    public void CleanBoilerplate_RemovesNavChrome()
    {
        var raw = "Five undervalued dividend stocks | Stock Market News Subscribe Sign in View Market Dashboard Home Markets";
        var cleaned = SentimentTextHelper.CleanBoilerplate(raw);

        Assert.Contains("undervalued", cleaned, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("subscribe", cleaned, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dashboard", cleaned, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PrepareAnalysisText_PrioritizesHeadline()
    {
        var title = "Reliance shares rally 3% on strong earnings beat";
        var noisyBody = "Subscribe sign in home markets market news stock markets read more also read trending now";

        var text = SentimentTextHelper.PrepareAnalysisText(title, noisyBody);

        Assert.StartsWith("Reliance shares rally", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("subscribe", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ScoreWithKeywords_DetectsUndervaluedAsPositive()
    {
        var scores = SentimentTextHelper.ScoreWithKeywords("Five undervalued dividend stocks to buy now");

        Assert.True(scores.Positive > scores.Negative);
        Assert.Equal("positive", scores.TopLabel().Label);
    }

    [Fact]
    public void ExtractArticleSnippetFromHtml_UsesMetaDescription()
    {
        const string html = """
            <html><head>
            <meta name="description" content="HDFC Bank profit rises 12% on strong loan growth." />
            </head><body><nav>Subscribe sign in</nav></body></html>
            """;

        var snippet = SentimentTextHelper.ExtractArticleSnippetFromHtml(html);

        Assert.Contains("profit rises", snippet, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("subscribe", snippet, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExtractReasonSnippet_PrefersCleanTitle()
    {
        var snippet = SentimentTextHelper.ExtractReasonSnippet(
            "Subscribe sign in home markets market news",
            "Tata Motors shares surge on export growth");

        Assert.Contains("surge", snippet, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("subscribe", snippet, StringComparison.OrdinalIgnoreCase);
    }
}
