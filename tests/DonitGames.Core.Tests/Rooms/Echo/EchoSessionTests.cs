using DonitGames.Core.Rooms;
using DonitGames.Core.Rooms.Echo;

namespace DonitGames.Core.Tests.Rooms.Echo;

public class EchoSessionTests
{
    private static RoomSnapshot<EchoState> MakeSnapshot(EchoState game, params Seat[] seats)
    {
        var presence = seats.ToDictionary(s => s.SeatId, s => new SeatPresence(1, DateTimeOffset.UtcNow));
        return new RoomSnapshot<EchoState>("TEST", seats, presence, game, 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
    }

    [Fact]
    public void ViewFor_reports_the_shared_tap_count_and_seats()
    {
        var host = new Seat(Guid.NewGuid(), "Ardon", IsHost: true, DateTimeOffset.UtcNow);
        var guest = new Seat(Guid.NewGuid(), "Robin", IsHost: false, DateTimeOffset.UtcNow);
        var snapshot = MakeSnapshot(new EchoState(3, guest.SeatId), host, guest);

        var view = new EchoSession().ViewFor(snapshot, guest.SeatId);

        Assert.Equal(3, view.TapCount);
        Assert.Equal(2, view.Seats.Count);
        Assert.Contains(view.Seats, s => s.SeatId == host.SeatId && s.IsHost);
        Assert.Contains(view.Seats, s => s.SeatId == guest.SeatId && !s.IsHost);
    }

    [Fact]
    public void ViewFor_sets_YouAreHost_only_for_the_host_seat()
    {
        var host = new Seat(Guid.NewGuid(), "Ardon", IsHost: true, DateTimeOffset.UtcNow);
        var guest = new Seat(Guid.NewGuid(), "Robin", IsHost: false, DateTimeOffset.UtcNow);
        var snapshot = MakeSnapshot(EchoState.Initial, host, guest);
        var session = new EchoSession();

        Assert.True(session.ViewFor(snapshot, host.SeatId).YouAreHost);
        Assert.False(session.ViewFor(snapshot, guest.SeatId).YouAreHost);
    }

    [Fact]
    public void ViewFor_sets_YouAreLastTapper_only_for_the_seat_that_tapped()
    {
        var host = new Seat(Guid.NewGuid(), "Ardon", IsHost: true, DateTimeOffset.UtcNow);
        var guest = new Seat(Guid.NewGuid(), "Robin", IsHost: false, DateTimeOffset.UtcNow);
        var snapshot = MakeSnapshot(new EchoState(1, guest.SeatId), host, guest);
        var session = new EchoSession();

        Assert.True(session.ViewFor(snapshot, guest.SeatId).YouAreLastTapper);
        Assert.False(session.ViewFor(snapshot, host.SeatId).YouAreLastTapper);
    }

    [Fact]
    public void ViewFor_reflects_disconnected_presence()
    {
        var seatId = Guid.NewGuid();
        var seat = new Seat(seatId, "Ardon", IsHost: true, DateTimeOffset.UtcNow);
        var snapshot = new RoomSnapshot<EchoState>(
            "TEST",
            [seat],
            new Dictionary<Guid, SeatPresence> { [seatId] = new(0, DateTimeOffset.UtcNow) },
            EchoState.Initial,
            1,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        var view = new EchoSession().ViewFor(snapshot, seatId);

        Assert.False(view.Seats.Single().IsConnected);
    }
}
