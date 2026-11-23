# gpu-rgb — Windows MSI Mystic Light controller

Windows port of the Linux `gpu-rgb` systemd daemon (source of truth:
`ref\gpu_rgb.py`) for the MSI **PRO B650-VC WIFI III** (MS-7E18) motherboard's
Mystic Light LEDs — 10 addressable fan-ring LEDs (slots 2..11) + 2 top-case-fan
LEDs (slots 12/13) — plus two extensions: user-reassignable drive levels and
screen-color mode.

Single C# file pair (`src\GpuRgb.cs` + `src\Patterns.cs`), compiled to one
`GpuRgb.exe` with the Windows-shipped csc (.NET Framework 4, **C# 5 only**).
All LED writes are volatile (`save_data=0`); the board is restored on every
clean exit.

> Note: PROMPT.md calls the machine an "Aegis ZS2 laptop"; the actual board
> (per OpenRGB detection + USB serial 7E18…) is the MSI PRO B650-VC WIFI III
> desktop motherboard. Behavior is identical — same HID protocol, same slot map.

---

## What computers does this run on?

**Short answer: yes — this box. It is board-specific, not universal.**

Built and verified on the **MSI PRO B650-VC WIFI III (MS-7E18)** desktop
board (this machine), which uses the MSI **"Mystic Light" USB HID
controller** — USB device `VID_0DB0&PID_0076`. This is **not** a generic GPU
lighting tool; it talks to that specific controller and its LED wiring.

It needs **all** of:

1. **The MSI Mystic Light USB HID controller** (`VID_0DB0&PID_0076`) with the
   same LED layout as this board — **10 addressable fan-ring LEDs (packet
   slots 2..11, "RING10") + 2 top-case-fan LEDs (slots 12/13)**. Live latches
   are slots 2..13; slots 0/1 and 14..239 are dead. A board with a different
   header layout will light differently (or not at all).
2. **An NVIDIA GPU with `nvidia-smi`** — temperature + power come from
   `nvidia-smi` (same `--query-gpu` call as the Linux daemon).
3. **Windows 10/11 (x64)** with built-in .NET Framework 4 (`csc.exe`) to
   build. Note: this build (Win11 26300) rejects the old raw `HID_IO_CONTROL`
   form, so the app uses the `hid.dll` `HidD_Get/SetFeature` path.

So it should also work on other MSI boxes that ship the same `0db0:0076`
Mystic Light controller with the same 10-LED ring + 2 top-fan wiring; it will
**not** light up an arbitrary PC.

---

## Quick start

```bat
cd C:\Users\linke\Desktop\gpu-rgb-app
build.bat                 :: compile GpuRgb.exe (BUILD OK)
GpuRgb.exe serve          :: run the daemon (temp/power drives the lights)
GpuRgb.exe status         :: another terminal: live state
GpuRgb.exe stop           :: graceful stop (restores the board)
```

**Autostart at logon** is installed as a Startup-folder shortcut
(`%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\GpuRgb.lnk` →
`GpuRgb.exe serve`, minimized). `schtasks /Create /SC ONLOGON` was attempted
first but requires elevation on this box (Access Denied), so the PROMPT's
documented fallback is used.

- Uninstall autostart: delete `GpuRgb.lnk` from the Startup folder.
- If you later want the Scheduled Task instead (elevated terminal):
  `schtasks /Create /TN GpuRgb /TR "\"C:\Users\linke\Desktop\gpu-rgb-app\GpuRgb.exe\" serve" /SC ONLOGON /RL LIMITED /F`
  then delete the Startup shortcut. Uninstall: `schtasks /Delete /TN GpuRgb /F`.

**Do not run MSI Center or OpenRGB while gpu-rgb runs** — one writer only.
A second gpu-rgb instance refuses to open the device (single-writer mutex).

---

## Subcommands

| Command | What it does |
|---|---|
| `serve [secs]` | The daemon. GPU temp/power drives the lights (Linux parity) or screen color (`mode:"screen"`). Config hot-reload ~1 s. Writes every frame @ 20 FPS, re-sends the per-LED enable every 60 s, logs state ~30 s-throttled, restores the board on exit. Default: until Ctrl+C or `stop`. Optional bounded run for tests. |
| `stop` | Signals a running `serve` to exit gracefully (named event `GpuRgbStopSignal`); the daemon restores the board. |
| `status` | Daemon RUNNING/STALE (from `status.json`, 5 s cadence), device path, mode, level + active pattern, raw/EMA temp & power, weights, screen color + capture CPU, config, autostart state. |
| `probe` | Opens the device, GET 0x52, prints the 185-byte zone table; saves a hex-dump evidence file. Read-only. |
| `hold <slot> <R,G,B> [secs]` | One slot lit, everything else black (default 10 s). Slot map below. Restores on exit. |
| `restore` | Exits per-LED mode: two-report write of the captured 0x52 state (`save_data=0`). |
| `selftest` | No-eyes verification of the write path: enable → GET 0x52 must reflect the enable template → restore → GET must equal the capture byte-for-byte. |
| `preview <name\|N> [secs]` | Render one pattern (default 15 s). Numbering = Linux mystic-preview catalog 1–32. |
| `cycle [N]` | All 32 patterns in catalog order, 10 s each + 1 s pure-black gap, then exit. With N: that pattern until Ctrl+C. |
| `off [secs]` | Pure black on all 240 slots, preview-only (default 15 s). |
| `stats [secs]` | nvidia-smi temp/power every 5 s, EMA α=0.25, derived level; prints raw + EMA + level + weights. Default: until Ctrl+C. No device writes. |
captured primary-monitor average RGB + hex, continuously (capture thread ~5 Hz, print 2 Hz). No device writes.
| `assign <level 0-3> <name\|N>` | Re-point a drive level to any pattern; writes `config.json`. A running daemon applies it within ~1 s. Unknown names/numbers **fail loudly**. |
| `--check` | Offline validation (no device): packet builders, drive model, all 32 patterns × 120 phases **byte-exact vs the Linux reference** (`tests\groundtruth.bin`), 7 crossfade levels × 120 phases byte-exact vs Linux `frame_colors` (`tests\driveframes.bin`), screen-frame invariants, registry order, fail-loud lookup. Output tee'd to `evidence\p2-check-*.txt`. |

Ctrl+C in any rendering command stops the loop and restores the board.

---

## Config file (`config.json`, project dir)

```json
{"mode":"gpu","levels":{"0":"gpu-spotlight-idle","1":"gpu-spotlight-somewhat-active","2":"sweep","3":"red_white"}}
```

- `levels` maps drive level 0–3 to any of the 32 patterns (name or number via
  `assign`). Missing file = the defaults above (Linux parity).
- `mode`: `"gpu"` (temp/power drive model) or `"screen"` (lights follow the
  captured screen color). Edit the file to switch — **hot-reloaded within ~1 s**,
  no restart, no re-entering per-LED mode. Invalid edits are rejected loudly
  (error logged, previous config kept).
- Workflow: `preview N` (or `cycle`) → see one you like →
  `assign 2 27` → level 2 now renders `rainbow` immediately, and the assignment
  survives restarts (it's on disk).

## Drive model (Linux parity, `ref\gpu_rgb.py`)

level = max(temp level, power level); thresholds smoothstep-blended (no hard
transitions); adjacent levels crossfade **per slot** exactly like `frame_colors`.

| Level | Condition | Default pattern |
|---|---|---|
| 0 | T ≤ 55 °C and PWR ≤ ~20 W (blend 15–25 W) | 23 `gpu-spotlight-idle` |
| 1 | T ≥ 60 °C or PWR > 20 W | 25 `gpu-spotlight-somewhat-active` |
| 2 | T ≥ 65 °C or PWR > 300 W (blend 290–310 W) | 6 `sweep` |
| 3 | T ≥ 75 °C (**power never drives level 3**) | 26 `red_white` |

Sensors: `nvidia-smi --query-gpu=temperature.gpu,power.draw --format=csv,noheader`
every 5 s; EMA α = 0.25 (first sample seeds; failed polls keep the last value;
pre-first-sample fallbacks 55 °C / 0 W — all exactly the Linux `cmd_serve`
semantics).

## Screen-color mode (Phases 6–7)

- "Screen color" = the **average of a 32×32 StretchBlt downscale of the primary
  monitor** (GDI BitBlt, 32-bit DIB) — documented choice per spec; dominant-color
  histogram is a stretch goal, not implemented.
- Capture runs on its own thread at ~5 Hz in `serve` (both modes) and in
  `screen`; the render side lerps toward the target at 20 FPS (0.1/frame).
- `mode:"screen"` paints the **live set 0..13** the captured color; dead
  14..239 stay black. Mode switch hot-reloads; clean exit still restores.
- CPU cost of the capture thread is measured (`GetThreadTimes`) and reported
  in `status` / `status.json` (`capture_cpu_pct`).
- **Locked workstation:** Windows denies screen-DC BitBlt while locked
  (StretchBlt Win32 error 5). The app detects the stall, logs it once, holds
  the last color, and resumes automatically on unlock. `screen` prints
  `[STALLED - workstation locked?]`.

## Slot map (authoritative: `ref\msi-led-slot-map.md`)

- **Live latches = slots 2..13.** Slots 0/1 are individually DEAD (probed
  2026-11-22 — light nothing) and are never painted; 14..239 are DEAD — never
  painted.
- **Standard ring = slots 2..11 (RING10, 10 positions, period P10).** O'clock
  in clockwise walk order: 12 (slot 10) → 1 (11) → 2 (2) → 4 (3) → 5 (4) →
  6 (5) → 7 (6) → 8 (7) → 10 (8) → 11 (9) → back to 12. **3 and 9 o'clock are
  unassigned** — the ring's two dark gaps. The ring spans firmware zones
  (2..5 onboard, 6 j_rgb_1, 7 j_rgb_2, 8..11 = j_rainbow_1 idx 0..3); address
  by PACKET SLOT, never by zone. O'clock re-probed 2026-11-21.
- **12/13 = top case fans only** (j_rainbow_1 idx 4/5), shining down on the
  GPU — NOT ring positions. Painted EVERY frame of EVERY pattern (never black;
  floor `BEACON_BG=(48,24,0)`), except the exception below.
- **Exception (user 2026-11-22):** patterns 23/24/25 (gpu-spotlight) light
  ONLY 12/13 — ring 2..11 is pure black in all three.
- Patterns 1–32 = `ref\mystic-preview-catalog.md` numbering (same numbers as
  Linux `mystic-preview`). Beacon family renders over `BEACON_BG=(48,24,0)`.
- All 32 patterns run this model (re-based 2026-11-22/23; see Verification
  status below).

## HID transport (important host-specific fact)

Feature reports go through **hid.dll `HidD_GetFeature`/`HidD_SetFeature`**
(the API OpenRGB/hidapi use). The old raw `HID_IO_CONTROL` DeviceIoControl
form (from `gpu-rgb-windows\gpu-rgb.ps1`) **fails host-wide on this Windows 11
build (10.0.26300) with Win32 error 1 on every HID device — including the live
internal keyboard**. That failure was misdiagnosed as a "device ghost" on
2026-11-19 and caused a long loop; see `evidence\pathdiag-2026-11-20.txt`.
Device discovery: CM interface list first, PnP-registry-built
`\\?\HID#VID_0DB0&PID_0076#<instance>#{HID-guid}` fallback (the CM list is
unreliable on this host). Packets are byte-identical to the Linux hidraw
writes (185B 0x52 enable / 725B 0x53 frames).

Hard rules inherited from Linux: write EVERY frame even when unchanged;
re-send the 185B enable every 60 s (USB autosuspend otherwise drops per-LED
mode → stuck white); `save_data=0` always; two-report restore on exit.

## Logging & state

- `logs\gpurgb-YYYY-MM-DD.log` — timestamped; state lines throttled ~30 s
  (like the Linux journal), discrete events (start/stop, config reload,
  assign, errors) immediate.
- `status.json` — daemon writes every 5 s (atomic-ish tmp+copy): pid, mode,
  level, pattern, weights, raw/EMA temp/power, levels config, screen color,
  capture CPU/stall, device path. `status` reads it (fresh < 15 s = RUNNING).

## Add-a-pattern workflow (mirrors `ref\msi-led-slot-map.md` §6)

1. Edit/add the frame function in `src\Patterns.cs` — pure `byte[] Fn(int t)`,
   t ∈ [0,120), 720 bytes; paint RING10 (2..11) + TOPFANS (12/13 — every frame,
   never black, floor `BEACON_BG`) only; 0/1 + 14..239 pure black; liveness
   floor on live latches, period divides 120, exactly one wrap/period,
   +1 index/frame `% 10`.
2. Register it in `Patterns.All` (number + name + base RGB). For a numbered
   catalog pattern, keep Linux numbering; extend `tests\groundtruth.*` from the
   Linux reference if the pattern exists there.
3. `build.bat` → `GpuRgb.exe --check` must pass (byte-exact where ground truth
   exists; structural invariants otherwise).
4. Live demo: `GpuRgb.exe preview <name|N>` — confirm visually.
5. Document it (catalog row + this README if user-facing).

## Verification status (2026-11-23)

| Phase | Software evidence | Visual sign-off |
|---|---|---|
| P1 control | probe 185B table; selftest PASS (write path + exact restore) | **Doug confirmed** (red position on all 8 fans; blue return) |
| P2 patterns | 3840/3840 frames byte-exact vs Linux; parent-rebuilt + re-checked | **Doug confirmed** all 8 spot-checks "perfect" |
| P3 stats | raw == manual nvidia-smi; ComfyUI SDXL load: 316 W peak, 28→68 °C, levels 0→1→2 | n/a (agent-generated load; Doug asleep) |
| P4 drive | 840 crossfade frames byte-exact; live serve under load: level 1.48 crossfade observed; mutex rejects 2nd instance | pending Doug (level patterns 23/6/26 confirmed in P2; 25 byte-identical) |
| P5 assign | assign→reload ~1 s (log timestamps); revert; persists restart; fail-loud asserted | pending Doug (rainbow confirmed in P2) |
| P6 screen | live solid-color test PASS: red/blue/magenta deviation 0; CPU median 3.1/2.3/1.6% (<5%) at 5 Hz | **Doug confirmed** |
| P7 screen mode | live PASS: lights followed blue [0,0,255] / magenta [254,0,254]; gpu mode resumed; lifecycle PASS | **Doug confirmed** |
| R1 RING10 rebase | all 32 patterns × 120 phases = 3840 frames byte-exact vs regenerated `tests\` (new ref); 840 crossfade frames byte-exact; selftest PASS 2026-11-23 (write path + exact restore); parent-rebuilt + re-checked after every kanban task; tests tamper-checked (SHA-256) | **Doug confirmed** all 8 spot-checks (1, 6, 22, 23, 26, 27, 28, 30) "all look great" (2026-11-23) |

Evidence files: `evidence\` (probe, selftest, pathdiag, p2 checks, p3 stats +
load tests, p4 serve console, p6/p7 test notes, autostart lifecycle,
RING10 rebase checks + spot-check).
Kanban: `hermes kanban --board gpu-rgb list` (7 phases + RING10 rebase chain,
12 tasks).

## Troubleshooting

- **"another gpu-rgb instance holds the device"** — a serve/preview is already
  running; `GpuRgb.exe stop` (or close it). Single writer by design.
- **Win32 error 5 on open** — another writer (MSI Center / OpenRGB) holds the
  device; close it. Error 5 on *StretchBlt* = workstation locked (screen mode
  holds last color until unlock).
- **Lights stuck white / frozen** — per-LED mode was dropped (write gap /
  autosuspend). The daemon prevents this by writing every frame + 60 s enable
  refresh; run `GpuRgb.exe restore` to reset the board.
- **Unknown pattern name** — fails loudly with the valid range; nothing is
  silently ignored (the Linux whitelist pitfall is designed out).
- Rebuild after edits: `build.bat` (compiles both sources; references
  System.Core, System.Drawing, System.Web.Extensions). From git-bash, build
  via Python subprocess with native paths — MSYS mangles csc path arguments.
