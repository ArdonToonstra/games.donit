namespace DonitGames.Core.Rooms;

/// <summary>
/// The immutable value a <see cref="GameRoom{TState}"/> hands out from <c>Read()</c> and to
/// subscribers — never a live collection, so a component rendering asynchronously can't tear.
/// </summary>
public sealed record RoomSnapshot<TState>(
    string Code,
    IReadOnlyList<Seat> Seats,
    IReadOnlyDictionary<Guid, SeatPresence> Presence,
    TState Game,
    long Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastActivityUtc);
