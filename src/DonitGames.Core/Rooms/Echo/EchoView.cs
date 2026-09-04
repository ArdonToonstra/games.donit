namespace DonitGames.Core.Rooms.Echo;

public sealed record EchoSeatView(Guid SeatId, string DisplayName, bool IsHost, bool IsConnected);

public sealed record EchoView(
    int TapCount,
    bool YouAreLastTapper,
    bool YouAreHost,
    IReadOnlyList<EchoSeatView> Seats);
