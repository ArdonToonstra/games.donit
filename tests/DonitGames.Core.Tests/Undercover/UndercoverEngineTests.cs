using DonitGames.Core.Rooms;
using DonitGames.Core.Undercover;
using DonitGames.Core.Words;

namespace DonitGames.Core.Tests.Undercover;

public class UndercoverEngineTests
{
    private static WordCategory Category(params (string A, string B)[] pairs) =>
        new("Test", "", "NL", pairs.Select(p => new WordPair(p.A, p.B)).ToList());

    private static IReadOnlyList<Seat> Seats(int count) =>
        Enumerable.Range(0, count)
            .Select(i => new Seat(Guid.NewGuid(), $"Speler {i}", IsHost: i == 0, DateTimeOffset.UtcNow))
            .ToList();

    private static UndercoverState PickAllCards(UndercoverState state, IReadOnlyList<Seat> seats, Random rng)
    {
        foreach (var seat in seats)
        {
            var card = state.Deck.First(c => c.TakenBySeatId is null);
            (state, _) = UndercoverEngine.PickCard(state, seat.SeatId, card.Index, rng);
        }

        return state;
    }

    [Fact]
    public void StartGame_builds_a_deck_sized_exactly_to_player_count()
    {
        var seats = Seats(6);
        var state = UndercoverEngine.StartGame(UndercoverState.Initial, seats, Category(("Kaas", "Vla")), new Random(1));

        Assert.Equal(UndercoverPhase.CardPicking, state.Phase);
        Assert.Equal(6, state.Deck.Count);
        Assert.Equal(6, state.Players.Count);
        Assert.Equal(3, state.Deck.Count(c => c.Role == UndercoverRole.Civilian));
        Assert.Equal(2, state.Deck.Count(c => c.Role == UndercoverRole.Undercover));
        Assert.Equal(1, state.Deck.Count(c => c.Role == UndercoverRole.MrWhite));
    }

    [Fact]
    public void PickCard_assigns_role_and_word_and_removes_the_card_from_the_pool()
    {
        var seats = Seats(3);
        var state = UndercoverEngine.StartGame(UndercoverState.Initial, seats, Category(("Kaas", "Vla")), new Random(1));
        var card = state.Deck[0];

        var (next, result) = UndercoverEngine.PickCard(state, seats[0].SeatId, card.Index, new Random(1));

        Assert.Equal(PickResult.Success, result);
        var player = next.Players.First(p => p.SeatId == seats[0].SeatId);
        Assert.Equal(card.Role, player.Role);
        Assert.Equal(card.Word, player.SecretWord);
        Assert.Equal(seats[0].SeatId, next.Deck.First(c => c.Index == card.Index).TakenBySeatId);
    }

    [Fact]
    public void PickCard_returns_AlreadyHaveACard_for_a_second_pick_by_the_same_seat()
    {
        var seats = Seats(3);
        var state = UndercoverEngine.StartGame(UndercoverState.Initial, seats, Category(("Kaas", "Vla")), new Random(1));
        (state, _) = UndercoverEngine.PickCard(state, seats[0].SeatId, state.Deck[0].Index, new Random(1));

        var (_, result) = UndercoverEngine.PickCard(state, seats[0].SeatId, state.Deck[1].Index, new Random(1));

        Assert.Equal(PickResult.AlreadyHaveACard, result);
    }

    [Fact]
    public void PickCard_returns_WrongPhase_outside_CardPicking()
    {
        var (_, result) = UndercoverEngine.PickCard(UndercoverState.Initial, Guid.NewGuid(), 0, new Random(1));
        Assert.Equal(PickResult.WrongPhase, result);
    }

    [Fact]
    public void PickCard_transitions_to_Discussion_once_every_seat_has_picked()
    {
        var seats = Seats(3);
        var rng = new Random(1);
        var state = UndercoverEngine.StartGame(UndercoverState.Initial, seats, Category(("Kaas", "Vla")), rng);

        state = PickAllCards(state, seats, rng);

        Assert.Equal(UndercoverPhase.Discussion, state.Phase);
        Assert.Equal(3, state.SpeakingOrder.Count);
        Assert.NotNull(state.DiscussionDeadlineUtc);
    }

    [Fact]
    public void Two_seats_racing_for_the_same_card_through_GameRoom_yields_exactly_one_winner()
    {
        var seats = Seats(4);
        var rng = new Random(7);
        var initial = UndercoverEngine.StartGame(UndercoverState.Initial, seats, Category(("Kaas", "Vla")), rng);
        var room = new GameRoom<UndercoverState>("TEST", initial, DateTimeOffset.UtcNow);
        var cardIndex = initial.Deck[0].Index;

        var results = new System.Collections.Concurrent.ConcurrentBag<PickResult>();
        Parallel.ForEach(seats.Take(2), seat =>
        {
            var (_, result) = room.Mutate(s =>
            {
                var (nextGame, pickResult) = UndercoverEngine.PickCard(s.Game, seat.SeatId, cardIndex, new Random());
                return (s with { Game = nextGame }, pickResult);
            });
            results.Add(result);
        });

        Assert.Equal(1, results.Count(r => r == PickResult.Success));
        Assert.Equal(1, results.Count(r => r == PickResult.CardAlreadyTaken));
        Assert.Equal(1, room.Read().Game.Deck.Count(c => c.Index == cardIndex && c.TakenBySeatId is not null));
    }

    [Fact]
    public void CastVote_auto_eliminates_on_a_unique_max()
    {
        var seats = Seats(3);
        var rng = new Random(2);
        var state = UndercoverEngine.StartGame(UndercoverState.Initial, seats, Category(("Kaas", "Vla")), rng);
        state = PickAllCards(state, seats, rng);
        state = UndercoverEngine.StartVoting(state);

        (state, _) = UndercoverEngine.CastVote(state, seats[0].SeatId, seats[2].SeatId);
        (state, _) = UndercoverEngine.CastVote(state, seats[1].SeatId, seats[2].SeatId);
        (state, _) = UndercoverEngine.CastVote(state, seats[2].SeatId, seats[0].SeatId);

        Assert.True(state.Players.First(p => p.SeatId == seats[2].SeatId).IsEliminated);
        Assert.True(state.Phase is UndercoverPhase.EliminationReveal or UndercoverPhase.MrWhiteGuess);
    }

    [Fact]
    public void CastVote_sets_TiedSeatIds_and_stays_in_Voting_on_a_tie()
    {
        var seats = Seats(4);
        var rng = new Random(3);
        var state = UndercoverEngine.StartGame(UndercoverState.Initial, seats, Category(("Kaas", "Vla")), rng);
        state = PickAllCards(state, seats, rng);
        state = UndercoverEngine.StartVoting(state);

        (state, _) = UndercoverEngine.CastVote(state, seats[0].SeatId, seats[2].SeatId);
        (state, _) = UndercoverEngine.CastVote(state, seats[1].SeatId, seats[3].SeatId);
        (state, _) = UndercoverEngine.CastVote(state, seats[2].SeatId, seats[2].SeatId);
        (state, _) = UndercoverEngine.CastVote(state, seats[3].SeatId, seats[3].SeatId);

        Assert.Equal(UndercoverPhase.Voting, state.Phase);
        Assert.Equal(2, state.TiedSeatIds.Count);
        Assert.Contains(seats[2].SeatId, state.TiedSeatIds);
        Assert.Contains(seats[3].SeatId, state.TiedSeatIds);
    }

    [Fact]
    public void ResolveTie_eliminates_the_chosen_seat_and_clears_the_tie()
    {
        var seats = Seats(4);
        var rng = new Random(3);
        var state = UndercoverEngine.StartGame(UndercoverState.Initial, seats, Category(("Kaas", "Vla")), rng);
        state = PickAllCards(state, seats, rng);
        state = UndercoverEngine.StartVoting(state);
        (state, _) = UndercoverEngine.CastVote(state, seats[0].SeatId, seats[2].SeatId);
        (state, _) = UndercoverEngine.CastVote(state, seats[1].SeatId, seats[3].SeatId);
        (state, _) = UndercoverEngine.CastVote(state, seats[2].SeatId, seats[2].SeatId);
        (state, _) = UndercoverEngine.CastVote(state, seats[3].SeatId, seats[3].SeatId);

        state = UndercoverEngine.ResolveTie(state, seats[2].SeatId);

        Assert.Empty(state.TiedSeatIds);
        Assert.True(state.Players.First(p => p.SeatId == seats[2].SeatId).IsEliminated);
    }

    [Fact]
    public void ForceEliminate_only_works_during_Voting()
    {
        var seats = Seats(3);
        var rng = new Random(1);
        var state = UndercoverEngine.StartGame(UndercoverState.Initial, seats, Category(("Kaas", "Vla")), rng);

        var unchanged = UndercoverEngine.ForceEliminate(state, seats[0].SeatId);

        Assert.Same(state, unchanged);
    }

    [Fact]
    public void Mr_White_guess_matches_accent_and_case_insensitively()
    {
        // Regression for bug #5: the reference app uses raw OrdinalIgnoreCase, so "cafe" fails
        // against "café". WordNormalizer.AreEquivalent must accept it.
        var mrWhite = new UndercoverPlayer(Guid.NewGuid(), UndercoverRole.MrWhite, null, true, 0);
        var civilian = new UndercoverPlayer(Guid.NewGuid(), UndercoverRole.Civilian, "Café", false, 0);
        var state = UndercoverState.Initial with
        {
            Phase = UndercoverPhase.MrWhiteGuess,
            Players = [mrWhite, civilian],
            Deck = [new FaceDownCard(0, UndercoverRole.Civilian, "Café", civilian.SeatId), new FaceDownCard(1, UndercoverRole.MrWhite, null, mrWhite.SeatId)],
            MrWhiteGuesserSeatId = mrWhite.SeatId,
        };

        var (next, result) = UndercoverEngine.SubmitMrWhiteGuess(state, mrWhite.SeatId, "cafe", new Random(1));

        Assert.Equal(GuessResult.Correct, result);
        Assert.Equal(UndercoverWinner.MrWhiteSolo, next.Winner);
        Assert.Equal(3, next.Players.First(p => p.SeatId == mrWhite.SeatId).Score);
    }

    [Fact]
    public void Civilian_win_pays_only_surviving_civilians_not_the_whole_roster()
    {
        // Regression for bug #3: the reference app pays every civilian ever in the game on a
        // civilian win, not just survivors.
        var eliminatedCivilian = new UndercoverPlayer(Guid.NewGuid(), UndercoverRole.Civilian, "Kaas", true, 0);
        var survivingCivilian = new UndercoverPlayer(Guid.NewGuid(), UndercoverRole.Civilian, "Kaas", false, 0);
        var mrWhite = new UndercoverPlayer(Guid.NewGuid(), UndercoverRole.MrWhite, null, true, 0);
        var state = UndercoverState.Initial with
        {
            Phase = UndercoverPhase.MrWhiteGuess,
            Players = [eliminatedCivilian, survivingCivilian, mrWhite],
            Deck = [new FaceDownCard(0, UndercoverRole.Civilian, "Kaas", null)],
            MrWhiteGuesserSeatId = mrWhite.SeatId,
        };

        var (next, result) = UndercoverEngine.SubmitMrWhiteGuess(state, mrWhite.SeatId, "wrong-guess", new Random(1));

        Assert.Equal(GuessResult.Incorrect, result);
        Assert.Equal(UndercoverWinner.Civilians, next.Winner);
        Assert.Equal(0, next.Players.First(p => p.SeatId == eliminatedCivilian.SeatId).Score);
        Assert.Equal(1, next.Players.First(p => p.SeatId == survivingCivilian.SeatId).Score);
    }

    [Fact]
    public void Wrong_MrWhite_guess_with_two_survivors_ends_the_game_on_survivor_count_not_total_roster()
    {
        // Regression for bug #4: "<=2 players left" must count survivors, not the full roster
        // (which here has 4 entries, 2 of them already eliminated earlier in the round).
        var eliminatedCivilian = new UndercoverPlayer(Guid.NewGuid(), UndercoverRole.Civilian, "Kaas", true, 0);
        var eliminatedUndercover = new UndercoverPlayer(Guid.NewGuid(), UndercoverRole.Undercover, "Vla", true, 0);
        var survivingCivilian = new UndercoverPlayer(Guid.NewGuid(), UndercoverRole.Civilian, "Kaas", false, 0);
        var mrWhite = new UndercoverPlayer(Guid.NewGuid(), UndercoverRole.MrWhite, null, true, 0);
        var state = UndercoverState.Initial with
        {
            Phase = UndercoverPhase.MrWhiteGuess,
            Players = [eliminatedCivilian, eliminatedUndercover, survivingCivilian, mrWhite],
            Deck = [new FaceDownCard(0, UndercoverRole.Civilian, "Kaas", null)],
            MrWhiteGuesserSeatId = mrWhite.SeatId,
        };

        var (next, _) = UndercoverEngine.SubmitMrWhiteGuess(state, mrWhite.SeatId, "wrong-guess", new Random(1));

        // Only 1 civilian and 0 threats survive -> civilians win immediately, game ends.
        Assert.Equal(UndercoverPhase.Results, next.Phase);
        Assert.Equal(UndercoverWinner.Civilians, next.Winner);
    }

    [Fact]
    public void Full_playthrough_from_StartGame_to_Results_completes_deterministically()
    {
        var seats = Seats(3);
        var rng = new Random(42);
        var state = UndercoverEngine.StartGame(UndercoverState.Initial, seats, Category(("Kaas", "Vla")), rng);
        state = PickAllCards(state, seats, rng);
        Assert.Equal(UndercoverPhase.Discussion, state.Phase);

        var guard = 0;
        while (state.Phase != UndercoverPhase.Results && guard++ < 20)
        {
            switch (state.Phase)
            {
                case UndercoverPhase.Discussion:
                    state = UndercoverEngine.StartVoting(state);
                    break;
                case UndercoverPhase.Voting:
                    var active = state.Players.Where(p => !p.IsEliminated).ToList();
                    // Everyone votes for the first active seat that isn't themselves.
                    foreach (var voter in active)
                    {
                        var target = active.First(p => p.SeatId != voter.SeatId);
                        (state, _) = UndercoverEngine.CastVote(state, voter.SeatId, target.SeatId);
                        if (state.Phase != UndercoverPhase.Voting)
                        {
                            break;
                        }
                    }

                    if (state.TiedSeatIds.Count > 0)
                    {
                        state = UndercoverEngine.ResolveTie(state, state.TiedSeatIds[0]);
                    }

                    break;
                case UndercoverPhase.EliminationReveal:
                    state = UndercoverEngine.AcknowledgeElimination(state, rng);
                    break;
                case UndercoverPhase.MrWhiteGuess:
                    (state, _) = UndercoverEngine.SubmitMrWhiteGuess(state, state.MrWhiteGuesserSeatId!.Value, "definitely-wrong", rng);
                    break;
            }
        }

        Assert.Equal(UndercoverPhase.Results, state.Phase);
        Assert.NotNull(state.Winner);
    }
}
