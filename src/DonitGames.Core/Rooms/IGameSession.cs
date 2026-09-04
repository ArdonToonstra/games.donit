namespace DonitGames.Core.Rooms;

/// <summary>
/// Stateless by design — holds no reference to any particular room, so it can be a DI singleton
/// and be unit-tested without any host machinery. <c>ViewFor</c> is the one seam hidden
/// information is allowed to cross through: whatever a viewer must not see must not be present
/// on the returned <typeparamref name="TView"/>, never merely hidden by markup.
/// </summary>
public interface IGameSession<TState, TView>
{
    TView ViewFor(RoomSnapshot<TState> snapshot, Guid seatId);
}
