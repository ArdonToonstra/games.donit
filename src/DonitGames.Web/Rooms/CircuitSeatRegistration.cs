namespace DonitGames.Web.Rooms;

/// <summary>
/// Scoped per circuit — bridges the component that knows which seat it's rendering for (a
/// parameter, set once in OnInitialized) to the circuit handler, which is the only thing still
/// alive when the circuit is finally evicted and has no component to ask.
/// </summary>
public sealed class CircuitSeatRegistration
{
    public Action? ReleaseSeat { get; set; }
}
