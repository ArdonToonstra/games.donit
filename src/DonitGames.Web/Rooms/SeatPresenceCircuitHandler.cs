using Microsoft.AspNetCore.Components.Server.Circuits;

namespace DonitGames.Web.Rooms;

/// <summary>
/// Ref-counts circuits per seat (CLAUDE.md non-negotiable #9: a circuit is per browser tab, a
/// seat is per person). The increment happens in RoomComponentBase.OnInitialized instead of
/// OnCircuitOpenedAsync here, because this handler runs before any component has started and
/// doesn't yet know which seat the circuit belongs to.
/// </summary>
public sealed class SeatPresenceCircuitHandler(CircuitSeatRegistration registration) : CircuitHandler
{
    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        registration.ReleaseSeat?.Invoke();
        registration.ReleaseSeat = null;
        return Task.CompletedTask;
    }
}
