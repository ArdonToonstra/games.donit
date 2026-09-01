# Implementation plan

The "where were we" file. Tick items off as they land.

Full design rationale lives in the approved plan; the load-bearing rules are condensed into
[`../CLAUDE.md`](../CLAUDE.md). Deployment is deferred — see [`DEPLOYMENT.md`](DEPLOYMENT.md).

## Shape of the thing

Three games, but only two are built here. The pass-and-play Undercover stays a Blazor
**WebAssembly** app in `C:\git\undercover-game`, untouched, so its taps stay instant; its
published output is bundled into `wwwroot/undercover/`. This repo is the Blazor **Server**
app: the portal plus the two games that need live room state on every phone.

**Accepted trade-off:** the Undercover rules will exist twice — inline in the WASM app's
`LocalGame.razor`, and again in `DonitGames.Core.Undercover`. A rules change must be made in
both. The known-bugs list below doubles as the divergence record.

---

## Phase 0 — Skeleton + docs ✅

- [x] `.gitignore`, `CLAUDE.md`, `docs/IMPLEMENTATION-PLAN.md`, `docs/DEPLOYMENT.md`
- [x] Solution + `DonitGames.Core` + `DonitGames.Web` + `DonitGames.Core.Tests` (net10.0)
- [x] `Directory.Build.props` with the two deliberate overrides
      (`InvariantGlobalization=false`, `ServerGarbageCollection=false`)
- [x] Design system copied verbatim: `design-system.css` (1294 lines), 4 woff2 fonts,
      3 role icons, favicons
- [x] `App.razor` with the ported `<head>` (viewport-fit, theme-color, apple-mobile-web-app)
- [x] Dutch `MainLayout`, portal `Home.razor`, `NotFound`, `Error`
- [x] `ReconnectModal` translated to Dutch and restyled with design-system tokens
- [x] `wwwroot/js/app.js`: manual `Blazor.start` with 60-retry backoff +
      `visibilitychange` fast-reconnect
- [x] `Program.cs`: forwarded headers, circuit/hub options, WASM MIME types,
      `/undercover/` SPA fallback, `/healthz`
- [x] Verified: portal renders, all assets 200, `/healthz` Healthy

## Phase 1 — Words + normalizer

Start here: both games depend on it, and the normalizer is the piece most likely to need
iteration.

- [ ] `WordPair(WordA, WordB)` + `OrientedPair(Civilian, Undercover)` — orientation chosen
      **per draw**, never at load time
- [ ] `WordDataLoader`, `WordPairProvider.Draw(category, rng, exclude)` with
      draw-without-replacement per room
- [ ] `WordNormalizer` / `NormalKeys` / `EditDistance`
- [ ] Dutch + English normalizer test table, **including the negative cases**
      (`kaas`≢`kaa`, `ijs`≢`ij`, `maan`≢`man`)
- [ ] Word-data integrity tests (no duplicate pairs, no A==B, no empties)

## Phase 2 — Room infrastructure, validated with a toy

The highest-risk phase, so it ships in isolation behind a trivial `EchoSession` —
a "who's here / tap the button / see the counter" toy with no game rules at all.

- [ ] `RoomCodeGenerator` (4 chars, alphabet `ACDEFGHJKMNPQRTUVWXY34679` — no `0/O`, `1/I/L`,
      `5/S`, `2/Z`, `8/B`, because these get read aloud across a table)
- [ ] `Seat`, `SeatPresence`, `GameRoom` (lock + version + **notify outside the lock**),
      `RoomRegistry`, `IGameSession`
- [ ] `SeatCookieService` + static-SSR `/join/{code}` form → HttpOnly `dg_seat` cookie
- [ ] `RoomPage` (static) → `RoomShell` (interactive) — seat identity crosses the render-mode
      boundary as a **component parameter**, since the two have different DI scopes
- [ ] `RoomComponentBase` — single subscriber per circuit, self-unsubscribing on
      `ObjectDisposedException`
- [ ] `SeatPresenceCircuitHandler` (ref-counted circuits per seat), `RoomJanitor`,
      `QrCodeRenderer`, `LobbyPanel`, `SeatList`, `HostTools`, `RoomExpired`
- [ ] `GameRoom` concurrency tests, including **the deadlock test**
- [ ] **Real-phone test before any game logic:** join by QR, lock 30 s / 6 min, background the
      browser, restart the process, kick a player, two tabs on one phone

## Phase 3 — Undercover Online

- [ ] `RoleDistribution` (3–10 tables), `SpeakingOrder` (Mr. White never first, may be last)
- [ ] `UndercoverGame` engine + `PublicView` / `PrivateView` leak-proofing
- [ ] `FaceDownCard` pick kept as a *choice* (`PickResult.CardAlreadyTaken` for the race two
      phones can now cause)
- [ ] `UndercoverSession`, per-phase room views, server-authoritative deadline, host overrides
- [ ] One regression test per known bug below

## Phase 4 — Just One

- [ ] `JustOneGame` / `JustOneRound` / `Clue`, phases incl. blind `NumberPick` (1–5)
- [ ] `DuplicateDetector` — union-find so cancellation is **transitive**, and **all** copies
      cancel, not copies-minus-one
- [ ] Near-duplicates **flagged, not cancelled** (a false cancel destroys information the
      table cannot recover; a false miss is what manual review is for)
- [ ] Manual review as a shared toggle; reveal is one action by the **judge**, not unanimity
- [ ] Judging: auto-accept on normalised match, escalate on mismatch, **never auto-reject**
- [ ] Scoring: correct = 1 card; **wrong = 2 cards**; pass = 1 card
- [ ] Degenerate cases as designed states: zero surviving clues, `Away` clue-giver
- [ ] `Data/JustOneWords.yaml` — seed ~120 Dutch words (300+ eventually)
- [ ] `PeekShield`, `DuplicateReviewView`, `.score-track` 13 pips
- [ ] Test that `ViewFor(guesserId).SecretWord == null` during `ClueWriting`

## Phase 5 — Bundle the WASM app locally

Do it by hand first, so the later CI step only automates something already proven.

- [ ] Publish `undercover-game` with `<base href="/undercover/">` into `wwwroot/undercover/`
- [ ] Copy its `WordPairs.yaml` into `Data/` (one source of truth for both variants)
- [ ] Verify a **hard load** of `/undercover/how-to-play` works — the bug GH Pages has today
- [ ] Verify `.wasm` / `.dat` assets return 200 with sensible content types

**End of the build pass.** All three games playable at `http://localhost:5000` and from phones.

---

## Deferred

- **Phase 6 — Ship.** Dockerfile, Actions workflow (incl. the cross-repo WASM publish), GHCR,
  compose, Cloudflare hostname + Access check + the three WebSocket settings, homepage tile.
  See [`DEPLOYMENT.md`](DEPLOYMENT.md).
- **Phase 7 — Polish.** PWA manifest, "Rejoin ABCD" on the portal, spectators,
  `/host/{code}` big-screen view, per-IP join rate limiting.

---

## Known bugs in the pass-and-play app

Left unfixed there (it is untouched); each is a **test case in the new engine**, and the list
is the divergence record between the two Undercover implementations.

| # | Where | What |
|---|---|---|
| 1 | `WordPairClientService.cs:148` | A/B orientation randomised once per app lifetime, not per draw |
| 2 | `LocalGame.razor:1009,1084` | "Shuffle pick order after round 1" branch is unreachable — `StartGame()` resets `isFirstRound` and `NextRound()` calls it |
| 3 | `LocalGame.razor:1357,1373` | Civilian win pays *all* civilians, undercover win pays only survivors. `HowToPlay.razor` documents survivors-only, so the code contradicts the rules |
| 4 | `LocalGame.razor:504` | `players.Count <= 2` should test *surviving* players |
| 5 | Mr. White guess | Raw `OrdinalIgnoreCase` compare — "cafe" vs "café" fails |
| 6 | `LocalGame.razor:201` | `@bind:event="oninput"` — harmless in WASM, a round-trip per keystroke on Server |
| 7 | `SetupRoleAssignment` | Silently truncates the deck, masking a distribution/player-count mismatch |
| 8 | `GetSpeakingOrder` | Reads a different active-player list than card-picking does |
| 9 | five methods | `new Random()` per method — inject one for testability |
