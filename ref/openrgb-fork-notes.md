# OpenRGB fork for this board — per-LED / GPU-temp drive mode

Fork of OpenRGB **release_1.1** adding a "GPU Temp" drive mode to the
MSIMotherboard185 driver (same drive model as gpu_rgb.py / gpu-rgb.ps1).
Deliverables live in `~/workspace/Kanban/openrgb-llamacpp-20261110/`
(NOTE.md there is the current pointer); source checkout:
`~/.hermes/cache/scratch/openrgb-src` (branch release_1.1, fork branch on
the MSI 185 controller under `Controllers/MSIMotherboardController/`).

## Core constraint: stock OpenRGB CANNOT do per-LED on this board
- The driver's per-LED path is gated on a tested-board table
  (`mystic_light_185_config` in MSIMotherboard185Controller.cpp). This
  board's id is not in the table → fallback `per_led_mode =
  DIRECT_MODE_DISABLED`.
- Consequence: `SetLedColor()` and the `Update()` per-LED send are BOTH
  guarded by `per_led_mode == DIRECT_MODE_PER_LED` → silent no-ops here.
  Code that rides the per-LED path compiles clean, "works", and lights
  nothing. Zone-mode effects (Static, Rainbow Wave, …) work fine (185B
  0x52).
- `SetDirectMode(true)` also sends nothing on this board (its enable send
  is the same guard) — don't assume calling it activates per-LED.
- Therefore the fork's per-LED writes go RAW via `hid_send_feature_report`,
  same protocol as gpu_rgb.py (packet layout: references/per-led.md):
  1. GET report 0x52 → captured board state.
  2. 185B 0x52 enable built from the capture; re-sent every ~60 s.
  3. 725B 0x53 per-LED frame at 20 FPS, EVERY tick (no dedup — USB
     autosuspend; see the autosuspend pitfall in SKILL.md).
  4. Stop: static dark-blue restore frame.
- Single writer: while the fork's GPU mode is active, OpenRGB's own device
  thread must NOT write (early-return in `DeviceUpdateLEDs`) — the engine
  thread is the sole writer.

## OpenRGB 1.1 C++ type facts (where "compiles in my head" fails)
- `RGBColor` is `typedef unsigned int RGBColor` — a PACKED int, not a
  struct with R/G/B members. Build: `ToRGBColor(r, g, b)`
  (=(b<<16)|(g<<8)|r); unpack: `RGBGetRValue/G/B` — all in
  `RGBController/RGBControllerInterface.h`.
- `zone` (class in RGBControllerInterface.h): `led* leds`, `RGBColor*`
  colors` — RAW POINTERS (allocated per zone; guard nullptr), plus
  `unsigned int leds_count`. No vectors, no resize. `led` has `int value`
  holding the zone type (MSI_ZONE_*).
- `RGBController`: `std::vector<zone> zones`, `std::shared_mutex
  AccessMutex`, `UpdateLEDs()`, `DeviceUpdateLEDs()`; active mode =
  `modes[i].value` (MSI_MODE_*).
- `MSIMotherboard185Controller` (device layer): `hid_device* dev`;
  raw send `hid_send_feature_report(dev, buf, len)`;
  725B per-LED packet = `FeaturePacket_PerLED_185` (report_id 0x53,
  hdr 0x25 0x06 0x00 0x00, then 240 × R/G/B) in MSIMotherboardCommon.h.
- New mode: add an MSI_MODE_* constant in MSIMotherboardCommon.h AND
  register it in the driver's supported-mode table — the enum constant
  alone never appears in the dropdown.

## Verification recipe (no MSVC on this Linux box — Windows-only app)
1. Sparse-checkout release_1.1 (the checkout above has the controller
   tree).
2. Syntax-check the new function in a standalone harness: MIRROR THE REAL
   TYPES VERBATIM from RGBControllerInterface.h / RGBController.h (RGBColor
   = unsigned int, pointer-based zone, `led.value`, shared_mutex) — never
   hand-roll. A harness with a hand-rolled `class RGBColor {R,G,B}` and
   vector colors PASSES while the real code is wrong: it validates your
   assumptions, not the code. (This exact failure happened: first
   harness "passed", real types exposed the bug.)
3. `g++ -std=c++17 -fsyntax-only -Wall` on the harness; use
   `std::chrono::milliseconds(n)` in harness code (g++ strictness) — the
   portable form is safe in the fork too (MSVC accepts both).
4. Fork branch on release_1.1; `git diff base..branch > patch`; verify
   `git apply --check` on a PRISTINE clone — the patch is the user-facing
   deliverable (they apply it on their Windows side).
5. Report verification status honestly: type-checked against real
   headers; not MSVC-built, not board-run (that's the user's Windows
   build; a build error there is a one-line fix, report it back).

## In-flight state (resume point)
- Engine `MSIGpuDriveEngine.h/.cpp` (self-contained: pure-function model
  mirroring the .ps1 + `SelfTest()` + callback-driven Run loop) is in the
  kanban task's workspace; controller raw passthroughs
  `SendRawFeature`/`ReadRawState185` are in place.
- Remaining: RGB-controller mode wiring (new MSI_MODE_* + mode-table
  entry, start/stop the engine thread, single-writer early-return),
  `SelfTest()` pass, package deliverables into the batch folder.
