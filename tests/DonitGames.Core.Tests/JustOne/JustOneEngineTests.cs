using DonitGames.Core.JustOne;
using DonitGames.Core.Rooms;

namespace DonitGames.Core.Tests.JustOne;

public class JustOneEngineTests
{
    private static IReadOnlyList<string> Words => ["Kaas", "Fiets", "Zon", "Boek", "Tuin", "Trein", "Appel", "Water"];

    private static IReadOnlyList<Seat> Seats(int count) =>
        Enumerable.Range(0, count)
            .Select(i => new Seat(Guid.NewGuid(), $"Speler {i}", IsHost: i == 0, DateTimeOffset.UtcNow))
            .ToList();

    private static JustOneState SubmitAllClues(JustOneState state, IReadOnlyList<Seat> seats, Func<Seat, string> clueFor)
    {
        foreach (var seat in seats.Where(s => s.SeatId != state.GuesserSeatId))
        {
            state = JustOneEngine.SubmitClue(state, seats, seat.SeatId, clueFor(seat));
        }

        return state;
    }

    [Fact]
    public void StartGame_picks_the_first_seat_as_guesser_and_enters_ClueWriting()
    {
        var seats = Seats(4);
        var state = JustOneEngine.StartGame(JustOneState.Initial, seats, Words, new Random(1));

        Assert.Equal(JustOnePhase.ClueWriting, state.Phase);
        Assert.Equal(seats[0].SeatId, state.GuesserSeatId);
        Assert.NotNull(state.SecretWord);
    }

    [Fact]
    public void SubmitClue_auto_advances_to_DuplicateReview_once_every_clue_giver_has_submitted()
    {
        var seats = Seats(4);
        var state = JustOneEngine.StartGame(JustOneState.Initial, seats, Words, new Random(1));

        state = SubmitAllClues(state, seats, s => $"Clue-{s.SeatId}");

        Assert.Equal(JustOnePhase.DuplicateReview, state.Phase);
    }

    [Fact]
    public void CloseClueWriting_works_as_a_host_override_with_an_away_unsubmitted_clue_giver()
    {
        var seats = Seats(4);
        var state = JustOneEngine.StartGame(JustOneState.Initial, seats, Words, new Random(1));
        var clueGivers = seats.Where(s => s.SeatId != state.GuesserSeatId).ToList();
        // Only 2 of 3 clue-givers submit — the third is "away".
        state = JustOneEngine.SubmitClue(state, seats, clueGivers[0].SeatId, "Melk");
        state = JustOneEngine.SubmitClue(state, seats, clueGivers[1].SeatId, "Boter");
        Assert.Equal(JustOnePhase.ClueWriting, state.Phase);

        state = JustOneEngine.CloseClueWriting(state);

        Assert.Equal(JustOnePhase.DuplicateReview, state.Phase);
        Assert.Equal(2, state.Clues.Count);
    }

    [Fact]
    public void Zero_surviving_clues_routes_straight_to_RoundResult_NoClues_without_entering_Guessing()
    {
        var seats = Seats(3);
        var state = JustOneEngine.StartGame(JustOneState.Initial, seats, Words, new Random(1));
        // Both clue-givers write the exact same clue -> both auto-cancelled -> zero survive.
        state = SubmitAllClues(state, seats, _ => "Melk");
        Assert.Equal(JustOnePhase.DuplicateReview, state.Phase);
        Assert.Equal(2, state.AutoCancelledSeatIds.Count);

        state = JustOneEngine.Reveal(state);

        Assert.Equal(JustOnePhase.RoundResult, state.Phase);
        Assert.Equal(RoundOutcome.NoClues, state.LastOutcome);
        Assert.Equal(JustOneState.StartingPips - 1, state.PipsRemaining);
    }

    [Fact]
    public void Reveal_moves_to_Guessing_when_at_least_one_clue_survives()
    {
        var seats = Seats(3);
        var state = JustOneEngine.StartGame(JustOneState.Initial, seats, Words, new Random(1));
        state = SubmitAllClues(state, seats, s => $"Uniek-{s.SeatId}");

        state = JustOneEngine.Reveal(state);

        Assert.Equal(JustOnePhase.Guessing, state.Phase);
    }

    [Fact]
    public void An_exact_guess_auto_accepts_as_Correct_and_costs_one_pip()
    {
        var seats = Seats(3);
        var state = JustOneEngine.StartGame(JustOneState.Initial, seats, Words, new Random(1));
        state = SubmitAllClues(state, seats, s => $"Uniek-{s.SeatId}");
        state = JustOneEngine.Reveal(state);
        var word = state.SecretWord!;

        state = JustOneEngine.SubmitGuess(state, state.GuesserSeatId!.Value, word.ToUpperInvariant());

        Assert.Equal(JustOnePhase.RoundResult, state.Phase);
        Assert.Equal(RoundOutcome.Correct, state.LastOutcome);
        Assert.Equal(1, state.CorrectCount);
        Assert.Equal(JustOneState.StartingPips - 1, state.PipsRemaining);
    }

    [Fact]
    public void A_non_matching_guess_never_auto_resolves_to_Incorrect_it_escalates_to_JudgeReview()
    {
        var seats = Seats(3);
        var state = JustOneEngine.StartGame(JustOneState.Initial, seats, Words, new Random(1));
        state = SubmitAllClues(state, seats, s => $"Uniek-{s.SeatId}");
        state = JustOneEngine.Reveal(state);

        state = JustOneEngine.SubmitGuess(state, state.GuesserSeatId!.Value, "DefinitelyNotTheWord");

        Assert.Equal(JustOnePhase.JudgeReview, state.Phase);
        Assert.Null(state.LastOutcome);
    }

    [Fact]
    public void JudgeDecision_Incorrect_costs_two_pips()
    {
        var seats = Seats(3);
        var state = JustOneEngine.StartGame(JustOneState.Initial, seats, Words, new Random(1));
        state = SubmitAllClues(state, seats, s => $"Uniek-{s.SeatId}");
        state = JustOneEngine.Reveal(state);
        state = JustOneEngine.SubmitGuess(state, state.GuesserSeatId!.Value, "Nope");

        state = JustOneEngine.JudgeDecision(state, correct: false);

        Assert.Equal(RoundOutcome.Incorrect, state.LastOutcome);
        Assert.Equal(JustOneState.StartingPips - 2, state.PipsRemaining);
        Assert.Equal(0, state.CorrectCount);
    }

    [Fact]
    public void Pass_costs_one_pip_and_does_not_count_as_correct()
    {
        var seats = Seats(3);
        var state = JustOneEngine.StartGame(JustOneState.Initial, seats, Words, new Random(1));
        state = SubmitAllClues(state, seats, s => $"Uniek-{s.SeatId}");
        state = JustOneEngine.Reveal(state);

        state = JustOneEngine.Pass(state, state.GuesserSeatId!.Value);

        Assert.Equal(RoundOutcome.Passed, state.LastOutcome);
        Assert.Equal(JustOneState.StartingPips - 1, state.PipsRemaining);
        Assert.Equal(0, state.CorrectCount);
    }

    [Fact]
    public void Pip_cost_clamps_at_zero_rather_than_going_negative()
    {
        var state = JustOneState.Initial with { Phase = JustOnePhase.Guessing, PipsRemaining = 1, GuesserSeatId = Guid.NewGuid() };

        state = JustOneEngine.Pass(state, state.GuesserSeatId!.Value);

        Assert.Equal(0, state.PipsRemaining);
    }

    [Fact]
    public void Judge_is_the_host_unless_the_host_is_this_rounds_guesser()
    {
        var seats = Seats(3);
        var state = JustOneEngine.StartGame(JustOneState.Initial, seats, Words, new Random(1));
        var snapshot = new RoomSnapshot<JustOneState>("TEST", seats, new Dictionary<Guid, SeatPresence>(), state, 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        // Round-robin picks seats[0] (the host) as the first guesser, so the judge must fall back.
        Assert.Equal(seats[0].SeatId, state.GuesserSeatId);
        var judge = JustOneEngine.Judge(snapshot);
        Assert.NotEqual(seats[0].SeatId, judge);
        Assert.NotNull(judge);
    }

    [Fact]
    public void Judge_is_the_host_when_the_host_is_not_the_guesser()
    {
        var seats = Seats(3);
        var state = JustOneEngine.StartGame(JustOneState.Initial, seats, Words, new Random(1)) with { GuesserSeatId = seats[1].SeatId };
        var snapshot = new RoomSnapshot<JustOneState>("TEST", seats, new Dictionary<Guid, SeatPresence>(), state, 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        Assert.Equal(seats[0].SeatId, JustOneEngine.Judge(snapshot));
    }

    [Fact]
    public void AcknowledgeRoundResult_advances_to_a_new_round_with_a_different_guesser()
    {
        var seats = Seats(3);
        var rng = new Random(1);
        var state = JustOneEngine.StartGame(JustOneState.Initial, seats, Words, rng);
        var firstGuesser = state.GuesserSeatId;
        state = SubmitAllClues(state, seats, s => $"Uniek-{s.SeatId}");
        state = JustOneEngine.Reveal(state);
        state = JustOneEngine.Pass(state, state.GuesserSeatId!.Value);

        state = JustOneEngine.AcknowledgeRoundResult(state, seats, Words, rng);

        Assert.Equal(JustOnePhase.ClueWriting, state.Phase);
        Assert.NotEqual(firstGuesser, state.GuesserSeatId);
        Assert.Equal(2, state.RoundNumber);
    }

    [Fact]
    public void AcknowledgeRoundResult_ends_the_game_once_pips_reach_zero()
    {
        var seats = Seats(3);
        var rng = new Random(1);
        var state = JustOneState.Initial with { Phase = JustOnePhase.RoundResult, PipsRemaining = 0, GuesserSeatId = seats[0].SeatId };

        state = JustOneEngine.AcknowledgeRoundResult(state, seats, Words, rng);

        Assert.Equal(JustOnePhase.GameResults, state.Phase);
    }

    [Fact]
    public void Full_playthrough_reaches_GameResults_deterministically()
    {
        var seats = Seats(4);
        var rng = new Random(99);
        var state = JustOneEngine.StartGame(JustOneState.Initial, seats, Words, rng);

        var guard = 0;
        while (state.Phase != JustOnePhase.GameResults && guard++ < 60)
        {
            switch (state.Phase)
            {
                case JustOnePhase.ClueWriting:
                    state = SubmitAllClues(state, seats, s => $"Clue-{s.SeatId}-{state.RoundNumber}");
                    break;
                case JustOnePhase.DuplicateReview:
                    state = JustOneEngine.Reveal(state);
                    break;
                case JustOnePhase.Guessing:
                    state = JustOneEngine.SubmitGuess(state, state.GuesserSeatId!.Value, "definitely-wrong");
                    break;
                case JustOnePhase.JudgeReview:
                    state = JustOneEngine.JudgeDecision(state, correct: false);
                    break;
                case JustOnePhase.RoundResult:
                    state = JustOneEngine.AcknowledgeRoundResult(state, seats, Words, rng);
                    break;
            }
        }

        Assert.Equal(JustOnePhase.GameResults, state.Phase);
        Assert.Equal(0, state.PipsRemaining);
    }
}
