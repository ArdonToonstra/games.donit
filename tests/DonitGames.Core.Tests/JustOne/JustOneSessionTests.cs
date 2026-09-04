using DonitGames.Core.JustOne;
using DonitGames.Core.Rooms;

namespace DonitGames.Core.Tests.JustOne;

public class JustOneSessionTests
{
    private static Seat Seat(string name, bool isHost = false) => new(Guid.NewGuid(), name, isHost, DateTimeOffset.UtcNow);

    private static RoomSnapshot<JustOneState> Snapshot(JustOneState game, params Seat[] seats)
    {
        var presence = seats.ToDictionary(s => s.SeatId, s => new SeatPresence(1, DateTimeOffset.UtcNow));
        return new RoomSnapshot<JustOneState>("TEST", seats, presence, game, 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
    }

    [Fact]
    public void ViewFor_guesser_SecretWord_is_null_during_ClueWriting()
    {
        var host = Seat("Ardon", isHost: true);
        var guesser = Seat("Robin");
        var game = JustOneState.Initial with
        {
            Phase = JustOnePhase.ClueWriting,
            GuesserSeatId = guesser.SeatId,
            SecretWord = "Kaas",
        };

        var view = new JustOneSession().ViewFor(Snapshot(game, host, guesser), guesser.SeatId);

        Assert.Null(view.SecretWord);
        Assert.True(view.YouAreGuesser);
    }

    [Fact]
    public void ViewFor_clue_giver_sees_the_secret_word_during_ClueWriting()
    {
        var host = Seat("Ardon", isHost: true);
        var guesser = Seat("Robin");
        var game = JustOneState.Initial with
        {
            Phase = JustOnePhase.ClueWriting,
            GuesserSeatId = guesser.SeatId,
            SecretWord = "Kaas",
        };

        var view = new JustOneSession().ViewFor(Snapshot(game, host, guesser), host.SeatId);

        Assert.Equal("Kaas", view.SecretWord);
    }

    [Fact]
    public void ViewFor_clue_giver_does_not_see_other_clue_givers_text_during_ClueWriting()
    {
        var guesser = Seat("Robin");
        var alice = Seat("Alice", isHost: true);
        var bob = Seat("Bob");
        var game = JustOneState.Initial with
        {
            Phase = JustOnePhase.ClueWriting,
            GuesserSeatId = guesser.SeatId,
            SecretWord = "Kaas",
            Clues = [new Clue(bob.SeatId, "Melk")],
        };

        var view = new JustOneSession().ViewFor(Snapshot(game, guesser, alice, bob), alice.SeatId);

        Assert.Empty(view.Clues);
        // But the submitted-count signal is still visible.
        Assert.True(view.Players.First(p => p.SeatId == bob.SeatId).HasSubmittedClue);
    }

    [Fact]
    public void ViewFor_clue_giver_sees_own_clue_text_during_ClueWriting()
    {
        var guesser = Seat("Robin");
        var alice = Seat("Alice", isHost: true);
        var game = JustOneState.Initial with
        {
            Phase = JustOnePhase.ClueWriting,
            GuesserSeatId = guesser.SeatId,
            SecretWord = "Kaas",
            Clues = [new Clue(alice.SeatId, "Melk")],
        };

        var view = new JustOneSession().ViewFor(Snapshot(game, guesser, alice), alice.SeatId);

        Assert.Equal("Melk", view.YourClueText);
    }

    [Fact]
    public void ViewFor_guesser_sees_only_surviving_clues_during_Guessing_never_cancelled_ones()
    {
        var guesser = Seat("Robin");
        var alice = Seat("Alice", isHost: true);
        var bob = Seat("Bob");
        var carol = Seat("Carol");
        var game = JustOneState.Initial with
        {
            Phase = JustOnePhase.Guessing,
            GuesserSeatId = guesser.SeatId,
            SecretWord = "Kaas",
            Clues = [new Clue(alice.SeatId, "Melk"), new Clue(bob.SeatId, "Zuivel"), new Clue(carol.SeatId, "Boer")],
            AutoCancelledSeatIds = [bob.SeatId],
        };

        var view = new JustOneSession().ViewFor(Snapshot(game, guesser, alice, bob, carol), guesser.SeatId);

        Assert.Equal(2, view.Clues.Count);
        Assert.DoesNotContain(view.Clues, c => c.SeatId == bob.SeatId);
        Assert.Contains(view.Clues, c => c.SeatId == alice.SeatId);
        Assert.Contains(view.Clues, c => c.SeatId == carol.SeatId);
    }

    [Fact]
    public void ViewFor_non_guesser_sees_cancellation_status_but_still_the_text_during_DuplicateReview()
    {
        var guesser = Seat("Robin");
        var alice = Seat("Alice", isHost: true);
        var bob = Seat("Bob");
        var game = JustOneState.Initial with
        {
            Phase = JustOnePhase.DuplicateReview,
            GuesserSeatId = guesser.SeatId,
            Clues = [new Clue(alice.SeatId, "Melk"), new Clue(bob.SeatId, "Melk")],
            AutoCancelledSeatIds = [alice.SeatId, bob.SeatId],
        };

        var view = new JustOneSession().ViewFor(Snapshot(game, guesser, alice, bob), alice.SeatId);

        Assert.Equal(2, view.Clues.Count);
        Assert.All(view.Clues, c => Assert.True(c.IsCancelled));
        Assert.All(view.Clues, c => Assert.Equal("Melk", c.Text));
    }

    [Fact]
    public void ViewFor_reports_YouAreHost_correctly()
    {
        var host = Seat("Ardon", isHost: true);
        var other = Seat("Robin");
        var game = JustOneState.Initial;

        var hostView = new JustOneSession().ViewFor(Snapshot(game, host, other), host.SeatId);
        var otherView = new JustOneSession().ViewFor(Snapshot(game, host, other), other.SeatId);

        Assert.True(hostView.YouAreHost);
        Assert.False(otherView.YouAreHost);
    }

    [Fact]
    public void ViewFor_reports_IsJudge_correctly_with_the_host_guesser_fallback()
    {
        var host = Seat("Ardon", isHost: true);
        var other = Seat("Robin");
        var game = JustOneState.Initial with { GuesserSeatId = host.SeatId };

        var view = new JustOneSession().ViewFor(Snapshot(game, host, other), other.SeatId);

        Assert.True(view.YouAreJudge);
    }
}
