namespace DonitGames.Core.Rooms.Echo;

/// <summary>No game rules at all — "who's here / tap the button / see the counter". Exists only
/// to validate the room infrastructure end to end before any real game depends on it.</summary>
public sealed record EchoState(int TapCount, Guid? LastTapperSeatId)
{
    public static EchoState Initial { get; } = new(0, null);
}
