# games.donit

Three party games at `games.donit.be`, self-hosted on a Raspberry Pi.

| Game | Devices | Runs where |
|---|---|---|
| Undercover — pass-and-play | one shared phone | **browser (WASM)**, separate repo |
| Undercover — online | one phone per player | server |
| Just One | one phone per player | server |

**This repo is the Blazor Server app**: the portal plus the two online games.
Pass-and-play lives in `C:\git\undercover-game` (Blazor WebAssembly, net8.0) and is
**never edited from here** — its published output is bundled into `wwwroot/undercover/`
so its taps stay instant. UI language is **Dutch only**; no localisation framework.

## Layout

- `src/DonitGames.Core` — game rules, rooms, word data. **No `Microsoft.AspNetCore.*`
  reference, ever.** That restriction is the whole point: it makes it structurally
  impossible for a rule to reach for `IJSRuntime` or `NavigationManager`, and it is why
  the engines are testable without a web host.
- `src/DonitGames.Web` — Blazor Server host, components, `Data/` word lists.
- `tests/DonitGames.Core.Tests` — xUnit. Everything is deterministic given a seeded
  `Random`, so there are no mocks.

## Non-negotiables

These are load-bearing. Each one is a bug that is invisible in normal use and expensive
to diagnose later.

1. **No per-player game state in a component field.** Which card someone picked, what
   clue they typed, whose turn it is — all of it lives in the `GameRoom`. A Blazor Server
   circuit dies when a phone locks; when it is rebuilt it must rebuild from room state.
   Component fields hold only the immutable snapshot and transient input text.

2. **`GameRoom.Mutate` notifies *outside* the lock.** A subscriber whose handler hops
   threads and then calls `Read` will deadlock otherwise. There is a test for this; do
   not delete it when simplifying.

3. **`Room.Read` returns immutable snapshots.** Never hand a live `List<Seat>` or a game
   object to a component — components render asynchronously and will tear.

4. **Catch `ObjectDisposedException` in room event handlers and self-unsubscribe.**
   `Dispose` is *not* called when a phone disconnects, only when the circuit is finally
   evicted up to five minutes later, so handlers do fire into dead circuits.

5. **Hidden information is enforced by projection, never by `@if` in markup.** Whatever
   lands in a render tree goes down that circuit's wire. Use `ViewFor(seatId)` /
   `PrivateView(playerId)`; if a viewer must not see the word, the type they receive must
   not carry it.

6. **Word lists live in `Data/`, never `wwwroot/`.** `wwwroot` is HTTP-served and the
   Just One deck must not be fetchable by a player mid-game.

7. **`InvariantGlobalization` stays `false`** (set in `Directory.Build.props`).
   `string.Normalize(NormalizationForm.FormD)` is the core of the duplicate normaliser and
   throws in invariant mode. Same reason: no Alpine base image without ICU.

8. **Never add `UseHttpsRedirection()` or `UseHsts()`.** The Cloudflare Tunnel speaks
   plain HTTP to this app; either one causes an infinite redirect loop.

9. **A circuit is per browser *tab*, a seat is per person.** Ref-count circuits per seat,
   or a second tab looks like a second player.

10. **Always `DateTimeOffset.UtcNow`**, never `DateTime.Now`.

## Conventions

- Design system is `wwwroot/css/design-system.css` — 1294 lines copied verbatim from the
  pass-and-play app. **Do not edit it**; put new classes in `wwwroot/css/rooms.css` and use
  its tokens (`--accent-primary`, `--tap`, `--radius`, …). Never hardcode a colour.
- Light mode only. Dark mode is an explicit non-goal.
- Mobile first: the phone is the target, `--tap: 48px` is the minimum touch target.
- Static SSR by default; add `@rendermode InteractiveServer` only where interactivity is
  actually needed, so reading the rules costs no circuit.
- Avoid `@bind:event="oninput"` — on Blazor Server that is a round-trip per keystroke.

## Commands

```powershell
dotnet build
dotnet test
dotnet run --project src/DonitGames.Web --urls "http://0.0.0.0:5000"   # phones on the LAN
cloudflared tunnel --url http://localhost:5000                          # real HTTPS for testing
```

The quick tunnel is worth the extra step: it exercises the real Cloudflare WebSocket path
and unlocks the secure-context APIs (clipboard, wake-lock) that plain-HTTP LAN testing
cannot reach.

## Related

- `docs/IMPLEMENTATION-PLAN.md` — phased build plan and where we are.
- `docs/DEPLOYMENT.md` — Docker / Pi / Cloudflare, deferred to a later pass.
