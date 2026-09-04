namespace DonitGames.Core.Undercover;

public enum PickResult
{
    Success,
    CardAlreadyTaken,
    AlreadyHaveACard,
    WrongPhase,
}

public enum VoteResult
{
    Success,
    WrongPhase,
    SeatNotActive,
}

public enum GuessResult
{
    Correct,
    Incorrect,
    WrongPhase,
    NotYourGuess,
}
