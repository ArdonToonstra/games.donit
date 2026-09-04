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

## Phase 1 — Words + normalizer ✅

- [x] `WordPair(WordA, WordB)` + `OrientedPair(Civilian, Undercover)` — orientation chosen
      **per draw**, never at load time
- [x] `WordDataLoader`, `WordPairProvider.Draw(category, rng, exclude)` with
      draw-without-replacement per room
- [x] `WordNormalizer` / `NormalKeys` / `EditDistance`
- [x] Dutch + English normalizer test table, **including the negative cases**
      (`kaas`≢`kaa`, `ijs`≢`ij`, `maan`≢`man`)
- [x] Word-data integrity tests (no duplicate pairs, no A==B, no empties) — run against the
      live `Data/WordPairs.yaml` via a `Link`ed file in the test project, not a copy

## Phase 2 — Room infrastructure, validated with a toy ✅ (code + automated verification)

The highest-risk phase, so it shipped in isolation behind a trivial `EchoSession` —
a "who's here / tap the button / see the counter" toy with no game rules at all.

- [x] `RoomCodeGenerator` (4 chars, alphabet `ACDEFGHJKMNPQRTUVWXY34679` — no `0/O`, `1/I/L`,
      `5/S`, `2/Z`, `8/B`, because these get read aloud across a table)
- [x] `Seat`, `SeatPresence`, `GameRoom` (lock + version + **notify outside the lock**),
      `RoomRegistry`, `IGameSession`
- [x] `SeatCookieService` + static-SSR `/join/{code}` form → HttpOnly `dg_seat` cookie
- [x] `RoomPage` (static) → `RoomShell` (interactive, `EchoRoomShell` today) — seat identity
      crosses the render-mode boundary as a **component parameter**
- [x] `RoomComponentBase` — single subscriber per circuit, self-unsubscribing on
      `ObjectDisposedException`
- [x] `SeatPresenceCircuitHandler` (ref-counted circuits per seat), `RoomJanitor`,
      `QrCodeRenderer`, `LobbyPanel`, `SeatList`, `HostTools`, `RoomExpired`
- [x] `GameRoom` concurrency tests, including **the deadlock test** (caught one real bug: the
      first draft recursed into a stack overflow on a re-entrant `Mutate`, not the classic lock
      deadlock — fixed and covered)
- [x] Playwright multi-context smoke test (two real browser circuits, no manual steps): create →
      QR/lobby renders → guest joins → presence updates live on the host's screen with no
      reload → tap on one circuit propagates to the other via the room's pub/sub → hidden-info
      projection (`YouAreLastTapper`) correct per viewer → host kicks guest → guest reactively
      hits `RoomExpired` → host's seat list updates live → unknown code hits `RoomExpired`. Also
      caught a real bug: `EditForm` already renders its own antiforgery token when `FormName` is
      set, so the explicit `<AntiforgeryToken />` duplicated the hidden field and a real browser
      (unlike a hand-crafted request) submitted both values, which antiforgery validation
      rejected outright — removed.
- [ ] **Real-phone test, still pending (needs physical hardware):** join by QR, lock 30 s / 6 min,
      background the browser, restart the process, two tabs on one phone, and the
      `cloudflared` tunnel WebSocket-upgrade check — none of these are exercisable from here.

## Phase 3 — Undercover Online ✅ (code + automated verification)

- [x] `RoleDistribution` (3–10 tables), `SpeakingOrder` (Mr. White never first, may be last)
- [x] `UndercoverEngine` (pure, static, `Random` always threaded in) + `UndercoverSession.ViewFor`
      leak-proofing (deck cards never carry Role/Word; other players' Role/Word hidden until
      eliminated or game-over; own Role/Word always visible)
- [x] `FaceDownCard` pick kept as a *choice* (`PickResult.CardAlreadyTaken` for the race two
      phones can now cause) — required adding a generic-result overload to `GameRoom<TState>.Mutate`
      (Phase 2), fully backward compatible
- [x] `UndercoverSession`, per-phase room views (`Lobby → CardPicking → Discussion → Voting →
      EliminationReveal → [MrWhiteGuess] → Results`), server-set `DiscussionDeadlineUtc`, host
      overrides (`StartVoting`, `ForceEliminate`, tie resolution)
- [x] One regression test per known bug below (#1–#9; #3, #4, #5 got dedicated tests since they're
      genuine rule bugs — #2/#7/#8 are structurally impossible to reintroduce given the new
      single-source-of-truth roster, and #6/#9 don't apply server-side)
- [x] Playwright 4-player smoke test: create → join → lobby live-updates → start game →
      **deliberate simultaneous same-card tap → exactly one `CardAlreadyTaken`** → all pick →
      Discussion → Voting with a clean majority → EliminationReveal → (Mr. White branch when
      drawn) → Results with a correct scoreboard. Ran 10+ times across different random role
      draws (both `Undercover wint!` and `De burgers winnen!` outcomes observed, Mr. White's
      guess exercised) — root-caused two real issues along the way: `@bind:event="onchange"`
      needs an explicit blur to fire in headless automation (test-only), and the deck-picking
      race assertion needed a positional lock instead of a `:not([disabled])` filter that
      Playwright's auto-wait would silently retarget (test-only, not an app bug).
- [ ] Real-phone pass — deferred alongside Phase 2's, same reason (needs hardware).

## Phase 4 — Just One ✅ (code + automated verification)

- [x] `JustOneEngine` / `JustOneState` / `Clue`. The blind `NumberPick (1–5)` die-roll mechanic
      was dropped after checking with you — each round draws one word directly
      (`JustOneWordBank.Draw`, same draw-without-replacement contract as Phase 1's
      `WordPairProvider.Draw`); `Data/JustOneWords.yaml` is a flat word list, not 5-per-card.
- [x] `DuplicateDetector` — union-find over `WordNormalizer.IsNearDuplicate` (which already treats
      an exact match as a subset of "near", so one pass covers both edge types) — cancellation is
      **transitive** (a 3-word A≈B≈C chain groups even when A and C aren't directly near) and
      **all** copies cancel, never copies-minus-one (regression-tested directly)
- [x] Near-duplicates (no exact pair inside the group) **flagged, not cancelled** — a `ReviewGroup`
      with a shared `ManuallyCancelled` toggle any clue-giver can flip
- [x] Reveal is one action by the **judge** (the room host; falls to the first other active seat
      when the host is this round's guesser, computed per-round not stored) — not unanimity
- [x] Judging: an exact normalized match (`WordNormalizer.AreEquivalent`) auto-accepts; anything
      else escalates to `JudgeReview` — **never auto-rejects** (regression-tested: a wrong-looking
      guess never resolves to `Incorrect` on its own)
- [x] Scoring: correct = 1 pip; **wrong = 2 pips**; pass = 1 pip; clamped at 0; 13 starting pips
- [x] Degenerate cases as designed states: zero surviving clues routes straight to
      `RoundResult(NoClues)` without ever entering `Guessing`; an away (unsubmitted) clue-giver is
      handled by the host/judge's `CloseClueWriting` override, mirroring Undercover's
      `ForceEliminate`
- [x] `Data/JustOneWords.yaml` — 123 curated Dutch words, integrity-tested (no empties, no
      duplicates even after normalizing)
- [x] `PeekShield`, `DuplicateReviewView`, `ScoreTrack` (13 pips)
- [x] `ViewFor(guesserId).SecretWord == null` during `ClueWriting` — plus the same discipline
      extended to clue text itself (clue-givers don't see each other's text while still writing,
      only a submitted-count) and to cancelled clues (absent from the guesser's view entirely,
      not merely flagged, same projection discipline as Undercover's deck cards)
- [x] Playwright 4-player smoke test: create → join → a genuine duplicate (two clue-givers
      deliberately writing the same clue) confirmed cancelled for the guesser, who sees exactly
      the 1 surviving clue → correct guess via exact match auto-accepts → round 2 forces a
      mismatched guess through `JudgeReview` (never auto-rejected) → judge marks it incorrect →
      full drain loop through to `GameResults` with 0 pips remaining. Ran 4 times cleanly. Two
      real test bugs found and fixed along the way (not app bugs): `PeekShield`'s eyebrow text is
      identical across every phase the guesser sees it in, so phase-detection had to key off the
      phase-specific message body instead; and the drain loop's iteration budget was too small to
      reach 0 pips across many rounds.
- [ ] Real-phone pass — deferred alongside Phases 2–3's, same reason (needs hardware).

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
