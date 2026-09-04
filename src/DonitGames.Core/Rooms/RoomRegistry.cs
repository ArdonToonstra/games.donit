using System.Collections.Concurrent;

namespace DonitGames.Core.Rooms;

public sealed class RoomRegistry<TState> : IRoomRegistry
{
    private readonly ConcurrentDictionary<string, GameRoom<TState>> _rooms = new();

    public GameRoom<TState> Create(TState initialGame)
    {
        while (true)
        {
            var code = RoomCodeGenerator.Generate(Random.Shared);
            var room = new GameRoom<TState>(code, initialGame, DateTimeOffset.UtcNow);
            if (_rooms.TryAdd(code, room))
            {
                return room;
            }
        }
    }

    public bool TryGet(string code, out GameRoom<TState> room) => _rooms.TryGetValue(code, out room!);

    public int RemoveIdleRooms(TimeSpan idleTimeout, DateTimeOffset nowUtc)
    {
        var removed = 0;
        foreach (var (code, room) in _rooms)
        {
            if (nowUtc - room.Read().LastActivityUtc >= idleTimeout && _rooms.TryRemove(code, out _))
            {
                removed++;
            }
        }

        return removed;
    }
}
