# MSI Mystic Light 185 protocol — zones, per-LED mode, addressability

Source: OpenRGB `MSIMotherboard185Controller` (read from source) + live
verification on this machine. Device `/dev/hidraw0` (0db0:0076).
Reports: 0x52 (185B zone config), 0x53 (725B per-LED colors).

## Zone static mode (rgb_ctl.sh / daemon restore path)
- Two-report volatile write: GET 0x52 → SET(current, save_data=0) →
  SET(new, save_data=0). Never persists, never flashes.
- 0x52 layout: `[0]=0x52`; ZoneData (10B) at zone offset =
  effect, R, G, B, speedAndBrightnessFlags, R2, G2, B2, colorFlags, pad.
  effect 0x01 = static; flags 0x28 = brightness 100%; colorFlags 0x80 =
  custom color (on_board_led_0 uses 0x81).
- Zone offsets: j_rgb_1=1 j_pipe_1=11 j_pipe_2=21 j_rainbow_1=31
  j_rainbow_2=42 j_corsair=53 j_corsair_outer=64 on_board_led_0=74
  on_board_led_1..5=84..124 on_board_led_6..9=134..164 j_rgb_2=174
  save_data=184.
- j_rainbow_1/2 are RainbowZoneData (11B; `cycle_or_led_num` at +10).
  j_corsair is CorsairZoneData: effect@0, color@1-3, fan_flags@4,
  quantity@5, padding[4]@6-9, is_individual@10.
- One color per zone (13 zones) — this is all rgb_ctl.sh does.

## Per-LED direct mode (per_led_test.py)
Per-LED colors exist for the j_rainbow_*/j_corsair lines + onboard LEDs.
NOT for j_rgb/j_pipe headers (1 LED each — zone-level only).

Sync mode (OpenRGB's default path; auto-selected when the board's
cycle_or_led_num ≤ 40/40/120):
1. SET the 0x52 enable message (single write): captured state + template —
   j_rainbow_1/2: effect 0x01 + led_num 40; j_corsair: effect 0x01 +
   is_individual 120; on_board_led: effect 0x25, flags 0xA9,
   colorFlags 0xBF (FULL_SYNC); j_rgb_2 flags 0x2A; on_board_led_1..9
   flags 0x28; j_rgb_1 flags 0x08.
2. SET the 0x53 per-LED packet (725B): `[0]=0x53`, `[1..4]=25 06 00 00`,
   then 240 RGB slots: [0..5] onboard, [6] jrgb1, [7] jrgb2,
   [8..47] j_rainbow_1 (40), [48..87] j_rainbow_2 (40), [88..207]
   j_corsair (120).
3. Restore: two-report write of the captured 0x52 state.

Full mode (needed past 40 LEDs on one line): enable msg instead sets
j_rainbow_1 effect 0x25 led_num 200, j_rainbow_2 0x25/240, j_corsair
0x25/240, on_board colorFlags 0xB1 (BASIC sync); then FOUR 0x53 packets
(13ms apart): jrainbow1 `25 04 00 00`, jrainbow2 `25 04 01 00`,
jcorsair `25 05 00 00`, onboard_and_sync `25 06 00 00`.

Live findings (this machine):
- Firmware ACCEPTS the sync-mode enable (j_rainbow_1 led_num read back 40)
  and normalizes on_board colorFlags 0xBF → 0x87, dropping sync flags for
  zones with nothing connected — the read-back flags double as a
  connected-device diagnostic.
- CONFIRMED WIRING (per-fan test, user-observed): all 8 fans show the
  IDENTICAL multi-color pattern in both per-fan and rainbow windows →
  multi-LED fans on one parallel data line. Per-fan individual control is
  impossible on this machine; the per-LED slots map to LEDs around each
  fan ring, not to individual fans.
- GET 0x53 is unsupported (device returns the 0x52 report) — per-LED state
  is not readable; effects verify visually only.

## Addressability decision table
| Want | Path |
|---|---|
| One color on all headers | zone static (rgb_ctl.sh / gpu-rgb daemon) |
| Different color per header | per-zone static — 13 independent colors in one 0x52 write (extend build_packet to take per-zone colors) |
| Different color per fan/LED on a j_rainbow line | per-LED mode; one slot per latching node (daisy-chain fans/LED strips) |
| Impossible | fans wired in PARALLEL on one header (all latch the first slot's color); j_rgb/j_pipe headers (1 LED each) |

Wiring checks: `zone_map.py` shows which zone a physical light is on.
Per-fan check: `per_led_test.py --both` — if all 8 fans show ONLY slot 8's
color, they're parallel-wired → not addressable; only rewiring fans across
separate headers (the board has 7) or an external controller gets
individual control. If each fan shows its own color (or a hue segment),
it's a daisy-chain and per-fan (even per-LED) control works.

## LIVE LATCH MAP (this board, verified 2026-11-15) — visibility rules
Only 14 of the 48 "ring" slots light a real LED:
- onboard ring: slots 0..5 (6 live latches; on_board_led_0..5)
- fan ring: slots 8..15 (8 live latches; the 8 fans share one parallel
  data line, so each slot is a RING POSITION around every fan at once —
  a dot at slot 8 shows on all 8 fans simultaneously, in phase)
- DEAD: slots 6..7 (empty j_rgb headers) and 16..47 (j_rainbow_1
  latches with nothing wired) — anything painted there is invisible.
Design rules for moving patterns (the "29% visibility" bug class):
- NEVER let a moving bright element tour a range that includes dead slots
  (old 48-slot ring = 71% dead → ~29% of the motion visible).
- One head PER ring when two rings are involved: a single touring dot
  leaves the other ring dark while it crosses (reads as a pause).
- Spins stay on their own ring: fan ring period 8 (0.4 s/rev at 20 FPS),
  onboard ring period 6 (0.3 s/rev); combined period 24 | 120.
- Lit spot < 8-LED fan window: 1-4 slot dots read as spin; full-width
  dots read as pulse. Small head + short fading tail = radar feel.
- No black frames: steady dim base under the motion, or a breathing ring
  with a brightness floor (~0.3).
- No off-canvas windows: continuous round trips (walk→look→walk back)
  instead of "exits and re-enters on the wrap".
- Periods must divide PHASE_PERIOD = 120 for a bit-identical seam.
- Dead latches stay (0,0,0) — `--check` asserts this for every state.

## Pitfalls
- Python: bytearray slicing returns a copy — build packets by indexing the
 parent buffer (`pkt[5 + 3*i] = r`).
- ioctl via `fcntl.ioctl` (no struct.ioctl).
- Capture-and-restore around experimental writes; keep the daemon's last
  color consistent (`gpu_rgb.py status` format: `set color: blue  (RGB 0 0 255)`).
- The daemon animates (20 Hz, per-LED sync mode is its NORMAL operating
  mode, entered via the enable message at startup): it is the one writer.
  Stop it before zone_map/per_led_test/rgb_ctl experiments, or use
  `gpu_rgb.py preview` (override file, safe while running).

## Accessing the device from Windows (PowerShell port)
Same protocol, Windows HID API — feature reports via DeviceIoControl,
not input/output reports.
- Device name: prefix `\\.\` (two backslashes, dot, one backslash —
  literal in PowerShell) + the instance path, where instance path = WMI
  `Win32_PnPEntity.DeviceID` matching `HID\VID_0DB0&PID_0076*` (fallback:
  Name like `*MYSTIC LIGHT*`). CreateFileW with GENERIC_READ|GENERIC_WRITE,
  share READ|WRITE|DELETE, OPEN_EXISTING.
- Reports are SET/GET FEATURE: build HID_IO_CONTROL (x64, 44-byte
  header): [0..27] = `HidIOControl` + NUL as UTF-16, [28..31] =
  IoControlCode, [32..35] = OutputBufferLength, [36..43] = pointer to the
  report data (pin one buffer with GCHandle; data at offset 44;
  DeviceIoControl in-buffer = the whole pinned block).
  IOCTL_HID_SET_FEATURE = 0x00220008, IOCTL_HID_GET_FEATURE = 0x0022000C.
  The 185B 0x52 / 725B 0x53 packets are byte-identical to the Linux
  hidraw writes.
- `nvidia-smi --query-gpu=temperature.gpu,power.draw
  --format=csv,noheader` works on Windows too (nvidia-smi.exe ships with
  the driver).
- If MSI Center / Mystic Light is running it may hold the device: open
  with full share flags; on access denied, close MSI Center.
- Verify the HID path with a GET 0x52 read-back (185B) before any SET.
