# Mystic Light preview — mystic-preview (39 preview states)

- v1 built by Kanban task t_30586601 (batch `mystic-preview-20261113`).
- v2 built by Kanban task t_2faf1985 (same batch): spinning warning beacon
  options 6-10 from `beacon-designs.md`. Patterns 1-5 byte-identical to v1.
- **v3 built by Kanban task t_f40d250e** (same batch): the 12 creative
  patterns 11-22 from `creative-patterns.md`. Patterns 1-10 are
  byte-identical to v2 (verified by exact-frame comparison, every phase
  0..119, see "Verify" below).
- v3.1 (2026-11-15, user feedback, direct fix): beacons 6-10 now render
  over a dim amber base (48,24,0) instead of pure black — the ring has
  color almost all the time, no all-dark frames; comet period 60→40
  slots (head can no longer fall off the 48-slot ring; 2.0 s/rev);
  heartbeat breath floor 0.3 (ring never dips to black); pattern gap
  3 s → 1 s. Patterns 1-5 unchanged.
- **v4.0 (2026-11-15, user feedback — "apply the learnings from this to
  all 22 samples"): the live-latch map is now a first-class rule.** This
  board lights exactly 14 latches: onboard slots 0..5 and fan-ring slots
  8..15. Slots 6..7 (empty j_rgb headers) and 16..47 (no LEDs wired) are
  DEAD — anything painted there is invisible, so moving features that
  toured the old 48-slot ring vanished mid-ring for most of each lap
  (~29% of the motion was visible in the worst cases). Every pattern now:
  keeps all moving elements on the 14 live latches; spins stay on their
  own ring (fan ring P8 = 0.4 s/rev, onboard ring P6 = 0.3 s/rev,
  combined period 24 | 120); never leaves a ring dark while crossing to
  the other; no black frames; no off-canvas windows. Details under "v4
  verification".
- **v5 (2026-11-17, patterns 1-5 re-based to the confirmed slot map):**
  The 2026-11-16 probe walk confirmed the live set as slots 0..13 — board
  ring 0..5 (P6) + ALL-fan ring 6..11 (the 7/8/9/10/12/1 o'clock
  positions, P6), plus slots 12/13 (2/3 o'clock, top fans only). Patterns
  1-5 (darkblue, perfan, chase_cool, chase_warm, beacon) are re-based
  onto the confirmed map, mirroring user-approved pattern 6 (sweep): board
  0..5 + fan 6..11, P6, period 6 | 120, +i, 1 latch/frame; slots 12/13
  SKIPPED; dead slots (14..239) never painted. The old 8..15 span (which
  painted dead 14/15 and skipped live 6/7) is retired for 1-5. Patterns
  7-22 still run the old span (re-base "later"). `--check` re-asserts 1-5
  (live latches colored, dead latches black, period-6 wrap); daemon
  restarted and each of 1-5 demoed live via `.override` (rendered, then
  removed). Source of truth for the full map:
  `~/workspace/mystic_light/msi-led-slot-map.md`.
- **v6 (2026-11-17, Kanban task t_ccaa35e2 — patterns 7-13 re-based to
  the confirmed slot map):** beacon options 7-10 (tandem, heartbeat,
  comet, hazard) and creative 11-13 (bitclock, telegraph, fireflies)
  re-based onto the confirmed map, mirroring user-approved pattern 6:
  fan ring 6..11 (ALL-fan positions, P6) + board ring 0..5 (P6); slots
  12/13 (top fans only) SKIPPED per the user's ALL-fans-priority
  decision; dead slots (14..239) never painted. Combined periods: 7/9/10
  -> 6 | 120, 11/12/13 -> 120 (fan ring now P6, so lap/echo/flies ride
  the 6-frame ring), 8 (heartbeat) breath 60 | 120, combined 60 | 120.
  The beacon family keeps its BEACON_BG (48,24,0) base and the
  no-all-dark-frames rule. Fireflies re-spaced (bases 7/8/9/10, amps
  1.0/1.25/1.25/1.0) so every fly stays within 6..11; its baseline now
  covers 0..11 only, so 12..47 stay pure black. The old 8..15 span is
  retired for 7-13 (14-22 still run it pending their own re-base).
  `--check` re-asserts 7-13 (per-phase head positions, period wrap,
  live latches >= BEACON_BG, non-live latches black); daemon restarted;
  each of 7-13 demoed live via `.override` (rendered, then removed,
  daemon back to temp-driven).
- **v7 (2026-11-17, Kanban task t_a90e1fdf — patterns 14-22 re-based to
  the confirmed slot map):** the last nine of the old span (creative:
  lava, sonar, metronome, orbit, bitstream, sparkler, echo, ecg, walk)
  re-based onto the confirmed map, mirroring user-approved pattern 6:
  board ring 0..5 + ALL-fan ring 6..11 (P6, center 8.5, half-width 2.5);
  slots 12/13 (top fans only) SKIPPED per the user's ALL-fans-priority
  decision; dead slots (14..239) never painted; the legacy 8..15 span
  (and its `LIVE_FAN`/`ONBOARD_SLOTS` constants) is fully retired. All
  motion now rides the 6..11 ring: lava blob centers 8.5±2.5 over a
  deep-purple fan baseline (board 0..5 near-black); sonar ripple expands
  from 8.5 to the live edges 6/11 every 8 frames (white 3-frame edge
  flash, ping lamp slot 4); metronome marker 8.5−2.5·cos(2πt/40) — true
  60 BPM, extrema on 6/11 every 20 frames, onboard 3-bit count on
  slots 0..2; orbit planet at the ring center (2-slot core 8/9 +
  4-slot halo 6/7/10/11) with satellite radii 2.5/1.75/1.0 so all
  three stay within 6..11 (hidden on the far side), pass lamps 0/2/4;
  bitstream 4-slot payload chunks clipped to 1.5 chunks of 6..11 (12
  shifts per 120-frame cycle), start-bit lamp slot 5; sparkler bursts
  on live 7/9/11, sparks wrap the 6-slot ring, fuse slot 5, dots at
  the pads; echo round trip 6→11→6 (20 frames) with ghosts lag 5/10 and
  wall flashes at the live edges; ecg trace only on 6..11, dim green
  baseline on board 0..5, rate lamp slot 4 at the R peak's center; walk
  round trip 6↔11 with the head clamped to the live ring, companion
  dot slot 3. `--check` re-asserts 14-22 (per-pattern anchors + every
  phase of all 9: board slots stay at their base colors, 12..47 always
  pure black, 120-frame wrap seam bit-identical). Daemon restarted;
  each of 14-22 demoed live via `.override` (rendered, then removed;
  daemon back to temp-driven). **Patterns 1-22 ALL now run the
  confirmed slot map.**
- **v8 (2026-11-17, Kanban task t_4ca6adee — pattern 27 `rainbow`):**
  new preview-only feature (user spec): a multi-colored wheel SLOWLY
  ROTATING around the ALL-fan ring 6..11 — at phase 0 the ring holds
  the six wheel colors in order (6=red, 7=orange, 8=yellow, 9=green,
  10=blue, 11=violet) and the whole wheel slides clockwise, one slot
  position per 20 frames (1 s), one full rotation per 120-frame phase
  (6 s/rev — the slowest full rotation whose period divides 120); smooth
  and continuous — every slot stays fully lit every frame (no blink).
  Board ring 0..5 mirrors the same wheel in phase (P6+P6, the sweep
  convention). Slots 12/13 (the GPU LEDs — sanctioned for this feature)
  slowly cycle the SAME six colors, one color at a time: 12-frame hold
  + 8-frame fade per color (1 s per color, one full loop per phase).
  14..239 dead, never painted. `--check` re-asserts 27 (phase-0 anchors
  on fan+board, clockwise walk, period wrap, per-frame liveness on all
  14 live slots, dead latches black, smoothness bound, 12/13 hold/fade
  anchors). Daemon restarted; 27 demoed live via `.override` (rendered,
  then removed; daemon back to temp-driven). Catalog rows 23-26
  back-filled in this version (they were never in the table).
- **v9 (2026-11-17, Kanban task t_632f5071 — pattern 28 `limelight`):**
  new preview-only GPU-showcase feature: the GPU gets the limelight —
  slots 12/13 (the top-fan LEDs that shine down on the GPU) breathe a
  bright star: warm gold (90,60,20) at the trough → bright white
  (255,255,250) at the peak, one breath per 60 frames (3 s, precomputed
  60-entry sine table — period 60 | 120 bit-exact). The rest of the case
  is a dim INVERSE-breathing backdrop (the room goes dark as the
  spotlight brightens): the ALL-fan ring 6..11 is a deep-blue wash
  (8,12,40)→(30,48,140) and the board ring 0..5 a dimmer blue wash
  (4,6,20)→(16,26,70), both at their brightest (1-b) when the star is
  dimmest and at their floor when it peaks. Live slots 0..13 never fully
  black (liveness); 14..239 dead, never painted; smooth (max per-frame
  delta 13, well under the 32 no-blink bound). `--check` re-asserts 28
  (exact peak/trough/half-breath anchors on 12/13 + fan + board, period-60
  bit-identity, per-frame liveness, dead black, smoothness). Daemon
  restarted; 28 demoed live via `.override` (rendered, then removed;
  daemon back to temp-driven).
- **v10 (2026-11-17, Kanban task t_69b8f586 — pattern 29 `catwalk`):**
  new preview-only RUNWAY + PHOTO FLASHES feature. The ALL-fan ring
  6..11 is the RUNWAY: a dim magenta carpet (70,12,60) with a strutting
  light bar — a bright warm-white 1-slot "model" head (255,255,250) + a
  2-slot fading hot-pink afterglow tail (255,90,190 then 150,30,110) —
  walking the ring 1 slot/frame (P6, 0.3 s/lap). Slots 12/13 (the GPU
  position — sanctioned for this feature) are the PHOTOGRAPHERS at the
  end of the runway: at every lap (t%6==0, as the model crosses the
  front) both fire a 2-frame near-white camera flash (255,255,250) + a
  1-frame afterglow (150,150,145) over a dim warm-gray standby
  (70,70,62) — rapid fire, one burst per lap (~3.3/s). The board ring
  0..5 is the AUDIENCE: a dim gold glow (48,38,16) that cheers (2 frames
  bright gold (235,195,105) + 1-frame glow (130,100,55)) on every flash.
  The flash is an INTENTIONAL hard blink (a photo flash blinks — no
  smoothness bound). Period 6 | 120 bit-exact; live 0..13 colored every
  frame (liveness); 14..239 dead, never painted. `--check` re-asserts 29
  (per-phase head/tail/carpet anchors on the fan ring, per-lap-phase
  flash/afterglow/standby anchors on 12/13 + cheer/glow/base anchors on
  the board, flash near-whiteness, period-6 bit-identity, per-frame
  liveness, dead black). Daemon restarted; 29 demoed live via
  `.override` (rendered, then removed; daemon back to temp-driven).
- **v11 (2026-11-17, Kanban task t_7fa723a8 — pattern 30 `supernova`):**
  new preview-only COSMIC STORY feature — the most ambitious pattern of
  the batch: a 6-second story in ONE loop with a climax. Slots 12/13
  (the top-fan LEDs that shine down on the GPU — sanctioned for this
  feature) are a STAR. BUILDUP (t 0..39, 2 s): the star charges from
  its dim gold rest state (89,61,39) toward white-hot (255,245,225),
  rippling, while the ALL-fan ring 6..11 swells a blue corona
  (10,14,40→40,60,160) and the board ring 0..5 swells a dimmer blue
  (6,9,28→24,36,110). DETONATION (t 40..45, 0.3 s): the explosion —
  EVERY live latch flashes near-white (255,255,250) for 2 frames, then
  cools to a hot-gold fireball (255,205,90) — the intentional hard blink,
  the climax. SHOCKWAVE (t 46..85, 2 s): a bright band (1-slot head +
  1-slot fading tail) sweeps the fan ring 6..11 AND the board ring 0..5
  IN PHASE (P6, 1 slot/frame), cooling bright gold (255,210,70) → deep
  orange-red (210,80,30) over cooling ember (90,44,18→50,22,10); the
  band fades out over the last 10 frames; meanwhile the star is a
  REMNANT PULSAR (fast near-white pulse). AFTERGLOW (t 86..119, 1.7 s):
  the sky cools — both rings blend ember → calm night blue (fan
  10,14,40 / board 6,9,28) and the pulsar settles back into the dim gold
  rest star. SEAMLESS LOOP: t=119 is bit-identical to t=0 (the night
  falls and a new star begins to charge). Period 120 | 120; live 0..13
  colored every frame (liveness, min channel [6,9,10]); 14..239 dead,
  never painted. `--check` re-asserts 30 (period-120 bit-identity,
  seamless-loop frame(119)==frame(0), buildup rest anchors on 12/13 +
  fan + board, detonation all-live near-white/fireball anchors,
  shockwave band head/tail/ember anchors on fan 6..11 AND board 0..5 IN
  PHASE + remnant pulsar, shockwave-end uniform-ember + afterglow
  continuity, afterglow rest anchors, per-frame liveness, dead 14..239
  black). Daemon restarted; 30 demoed live via `.override` (rendered,
  then removed; daemon back to temp-driven).
- **v12 (2026-11-17, Kanban task t_aa09a638 — pattern 31 `inferno`):**
  new preview-only FURNACE feature — the GPU sits underneath a furnace.
  ALL-fan ring 6..11 slowly cycles dark red (120,24,6) → bright orange
  (255,130,24) on a 60-frame cosine breath (t=0 trough, t=30 peak;
  60 | 120). Top fans 12/13 (sanctioned for this feature) pulse randomly
  like flames: a bright yellow/orange base (255,170,30) lerp'd toward
  near-white by 0.35+0.5·v where v is a deterministic 120-entry
  fixed-seed-LCG flicker table (period 120 | 120), with a hard near-white
  spark (255,255,250) whenever v ≥ 0.82 (24 of 120 frames). Board ring
  0..5 glows a dim ember (70,16,4)→(180,70,16) in phase with the fan
  cycle. Live 0..13 colored every frame; 14..239 dead, never painted.
  `--check` re-asserts 31 (period-120 bit-identity, fan/board
  trough@t=0 + peak@t=30 anchors, 12/13 in-sync + always ≥ base color +
  spark frames 5..30, per-frame liveness on 0..13, dead 14..239 black).
  Daemon restarted; 31 demoed live via `mystic-preview 31` (rendered,
  then removed; daemon back to temp-driven).
- **v13 (2026-11-17, Kanban task t_eafc748b — pattern 32 `reactor-core`):**
  new preview-only REACTOR feature — the GPU as the POWER SOURCE of a
  futuristic reactor. ALL-fan ring 6..11: deep blue, slowly pulsing —
  (8,16,70) → (36,96,230) on a 60-frame cosine breath (t=0 trough, t=30
  peak; 60 | 120). Top fans 12/13 (sanctioned for this feature): bright
  cyan (150,235,255) that flashes BRIGHTER toward near-white
  (255,255,250) every 60 frames (3 s) on a 13-frame raised-cosine pulse
  (u=0..12, peak u=6) — as if energy is being transferred into the GPU.
  Board ring 0..5: a dim blue wash in phase with the fan breath
  (4,8,34)→(18,48,116) that ripples toward (60,140,255) by 0.4·pulse at
  each flash. Live 0..13 colored every frame; 14..239 dead, never
  painted. `--check` re-asserts 32 (period-120 bit-identity,
  fan/board trough@t=0 + peak@t=30 anchors, 12/13 in-sync + flash
  anchors near-white at t=6/66 + R/G never below base + B in the
  250..255 band, per-frame liveness on 0..13, dead 14..239 black,
  per-frame smoothness ≤ 1/8 range). Daemon restarted; 32 demoed live
  via `mystic-preview 32` and a direct `.override` (rendered, then
  removed; daemon back to temp-driven).

- **2026-11-21 (batch `ring10-20261121`, final task — RING10 re-base of
  ALL 39 preview states):** `ring10-rebase-spec.md` re-based all 39
  states onto RING10 (standard ring slots 2..11, P10, 1 slot/frame,
  0.5 s/rev) + TOPFANS (12/13): 12/13 are painted EVERY frame of EVERY
  state (never black; floor BEACON_BG (48,24,0)) — the user REVERSED
  the 2026-11-16 "skip 12/13" decision (void); the old board ring 0..5
  is RETIRED — slots 2..5 are ring positions (2/4/5/6 o'clock), slots
  0/1 (live, unassigned) are pure black in every state; 14..239 dead,
  never painted. The legacy LIVE_ONBOARD/SWEEP_FAN/SWEEP_ONBOARD
  constants were removed (no code references remained). Full 39-state
  catalog below; canonical spec:
  `~/workspace/mystic_light/ring10-rebase-spec.md`.

## Commands (no sudo)

    mystic-preview            # cycle ALL 39 preview states: 10 s state + 1 s
                              # pure-black gap each, ~430 s total, then exit
    mystic-preview 16         # show only pattern 16 (metronome) until Ctrl+C
    mystic-preview list       # numbered catalog (1-39)

Stop: Ctrl+C at any time (single-pattern mode, or to interrupt a cycle run).
A full no-arg run also stops itself after pattern 39 (~430 s).

Picking a favorite: just say "I like number N" — it can be made permanent
(e.g. always-on, or wired in as the daemon's base state).

On exit (natural or Ctrl+C) the override is removed and the daemon resumes
temp/power-driven rendering; the script verifies this via `gpu_rgb.py status`.
The 1-second gap between patterns is the internal `off` state (pure (0,0,0)
on every slot) — not a numbered pattern.

## All 39 preview states (stable numbering — `list` is the source of truth)

**All 39 states re-based 2026-11-21 to RING10 (slots 2..11, P10) +
TOPFANS (12/13)** (batch `ring10-20261121`, spec `ring10-rebase-spec.md`):
12/13 are painted EVERY frame of EVERY state (never black; floor
BEACON_BG (48,24,0)); slots 0/1 (live, unassigned) are pure black in all
states; 14..239 dead, never painted. The 2026-11-16 "skip 12/13"
decision is **VOID** (the user reversed it 2026-11-21). The old board
ring 0..5 is retired — slots 2..5 are ring positions (2/4/5/6 o'clock).
The legacy LIVE_ONBOARD/SWEEP_FAN/SWEEP_ONBOARD constants were removed
(final task). **Exception (user 2026-11-22): states 23/24/25 light ONLY
12/13 — RING10 2..11 pure black in all three (the re-base ring wash is
removed).**

| # | Pattern | What it does |
|---|---------|--------------|
| 1 | `darkblue` | static dark blue (33,33,140) on RING10 2..11; 12/13 dim dark blue (48,24,70) |
| 2 | `perfan` | 6-color wheel recast on RING10 (colors repeat once over 10 slots, P10); 12/13 dim blue |
| 3 | `chase_cool` | three 2-slot blocks (blue/orange/yellow) chasing on RING10 P10 — old two rings merged into the single ring; 12/13 dim base + flare |
| 4 | `chase_warm` | same chase, warm palette (yellow/orange/red) |
| 5 | `beacon` | 2-slot yellow bulb + 1 fading tail over the steady orange ring, RING10 P10, head leading; 12/13 dim orange base + flare |
| **6** | **`sweep`** | THE TEMPLATE: 1-slot yellow head + 3-slot linear amber afterglow, P10 on RING10; 12/13 dim amber base + f-blend flare (f=1.0 head on 10, 0.5 on 9/11) |
| 7 | `tandem` | two 2-slot amber heads counter-rotating on RING10 P10 (one +1/frame, one -1/frame), 4-slot smoothstep tails; 12/13 amber base, f = max over both heads |
| 8 | `heartbeat` | breathing deep-orange ring on RING10 (60 \| 120, floor 30%) + 1-slot yellow lap dot P10; 12/13 breathe in phase |
| 9 | `comet` | 2-slot yellow head + smoothstep gradient tail filling RING10 (-i), P10; 12/13 dim base + flare |
| 10 | `hazard` | 2-slot head strobing 10 Hz (amber<->yellow) + blocky 4-slot orange tail on RING10 P10; 12/13 strobe in sync (floor = base) |
| 11 | `bitclock` | 6-bit counter on RING10 2..7 (moved from board ring); lap flash on ring slots 8..11 (15f decay at the 120f wrap); 12/13 breathe in phase with the lap (floored BEACON_BG) |
| 12 | `telegraph` | transmitter on ring slot 4 (5 o'clock): Morse "H" on-15/off-15 (8 units = 120f); echo = whole RING10 10f late at 35% warm white over dim BEACON_BG; 12/13 strobe base <-> echo in sync |
| 13 | `fireflies` | four warm-amber flies drifting on RING10 (board-ring fifth fly retired); 12/13 breathe in phase to the fly core by the max envelope |
| 14 | `lava` | three additive blobs drifting across RING10 (center 6.5, amp 4.0; 120/60/40f periods); board near-black ring retired; 12/13 motion rule base -> head (f = max over blobs) |
| 15 | `sonar` | ripples expand from RING10 center 6.5 (30f, 0.85^d falloff); edge flash 2/11; ping lamp on ring 6/7; board baseline + rate lamp dropped; 12/13 base + cyan flare (f = max over ripple points) |
| 16 | `metronome` | 60 BPM pendulum swings RING10, extrema at edges 2/11 every 20f; 3-bit tick counter dropped (board ring retired); downbeat = 3f white flash on marker + edges; 12/13 base + gold flare (f = marker position) |
| 17 | `orbit` | planet recentered: 2-slot core 6/7 + halo 4/5/8/9; satellite radii x1.8 (outer reaches edge 2/11); onboard pass lamps dropped; 12/13 base + flare toward the planet's white (f = max over visible satellites) |
| 18 | `bitstream` | same 12-bit payload as 4-slot chunks scrolling left across RING10 (2.5 chunks visible, clipped; 1 chunk/10f = P10, 12 shifts per 120f seamless); start-bit lamp moved to 12/13 (5-on/5-off blink, floored BEACON_BG) |
| 19 | `sparkler` | bursts at pads 7/9/11 every 40f (3f near-white flash, 2f shock ring d=1..3, 4 sparks/side wrapping RING10); fuse countdown moved to 12/13 (tb 35..39); 12/13 flare toward white while a burst is live (f=1.0 burst@11, 0.5@9, 0@7) |
| 20 | `echo` | bounce 2<->11 in 20f (ghosts lag 10/5, widths 2.0/1.0); wall flash at ring edges 2/11 (3f); baseline BEACON_BG (old pure black gone); 12/13 motion-rule flare toward the white core (f from the nearest slot) |
| 21 | `ecg` | vital-signs trace scrolls RING10 (beat 30f); board baseline dropped; rate lamp moved to 12/13 (2f white as the R peak crosses ring center 6/7) + motion-rule flare toward the white R peak (f=1.0 on 10, 0.5 on 9/11) |
| 22 | `walk` | creature walks 2<->11 (45/10/45/10 timeline), body bob P=10, blink t=20..27; companion dot moved to 12/13 (blink in sync, floored) |
| 23 | `gpu-spotlight-idle` | **ONLY 12/13 lit** — very-slow blue->green->purple spotlight subject (1 loop / 6 s); everything else pure black (user 2026-11-22) — temp model level 0 |
| 24 | `gpu-spotlight-fire` | **ONLY 12/13 lit** — fast red->orange->yellow subject (1 loop / 0.75 s); everything else pure black (user 2026-11-22) — preview-only (level 3 is 26) |
| 25 | `gpu-spotlight-somewhat-active` | **ONLY 12/13 lit** — medium yellow->white->orange subject (1 loop / 1 s); everything else pure black (user 2026-11-22) — temp model level 1 |
| 26 | `red_white` | static — RING10 (2..11) pure red; 12/13 pure white; 0/1 black — temp model level 3 |
| 27 | `rainbow` | rotating 6-color wheel on RING10 (6 colors repeat once over 10 slots; slides clockwise 1 slot/20 f, 1 full rotation per 120 f — smooth, no blink); board mirror dropped; 12/13 keep the one-color-at-a-time cycle (12-f hold + 8-f fade) |
| 28 | `limelight` | 12/13 breathing star unchanged (warm gold (90,60,20) trough -> bright white (255,255,250) peak, 1 breath / 60 f); RING10 dim blue wash breathes INVERSE (the room dims as the star flares); board wash dropped |
| 29 | `catwalk` | runway on RING10: magenta carpet + strutting warm-white light bar (1-slot head + 2-slot hot-pink tail, P10, 1 slot/frame); 12/13 = the photographers — every lap both fire a 2-frame near-white camera flash + 1-frame afterglow over dim standby; audience dropped; period 120 |
| 30 | `supernova` | 12/13 STAR unchanged (buildup -> detonation near-white/fireball -> remnant pulsar -> afterglow); SHOCKWAVE band (1-slot head + 1-slot tail) sweeps RING10 (P10, 1 slot/frame) cooling gold -> deep orange-red over ember; board ring dropped; period 120 |
| 31 | `inferno` | 12/13 = flames (bright yellow/orange flicker table + hard near-white sparks when the table >= 0.82); RING10 slowly cycles dark red (120,24,6) -> bright orange (255,130,24) (60-f cosine breath); board ember dropped; period 120 |
| 32 | `reactor-core` | 12/13 = bright cyan energy, flashing toward near-white every 60 f (13-f raised-cosine pulse); RING10 deep blue slowly pulsing (8,16,70) -> (36,96,230) (60-f cosine breath); board wash dropped; period 120 |
| 33 | `matrix` | green columns + white flashes: columns on RING10; 12/13 dim green base + flash sync; 0/1 black |
| 34 | `cyberpunk` | purple/cyan glitch on RING10; 12/13 dim base + burst flare; 0/1 black |
| 35 | `cryogenic` | ice-blue frost on RING10; 12/13 dim ice base + flare; 0/1 black |
| 36 | `emergency-mode` | RING10 dark blue rest, 3-frame red/amber/red strobe at t=0..2 (150 ms per 120-f phase); 12/13 strobe IN SYNC with the ring, dim red/amber base (>= BEACON_BG floor) otherwise; 0/1 black |
| 37 | `lightning-strike` | 12/13 (the strike point) dim blue rest -> pure white t=0..1; RING10 dark blue -> cyan wash t=2..3 (a split second later, sequential non-overlap); everything dark after t=3; 0/1 black |
| 38 | `sweeping-rainbow` | RING10 white-head sweep 1 slot/12 f (one full pass per 120 f = 6 s), swept slots orange (12-f decay), 7-color base cycle (18-f red + 6 x 17-f, seamless); 12/13 track the head base color DIM (0.5x, floored to BEACON_BG, in sync); 0/1 black |
| 39 | `plasma-storm` | 10 independent fixed-seed 120-entry brightness walks (2..6-frame holds = 100..300 ms re-randomize) dark violet <-> bright purple on RING10; 12/13 dim violet base; full-case near-white flash on 2..13 at t=96..101 (once / 6 s, "occasionally"); 0/1 black |

Beacon options 5-10 all keep color almost all the time: 6-10 render over a
dim amber base (48,24,0) added 2026-11-15 (5 has its own steady orange
ring, 8 its breathing ring), so no all-dark frames occur in any of them.

Patterns 5-22 are PREVIEW-ONLY (never temp/power-selected). v4 rule set:
moving elements only on the 14 live latches (onboard 0..5, fan ring
8..15); anything that spins stays on its own ring (fan P8 / onboard P6,
combined period 24 | 120); one head per ring — a single touring dot left
the other ring dark while crossing; lit spots stay ≤ 2-4 slots wide
(point zone, well under the 8-LED fan window — a spin, not a pulse);
dead latches (6..7, 16..47) are never painted; every period divides the
120-frame phase wrap, so the seam is bit-identical. Full specs:
`beacon-designs.md` (6-10) and `creative-patterns.md` (11-22).

## How it works (safe / single-writer)

- `/home/link744/bin/mystic-preview` only writes/removes
  `/home/link744/bin/gpu-rgb/.override` (`{"state": <name>}`, atomic
  replace). It never touches the HID device.
- The gpu-rgb daemon (the single HID writer) picks the override up within
  one frame (50 ms) and renders it; removal returns it to temp/power-driven
  rendering.
- The daemon must be running: `systemctl is-active gpu-rgb`
  (start/restart: `sudo systemctl start/restart gpu-rgb` — NOPASSWD-scoped).
- Do not run two instances at once (both would write the override).

## gpu_rgb.py changes in v3 (additive — patterns 1-10 untouched)

- Twelve new preview-only frame functions: `bitclock_frame`,
  `telegraph_frame`, `fireflies_frame`, `lava_frame`, `sonar_frame`,
  `metronome_frame`, `orbit_frame`, `bitstream_frame`, `sparkler_frame`,
  `echo_frame`, `ecg_frame`, `walk_frame` (+ helpers `_tri`, `_add_slot`,
  `_fill_range`, `_firefly_env`, `_lava_bump`, `_echo_pos`), each painting
  slots 0..47 (slots 48..239 stay black) and 120-periodic in phase.
- `PREVIEW_STATES` registry extended to all 18 preview states (5-10 + 11-22);
  `read_override()` whitelist is `PREVIEW_STATE_NAMES`
  (= `PREVIEW_STATES` ∪ `{"off"}`), so all 22 preview names are accepted —
  an unlisted name is the "invisible beacon" silent-ignore regression.
- `preview` subcommand + usage text + `--check` cover all 22 names.
- `--check` now also asserts: all 12 new states (right length, in-range
  bytes, black tail slots, 120-frame wrap seam bit-identity) with exact
  per-spec frame anchors (bitclock bits/lap, telegraph dot/echo, fireflies
  baseline/peak, lava blob A, sonar edge flash/ping lamp, metronome
  extremum/marker/count, orbit pass lamps/planet, bitstream payload/start
  bit, sparkler bursts/fuse, echo core/ghosts/wall, ecg R/T/trace/lamp,
  walk head/companion/look/dead-window) and the 22-name whitelist.

IMPORTANT — daemon restart required once per patch: the running daemon must
re-load the patched file before the new states actually render. Done for v3:

    sudo systemctl restart gpu-rgb

## Verify

    gpu_rgb.py --check       # offline validation (30-name whitelist + 11-22 anchors)
    gpu_rgb.py status        # shows "override : <name>" while previewing
    mystic-preview list      # shows 30 numbered patterns

Final verification pass (Kanban task t_07de544c, 2026-11-14):
- `list` clean: 22 patterns, exit 0.
- Full no-arg run #1: rc=0, all 22 patterns in order, 3 s black gaps, zero
  errors, override removed, daemon back to temp-driven.
- Interruption: SIGINT ~28 s into a run (pattern 3) → "stopped (Ctrl+C).",
  override removed, daemon back to temp-driven (journal-confirmed live
  rendering of each override).
- Full no-arg run #2 (double-run): rc=0, 22/22, zero errors, clean restore.
- Dead-daemon path (sandboxed, live daemon untouched): stale or missing
  state file → clear "daemon is not running" error, exit 1, no override
  written.
- Daemon running and temp-driven after every test.

v3 verification (this task):
- `--check` green (incl. all 11-22 anchors + 22-name whitelist).
- Full no-arg run rc=0 (~286 s) cycling ALL 22 patterns in order with 3 s
  black gaps; every state's `.override` confirmed on disk via 1-s polling;
  natural exit removed the override and the daemon returned to
  temp-driven (verified via `status` + journal).
- Ctrl+C mid-run (SIGINT, around pattern 3): override removed, daemon back
  to temp-driven. Spot-checks of single-pattern runs (16 metronome, 3
  chase_cool) also verified override-on-disk + cleanup.
- Journal: every override rendered live by the daemon (no silent ignores),
  no errors during any run.
- Patterns 1-10 (+off) byte-identical to the v2 baseline: exact frame bytes
  compared for all 10 patterns at every phase 0..119 (1,200 comparisons,
  0 mismatches); original PREVIEW_STATES entries (5-10) unchanged.

## v4 verification (2026-11-15)

- `--check` green: all 22 states re-anchored on the live-latch map.
  New asserts: dead latches (6..7, 16..47) never painted in any state;
  per-ring head positions for beacon/sweep/tandem/heartbeat/comet/hazard
  (every phase of the 24-frame combined period); per-pattern spot values
  (bitclock 6-bit count, telegraph echo window, fireflies fly positions,
  sonar edge flash, metronome extrema, orbit pass lamps, bitstream
  window, sparkler burst/fuse slots, echo bounce, ecg R-peak center lamp,
  walk look-around windows); 120-frame wrap seam bit-identity for all 12
  creative states.
- Daemon restarted once (`sudo systemctl restart gpu-rgb`), status
  confirmed temp-driven.
- Full no-arg run: all 22 patterns cycled, override confirmed on disk per
  state, clean restore to temp-driven at exit.

## Files in this dir

- `mystic-preview`   — v3 script (installed at /home/link744/bin/mystic-preview)
- `gpu_rgb.py`       — v3 patched daemon (runtime dir /home/link744/bin/gpu-rgb/)
- `gpu_rgb.py.orig`  — pre-v1 original backup (md5 bb0c607e4cfffe8adf681bd275c99cba)
- `beacon-designs.md` — the 5 beacon option specs (options 6-10)
- `creative-patterns.md` — the 12 creative pattern specs (options 11-22)
- `full-run.log`, `full-run-driver.py`, `ctrlc-test.py` — v1 test evidence
- v3 test evidence: see the `creative-build/` workdir of task t_f40d250e
  (batch dir `batches/mystic-preview-20261113/`) — identity_test.py,
  spotcheck.sh, fullrun.sh, csc_test.sh
