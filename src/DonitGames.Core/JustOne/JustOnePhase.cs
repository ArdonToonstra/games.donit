namespace DonitGames.Core.JustOne;

public enum JustOnePhase
{
    Lobby,
    ClueWriting,
    DuplicateReview,
    Guessing,
    JudgeReview,
    RoundResult,
    GameResults,
}

public enum RoundOutcome
{
    Correct,
    Incorrect,
    Passed,
    NoClues,
}
