using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DonitGames.Core.JustOne;

/// <summary>
/// Parses the flat <c>words: [...]</c> shape — deliberately not <c>WordDataLoader</c>'s
/// categories/pairs shape, which is a different data model for a different game.
/// </summary>
public static class JustOneWordBank
{
    public static IReadOnlyList<string> LoadFromYaml(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var root = deserializer.Deserialize<YamlRoot>(yaml)
            ?? throw new InvalidDataException("Word bank YAML has no content.");

        if (root.Words.Count == 0)
        {
            throw new InvalidDataException("Word bank YAML has no words.");
        }

        return root.Words;
    }

    public static IReadOnlyList<string> LoadFromFile(string path) => LoadFromYaml(File.ReadAllText(path));

    /// <summary>Same draw-without-replacement contract as <c>WordPairProvider.Draw</c>.</summary>
    public static string Draw(IReadOnlyList<string> words, Random rng, IReadOnlySet<string> exclude)
    {
        ArgumentNullException.ThrowIfNull(words);
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentNullException.ThrowIfNull(exclude);

        var available = words.Where(w => !exclude.Contains(w)).ToList();
        if (available.Count == 0)
        {
            throw new InvalidOperationException("No unused words left in the bank.");
        }

        return available[rng.Next(available.Count)];
    }

    private sealed class YamlRoot
    {
        public List<string> Words { get; set; } = [];
    }
}
