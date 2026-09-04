using DonitGames.Core.Rooms;
using DonitGames.Core.Rooms.Echo;

namespace DonitGames.Core.Tests.Rooms;

public class GameRoomTests
{
    private static GameRoom<EchoState> NewRoom() => new("TEST", EchoState.Initial, DateTimeOffset.UtcNow);

    [Fact]
    public void Read_returns_the_latest_snapshot_after_Mutate()
    {
        var room = NewRoom();

        room.Mutate(s => s with { Game = s.Game with { TapCount = 1 } });

        Assert.Equal(1, room.Read().Game.TapCount);
    }

    [Fact]
    public void Mutate_bumps_version_and_last_activity()
    {
        var room = NewRoom();
        var before = room.Read();

        var after = room.Mutate(s => s);

        Assert.Equal(before.Version + 1, after.Version);
        Assert.True(after.LastActivityUtc >= before.LastActivityUtc);
    }

    [Fact]
    public void Subscribe_receives_the_post_mutate_snapshot()
    {
        var room = NewRoom();
        RoomSnapshot<EchoState>? received = null;
        using var subscription = room.Subscribe(s => received = s);

        var result = room.Mutate(s => s with { Game = s.Game with { TapCount = 7 } });

        Assert.Same(result, received);
    }

    [Fact]
    public void Disposing_the_subscription_stops_further_notifications()
    {
        var room = NewRoom();
        var callCount = 0;
        var subscription = room.Subscribe(_ => callCount++);

        room.Mutate(s => s);
        subscription.Dispose();
        room.Mutate(s => s);

        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task A_subscriber_calling_Read_synchronously_does_not_deadlock()
    {
        var room = NewRoom();
        using var subscription = room.Subscribe(_ => room.Read());

        var mutateTask = Task.Run(() => room.Mutate(s => s));
        var completed = await Task.WhenAny(mutateTask, Task.Delay(TimeSpan.FromSeconds(2))) == mutateTask;

        Assert.True(completed, "Mutate did not return within 2s — the subscriber's Read() call deadlocked.");
    }

    [Fact]
    public async Task A_subscriber_calling_Mutate_synchronously_does_not_deadlock()
    {
        // Guarded to one re-entrant call: an unconditional "call Mutate from inside my own
        // notification" would recurse forever by construction (every Mutate re-notifies the
        // same subscriber) regardless of locking. What this test actually checks is that the
        // one nested call isn't blocked by a lock the outer Mutate still holds.
        var room = NewRoom();
        var reentered = false;
        using var subscription = room.Subscribe(_ =>
        {
            if (!reentered)
            {
                reentered = true;
                room.Mutate(s => s);
            }
        });

        var mutateTask = Task.Run(() => room.Mutate(s => s));
        var completed = await Task.WhenAny(mutateTask, Task.Delay(TimeSpan.FromSeconds(2))) == mutateTask;

        Assert.True(completed, "Mutate did not return within 2s — the subscriber's re-entrant Mutate() call deadlocked.");
    }

    [Fact]
    public void A_subscriber_throwing_ObjectDisposedException_does_not_stop_other_subscribers()
    {
        var room = NewRoom();
        using var throwing = room.Subscribe(_ => throw new ObjectDisposedException("stale-circuit"));
        var otherNotified = false;
        using var other = room.Subscribe(_ => otherNotified = true);

        room.Mutate(s => s);

        Assert.True(otherNotified);
    }

    [Fact]
    public void AddSeat_appends_to_the_seat_list()
    {
        var room = NewRoom();
        var seat = new Seat(Guid.NewGuid(), "Ardon", IsHost: true, DateTimeOffset.UtcNow);

        var snapshot = room.AddSeat(seat);

        Assert.Single(snapshot.Seats);
        Assert.Equal(seat, snapshot.Seats[0]);
    }

    [Fact]
    public void RemoveSeat_removes_the_seat_and_its_presence()
    {
        var room = NewRoom();
        var seat = new Seat(Guid.NewGuid(), "Ardon", IsHost: true, DateTimeOffset.UtcNow);
        room.AddSeat(seat);
        room.AdjustPresence(seat.SeatId, +1);

        var snapshot = room.RemoveSeat(seat.SeatId);

        Assert.Empty(snapshot.Seats);
        Assert.False(snapshot.Presence.ContainsKey(seat.SeatId));
    }

    [Fact]
    public void AdjustPresence_accumulates_across_multiple_circuits()
    {
        var room = NewRoom();
        var seatId = Guid.NewGuid();
        room.AddSeat(new Seat(seatId, "Ardon", IsHost: true, DateTimeOffset.UtcNow));

        room.AdjustPresence(seatId, +1);
        var snapshot = room.AdjustPresence(seatId, +1);

        Assert.Equal(2, snapshot.Presence[seatId].ActiveCircuitCount);
        Assert.True(snapshot.Presence[seatId].IsConnected);
    }

    [Fact]
    public void AdjustPresence_clamps_at_zero()
    {
        var room = NewRoom();
        var seatId = Guid.NewGuid();
        room.AddSeat(new Seat(seatId, "Ardon", IsHost: true, DateTimeOffset.UtcNow));

        var snapshot = room.AdjustPresence(seatId, -1);

        Assert.Equal(0, snapshot.Presence[seatId].ActiveCircuitCount);
        Assert.False(snapshot.Presence[seatId].IsConnected);
    }

    [Fact]
    public void AdjustPresence_is_a_noop_for_a_seat_that_no_longer_exists()
    {
        var room = NewRoom();
        var seatId = Guid.NewGuid();

        var snapshot = room.AdjustPresence(seatId, +1);

        Assert.False(snapshot.Presence.ContainsKey(seatId));
    }

    [Fact]
    public void Generic_Mutate_returns_the_mutators_result_alongside_the_snapshot()
    {
        var room = NewRoom();

        var (snapshot, result) = room.Mutate(s => (s with { Game = s.Game with { TapCount = 5 } }, "winner"));

        Assert.Equal(5, snapshot.Game.TapCount);
        Assert.Equal("winner", result);
    }

    [Fact]
    public void Generic_Mutate_still_notifies_subscribers_with_only_the_snapshot()
    {
        var room = NewRoom();
        RoomSnapshot<EchoState>? received = null;
        using var subscription = room.Subscribe(s => received = s);

        var (snapshot, _) = room.Mutate(s => (s with { Game = s.Game with { TapCount = 9 } }, 42));

        Assert.Same(snapshot, received);
    }

    [Fact]
    public async Task Generic_Mutate_called_re_entrantly_from_a_subscriber_does_not_deadlock()
    {
        var room = NewRoom();
        var reentered = false;
        using var subscription = room.Subscribe(_ =>
        {
            if (!reentered)
            {
                reentered = true;
                room.Mutate(s => (s, 0));
            }
        });

        var mutateTask = Task.Run(() => room.Mutate(s => (s, 0)));
        var completed = await Task.WhenAny(mutateTask, Task.Delay(TimeSpan.FromSeconds(2))) == mutateTask;

        Assert.True(completed, "Generic Mutate did not return within 2s — the re-entrant call deadlocked.");
    }
}
