namespace DonitGames.Core.JustOne;

public sealed record JustOnePlayerView(
    Guid SeatId,
    string DisplayName,
    bool IsHost,
    bool IsConnected,
    bool IsGuesser,
    bool IsJudge,
    bool HasSubmittedClue);

/// <summary>A cancelled clue is only ever absent from the guesser's view entirely (see
/// <c>JustOneSession.ViewFor</c>) — <see cref="IsCancelled"/> exists for clue-givers/the judge,
/// who watch the review happen.</summary>
public sealed record ClueView(Guid SeatId, string Text, bool IsCancelled, bool IsFlaggedForReview);

/// <summary><see cref="Index"/> is what a toggle action sends back to
/// <c>JustOneEngine.ToggleReviewGroup</c> — a per-clue boolean flag alone can't identify which
/// group to toggle.</summary>
public sealed record ReviewGroupView(int Index, IReadOnlyList<Guid> SeatIds, bool ManuallyCancelled);

public sealed record JustOneView(
    JustOnePhase Phase,
    int RoundNumber,
    IReadOnlyList<JustOnePlayerView> Players,
    bool YouAreHost,
    bool YouAreGuesser,
    bool YouAreJudge,
    string? SecretWord,
    string? YourClueText,
    IReadOnlyList<ClueView> Clues,
    IReadOnlyList<ReviewGroupView> ReviewGroups,
    string? GuesserAttempt,
    RoundOutcome? LastOutcome,
    int PipsRemaining,
    int CorrectCount,
    int RoundsPlayed);
