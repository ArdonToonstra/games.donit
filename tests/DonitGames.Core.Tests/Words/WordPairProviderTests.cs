using DonitGames.Core.Words;

namespace DonitGames.Core.Tests.Words;

public class WordPairProviderTests
{
    private static WordCategory MakeCategory(params (string A, string B)[] pairs) =>
        new("Test", "", "EN", pairs.Select(p => new WordPair(p.A, p.B)).ToList());

    [Fact]
    public void Draw_returns_one_of_the_two_orientations()
    {
        var category = MakeCategory(("Coffee", "Tea"));
        var seenCivilian = new HashSet<string>();

        for (var i = 0; i < 50; i++)
        {
            var drawn = WordPairProvider.Draw(category, new Random(i), new HashSet<WordPair>());
            seenCivilian.Add(drawn.Civilian);
            Assert.True(
                (drawn.Civilian == "Coffee" && drawn.Undercover == "Tea") ||
                (drawn.Civilian == "Tea" && drawn.Undercover == "Coffee"));
        }

        Assert.Equal(2, seenCivilian.Count);
    }

    [Fact]
    public void Draw_never_returns_an_excluded_pair()
    {
        var category = MakeCategory(("Coffee", "Tea"), ("Dog", "Cat"));
        var exclude = new HashSet<WordPair> { new("Coffee", "Tea") };

        for (var i = 0; i < 20; i++)
        {
            var drawn = WordPairProvider.Draw(category, new Random(i), exclude);
            Assert.True(
                (drawn.Civilian == "Dog" && drawn.Undercover == "Cat") ||
                (drawn.Civilian == "Cat" && drawn.Undercover == "Dog"));
        }
    }

    [Fact]
    public void Draw_throws_once_every_pair_in_the_category_is_excluded()
    {
        var category = MakeCategory(("Coffee", "Tea"));
        var exclude = new HashSet<WordPair> { new("Coffee", "Tea") };

        Assert.Throws<InvalidOperationException>(() => WordPairProvider.Draw(category, new Random(), exclude));
    }

    [Fact]
    public void Draw_Source_identifies_the_original_pair_regardless_of_orientation()
    {
        var category = MakeCategory(("Coffee", "Tea"));
        var original = category.Pairs[0];

        for (var i = 0; i < 20; i++)
        {
            var drawn = WordPairProvider.Draw(category, new Random(i), new HashSet<WordPair>());
            Assert.Equal(original, drawn.Source);
        }
    }

    [Fact]
    public void Draw_orientation_is_independent_across_repeated_draws_of_the_same_pair()
    {
        // Same pair can recur across rooms; each draw must re-roll orientation rather than
        // reusing whatever was chosen the first time (the bug in the WASM app's loader).
        var category = MakeCategory(("Coffee", "Tea"));
        var rng = new Random(1);

        var first = WordPairProvider.Draw(category, rng, new HashSet<WordPair>());
        var results = Enumerable.Range(0, 50)
            .Select(_ => WordPairProvider.Draw(category, rng, new HashSet<WordPair>()))
            .ToList();

        Assert.Contains(results, r => r.Civilian != first.Civilian);
    }
}
