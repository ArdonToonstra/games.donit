namespace DonitGames.Core.Rooms;

/// <summary>Non-generic so <c>RoomJanitor</c> can sweep every game's registry without knowing
/// each one's state type.</summary>
public interface IRoomRegistry
{
    int RemoveIdleRooms(TimeSpan idleTimeout, DateTimeOffset nowUtc);
}
