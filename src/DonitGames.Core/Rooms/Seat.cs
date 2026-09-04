namespace DonitGames.Core.Rooms;

public sealed record Seat(Guid SeatId, string DisplayName, bool IsHost, DateTimeOffset JoinedAtUtc);

/// <summary>How many circuits (browser tabs) are currently open for this seat — a seat is per
/// person, a circuit is per tab, so this must be ref-counted rather than boolean.</summary>
public sealed record SeatPresence(int ActiveCircuitCount, DateTimeOffset LastSeenUtc)
{
    public bool IsConnected => ActiveCircuitCount > 0;
}
