using DonitGames.Core.Words;

namespace DonitGames.Core.Undercover;

public sealed record UndercoverState(
    UndercoverPhase Phase,
    IReadOnlyList<UndercoverPlayer> Players,
    IReadOnlyList<FaceDownCard> Deck,
    IReadOnlyList<Guid> SpeakingOrder,
    DateTimeOffset? DiscussionDeadlineUtc,
    IReadOnlyDictionary<Guid, Guid> Votes,
    IReadOnlyList<Guid> TiedSeatIds,
    Guid? JustEliminatedSeatId,
    Guid? MrWhiteGuesserSeatId,
    UndercoverWinner? Winner,
    IReadOnlyList<WordPair> UsedPairs,
    int RoundNumber)
{
    public static UndercoverState Initial { get; } = new(
        UndercoverPhase.Lobby,
        [],
        [],
        [],
        null,
        new Dictionary<Guid, Guid>(),
        [],
        null,
        null,
        null,
        [],
        0);
}
