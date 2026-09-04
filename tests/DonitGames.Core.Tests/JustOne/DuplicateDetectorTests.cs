using DonitGames.Core.JustOne;

namespace DonitGames.Core.Tests.JustOne;

public class DuplicateDetectorTests
{
    [Fact]
    public void A_three_way_exact_duplicate_cancels_all_three_not_two_of_three()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var clues = new[] { new Clue(a, "Kaas"), new Clue(b, "kaas"), new Clue(c, "Kaas") };

        var analysis = DuplicateDetector.Analyze(clues);

        Assert.Equal(3, analysis.AutoCancelledSeatIds.Count);
        Assert.Contains(a, analysis.AutoCancelledSeatIds);
        Assert.Contains(b, analysis.AutoCancelledSeatIds);
        Assert.Contains(c, analysis.AutoCancelledSeatIds);
        Assert.Empty(analysis.ReviewGroups);
    }

    [Fact]
    public void A_transitive_chain_groups_all_three_even_when_the_ends_are_not_directly_near()
    {
        // "aaaaa" is one edit from "aaaab" (near-dup, min length 5 -> threshold 1) which is one
        // edit from "aaabb" (also near-dup) — but "aaaaa" to "aaabb" is 2 edits, over that same
        // threshold, so a pairwise-only check would never group the two ends directly.
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var clues = new[] { new Clue(a, "aaaaa"), new Clue(b, "aaaab"), new Clue(c, "aaabb") };
        Assert.Equal(1, DonitGames.Core.Words.EditDistance.Levenshtein("aaaaa", "aaaab"));
        Assert.Equal(1, DonitGames.Core.Words.EditDistance.Levenshtein("aaaab", "aaabb"));
        Assert.Equal(2, DonitGames.Core.Words.EditDistance.Levenshtein("aaaaa", "aaabb"));

        var analysis = DuplicateDetector.Analyze(clues);

        var group = Assert.Single(analysis.ReviewGroups);
        Assert.Equal(new[] { a, b, c }.OrderBy(x => x), group.SeatIds.OrderBy(x => x));
        Assert.Empty(analysis.AutoCancelledSeatIds);
    }

    [Fact]
    public void A_near_duplicate_pair_with_no_exact_match_is_flagged_not_cancelled()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var clues = new[] { new Clue(a, "Koffie"), new Clue(b, "Kofie") };

        var analysis = DuplicateDetector.Analyze(clues);

        Assert.Empty(analysis.AutoCancelledSeatIds);
        var group = Assert.Single(analysis.ReviewGroups);
        Assert.Equal(2, group.SeatIds.Count);
        Assert.False(group.ManuallyCancelled);
    }

    [Fact]
    public void A_group_containing_any_exact_pair_auto_cancels_entirely_even_with_a_near_only_member()
    {
        // A exact== B, B near-but-not-exact C: the whole {A,B,C} component auto-cancels, since it
        // contains a provable exact-duplicate pair.
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var clues = new[] { new Clue(a, "Koffie"), new Clue(b, "Koffie"), new Clue(c, "Kofie") };

        var analysis = DuplicateDetector.Analyze(clues);

        Assert.Equal(3, analysis.AutoCancelledSeatIds.Count);
        Assert.Empty(analysis.ReviewGroups);
    }

    [Theory]
    [InlineData("kaas", "kaa")]
    [InlineData("ijs", "ij")]
    [InlineData("maan", "man")]
    public void Unrelated_short_words_from_the_normalizer_table_stay_ungrouped(string a, string b)
    {
        var seatA = Guid.NewGuid();
        var seatB = Guid.NewGuid();
        var clues = new[] { new Clue(seatA, a), new Clue(seatB, b) };

        var analysis = DuplicateDetector.Analyze(clues);

        Assert.Empty(analysis.AutoCancelledSeatIds);
        Assert.Empty(analysis.ReviewGroups);
    }

    [Fact]
    public void Unrelated_clues_produce_no_groups_at_all()
    {
        var clues = new[] { new Clue(Guid.NewGuid(), "Kaas"), new Clue(Guid.NewGuid(), "Fiets"), new Clue(Guid.NewGuid(), "Zon") };

        var analysis = DuplicateDetector.Analyze(clues);

        Assert.Empty(analysis.AutoCancelledSeatIds);
        Assert.Empty(analysis.ReviewGroups);
    }
}
