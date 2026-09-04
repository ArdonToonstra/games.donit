namespace DonitGames.Core.Undercover;

public enum UndercoverPhase
{
    Lobby,
    CardPicking,
    Discussion,
    Voting,
    EliminationReveal,
    MrWhiteGuess,
    Results,
}

public enum UndercoverWinner
{
    Civilians,
    UndercoverTeam,
    MrWhiteSolo,
}
