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
    JustOneMode Mode,
    int RoundNumber,
    Guid YourSeatId,
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
    int RoundsPlayed)
{
    /// <summary>Everyone whose job this round is to write a clue — i.e. the table minus the
    /// guesser. The denominator of every "who's still typing" line in the UI.</summary>
    public IReadOnlyList<JustOnePlayerView> ClueGivers => [.. Players.Where(p => !p.IsGuesser)];

    public int ClueGiverCount => Players.Count(p => !p.IsGuesser);

    public int SubmittedClueCount => Players.Count(p => !p.IsGuesser && p.HasSubmittedClue);

    public JustOnePlayerView? Guesser => Players.FirstOrDefault(p => p.IsGuesser);

    /// <summary>Table mode gives the verdict to any clue-giver rather than to the judge —
    /// see <c>JustOneEngine.RecordTableVerdict</c>.</summary>
    public bool YouMayJudge => Mode == JustOneMode.Table ? !YouAreGuesser : YouAreJudge;

    public string NameOf(Guid seatId) => Players.FirstOrDefault(p => p.SeatId == seatId)?.DisplayName ?? "?";
}
