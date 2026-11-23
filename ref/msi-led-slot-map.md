# MSI Mystic LED — Slot Map & Light-Feature Reference

**Read this before writing new light features or modifying existing ones.**
Machine: MSI "Aegis ZS2" (RTX 4080 SUPER). Control runs from Linux; the
`gpu-rgb` systemd service is the single HID writer (code:
`/home/link744/bin/gpu-rgb/gpu_rgb.py`).

**Source of truth:** per-address probe walk completed 2026-11-16
(every zone/LED isolated — one address lit bright white, every other
slot pure black — and matched to physical position by the user), plus
the onboard `hold all` confirmation, **plus the fan-ring o'clock
re-probe completed 2026-11-21** (`mystic-ring-test`: one LED slowly
around a complete circle, three runs, user confirmed the o'clock times
in walk order). The 2026-11-21 re-probe supersedes the 2026-11-16
o'clock assignments for slots 2..11; see section 2. Re-verify with
`mystic-ledprobe` / `mystic-ring-test` if wiring is ever changed or
behavior contradicts this doc.

---

## 1. Addressing

- Per-LED stream: **725-byte packet at 20 FPS** = 3-byte enable header
  (`0x52 0x01 0x00`) + 720 bytes RGB = **240 slots**; slot `i` = bytes
  `3+3i .. 3+3i+2`.
- The daemon is the only process allowed to write the MSI hidraw device
  (udev rule grants `link744` read/write on the MSI vendor device).
- Preview/override mechanism: writing
  `/home/link744/bin/gpu-rgb/.override` with `{"state": "<name>", ...}`
  makes the running daemon render that state **within one frame (50 ms)**
  — no restart needed for demos. Delete the file → daemon returns to
  temp/power-driven. `mystic-preview` and `gpu_rgb.py preview` are
  wrappers (they auto-clear after a timeout).

### Zone → packet-slot map (from `mystic-ledprobe`, mirrors daemon `LEDPROBE_ZONES`)

| Picker # | Zone        | Packet slots | Count |
|----------|-------------|--------------|-------|
| 1        | `onboard`   | 0..5         | 6     |
| 2        | `j_rgb_1`   | 6            | 1     |
| 3        | `j_rgb_2`   | 7            | 1     |
| 4        | `j_rainbow_1` | 8..47      | 40    |  ← **the case fans**
| 5        | `j_rainbow_2` | 48..87     | 40    |
| 6        | `j_corsair` | 88..207      | 120   |
| 7        | `spare`     | 208..239     | 32    |

Firmware side (GET 0x52 table, provenance only — 18 zones):
`j_rgb_1, j_pipe_1, j_pipe_2, j_rainbow_1 (40), j_rainbow_2 (40),
j_corsair (120), j_corsair_outer, on_board_led_0..9 (10 entries),
j_rgb_2`. Note the firmware lists **10** `on_board_led` entries while the
per-LED stream exposes only **6** onboard slots (0..5) — see open items.

## 2. Confirmed slot map (what each slot actually does)

**O'clock positions re-verified 2026-11-21** — `mystic-ring-test` ran
one bright LED slowly around a complete circle (three runs); the user
read the o'clock times in walk order: **12, 1, 2, 4, 5, 6, 7, 8, 10,
11** (back to 12). This supersedes the 2026-11-16 o'clock labels:
slots 6, 7, 10, 11 unchanged; slots 8, 9 move one hour late; onboard
slots 2..5 newly assigned; **3 and 9 o'clock are unassigned** (the
ring's two dark gaps).

| Slot(s) | Zone        | Physical behavior (user-confirmed) |
|---------|-------------|-------------------------------------|
| 0..1    | `onboard`   | **DEAD as individual latches** (tested 2026-11-22: per-LED frame slot 0 red / slot 1 green, rest black, 180 s hold — user confirmed no light). No ring position (not in the 2026-11-21 walk). The zone is live at ZONE level only (2026-11-16 `hold onboard all` showed the board ring); individually lit addresses = 2..5 |
| 2       | `onboard`   | Standard ring, **2 o'clock** |
| 3       | `onboard`   | Standard ring, **4 o'clock** |
| 4       | `onboard`   | Standard ring, **5 o'clock** |
| 5       | `onboard`   | Standard ring, **6 o'clock** |
| 6       | `j_rgb_1`   | Standard ring, **7 o'clock** |
| 7       | `j_rgb_2`   | Standard ring, **8 o'clock** |
| 8       | `j_rainbow_1` idx 0 | Standard ring, **10 o'clock** |
| 9       | `j_rainbow_1` idx 1 | Standard ring, **11 o'clock** |
| 10      | `j_rainbow_1` idx 2 | Standard ring, **12 o'clock** |
| 11      | `j_rainbow_1` idx 3 | Standard ring, **1 o'clock** |
| 12      | `j_rainbow_1` idx 4 | **TOP case fans only — shining down on the GPU; NOT part of the standard ring** (no o'clock position) |
| 13      | `j_rainbow_1` idx 5 | **TOP case fans only — shining down on the GPU; NOT part of the standard ring** (no o'clock position) |
| 14..47  | `j_rainbow_1` idx 6..39 | **DEAD** (no light — walked) |
| 48..87  | `j_rainbow_2` | **DEAD** (no light — walked) |
| 88..207 | `j_corsair` | **DEAD** (no light — walked) |
| 208..239| `spare`     | **DEAD** (no light — user's walk reported the second "zone 6" as zone 7/spare; assumed) |

**Live set = slots 2..13 (12 latches). Slots 0..1 light nothing
individually (tested 2026-11-22 — see table above); everything else is
dead.**

### Ring geometry (what features should use)

- **Standard ring: contiguous slots 2..11 (10 positions)** — o'clock
  labels in clockwise walk order: **12** (slot 10) → **1** (11) →
  **2** (onboard 2) → **4** (onboard 3) → **5** (onboard 4) →
  **6** (onboard 5) → **7** (j_rgb_1 6) → **8** (j_rgb_2 7) →
  **10** (j_rainbow_1 8) → **11** (j_rainbow_1 9) → back to **12**.
  **3 and 9 o'clock are unassigned** — a moving dot skips them (two
  dark gaps in the ring). 10 positions = **P10** (0.5 s/rev at 20
  FPS; 10 divides PHASE_PERIOD 120).
- **Top-fans-only: slots 12, 13** — shining down on the GPU; **NOT
  part of the standard ring** (no o'clock position). Used by the
  gpu-spotlight / limelight / catwalk / supernova family — everything
  else dark.
- **Board-only: slots 0..1** — DEAD as individual latches (2026-11-22
  test: red/green frame, no light); no ring position. The onboard zone
  only lights via addresses 2..5 (ring positions 2/4/5/6 o'clock).
- The old/legacy span **8..15** is fully retired (2026-11-17 — all 30
  patterns now run the confirmed map): it includes dead slots 14, 15
  and skips live 6, 7 — do not use it for new work.

## 3. User decisions (binding — do not revert without asking)

1. **2026-11-16 — prefer ALL-fan slots, skip 12/13 — VOID (reversed by
   the user 2026-11-21).** Original: "Give priority to the ALL fans and
   skip slot 12 and 13. The top fans aren't as visible as the others."
   (fan ring = **6..11**; slots 12/13 only by exception — gpu-spotlight
   23/24/25, rainbow 27, limelight 28 — and this decision also fixed a
   ~100 ms blink when a moving dot crossed the top-fan-only slots.)
   **The user reversed it on 2026-11-21**: every one of the 39 preview
   states now paints slots 12/13 EVERY frame (re-based to RING10 +
   TOPFANS — see section 5). Do not re-introduce the skip.
2. **2026-11-15 — keep the board ring — SUPERSEDED 2026-11-21** (re-base
   to RING10 + TOPFANS: the board ring 0..5 is retired — slots 2..5 are
   now ring positions, slots 0/1 are live but unassigned and pure black
   in every pattern). The user had confirmed the board LEDs are visible
   and wanted the board dot/ring in two-ring patterns (a fan-only sweep
   variant was tried and reverted the same day).
3. **2026-11-15 — beacon family (options 6–10) renders on a dim amber
   base `BEACON_BG = (48, 24, 0)`**, never pure black; no all-dark
   frames.
4. **2026-11-13 — bulb/arc smaller than the ring window.** A moving
   feature that covers its whole ring reads as a pulse, not a rotation
   (bit the us on the P14/P24 arc==window case).
5. **Pattern 6 (`sweep`) is the user-approved reference** for the
   re-based model — **re-based 2026-11-21**: RING10 (slots 2..11, P10)
   with a 1-slot yellow head + 3-slot linear amber afterglow; TOPFANS
   12/13 hold a dim amber base and flare toward the head color when the
   head crosses slot 10 (f=1.0) or 9/11 (f=0.5). (Supersedes the old
   two-ring reference: fan ring 6..11 + board ring 0..5.)
6. **2026-11-16 — temp drive model rewired to spotlight/sweep.** Level 0
   (zone 1) = `gpu-spotlight-idle`, level 1 (zone 2) =
   `gpu-spotlight-somewhat-active`, level 2 (zone 3) = `sweep` (pattern 6),
   level 3 (zone 4) = `red_white` (pattern 26 — replaced
   gpu-spotlight-fire there 2026-11-16); adjacent levels crossfade.
   The old darkblue/perfan/chase_cool/chase_warm levels are retired
   (still previewable as patterns 1–4).
7. **2026-11-22 — GPU spotlight states (23/24/25) light ONLY 12/13.**
   User: "Only the two top LEDs should be on. All of the rest of the
   LEDs should be dark." These three render ONLY the 12/13 spotlight
   subject; RING10 2..11, 0/1, and dead 14..239 are pure black in all
   three. This REVERTS the RING10 wash the 2026-11-21 re-base added to
   them (they are the exception to "every state paints RING10").

## 4. Animation design rules

- **Liveness rule:** a slot a feature can show must be at least
  `BEACON_BG` in every frame (or the feature is off). Never a live
  latch at full black while the feature is "on".
- **Dead-latch rule:** moving features (heads, comets, tails) must only
  land on live latches (0..13); never paint dead latches 14..239.
- **Wrap rule:** advance `+1 index per frame` with `% len(ring)`;
  exactly one wrap point per period. Never a mid-ring dead gap (that's
  what made the old 8..15 ring blink).
- **Period rule:** a state's period must divide `PHASE_PERIOD = 120`
  (6 s at 20 FPS). Multi-ring states: combined period = LCM of the
  rings' periods, must also divide 120 (P6+P6 → 6, P6+P8 → 24, both OK).
- **Tails:** floor each tail color on the base with `_on_base()` so
  tail slot 3 `(0,0,0)` becomes `BEACON_BG`, not black.
- **Speeds:** 20 FPS; P6 = 0.3 s/rev, P8 = 0.4 s/rev, P10 = 0.5 s/rev. Half-speed demos:
  override param `{"slow": N}` (sweep) divides the phase.
- **`--check` is the gate:** any new/changed frame function gets
  assertions (head/tail positions per phase, period wrap, liveness on
  live latches, black on non-live latches). `--check` passes offline and
  must pass before daemon restart.

## 5. Current state of the 39 preview states

**ALL 39 preview states re-based 2026-11-21 to RING10 + TOPFANS**
(batch `ring10-20261121`, spec `ring10-rebase-spec.md`): ring = slots
2..11 (P10, 1 slot/frame, 1 rev = 6 s); TOPFANS 12/13 are painted EVERY
frame of EVERY state (never black; floor BEACON_BG (48,24,0)); slots
0/1 are dead (light nothing individually — tested 2026-11-22) and pure
black in every pattern; dead slots (0/1 and 14..239) never painted. The 2026-11-16 "skip 12/13" user decision is **VOID**
(reversed 2026-11-21 — see section 3).
**Exception (user 2026-11-22): states 23/24/25 light ONLY 12/13 —
RING10 2..11 is pure black in all three (the re-base ring wash is
removed; see section 3.7).** The old board ring 0..5 is
**RETIRED**: slots 2..5 are ring positions (2/4/5/6 o'clock); the
per-pattern board displays (counters/lamps/traces/companion dots) were
dropped or moved to 12/13; 0/1 unassigned/black. The legacy
LIVE_ONBOARD / SWEEP_FAN / SWEEP_ONBOARD constants were removed (final
task, 2026-11-21) — nothing references them.

| # | State | Status (all rows re-based 2026-11-21 to RING10 + TOPFANS) |
|---|-------|--------|
| 1 | `darkblue` | static dark blue (33,33,140) on RING10 2..11; 12/13 dim dark blue (48,24,70) |
| 2 | `perfan` | 6-color wheel recast on RING10 (colors repeat once over 10 slots, P10); 12/13 dim blue |
| 3 | `chase_cool` | three 2-slot blocks (blue/orange/yellow) chasing on RING10 P10 — old two rings merged into the single ring; 12/13 dim base + flare |
| 4 | `chase_warm` | same chase, warm palette (yellow/orange/red) |
| 5 | `beacon` | 2-slot yellow bulb + 1 fading tail over the steady orange ring, RING10 P10, head leading; 12/13 dim orange base + flare |
| **6** | **`sweep`** | THE TEMPLATE: 1-slot yellow head + 3-slot linear amber afterglow, P10 on RING10; 12/13 dim amber base + f-blend flare (f=1.0 head on 10, 0.5 on 9/11) |
| 7 | `tandem` | two 2-slot amber heads counter-rotating on RING10 P10 (one +1/frame, one -1/frame), 4-slot smoothstep tails; 12/13 amber base, f = max over both heads |
| 8 | `heartbeat` | breathing deep-orange ring on RING10 (60 \| 120, floor 30%) + 1-slot yellow lap dot P10; 12/13 breathe in phase |
| 9 | `comet` | 2-slot yellow head + smoothstep gradient tail filling RING10 (-i), P10; 12/13 dim base + flare |
| 10 | `hazard` | 2-slot head strobing 10 Hz (amber<->yellow) + blocky 4-slot orange tail on RING10 P10; 12/13 strobe in sync (floor = base, never black) |
| 11 | `bitclock` | 6-bit counter moved from board ring 0..5 to RING10 2..7 (ON (0,255,120) / OFF dim); lap flash on ring slots 8..11 (15f decay at the 120f wrap); 12/13 breathe in phase with the lap (base -> LAP, floored BEACON_BG) |
| 12 | `telegraph` | transmitter moved to ring slot 4 (5 o'clock): Morse "H" 4 dots on-15/off-15 (8 units = exactly 120f), warm white; echo = whole RING10 10 frames late at 35% warm white over dim BEACON_BG base; 12/13 strobe base <-> echo in sync |
| 13 | `fireflies` | four warm-amber flies drifting on RING10 (board-ring fifth fly retired); 12/13 breathe in phase to the fly core by the max envelope |
| 14 | `lava` | three additive blobs drifting across RING10 (center 6.5, amp 4.0; periods 120/60/40); board near-black ring retired; 12/13 motion rule base -> head (f = max over blobs) |
| 15 | `sonar` | ripples expand from RING10 center 6.5 (30f period, 0.85^d falloff); edge flash at 2/11; ping lamp moved to ring slots 6/7; board baseline + rate lamp dropped; 12/13 base + cyan flare (f = max over ripple points) |
| 16 | `metronome` | 60 BPM pendulum swings RING10, extrema at ring edges 2/11 every 20f; 3-bit tick counter dropped (board ring retired); downbeat = 3f white flash on marker + edges; 12/13 base + gold flare (f = marker position) |
| 17 | `orbit` | planet recentered: 2-slot core 6/7 + halo 4/5/8/9; satellite radii scaled x1.8 (outer reaches edge 2/11); onboard pass lamps dropped; 12/13 base + flare toward the planet's white (f = max over visible satellites) |
| 18 | `bitstream` | same 12-bit payload as 4-slot chunks scrolling left across RING10 (2.5 chunks visible, clipped at the ring; 1 chunk/10f = P10, 12 shifts per 120f seamless); start-bit lamp moved from onboard slot 5 -> TOPFANS 12/13 (5-on/5-off blink, floored BEACON_BG) |
| 19 | `sparkler` | bursts at pads 7/9/11 every 40f (3f near-white flash, 2f shock ring d=1..3, 4 sparks/side wrapping RING10); fuse countdown moved from onboard slot 5 -> 12/13 (tb 35..39); 12/13 flare toward white while a burst is live (f=1.0 burst@11, 0.5@9, 0@7) |
| 20 | `echo` | bounce 2<->11 in 20f (ghosts lag 10/5, widths 2.0/1.0); wall flash at ring edges 2/11 (3f); baseline BEACON_BG (old pure black gone); 12/13 motion-rule flare toward the white core (f from the nearest slot) |
| 21 | `ecg` | vital-signs trace scrolls RING10 (beat period 30f); board ECG_BASE dropped; rate lamp moved from onboard slot 4 -> 12/13 (2f white as the R peak crosses ring center 6/7) + motion-rule flare toward the white R peak (f=1.0 on 10, 0.5 on 9/11) |
| 22 | `walk` | creature walks 2<->11 (45/10/45/10 timeline, 9 slots/leg), body bob P=10, blink t=20..27; companion dot moved from onboard slot 3 -> 12/13 (blink in sync, floored) |
| 23 | `gpu-spotlight-idle` | **ONLY 12/13 lit** — very-slow blue→green→purple→blue spotlight subject (1 loop / 6 s); everything else pure black (user 2026-11-22 — the re-base ring wash is removed) — temp model level 0 |
| 24 | `gpu-spotlight-fire` | **ONLY 12/13 lit** — fast red→orange→yellow subject (1 loop / 0.75 s); everything else pure black (user 2026-11-22) — preview-only (level 3 is 26) |
| 25 | `gpu-spotlight-somewhat-active` | **ONLY 12/13 lit** — medium yellow→white→orange subject (1 loop / 1 s); everything else pure black (user 2026-11-22) — temp model level 1 |
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

**Temp/power drive model (user 2026-11-16, live in the service):**
level 0 (zone 1) = `gpu-spotlight-idle`; level 1 (zone 2) =
`gpu-spotlight-somewhat-active`; level 2 (zone 3) = `sweep` (pattern 6);
level 3 (zone 4) = `red_white` (pattern 26 — static: RING10 red, 12/13
white). Adjacent levels crossfade
smoothly per slot (`frame_colors` blends the four level frames).
Thresholds unchanged: level 0 = T ≤ 55 C and PWR ≤ ~20 W; level 1 =
T ≥ 60 C or PWR > 20 W; level 2 = T ≥ 65 C or PWR > 300 W; level 3 =
T ≥ 75 C (power alone never reaches level 3). The old A/B/C/D states
(darkblue/perfan/chase_cool/chase_warm, patterns 1–4) are no longer
temp-driven — re-based 2026-11-21 to RING10 + TOPFANS (12/13 painted,
board 0..5 retired) as preview-only, and still previewable.

## 6. Workflow: adding a feature or modifying an existing one

1. Read this doc; live slots 2..13 (0/1 light nothing individually —
   tested 2026-11-22). Standard ring = **slots 2..11**
   (o'clock 12, 1, 2, 4, 5, 6, 7, 8, 10, 11 — 3/9 unassigned);
   slots 12/13 = **top fans only, shine down on the GPU** (never part of
   the ring); slots 0/1 dead as individual latches (2026-11-22).
2. Edit `/home/link744/bin/gpu-rgb/gpu_rgb.py`: frame function(s) +
   `PREVIEW_STATES` + `--check` assertions for the new/changed state.
3. `python3 /home/link744/bin/gpu-rgb/gpu_rgb.py --check` → must pass.
4. `sudo -n systemctl restart gpu-rgb` (exempt service; restarts are
   always-approved — never block on it).
5. Demo live: write `.override` (e.g. `{"state": "sweep"}`, params
   optional) → daemon picks it up in 1 frame; user watches; **remove the
   override when done** (daemon returns to temp-driven).
   `mystic-preview N` cycles a pattern; `gpu_rgb.py preview X SECS`
   auto-clears.
6. Record the result: this doc, the `msi-mystic-light-leds` skill, and
   the 22-pattern catalog at
   `/home/link744/workspace/mystic_light/mystic-preview/README.md`.

## 7. Re-verification tools & open items

- `mystic-ledprobe table` — zone table.
- `mystic-ledprobe hold [ZONE:]INDEX|all [SECS]` — hold one address (or
  a whole zone) lit bright white, all else black.
- `mystic-ledprobe sweep [START END DWELL GAP]` — sweep the fan line.
- `mystic-ledprobe` (tty) — interactive picker: `1-7` zone, `space/→`
  next LED, `←` prev, `a` all/one toggle, `Esc` back, `l` table, `q`
  quit. Never run two instances at once.
- `mystic-ring-test [DWELL [GAP [REPS]]]`
  (`/home/link744/workspace/mystic_light/mystic-ring-test/`) — walks
  the standard ring (slots 2..11) with ONE bright LED, one o'clock at
  a time, slowly (override-driven, no sudo, auto-restores; `--check`
  offline). The 2026-11-21 re-probe tool.
- **Open items:**
  - Resolved 2026-11-22: onboard slots 0..1 probed — per-LED frame with
    slot 0 red / slot 1 green (rest black, 180 s hold), user confirmed
    no light: DEAD as individual latches. The onboard zone is live at
    zone level only (2026-11-16 `hold onboard all` showed the board
    ring); individually live addresses = 2..5 (ring positions 2/4/5/6
    o'clock).
  - **Resolved 2026-11-22 (user bug report): patterns 23/24/25 fixed to
    light ONLY slots 12/13** — the RING10 wash added by the 2026-11-21
    re-base is removed; RING10 2..11, 0/1, 14..239 pure black in all
    three (user: "Only the two top LEDs should be on"). `--check`
    asserts the ring is black every frame.
  - **Resolved 2026-11-21 (batch `ring10-20261121`): ALL 39 preview
    states re-based to RING10 (slots 2..11, P10) + TOPFANS (12/13).**
    12/13 are painted EVERY frame of EVERY state — the 2026-11-16
    "skip 12/13" user decision is VOID (user reversed it 2026-11-21);
    the board ring 0..5 is retired (0/1 unassigned/black); the legacy
    LIVE_ONBOARD / SWEEP_FAN / SWEEP_ONBOARD constants were removed
    (final task). Per-pattern status: section 5.
  - Slots 208..239 (`spare`) are *assumed* dead (ambiguous walk report).
  - Resolved 2026-11-17: patterns 14–22 re-based to the confirmed map
    (board 0..5 + fan 6..11, 12/13 skipped, dead 14..239 never painted).
    **Patterns 1–22 ALL run the confirmed slot map** (pattern 6
    re-based 2026-11-16; patterns 1–5 and 7–13 re-based 2026-11-17).
    The temp model does not use 1–5 (2026-11-16: levels 0–3 now drive
    the gpu-spotlight/sweep states).

## 8. File map

| Path | What |
|------|------|
| `/home/link744/bin/gpu-rgb/gpu_rgb.py` | daemon + all states + `--check` + preview CLI |
| `/home/link744/bin/mystic-ledprobe` | per-LED probe CLI (override-driven, read-only on HID) |
| `/home/link744/bin/mystic-preview` | 39-state cycler (override-only, no sudo) |
| `/home/link744/bin/gpu-rgb/.override` | live override (preview lab) |
| `/var/tmp/gpu_rgb.state` | daemon state file (temp, power, color, freshness) |
| `/home/link744/workspace/mystic_light/mystic-ledprobe/README.md` | probe walk artifact |
| `/home/link744/workspace/mystic_light/mystic-ring-test/mystic-ring-test` | standard-ring o'clock test (one LED, complete circle) — the 2026-11-21 re-probe |
| `/home/link744/workspace/mystic_light/mystic-preview/README.md` | 30-pattern catalog |
| skill `msi-mystic-light-leds` | operating notes (sudo exemption, pitfalls) |
