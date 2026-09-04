namespace DonitGames.Core.Words;

public static class WordPairProvider
{
    /// <summary>
    /// Draws one pair from <paramref name="category"/>, skipping anything in <paramref name="exclude"/>
    /// (the pairs already used in this room), and picks civilian/undercover orientation for this draw only —
    /// never cache the result across draws, or every reuse of a pair gets the same orientation.
    /// </summary>
    public static OrientedPair Draw(WordCategory category, Random rng, IReadOnlySet<WordPair> exclude)
    {
        ArgumentNullException.ThrowIfNull(category);
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentNullException.ThrowIfNull(exclude);

        var available = category.Pairs.Where(p => !exclude.Contains(p)).ToList();
        if (available.Count == 0)
        {
            throw new InvalidOperationException($"Category '{category.Name}' has no unused word pairs left.");
        }

        var pair = available[rng.Next(available.Count)];
        return rng.Next(2) == 0
            ? new OrientedPair(pair.WordA, pair.WordB, pair)
            : new OrientedPair(pair.WordB, pair.WordA, pair);
    }
}
