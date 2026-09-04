using DonitGames.Core.Words;

namespace DonitGames.Core.Tests.Words;

public class WordDataLoaderTests
{
    private const string Yaml = """
        categories:
          - name: "English Words"
            description: "Everyday pairs"
            language: "EN"
            pairs:
              - wordA: "Coffee"
                wordB: "Tea"
              - wordA: "Dog"
                wordB: "Cat"
        """;

    [Fact]
    public void LoadFromYaml_parses_categories_and_pairs()
    {
        var categories = WordDataLoader.LoadFromYaml(Yaml);

        var category = Assert.Single(categories);
        Assert.Equal("English Words", category.Name);
        Assert.Equal("Everyday pairs", category.Description);
        Assert.Equal("EN", category.Language);
        Assert.Equal(2, category.Pairs.Count);
        Assert.Contains(category.Pairs, p => p is { WordA: "Coffee", WordB: "Tea" });
        Assert.Contains(category.Pairs, p => p is { WordA: "Dog", WordB: "Cat" });
    }

    [Fact]
    public void LoadFromYaml_throws_when_a_category_has_no_name()
    {
        const string yaml = """
            categories:
              - language: "EN"
                pairs:
                  - wordA: "Coffee"
                    wordB: "Tea"
            """;

        Assert.Throws<InvalidDataException>(() => WordDataLoader.LoadFromYaml(yaml));
    }

    [Fact]
    public void LoadFromYaml_throws_when_a_category_has_no_language()
    {
        const string yaml = """
            categories:
              - name: "English Words"
                pairs:
                  - wordA: "Coffee"
                    wordB: "Tea"
            """;

        Assert.Throws<InvalidDataException>(() => WordDataLoader.LoadFromYaml(yaml));
    }
}
