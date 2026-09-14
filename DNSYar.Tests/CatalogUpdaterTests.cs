using DNSYar.Services;
using Xunit;

namespace DNSYar.Tests;

public sealed class CatalogUpdaterTests
{
    [Fact]
    public void ConvertsGithubBlobUrlToRawUrl()
    {
        var actual = CatalogUrl.NormalizeGithubUrl(
            "https://github.com/owner/repository/blob/main/data/dns.txt");

        Assert.Equal("https://raw.githubusercontent.com/owner/repository/main/data/dns.txt", actual);
    }

    [Fact]
    public void LeavesRawHttpsUrlUnchanged()
    {
        const string url = "https://raw.githubusercontent.com/owner/repository/main/dns.txt";
        Assert.Equal(url, CatalogUrl.NormalizeGithubUrl(url));
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("")]
    public void InvalidInputDoesNotBecomeAUrl(string input)
    {
        Assert.Equal(input, CatalogUrl.NormalizeGithubUrl(input));
    }

    [Theory]
    [InlineData("https://example.com/dns.json", true)]
    [InlineData("http://example.com/dns.json", false)]
    [InlineData("not a url", false)]
    public void AcceptsOnlyHttpsCatalogUrls(string input, bool expected)
    {
        Assert.Equal(expected, CatalogUrl.IsHttps(input));
    }
}
