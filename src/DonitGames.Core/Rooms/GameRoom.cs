using System.Collections.Immutable;

namespace DonitGames.Core.Rooms;

/// <summary>
/// Owns one room's state behind a lock, and notifies subscribers of every change — outside the
/// lock. Notifying inside the lock would deadlock any subscriber whose handler hops back onto
/// this room (directly, or via a UI thread dispatch) and calls <c>Read()</c> or <c>Mutate()</c>.
/// </summary>
public sealed class GameRoom<TState>
{
    private readonly object _gate = new();
    private RoomSnapshot<TState> _snapshot;
    private ImmutableList<Action<RoomSnapshot<TState>>> _subscribers = ImmutableList<Action<RoomSnapshot<TState>>>.Empty;

    public GameRoom(string code, TState initialGame, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(code);

        _snapshot = new RoomSnapshot<TState>(
            code,
            [],
            new Dictionary<Guid, SeatPresence>(),
            initialGame,
            Version: 0,
            CreatedAtUtc: nowUtc,
            LastActivityUtc: nowUtc);
    }

    public string Code => _snapshot.Code;

    public RoomSnapshot<TState> Read()
    {
        lock (_gate)
        {
            return _snapshot;
        }
    }

    public RoomSnapshot<TState> Mutate(Func<RoomSnapshot<TState>, RoomSnapshot<TState>> mutator)
    {
        ArgumentNullException.ThrowIfNull(mutator);
        return Mutate(s => (mutator(s), true)).Snapshot;
    }

    /// <summary>
    /// Same atomic mutate-and-notify as <see cref="Mutate(Func{RoomSnapshot{TState},RoomSnapshot{TState}})"/>,
    /// but the mutator can also hand back a result describing what happened to *this* caller
    /// specifically — e.g. did this seat's card pick win the race against another seat's
    /// simultaneous pick of the same card. That's not an error, just a second piece of
    /// information from the same atomic operation, so it travels back to the caller of
    /// <c>Mutate</c> only — subscribers still only ever see the resulting snapshot.
    /// </summary>
    public (RoomSnapshot<TState> Snapshot, TResult Result) Mutate<TResult>(
        Func<RoomSnapshot<TState>, (RoomSnapshot<TState> Next, TResult Result)> mutator)
    {
        ArgumentNullException.ThrowIfNull(mutator);

        RoomSnapshot<TState> updated;
        TResult result;
        ImmutableList<Action<RoomSnapshot<TState>>> subscribers;
        lock (_gate)
        {
            var (next, mutatorResult) = mutator(_snapshot);
            updated = next with { Version = _snapshot.Version + 1, LastActivityUtc = DateTimeOffset.UtcNow };
            result = mutatorResult;
            _snapshot = updated;
            subscribers = _subscribers;
        }

        foreach (var subscriber in subscribers)
        {
            try
            {
                subscriber(updated);
            }
            catch (ObjectDisposedException)
            {
                // A subscriber backed by an evicted circuit that hasn't self-unsubscribed yet
                // (CLAUDE.md non-negotiable #4) must not stop the rest of the room from being
                // notified. Anything other than ObjectDisposedException is a real bug and
                // propagates normally.
            }
        }

        return (updated, result);
    }

    public IDisposable Subscribe(Action<RoomSnapshot<TState>> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        lock (_gate)
        {
            _subscribers = _subscribers.Add(handler);
        }

        return new Subscription(this, handler);
    }

    private void Unsubscribe(Action<RoomSnapshot<TState>> handler)
    {
        lock (_gate)
        {
            _subscribers = _subscribers.Remove(handler);
        }
    }

    public RoomSnapshot<TState> AddSeat(Seat seat)
    {
        ArgumentNullException.ThrowIfNull(seat);

        return Mutate(s => s with { Seats = [.. s.Seats, seat] });
    }

    public RoomSnapshot<TState> RemoveSeat(Guid seatId) =>
        Mutate(s => s with
        {
            Seats = s.Seats.Where(seat => seat.SeatId != seatId).ToList(),
            Presence = s.Presence.Where(p => p.Key != seatId).ToDictionary(p => p.Key, p => p.Value),
        });

    /// <summary>Ref-counts circuits per seat: +1 when a tab's circuit initializes, -1 when it's
    /// finally evicted. Clamped at 0 so a stray extra release can't go negative.</summary>
    public RoomSnapshot<TState> AdjustPresence(Guid seatId, int delta) =>
        Mutate(s =>
        {
            // A no-op for a seat that's already gone (kicked, or the room was swept) — otherwise
            // a circuit's delayed release (fired at eviction, well after RemoveSeat ran) would
            // resurrect a presence entry for a seat nobody can ever see again.
            if (!s.Seats.Any(seat => seat.SeatId == seatId))
            {
                return s;
            }

            var current = s.Presence.GetValueOrDefault(seatId, new SeatPresence(0, DateTimeOffset.UtcNow));
            var updated = current with
            {
                ActiveCircuitCount = Math.Max(0, current.ActiveCircuitCount + delta),
                LastSeenUtc = DateTimeOffset.UtcNow,
            };

            var presence = new Dictionary<Guid, SeatPresence>(s.Presence) { [seatId] = updated };
            return s with { Presence = presence };
        });

    private sealed class Subscription(GameRoom<TState> room, Action<RoomSnapshot<TState>> handler) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                room.Unsubscribe(handler);
            }
        }
    }
}
