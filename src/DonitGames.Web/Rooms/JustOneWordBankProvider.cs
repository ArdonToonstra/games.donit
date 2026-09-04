using DonitGames.Core.JustOne;

namespace DonitGames.Web.Rooms;

/// <summary>Loads Data/JustOneWords.yaml once, mirrors UndercoverWordCategoryProvider.</summary>
public sealed class JustOneWordBankProvider
{
    public IReadOnlyList<string> Words { get; }

    public JustOneWordBankProvider()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "JustOneWords.yaml");
        Words = JustOneWordBank.LoadFromFile(path);
    }
}
