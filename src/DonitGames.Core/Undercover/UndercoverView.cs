namespace DonitGames.Core.Undercover;

/// <summary>Deck entries never carry Role/Word in a view — not even the picker's own, which
/// comes from <see cref="UndercoverPlayerView"/> instead. This is the actual enforcement point
/// for "nobody sees an unpicked card's role" (CLAUDE.md non-negotiable #5).</summary>
public sealed record UndercoverCardView(int Index, Guid? TakenBySeatId);

/// <summary>RevealedRole/RevealedWord are null unless this seat is eliminated or is the viewer's
/// own seat — while active, another player's role/word is simply not present on the type.</summary>
public sealed record UndercoverPlayerView(
    Guid SeatId,
    string DisplayName,
    bool IsHost,
    bool IsConnected,
    bool IsEliminated,
    UndercoverRole? RevealedRole,
    string? RevealedWord,
    int Score);

public sealed record UndercoverView(
    UndercoverPhase Phase,
    int RoundNumber,
    IReadOnlyList<UndercoverPlayerView> Players,
    IReadOnlyList<UndercoverCardView> Deck,
    IReadOnlyList<Guid> SpeakingOrder,
    DateTimeOffset? DiscussionDeadlineUtc,
    bool YouAreHost,
    UndercoverRole? YourRole,
    string? YourSecretWord,
    bool HaveYouVoted,
    IReadOnlyDictionary<Guid, Guid>? VoteBreakdown,
    IReadOnlyList<Guid> TiedSeatIds,
    Guid? JustEliminatedSeatId,
    bool YouAreTheGuesser,
    UndercoverWinner? Winner);
