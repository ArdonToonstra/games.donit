using DonitGames.Core.JustOne;
using DonitGames.Core.Rooms;

namespace DonitGames.Core.Tests.JustOne;

/// <summary>
/// Table mode: the phones only carry the clue. No duplicate pass, no typed guess — the table
/// does both itself, and the app's job shrinks to dealing a word and recording a verdict.
/// </summary>
public class JustOneTableModeTests
{
    private static IReadOnlyList<string> Words => ["Kaas", "Fiets", "Zon", "Boek", "Tuin", "Trein", "Appel", "Water"];

    private static IReadOnlyList<Seat> Seats(int count) =>
        Enumerable.Range(0, count)
            .Select(i => new Seat(Guid.NewGuid(), $"Speler {i}", IsHost: i == 0, DateTimeOffset.UtcNow.AddSeconds(i)))
            .ToList();

    private static RoomSnapshot<JustOneState> Snapshot(JustOneState game, IReadOnlyList<Seat> seats)
    {
        var presence = seats.ToDictionary(s => s.SeatId, _ => new SeatPresence(1, DateTimeOffset.UtcNow));
        return new RoomSnapshot<JustOneState>("TEST", seats, presence, game, 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
    }

    private static (JustOneState State, IReadOnlyList<Seat> Seats) StartedTableGame(int players = 4)
    {
        var seats = Seats(players);
        var lobby = JustOneEngine.SetMode(JustOneState.Initial, JustOneMode.Table);
        return (JustOneEngine.StartGame(lobby, seats, Words, new Random(1)), seats);
    }

    private static JustOneState SubmitAllClues(JustOneState state, IReadOnlyList<Seat> seats, Func<Seat, string> clueFor)
    {
        foreach (var seat in seats.Where(s => s.SeatId != state.GuesserSeatId))
        {
            state = JustOneEngine.SubmitClue(state, seats, seat.SeatId, clueFor(seat));
        }

        return state;
    }

    [Fact]
    public void SetMode_only_takes_effect_in_the_lobby()
    {
        var lobby = JustOneEngine.SetMode(JustOneState.Initial, JustOneMode.Table);
        Assert.Equal(JustOneMode.Table, lobby.Mode);

        var midRound = JustOneEngine.SetMode(lobby with { Phase = JustOnePhase.ClueWriting }, JustOneMode.Phones);
        Assert.Equal(JustOneMode.Table, midRound.Mode);
    }

    [Fact]
    public void The_mode_survives_a_new_game_on_the_same_table()
    {
        var (state, seats) = StartedTableGame();

        var next = JustOneEngine.StartNewGame(state with { Phase = JustOnePhase.GameResults }, seats, Words, new Random(2));

        Assert.Equal(JustOneMode.Table, next.Mode);
    }

    [Fact]
    public void The_last_clue_sends_the_round_straight_to_ClueReveal_skipping_duplicate_review()
    {
        var (state, seats) = StartedTableGame();

        state = SubmitAllClues(state, seats, s => $"Uniek-{s.DisplayName}");

        Assert.Equal(JustOnePhase.ClueReveal, state.Phase);
    }

    [Fact]
    public void Identical_clues_are_left_alone_because_the_table_spots_them_itself()
    {
        var (state, seats) = StartedTableGame(3);

        state = SubmitAllClues(state, seats, _ => "Melk");

        Assert.Equal(JustOnePhase.ClueReveal, state.Phase);
        Assert.Empty(state.AutoCancelledSeatIds);
        Assert.Empty(state.ReviewGroups);
        Assert.Equal(2, state.Clues.Count);
    }

    [Fact]
    public void Closing_clue_writing_with_nobody_submitted_still_lands_on_NoClues()
    {
        var (state, _) = StartedTableGame();

        state = JustOneEngine.CloseClueWriting(state);

        Assert.Equal(JustOnePhase.RoundResult, state.Phase);
        Assert.Equal(RoundOutcome.NoClues, state.LastOutcome);
    }

    [Fact]
    public void The_guesser_hands_the_round_back_to_the_table_and_nobody_else_can()
    {
        var (state, seats) = StartedTableGame();
        state = SubmitAllClues(state, seats, s => $"Uniek-{s.DisplayName}");
        var someoneElse = seats.First(s => s.SeatId != state.GuesserSeatId);

        Assert.Equal(JustOnePhase.ClueReveal, JustOneEngine.FinishGuessing(state, someoneElse.SeatId).Phase);
        Assert.Equal(JustOnePhase.TableVerdict, JustOneEngine.FinishGuessing(state, state.GuesserSeatId!.Value).Phase);
    }

    [Fact]
    public void Any_clue_giver_may_record_the_verdict_but_the_guesser_may_not()
    {
        var (state, seats) = StartedTableGame();
        state = SubmitAllClues(state, seats, s => $"Uniek-{s.DisplayName}");
        state = JustOneEngine.FinishGuessing(state, state.GuesserSeatId!.Value);

        // The last clue-giver in the roster, i.e. deliberately not the host/judge.
        var anyClueGiver = seats.Last(s => s.SeatId != state.GuesserSeatId);
        var recorded = JustOneEngine.RecordTableVerdict(state, anyClueGiver.SeatId, correct: true);

        Assert.Equal(JustOnePhase.RoundResult, recorded.Phase);
        Assert.Equal(RoundOutcome.Correct, recorded.LastOutcome);
        Assert.Equal(1, recorded.CorrectCount);

        var byGuesser = JustOneEngine.RecordTableVerdict(state, state.GuesserSeatId!.Value, correct: true);
        Assert.Equal(JustOnePhase.TableVerdict, byGuesser.Phase);
    }

    [Fact]
    public void A_wrong_verdict_costs_two_cards_the_same_as_in_phones_mode()
    {
        var (state, seats) = StartedTableGame();
        state = SubmitAllClues(state, seats, s => $"Uniek-{s.DisplayName}");
        state = JustOneEngine.FinishGuessing(state, state.GuesserSeatId!.Value);
        var clueGiver = seats.First(s => s.SeatId != state.GuesserSeatId);

        state = JustOneEngine.RecordTableVerdict(state, clueGiver.SeatId, correct: false);

        Assert.Equal(RoundOutcome.Incorrect, state.LastOutcome);
        Assert.Equal(JustOneState.StartingPips - 2, state.PipsRemaining);
    }

    [Fact]
    public void The_guesser_can_pass_straight_from_the_reveal_screen()
    {
        var (state, seats) = StartedTableGame();
        state = SubmitAllClues(state, seats, s => $"Uniek-{s.DisplayName}");

        state = JustOneEngine.Pass(state, state.GuesserSeatId!.Value);

        Assert.Equal(RoundOutcome.Passed, state.LastOutcome);
        Assert.Equal(JustOneState.StartingPips - 1, state.PipsRemaining);
    }

    [Fact]
    public void Nobody_can_read_a_clue_off_their_own_screen_during_the_hold_up()
    {
        var (state, seats) = StartedTableGame();
        state = SubmitAllClues(state, seats, s => $"Hint-{s.DisplayName}");
        var session = new JustOneSession();
        var snapshot = Snapshot(state, seats);

        foreach (var seat in seats)
        {
            var view = session.ViewFor(snapshot, seat.SeatId);
            // Not "hidden in markup" — the clues simply aren't in the payload (CLAUDE.md #5).
            Assert.Empty(view.Clues);
        }

        // Your own clue is still yours, because that is what your phone has to display.
        var writer = seats.First(s => s.SeatId != state.GuesserSeatId);
        Assert.Equal($"Hint-{writer.DisplayName}", session.ViewFor(snapshot, writer.SeatId).YourClueText);
        Assert.Null(session.ViewFor(snapshot, state.GuesserSeatId!.Value).YourClueText);
    }

    [Fact]
    public void The_guesser_still_never_sees_the_word_until_the_round_is_over()
    {
        var (state, seats) = StartedTableGame();
        state = SubmitAllClues(state, seats, s => $"Hint-{s.DisplayName}");
        state = JustOneEngine.FinishGuessing(state, state.GuesserSeatId!.Value);
        var session = new JustOneSession();

        var duringVerdict = session.ViewFor(Snapshot(state, seats), state.GuesserSeatId!.Value);
        Assert.Null(duringVerdict.SecretWord);

        // The clue-givers do see it — they are the ones judging the answer against it.
        var clueGiver = seats.First(s => s.SeatId != state.GuesserSeatId);
        Assert.Equal(state.SecretWord, session.ViewFor(Snapshot(state, seats), clueGiver.SeatId).SecretWord);
    }

    [Fact]
    public void Everyone_sees_every_clue_once_the_round_is_over()
    {
        var (state, seats) = StartedTableGame();
        state = SubmitAllClues(state, seats, s => $"Hint-{s.DisplayName}");
        state = JustOneEngine.FinishGuessing(state, state.GuesserSeatId!.Value);
        var clueGiver = seats.First(s => s.SeatId != state.GuesserSeatId);
        state = JustOneEngine.RecordTableVerdict(state, clueGiver.SeatId, correct: true);

        var guesserView = new JustOneSession().ViewFor(Snapshot(state, seats), state.GuesserSeatId!.Value);

        Assert.Equal(3, guesserView.Clues.Count);
        Assert.Equal(state.SecretWord, guesserView.SecretWord);
    }

    [Fact]
    public void YouMayJudge_opens_up_to_every_clue_giver_in_table_mode_but_stays_the_judge_in_phones_mode()
    {
        var (tableState, seats) = StartedTableGame();
        var session = new JustOneSession();

        // A clue-giver who is neither host nor judge.
        var plainPlayer = seats.Last(s => s.SeatId != tableState.GuesserSeatId);
        var tableView = session.ViewFor(Snapshot(tableState, seats), plainPlayer.SeatId);
        Assert.False(tableView.YouAreJudge);
        Assert.True(tableView.YouMayJudge);

        var phonesView = session.ViewFor(Snapshot(tableState with { Mode = JustOneMode.Phones }, seats), plainPlayer.SeatId);
        Assert.False(phonesView.YouMayJudge);
    }

    [Fact]
    public void A_full_table_mode_playthrough_reaches_GameResults()
    {
        var (state, seats) = StartedTableGame();
        var rng = new Random(7);

        var guard = 0;
        while (state.Phase != JustOnePhase.GameResults && guard++ < 80)
        {
            switch (state.Phase)
            {
                case JustOnePhase.ClueWriting:
                    state = SubmitAllClues(state, seats, s => $"Clue-{s.DisplayName}-{state.RoundNumber}");
                    break;
                case JustOnePhase.ClueReveal:
                    state = JustOneEngine.FinishGuessing(state, state.GuesserSeatId!.Value);
                    break;
                case JustOnePhase.TableVerdict:
                    state = JustOneEngine.RecordTableVerdict(state, seats.First(s => s.SeatId != state.GuesserSeatId).SeatId, correct: false);
                    break;
                case JustOnePhase.RoundResult:
                    state = JustOneEngine.AcknowledgeRoundResult(state, seats, Words, rng);
                    break;
                default:
                    Assert.Fail($"Table mode should never reach {state.Phase}.");
                    break;
            }
        }

        Assert.Equal(JustOnePhase.GameResults, state.Phase);
        Assert.Equal(0, state.PipsRemaining);
    }
}
