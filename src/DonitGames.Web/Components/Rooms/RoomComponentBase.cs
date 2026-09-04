using DonitGames.Core.Rooms;
using DonitGames.Web.Rooms;
using Microsoft.AspNetCore.Components;

namespace DonitGames.Web.Components.Rooms;

/// <summary>
/// Shared lifecycle every concrete game shell (EchoRoomShell today; Undercover/Just One later)
/// inherits: resolve the room, subscribe once, self-unsubscribe on a dead circuit, ref-count
/// this seat's presence. Rendering the game itself is each shell's own job — this class renders
/// nothing.
///
/// The page hosting a subclass MUST disable prerendering
/// (<c>new InteractiveServerRenderMode(prerender: false)</c>) — otherwise OnInitialized runs
/// once during static prerendering (no real circuit yet) and again when the interactive circuit
/// connects, double-counting this seat's presence.
/// </summary>
public abstract class RoomComponentBase<TState, TView> : ComponentBase, IDisposable
{
    [Parameter, EditorRequired]
    public string RoomCode { get; set; } = string.Empty;

    [Parameter, EditorRequired]
    public Guid SeatId { get; set; }

    [Inject]
    protected RoomRegistry<TState> Registry { get; set; } = default!;

    [Inject]
    protected IGameSession<TState, TView> Session { get; set; } = default!;

    [Inject]
    protected CircuitSeatRegistration CircuitRegistration { get; set; } = default!;

    protected GameRoom<TState>? Room { get; private set; }

    protected RoomSnapshot<TState>? Snapshot { get; private set; }

    protected TView? View { get; private set; }

    /// <summary>True when the seat cookie no longer points at anything real — room gone
    /// (expired/never existed) or seat gone (kicked). Concrete shells render RoomExpired.</summary>
    protected bool RoomMissing { get; private set; }

    private IDisposable? _subscription;

    protected override void OnInitialized()
    {
        if (!Registry.TryGet(RoomCode, out var room))
        {
            RoomMissing = true;
            return;
        }

        var snapshot = room.Read();
        if (!snapshot.Seats.Any(s => s.SeatId == SeatId))
        {
            RoomMissing = true;
            return;
        }

        Room = room;
        Snapshot = snapshot;
        View = Session.ViewFor(snapshot, SeatId);
        _subscription = room.Subscribe(OnRoomChanged);

        room.AdjustPresence(SeatId, +1);
        CircuitRegistration.ReleaseSeat = () => room.AdjustPresence(SeatId, -1);
    }

    private void OnRoomChanged(RoomSnapshot<TState> snapshot)
    {
        try
        {
            _ = InvokeAsync(() =>
            {
                if (!snapshot.Seats.Any(s => s.SeatId == SeatId))
                {
                    // Kicked, or this room got swept out from under us mid-session.
                    RoomMissing = true;
                    Snapshot = null;
                    View = default;
                    _subscription?.Dispose();
                    StateHasChanged();
                    return;
                }

                Snapshot = snapshot;
                View = Session.ViewFor(snapshot, SeatId);
                StateHasChanged();
            });
        }
        catch (ObjectDisposedException)
        {
            // This circuit's renderer is gone but the subscriber list doesn't know that yet —
            // CLAUDE.md non-negotiable #4. Self-unsubscribe so it stops firing into the void.
            _subscription?.Dispose();
        }
    }

    public virtual void Dispose()
    {
        // Presence release is deliberately NOT here — that's SeatPresenceCircuitHandler's job,
        // tied to the circuit's actual eviction rather than this component's disposal.
        _subscription?.Dispose();
    }
}
