using DonitGames.Core.Rooms;
using DonitGames.Core.Rooms.Echo;

namespace DonitGames.Core.Tests.Rooms;

public class RoomRegistryTests
{
    [Fact]
    public void Create_returns_a_room_that_TryGet_can_find_by_code()
    {
        var registry = new RoomRegistry<EchoState>();

        var room = registry.Create(EchoState.Initial);

        Assert.True(registry.TryGet(room.Code, out var found));
        Assert.Same(room, found);
    }

    [Fact]
    public void Create_produces_unique_codes_even_under_heavy_creation()
    {
        var registry = new RoomRegistry<EchoState>();
        var codes = new HashSet<string>();

        for (var i = 0; i < 2000; i++)
        {
            codes.Add(registry.Create(EchoState.Initial).Code);
        }

        Assert.Equal(2000, codes.Count);
    }

    [Fact]
    public void TryGet_returns_false_for_an_unknown_code()
    {
        var registry = new RoomRegistry<EchoState>();

        Assert.False(registry.TryGet("ZZZZ", out _));
    }

    [Fact]
    public void RemoveIdleRooms_removes_only_rooms_past_the_threshold()
    {
        var registry = new RoomRegistry<EchoState>();
        var stale = registry.Create(EchoState.Initial);
        var fresh = registry.Create(EchoState.Initial);
        var now = DateTimeOffset.UtcNow;

        var removed = registry.RemoveIdleRooms(TimeSpan.FromHours(1), now.AddHours(2));

        Assert.Equal(2, removed); // both created "now", both idle relative to +2h
        Assert.False(registry.TryGet(stale.Code, out _));
        Assert.False(registry.TryGet(fresh.Code, out _));
    }

    [Fact]
    public void RemoveIdleRooms_keeps_rooms_with_recent_activity()
    {
        var registry = new RoomRegistry<EchoState>();
        var room = registry.Create(EchoState.Initial);
        room.Mutate(s => s); // touches LastActivityUtc

        var removed = registry.RemoveIdleRooms(TimeSpan.FromHours(1), DateTimeOffset.UtcNow);

        Assert.Equal(0, removed);
        Assert.True(registry.TryGet(room.Code, out _));
    }
}
