using DonitGames.Core.Words;

namespace DonitGames.Core.Tests.Words;

public class EditDistanceTests
{
    [Theory]
    [InlineData("", "", 0)]
    [InlineData("kaas", "kaas", 0)]
    [InlineData("kaas", "", 4)]
    [InlineData("", "kaas", 4)]
    [InlineData("kaas", "kaa", 1)]
    [InlineData("kitten", "sitting", 3)]
    [InlineData("koffie", "kofie", 1)]
    [InlineData("maan", "man", 1)]
    public void Levenshtein_matches_known_distances(string a, string b, int expected)
    {
        Assert.Equal(expected, EditDistance.Levenshtein(a, b));
    }

    [Fact]
    public void Levenshtein_is_symmetric()
    {
        Assert.Equal(EditDistance.Levenshtein("vakantie", "vaknatie"), EditDistance.Levenshtein("vaknatie", "vakantie"));
    }
}
