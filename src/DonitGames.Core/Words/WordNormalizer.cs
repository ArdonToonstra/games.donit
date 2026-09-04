namespace DonitGames.Core.Words;

/// <summary>
/// Word-equivalence used by both games: Mr. White's spoken guess against the secret word,
/// word-list integrity checks, and (from Just One onward) clue near-duplicate detection.
/// </summary>
public static class WordNormalizer
{
    public static bool AreEquivalent(string a, string b) => NormalKeys.Compute(a) == NormalKeys.Compute(b);

    /// <summary>
    /// True if two *different* words are close enough to be the same clue caught by a typo.
    /// Short words get a zero-tolerance floor: "kaas"/"kaa", "ijs"/"ij" and "maan"/"man" are each
    /// one edit apart but are unrelated Dutch words, not typos of each other, so a flat
    /// distance-based threshold would wrongly cancel them.
    /// </summary>
    public static bool IsNearDuplicate(string a, string b)
    {
        var keyA = NormalKeys.Compute(a);
        var keyB = NormalKeys.Compute(b);
        if (keyA == keyB)
        {
            return true;
        }

        var threshold = ThresholdFor(Math.Min(keyA.Length, keyB.Length));
        return threshold > 0 && EditDistance.Levenshtein(keyA, keyB) <= threshold;
    }

    private static int ThresholdFor(int shorterKeyLength) => shorterKeyLength switch
    {
        <= 4 => 0,
        <= 8 => 1,
        _ => 2,
    };
}
