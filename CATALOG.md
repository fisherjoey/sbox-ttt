# Trouble in Terrorist Town — Feature Catalog

A from-scratch implementation reference for cloning Bad King Urgrain's GMod gamemode on s&box. Numbers are taken from the official `troubleinterroristtown.com` settings page, the `troubleinterroristtown.wiki.gg` wiki, and the canonical Lua source at `Facepunch/garrysmod`. Where community lore disagrees with code, code wins; where I have to guess, I say so.

Convar names are reproduced verbatim because they double as a feature checklist. We don't have to expose them all in s&box, but each one points at a behavior we need to make a decision about.

---

## 1. Round structure

Three phases per round (source: <https://www.troubleinterroristtown.com/help/gameplay/>):

| Phase     | Convar                          | Default      | Notes                                                                 |
|-----------|---------------------------------|--------------|-----------------------------------------------------------------------|
| Prep      | `ttt_preptime_seconds`          | **30 s**     | Players spawn, walk around, no roles assigned, no PvP. Weapons can be picked up but not fired. |
| First prep | `ttt_firstpreptime`            | **60 s**     | Only the first round after a map load gets the longer prep.           |
| Active    | `ttt_roundtime_minutes`         | **10 min**   | Roles are assigned at the start of this phase. PvP enabled.           |
| Post     | `ttt_posttime_seconds`          | **30 s**     | Round-end report shown. Stats and karma freeze. No new actions tracked.|

(source: <https://www.troubleinterroristtown.com/config/settings/>)

**Haste mode** (`ttt_haste`, default `1` — i.e. enabled) replaces the static round time with a dynamic one:

- Initial timer: `ttt_haste_starting_minutes` = **5 min**
- Each death adds: `ttt_haste_minutes_per_death` = **0.5 min** (30 s) (source: <https://www.troubleinterroristtown.com/config/settings/>)

The intent is to pressure traitors to act early — sitting on a long innocent stack costs them clock. This is a load-bearing TTT feel; the static 10-minute mode is rarely used in practice.

**Round end conditions:**

- **Traitors win** when every non-traitor (Innocent + Detective) is dead.
- **Innocents win** when every Traitor is dead **OR** the active-phase timer hits 0 with at least one innocent alive.
- A draw/timeout is treated as an innocent win in vanilla — there is no separate timer-runs-out screen. (source: <https://www.troubleinterroristtown.com/help/gameplay/>)

**Map lifecycle:**

- `ttt_round_limit` = **6** rounds before forced map vote.
- `ttt_time_limit_minutes` = **75** real-world minutes before forced map vote (whichever hits first).
- `ttt_minimum_players` = **2** to actually start a round; below this the prep timer just loops.

---

## 2. Roles

Player count drives role counts. **Confirming the formula in the prompt: it is wrong as written.** The real defaults are *percentage-based with a clamp*, not bit-shifts.

(source: <https://www.troubleinterroristtown.com/config/settings/>)

```
traitors   = clamp( floor(N * ttt_traitor_pct),   1, ttt_traitor_max )
              # default pct 0.25, max 32  -> 1/4 of players, min 1

detectives = clamp( floor(N * ttt_detective_pct), 0, ttt_detective_max ) if N >= ttt_detective_min_players else 0
              # default pct 0.13, max 32, min_players 10
```

Concretely, with vanilla defaults:

| Players | Traitors | Detectives |
|---------|----------|------------|
| 2       | 1        | 0          |
| 4       | 1        | 0          |
| 6       | 1        | 0          |
| 8       | 2        | 0          |
| 10      | 2        | 1          |
| 12      | 3        | 1          |
| 16      | 4        | 2          |
| 20      | 5        | 2          |
| 24      | 6        | 3          |
| 32      | 8        | 4          |

The `N>>2` / `N>>3` shorthand produces the same numbers at most player counts but is **not** what vanilla actually computes. Use floor-of-percent so we can change the ratio with a convar later.

**Karma gate on detective:** `ttt_detective_karma_min` = **600**. A player whose karma is below 600 has reduced *probability* of being chosen as detective; they are not hard-excluded. (source: <https://www.troubleinterroristtown.com/config/settings/>) The gmod wiki article on the Detective role mistakenly says "8 players minimum" — the actual convar default is 10. (source: <https://troubleinterroristtown.wiki.gg/wiki/Detective>)

### Innocent

- **Knows:** nothing. Detectives are publicly visible; everyone else is just a fellow "Terrorist."
- **Default loadout:** crowbar, the standard pistol, magneto-stick, possibly a frag/incendiary pickup if the map placed any. No equipment menu, no credits.
- **Win:** all traitors dead, OR clock runs out alive.
- **Special:** can pick up DNA scanners / health stations / etc. dropped by dead detectives. Can identify corpses. Cannot purchase anything. (source: <https://troubleinterroristtown.wiki.gg/wiki/Innocent>)

### Detective

- **Knows:** identity is **publicly broadcast** — everyone sees the blue hat / role on scoreboard.
- **Default loadout:** standard pistol + crowbar + magneto **plus body armor (free) and a DNA Scanner (free)**.
- **Credits:** start with `ttt_det_credits_starting` = **1**. Earn credits when a traitor's body is confirmed (`ttt_det_credits_traitordead` = **1**). Can loot unspent credits off dead detectives/traitors.
- **Win:** with the innocent team.
- **Special:** body searches done by a detective broadcast results to the entire scoreboard, not just the searcher. (source: <https://troubleinterroristtown.wiki.gg/wiki/Detective>)

### Traitor

- **Knows:** identities of all other traitors. Sees fellow traitors highlighted (red name/glow) through walls.
- **Default loadout:** identical to innocent (no free traitor weapon at spawn).
- **Credits:** start with `ttt_credits_starting` = **2**. Earn 1 credit each time the traitors collectively kill `ttt_credits_award_pct` = **35%** of the original innocent count (`ttt_credits_award_repeat` = **1**, so this triggers multiple times per round). Bonus credit on detective kill (`ttt_credits_detectivekill` = **1**). Can loot unspent credits off dead traitors/detectives. (source: <https://www.troubleinterroristtown.com/config/settings/>)
- **Win:** all non-traitors dead.
- **Special:**
  - Equipment menu (default key `C`) with 11 vanilla items (see §4).
  - **Traitor voice/text chat**: hold sprint while talking, or use a `T:` chat prefix (see §6).
  - **Traitor radar** highlights teammates and decoys.
  - **Traitor traps** on maps — buttons in `func_button` with the `traitor`-only filter, fired through map I/O. (source: <https://www.troubleinterroristtown.com/development/mapping/>)

---

## 3. Karma system

This is the part to get right. All numbers from `garrysmod/gamemodes/terrortown/gamemode/karma.lua` (source: <https://github.com/Facepunch/garrysmod/blob/master/garrysmod/gamemodes/terrortown/gamemode/karma.lua>).

| Convar                          | Default | Meaning                                                       |
|---------------------------------|---------|---------------------------------------------------------------|
| `ttt_karma`                     | 1       | Master enable.                                                |
| `ttt_karma_starting`            | 1000    | New player + per-round-start floor.                           |
| `ttt_karma_max`                 | 1000    | Hard cap. Karma above 1000 decays back toward 1000.           |
| `ttt_karma_strict`              | 1       | Strict damage-scaling curve (see formula).                    |
| `ttt_karma_ratio`               | 0.001   | Damage-to-victim-karma penalty ratio.                         |
| `ttt_karma_kill_penalty`        | 15      | Extra "damage" worth applied on the killing blow.             |
| `ttt_karma_round_increment`     | 5       | Karma healed at round end regardless.                         |
| `ttt_karma_clean_bonus`         | 30      | Extra heal if you didn't damage a teammate this round.        |
| `ttt_karma_clean_half`          | 0.25    | Decay coefficient used when karma > starting (see formula).   |
| `ttt_karma_traitordmg_ratio`    | 0.0003  | Karma *gained* per damage point dealt to opposing team.       |
| `ttt_karma_traitorkill_bonus`   | 40      | Karma gained for killing an enemy.                            |
| `ttt_karma_low_autokick`        | 1       | Auto-kick on threshold cross.                                 |
| `ttt_karma_low_amount`          | 450     | Kick threshold.                                               |
| `ttt_karma_low_ban`             | 1       | Ban (not just kick) on threshold cross.                       |
| `ttt_karma_low_ban_minutes`     | 60      | Ban duration.                                                 |

### Damage scaling (outgoing damage multiplier `df`)

Let `k = karma - 1000` (so `k <= 0`).

```
strict mode (default):
  df = 1 + 0.0007 * k + (-0.000002) * k^2

non-strict mode:
  df = 1 + (-0.0000025) * k^2

clamp(df, 0.1, 1.0)
```

A player at 800 karma does **~0.79x** strict / **~0.90x** non-strict damage. At 500 (just above kick threshold) they do **~0.15x** strict / **~0.38x** non-strict. (source: <https://github.com/Facepunch/garrysmod/blob/master/garrysmod/gamemodes/terrortown/gamemode/karma.lua>)

### RDM / friendly-fire penalty

```
hurt_penalty = victim_live_karma * clamp(damage * ttt_karma_ratio, 0, 1)
kill_penalty = victim_live_karma * clamp(15      * ttt_karma_ratio, 0, 1)   # uses kill_penalty constant
```

Penalties are applied immediately to a "live" karma value; the *visible* karma for damage scaling only updates at round start, so a single bad round can't insta-kick someone mid-round (they get one more round at full strength, then start neutered next round).

### Round-end recovery

```
heal = ttt_karma_round_increment + (clean_round ? ttt_karma_clean_bonus : 0)   # 5 or 35
karma = min(karma + heal, ttt_karma_max)
```

If your karma is somehow above 1000, it decays toward 1000 with `ttt_karma_clean_half` (0.25) acting as a half-life-style coefficient — only really matters if a server raised `ttt_karma_max`.

### Autokick

If `karma <= 450` at round end and `ttt_karma_low_autokick = 1`, kick. If `ttt_karma_low_ban = 1`, ban for `ttt_karma_low_ban_minutes` (60).

---

## 4. Equipment shop

Cost is **1 credit per item** for everything in vanilla — this is a deliberate design choice by Urgrain and has not changed. (source: <https://trouble-in-terrorist-town.fandom.com/wiki/Traitor>) Servers commonly mod this; we should treat cost as data, not constant.

Each item has an inventory limit of 1 (you can't stack two C4s) and *most* items are consumed on use. Passive items (Body Armor, Disguiser, Radar) persist for the round.

### Traitor items (11)

| Item             | Cost | What it does                                                                                      | Limit                             | Notes |
|------------------|------|---------------------------------------------------------------------------------------------------|-----------------------------------|-------|
| Body Armor       | 1    | 30% reduction on bullet damage (all hitboxes since later patch).                                  | 1 / round, passive                | (source: <https://troubleinterroristtown.wiki.gg/wiki/Body_Armor>) |
| Radar            | 1    | Reveals all living players' positions every **30 s**, results visible **45 s**.                   | 1 / round, passive                | Disguised traitors hidden from enemy radar. |
| Disguiser        | 1    | Hides name/health/karma in TargetID; hides from enemy radar. Toggle (`ttt_toggle_disguise`, NumpadEnter). | 1 / round, toggle           | Allies still see real ID. |
| Silenced Pistol  | 1    | 28 body / 76 head dmg, 20-round mag, 60 reserve. Body-shot kills suppress death-scream.           | 1 / inventory                     | Slot 7. (source: <https://troubleinterroristtown.wiki.gg/wiki/Silenced_Pistol>) |
| Knife            | 1    | Melee: 50 dmg/stab, **instakill if target ≤ 50 HP**. Throw (RMB): instakill at ≥ 300 units.       | 1 use; consumed on hit            | Slot 7. Anyone can pick it up if it's thrown and missed. |
| C4               | 1    | Plantable bomb, timer 45 s – 10 min. Defuse wire count scales with timer (5/4/3/2/1 safe wires for 45-60 / 1-2 / 2-3 / 3-4 / 4-10 min ranges → 83/67/50/33/17% defuse success). Detective Defuser is instant. Sphere-lethal to 541 u (216 u if blown by failed defuse), blast to ~591 u with LOS. | 1 / round, max 1 alive at a time  | Slot 8. Beeps; Defuser-carrying detectives within 300 u amplify the beep+light. (source: <https://troubleinterroristtown.wiki.gg/wiki/C4>) |
| Flare Gun        | 1    | 4 shots, no reload. 7 base + 50 burn dmg. **Burns corpses to nothing in seconds, destroying ID/DNA.** | 4 shots / round                | Slot 7. Silenced. |
| Newton Launcher  | 1    | Push gun. ~1-5 dmg but launches players/props. 3 s recharge between shots, infinite ammo.         | 1 / round                         | Slot 8. Famous for shoving people off rooftops. |
| Poltergeist      | 1    | Attaches a "thumper" to a prop, slamming it around for prop-kills. 6 thumps per attach, last one explodes. | 1 / inventory, 1 thumper / prop | Slot 8. |
| Decoy            | 1    | Placeable. Generates fake green radar blip; if a detective DNA-scans you, the result points at the decoy instead. | 1 / round                       | Slot 8. Can be destroyed. |
| Radio            | 1    | Placeable. Plays fake gunshot / scream / C4-beep / footstep sounds (queue up to 5). Immune to DNA scanner. | 1 / round                       | Slot 8. Destroyed by gunfire. |

### Detective items (7, plus 2 shared)

| Item             | Cost | What it does                                                                                      | Limit             | Notes |
|------------------|------|---------------------------------------------------------------------------------------------------|-------------------|-------|
| Body Armor       | 0    | (Free at spawn.) Same 30% bullet resist.                                                          | passive           | |
| DNA Scanner      | 0    | (Free at spawn.) See §5.                                                                          | infinite uses     | |
| Radar            | 1    | Same as traitor's, but disguised traitors are hidden.                                             | 1 / round, passive| Shared item. |
| Binoculars       | 1    | Aim at corpse; after a few seconds of held aim, identifies it remotely (any range with LOS). 4 zoom levels. | infinite uses | Cannot loot credits this way. (source: <https://troubleinterroristtown.wiki.gg/wiki/Binoculars>) |
| Defuser          | 1    | Click on a C4 → instant 100% defuse. Carrying it within 300 u of a planted C4 makes it visibly beep louder + flash red. | infinite uses | (source: <https://troubleinterroristtown.wiki.gg/wiki/Defuser>) |
| Health Station   | 1    | Placeable. 200 HP pool, dispenses 1 HP / 2 s tick to anyone holding `E` near it. Slowly recharges. **Logs DNA of every user** (forensic trail). Destructible. | 1 / round | (source: <https://troubleinterroristtown.wiki.gg/wiki/Health_Station>) |
| UMP-45 Prototype | 1    | SMG. 9 dmg / shot, 41 head, 30/60 mag. **Hits jolt the target's aim**, disrupting their fire.     | 1 / inventory     | (source: <https://troubleinterroristtown.wiki.gg/wiki/UMP_Prototype>) |
| Visualizer       | 1    | Placeable near a (gunshot-killed) corpse. Renders blue ghost hologram of victim + killer + shot direction at moment of death. | infinite uses | Auto-activates when a detective who's carrying it dies, revealing their killer. (source: <https://troubleinterroristtown.wiki.gg/wiki/Visualizer>) |
| Teleporter       | 1    | RMB at standstill = mark spot; LMB = teleport. **16 charges / round**. 2-second freeze animation = vulnerable. Cannot mark on props or in elevators. | 16 charges | Originally traitor-only, then shared. Slot 8. (source: <https://troubleinterroristtown.wiki.gg/wiki/Teleporter>) |

**Defibrillator:** *Not vanilla.* Multiple sources call it out as a popular community addon that almost everyone runs, but it isn't shipped in base TTT. (source: <https://trouble-in-terrorist-town.fandom.com/wiki/Defibrillator>) If we want it for v1, treat it as a custom detective tool: ~5 s use animation on a corpse, revives the corpse's owner with the same role they had, consumed on use. Decide deliberately whether to include — we'll skip in v1 to stay vanilla.

### Default weapons (map-armed, no shop cost)

Picked up off the floor, spawned by map-placed `weapon_*` entities or by the rearm system. Source: <https://troubleinterroristtown.wiki.gg/wiki/Weapons/Equipment>

- **Crowbar** — melee, default for everyone.
- **Magneto-Stick** — pickup tool for moving corpses, props, weapons.
- **Pistol** — default sidearm, ~25 dmg body.
- **Glock** — slightly weaker burst sidearm.
- **Deagle** — 37 body / 100+ head (instakill on headshot), 8 round mag.
- **Five-seveN** — pistol-tier, 20-round mag.
- **MAC10** — SMG, 12 body / 32 head, 30 round mag.
- **M16** — rifle, 23 body / 62 head, 20 round mag, scoped.
- **Shotgun** — ~140 unit close range, 8 shells, headshot 3.1x multiplier per pellet.
- **Rifle (sniper)** — bolt-action, high single-shot dmg.
- **HUGE-249** — LMG, low per-shot dmg but high mag.
- **Frag, Incendiary, Smoke, Discombobulator** grenades.

Damage numbers are from <https://trouble-in-terrorist-town.fandom.com/wiki/Weapons/Equipment>; treat as approximate — the exact values live in `weapons/weapon_zm_*` SWEPs.

**Map-armed vs. fallback:** if a map ships its own weapon spawns it uses them; if it doesn't, TTT runs a *rearm script* that scatters a fixed default loadout at random `info_player_*` spawns. (source: <https://www.troubleinterroristtown.com/development/rearm/>)

---

## 5. Bodies and identification

This is the loop the whole game hangs on.

**Identifying a corpse:**

- Walk up, hold `E`. (Or shoot it with binoculars from across the map if detective.)
- Reveals on the **scoreboard** for everyone:
  - Player name (was MIA → "Confirmed Dead").
  - Their role.
  - Time of death, weapon used, what killed them, headshot flag, last words (if any), credits unspent on the corpse, DNA sample if one was registered.
- If the searcher is a detective, the same info is broadcast to **every player's** scoreboard. If innocent/traitor, it's **private to the searcher** — they have to verbalize what they saw. (source: <https://www.troubleinterroristtown.com/help/gameplay/>)
- Looting unspent credits is an option on the search dialog (traitors and detectives only).

**Scoreboard states:**

1. *Alive* — default.
2. *Missing in Action (MIA)* — player is dead, no body found yet. Visible only to spectators in vanilla; living players just see them as alive.
3. *Confirmed Dead* — body has been searched.

Living innocents don't see the MIA marker. That's deliberate — you have to actually find the body to know someone died. (source: <https://www.troubleinterroristtown.com/help/gameplay/>)

**DNA Scanner:**

- `ttt_killer_dna_range` = **550** units. Killer's DNA is only deposited on the victim if the killer was within 550 u at the moment of the kill.
- `ttt_killer_dna_basetime` = **100** s baseline duration. Actual decay is `basetime - distance_factor`: the further away the killer was, the faster the sample degrades (and at point-blank it lasts the full 100 s).
- A round-zero DNA icon appears over corpses with collectable DNA; once expired the body has nothing to scan.
- LMB on a corpse / dropped weapon / health station to **collect** the sample into the scanner. Up to a small number of slots (16 in vanilla).
- Activate a slot to start scanning. Cooldown between scan-pings scales with distance to target: close target = ping every couple seconds, far target = much longer wait. The display shows a directional arrow + range. (source: <https://troubleinterroristtown.wiki.gg/wiki/DNA_Scanner>)
- Most droppable items keep DNA of every player who handled them; **Decoys and Radios are explicitly excluded** so they make good plant-and-frame tools.
- If a Decoy is in play and the scanner targets the killer, results may resolve to the decoy's location instead.

**Defibrillator (community, not vanilla):** see §4.

**Public reveal vs. private:** Detective body searches publish to the round scoreboard; everyone else's searches don't. This is the single biggest reason detectives are powerful even before their equipment.

---

## 6. Communication

(source: <https://www.troubleinterroristtown.com/help/gameplay/>)

- **All-chat (text):** anyone alive types and everyone alive sees it.
- **Team-chat (text):** traitors-only, prefix toggle in chat (`@` or shift-prefix in vanilla). Innocents have no team channel — there's no innocent team. Detectives are the public face of the innocent team and use all-chat.
- **Voice (alive):** by default, all alive players hear each other. Spatialization is global, not 3D, in vanilla — you hear someone across the map.
- **Voice (traitor team-only):** hold **Sprint (Shift)** while talking. Mic key + shift = traitors-only voice. Other traitors get a different colored mic icon to distinguish.
- **Voice (dead):** spectators hear other spectators only. Living players never hear the dead.
- **`ttt_voice_drain`** (default 0): optional "battery" for voice — when enabled, talking depletes a meter and you can be muted by overuse. Off by default.
- **Quickchat / radio commands:** bound menu of canned callouts ("Traitor!", "Suspect", "I'm with X", role-tagged for detective hat etc.). These show in chat and on the scoreboard tags column.

For our v1, the load-bearing pieces are: alive-only voice, traitor team voice on shift, dead-only spectator chat. Everything else is nice-to-have.

---

## 7. Map mechanics

**Naming:** TTT maps use the `ttt_` prefix by convention. A handful of older maps use `terror_` (the gamemode's original name was Terror Town). The TTT map cycle scans these prefixes. (source: <https://www.troubleinterroristtown.com/development/mapping/>)

**Map-armed weapons vs. fallback:** if the map's BSP has TTT-specific entities (`weapon_*` spawn points, `ttt_random_weapon`, etc.) they're used. Otherwise the **rearm** system iterates default `info_player_*` entities and seeds them with a stock loadout. Implementing rearm-fallback is necessary if we want to support generic Source/Hammer maps. (source: <https://www.troubleinterroristtown.com/development/rearm/>)

**Common map gimmicks:**

- **Traitor-only buttons** (`func_button` filtered to traitor team) — wired through map I/O to traps: lava floors, electrified water, falling pianos, lockable rooms, lights-out switches.
- **Traitor testers** — innocent-side deduction devices (typically a chamber that pulses some effect; if you light up red you're traitor). Map-author choice; not engine-supplied. Common on `ttt_innocentmotel`, `ttt_67thway`.
- **Round-end teleporters** — area triggers that warp survivors into a kill-room when the timer is about to expire (anti-camping).
- **Health stations / armory rooms** — pre-placed.
- **Hidden weapons** — Easter-egg M16 / Deagle behind a vent, etc.

**Top maps** (rough community ranking; source: <https://troubleinterroristtown.wiki.gg/wiki/Category:Maps>, <https://steamcommunity.com/sharedfiles/filedetails/?id=186534234>):

1. `ttt_minecraft_b5` — by far the most-subscribed TTT map ever, ported to s&box / Pavlov already.
2. `ttt_67thway` — apartment block; the canonical "first TTT map you ever play."
3. `ttt_rooftops` — high places + Newton Launcher = hilarity.
4. `ttt_community_pool` — small-medium pool complex with ~5 named traps.
5. `ttt_clue` (and `ttt_clue_se`) — the Clue board game mansion.
6. `ttt_waterworld` — Leith Waterworld park, lots of traitor traps.
7. `ttt_innocentmotel` — three-story motel with tester.
8. `ttt_dolls` — toy-store/dolls scale map, very popular.
9. `ttt_lost_temple`
10. `ttt_island_2013`
11. `ttt_mountain_pass`
12. `ttt_camel`
13. `ttt_richland` — small western-town, fast rounds.
14. `ttt_terrortrain` — long thin train map, classic.
15. `ttt_canyon`

For v1 on s&box we should ship 2-3 maps minimum (Minecraft is already ported and is probably the right "headline" map), and define the Hammer entity contract for traitor buttons / testers / round-end traps.

---

## 8. Scoring / post-round

The 30-second post-round phase shows a tabbed report (source: <https://www.troubleinterroristtown.com/help/gameplay/>):

- **Win banner** at top — "Traitors win" / "Innocents win" with team logos.
- **Score tab** — per-player table: name, role (revealed for everyone now), kills, score, karma delta. Score points are awarded for: killing the right team, surviving, planting/defusing C4, etc. Vanilla doesn't expose the exact point breakdown to players; servers tend to add it.
- **Events tab** — chronological event log: "Player A killed Player B with deagle," "Player C searched body of Player D," etc.
- **Highlights / MVP** — picks one player from the winning team based on kills + role-objectives. (source: search results above) Always from the winning team.
- **Karma table** — per-player karma at start vs. end of round, +/- delta, clean-bonus icon if applicable.
- **Traitor reveal** — the round-end screen also colors traitor names red and detective names blue retroactively in chat history.

Stat tracking *stops* during post-round; damage dealt during the 30 s window is not counted toward karma.

---

## 9. Server admin / lifecycle

Vanilla TTT ships only the basics here. Most "real" admin features come from ULX / sandbox-admin addons, not from TTT itself. (source: <https://www.troubleinterroristtown.com/config/commands/>)

**Console commands shipped with TTT:**

- `ttt_roundrestart` — admin, force a fresh round immediately.
- `ttt_print_traitors` — admin, log current traitor list to console.
- `ttt_print_adminreport` — admin, dump kill log.
- `ttt_print_damagelog` — admin, dump damage log (requires `ttt_log_damage_for_console = 1`).
- `ttt_print_usergroups` — admin, list connected player ranks.
- `ttt_dropweapon` — player, drops current weapon.
- `ttt_quickslot N` — player, swap to slot N.
- `ttt_toggle_disguise` — traitor, toggles disguiser.
- `ttt_spectate` — switch to spectator team.
- `ttt_force_terror|traitor|detective` — `sv_cheats 1` only, debug.
- `ttt_cheat_credits` — `sv_cheats 1` only, gives credits.

**RTV / map vote:** **Not in vanilla.** Every server runs an addon (typically <https://steamcommunity.com/sharedfiles/filedetails/?id=151583504> "MapVote — Fretta-like Map Voting"). Threshold is configurable; community default is around **66%** of living players typing `rtv` in chat to trigger a vote. Our v1 should ship something equivalent — RTV is part of the lived experience even if not technically "TTT vanilla."

**Kick votes / tribunal / report:** **Not in vanilla.** Karma autokick (§3) is the only built-in. The "report a player" / tribunal flows people remember are ULX-style admin tools.

**Idle handling:** `ttt_idle_limit` = **180** s. Idle players are forced to spectator after 3 minutes. (source: <https://www.troubleinterroristtown.com/config/settings/>)

**Other notable convars:**

- `ttt_weapon_carrying = 1` — magneto-stick can pick up weapons, not just props.
- `ttt_ragdoll_pinning = 1` — traitors can stake corpses to surfaces with the magneto, hiding them.
- `ttt_namechange_kick = 1` — auto-kick mid-round name changers.
- `ttt_namechange_bantime = 10` — and ban them.
- `ttt_dyingshot = 0` — fatally-shot-but-not-yet-dead players can fire one return shot. Off by default.

---

## 10. Common house rules / community customs

Not coded into the gamemode; players enforce. Worth knowing because they shape how the loop *feels*.

- **"Pass the test."** Early-round demand from suspicious players that you walk through the map's traitor tester. Refusal generates **High Suspicion** but is **not a KOS-able offense in vanilla rules**. (source: <https://minewack-ttt.weebly.com/ttt-rules.html>)
- **T-baiting.** Acting like a traitor — shooting *near* people, swinging crowbar at backs, brandishing a Deagle in someone's face — to bait an RDM. Most servers treat *deliberate* T-baiting as KOS-able (you've forfeited the protection); some treat retaliation as still being RDM. There is no consensus and rules vary per server. (source: <https://trouble-in-terrorist-town.fandom.com/wiki/Traitor_Baiting>)
- **KOS callouts.** "KOS Player X" in chat or voice puts a target on someone — innocents who hear/read it can shoot X without RDM penalty. KOS without justification gets the *caller* in trouble for **false KOS**. KOSes from a confirmed dead or traitor player are typically void.
- **No prop-killing innocents as innocent.** Crowbarring a prop into someone counts as RDM even though the engine attributes it to physics.
- **Cross-fire allowance.** If two players are fighting and a third gets hit by a stray, most communities don't penalize the shooter — but karma still tracks the damage, so it works itself out.
- **"Don't kill last innocent if it's clearly bugged"** — etiquette only.
- **No body-blocking the tester / armory.**
- **Detective-says-it-goes** — many casual servers default to "the detective is acting commander," but this is not enforced by code.

---

## Gap analysis: what's load-bearing vs. what we can defer

Sorting the ~15 most important features by "if this is missing, does it still feel like TTT?"

### Load-bearing — must ship in v1 or it's not TTT

1. **Three-role assignment with the percentage formula** (Innocent / Traitor / Detective, 25% / 13% gated at 10 players). Without this it's just a deathmatch.
2. **Body identification + MIA / Confirmed Dead scoreboard states.** The deduction loop literally does not exist without this.
3. **Traitor equipment menu with credits.** Even if we ship only 4 items (Body Armor, Silenced Pistol, C4, Disguiser), the menu itself is the traitor's whole power fantasy.
4. **Detective public-reveal role + their search broadcasts to all.** Detectives being a known anchor is what makes the social game tractable.
5. **Karma damage scaling + autokick.** Without this, RDM has no cost and the game devolves into deathmatch within 15 minutes of public play.
6. **Round phases with prep / active / post timers and haste mode.** The pacing rhythm.
7. **Traitor team voice/text on hold-Shift.** Coordination is most of the traitor experience.
8. **DNA Scanner (vanilla mechanics).** This is the detective's main toy and the main check on traitor stealth.
9. **C4 with timer + wire defuse.** The signature traitor weapon.
10. **Map traitor buttons / traps via Hammer I/O.** Part of every popular map; if maps can't fire traps, the maps stop being TTT maps.

### Nice-to-have — measurable feel loss but not gamemode-defining

11. **Health Station, Visualizer, Decoy, Radio, Poltergeist, Flare Gun, Newton Launcher, Knife.** Each is a distinctive flavor item. We can ship a v1 with maybe Knife + Flare Gun + Decoy and add the rest in patches.
12. **MVP / Highlights tab in round report.** Score and Events tabs are required; MVP can wait.
13. **Disguiser.** Traitors can win without it; it's just a power fantasy.
14. **Idle-to-spectator forced move.** Useful on public servers but not for playtesting.
15. **Karma clean-bonus / non-strict mode toggle.** Strict mode is the default and the right starting point.

### Skippable in v1

- **Defibrillator** — already not vanilla. Defer.
- **`ttt_dyingshot`** dying-shot mechanic — off by default in vanilla anyway.
- **Voice drain (`ttt_voice_drain`)** — off by default.
- **Karma ban-on-kick** — kick is enough; ban can be manual for a while.
- **Quickchat radio commands** — text chat covers it.
- **Per-map weapon rearm fallback** — only matters if we want to support arbitrary user maps; for shipped curated maps we just place spawns ourselves.
- **Round-and-time map cycling (`ttt_round_limit` / `ttt_time_limit_minutes`)** — can hardcode rotation in v1 and add the convars later.
- **Rock-the-vote** — also not vanilla; ship without and add when the player base asks.

---

## Open questions / uncertain spots

- The **MVP scoring formula** is not documented anywhere I could find. Source code dive into `terrortown/gamemode/scoring.lua` would be the next step. The community generally treats it as opaque.
- **Knife throw distance** is reported as "300 units" in the wiki but the original code review thread referenced `~256`. Either is fine for our v1; pick one and commit.
- **C4 defuse-failure damage** numbers (216 u sphere) come from the wiki and aren't double-confirmed against source. Worth verifying against `weapons/weapon_ttt_c4.lua` before we lock the radius.
- **Detective minimum players**: convar default is 10, the wiki article says 8. Trust the convar (10).
- **Teleporter charges**: wiki says 16; some forks say 8. Vanilla source = 16. Use 16.
- **Whether the Defibrillator is "in vanilla"** depends on which version of TTT you played: it's been in some semi-official builds and absent from others. Treat as community.

---

## Sources

- <https://www.troubleinterroristtown.com/> — official site (Bad King Urgrain).
  - <https://www.troubleinterroristtown.com/help/gameplay/> — gameplay overview.
  - <https://www.troubleinterroristtown.com/config/settings/> — every convar default.
  - <https://www.troubleinterroristtown.com/config/commands/> — every console command.
  - <https://www.troubleinterroristtown.com/development/mapping/> — map entities.
  - <https://www.troubleinterroristtown.com/development/rearm/> — rearm fallback.
- <https://troubleinterroristtown.wiki.gg/> — community wiki (canon-ish).
  - Per-item pages: Knife, Radar, Disguiser, C4, Body_Armor, Silenced_Pistol, Flare_Gun, Newton_Launcher, Poltergeist, Decoy, Radio, Visualizer, UMP_Prototype, Teleporter, Binoculars, Defuser, Health_Station, DNA_Scanner.
  - Role pages: Innocent, Detective, Traitor.
  - Map category: <https://troubleinterroristtown.wiki.gg/wiki/Category:Maps>.
- <https://github.com/Facepunch/garrysmod/blob/master/garrysmod/gamemodes/terrortown/gamemode/karma.lua> — authoritative karma formulas.
- <https://trouble-in-terrorist-town.fandom.com/> — older Fandom wiki, useful for community lore (Defibrillator, Traitor Baiting, etc.).
- <https://github.com/CigarLounge/sbox-TTT> — existing s&box port, treat as derivative reference.
- <https://steamcommunity.com/sharedfiles/filedetails/?id=186534234> — community map ranking guide.
