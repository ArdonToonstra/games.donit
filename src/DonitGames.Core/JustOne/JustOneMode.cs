namespace DonitGames.Core.JustOne;

/// <summary>
/// How a round is played out once the clues are written. Chosen in the lobby, fixed for the
/// game — it changes which phases exist, so switching mid-round would strand whoever is
/// standing in a phase the other mode doesn't have.
/// </summary>
public enum JustOneMode
{
    /// <summary>Everything happens on the phones: the app collects the clues, cancels the
    /// duplicates, shows the survivors to the guesser, takes the typed guess.</summary>
    Phones,

    /// <summary>The phones only carry the clue. Each clue-giver's screen becomes their clue in
    /// giant letters and gets held up; the table spots its own duplicates, the guesser answers
    /// out loud, and one tap records whether that was right. Less screen, more table.</summary>
    Table,
}
