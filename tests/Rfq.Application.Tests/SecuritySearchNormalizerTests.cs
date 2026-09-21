using Xunit;

namespace Rfq.Application.Tests;

public sealed class SecuritySearchNormalizerTests
{
    [Theory]
    [InlineData("375-1", "0-02-0375-00001")]
    [InlineData("0-2-375-1", "0-02-0375-00001")]
    [InlineData("0-02-0375-00001", "0-02-0375-00001")]
    public void NormalizeInternalCodeSupportsShortAndFullGrammar(
        string input,
        string expected)
    {
        Assert.Equal(expected, SecuritySearchNormalizer.NormalizeInternalCode(input));
    }

    [Theory]
    [InlineData("1-2-3", null)]
    [InlineData("0-002-1-1", null)]
    [InlineData("abc-def", null)]
    public void NormalizeInternalCodeRejectsInvalidGrammar(
        string input,
        string? expected)
    {
        Assert.Equal(expected, SecuritySearchNormalizer.NormalizeInternalCode(input));
    }

    [Fact]
    public void NormalizeBbgTextNormalizesCaseWhitespaceCouponAndDate()
    {
        Assert.Equal(
            "TOYOTA 0.5 03/20/2030 #1",
            SecuritySearchNormalizer.NormalizeBbgText(" toyota   0.500  3/20/30  #1 "));
    }

    [Theory]
    [InlineData("jp36346", "JP36346")]
    [InlineData("JP363460AG12", "JP363460AG12")]
    [InlineData("JP123", null)]
    [InlineData("1234567", null)]
    public void NormalizeIsinPrefixValidatesSevenToTwelveCharacters(
        string input,
        string? expected)
    {
        Assert.Equal(expected, SecuritySearchNormalizer.NormalizeIsinPrefix(input));
    }
}
