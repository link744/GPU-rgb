# RING10 re-base — canonical spec (2026-11-21)

USER DIRECTIVE (2026-11-21, after the confirmed fan-ring o'clock
re-probe): update ALL preview patterns to the new 10-slot ring
baseline, and EVERY pattern must also use the two top-fan-only slots
(12/13). This spec is binding for every task in the `ring10-20261121`
batch. It overrides any older instruction in frame-function docstrings
and the user-decisions section of msi-led-slot-map.md (the 2026-11-16
"skip 12/13" decision is VOID — the user reversed it 2026-11-21).

Target file: /home/link744/bin/gpu-rgb/gpu_rgb.py (~4200 lines; all
patterns in one file). Slot map doc (read for context):
/home/link744/workspace/mystic_light/msi-led-slot-map.md

## 1. The new baseline

- Standard ring = contiguous slots 2..11 — 10 positions, period P10
  (10 frames; 10 divides PHASE_PERIOD 120).
  O'clock labels (reference only, never in code):
  12->10, 1->11, 2->2, 4->3, 5->4, 6->5, 7->6, 8->7, 10->8, 11->9.
  3 and 9 o'clock have NO slot (dark gaps).
- Top fans = slots 12, 13: top case fans ONLY, shining down on the GPU.
  Every pattern paints them (rules below).
- Slots 0..1: live but UNASSIGNED — pure black in every pattern.
- Slots 14..239: dead — pure black in every pattern.
- The old board ring (LIVE_ONBOARD 0..5) is RETIRED: slots 2..5 are now
  ring positions (2/4/5/6 o'clock).
- The old fan ring (SWEEP_FAN 6..11) is a subset of RING10
  (7/8/10/11/12/1 o'clock).

## 2. Constants (foundation task T1 adds, near the LIVE LATCH MAP
comment block at ~line 190-203)

    RING10 = (2, 3, 4, 5, 6, 7, 8, 9, 10, 11)   # 10 slots
    TOPFANS = (12, 13)                           # top case fans (GPU)
    UNASSIGNED = (0, 1)                          # live, unassigned, black

T1 also rewrites the LIVE LATCH MAP comment block to the 2026-11-21
map. The old constants LIVE_ONBOARD / SWEEP_FAN / SWEEP_ONBOARD stay
until T11 removes them once nothing references them.

## 3. Per-pattern re-base rules (every pattern, no exceptions)

1. RING: the pattern's moving element(s) run on RING10, period P10
   (10 frames). Keep the pattern's character (colors, motion type,
   feel) — re-time it to the 10-slot ring. Wrap rule: advance +1 slot
   per frame, % 10, exactly one wrap point per period. Old period-6
   assertions (fn(0) == fn(6)) become fn(0) == fn(10) where the
   pattern is periodic with period 10.
2. TOPFANS (12/13) — painted EVERY frame of every pattern, never
   black. Pick the family from the pattern's docstring:
   - motion (head/comet/dot sweeping the ring): hold a dim base
     color of the family; when the primary moving element is on slot
     10 blend toward the family's head color with f=1.0, on slot 9
     or 11 with f=0.5, otherwise f=0:
       topfan_rgb = base + f * (head_rgb - base)   (per channel, int)
     Multi-element patterns: f = max over elements.
   - breathing: 12/13 breathe in the family color in phase with the
     ring breath (inverse if the pattern's character is inverse).
   - static: a static family color, >= floor.
   - subject (the spotlight/GPU family — gpu-spotlight-*, red_white,
     limelight, catwalk, supernova, inferno, reactor-core, matrix,
     cyberpunk, cryogenic, emergency-mode, lightning-strike,
     plasma-storm): 12/13 STAY the bright subject; RING10 carries
     the supporting motion/wash dimmer than 12/13 so the spotlight
     character survives.
   - Strobe patterns (hazard, emergency-mode, lightning-strike):
     12/13 strobe BETWEEN base and bright — the floor is the base
     color, never black.
   Floor = BEACON_BG (48, 24,0) (line 562).
3. Old board ring (0..5) content: DROP it — 0..1 go black. If the
   pattern's secondary display (counts/lamps/traces) no longer fits,
   drop the display; the pattern's character must survive on RING10
   + TOPFANS.
4. SEAM: seamless at PHASE_PERIOD=120. Multi-element: LCM of element
   periods must divide 120.
5. CHECK: update the pattern's assertion block inside the big check
   function (starts ~line 2900; one commented block per pattern):
   - every checked frame: slots 2..11 all >= floor (no pure black);
   - every checked frame: slots 12,13 >= (48,24,0) — DELETE old
     asserts that 12/13 are black ("skipped");
   - every checked frame: slots 0,1 == (0,0,0);
   - every checked frame: slots 14..239 == (0,0,0);
   - motion asserts re-derived for the 10-slot ring geometry.
   Keep the per-state comment header style; note "re-based 2026-11-21
   to RING10 + TOPFANS".

## 4. Reference template

Pattern 6 (sweep) is re-based FIRST in T1 and is the canonical
template. Before touching any other pattern, read sweep_frame (~line
574) + its check block and copy its RING10 + TOPFANS structure.

## 5. Per-pattern list (all 39 preview states)

T1  sweep — THE TEMPLATE: yellow head + 3 amber tail, P10 on RING10;
    TOPFANS dim sweep base + f-blend flare on slot 10.
T2  darkblue — static: 2..11 DARK_BLUE; 12/13 dim dark blue.
    perfan — 6-color ring: recast on RING10 (10 slots, colors repeat,
    P10); 12/13 dim blue.
    chase_cool / chase_warm — 3-color comet: recast on RING10 P10
    (old "both rings" merges into the single ring); 12/13 dim base +
    flare.
    beacon — 2-slot bulb + 1 tail on orange base: recast on RING10
    P10; 12/13 dim orange base + flare. (Its check block currently
    asserts 12/13 black — must flip.)
T3  tandem — two 2-slot amber heads counter-rotating: both on RING10
    P10 (one +1/frame, one -1/frame); 12/13 amber base, f = max
    over both heads.
    heartbeat — breathing ring + 1 yellow lap dot: breath on RING10
    + lap dot P10; 12/13 breathe in phase.
    comet — 2-slot head + smoothstep tail: RING10 P10; 12/13 dim
    base + flare.
    hazard — 10 Hz strobing 2-slot head + 4 tail: RING10 P10; 12/13
    strobe in sync (floor = base, never black).
T4-T6  bitclock, telegraph, fireflies, lava, sonar, metronome, orbit,
    bitstream, sparkler, echo, ecg, walk — creative family: read each
    frame function's docstring; keep its character; recast its moving
    element(s) onto RING10 P10; drop the old board-ring display (0/1
    black); 12/13 per family (motion => base + flare; breathing =>
    in-phase; static => static color).
T7  gpu-spotlight-idle / -fire / -somewhat-active — 12/13 stay the
    very-slow spotlight subject; RING10 adds a VERY DIM wash in the
    same family (>= floor); 0/1 black. (These are temp-driven levels
    0/1 — the idle look must stay mostly the GPU spotlight.)
    red_white — RING10 (2..11) RED; 12/13 WHITE (as today).
T8  rainbow — 6-color wheel on RING10 (6-color sequence over 10
    slots, slide 1 slot per 20 frames; must be seamless at the 120f
    phase — verify); board mirror dropped (0/1 black); 12/13 keep the
    one-color-at-a-time cycle (12f hold + 8f fade).
    limelight — 12/13 star stays; RING10 inverse breathe; 0/1 black.
    catwalk — runway on RING10; photographers 12/13 kept; audience
    (old board ring) dropped, 0/1 black.
    supernova — 12/13 STAR stays; shockwave on RING10; 0/1 black.
T9  inferno — 12/13 flame (subject) + RING10 hot cycle; 0/1 black.
    reactor-core — 12/13 energy (subject) + RING10 pulse; 0/1 black.
    matrix — green columns + white flashes: columns on RING10; 12/13
    dim green base + flash sync.
    cyberpunk — purple/cyan glitch: RING10 glitch; 12/13 dim base +
    flare on bursts.
    cryogenic — ice-blue frost: RING10 frost; 12/13 dim ice base +
    flare.
T10 emergency-mode — red/amber 24 Hz: RING10 strobe; 12/13 strobe in
    sync (floor = base).
    lightning-strike — white 40 Hz strobe + cyan wash: RING10 strobe
    + cyan wash; 12/13 flare on strikes.
    sweeping-rainbow — full rainbow sweep: RING10 sweep; 12/13 track
    the head color (dim).
    plasma-storm — multi-color plasma: RING10 plasma; 12/13 dim base
    + flare on bursts.
T11 finalize — see its task body (no patterns).

## 6. Done criteria (per pattern task)

1. `python3 /home/link744/bin/gpu-rgb/gpu_rgb.py --check` passes.
2. `sudo -n systemctl restart gpu-rgb` — ALWAYS approved for this
   project (the gpu-rgb unit is on the sudoers allowlist; no prompt).
   The daemon holds the OLD code until restarted.
3. Demo: `printf '{"state": "<name>"}' > /home/link744/bin/gpu-rgb/.override`
   (that exact JSON — read_override reads the "state" key), sleep 5,
   `rm` it, then `python3 /home/link744/bin/gpu-rgb/gpu_rgb.py status`
   shows the override gone and a live state. The daemon re-reads the
   override every frame — no daemon involvement needed to demo.
4. Progress note (what changed per pattern).

NEVER touch patterns outside your task's list. NEVER remove the old
constants (LIVE_ONBOARD/SWEEP_FAN/SWEEP_ONBOARD) — only T11 does that.
NEVER run two probe/preview/override writers at once.
