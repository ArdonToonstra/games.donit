# Deployment

> **Status: done — `games.donit.be` is live**, running the real CI-built image
> (`ghcr.io/ardontoonstra/donit-games:latest`), pulled anonymously by the Pi (the source repo
> is public, and GHCR inherited that visibility — no separate package-visibility toggle or PAT
> needed). All three games verified reachable through the tunnel: the portal, the WASM bundle
> (`/undercover/`, including a hard-loaded deep link), and `WordPairs.yaml`/`JustOneWords.yaml`
> all return real content through `https://games.donit.be`. Remaining follow-ups, not blockers:
> the Pi's kernel doesn't enforce `mem_limit`/`cpus` yet (see below), and the temporary SSH
> access below should be torn down once back on the home LAN.
>
> **Correction to earlier research:** `cloudflared` on this Pi runs as a **container**
> (`cloudflare/cloudflared:latest`, on the default `bridge` network), not a native systemd
> service as originally assumed here and in `donit-pi-server/README.md`. That assumption drove
> the original `127.0.0.1:8085` plan below — abandoned in favor of an isolated Docker network
> (see "Compose" section) once discovered, since a host-loopback-only bind is invisible to a
> container-mode `cloudflared` (confirmed the hard way: identical symptom broke the temporary
> SSH-over-tunnel access set up to do this work remotely — `localhost:22`/`127.0.0.1:22` both
> got `connection refused` from cloudflared's own container namespace).
>
> **Temporary remote access, added for this session:** a `ssh.donit.be` Public Hostname +
> Access application (email-gated, `ardontoonstra@hotmail.com`) plus a `Host rpi-tunnel` entry
> in `~/.ssh/config`, added because this work happened away from the home LAN. Safe to remove
> once back home — delete both the Cloudflare hostname/Access app and the `rpi-tunnel` SSH
> config block; `Host rpi` (direct LAN IP) is untouched and still works as before.

The Pi setup is in `C:\git\donit-pi-server\README.md`: Docker Compose, ingress via a
**Cloudflare Zero Trust Tunnel**, with hostname→origin maps living only in the Cloudflare
dashboard (a "dashboard-managed" tunnel — no local `config.yml` on the Pi to edit for a new
hostname). ARM64, Debian Trixie, LAN `192.168.68.74`, Docker data-root on an ext4 USB-2.0 SSD.

**No .NET app had run on this Pi before this pass** — there was no container precedent to
copy, but the Dockerfile below has now been build-tested and run-tested for real on the Pi's
actual arm64 Docker daemon (via a `docker context create rpi --docker "host=ssh://..."`),
including the full hardening flag set from the "Compose" section (`read_only`, `cap_drop: ALL`,
`security_opt: no-new-privileges`, etc.) — all confirmed compatible with the app at runtime.

**Kernel gap found during testing:** `mem_limit`/`cpus` in Compose are silently unenforced —
`docker compose up` warned `Your kernel does not support memory limit capabilities or the
cgroup is not mounted`. Raspberry Pi OS/Debian needs `cgroup_enable=memory cgroup_memory=1`
added to `/boot/cmdline.txt` (then a reboot) for these limits to actually apply. Not a blocker
— the container runs fine without them — but the safety net they're meant to provide isn't
there yet.

### Cloudflare Access is a landmine — check this first

I probed the live site: `donit.be`, `wallos.donit.be` **and** `beszel.donit.be` all 302 to
`ajrdonster.cloudflareaccess.com/cdn-cgi/access/login/...`. Every existing hostname sits behind
a Cloudflare Access login gate.

A party game your friends open from a phone must have **no Access application** on it, or an
explicit *Bypass — Everyone* policy. Before anything else, confirm in Zero Trust → Access →
Applications that no `*.donit.be` **wildcard** app exists — the differing `kid` per hostname
suggests per-host apps, which is the good case. If a wildcard is what gates them,
`games.donit.be` needs an explicit bypass ahead of it. Getting this wrong looks exactly like
"the site is broken".

### Three settings that silently break WebSockets

WebSockets work on the Free plan through `cloudflared` with no special config — but three
things kill them quietly, and the failure mode is a *working but sluggish* game, not an error:

1. **`HTTP/2 connection to your origin` must be OFF** on the hostname's HTTP settings. With it
   on, cloudflared speaks HTTP/2 to Kestrel, WebSocket-over-HTTP/2 (RFC 8441) is not enabled in
   Kestrel by default, the upgrade fails, and Blazor silently drops to long polling. The most
   likely cause of a mysteriously laggy game.
2. **Rocket Loader OFF** (Speed → Optimization) — it defers and reorders every `<script>`,
   breaking `blazor.web.js` boot ordering. **Bot Fight Mode OFF** (Security → Bots) — it
   challenges `POST /_blazor/negotiate` and the WS upgrade, and guests on 4G behind shared CGNAT
   IPs are prime false positives. To keep it, add a WAF Skip rule for
   `starts_with(http.request.uri.path, "/_blazor")`.
3. **`cloudflared` self-updates and restarts**, dropping every live WebSocket on *every*
   hostname. Check `ssh rpi 'systemctl cat cloudflared'` for `--no-autoupdate` and add it via
   `systemctl edit cloudflared` if absent. A real source of "the game froze for everyone at
   once".

The DNS record stays **proxied** — `cfargotunnel.com` only resolves through Cloudflare's edge,
so DNS-only is not merely suboptimal, it is non-functional. Leave Network → WebSockets on.

### Program.cs specifics

- **No** `UseHttpsRedirection()` and **no** `UseHsts()` — either gives a redirect loop.
- `UseForwardedHeaders` **first**, for `XForwardedProto | XForwardedFor | XForwardedHost`, with
  `KnownNetworks` and `KnownProxies` **cleared** and `ForwardLimit = 2` (edge + cloudflared).
  The defaults trust only loopback, but inside Docker the peer is the bridge gateway, so the
  headers would be silently dropped and the app would think the scheme is `http` — breaking
  generated URLs, `Secure` cookies and antiforgery. Map `CF-Connecting-IP` onto
  `Connection.RemoteIpAddress` for sane logs. Do **not** also set
  `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` — you would run the middleware twice.
- Circuit options: `DisconnectedCircuitRetentionPeriod` 5 min (longer than the 3 min default),
  `DisconnectedCircuitMaxRetained` ~24-50 (bound Pi memory; default 100),
  `JSInteropDefaultCallTimeout` 1 min.
- Hub options: `KeepAliveInterval` 15 s, `ClientTimeoutInterval` 40-60 s (≥ 2× keep-alive),
  `HandshakeTimeout` 30 s (mobile is slow). Cloudflare closes an idle proxied WebSocket at
  ~100 s, so the 15 s ping keeps it alive — **never raise either past ~90 s**; the long-polling
  fallback's 90 s `PollTimeout` is also safely under.
- `app.UseAntiforgery()` must be in the pipeline for the static join form, with
  `SecurePolicy = Always` (not `SameAsRequest`) and `SameSite=Lax`.
- `AllowedHosts` must include `127.0.0.1` or the container healthcheck gets a 400.
- **DataProtection keys must be a mounted volume.** Otherwise every container restart
  invalidates all seat cookies *and* the protected component parameters.
- Client side in `App.razor`: start Blazor manually with generous `reconnectionOptions` (~60
  retries with backoff, not the default ~8/30 s) plus the `visibilitychange` listener.

### Container

Multi-stage, and the load-bearing trick is `FROM --platform=$BUILDPLATFORM
mcr.microsoft.com/dotnet/sdk:10.0` with `dotnet publish -a $TARGETARCH`: the SDK
cross-publishes to linux-arm64 while Roslyn and crossgen run **natively** on the builder. One
Dockerfile that is fast on a native arm runner, on x64+QEMU, and on the Pi.

Runtime `mcr.microsoft.com/dotnet/aspnet:10.0` (Debian trixie-slim) — **not** Alpine (musl plus
hand-adding `icu-libs`/`tzdata` erases the size win and adds a variable to the first .NET
deployment here) and **not** `-noble-chiseled` (no shell means no `curl` healthcheck and no
`docker exec` to debug). Revisit once this is boring.

`USER $APP_UID` (non-root), `ASPNETCORE_URLS=http://+:8080`, `EXPOSE 8080`,
`TZ=Europe/Brussels`, `HEALTHCHECK` on `/healthz` via `curl` — the aspnet image ships neither
curl nor wget, and that `apt-get install` is the one instruction needing QEMU when
cross-building (~30 s).

`-p:PublishReadyToRun=true` is worth it: 30-40% faster cold start, ~2 s vs ~4 s to first byte
on a Pi, for ~12 MB. **No trimming** (`PublishTrimmed` is unsupported for Razor components —
DI and component discovery break at runtime, sometimes only on one page) and **no Native AOT**.

Do **not** hand-set `DOTNET_GCHeapHardLimit` — since .NET 5 the runtime reads the cgroup limit
and caps the heap at 75%, so `mem_limit: 512m` already gives a ~384 MB ceiling. Add
`DOTNET_gcServer=0` and `DOTNET_GCConserveMemory=5`. Expect ~90-120 MB idle, +150-300 KB per
circuit; ~140-190 MB at 30 phones. The bundled WASM files add ~10-20 MB of image size and no
runtime memory — they are served straight off disk.

### Compose

**No published host port at all** — superseding the original `127.0.0.1:8085:8080` plan once
`cloudflared` turned out to be a container, not a native service (see the correction at the top
of this doc). Instead:

```bash
docker network create games_net
docker network connect games_net cloudflared   # additive — cloudflared keeps its default
                                                # bridge network too, other hostnames unaffected
```

`donit-games` joins `games_net` (`external: true` in Compose, since it's created by hand, not
managed by any compose file — `cloudflared` isn't managed by `donit-pi-server`'s compose file
either). Cloudflare's Public Hostname origin for `games.donit.be` is `http://donit-games:8080`
— a compose service name resolves on a user-defined network the same way a container name does.
This is what makes clearing `KnownProxies` safe: nothing on the LAN can reach the app at all,
only `cloudflared` can, over a network no other container is on.

Use `image: ghcr.io/ardontoonstra/donit-games:${DONIT_GAMES_TAG:-latest}` with a committed
`.env` holding `DONIT_GAMES_TAG=latest`. Compose interpolates `.env` locally on Windows even
with `--context rpi`, so setting `DONIT_GAMES_TAG=sha-a1b2c3d` gives immutable deploys and
second-long rollback. Worth retrofitting onto the other services later.

Apply the hardening `TODO.md` prescribes but which has never actually been implemented anywhere
in the stack. This is the first genuinely internet-shared app on the box, so it is the right
pilot: `read_only: true` + `tmpfs /tmp`, `cap_drop: [ALL]`,
`security_opt: [no-new-privileges:true]`, its own `games_net`, **no** docker.sock (it needs
none), `mem_limit: 512m` / `cpus: 1.5` / `pids_limit: 256`, and **log rotation**
(`max-size: 10m`, `max-file: 3`) — which no existing service has, and unbounded json-file logs
on that SSD are a slow-motion disk filler.

One volume: `donitgames_dpkeys:/home/app/.aspnet/DataProtection-Keys` (create and `chown` the
dir in the Dockerfile so a fresh volume inherits ownership). It lands under
`/mnt/ssd/docker-data/volumes/` and is therefore picked up by the existing 03:00 rsync for
free. Game state is deliberately **not** persisted.

Two gotchas that will bite:

- **`build: ../wallos` is broken** — `C:\git\wallos` does not exist, so any repo-wide
  `compose build` / `up --build` fails on the wallos service. **Always scope and always
  `--no-deps`**: `docker --context rpi compose up -d --no-deps donit-games`. Never run
  `compose down` for a single-service deploy — it takes Beszel and Wallos down too. The
  README's documented `down && build && up -d` flow is now actively wrong.
- **The `rpi` docker context does not exist** on this machine (`docker context ls` shows only
  `default` and `desktop-linux`, both erroring — Docker Desktop isn't running). Create it:
  `docker context create rpi --docker "host=ssh://rpi"`. Usefully it talks over SSH via
  `docker system dial-stdio`, so it works **without** a local Docker engine. It does need LAN
  access: `ssh rpi` currently times out from here even though the tunnel is up. **That is step
  zero** — nothing in this section is executable until it works.

### CI/CD — build in Actions, pull on the Pi

- GitHub Actions on push to `main`, `runs-on: ubuntu-24.04-arm` — free *native* arm64 runners
  for public repos, so no QEMU at all.
- Steps: `setup-dotnet` with **both** `8.0.x` and `10.0.x`; checkout both repos; publish the
  WASM app and rewrite its base href to `/undercover/`; stage it into the build context;
  `dotnet test` to fail fast; buildx → push `ghcr.io/ardontoonstra/donit-games:latest` and
  `:sha-<short>`, `cache-from/to: type=gha`, `provenance: false`.
- Then, from `donit-pi-server`: `docker --context rpi compose pull donit-games &&
  docker --context rpi compose up -d --no-deps donit-games`.
- The repo was made **public** before the first successful run, and the GHCR package inherited
  that visibility automatically — no separate "set package visibility" step was needed in
  practice. The Pi pulls anonymously: **no PAT, no `docker login`, no secret on the Pi at all**.
  (If the repo were ever private again, the package would need its visibility set explicitly in
  its own package settings — GHCR visibility doesn't automatically follow repo visibility in
  that direction.)
- Add a `workflow_dispatch` trigger, since a change in `undercover-game` won't trigger this
  workflow and you'll want to rebuild the bundle by hand. (A `repository_dispatch` from the
  other repo is the tidier long-term answer; not worth it for v1.)

Why not build on the Pi: the SDK image is >1.5 GB uncompressed onto an SSD deliberately
throttled to **USB 2.0** for power stability, with NuGet restore competing for RAM against five
other containers. Cross-building locally is no fallback either — Docker Desktop isn't running.
Going `image:` instead of `build:` also sidesteps the `../wallos` relative-context fragility.

**Deploys stay manual, by design.** There are no open router ports, so Actions *cannot* SSH in
— and that is the right outcome: a container restart destroys every in-flight circuit, so a
party game dying mid-round because CI merged a README fix would be worse than typing two
commands. CI publishes; you choose when to swap.

Add `.github/dependabot.yml` (nuget + docker + github-actions, weekly), then digest-pin the
base images and let Dependabot bump them — fixing ":latest everywhere" for at least this repo.

When making the repo public, audit first: `donit-pi-server` has plaintext Beszel `TOKEN`/`KEY`
and no `.gitignore` at all. Don't repeat that. This app has no secrets (no DB, no auth), so
public is genuinely fine.

### Homepage tile

Append to `config/homepage/services.yaml`:

```yaml
    - Party Games:
        icon: mdi-party-popper
        href: https://games.donit.be
        description: Undercover & online party games
        server: my-docker
        container: donit-games
```

`container:` must match `container_name` exactly or the tile shows no status. `settings.yaml`
already has `columns: 4` and this is the 4th app, so no layout edit. And critically:
`config/homepage/Dockerfile` is `COPY . /app/config`, so the YAML is **baked into the image** —
a `restart` does nothing. `compose build homepage && compose up -d --no-deps homepage`.

---

## Post-deploy verification

1. `docker --context rpi compose ps donit-games` — STATUS `healthy`; `logs` clean.
   `docker --context rpi stats --no-stream` — well under 512 MiB.
2. `docker network inspect games_net` — should list exactly `cloudflared` and `donit-games`,
   nothing else. No `ss -ltnp` port check needed — there's no host port to check; that's the
   point of the isolated-network approach.
3. `curl -I https://games.donit.be` → **200, not a 302** to `cloudflareaccess.com`. A 302 means
   an Access policy is gating it; fix that before anything else. A 502 means `cloudflared`
   can't resolve/reach `donit-games:8080` — check both are still on `games_net`; a redirect
   loop means `UseHttpsRedirection` is still there.
4. **The bundled WASM app**: `https://games.donit.be/undercover/` loads and plays; a **hard
   load** of `https://games.donit.be/undercover/how-to-play` returns the app rather than a 404
   (the SPA fallback); and DevTools → Network shows the `.wasm`/`.dat` assets returning 200 with
   sensible content types, not 404s.
5. **Did the WebSocket actually upgrade** — the check that matters. DevTools → Network → filter
   `WS`: exactly one `_blazor?id=...` entry, status **`101 Switching Protocols`**, scheme
   `wss://`, with small frames every ~15 s in the Messages tab. The **failure signature** is no
   `WS` entry at all and instead repeating `POST`/`GET /_blazor?id=...` returning 200
   `text/plain` — that is long-polling fallback, so go back to `http2Origin`, Bot Fight Mode,
   Rocket Loader, in that order. Long polling adds 200-600 ms of input lag, very visible in a
   party game: treat WebSocket as a hard requirement.
6. Two phones on **different** networks (one 4G with wifi off, one wifi) in one room — this is
   what proves the tunnel path, not the LAN. State changes should propagate well under 500 ms.
7. Locked-screen recovery, the acceptance test for the reconnect design:

   | Lock duration | Expected |
   |---|---|
   | 30 s | instant seamless resume (same circuit, `visibilitychange` reconnect) |
   | 3 min | brief "Reconnecting…" then resume with state intact |
   | 8 min | circuit gone → reload → **auto-rejoin the same seat from the cookie** |
   | after a redeploy | reload → rejoin fails → friendly `RoomExpired`, not a hang |

   The 8-minute row fails first if game state lives in component fields.
8. `https://donit.be` shows the Party Games tile with a green indicator (grey = `container:`
   name mismatch, or you restarted homepage instead of rebuilding it).
