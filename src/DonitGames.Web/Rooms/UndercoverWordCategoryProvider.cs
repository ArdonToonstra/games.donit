using DonitGames.Core.Words;

namespace DonitGames.Web.Rooms;

/// <summary>Loads Data/WordPairs.yaml once and exposes the Dutch category — games.donit's UI is
/// Dutch-only (CLAUDE.md); the English category in that file is leftover from the WASM app's
/// language picker.</summary>
public sealed class UndercoverWordCategoryProvider
{
    public WordCategory NederlandseWoorden { get; }

    public UndercoverWordCategoryProvider()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "WordPairs.yaml");
        var categories = WordDataLoader.LoadFromFile(path);
        NederlandseWoorden = categories.First(c => c.Name == "Nederlandse Woorden");
    }
}
