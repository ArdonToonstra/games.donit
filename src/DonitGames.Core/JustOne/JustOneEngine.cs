using DonitGames.Core.Rooms;
using DonitGames.Core.Words;

namespace DonitGames.Core.JustOne;

/// <summary>
/// Static and pure — every function takes the current <see cref="JustOneState"/> (and a
/// <see cref="Random"/> where randomness is needed, never constructed internally) and returns a
/// new one, matching the convention already established in Phases 1 and 3.
/// </summary>
public static class JustOneEngine
{
    public const int MinPlayers = 3;
    public const int MaxPlayers = 8;

    public static JustOneState StartGame(JustOneState state, IReadOnlyList<Seat> seats, IReadOnlyList<string> words, Random rng) =>
        BeginRound(state, seats, words, rng);

    /// <summary>Same room, same seats/scores lineage — resets the pip track and tally, keeps
    /// <see cref="JustOneState.UsedWords"/> growing (no repeats across games in one sitting,
    /// same reasoning as Undercover's <c>UsedPairs</c>) and keeps the guesser rotation going
    /// rather than always restarting at seat 0.</summary>
    public static JustOneState StartNewGame(JustOneState state, IReadOnlyList<Seat> seats, IReadOnlyList<string> words, Random rng) =>
        BeginRound(state with { PipsRemaining = JustOneState.StartingPips, CorrectCount = 0, RoundsPlayed = 0 }, seats, words, rng);

    private static JustOneState BeginRound(JustOneState state, IReadOnlyList<Seat> seats, IReadOnlyList<string> words, Random rng)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(seats);
        ArgumentNullException.ThrowIfNull(words);
        ArgumentNullException.ThrowIfNull(rng);

        if (seats.Count is < MinPlayers or > MaxPlayers)
        {
            throw new ArgumentOutOfRangeException(nameof(seats), seats.Count, $"Just One supports {MinPlayers} to {MaxPlayers} players.");
        }

        var guesserSeatId = PickNextGuesser(state, seats);
        var excluded = new HashSet<string>(state.UsedWords);
        var word = JustOneWordBank.Draw(words, rng, excluded);

        return state with
        {
            Phase = JustOnePhase.ClueWriting,
            GuesserSeatId = guesserSeatId,
            SecretWord = word,
            Clues = [],
            AutoCancelledSeatIds = [],
            ReviewGroups = [],
            GuesserAttempt = null,
            LastOutcome = null,
            UsedWords = [.. state.UsedWords, word],
            RoundNumber = state.RoundNumber + 1,
        };
    }

    private static Guid PickNextGuesser(JustOneState state, IReadOnlyList<Seat> seats)
    {
        var lastIndex = state.GuesserSeatId is { } lastGuesser
            ? seats.ToList().FindIndex(s => s.SeatId == lastGuesser)
            : -1;

        // -1 covers both "no previous guesser" (first round) and "the last guesser is no longer
        // in the room" (kicked) — either way, start back at the top of the current roster.
        return lastIndex < 0 ? seats[0].SeatId : seats[(lastIndex + 1) % seats.Count].SeatId;
    }

    /// <remarks>Auto-advances to <see cref="JustOnePhase.DuplicateReview"/> once every active
    /// non-guesser seat in <paramref name="seats"/> has submitted — mirrors <c>CastVote</c>'s
    /// auto-resolve in the Undercover engine. <see cref="CloseClueWriting"/> is the host/judge
    /// override for the <c>Away</c> clue-giver case.</remarks>
    public static JustOneState SubmitClue(JustOneState state, IReadOnlyList<Seat> seats, Guid seatId, string text)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(seats);
        ArgumentNullException.ThrowIfNull(text);

        if (state.Phase != JustOnePhase.ClueWriting || seatId == state.GuesserSeatId)
        {
            return state;
        }

        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return state;
        }

        // Replacing rather than rejecting a second submission lets someone fix a typo before
        // the round closes.
        var clues = state.Clues.Where(c => c.SeatId != seatId).Append(new Clue(seatId, trimmed)).ToList();
        var next = state with { Clues = clues };

        var expectedSeatIds = seats.Where(s => s.SeatId != state.GuesserSeatId).Select(s => s.SeatId).ToHashSet();
        var submittedSeatIds = clues.Select(c => c.SeatId).ToHashSet();
        return expectedSeatIds.IsSubsetOf(submittedSeatIds) ? CloseClueWriting(next) : next;
    }

    /// <summary>Host/judge override — closes clue-writing even if not every seat has submitted
    /// (the <c>Away</c> clue-giver degenerate case).</summary>
    public static JustOneState CloseClueWriting(JustOneState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Phase != JustOnePhase.ClueWriting)
        {
            return state;
        }

        var analysis = DuplicateDetector.Analyze(state.Clues);
        return state with
        {
            Phase = JustOnePhase.DuplicateReview,
            AutoCancelledSeatIds = analysis.AutoCancelledSeatIds,
            ReviewGroups = analysis.ReviewGroups,
        };
    }

    public static JustOneState ToggleReviewGroup(JustOneState state, int groupIndex)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Phase != JustOnePhase.DuplicateReview || groupIndex < 0 || groupIndex >= state.ReviewGroups.Count)
        {
            return state;
        }

        var groups = state.ReviewGroups
            .Select((g, i) => i == groupIndex ? g with { ManuallyCancelled = !g.ManuallyCancelled } : g)
            .ToList();
        return state with { ReviewGroups = groups };
    }

    /// <summary>The judge's one click. Zero surviving clues (everyone duplicated, or nobody
    /// submitted) is the other named degenerate case — skips Guessing entirely.</summary>
    public static JustOneState Reveal(JustOneState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Phase != JustOnePhase.DuplicateReview)
        {
            return state;
        }

        var cancelled = CancelledSeatIds(state);
        var survivingCount = state.Clues.Count(c => !cancelled.Contains(c.SeatId));
        return survivingCount == 0 ? ApplyOutcome(state, RoundOutcome.NoClues) : state with { Phase = JustOnePhase.Guessing };
    }

    private static HashSet<Guid> CancelledSeatIds(JustOneState state) =>
        state.AutoCancelledSeatIds
            .Concat(state.ReviewGroups.Where(g => g.ManuallyCancelled).SelectMany(g => g.SeatIds))
            .ToHashSet();

    public static JustOneState Pass(JustOneState state, Guid guesserSeatId)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.Phase != JustOnePhase.Guessing || state.GuesserSeatId != guesserSeatId
            ? state
            : ApplyOutcome(state, RoundOutcome.Passed);
    }

    /// <summary>Auto-accepts only on an exact normalized match — never auto-rejects. Anything
    /// else escalates to <see cref="JudgeReview"/> rather than being auto-marked wrong, since a
    /// guess can be "close enough" in ways no string comparison captures.</summary>
    public static JustOneState SubmitGuess(JustOneState state, Guid guesserSeatId, string guess)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(guess);

        if (state.Phase != JustOnePhase.Guessing || state.GuesserSeatId != guesserSeatId)
        {
            return state;
        }

        var attempt = guess.Trim();
        if (attempt.Length == 0)
        {
            return state;
        }

        if (WordNormalizer.AreEquivalent(attempt, state.SecretWord!))
        {
            return ApplyOutcome(state with { GuesserAttempt = attempt }, RoundOutcome.Correct);
        }

        return state with { Phase = JustOnePhase.JudgeReview, GuesserAttempt = attempt };
    }

    public static JustOneState JudgeDecision(JustOneState state, bool correct)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.Phase != JustOnePhase.JudgeReview
            ? state
            : ApplyOutcome(state, correct ? RoundOutcome.Correct : RoundOutcome.Incorrect);
    }

    private static JustOneState ApplyOutcome(JustOneState state, RoundOutcome outcome)
    {
        var cost = outcome == RoundOutcome.Incorrect ? 2 : 1;
        return state with
        {
            Phase = JustOnePhase.RoundResult,
            LastOutcome = outcome,
            PipsRemaining = Math.Max(0, state.PipsRemaining - cost),
            CorrectCount = outcome == RoundOutcome.Correct ? state.CorrectCount + 1 : state.CorrectCount,
            RoundsPlayed = state.RoundsPlayed + 1,
        };
    }

    /// <summary>Dismissed by any seat — acknowledging a fact, not a decision (same reasoning as
    /// Undercover's <c>AcknowledgeElimination</c>). Advances to the next round, or to
    /// <see cref="JustOnePhase.GameResults"/> once the deck is empty.</summary>
    public static JustOneState AcknowledgeRoundResult(JustOneState state, IReadOnlyList<Seat> seats, IReadOnlyList<string> words, Random rng)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Phase != JustOnePhase.RoundResult)
        {
            return state;
        }

        return state.PipsRemaining <= 0
            ? state with { Phase = JustOnePhase.GameResults }
            : BeginRound(state, seats, words, rng);
    }

    /// <summary>Computed, not stored — the room host, unless they're this round's guesser (can't
    /// judge their own guess), in which case it falls to the first other active seat.</summary>
    public static Guid? Judge(RoomSnapshot<JustOneState> snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var host = snapshot.Seats.FirstOrDefault(s => s.IsHost);
        if (host is null)
        {
            return null;
        }

        if (host.SeatId != snapshot.Game.GuesserSeatId)
        {
            return host.SeatId;
        }

        return snapshot.Seats.FirstOrDefault(s => s.SeatId != snapshot.Game.GuesserSeatId)?.SeatId;
    }
}
