using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DonitGames.Core.Words;

/// <summary>Parses the <c>categories: [{ name, description, language, pairs: [{ wordA, wordB }] }]</c> shape.</summary>
public static class WordDataLoader
{
    public static IReadOnlyList<WordCategory> LoadFromYaml(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var root = deserializer.Deserialize<YamlRoot>(yaml)
            ?? throw new InvalidDataException("Word data YAML has no content.");

        return root.Categories
            .Select(ToCategory)
            .ToList();
    }

    public static IReadOnlyList<WordCategory> LoadFromFile(string path) =>
        LoadFromYaml(File.ReadAllText(path));

    private static WordCategory ToCategory(YamlCategory category)
    {
        if (string.IsNullOrWhiteSpace(category.Name))
        {
            throw new InvalidDataException("A word category is missing its name.");
        }

        if (string.IsNullOrWhiteSpace(category.Language))
        {
            throw new InvalidDataException($"Category '{category.Name}' is missing its language.");
        }

        var pairs = category.Pairs
            .Select(p => new WordPair(p.WordA, p.WordB))
            .ToList();

        return new WordCategory(category.Name, category.Description ?? string.Empty, category.Language, pairs);
    }

    private sealed class YamlRoot
    {
        public List<YamlCategory> Categories { get; set; } = [];
    }

    private sealed class YamlCategory
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Language { get; set; }
        public List<YamlPair> Pairs { get; set; } = [];
    }

    private sealed class YamlPair
    {
        public string WordA { get; set; } = string.Empty;
        public string WordB { get; set; } = string.Empty;
    }
}
