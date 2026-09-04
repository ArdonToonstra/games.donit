namespace DonitGames.Core.JustOne;

public enum JustOnePhase
{
    Lobby,
    ClueWriting,

    /// <summary>Phones mode only.</summary>
    DuplicateReview,

    /// <summary>Phones mode only.</summary>
    Guessing,

    /// <summary>Phones mode only.</summary>
    JudgeReview,

    /// <summary>Table mode only — every clue-giver's screen is their clue, in letters big
    /// enough to read across a table. Nothing else is on the screen, because the screen is
    /// being held up rather than looked at.</summary>
    ClueReveal,

    /// <summary>Table mode only — the guess was spoken out loud, so all the app has to do is
    /// record what the table decided about it.</summary>
    TableVerdict,

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
