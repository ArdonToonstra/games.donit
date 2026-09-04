namespace DonitGames.Core.Rooms.Echo;

public sealed class EchoSession : IGameSession<EchoState, EchoView>
{
    public EchoView ViewFor(RoomSnapshot<EchoState> snapshot, Guid seatId)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var viewer = snapshot.Seats.FirstOrDefault(s => s.SeatId == seatId);
        var seats = snapshot.Seats
            .Select(seat => new EchoSeatView(
                seat.SeatId,
                seat.DisplayName,
                seat.IsHost,
                snapshot.Presence.TryGetValue(seat.SeatId, out var presence) && presence.IsConnected))
            .ToList();

        return new EchoView(
            snapshot.Game.TapCount,
            snapshot.Game.LastTapperSeatId == seatId,
            viewer?.IsHost ?? false,
            seats);
    }
}
