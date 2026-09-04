using DonitGames.Core.Words;

namespace DonitGames.Core.JustOne;

/// <summary>
/// Groups clues by <c>WordNormalizer.IsNearDuplicate</c> — the single edge test needed, since it
/// already treats an exact match as a subset of "near" (checks the normalized key first, falls
/// through to edit distance only if that's not equal). One union-find pass over that edge makes
/// cancellation transitive (A≈B≈C groups together even if A and C aren't directly near each
/// other) automatically. Each resulting group of size ≥ 2 is either auto-cancelled (it contains
/// at least one exact-match pair) or a review group (every edge inside it is near-but-not-exact)
/// — never partially cancelled.
/// </summary>
public static class DuplicateDetector
{
    public sealed record Analysis(IReadOnlyList<Guid> AutoCancelledSeatIds, IReadOnlyList<ReviewGroup> ReviewGroups);

    public static Analysis Analyze(IReadOnlyList<Clue> clues)
    {
        ArgumentNullException.ThrowIfNull(clues);

        var parent = clues.ToDictionary(c => c.SeatId, c => c.SeatId);

        Guid Find(Guid x)
        {
            while (parent[x] != x)
            {
                parent[x] = parent[parent[x]];
                x = parent[x];
            }

            return x;
        }

        void Union(Guid a, Guid b)
        {
            var rootA = Find(a);
            var rootB = Find(b);
            if (rootA != rootB)
            {
                parent[rootA] = rootB;
            }
        }

        var exactPairRoots = new HashSet<Guid>();
        for (var i = 0; i < clues.Count; i++)
        {
            for (var j = i + 1; j < clues.Count; j++)
            {
                if (!WordNormalizer.IsNearDuplicate(clues[i].Text, clues[j].Text))
                {
                    continue;
                }

                Union(clues[i].SeatId, clues[j].SeatId);
                if (WordNormalizer.AreEquivalent(clues[i].Text, clues[j].Text))
                {
                    exactPairRoots.Add(Find(clues[i].SeatId));
                }
            }
        }

        // Union() can change a group's root after an exact pair was recorded against an older
        // one — re-resolve every recorded root to its final one before comparing.
        var finalExactPairRoots = exactPairRoots.Select(Find).ToHashSet();

        var autoCancelled = new List<Guid>();
        var reviewGroups = new List<ReviewGroup>();

        foreach (var group in clues.Select(c => c.SeatId).GroupBy(Find).Where(g => g.Count() > 1))
        {
            var seatIds = group.ToList();
            if (finalExactPairRoots.Contains(group.Key))
            {
                autoCancelled.AddRange(seatIds);
            }
            else
            {
                reviewGroups.Add(new ReviewGroup(seatIds, ManuallyCancelled: false));
            }
        }

        return new Analysis(autoCancelled, reviewGroups);
    }
}
