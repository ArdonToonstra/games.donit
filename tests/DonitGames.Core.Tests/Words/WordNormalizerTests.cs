using DonitGames.Core.Words;

namespace DonitGames.Core.Tests.Words;

public class WordNormalizerTests
{
    [Theory]
    [InlineData("cafe", "café")]
    [InlineData("Koffie", "koffie")]
    [InlineData("  Appel", "appel")]
    public void AreEquivalent_ignores_case_accents_and_padding(string a, string b)
    {
        Assert.True(WordNormalizer.AreEquivalent(a, b));
    }

    [Theory]
    [InlineData("Kaas", "Worst")]
    [InlineData("koffie", "thee")]
    public void AreEquivalent_rejects_different_words(string a, string b)
    {
        Assert.False(WordNormalizer.AreEquivalent(a, b));
    }

    // Negative cases: short Dutch words one edit apart that are NOT the same word.
    [Theory]
    [InlineData("kaas", "kaa")]
    [InlineData("ijs", "ij")]
    [InlineData("maan", "man")]
    [InlineData("bus", "bos")]
    [InlineData("pen", "pan")]
    public void IsNearDuplicate_rejects_short_unrelated_words(string a, string b)
    {
        Assert.False(WordNormalizer.IsNearDuplicate(a, b));
    }

    // Positive cases: longer words with a genuine one-letter typo.
    [Theory]
    [InlineData("koffie", "kofie")]
    [InlineData("vakantie", "vacantie")]
    [InlineData("supermarkt", "supermrkt")]
    public void IsNearDuplicate_accepts_typos_of_longer_words(string a, string b)
    {
        Assert.True(WordNormalizer.IsNearDuplicate(a, b));
    }

    [Fact]
    public void IsNearDuplicate_accepts_exact_matches_after_normalization()
    {
        Assert.True(WordNormalizer.IsNearDuplicate("Café", "cafe"));
    }

    [Theory]
    [InlineData("dog", "cat")]
    [InlineData("summer", "winter")]
    public void IsNearDuplicate_rejects_unrelated_English_words(string a, string b)
    {
        Assert.False(WordNormalizer.IsNearDuplicate(a, b));
    }
}
