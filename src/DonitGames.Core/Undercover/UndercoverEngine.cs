using DonitGames.Core.Rooms;
using DonitGames.Core.Words;

namespace DonitGames.Core.Undercover;

/// <summary>
/// Static and pure — every function takes the current <see cref="UndercoverState"/> (and a
/// <see cref="Random"/> where randomness is needed, never constructed internally) and returns a
/// new one, so every rule here is testable without a room, a circuit, or any host machinery.
/// </summary>
public static class UndercoverEngine
{
    public const int MinPlayers = 3;
    public const int MaxPlayers = 10;
    public static readonly TimeSpan DiscussionDuration = TimeSpan.FromSeconds(300);

    /// <summary>Starts the very first game in a room (from <see cref="UndercoverPhase.Lobby"/>).</summary>
    public static UndercoverState StartGame(UndercoverState state, IReadOnlyList<Seat> seats, WordCategory category, Random rng) =>
        BeginGame(state, seats, category, rng);

    /// <summary>Starts another game in the same room — same seats, scores carried over, a fresh
    /// word pair excluding everything already used this room (Phase 1's draw-without-replacement).</summary>
    public static UndercoverState StartNewGame(UndercoverState state, IReadOnlyList<Seat> seats, WordCategory category, Random rng) =>
        BeginGame(state, seats, category, rng);

    private static UndercoverState BeginGame(UndercoverState state, IReadOnlyList<Seat> seats, WordCategory category, Random rng)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(seats);
        ArgumentNullException.ThrowIfNull(category);
        ArgumentNullException.ThrowIfNull(rng);

        var counts = RoleDistribution.Default(seats.Count);
        var excluded = new HashSet<WordPair>(state.UsedPairs);
        var drawn = WordPairProvider.Draw(category, rng, excluded);

        var roles = new List<UndercoverRole>(seats.Count);
        roles.AddRange(Enumerable.Repeat(UndercoverRole.Civilian, counts.Civilians));
        roles.AddRange(Enumerable.Repeat(UndercoverRole.Undercover, counts.Undercover));
        roles.AddRange(Enumerable.Repeat(UndercoverRole.MrWhite, counts.MrWhite));
        Shuffle(roles, rng);

        var deck = roles
            .Select((role, index) => new FaceDownCard(index, role, WordFor(role, drawn), TakenBySeatId: null))
            .ToList();

        var existingScores = state.Players.ToDictionary(p => p.SeatId, p => p.Score);
        var players = seats
            .Select(seat => new UndercoverPlayer(
                seat.SeatId,
                Role: null,
                SecretWord: null,
                IsEliminated: false,
                Score: existingScores.GetValueOrDefault(seat.SeatId, 0)))
            .ToList();

        return state with
        {
            Phase = UndercoverPhase.CardPicking,
            Players = players,
            Deck = deck,
            SpeakingOrder = [],
            DiscussionDeadlineUtc = null,
            Votes = new Dictionary<Guid, Guid>(),
            TiedSeatIds = [],
            JustEliminatedSeatId = null,
            MrWhiteGuesserSeatId = null,
            Winner = null,
            UsedPairs = [.. state.UsedPairs, drawn.Source],
            RoundNumber = state.RoundNumber + 1,
        };
    }

    private static string? WordFor(UndercoverRole role, OrientedPair pair) => role switch
    {
        UndercoverRole.Civilian => pair.Civilian,
        UndercoverRole.Undercover => pair.Undercover,
        UndercoverRole.MrWhite => null,
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };

    /// <remarks><paramref name="rng"/> is only used if this pick completes the deck (every seat
    /// now holds a card), to compute the first speaking order.</remarks>
    public static (UndercoverState State, PickResult Result) PickCard(UndercoverState state, Guid seatId, int cardIndex, Random rng)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(rng);

        if (state.Phase != UndercoverPhase.CardPicking)
        {
            return (state, PickResult.WrongPhase);
        }

        var player = state.Players.FirstOrDefault(p => p.SeatId == seatId);
        if (player is null)
        {
            return (state, PickResult.WrongPhase);
        }

        if (player.Role is not null)
        {
            return (state, PickResult.AlreadyHaveACard);
        }

        var card = state.Deck.FirstOrDefault(c => c.Index == cardIndex);
        if (card is null || card.TakenBySeatId is not null)
        {
            return (state, PickResult.CardAlreadyTaken);
        }

        var deck = state.Deck
            .Select(c => c.Index == cardIndex ? c with { TakenBySeatId = seatId } : c)
            .ToList();
        var players = state.Players
            .Select(p => p.SeatId == seatId ? p with { Role = card.Role, SecretWord = card.Word } : p)
            .ToList();

        var next = state with { Deck = deck, Players = players };

        // Once every seat holds a card, discussion starts — speaking order + a fresh deadline.
        if (next.Players.All(p => p.Role is not null))
        {
            next = next with
            {
                Phase = UndercoverPhase.Discussion,
                SpeakingOrder = Undercover.SpeakingOrder.Compute(next.Players.Where(p => !p.IsEliminated).ToList(), rng),
                DiscussionDeadlineUtc = DateTimeOffset.UtcNow + DiscussionDuration,
            };
        }

        return (next, PickResult.Success);
    }

    /// <summary>Host-triggered (the Web layer only shows the button to the host — engine
    /// functions here don't re-validate host-ness, matching Phase 2's precedent for
    /// <c>GameRoom.RemoveSeat</c>/HostTools). Clears any leftover votes from a previous round.</summary>
    public static UndercoverState StartVoting(UndercoverState state) =>
        state.Phase != UndercoverPhase.Discussion
            ? state
            : state with { Phase = UndercoverPhase.Voting, Votes = new Dictionary<Guid, Guid>(), TiedSeatIds = [] };

    public static (UndercoverState State, VoteResult Result) CastVote(UndercoverState state, Guid voterSeatId, Guid targetSeatId)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Phase != UndercoverPhase.Voting)
        {
            return (state, VoteResult.WrongPhase);
        }

        var voter = state.Players.FirstOrDefault(p => p.SeatId == voterSeatId);
        var target = state.Players.FirstOrDefault(p => p.SeatId == targetSeatId);
        if (voter is null || voter.IsEliminated || target is null || target.IsEliminated)
        {
            return (state, VoteResult.SeatNotActive);
        }

        var votes = new Dictionary<Guid, Guid>(state.Votes) { [voterSeatId] = targetSeatId };
        var next = state with { Votes = votes };

        var activeSeatIds = next.Players.Where(p => !p.IsEliminated).Select(p => p.SeatId).ToHashSet();
        if (!activeSeatIds.IsSubsetOf(votes.Keys))
        {
            // Not everyone active has voted yet.
            return (next, VoteResult.Success);
        }

        var tally = votes.Values.GroupBy(v => v).OrderByDescending(g => g.Count()).ToList();
        var topCount = tally[0].Count();
        var topSeats = tally.Where(g => g.Count() == topCount).Select(g => g.Key).ToList();

        next = topSeats.Count == 1
            ? Eliminate(next, topSeats[0])
            : next with { TiedSeatIds = topSeats };

        return (next, VoteResult.Success);
    }

    public static UndercoverState ResolveTie(UndercoverState state, Guid chosenSeatId)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.TiedSeatIds.Contains(chosenSeatId) ? Eliminate(state with { TiedSeatIds = [] }, chosenSeatId) : state;
    }

    /// <summary>Host override — available any time during Voting, e.g. an away player is
    /// stalling the round.</summary>
    public static UndercoverState ForceEliminate(UndercoverState state, Guid targetSeatId)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Phase != UndercoverPhase.Voting || state.Players.All(p => p.SeatId != targetSeatId))
        {
            return state;
        }

        return Eliminate(state with { TiedSeatIds = [] }, targetSeatId);
    }

    /// <summary>Always stops at EliminationReveal — routing to MrWhiteGuess or back into the
    /// game happens only once <see cref="AcknowledgeElimination"/> dismisses that beat, so every
    /// eliminated seat gets its moment shown before the round rushes on.</summary>
    private static UndercoverState Eliminate(UndercoverState state, Guid seatId)
    {
        var players = state.Players.Select(p => p.SeatId == seatId ? p with { IsEliminated = true } : p).ToList();

        return state with
        {
            // Votes deliberately survive into EliminationReveal (cleared instead at the start of
            // the *next* StartVoting) so the reveal can show the final tally that led here.
            Phase = UndercoverPhase.EliminationReveal,
            Players = players,
            JustEliminatedSeatId = seatId,
        };
    }

    /// <summary>Any active seat can dismiss the elimination reveal — it's acknowledging a fact,
    /// not a decision, so it isn't host-gated. Routes to MrWhiteGuess if that's who was just
    /// eliminated, otherwise re-checks the win condition.</summary>
    public static UndercoverState AcknowledgeElimination(UndercoverState state, Random rng)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(rng);

        if (state.Phase != UndercoverPhase.EliminationReveal || state.JustEliminatedSeatId is not { } seatId)
        {
            return state;
        }

        var eliminated = state.Players.First(p => p.SeatId == seatId);
        return eliminated.Role == UndercoverRole.MrWhite
            ? state with { Phase = UndercoverPhase.MrWhiteGuess, MrWhiteGuesserSeatId = seatId, JustEliminatedSeatId = null }
            : ApplyWinCheckOrContinue(state, rng);
    }

    public static (UndercoverState State, GuessResult Result) SubmitMrWhiteGuess(UndercoverState state, Guid guesserSeatId, string guess, Random rng)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(guess);
        ArgumentNullException.ThrowIfNull(rng);

        if (state.Phase != UndercoverPhase.MrWhiteGuess)
        {
            return (state, GuessResult.WrongPhase);
        }

        if (state.MrWhiteGuesserSeatId != guesserSeatId)
        {
            return (state, GuessResult.NotYourGuess);
        }

        var civilianWord = state.Deck.First(c => c.Role == UndercoverRole.Civilian).Word!;
        if (WordNormalizer.AreEquivalent(guess, civilianWord))
        {
            var players = state.Players
                .Select(p => p.SeatId == guesserSeatId ? p with { Score = p.Score + 3 } : p)
                .ToList();
            return (state with { Phase = UndercoverPhase.Results, Players = players, Winner = UndercoverWinner.MrWhiteSolo }, GuessResult.Correct);
        }

        return (ApplyWinCheckOrContinue(state with { MrWhiteGuesserSeatId = null }, rng), GuessResult.Incorrect);
    }

    /// <summary>The single place every elimination path routes through — the one source of
    /// truth for "is the game over", counting only survivors (fixes bug #4).</summary>
    private static UndercoverState ApplyWinCheckOrContinue(UndercoverState state, Random rng)
    {
        var survivors = state.Players.Where(p => !p.IsEliminated).ToList();
        var civilians = survivors.Count(p => p.Role == UndercoverRole.Civilian);
        var threats = survivors.Count(p => p.Role is UndercoverRole.Undercover or UndercoverRole.MrWhite);

        UndercoverWinner? winner = threats == 0
            ? UndercoverWinner.Civilians
            : threats >= civilians
                ? UndercoverWinner.UndercoverTeam
                : null;

        if (winner is null)
        {
            return state with
            {
                Phase = UndercoverPhase.Discussion,
                SpeakingOrder = SpeakingOrder.Compute(survivors, rng),
                DiscussionDeadlineUtc = DateTimeOffset.UtcNow + DiscussionDuration,
                JustEliminatedSeatId = null,
            };
        }

        // Survivors-only payout on both sides (fixes bug #3 — the reference pays every civilian
        // ever in the game, but only surviving Undercover/Mr. White).
        var winningRoles = winner == UndercoverWinner.Civilians
            ? new[] { UndercoverRole.Civilian }
            : [UndercoverRole.Undercover, UndercoverRole.MrWhite];

        var players = state.Players
            .Select(p => !p.IsEliminated && p.Role is not null && winningRoles.Contains(p.Role.Value)
                ? p with { Score = p.Score + 1 }
                : p)
            .ToList();

        return state with { Phase = UndercoverPhase.Results, Players = players, Winner = winner, JustEliminatedSeatId = null };
    }

    private static void Shuffle<T>(List<T> items, Random rng)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}
