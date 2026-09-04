using DonitGames.Core.JustOne;
using DonitGames.Core.Words;

namespace DonitGames.Core.Tests.JustOne;

/// <summary>Guards the real Data/JustOneWords.yaml — mirrors WordDataIntegrityTests.cs for the
/// Undercover word list (Phase 1).</summary>
public class JustOneWordBankIntegrityTests
{
    private static readonly IReadOnlyList<string> Words =
        JustOneWordBank.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "Data", "JustOneWords.yaml"));

    [Fact]
    public void At_least_120_words_are_seeded()
    {
        Assert.True(Words.Count >= 120, $"Expected at least 120 words, found {Words.Count}.");
    }

    [Fact]
    public void No_word_is_empty_or_whitespace()
    {
        Assert.All(Words, w => Assert.False(string.IsNullOrWhiteSpace(w)));
    }

    [Fact]
    public void No_duplicate_words_even_after_normalizing_case_and_accents()
    {
        var seen = new HashSet<string>();
        foreach (var word in Words)
        {
            var key = NormalKeys.Compute(word);
            Assert.True(seen.Add(key), $"Duplicate word (after normalizing): '{word}'.");
        }
    }
}
