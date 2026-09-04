using DonitGames.Core.JustOne;
using DonitGames.Core.Rooms;

namespace DonitGames.Core.Tests.JustOne;

/// <summary>
/// The degenerate cases a room actually hits on a Saturday night — a phone that died, a player
/// who left, a bank that ran dry. Each one used to throw out of <c>GameRoom.Mutate</c>, which on
/// Blazor Server means the tapping player's circuit dies and the room is stuck on a screen whose
/// only button is now fatal. None of them may throw.
/// </summary>
public class JustOneStabilityTests
{
    private static IReadOnlyList<Seat> Seats(int count) =>
        Enumerable.Range(0, count)
            .Select(i => new Seat(Guid.NewGuid(), $"Speler {i}", IsHost: i == 0, DateTimeOffset.UtcNow.AddSeconds(i)))
            .ToList();

    private static RoomSnapshot<JustOneState> Snapshot(JustOneState game, IReadOnlyList<Seat> seats, params Guid[] connected)
    {
        var presence = connected.ToDictionary(id => id, _ => new SeatPresence(1, DateTimeOffset.UtcNow));
        return new RoomSnapshot<JustOneState>("TEST", seats, presence, game, 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
    }

    [Fact]
    public void A_bank_with_no_unused_words_left_recycles_instead_of_throwing()
    {
        var seats = Seats(3);
        IReadOnlyList<string> words = ["Kaas", "Fiets"];
        var state = JustOneState.Initial with
        {
            Phase = JustOnePhase.RoundResult,
            PipsRemaining = 5,
            GuesserSeatId = seats[0].SeatId,
            UsedWords = words,
        };

        state = JustOneEngine.AcknowledgeRoundResult(state, seats, words, new Random(1));

        Assert.Equal(JustOnePhase.ClueWriting, state.Phase);
        Assert.Contains(state.SecretWord, words);
        // The no-repeat window restarts at the word just dealt rather than staying full.
        Assert.Equal([state.SecretWord!], state.UsedWords);
    }

    [Fact]
    public void The_round_after_the_table_drops_below_the_minimum_ends_the_game_rather_than_throwing()
    {
        var seats = Seats(2);
        var state = JustOneState.Initial with
        {
            Phase = JustOnePhase.RoundResult,
            PipsRemaining = 9,
            GuesserSeatId = seats[0].SeatId,
        };

        state = JustOneEngine.AcknowledgeRoundResult(state, seats, ["Kaas", "Fiets"], new Random(1));

        Assert.Equal(JustOnePhase.GameResults, state.Phase);
    }

    [Fact]
    public void A_player_joining_mid_game_past_the_maximum_does_not_end_the_sitting()
    {
        var seats = Seats(JustOneEngine.MaxPlayers + 1);
        var state = JustOneState.Initial with
        {
            Phase = JustOnePhase.RoundResult,
            PipsRemaining = 9,
            GuesserSeatId = seats[0].SeatId,
        };

        state = JustOneEngine.AcknowledgeRoundResult(state, seats, ["Kaas", "Fiets"], new Random(1));

        Assert.Equal(JustOnePhase.ClueWriting, state.Phase);
    }

    [Fact]
    public void CanStart_gates_the_range_that_StartGame_still_throws_on()
    {
        Assert.False(JustOneEngine.CanStart(Seats(2)));
        Assert.True(JustOneEngine.CanStart(Seats(3)));
        Assert.True(JustOneEngine.CanStart(Seats(8)));
        Assert.False(JustOneEngine.CanStart(Seats(9)));
    }

    [Fact]
    public void The_judge_skips_a_host_whose_phone_is_gone()
    {
        var seats = Seats(4);
        var game = JustOneState.Initial with { GuesserSeatId = seats[3].SeatId };
        // Host (seats[0]) is offline, and so is seats[1]. seats[2] is the first one actually there.
        var snapshot = Snapshot(game, seats, seats[2].SeatId, seats[3].SeatId);

        Assert.Equal(seats[2].SeatId, JustOneEngine.Judge(snapshot));
    }

    [Fact]
    public void The_judge_is_still_the_host_when_the_host_is_connected()
    {
        var seats = Seats(4);
        var game = JustOneState.Initial with { GuesserSeatId = seats[3].SeatId };
        var snapshot = Snapshot(game, seats, seats[0].SeatId, seats[2].SeatId);

        Assert.Equal(seats[0].SeatId, JustOneEngine.Judge(snapshot));
    }

    [Fact]
    public void The_judge_falls_back_to_the_host_when_nobody_is_recorded_as_connected()
    {
        var seats = Seats(4);
        var game = JustOneState.Initial with { GuesserSeatId = seats[3].SeatId };

        Assert.Equal(seats[0].SeatId, JustOneEngine.Judge(Snapshot(game, seats)));
    }

    [Fact]
    public void EndGame_is_available_from_every_in_play_phase()
    {
        var inPlay = Enum.GetValues<JustOnePhase>().Except([JustOnePhase.Lobby, JustOnePhase.GameResults]);
        foreach (var phase in inPlay)
        {
            var ended = JustOneEngine.EndGame(JustOneState.Initial with { Phase = phase });
            Assert.Equal(JustOnePhase.GameResults, ended.Phase);
        }
    }

    [Fact]
    public void EndGame_is_a_no_op_in_the_lobby()
    {
        Assert.Equal(JustOnePhase.Lobby, JustOneEngine.EndGame(JustOneState.Initial).Phase);
    }

    [Theory]
    [InlineData("  melk  ", "melk")]
    [InlineData("witte\nkaas", "witte kaas")]
    [InlineData("veel    spaties", "veel spaties")]
    public void A_clue_is_collapsed_to_one_line_however_it_was_typed_or_pasted(string typed, string expected)
    {
        var seats = Seats(3);
        var state = JustOneEngine.StartGame(JustOneState.Initial, seats, ["Kaas", "Fiets", "Zon"], new Random(1));
        var writer = seats.First(s => s.SeatId != state.GuesserSeatId);

        state = JustOneEngine.SubmitClue(state, seats, writer.SeatId, typed);

        Assert.Equal(expected, state.Clues.Single(c => c.SeatId == writer.SeatId).Text);
    }

    [Fact]
    public void An_over_long_clue_is_clipped_rather_than_stretched_across_everyone_elses_screen()
    {
        var seats = Seats(3);
        var state = JustOneEngine.StartGame(JustOneState.Initial, seats, ["Kaas", "Fiets", "Zon"], new Random(1));
        var writer = seats.First(s => s.SeatId != state.GuesserSeatId);

        state = JustOneEngine.SubmitClue(state, seats, writer.SeatId, new string('a', 400));

        Assert.Equal(JustOneEngine.MaxClueLength, state.Clues.Single(c => c.SeatId == writer.SeatId).Text.Length);
    }

    [Fact]
    public void An_over_long_guess_is_clipped_too()
    {
        var seats = Seats(3);
        var started = JustOneEngine.StartGame(JustOneState.Initial, seats, ["Kaas", "Fiets", "Zon"], new Random(1));
        var state = started with { Phase = JustOnePhase.Guessing };

        state = JustOneEngine.SubmitGuess(state, state.GuesserSeatId!.Value, new string('b', 400));

        Assert.Equal(JustOneEngine.MaxGuessLength, state.GuesserAttempt!.Length);
    }

    [Fact]
    public void Removing_the_host_promotes_the_longest_seated_survivor()
    {
        var room = new GameRoom<JustOneState>("TEST", JustOneState.Initial, DateTimeOffset.UtcNow);
        var seats = Seats(3);
        foreach (var seat in seats)
        {
            room.AddSeat(seat);
        }

        var after = room.RemoveSeat(seats[0].SeatId);

        // Without this, Judge() has no host to resolve from and DuplicateReview has no way out.
        var host = Assert.Single(after.Seats, s => s.IsHost);
        Assert.Equal(seats[1].SeatId, host.SeatId);
    }

    [Fact]
    public void Removing_a_non_host_leaves_the_host_alone()
    {
        var room = new GameRoom<JustOneState>("TEST", JustOneState.Initial, DateTimeOffset.UtcNow);
        var seats = Seats(3);
        foreach (var seat in seats)
        {
            room.AddSeat(seat);
        }

        var after = room.RemoveSeat(seats[2].SeatId);

        Assert.Equal(seats[0].SeatId, Assert.Single(after.Seats, s => s.IsHost).SeatId);
    }

    [Fact]
    public void Removing_the_last_seat_leaves_an_empty_room_rather_than_throwing()
    {
        var room = new GameRoom<JustOneState>("TEST", JustOneState.Initial, DateTimeOffset.UtcNow);
        var seat = Seats(1)[0];
        room.AddSeat(seat);

        Assert.Empty(room.RemoveSeat(seat.SeatId).Seats);
    }
}
