# Config + voting design

This is the persistent design for player-tunable round settings. Live decisions captured here so they don't decay into chat history. Implementation phasing is at the bottom.

## Goals

1. Make every meaningful round-affecting value tunable, with vanilla TTT defaults from CATALOG.md.
2. Let host edit values directly (Among Us model).
3. Let players force a config conversation when the host's settings are unfun, via a deliberate friction-heavy vote flow that prevents griefing.
4. Validators block configs that produce unwinnable or unreachable game states.

## What's tunable

Six categories. Knob count is intentionally bounded — Among Us exposes ~25, this is similar.

### Pace
- `PrepDuration` (default 30s) — `ttt_preptime_seconds`
- `FirstPrepDuration` (60s) — `ttt_firstpreptime`
- `ActiveDuration` (300s) — haste starting time, or static round time if haste off
- `PostRoundDuration` (30s)
- `HasteMode` (true)
- `HasteSecondsPerDeath` (30s)

### Roles
- `TraitorPct` (0.25)
- `DetectivePct` (0.13)
- `DetectiveMinPlayers` (10)
- `MinDetectiveKarma` (600)

### Combat
- `BaseHealth` (100)
- `HeadshotMultiplier` (2.0) — `ttt_damage_headshotmult`
- `FriendlyFire` (true) — TTT requires it
- `DyingShot` (false)

### Karma
- `KarmaMode` (Strict / Lenient / Off)
- `StartingKarma` (1000)
- `KickThreshold` (450)
- `RecoveryPerRound` (5)
- `CleanRoundBonus` (30)

### Economy
- `StartingCredits` (2 for Traitor, 1 for Detective)
- `KillBonusCredits` (1)
- `MaxCredits` (10)
- `EquipmentLimitPerRound` (0 = unlimited)

### Map
- `RoundLimit` (6)
- `TimeLimitMinutes` (75)
- `MinPlayers` (2)
- `IdleToSpectatorSeconds` (180)

## Validators

Run on every config commit, before the change is applied. A failure surfaces as a UI error and blocks the save/vote. Listed by category.

### Liveness invariants
- `floor(MinPlayers * TraitorPct) >= 1` — at least one traitor possible. Hard rule.
- `TraitorPct + DetectivePct < 0.95` — innocents must always exist.
- `ActiveDuration + HasteSecondsPerDeath * MaxLikelyDeaths < 30 * 60` where `MaxLikelyDeaths ≈ MinPlayers` — timer can't grow beyond 30 minutes, otherwise haste mode loops forever.

### Pace floors
- `PrepDuration >= 10`
- `ActiveDuration >= 60`
- `PostRoundDuration >= 5`
- `HasteSecondsPerDeath >= 0`

### Karma sanity
- `KickThreshold < StartingKarma` else no one ever gets kicked.
- `RecoveryPerRound > 0` if `KarmaMode != Off` else karma only ever decreases.
- `damage_scale_factor(KickThreshold) > 0` — at the kick threshold, damage scaling must produce some non-zero damage; otherwise low-karma players are functionally invincible.

### Economy sanity
- `StartingCredits + KillBonusCredits * 2 >= 1` else shop is unreachable on any round.
- `MaxCredits >= StartingCredits`.

### Detective gating
- If `DetectivePct > 0`: `DetectiveMinPlayers >= ceil(1 / DetectivePct)` else detectives never spawn. At 13% you need 8+ players for the formula to round to 1, so DetectiveMinPlayers should be ≥ 8. Vanilla uses 10 as a buffer.

### Combat
- `BaseHealth >= 50` — instakill weapons trivialize the game otherwise.
- `0.5 <= HeadshotMultiplier <= 5.0`.

## Vote flow (motion → second → vote)

Three-state machine, host-authoritative. Anti-grief guarantees baked in.

```
            ┌──────────────────────────────────────────────┐
            │                                              │
            ▼                                              │
         Idle ──── any player /motion ────► Motion ────────┤
                                              │            │
                                              │ 30s timer  │
                                              │            │
                          quorum not met ─────┴──── seconds ≥ 25%
                                                           │
                                                           ▼
                                                       Vote (60s)
                                                           │
                                                           ├──── ≥60% yes ──► Apply config, Idle
                                                           │
                                                           └──── < 60% yes ──► Cooldown, Idle
```

### Tunings (these are themselves tunable, but with sane defaults)

| Knob | Default | Notes |
|---|---|---|
| `MotionDuration` | 30s | Time to gather seconds |
| `MotionQuorumPct` | 0.25 | Of connected non-spectators. At 8 players → 2 needed |
| `VoteDuration` | 60s | Time the actual vote stays open |
| `VotePassThreshold` | 0.60 | Super-majority — config changes are sticky |
| `CooldownAfterFail` | full round | After failed motion or failed vote, no new motions until next prep |
| `MaxMotionsPerMap` | 2 | Hard cap regardless of outcome |

### Timing rules

- Motion phase is **non-blocking ambient UI** — small banner, doesn't pause the game, can fire mid-Active.
- Vote phase **only fires during Prep or PostRound** — the actual UI panel that demands attention is deferred. If a motion passes mid-Active, the vote queues until the round ends.
- During Vote phase, if a player joins, they get a default-no vote (treated as abstain unless they actively yes).
- During Vote phase, if a player disconnects, their vote is removed from both numerator and denominator.

### Anti-grief specifics

- The **player who motions** can't second their own motion (prevents 1-player griefing where motion == second).
- After a failed motion (timeout) **or** failed vote, that map's motion budget decrements. Three failures → no more votes until map change.
- Cooldown is per-map, not per-player. One player griefing doesn't punish them specifically; it costs the map a motion slot.

## UI requirements

- **Host inspector** (v2) — open `GameConfig` component in the s&box editor inspector, edit values, validators run on save. No in-game UI yet.
- **In-game lobby panel** (v3) — Razor + SCSS, sliders + toggles, validation errors highlighted next to fields, "Reset to recommended" per knob computed from current player count.
- **Motion banner** (v4) — small unobtrusive top-of-screen banner during motion phase: `"Player X motioned to change settings — N/M seconds (press F12 to second)"`.
- **Vote panel** (v4) — full-screen panel during prep, shows what's changing (diff against current), yes/no buttons, countdown.

## Phasing

| Phase | Scope | Status |
|---|---|---|
| **v1** | Hardcoded vanilla defaults; no config object | ✅ shipped (commit `02d3919`) |
| **v2** | `GameConfig` Component, `[Sync]`'d host→all, host edits via inspector. Validators run on host-side commit. | scaffolding now |
| **v3** | In-game host config panel (Razor UI, sliders + toggles + validator surfacing) | post-editor |
| **v4** | Motion → second → vote flow with all the anti-grief rules above | post-v3 |

v2 is the load-bearing one — it unblocks playtesting with non-default values. v3/v4 are UX layers on top.

## Anti-ghosting strategy

"Ghosting" = a dead player communicating with a living player out-of-band (Discord, in person) to leak role information. Three layers, in priority order:

### Layer 1 — chat/voice scoping (do this first)

The vast majority of in-game info leakage is solved by routing chat correctly. Vanilla TTT does this and most clones get it right.

- **Voice chat.** Alive players can hear other alive players. Dead players hear other dead players. No crossing. Only exception: the round-end period, where everyone can talk freely.
- **Text chat.** Alive↔alive, dead↔dead. Spectator chat is its own channel. Traitor team chat (hold-Shift in vanilla) is alive-traitors-only and visible only to alive traitors.
- **Voice radius / proximity.** Vanilla TTT is global voice, but a proximity option `ttt_voice_radius` exists. Off by default; expose as a config knob.

This layer is `v2` work — needs the chat/voice layer wired up, no detection ML required, no false-positive risk. It eliminates the *capability* to leak rather than trying to detect the *behavior*. Highest ROI by far.

### Layer 2 — admin tools (post-launch)

Surface suspicious patterns for human review rather than auto-flagging:

- Per-player kill-target distribution across rounds (do you always shoot the same player who happens to be alive when you're spectating?)
- Suspect-accusation accuracy (does your kos call resolve to traitors faster than chance?)
- Time-from-teammate-death-to-suspect-shot histograms (do you suddenly know who to shoot 5 seconds after a friend dies?)

Admin opens a per-server dashboard, sees patterns, makes a human call. No auto-action. This is `v3+` and requires telemetry plumbing we haven't designed.

### Layer 3 — algorithmic detection (probably never)

Anomaly detection on in-game signals (innocents-shooting-traitors-faster-than-chance, dead-player gaze correlating with friend's kills) is technically possible but fundamentally hard:

- **Skilled players look identical to ghosters.** Reading tells, following hunches, reacting to suspicious behavior is the actual game. A heuristic that flags "innocent who shot the traitor first" punishes skill.
- **No ground truth.** You can only validate against community reports, which are noisy and political.
- **Adversarial.** Once any heuristic is documented, ghosters adjust.

If we ever build this, it should output suspicion scores for admin review (i.e. enhance Layer 2), not auto-action. Punt indefinitely.

### What this means for v1/v2

- Voice/text scoping is on the v1 cut list (per CATALOG.md gap analysis: load-bearing) and stays cut for v1 — but escalate to **first item in v2** because it's the cheapest meaningful improvement to the social game.
- No detection algorithm work. If a community wants telemetry hooks for admin tools, we add the data emission in v3 and let the community build dashboards.

## Open questions

- **Presets.** Should we ship "Vanilla / Action / Stealth" preset slots as `GameResource` files that load into `GameConfig`? Probably yes, but defer to v3.
- **Does the motion-phase banner work mid-Active?** Concern: during a tense Active phase, an interrupting banner pulls attention. Could compromise: banner only appears in third-person spectator HUD, hidden from alive players' first-person.
- **Mid-round motion → vote-deferred-to-prep** — this may feel slow. Alternative: allow vote to fire mid-Active but only show panel during prep. Defer this until we playtest.
- **Karma knobs are dangerous to tune.** Maybe `KarmaMode` slider exposes only `Off / Lenient / Strict` presets and the underlying knobs aren't directly user-editable.
