# games.donit

Party games for `games.donit.be`. Three games, two apps.

| Game | Devices | Runs where |
|---|---|---|
| **Undercover** — pass-and-play | one shared phone | browser (Blazor WebAssembly) |
| **Undercover** — online | one phone per player | server (Blazor Server) |
| **Just One** | one phone per player | server (Blazor Server) |

This repo is the **Blazor Server** app: the portal and the two online games. UI is Dutch.

The pass-and-play game deliberately stays a **WebAssembly** app, in its own repo at
`ArdonToonstra/undercover-game`, and is bundled into this container at `/undercover/`. Keeping
it client-side means its taps stay instant — moving it to Blazor Server would have turned every
tap into a ~60-150 ms round-trip from a phone on mobile data, through Cloudflare, to a
Raspberry Pi.

## Quick start

```powershell
dotnet build
dotnet test
dotnet run --project src/DonitGames.Web --urls "http://0.0.0.0:5000"
```

Bind `0.0.0.0`, not localhost, so phones on the wifi can reach it, and allow the port for the
**Private** firewall profile:

```powershell
New-NetFirewallRule -DisplayName "DonitGames dev" -Direction Inbound `
  -Protocol TCP -LocalPort 5000 -Action Allow -Profile Private
```

### Testing with real phones

Plain HTTP over the LAN works, but costs you the secure-context APIs this app actually wants —
`navigator.clipboard.writeText` (copying the room code, the natural way to share it) and
`navigator.wakeLock` (stopping phones sleeping mid-game) are secure-context-only, and
`192.168.x.x` is not exempt. Don't reach for dev certs either: self-signed means an interstitial
on Android and a per-device configuration-profile dance on iOS.

Use a throwaway Cloudflare quick tunnel instead:

```powershell
winget install Cloudflare.cloudflared
cloudflared tunnel --url http://localhost:5000
```

That prints a `https://<random>.trycloudflare.com` URL — a real HTTPS origin that exercises the
same Cloudflare edge and WebSocket-upgrade path as production, with no account needed. It also
lets you test one phone on 4G against another on wifi.

## Architecture

- `src/DonitGames.Core` — game rules, rooms, word data. **No ASP.NET reference**, so the
  engines are testable without a web host and no rule can reach for `IJSRuntime`.
- `src/DonitGames.Web` — Blazor Server host, components, `Data/` word lists.
- `tests/DonitGames.Core.Tests` — xUnit, no mocks (everything is deterministic given a seeded
  `Random`).

**All room state is in-memory**, in a singleton registry, keyed by a 4-character room code.
There is no database: sessions last 20-40 minutes and everyone is in the same physical room, so
persistence would buy a Redis dependency for a case that resolves itself with "start a new
room". A restart therefore ends running games, which is expected and handled with a friendly
page rather than an error.

**Single replica, always.** Blazor Server circuits are node-affine; scaling out would need
sticky sessions and a shared circuit store.

A player's identity is an HttpOnly `dg_seat` cookie set by a static-SSR join form, so a phone
that locks its screen for eight minutes reclaims its seat on the first paint after the reload.
See `CLAUDE.md` for the reconnection contract and the rules that make it work.

## Docs

- [`CLAUDE.md`](CLAUDE.md) — conventions and the non-negotiables. Read this first.
- [`docs/IMPLEMENTATION-PLAN.md`](docs/IMPLEMENTATION-PLAN.md) — phased plan and progress.
- [`docs/DEPLOYMENT.md`](docs/DEPLOYMENT.md) — Docker / Pi / Cloudflare. Deferred, not yet done.
