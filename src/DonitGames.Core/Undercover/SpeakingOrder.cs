namespace DonitGames.Core.Undercover;

public static class SpeakingOrder
{
    /// <summary>Shuffles non-Mr.White players, shuffles Mr. Whites, then inserts each Mr. White
    /// at a random index in [1, count] of the non-Mr.White list — never first, but the last
    /// valid insertion point is the very end, so they may speak last.</summary>
    public static IReadOnlyList<Guid> Compute(IReadOnlyList<UndercoverPlayer> active, Random rng)
    {
        ArgumentNullException.ThrowIfNull(active);
        ArgumentNullException.ThrowIfNull(rng);

        var nonMrWhite = Shuffle(active.Where(p => p.Role != UndercoverRole.MrWhite).Select(p => p.SeatId).ToList(), rng);
        var mrWhites = Shuffle(active.Where(p => p.Role == UndercoverRole.MrWhite).Select(p => p.SeatId).ToList(), rng);

        if (nonMrWhite.Count == 0)
        {
            // Every active player is Mr. White (e.g. the tail end of a round) — "not first"
            // can't be enforced, so just hand back the shuffled Mr. White order.
            return mrWhites;
        }

        foreach (var mrWhite in mrWhites)
        {
            var insertIndex = rng.Next(1, nonMrWhite.Count + 1); // [1, count] inclusive
            nonMrWhite.Insert(insertIndex, mrWhite);
        }

        return nonMrWhite;
    }

    private static List<T> Shuffle<T>(List<T> items, Random rng)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }

        return items;
    }
}
