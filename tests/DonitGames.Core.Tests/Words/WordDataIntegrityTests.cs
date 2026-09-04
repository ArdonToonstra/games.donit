using DonitGames.Core.Words;

namespace DonitGames.Core.Tests.Words;

/// <summary>Guards the real Data/WordPairs.yaml — a bad entry here is a live-game bug, not a test fixture bug.</summary>
public class WordDataIntegrityTests
{
    private static readonly IReadOnlyList<WordCategory> Categories =
        WordDataLoader.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "Data", "WordPairs.yaml"));

    public static IEnumerable<object[]> AllCategories() => Categories.Select(c => new object[] { c });

    [Fact]
    public void At_least_one_category_loads()
    {
        Assert.NotEmpty(Categories);
    }

    [Theory]
    [MemberData(nameof(AllCategories))]
    public void Category_has_no_empty_words(WordCategory category)
    {
        foreach (var pair in category.Pairs)
        {
            Assert.False(string.IsNullOrWhiteSpace(pair.WordA), $"{category.Name}: WordA is empty.");
            Assert.False(string.IsNullOrWhiteSpace(pair.WordB), $"{category.Name}: WordB is empty.");
        }
    }

    [Theory]
    [MemberData(nameof(AllCategories))]
    public void Category_has_no_pair_where_A_equals_B(WordCategory category)
    {
        foreach (var pair in category.Pairs)
        {
            Assert.False(
                WordNormalizer.AreEquivalent(pair.WordA, pair.WordB),
                $"{category.Name}: '{pair.WordA}' / '{pair.WordB}' are the same word.");
        }
    }

    [Theory]
    [MemberData(nameof(AllCategories))]
    public void Category_has_no_duplicate_pair(WordCategory category)
    {
        var seen = new HashSet<(string, string)>();
        foreach (var pair in category.Pairs)
        {
            var a = NormalKeys.Compute(pair.WordA);
            var b = NormalKeys.Compute(pair.WordB);
            var key = string.CompareOrdinal(a, b) <= 0 ? (a, b) : (b, a);

            Assert.True(seen.Add(key), $"{category.Name}: '{pair.WordA}' / '{pair.WordB}' is a duplicate pair.");
        }
    }

    [Fact]
    public void Every_category_has_a_name_a_language_and_at_least_one_pair()
    {
        foreach (var category in Categories)
        {
            Assert.False(string.IsNullOrWhiteSpace(category.Name));
            Assert.False(string.IsNullOrWhiteSpace(category.Language));
            Assert.NotEmpty(category.Pairs);
        }
    }
}
