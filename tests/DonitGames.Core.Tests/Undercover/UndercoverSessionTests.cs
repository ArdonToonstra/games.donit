using DonitGames.Core.Rooms;
using DonitGames.Core.Undercover;

namespace DonitGames.Core.Tests.Undercover;

public class UndercoverSessionTests
{
    private static RoomSnapshot<UndercoverState> Snapshot(UndercoverState game, params Seat[] seats)
    {
        var presence = seats.ToDictionary(s => s.SeatId, s => new SeatPresence(1, DateTimeOffset.UtcNow));
        return new RoomSnapshot<UndercoverState>("TEST", seats, presence, game, 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
    }

    [Fact]
    public void ViewFor_hides_an_active_players_role_and_word_from_other_viewers()
    {
        var alice = new Seat(Guid.NewGuid(), "Alice", true, DateTimeOffset.UtcNow);
        var bob = new Seat(Guid.NewGuid(), "Bob", false, DateTimeOffset.UtcNow);
        var game = UndercoverState.Initial with
        {
            Phase = UndercoverPhase.Discussion,
            Players =
            [
                new UndercoverPlayer(alice.SeatId, UndercoverRole.Civilian, "Kaas", false, 0),
                new UndercoverPlayer(bob.SeatId, UndercoverRole.Undercover, "Vla", false, 0),
            ],
        };

        var view = new UndercoverSession().ViewFor(Snapshot(game, alice, bob), bob.SeatId);

        var aliceView = view.Players.First(p => p.SeatId == alice.SeatId);
        Assert.Null(aliceView.RevealedRole);
        Assert.Null(aliceView.RevealedWord);
    }

    [Fact]
    public void ViewFor_always_reveals_the_viewers_own_role_and_word_to_themselves()
    {
        var alice = new Seat(Guid.NewGuid(), "Alice", true, DateTimeOffset.UtcNow);
        var game = UndercoverState.Initial with
        {
            Phase = UndercoverPhase.Discussion,
            Players = [new UndercoverPlayer(alice.SeatId, UndercoverRole.Civilian, "Kaas", false, 0)],
        };

        var view = new UndercoverSession().ViewFor(Snapshot(game, alice), alice.SeatId);

        Assert.Equal(UndercoverRole.Civilian, view.YourRole);
        Assert.Equal("Kaas", view.YourSecretWord);
        var ownEntry = view.Players.Single();
        Assert.Equal(UndercoverRole.Civilian, ownEntry.RevealedRole);
        Assert.Equal("Kaas", ownEntry.RevealedWord);
    }

    [Fact]
    public void ViewFor_reveals_role_and_word_once_a_player_is_eliminated()
    {
        var alice = new Seat(Guid.NewGuid(), "Alice", true, DateTimeOffset.UtcNow);
        var bob = new Seat(Guid.NewGuid(), "Bob", false, DateTimeOffset.UtcNow);
        var game = UndercoverState.Initial with
        {
            Phase = UndercoverPhase.EliminationReveal,
            Players =
            [
                new UndercoverPlayer(alice.SeatId, UndercoverRole.MrWhite, null, true, 0),
                new UndercoverPlayer(bob.SeatId, UndercoverRole.Civilian, "Kaas", false, 0),
            ],
            JustEliminatedSeatId = alice.SeatId,
        };

        var view = new UndercoverSession().ViewFor(Snapshot(game, alice, bob), bob.SeatId);

        var aliceView = view.Players.First(p => p.SeatId == alice.SeatId);
        Assert.Equal(UndercoverRole.MrWhite, aliceView.RevealedRole);
    }

    [Fact]
    public void ViewFor_reveals_every_role_once_the_game_reaches_Results_even_for_survivors()
    {
        var alice = new Seat(Guid.NewGuid(), "Alice", true, DateTimeOffset.UtcNow);
        var bob = new Seat(Guid.NewGuid(), "Bob", false, DateTimeOffset.UtcNow);
        var game = UndercoverState.Initial with
        {
            Phase = UndercoverPhase.Results,
            Winner = UndercoverWinner.Civilians,
            Players =
            [
                new UndercoverPlayer(alice.SeatId, UndercoverRole.Civilian, "Kaas", false, 1),
                new UndercoverPlayer(bob.SeatId, UndercoverRole.Undercover, "Vla", true, 0),
            ],
        };

        var view = new UndercoverSession().ViewFor(Snapshot(game, alice, bob), bob.SeatId);

        var aliceView = view.Players.First(p => p.SeatId == alice.SeatId);
        Assert.Equal(UndercoverRole.Civilian, aliceView.RevealedRole);
        Assert.Equal("Kaas", aliceView.RevealedWord);
    }

    [Fact]
    public void ViewFor_never_puts_role_or_word_on_a_deck_card_for_any_viewer()
    {
        var alice = new Seat(Guid.NewGuid(), "Alice", true, DateTimeOffset.UtcNow);
        var game = UndercoverState.Initial with
        {
            Phase = UndercoverPhase.CardPicking,
            Players = [new UndercoverPlayer(alice.SeatId, null, null, false, 0)],
            Deck = [new FaceDownCard(0, UndercoverRole.Civilian, "Kaas", alice.SeatId)],
        };

        var view = new UndercoverSession().ViewFor(Snapshot(game, alice), alice.SeatId);

        var card = view.Deck.Single();
        Assert.Equal(0, card.Index);
        Assert.Equal(alice.SeatId, card.TakenBySeatId);
        // UndercoverCardView has no Role/Word property at all — the type itself is the guarantee.
    }

    [Fact]
    public void ViewFor_hides_votes_while_voting_is_still_open_and_untied()
    {
        var alice = new Seat(Guid.NewGuid(), "Alice", true, DateTimeOffset.UtcNow);
        var bob = new Seat(Guid.NewGuid(), "Bob", false, DateTimeOffset.UtcNow);
        var game = UndercoverState.Initial with
        {
            Phase = UndercoverPhase.Voting,
            Players =
            [
                new UndercoverPlayer(alice.SeatId, UndercoverRole.Civilian, "Kaas", false, 0),
                new UndercoverPlayer(bob.SeatId, UndercoverRole.Undercover, "Vla", false, 0),
            ],
            Votes = new Dictionary<Guid, Guid> { [alice.SeatId] = bob.SeatId },
        };

        var view = new UndercoverSession().ViewFor(Snapshot(game, alice, bob), bob.SeatId);

        Assert.Null(view.VoteBreakdown);
    }

    [Fact]
    public void ViewFor_exposes_the_breakdown_once_a_tie_needs_resolving()
    {
        var alice = new Seat(Guid.NewGuid(), "Alice", true, DateTimeOffset.UtcNow);
        var bob = new Seat(Guid.NewGuid(), "Bob", false, DateTimeOffset.UtcNow);
        var game = UndercoverState.Initial with
        {
            Phase = UndercoverPhase.Voting,
            Players =
            [
                new UndercoverPlayer(alice.SeatId, UndercoverRole.Civilian, "Kaas", false, 0),
                new UndercoverPlayer(bob.SeatId, UndercoverRole.Undercover, "Vla", false, 0),
            ],
            Votes = new Dictionary<Guid, Guid> { [alice.SeatId] = bob.SeatId, [bob.SeatId] = alice.SeatId },
            TiedSeatIds = [alice.SeatId, bob.SeatId],
        };

        var view = new UndercoverSession().ViewFor(Snapshot(game, alice, bob), alice.SeatId);

        Assert.NotNull(view.VoteBreakdown);
        Assert.Equal(2, view.VoteBreakdown!.Count);
    }
}
