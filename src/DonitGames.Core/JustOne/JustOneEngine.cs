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

    /// <summary>A clue is one word. The cap is defensive, not a rule: the input already carries
    /// a matching <c>maxlength</c>, but a paste on a phone keyboard routes around that, and a
    /// 400-character "clue" would wreck every other player's layout mid-round.</summary>
    public const int MaxClueLength = 24;

    public const int MaxGuessLength = 32;

    /// <summary>Whether <see cref="StartGame"/>/<see cref="StartNewGame"/> would be legal right
    /// now. Callers check this *inside* their mutator, against the room's current seats — a
    /// button's disabled state is computed from a snapshot that may already be one kick old.</summary>
    public static bool CanStart(IReadOnlyList<Seat> seats)
    {
        ArgumentNullException.ThrowIfNull(seats);
        return seats.Count is >= MinPlayers and <= MaxPlayers;
    }

    /// <summary>Lobby only. The two modes have different phase sets, so a switch mid-game would
    /// leave whoever is mid-phase on a screen the new mode never reaches.</summary>
    public static JustOneState SetMode(JustOneState state, JustOneMode mode)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.Phase is JustOnePhase.Lobby or JustOnePhase.GameResults ? state with { Mode = mode } : state;
    }

    public static JustOneState StartGame(JustOneState state, IReadOnlyList<Seat> seats, IReadOnlyList<string> words, Random rng)
    {
        RequireStartableTable(seats);
        return BeginRound(state, seats, words, rng);
    }

    /// <summary>Same room, same seats/scores lineage — resets the pip track and tally, keeps
    /// <see cref="JustOneState.UsedWords"/> growing (no repeats across games in one sitting,
    /// same reasoning as Undercover's <c>UsedPairs</c>) and keeps the guesser rotation going
    /// rather than always restarting at seat 0.</summary>
    public static JustOneState StartNewGame(JustOneState state, IReadOnlyList<Seat> seats, IReadOnlyList<string> words, Random rng)
    {
        ArgumentNullException.ThrowIfNull(state);
        RequireStartableTable(seats);
        return BeginRound(
            state with { PipsRemaining = JustOneState.StartingPips, CorrectCount = 0, RoundsPlayed = 0 },
            seats,
            words,
            rng);
    }

    /// <summary>The full lobby range, enforced only where a game is *started*. Guard with
    /// <see cref="CanStart"/> first — reaching this exception from a UI path means a button was
    /// enabled against a stale snapshot.</summary>
    private static void RequireStartableTable(IReadOnlyList<Seat> seats)
    {
        ArgumentNullException.ThrowIfNull(seats);

        if (!CanStart(seats))
        {
            throw new ArgumentOutOfRangeException(nameof(seats), seats.Count, $"Just One supports {MinPlayers} to {MaxPlayers} players.");
        }
    }

    private static JustOneState BeginRound(JustOneState state, IReadOnlyList<Seat> seats, IReadOnlyList<string> words, Random rng)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(seats);
        ArgumentNullException.ThrowIfNull(words);
        ArgumentNullException.ThrowIfNull(rng);

        // Only the floor, not CanStart's full range: the ceiling is a lobby rule about how big a
        // game to *start*, and enforcing it here would end a running game the moment a ninth
        // person wandered in off the join link. Every caller that can reach an empty table
        // guards this already, so a violation here is a bug rather than a table state.
        if (seats.Count < MinPlayers)
        {
            throw new ArgumentOutOfRangeException(nameof(seats), seats.Count, $"Just One needs at least {MinPlayers} players.");
        }

        var guesserSeatId = PickNextGuesser(state, seats);

        // The bank running dry mid-sitting used to throw out of Mutate and take the tapping
        // player's circuit down with it, leaving the room wedged on RoundResult with no way
        // forward. Recycling is the only sane recovery: start a fresh no-repeat window rather
        // than refuse to deal a round.
        var used = new HashSet<string>(state.UsedWords);
        var exhausted = words.All(used.Contains);
        var word = JustOneWordBank.Draw(words, rng, exhausted ? new HashSet<string>() : used);

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
            UsedWords = exhausted ? [word] : [.. state.UsedWords, word],
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

    /// <summary>Collapses every run of whitespace (a pasted newline included) to a single space
    /// and clips to <paramref name="maxLength"/>, so what the room renders is always one short
    /// line however it was typed or pasted.</summary>
    private static string Sanitize(string text, int maxLength)
    {
        var collapsed = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return collapsed.Length <= maxLength ? collapsed : collapsed[..maxLength].TrimEnd();
    }

    /// <remarks>Auto-closes clue-writing once every active non-guesser seat in
    /// <paramref name="seats"/> has submitted — mirrors <c>CastVote</c>'s auto-resolve in the
    /// Undercover engine. Where that lands depends on the mode (see
    /// <see cref="CloseClueWriting"/>), which is also the host/judge override for the
    /// <c>Away</c> clue-giver case.</remarks>
    public static JustOneState SubmitClue(JustOneState state, IReadOnlyList<Seat> seats, Guid seatId, string text)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(seats);
        ArgumentNullException.ThrowIfNull(text);

        if (state.Phase != JustOnePhase.ClueWriting || seatId == state.GuesserSeatId)
        {
            return state;
        }

        var trimmed = Sanitize(text, MaxClueLength);
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

        if (state.Mode == JustOneMode.Table)
        {
            // No DuplicateDetector pass at all in table mode — five clues held up side by side
            // is a better duplicate check than any edit-distance threshold, and it is the part
            // of the game people actually enjoy arguing about.
            return state.Clues.Count == 0
                ? ApplyOutcome(state, RoundOutcome.NoClues)
                : state with { Phase = JustOnePhase.ClueReveal };
        }

        var analysis = DuplicateDetector.Analyze(state.Clues);
        return state with
        {
            Phase = JustOnePhase.DuplicateReview,
            AutoCancelledSeatIds = analysis.AutoCancelledSeatIds,
            ReviewGroups = analysis.ReviewGroups,
        };
    }

    /// <summary>Table mode: the guesser has said their answer out loud and hands the round back
    /// to the table. Theirs is the one phone not being held up, so theirs is the button.</summary>
    public static JustOneState FinishGuessing(JustOneState state, Guid guesserSeatId)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.Phase != JustOnePhase.ClueReveal || state.GuesserSeatId != guesserSeatId
            ? state
            : state with { Phase = JustOnePhase.TableVerdict };
    }

    /// <summary>Table mode: any clue-giver records what the table decided. Deliberately not
    /// restricted to the judge — in this mode the phones have just come down and whoever is
    /// holding theirs closest taps it, which is also one less seat that can wedge the round by
    /// being offline.</summary>
    public static JustOneState RecordTableVerdict(JustOneState state, Guid seatId, bool correct)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.Phase != JustOnePhase.TableVerdict || seatId == state.GuesserSeatId
            ? state
            : ApplyOutcome(state, correct ? RoundOutcome.Correct : RoundOutcome.Incorrect);
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

    /// <summary>The guesser giving up, from whichever phase their mode puts them in.</summary>
    public static JustOneState Pass(JustOneState state, Guid guesserSeatId)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.Phase is not (JustOnePhase.Guessing or JustOnePhase.ClueReveal) || state.GuesserSeatId != guesserSeatId
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

        var attempt = Sanitize(guess, MaxGuessLength);
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

        // The pip track is the normal end. Seats dropping below MinPlayers is the other one:
        // this button is tapped by *anyone*, so letting BeginRound throw here would kill that
        // player's circuit and strand the room on a screen whose only button is now fatal.
        // Only the lower bound is checked — a ninth player wandering in mid-game shouldn't end
        // the sitting, they just join the round.
        return state.PipsRemaining <= 0 || seats.Count < MinPlayers
            ? state with { Phase = JustOnePhase.GameResults }
            : BeginRound(state, seats, words, rng);
    }

    /// <summary>The host's way out of a round nobody can finish — a judge whose phone is dead,
    /// a guesser who left, or simply a table that wants to stop. Always available while a game
    /// is running, because every *other* control in this engine belongs to a specific seat that
    /// may be the one that's gone.</summary>
    public static JustOneState EndGame(JustOneState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.Phase is JustOnePhase.Lobby or JustOnePhase.GameResults
            ? state
            : state with { Phase = JustOnePhase.GameResults };
    }

    /// <summary>
    /// Computed, not stored — the room host, unless they're this round's guesser (can't judge
    /// their own guess) or their phone is currently gone, in which case it falls to another seat.
    ///
    /// Connectedness is part of the choice rather than a nicety: Reveal and the correct/wrong
    /// call are the only ways out of DuplicateReview and JudgeReview, so pinning them to a seat
    /// that isn't there wedges the whole room. The judge can therefore change hands mid-phase
    /// when someone's screen locks; that is the intended trade — the button moving is recoverable,
    /// a room with no button is not. When nobody is connected (a snapshot with no presence at
    /// all, as in tests) it degrades to the plain host-then-first-seat order.
    /// </summary>
    public static Guid? Judge(RoomSnapshot<JustOneState> snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var candidates = snapshot.Seats.Where(s => s.SeatId != snapshot.Game.GuesserSeatId).ToList();
        if (candidates.Count == 0)
        {
            return null;
        }

        bool IsConnected(Seat seat) =>
            snapshot.Presence.TryGetValue(seat.SeatId, out var presence) && presence.IsConnected;

        var host = candidates.FirstOrDefault(s => s.IsHost);
        if (host is not null && IsConnected(host))
        {
            return host.SeatId;
        }

        return (candidates.FirstOrDefault(IsConnected) ?? host ?? candidates[0]).SeatId;
    }
}
