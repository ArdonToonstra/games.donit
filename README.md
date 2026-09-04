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

## Deployment

**Live at `https://games.donit.be`**, self-hosted on a Raspberry Pi behind a Cloudflare Zero
Trust Tunnel. On every push to `main`, `.github/workflows/build-and-publish.yml`:

1. checks out this repo and the public `ArdonToonstra/undercover-game` repo,
2. publishes the pass-and-play WASM app and stages it into `wwwroot/undercover/` (the same
   by-hand steps as local Phase 5 testing, mechanized),
3. runs `dotnet test`,
4. builds and pushes a multi-arch-ready `linux/arm64` image to
   `ghcr.io/ardontoonstra/donit-games` (tagged `latest` and `sha-<short>`).

Deploys to the Pi stay **manual**, by design — a container restart drops every in-flight
circuit, so nothing should redeploy the app mid-round without a human choosing that moment. From
`donit-pi-server`:

```bash
docker --context rpi compose pull donit-games
docker --context rpi compose up -d --no-deps donit-games
```

The container is reachable **only** through the Cloudflare Tunnel — it publishes no host port
at all, sitting on an isolated Docker network (`games_net`) that only `cloudflared` shares. This
app clears `KnownProxies`/`KnownIPNetworks` to trust Cloudflare's `X-Forwarded-*` headers, so
nothing else needs to be able to reach it directly. See
[`docs/DEPLOYMENT.md`](docs/DEPLOYMENT.md) for the full research, the Cloudflare Access/WebSocket
landmines, and everything discovered while actually standing this up.

## Docs

- [`CLAUDE.md`](CLAUDE.md) — conventions and the non-negotiables. Read this first.
- [`docs/IMPLEMENTATION-PLAN.md`](docs/IMPLEMENTATION-PLAN.md) — phased plan and progress.
- [`docs/DEPLOYMENT.md`](docs/DEPLOYMENT.md) — Docker / Pi / Cloudflare, now live (see above).
