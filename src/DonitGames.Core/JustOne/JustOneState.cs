namespace DonitGames.Core.JustOne;

public sealed record JustOneState(
    JustOnePhase Phase,
    Guid? GuesserSeatId,
    string? SecretWord,
    IReadOnlyList<Clue> Clues,
    IReadOnlyList<Guid> AutoCancelledSeatIds,
    IReadOnlyList<ReviewGroup> ReviewGroups,
    string? GuesserAttempt,
    RoundOutcome? LastOutcome,
    int PipsRemaining,
    int CorrectCount,
    int RoundsPlayed,
    IReadOnlyList<string> UsedWords,
    int RoundNumber)
{
    public const int StartingPips = 13;

    public static JustOneState Initial { get; } = new(
        JustOnePhase.Lobby,
        null,
        null,
        [],
        [],
        [],
        null,
        null,
        StartingPips,
        0,
        0,
        [],
        0);
}
