using DNSYar.Services;
using Xunit;

namespace DNSYar.Tests;

public sealed class CatalogUpdaterTests
{
    [Fact]
    public void ConvertsGithubBlobUrlToRawUrl()
    {
        var actual = CatalogUpdater.NormalizeGithubUrl(
            "https://github.com/owner/repository/blob/main/data/dns.txt");

        Assert.Equal("https://raw.githubusercontent.com/owner/repository/main/data/dns.txt", actual);
    }

    [Fact]
    public void LeavesRawHttpsUrlUnchanged()
    {
        const string url = "https://raw.githubusercontent.com/owner/repository/main/dns.txt";
        Assert.Equal(url, CatalogUpdater.NormalizeGithubUrl(url));
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("")]
    public void InvalidInputDoesNotBecomeAUrl(string input)
    {
        Assert.Equal(input, CatalogUpdater.NormalizeGithubUrl(input));
    }
}
