using DonitGames.Core.Words;

namespace DonitGames.Core.Tests.Words;

public class NormalKeysTests
{
    [Theory]
    [InlineData("cafe", "café")]
    [InlineData("Auto", "auto")]
    [InlineData("  Kaas ", "kaas")]
    [InlineData("Café", "cafe")]
    public void Compute_treats_case_accent_and_padding_as_equivalent(string a, string b)
    {
        Assert.Equal(NormalKeys.Compute(a), NormalKeys.Compute(b));
    }

    [Theory]
    [InlineData("kaas", "kaa")]
    [InlineData("ijs", "ij")]
    [InlineData("maan", "man")]
    [InlineData("Ham", "Kaas")]
    public void Compute_keeps_distinct_words_distinct(string a, string b)
    {
        Assert.NotEqual(NormalKeys.Compute(a), NormalKeys.Compute(b));
    }
}
