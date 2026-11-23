#!/usr/bin/env python3
"""gpu_rgb.py — MSI Aegis ZS2: GPU temp -> Mystic Light per-LED animation.

Drive-level model (GPU temp AND GPU power OR'd together, smooth blends
between adjacent levels; level N is "zone N+1"):
  level 0 (zone 1): gpu-spotlight-idle            T <= 55 C and PWR <= ~20 W
  level 1 (zone 2): gpu-spotlight-somewhat-active T >= 60 C  OR  PWR > 20 W
  level 2 (zone 3): sweep (pattern 6)             T >= 65 C  OR  PWR > 300 W
  level 3 (zone 4): red_white (pattern 26)        T >= 75 C
  (the old level states darkblue/perfan/chase_cool/chase_warm are no
   longer temp-driven; still previewable as patterns 1-4)
  The higher of the temp-level and the power-level wins (OR semantics);
  each threshold is smoothstep-blended, no hard transitions.

The fan ring is the j_rainbow_1 ARGB line, driven in the board's per-LED
direct mode (OpenRGB MSIMotherboard185Controller sync protocol):
  - 185-byte report 0x52 "enable per-LED" message (once at startup)
  - 725-byte report 0x53 color packet every frame (20 Hz), 240 slots:
      [0..5] onboard, [6] j_rgb_1, [7] j_rgb_2, [8..47] j_rainbow_1 (fans),
      [48..87] j_rainbow_2, [88..207] j_corsair
  The fans latch the first N slots of the j_rainbow_1 stream (N = LEDs per
  fan, hardware-dependent); the chase period is 24 slots, so it animates
  correctly for any N <= 40. All fans share one data line and show the
  same animation (they are electrically one bus).

Commands:
  serve                run the daemon loop (systemd)
  preview <state> [S]  force a state for S seconds
                       states: darkblue|perfan|chase_cool|chase_warm
                             |beacon|sweep|tandem|heartbeat|comet|hazard
                             |bitclock|telegraph|fireflies|lava|sonar|
                             metronome|orbit|bitstream|sparkler|echo|
                             ecg|walk|gpu-spotlight-idle|
                             gpu-spotlight-fire|
                             gpu-spotlight-somewhat-active|red_white|
                             rainbow|limelight|catwalk|supernova|inferno|\
                             reactor-core|matrix|cyberpunk|cryogenic|\n                             emergency-mode|lightning-strike|ledprobe|off
                       (all preview-only states are NEVER temp/power-
                       selected: beacon = bright yellow arc sweeping over
                       a dim orange ring; sweep..hazard = the five
                       spinning warning beacon options (beacon-designs.md);
                       bitclock..walk = the 12 creative patterns
                       (creative-patterns.md); gpu-spotlight-idle/fire/
                       somewhat-active = options 23-25 (ONLY the
                       top-fan 12/13 spotlight subject shines down on
                       the GPU, everything else pure black — user
                       2026-11-22); red_white =
                       option 26 (static: RING10 2..11 pure red, 12/13
                       pure white, rest dark); rainbow = option 27 (a
                       multi-colored wheel slowly rotating around the
                       fan ring 6..11 + board mirror, GPU LEDs 12/13
                       cycling the same six colors one at a time);
                       limelight = option 28 (the GPU gets the
                       limelight: 12/13 breathe a bright white-hot
                       star while the fan 6..11 + board 0..5 dim
                       blue wash breathes inversely — the room dims
                       as the star flares);
                       catwalk = option 29 (the runway show: a
                       strutting warm-white light bar walks the magenta
                       fan-ring runway 6..11 while the 12/13
                       photographers fire rapid near-white camera
                       flashes every lap and the board 0..5 audience
                       cheers gold);
                       supernova = option 30 (a 6-second cosmic story:
                       the GPU itself (12/13) is a STAR that charges
                       (the fan 6..11 + board 0..5 swell a blue
                       corona), DETONATES (all live flash near-white ->
                       hot-gold fireball — the hard blink climax), throws
                       a SHOCKWAVE band that sweeps both rings IN PHASE
                       cooling bright gold -> deep orange-red over
                       ember (the star is now a remnant PULSAR), then
                       cools to the calm night state — seamless loop);
                       inferno = option 31 (the GPU sits underneath a
                       furnace: the fan ring 6..11 slowly cycles dark red
                       -> bright orange; the 12/13 top fans pulse randomly
                       like flames — bright yellow/orange with near-white
                       sparks; the board ring 0..5 glows a dim ember in
                       phase);
                       reactor-core = option 32 (the GPU as the power
                       source of a futuristic reactor: the ALL-fan ring
                       6..11 deep blue slowly pulsing; the 12/13 top
                       fans bright cyan flashing brighter toward near-
                       white every 3 s as energy transfers into the GPU;
                       the board ring 0..5 dim blue in phase with a
                       flash ripple);
                       matrix = option 33 (a computer running a massive
                       simulation: the ALL-fan ring 6..11 very dark
                       green that flashes bright green across every fan
                       once every 6 s (the data burst); the 12/13 top
                       fans bright neon green slowly pulsing, lerp'd
                       toward a brighter green by the same flash; the
                       board ring 0..5 a dim green wash in phase with a
                       flash ripple);
                       cyberpunk = option 34 (futuristic neon: the
                       ALL-fan ring 6..11 purple slowly transitioning
                       to electric blue; the 12/13 top fans electric
                       cyan that flash magenta once every 6 s; the
                       board ring 0..5 a dim purple/blue wash in phase
                       with a magenta ripple at the flash);
                       cryogenic = option 35 (a frozen GPU cooling
                       chamber: the ALL-fan ring 6..11 dim icy blue
                       slowly fading toward white and back; the 12/13
                       top fans bright white occasionally flashing pure
                       white; the board ring 0..5 a dim icy-blue wash
                       in phase);
                       emergency-mode = option 36 (an emergency
                       response system: the ALL-fan ring 6..11 dark
                       blue that briefly flashes pure red across every
                       fan once every 6 s; the 12/13 top fans
                       alternate red and blue, swapping every 1.5 s;
                       the board ring 0..5 dim dark blue, strobing red
                       in phase with the all-fan flash);
                       lightning-strike = option 37 (a lightning strike:
                       the ALL-fan ring 6..11 stay dark blue; the 12/13
                       top fans flash pure white — the strike — and a split
                       second later the all fans flash bright blue;
                       everything returns to dark; the board ring 0..5 dim
                       blue wash ripples during the flashes);
                       plasma-storm = option 39 (a plasma storm: every
                       fan flickers independently between dark violet and
                       bright purple, the 12/13 top fans flicker
                       independently between electric blue and white,
                       each slot's brightness re-randomized every
                       100-300 ms, and the full case occasionally flashes
                       near-white once every 6 s; the board ring 0..5
                       carries a dim violet wash with its own flicker);
                       ledprobe =
                       exactly one
                       j_rainbow_1 address (slot 8+index) white, every
                       other slot pure black — the mystic-ledprobe
                       one-address-at-a-time probe; off = pure black (0,0,0)
                       on every slot)
                       (daemon running -> hands rendering to it via
                       .override, safe; --direct = drive device directly)
  status               current temp, state, and set color
  --check              offline validation (no device touched)

Stdlib only. Logs to stderr (journald). On SIGTERM/SIGINT the board is
left in zone-static dark blue (per-LED mode exited cleanly).
"""

import fcntl
import glob
import json
import math
import os
import signal
import subprocess
import sys
import time

# ---- drive model (temp OR power) ----------------------------------------------
FPS = 20
TEMP_A_MAX = 55    # <= 55 : dark blue static
TEMP_B_MAX = 60    # 55..60: -> 8-color ring (perfan)
TEMP_C_MAX = 65    # 60..65: -> chase_cool
TEMP_D_MAX = 75    # 65..75: -> chase_warm ; >= 75: chase_warm
PWR_B_LOW = 15.0   # power 0->1 blend: 15..25 W (trigger: > 20 W)
PWR_B_HIGH = 25.0
PWR_C_LOW = 290.0  # power 1->2 blend: 290..310 W (trigger: > 300 W)
PWR_C_HIGH = 310.0

DARK_BLUE = (33, 33, 140)
PERFAN = [
    (255, 0, 0), (0, 255, 0), (0, 0, 255), (255, 255, 0),
    (255, 0, 255), (0, 255, 255), (255, 255, 255), (255, 128, 0),
]
PALETTE_COOL = [(0, 0, 255), (255, 128, 0), (255, 255, 0)]
PALETTE_WARM = [(255, 255, 0), (255, 128, 0), (255, 0, 0)]

# (The old 12-slot/8-latch comet constants SEG_LEN/GAP_LEN/PERIOD were
# retired by the 2026-11-21 RING10 re-base of patterns 3/4.)

# Preview-only beacon state (never selected by the temp/power model):
# a SMALL yellow bulb (with a short fading tail) spinning over a steady
# orange ring — the bulb must stay much smaller than the 8-LED fan window
# or the whole fan lights in unison (reads as a pulse, not a spin).
# The override file may pass {"base": [r,g,b]} for live tweaking
# ("preview lab", no restart); head/tail geometry is fixed per ring.
BEACON_BASE = (255, 128, 0)   # steady orange ring
BEACON_ARC = (255, 255, 0)    # yellow bulb
PHASE_PERIOD = 120             # phase wrap; any period used must divide it

FAN_START = 8                  # j_rainbow_1 stream starts at packet slot 8
FAN_LEN = 40                   # sync-mode cap for j_rainbow_1
NUM_SLOTS = 240

# LIVE LATCH MAP (re-probed 2026-11-21, mystic-ring-test, user-
# confirmed): the standard ring is the contiguous 10 slots 2..11 —
# o'clock 12->10, 1->11, 2->2, 4->3, 5->4, 6->5, 7->6, 8->7, 10->8,
# 11->9; 3 and 9 o'clock have NO slot (dark gaps). Slots 12/13 are the
# TOP case fans only, shining down on the GPU — not part of the ring,
# and EVERY pattern paints them. Slots 0..1 are live but UNASSIGNED:
# pure black in every pattern. Slots 14..239 are DEAD latches: pure
# black in every pattern. The old board ring (LIVE_ONBOARD 0..5) is
# RETIRED — 2..5 are ring positions (2/4/5/6 o'clock); the old fan
# ring (SWEEP_FAN 6..11) is a subset of RING10 (7/8/10/11/12/1
# o'clock). The old LIVE_ONBOARD / SWEEP_FAN / SWEEP_ONBOARD constants
# are retired (removed 2026-11-21, final task) — nothing references
# them; every pattern is re-based onto RING10 + TOPFANS (re-base
# 2026-11-21, period P10, 10 | 120).
RING10 = (2, 3, 4, 5, 6, 7, 8, 9, 10, 11)
TOPFANS = (12, 13)
UNASSIGNED = (0, 1)
BLACK_FRAME = bytes(NUM_SLOTS * 3)   # preview-only "off": pure (0,0,0)
                                       # on every slot (frame_colors would
                                       # paint onboard slots 0..7 dark blue)

LEDPROBE_ON = (255, 255, 255)

# Per-LED zone map for ledprobe: name -> (start_slot, count) in the 725B
# stream's 240 RGB slots. Layout: [0..5] onboard, [6] j_rgb_1, [7] j_rgb_2,
# [8..47] j_rainbow_1 (the case fans), [48..87] j_rainbow_2, [88..207]
# j_corsair, [208..239] unnamed spare tail. The probe walks one of these
# zones at a time, one slot at a time, so the user can map each address to
# the physical LED it drives.
LEDPROBE_ZONES = {
    "onboard":     (0, 6),
    "j_rgb_1":     (6, 1),
    "j_rgb_2":     (7, 1),
    "j_rainbow_1": (8, 40),
    "j_rainbow_2": (48, 40),
    "j_corsair":   (88, 120),
    "spare":       (208, 32),
}
LEDPROBE_ZONE_ORDER = ("onboard", "j_rgb_1", "j_rgb_2", "j_rainbow_1",
                       "j_rainbow_2", "j_corsair", "spare")


def ledprobe_zone(name):
    """(start_slot, count) for a zone name; unknown names fall back to
    j_rainbow_1 (the original fan line)."""
    if name in LEDPROBE_ZONES:
        return LEDPROBE_ZONES[name]
    return LEDPROBE_ZONES["j_rainbow_1"]


def ledprobe_frame(phase, params=None):
    """Preview-only ledprobe (mystic-ledprobe): exactly ONE per-LED address
    (or a WHOLE zone) bright white, EVERY other slot pure (0,0,0) — like the
    off / BLACK_FRAME treatment, so each step shows exactly one position
    (or one zone) with no background muddying the count. Static
    (phase-independent).
    params may carry {"zone": name, "index": i_or_"all"}:
      - index = int  -> light exactly slot start+index (0-based, clamped)
      - index = "all" -> light EVERY slot of the zone (start .. start+count-1)
    zone defaults to j_rainbow_1, which with an int index reproduces the
    old index-only fan probe exactly (slot 8+index)."""
    p = params or {}
    start, count = ledprobe_zone(p.get("zone", "j_rainbow_1"))
    raw = p.get("index", 0)
    if isinstance(raw, str) and raw.strip().lower() == "all":
        lo, hi = start, start + count - 1
    else:
        try:
            idx = int(raw)
        except (TypeError, ValueError):
            idx = 0
        idx = max(0, min(idx, count - 1))
        lo = hi = start + idx
    out = bytearray(NUM_SLOTS * 3)
    for i in range(lo, hi + 1):
        out[3 * i:3 * i + 3] = bytes(LEDPROBE_ON)
    return bytes(out)


STATE_NAMES = {
    "A": "darkblue",
    "B": "perfan",
    "C": "chase_cool",
    "D": "chase_warm",
}
STATE_RGB = {
    "A": DARK_BLUE,
    "B": (0, 0, 255),
    "C": (0, 0, 255),
    "D": (255, 255, 0),
}

# ---- device protocol (OpenRGB MSIMotherboard185Controller sync mode) --------
REPORT_ID = 0x52
PERLED_REPORT_ID = 0x53
PKT = 185
PERLED_PKT = 725               # 1 report_id + 4 hdr + 240*3 colors

J_RGB_1 = 1
J_PIPE_1 = 11
J_PIPE_2 = 21
J_RAINBOW_1 = 31               # RainbowZoneData (11B), cycle_or_led_num at +10
J_RAINBOW_2 = 42
J_CORSAIR = 53                 # CorsairZoneData (11B)
J_CORSAIR_OUTER = 64
ON_BOARD = 74
ON_BOARD_1 = 84
J_RGB_2 = 174
SAVE_OFF = 184

EFFECT_STATIC = 0x01
EFFECT_DIRECT = 0x25           # MSI_DIRECT_MODE
FULL_SYNC = 0x80 | 0x01 | 0x02 | 0x04 | 0x08   # PER_LED_FULL_SYNC_MODE

DEV = None


def log(msg):
    sys.stderr.write("gpu_rgb: %s\n" % msg)
    sys.stderr.flush()


def find_msi_hidraw():
    for n in sorted(glob.glob("/sys/class/hidraw/hidraw*")):
        ue = os.path.join(n, "device", "uevent")
        if os.path.exists(ue) and "HID_NAME=MSI MYSTIC LIGHT" in open(ue).read():
            return "/dev/" + os.path.basename(n)
    return None


def _ioc(d, t, n, s):
    return (d << 30) | (s << 16) | (ord(t) << 8) | n


def HIDIOCSFEATURE(n):
    return _ioc(3, "H", 0x06, n)


def HIDIOCGFEATURE(n):
    return _ioc(3, "H", 0x07, n)


def _ctl(fd, code, buf):
    return fcntl.ioctl(fd, code, buf)


def open_dev():
    global DEV
    if not DEV:
        DEV = find_msi_hidraw()
    if not DEV:
        log("error: no MSI MYSTIC LIGHT hidraw device found")
        return None
    return os.open(DEV, os.O_RDWR)


def get_report(fd, report_id, length):
    buf = bytearray(length)
    buf[0] = report_id
    res = _ctl(fd, HIDIOCGFEATURE(length), buf)
    if res != length:
        raise IOError("GET returned %d, expected %d" % (res, length))
    return bytes(buf)


def set_report(fd, data):
    _ctl(fd, HIDIOCSFEATURE(len(data)), data)


def build_enable_msg(state):
    """Sync-mode per-LED enable message (185B report 0x52), OpenRGB-faithful:
    constructor template + SelectPerLedProtocol sync overrides."""
    m = bytearray(state)
    def zone(off, flags=None, cf=None):
        if flags is not None:
            m[off + 4] = flags
        if cf is not None:
            m[off + 8] = cf
    zone(J_RGB_1, flags=0x08, cf=0x80)
    zone(J_PIPE_1, flags=0x2A, cf=0x80)
    zone(J_PIPE_2, flags=0x2A, cf=0x80)
    zone(J_RAINBOW_1, flags=0x29, cf=0x80)
    m[J_RAINBOW_1 + 10] = 0x28
    zone(J_RAINBOW_2, flags=0x29, cf=0x80)
    m[J_RAINBOW_2 + 10] = 0x28
    m[J_CORSAIR + 4] = 0x29
    m[J_CORSAIR + 5] = 0x00
    m[J_CORSAIR + 6 + 2] = 0x82
    m[J_CORSAIR + 10] = 0x78
    zone(J_CORSAIR_OUTER, flags=0x28, cf=0x80)
    m[ON_BOARD + 0] = EFFECT_DIRECT
    m[ON_BOARD + 4] = 0x29 | 0x80
    m[ON_BOARD + 8] = FULL_SYNC
    for off in range(ON_BOARD_1, 174, 10):
        zone(off, flags=0x28, cf=0x80)
    zone(J_RGB_2, flags=0x2A, cf=0x80)
    # sync-mode overrides
    m[J_RAINBOW_1 + 0] = EFFECT_STATIC
    m[J_RAINBOW_1 + 10] = 40
    m[J_RAINBOW_2 + 0] = EFFECT_STATIC
    m[J_RAINBOW_2 + 10] = 40
    m[J_CORSAIR + 0] = EFFECT_STATIC
    m[J_CORSAIR + 10] = 120
    m[ON_BOARD + 8] = FULL_SYNC
    return bytes(m)


def build_static_state(captured, rgb=DARK_BLUE):
    """Zone-static 185B config at `rgb` (exits per-LED mode)."""
    base = bytearray(captured) if captured is not None else bytearray(PKT)
    base[0] = REPORT_ID
    zones = [J_RGB_1, J_PIPE_1, J_PIPE_2, J_RAINBOW_1, J_RAINBOW_2,
             J_CORSAIR, J_CORSAIR_OUTER,
             ON_BOARD, ON_BOARD + 10, ON_BOARD + 20, ON_BOARD + 30,
             ON_BOARD + 40, ON_BOARD + 50, ON_BOARD + 60, ON_BOARD + 70,
             ON_BOARD + 80, ON_BOARD + 90, J_RGB_2]
    for off in zones:
        base[off + 0] = EFFECT_STATIC
        base[off + 1] = rgb[0]
        base[off + 2] = rgb[1]
        base[off + 3] = rgb[2]
        base[off + 4] = 0x28
        base[off + 5] = rgb[0]
        base[off + 6] = rgb[1]
        base[off + 7] = rgb[2]
        base[off + 8] = 0x81 if off == ON_BOARD else 0x80
        base[off + 9] = 0x00
    base[SAVE_OFF] = 0
    return bytes(base)


def restore_static(fd, captured, rgb=DARK_BLUE):
    try:
        cur = get_report(fd, REPORT_ID, PKT)
        c = bytearray(cur)
        c[SAVE_OFF] = 0
        set_report(fd, bytes(c))
    except Exception as e:
        log("warn: pre-restore read failed: %s" % e)
    set_report(fd, build_static_state(captured, rgb))


# ---- color math --------------------------------------------------------------

def smoothstep(x):
    x = max(0.0, min(1.0, x))
    return x * x * (3.0 - 2.0 * x)


def level_from_temp(temp):
    """0..3 drive level from temperature alone. Pure."""
    if temp <= TEMP_A_MAX:
        return 0.0
    if temp < TEMP_B_MAX:
        return smoothstep((temp - TEMP_A_MAX) / (TEMP_B_MAX - TEMP_A_MAX))
    if temp < TEMP_C_MAX:
        return 1.0 + smoothstep((temp - TEMP_B_MAX) / (TEMP_C_MAX - TEMP_B_MAX))
    if temp < TEMP_D_MAX:
        return 2.0 + smoothstep((temp - TEMP_C_MAX) / (TEMP_D_MAX - TEMP_C_MAX))
    return 3.0


def level_from_power(pwr):
    """0..2 drive level from power alone. Pure. (power never drives
    chase_warm: that stays temp-only)."""
    if pwr <= PWR_B_LOW:
        return 0.0
    if pwr < PWR_B_HIGH:
        return smoothstep((pwr - PWR_B_LOW) / (PWR_B_HIGH - PWR_B_LOW))
    if pwr <= PWR_C_LOW:
        return 1.0
    if pwr < PWR_C_HIGH:
        return 1.0 + smoothstep((pwr - PWR_C_LOW) / (PWR_C_HIGH - PWR_C_LOW))
    return 2.0


def state_weights(temp, power):
    """Blended weights (wA, wB, wC, wD) for a float temp and power (W).
    OR semantics: the higher of the two drive levels wins. Pure."""
    level = max(level_from_temp(temp), level_from_power(power))
    wA = max(0.0, 1.0 - level)
    wB = max(0.0, 1.0 - abs(level - 1.0))
    wC = max(0.0, 1.0 - abs(level - 2.0))
    wD = max(0.0, 1.0 - abs(level - 3.0))
    return (wA, wB, wC, wD)


def chase(i, phase, palette):
    """One slot of the 3-color comet chase on RING10 (re-based
    2026-11-21): three 2-slot color blocks (the palette) + 4 dim
    BEACON_BG base slots, period 10 (0.5 s/lap), +i, 1 slot/frame.
    i = ring index 0..9. (The old 12-slot/8-latch pattern is
    retired.)"""
    j = (i - phase) % 10
    if j in (0, 1):
        return palette[0]
    if j in (3, 4):
        return palette[1]
    if j in (6, 7):
        return palette[2]
    return BEACON_BG


def _chase_topfan(phase, palette):
    """TOPFANS 12/13 color for patterns 3/4 (spec 3.2 motion rule,
    re-based 2026-11-21): dim BEACON_BG base; flare toward a block's
    palette color when a 2-slot block is on slot 10 (f=1.0) or 9/11
    (f=0.5); f = max over the three blocks. The blend is floored to the
    base per channel (_on_base) so 12/13 never dip below the floor."""
    f, color = 0.0, BEACON_BG
    for k in range(3):
        a, b = (k * 3 + phase) % 10, (k * 3 + 1 + phase) % 10
        slots = (RING10[a], RING10[b])
        if 10 in slots:
            f, color = 1.0, palette[k]
        elif 0.5 > f and (9 in slots or 11 in slots):
            f, color = 0.5, palette[k]
    blend = tuple(int(b0 + f * (c0 - b0))
                  for b0, c0 in zip(BEACON_BG, color))
    return _on_base(blend)


def beacon_frame(phase, params=None):
    """720 bytes (240 RGB slots) for the preview-only beacon state. Pure.
    (re-based 2026-11-21 to RING10 + TOPFANS) One 2-slot yellow beacon
    bulb with a 1-slot fading tail (0.5, behind) spinning on the
    standard ring RING10 (slots 2..11, P10, 0.5 s/rev) over a steady
    orange ring, +i, head leading. TOPFANS slots 12/13 hold the dim
    orange base and flare toward the yellow bulb when the bulb is on
    slot 10 (f=1.0) or 9/11 (f=0.5) — spec 3.2 motion rule. Slots 0..1
    (live, unassigned) and 14..239 (dead) stay pure black. Period 10
    | 120. The old two-ring (board 0..5 + fan 6..11) version is
    retired: single RING10. params (preview lab) may override the
    orange ring base; bad values fall back."""
    p = params or {}
    try:
        base = tuple(int(x) for x in p.get("base", BEACON_BASE))
        if len(base) != 3:
            raise ValueError
    except (TypeError, ValueError):
        base = BEACON_BASE
    out = bytearray(NUM_SLOTS * 3)
    for i in RING10:
        out[3 * i:3 * i + 3] = bytes(base)
    for k, t in ((0, 1.0), (1, 1.0), (-1, 0.5)):
        i = RING10[(phase + k) % len(RING10)]
        c = tuple(int(base[ch] + (BEACON_ARC[ch] - base[ch]) * t)
                  for ch in range(3))
        out[3 * i:3 * i + 3] = bytes(c)
    # TOPFANS: dim orange base + f-blend flare toward the bulb (spec
    # 3.2 motion rule); the bulb is brighter than the base on every
    # channel, so the blend can never dip below the floor.
    bulb = (RING10[phase % len(RING10)], RING10[(phase + 1) % len(RING10)])
    f = 1.0 if 10 in bulb else (0.5 if 9 in bulb or 11 in bulb else 0.0)
    topfan = tuple(int(b + f * (h - b))
                   for b, h in zip(BEACON_BG, BEACON_ARC))
    for i in TOPFANS:
        out[3 * i:3 * i + 3] = bytes(topfan)
    return bytes(out)


# ---- Preview-only beacon options 6-10 (from beacon-designs.md) --------------
# All paint the live set 0..11 (board ring 0..5 + ALL-fan ring 6..11) as
# two rings — 12/13 (top fans only) are skipped and stay dark (user
# 2026-11-16), 14..239 are dead and never painted. Heads are <= 2 slots
# (well under the 6-LED fan ring window, so they read as a SPIN, not a
# PULSE). Every period divides PHASE_PERIOD (120). Direction +i:
# j = (i - phase) % period; direction -i: j = (i + phase) % period.
# Backgrounds are a dim amber base (BEACON_BG) so the ring has color
# almost all the time (user request 2026-11-15); HEARTBEAT (8) has its
# own brighter breathing ring.
SWEEP_PERIOD = 10  # RING10 period (re-based 2026-11-21); 10 | 120
SWEEP_HEAD = (255, 255, 0)
SWEEP_TAIL = [(170, 127, 0), (85, 63, 0), (0, 0, 0)]
# SWEEP (6) is the RING10 + TOPFANS reference template (re-based
# 2026-11-21): one 1-slot yellow radar dot on RING10 (slots 2..11,
# P10, 0.5 s/rev, +i) with a 3-slot linear amber afterglow wrapping
# the ring; TOPFANS 12/13 hold the dim amber base and flare toward
# the head color when the head crosses slot 10 (f=1.0) or 9/11
# (f=0.5). Every later re-base task copies this structure. Slots 0..1
# (live, unassigned) and 14..239 (dead) stay pure black. The legacy
# SWEEP_FAN / SWEEP_ONBOARD / LIVE_ONBOARD constants are retired
# (removed 2026-11-21, final task) — nothing references them.

TANDEM_HEAD = (255, 191, 0)
TANDEM_TAIL = [(215, 161, 0), (127, 95, 0), (39, 29, 0), (0, 0, 0)]

HEARTBEAT_BREATH_PERIOD = 60
HEARTBEAT_RING = (255, 102, 0)
HEARTBEAT_HEAD = (255, 255, 0)

COMET_HEAD = (255, 255, 0)
COMET_HUE_START = (255, 191, 0)    # amber at the bulb
COMET_HUE_END = (255, 102, 0)      # deep orange at the tip

HAZARD_HEAD_EVEN = (255, 191, 0)   # even frames (amber)
HAZARD_HEAD_ODD = (255, 255, 0)    # odd frames (yellow) -> 10 Hz blink
HAZARD_TAIL = [(255, 128, 0), (153, 76, 0), (76, 38, 0), (0, 0, 0)]
BEACON_BG = (48, 24, 0)  # dim amber base under beacons 6-10: user wants
                        # beacons to have color almost all the time
                        # (2026-11-15), no pure-black background


def _on_base(c, base=BEACON_BG):
    """Per-channel max blend of a slot color over the dim amber base:
    the brighter of (slot, base) wins per channel, so tail tips fade
    into the base instead of a black notch."""
    return (max(base[0], c[0]), max(base[1], c[1]), max(base[2], c[2]))


def sweep_frame(phase, params=None):
    """Option 6 SWEEP — THE TEMPLATE (re-based 2026-11-21 to RING10 +
    TOPFANS): one 1-slot yellow radar dot on the standard ring RING10
    (slots 2..11, P10, 0.5 s/rev), 1 live latch/frame, +i, with a
    3-slot linear amber afterglow wrapping the ring; TOPFANS slots
    12/13 hold the dim amber sweep base and flare toward the head color
    when the head crosses slot 10 (f=1.0) or 9/11 (f=0.5) — spec 3.2
    motion rule, f=0 otherwise. Slots 0..1 (live, unassigned) and
    14..239 (dead) stay pure black. Period 10 | 120. Preview lab:
    params {"slow": N} (int >= 1) divides the phase — slow 2 = half
    speed (each position held 2 frames, 1.0 s/rev)."""
    p = params or {}
    try:
        slow = int(p.get("slow", 1))
        if slow < 1:
            raise ValueError
    except (TypeError, ValueError):
        slow = 1
    ph = phase // slow
    out = bytearray(NUM_SLOTS * 3)
    for i in RING10:
        out[3 * i:3 * i + 3] = bytes(BEACON_BG)
    for k in range(1, len(SWEEP_TAIL) + 1):
        i = RING10[(ph - k) % len(RING10)]
        out[3 * i:3 * i + 3] = bytes(_on_base(SWEEP_TAIL[k - 1]))
    head_slot = RING10[ph % len(RING10)]
    out[3 * head_slot:3 * head_slot + 3] = bytes(SWEEP_HEAD)
    # TOPFANS: dim sweep base + f-blend flare (spec 3.2 motion rule):
    # f=1.0 when the head is on slot 10, f=0.5 on slot 9 or 11.
    f = 1.0 if head_slot == 10 else (0.5 if head_slot in (9, 11) else 0.0)
    topfan = tuple(int(base + f * (head - base))
                   for base, head in zip(BEACON_BG, SWEEP_HEAD))
    for i in TOPFANS:
        out[3 * i:3 * i + 3] = bytes(topfan)
    return bytes(out)


def _tandem_slot(j):
    """One slot of a single TANDEM head: 2-slot amber bulb (j 0..1), 4-slot
    smoothstep tail (j 2..5), base beyond."""
    if j <= 1:
        return TANDEM_HEAD
    if j <= 1 + len(TANDEM_TAIL):
        return TANDEM_TAIL[j - 2]
    return BEACON_BG


def tandem_frame(phase, params=None):
    """Option 7 TANDEM: two amber bulbs (2-slot heads, 4-slot smoothstep
    tails) racing each other on the standard ring RING10 in opposite
    directions, 180 deg apart at phase 0: head A +i, head B -i,
    1 slot/frame (P10, 0.5 s/rev); per-channel max blend, head leading,
    tail trailing. Re-based 2026-11-21 to RING10 + TOPFANS (old board
    0..5 + fan 6..11 double ring retired — 0/1 go black). TOPFANS 12/13
    hold the dim amber base and flare toward the head color, f = max
    over both bulbs (1.0 on slot 10, 0.5 on 9/11, spec 3.2 motion
    rule). Period 10 | 120."""
    out = bytearray(NUM_SLOTS * 3)
    n = len(RING10)
    half = n // 2
    for i in RING10:
        out[3 * i:3 * i + 3] = bytes(BEACON_BG)
    for s in range(n):
        i = RING10[s]
        kA = (s - phase) % n                 # A, +i: head k 0..1
        kB = (s + phase - half) % n          # B, -i, 180 deg apart
        if kA <= 1:
            ca = TANDEM_HEAD
        elif n - 4 <= kA <= n - 1:
            ca = TANDEM_TAIL[n - 1 - kA]    # A tail behind (kA 9..6)
        else:
            ca = BEACON_BG
        if kB <= 1:
            cb = TANDEM_HEAD
        elif kB <= 1 + len(TANDEM_TAIL):
            cb = TANDEM_TAIL[kB - 2]        # B tail behind (kB 2..5)
        else:
            cb = BEACON_BG
        c = (max(ca[0], cb[0]), max(ca[1], cb[1]), max(ca[2], cb[2]))
        out[3 * i:3 * i + 3] = bytes(_on_base(c))
    # TOPFANS: dim amber base + f-blend flare, f = max over both bulbs
    # (spec 3.2 motion rule): bulb A on RING10[phase]..[phase+1], bulb B
    # 180 deg apart; 1.0 on slot 10, 0.5 on 9/11.
    f = 0.0
    for bulb in ((RING10[phase % n], RING10[(phase + 1) % n]),
                 (RING10[(half - phase) % n],
                  RING10[(half - phase + 1) % n])):
        if 10 in bulb:
            f = max(f, 1.0)
        elif 9 in bulb or 11 in bulb:
            f = max(f, 0.5)
    topfan = tuple(int(base + f * (head - base))
                   for base, head in zip(BEACON_BG, TANDEM_HEAD))
    for i in TOPFANS:
        out[3 * i:3 * i + 3] = bytes(topfan)
    return bytes(out)


def heartbeat_frame(phase, params=None):
    """Option 8 HEARTBEAT: a deep-orange ring that breathes (60-frame /
    3 s period, floor 0.3 so the ring dims to (76,30,0), never black)
    while a sharp 1-slot yellow dot laps the standard ring RING10
    (+i, P10, 0.5 s/rev) with a 1-slot yellow->ring blend behind it.
    Re-based 2026-11-21 to RING10 + TOPFANS (old board 0..5 + fan 6..11
    double ring retired — 0/1 go black); TOPFANS 12/13 breathe in phase
    with the ring (spec 3.2 breathing rule). The only design with a lit
    breathing ring. Periods 60 | 120 (breath), 10 | 120 (dot)."""
    breath = 0.65 + 0.35 * math.sin(2.0 * math.pi * phase
                                    / HEARTBEAT_BREATH_PERIOD)
    ring = (int(HEARTBEAT_RING[0] * breath),
            int(HEARTBEAT_RING[1] * breath), 0)
    out = bytearray(NUM_SLOTS * 3)
    for i in RING10:
        out[3 * i:3 * i + 3] = bytes(ring)
    i = RING10[phase % len(RING10)]
    out[3 * i:3 * i + 3] = bytes(HEARTBEAT_HEAD)
    # slot behind the head blends yellow -> current ring color
    i = RING10[(phase - 1) % len(RING10)]
    c = (int((255 + ring[0]) / 2), int((255 + ring[1]) / 2), 0)
    out[3 * i:3 * i + 3] = bytes(c)
    # TOPFANS: breathe in phase with the ring (spec 3.2)
    for i in TOPFANS:
        out[3 * i:3 * i + 3] = bytes(ring)
    return bytes(out)


def _comet_tail(k, n):
    """Slot k (1..n-2) of the COMET tail on an n-slot ring: smoothstep
    ease-out brightness with a hue ramp amber (255,191,0) -> deep orange
    (255,102,0). n=6 reproduces the pre-2026-11-21 P6 tail exactly."""
    s = 1.0 - k / (n + 1.0)
    brightness = s * s * (3.0 - 2.0 * s)
    u = k / n
    g_ramp = COMET_HUE_START[1] - (COMET_HUE_START[1] - COMET_HUE_END[1]) * u
    return (int(255 * brightness), int(g_ramp * brightness), 0)


def comet_frame(phase, params=None):
    """Option 9 COMET: a 2-slot yellow bulb trailing a smoothstep gradient
    tail that fills its own ring, -i direction, dim amber base: the
    standard ring RING10 (2 head + 8 tail, P10, 0.5 s/rev). Re-based
    2026-11-21 to RING10 + TOPFANS (old board 0..5 + fan 6..11 double
    ring retired — 0/1 go black); TOPFANS 12/13 hold the dim amber base
    and flare toward the yellow bulb (f=1.0 bulb on slot 10, 0.5 on
    9/11, spec 3.2 motion rule). The whole ring is lit every frame — the
    comet never leaves the ring, so there is never a dark frame.
    Period 10 | 120."""
    out = bytearray(NUM_SLOTS * 3)
    n = len(RING10)
    for i in RING10:
        out[3 * i:3 * i + 3] = bytes(BEACON_BG)
    pos = (-phase) % n                       # -i: head leading edge
    for k in range(n):
        i = RING10[(pos - k) % n]           # k 0..1 head, 2.. tail
        if k <= 1:
            c = COMET_HEAD
        else:
            c = _comet_tail(k - 1, n)       # k-1 = 1..n-2
        out[3 * i:3 * i + 3] = bytes(_on_base(c))
    # TOPFANS: dim amber base + f-blend flare toward the yellow bulb
    # (spec 3.2 motion rule)
    bulb = (RING10[(-phase) % n], RING10[(-phase - 1) % n])
    f = (1.0 if 10 in bulb else
         0.5 if 9 in bulb or 11 in bulb else 0.0)
    topfan = tuple(int(base + f * (head - base))
                   for base, head in zip(BEACON_BG, COMET_HEAD))
    for i in TOPFANS:
        out[3 * i:3 * i + 3] = bytes(topfan)
    return bytes(out)


def hazard_frame(phase, params=None):
    """Option 10 HAZARD: a 2-slot head strobing at 10 Hz (frame parity:
    even = amber, odd = pure yellow) trailing a blocky 4-slot quantized
    orange tail, on the standard ring RING10 (+i, P10, 0.5 s/rev),
    dim amber base. Re-based 2026-11-21 to RING10 + TOPFANS (old board
    0..5 + fan 6..11 double ring retired — 0/1 go black). TOPFANS 12/13
    strobe in sync with the head (spec 3.2 strobe rule: between base and
    bright — even frame bright amber, odd frame dim amber base; floor =
    BEACON_BG, never black). The blink is phase-parity (120 is even),
    independent of the rotation period. Period 10 | 120."""
    head = (HAZARD_HEAD_EVEN if phase % 2 == 0 else HAZARD_HEAD_ODD)
    out = bytearray(NUM_SLOTS * 3)
    n = len(RING10)
    for i in RING10:
        out[3 * i:3 * i + 3] = bytes(BEACON_BG)
    for k in range(6):
        i = RING10[(phase + 1 - k) % n]     # k0/1 head, k2..5 tail
        if k <= 1:
            c = head
        else:
            c = HAZARD_TAIL[k - 2]
        out[3 * i:3 * i + 3] = bytes(_on_base(c))
    # TOPFANS: strobe in sync (spec 3.2 strobe rule): even frame bright
    # amber, odd frame dim base — floor = BEACON_BG, never black.
    topfan = (HAZARD_HEAD_EVEN if phase % 2 == 0 else BEACON_BG)
    for i in TOPFANS:
        out[3 * i:3 * i + 3] = bytes(topfan)
    return bytes(out)


# ---- Preview-only creative options 11-22 (from creative-patterns.md) ----
# ALL 12 (11-22) RE-BASED 2026-11-17 to the confirmed slot map: board
# ring 0..5 + ALL-fan ring 6..11 (P6), 12/13 skipped (user 2026-11-16:
# ALL-fans priority), 14..47 never painted (the legacy 8..15 span is
# fully retired with this batch). 11-14 RE-BASED 2026-11-21 to RING10
# + TOPFANS per ring10-rebase-spec (ring 2..11; 0/1 black; 12/13
# painted per family; see each block). Every period divides PHASE_PERIOD
# (120); every frame fn derives all timing from t = phase % 120 (small
# integer), so each frame is bit-identical across the 120-frame wrap
# seam. Moving features are <= 2-4 slots wide (point zone, well under
# the fan window) or span the ring uniformly. All tables below are fixed
# (no runtime randomness).

def _tri(d, half=2.0):
    """Triangular (3-slot Gaussian) falloff: 1.0 at 0, 0.5 at 1, 0 at 2."""
    return max(0.0, 1.0 - abs(d) / half)


def _add_slot(out, i, r, g, b):
    """Additive (cap 255) paint of one slot; ignores out-of-canvas slots."""
    if not 0 <= i < FAN_START + FAN_LEN:
        return
    out[3 * i] = min(255, out[3 * i] + r)
    out[3 * i + 1] = min(255, out[3 * i + 1] + g)
    out[3 * i + 2] = min(255, out[3 * i + 2] + b)


def _fill_range(out, lo, hi, color):
    for i in range(lo, hi + 1):
        out[3 * i:3 * i + 3] = bytes(color)


# 11 BITCLOCK: 6-bit binary counter on RING10 slots 2..7 (bit i ->
# slot 2+i); ring slots 8..11 dim baseline + 15-frame lap flash at every
# 120-frame wrap; 12/13 flash in phase with the lap.
BITCLOCK_ON = (0, 255, 120)
BITCLOCK_OFF = (10, 30, 18)
BITCLOCK_BASE = (0, 20, 10)
BITCLOCK_LAP = (0, 255, 160)
BITCLOCK_TOP_BASE = (48, 30, 18)     # 12/13 dim base: BITCLOCK_OFF floored
                                      # to the BEACON_BG floor (>= (48,24,0))


def bitclock_frame(phase, params=None):
    """Option 11 BITCLOCK (re-based 2026-11-21 to RING10 + TOPFANS): a
    6-bit binary counter lights RING10 slots 2..7 (bit i lights when
    (t >> i) & 1, bit i -> slot 2+i). Ring slots 8..11 hold the dim
    baseline and flash the green lap indicator (BITCLOCK_LAP) that
    decays over 15 frames after each 120-frame wrap. The board-ring
    counter 0..5 is retired (0/1 go black). TOPFANS 12/13 breathe in
    phase with the lap flash (spec 3.2 breathing rule): the dim base
    blended toward the lap color, floored to the BEACON_BG floor (>=
    (48,24,0)); slots 14..239 (dead) stay pure black. Period 120 | 120."""
    t = phase % PHASE_PERIOD
    out = bytearray(NUM_SLOTS * 3)
    for i in range(6):
        c = BITCLOCK_ON if (t >> i) & 1 else BITCLOCK_OFF
        out[3 * (2 + i):3 * (2 + i) + 3] = bytes(c)
    lap = 1.0 - smoothstep(t / 15.0)          # full at t=0, ~0 by t=15
    for i in (8, 9, 10, 11):
        _add_slot(out, i, BITCLOCK_BASE[0], BITCLOCK_BASE[1], BITCLOCK_BASE[2])
        _add_slot(out, i, int(BITCLOCK_LAP[0] * lap),
                  int(BITCLOCK_LAP[1] * lap), int(BITCLOCK_LAP[2] * lap))
    # TOPFANS: breathe in phase with the lap (spec 3.2): dim base ->
    # lap color, per-channel int blend, floored to BEACON_BG
    top = _on_base(tuple(int(BITCLOCK_TOP_BASE[c]
                        + lap * (BITCLOCK_LAP[c] - BITCLOCK_TOP_BASE[c]))
                        for c in range(3)))
    for i in TOPFANS:
        out[3 * i:3 * i + 3] = bytes(top)
    return bytes(out)


# 12 TELEGRAPH: slot 4 (now a RING10 position, 5 o'clock) transmits
# Morse "H" (4 dots, on-15/off-15, 15-frame unit = exactly 8 units =
# 120 frames); the rest of RING10 hears it as a 35% warm-white echo
# delayed 10 frames; 12/13 strobe base <-> echo in sync.
TELEGRAPH_DOT = (255, 244, 214)
TELEGRAPH_ECHO = (89, 85, 75)


def telegraph_frame(phase, params=None):
    """Option 12 TELEGRAPH (re-based 2026-11-21 to RING10 + TOPFANS):
    one ring LED (slot 4, now a RING10 position) transmits Morse "H"
    (4 dots at a 15-frame unit: on-15/off-15 x4, the final off is the
    15-frame tail) as warm white (255,244,214); the whole standard ring
    RING10 echoes it 10 frames late at 35% warm white (89,85,75) over a
    dim BEACON_BG base (the old pure-black baseline is gone — no pure
    black on the ring). The dot overwrites slot 4 even during the echo.
    The old fan ring 6..11 echo is retired (0/1 go black). TOPFANS 12/13
    strobe between the dim base and the echo color in sync with the echo
    (spec 3.2 strobe rule: floor = base, never black); slots 14..239
    (dead) stay pure black. Period 30 | 120."""
    t = phase % PHASE_PERIOD
    out = bytearray(NUM_SLOTS * 3)
    for i in RING10:
        out[3 * i:3 * i + 3] = bytes(BEACON_BG)
    echo = ((t - 10) % 30) < 15              # echo, 10 frames late
    if echo:
        for i in RING10:
            out[3 * i:3 * i + 3] = bytes(TELEGRAPH_ECHO)
    for i in TOPFANS:
        c = TELEGRAPH_ECHO if echo else BEACON_BG
        out[3 * i:3 * i + 3] = bytes(c)
    if (t % 30) < 15:                        # transmitter: 4 dots
        out[3 * 4:3 * 4 + 3] = bytes(TELEGRAPH_DOT)
    return bytes(out)


# 13 FIREFLIES: four warm amber points drifting on RING10 (3-slot
# falloff: full at 0, 50% at 1, 0 at 2), staggered 25 frames. Raised-
# cosine envelope: 60 frames visible, 60 dark; 12/13 breathe in phase.
FIREFLY_CORE = (255, 196, 87)
FIREFLY_BASE = (0, 4, 10)
FIREFLY_FLIES = (                  # (base slot, drift amp, phase deg, off)
    (3.0, 1.0, 0, 0),
    (5.0, 1.25, 60, 25),
    (7.0, 1.25, 30, 50),
    (9.0, 1.0, 90, 75),
)


def _firefly_env(t, off):
    """Raised-cosine envelope: 0->1->0 over 60 frames, 0 for the rest."""
    u = (t - off) % PHASE_PERIOD
    if u >= 60:
        return 0.0
    return 0.5 + 0.5 * math.cos(math.pi * (u - 30.0) / 30.0)


def fireflies_frame(phase, params=None):
    """Option 13 FIREFLIES (re-based 2026-11-21 to RING10 + TOPFANS):
    four warm amber points drift slowly on the standard ring RING10
    (pos_k = base_k + amp_k*sin(2pi*t/120 + phi_k); bases 3/5/7/9 so
    every fly stays within 2..11 with amp <= 1.25), each fading in and
    out on a staggered 60-on/60-off envelope (raised-cosine); 3-slot
    falloff (full at 0, 50% at 1, 0 at 2); near-black base (0,4,10).
    The board-ring fifth fly (onboard slot 3) is retired (0/1 go black).
    TOPFANS 12/13 breathe in phase with the flies (spec 3.2 breathing
    rule): the dim BEACON_BG base blended toward the fly core by the max
    envelope across the four flies, floored to BEACON_BG; slots 14..239
    (dead) stay pure black. Period 120 | 120."""
    t = phase % PHASE_PERIOD
    out = bytearray(NUM_SLOTS * 3)
    for i in RING10:
        out[3 * i:3 * i + 3] = bytes(FIREFLY_BASE)
    envs = []
    for base, amp, phi, off in FIREFLY_FLIES:
        env = _firefly_env(t, off)
        envs.append(env)
        if env <= 0:
            continue
        pos = base + amp * math.sin(2.0 * math.pi * t / PHASE_PERIOD
                                    + math.radians(phi))
        for i in RING10:
            b = _tri(i - pos)
            if b > 0:
                _add_slot(out, i, int(FIREFLY_CORE[0] * b * env),
                          int(FIREFLY_CORE[1] * b * env),
                          int(FIREFLY_CORE[2] * b * env))
    # TOPFANS: breathe in phase (spec 3.2): max envelope across the
    # flies, dim base -> fly core, floored to BEACON_BG
    env = max(envs)
    top = _on_base(tuple(int(BEACON_BG[c]
                        + env * (FIREFLY_CORE[c] - BEACON_BG[c]))
                        for c in range(3)))
    for i in TOPFANS:
        out[3 * i:3 * i + 3] = bytes(top)
    return bytes(out)


# 14 LAVA LAMP: three additive blobs (orange/magenta/amber) drifting at
# three incommensurate speeds across RING10; deep purple base.
LAVA_BASE = (12, 6, 20)
LAVA_CENTER = 6.5                 # RING10 center (2..11)
LAVA_AMP = 4.0                    # blob drift amplitude (span 2.5..10.5)
LAVA_BLOBS = (                    # (color, period, phase deg)
    ((255, 110, 40), 120, 0),
    ((255, 60, 150), 60, 80),
    ((255, 170, 60), 40, 40),
)
LAVA_HEAD = (255, 113, 83)        # mean of the three blob colors — the
                                   # 12/13 flare target (motion rule)
LAVA_TOP_BASE = (48, 24, 20)      # 12/13 dim base: LAVA_BASE floored to
                                   # the BEACON_BG floor (>= (48,24,0))


def _lava_bump(d):
    """6-slot raised-cosine bump: full at |d|<=2, 0 at |d|>=4."""
    d = abs(d)
    if d <= 2:
        return 1.0
    if d < 4:
        return 0.5 + 0.5 * math.cos(math.pi * (d - 2.0) / 2.0)
    return 0.0


def lava_frame(phase, params=None):
    """Option 14 LAVA LAMP (re-based 2026-11-21 to RING10 + TOPFANS):
    three soft blobs (pos = LAVA_CENTER + LAVA_AMP*sin(2pi*t/P + phi);
    P = 120/60/40) drift across the standard ring RING10 and blend
    additively where they overlap; 6-slot raised-cosine bumps; deep-
    purple baseline (12,6,20) on the ring. The board ring 0..5 near-black
    is retired (0/1 go black). TOPFANS 12/13 follow the motion rule
    (spec 3.2): the dim base blends toward the lava head color,
    f = max over the three blobs (1.0 a blob on slot 10, 0.5 on 9/11),
    floored to BEACON_BG; slots 14..239 (dead) stay pure black. Periods
    120/60/40 | 120."""
    t = phase % PHASE_PERIOD
    out = bytearray(NUM_SLOTS * 3)
    for i in RING10:
        out[3 * i:3 * i + 3] = bytes(LAVA_BASE)
    f = 0.0
    for color, period, phi in LAVA_BLOBS:
        pos = LAVA_CENTER + LAVA_AMP * math.sin(
            2.0 * math.pi * (t % period) / period + math.radians(phi))
        if abs(pos - 10) <= 0.5:
            f = max(f, 1.0)
        elif abs(pos - 9) <= 0.5 or abs(pos - 11) <= 0.5:
            f = max(f, 0.5)
        for i in RING10:
            b = _lava_bump(i - pos)
            if b > 0:
                _add_slot(out, i, int(color[0] * b), int(color[1] * b),
                          int(color[2] * b))
    # TOPFANS: dim base + f-blend toward the lava head (spec 3.2 motion
    # rule); both blend endpoints are >= floor, floored anyway
    top = _on_base(tuple(int(LAVA_TOP_BASE[c]
                        + f * (LAVA_HEAD[c] - LAVA_TOP_BASE[c]))
                        for c in range(3)))
    for i in TOPFANS:
        out[3 * i:3 * i + 3] = bytes(top)
    return bytes(out)


# 15 SONAR RIPPLE: a cyan ping launches from the RING10 center (6.5)
# every 8 frames and expands until it reaches both ring edges (slots
# 2/11), which flash white for 1 frame; the ring-center slots 6/7 are
# the ping lamp (2 frames). Continuous: a new ripple starts as the old
# one lands — no dark gap.
SONAR_RING = (0, 220, 255)
SONAR_BASE = (0, 6, 14)
SONAR_PING_SLOTS = (6, 7)
SONAR_CENTER = 6.5


def sonar_frame(phase, params=None):
    """Option 15 SONAR RIPPLE (re-based 2026-11-21 to RING10 + TOPFANS):
    two ripple points at 6.5-r and 6.5+r (r = 4.5*(t % 8)/7) expand
    symmetrically from the standard ring RING10's center; 0.85^d falloff
    over 3 slots each side; when t%8==7 the outer points reach the
    ring's physical edges (slots 2/11), which flash white for 1 frame;
    the ring-center slots 6/7 are the ping lamp, 2 frames at launch. The
    old onboard slot 4 ping lamp is dropped (board ring retired — 0/1 go
    black). TOPFANS 12/13 hold the dim base and flare toward the cyan
    family color when a ripple point is on slot 10 (f=1.0) or 9/11
    (f=0.5), else f=0, floored to BEACON_BG (spec 3.2 motion rule).
    Period 8 | 120."""
    t = phase % PHASE_PERIOD
    out = bytearray(NUM_SLOTS * 3)
    for i in RING10:
        out[3 * i:3 * i + 3] = bytes(SONAR_BASE)
    f = t % 8
    if f < 2:                               # ping lamp, 2 frames at launch
        for i in SONAR_PING_SLOTS:
            out[3 * i:3 * i + 3] = bytes(SONAR_RING)
    r = 4.5 * f / 7.0
    pts = (SONAR_CENTER - r, SONAR_CENTER + r)
    for p in pts:
        for i in RING10:
            b = 0.85 ** abs(i - p)
            _add_slot(out, i, int(SONAR_RING[0] * b),
                      int(SONAR_RING[1] * b), int(SONAR_RING[2] * b))
    if f == 7:                              # edge hit: 1-frame white flash
        for e in (RING10[0], RING10[-1]):
            out[3 * e:3 * e + 3] = bytes((255, 255, 255))
    # TOPFANS: dim base + f-blend flare toward cyan (spec 3.2 motion
    # rule), floored to BEACON_BG
    fl = 0.0
    for p in pts:
        s = int(p + 0.5)
        if s == 10:
            fl = max(fl, 1.0)
        elif s in (9, 11):
            fl = max(fl, 0.5)
    top = _on_base(tuple(int(BEACON_BG[c]
                          + fl * (SONAR_RING[c] - BEACON_BG[c]))
                         for c in range(3)))
    for i in TOPFANS:
        out[3 * i:3 * i + 3] = bytes(top)
    return bytes(out)


# 16 METRONOME: a gold 2-slot marker swings the standard ring RING10 at
# a real 60 BPM (extrema every 20 frames at the ring edges 2/11); each
# extremum flashes white on the marker AND both ring edges.
METRO_MARK = (255, 214, 90)
METRO_ACCENT = (255, 255, 255)
METRO_BASE = (16, 12, 6)


def metronome_frame(phase, params=None):
    """Option 16 METRONOME (re-based 2026-11-21 to RING10 + TOPFANS):
    x(t) = 6.5 - 4.5*cos(2pi*t/40) swings the standard ring RING10
    left<->right with extrema every 20 frames (60 BPM) at the ring's
    edges 2/11; the 2-slot Gaussian marker flashes white for 3 frames at
    each extremum, together with the ring's two edges; the old 3-bit
    tick counter on onboard 0..2 is dropped (board ring retired — 0/1
    go black). TOPFANS 12/13 hold the dim base and flare toward gold
    when the marker is on slot 10 (f=1.0) or 9/11 (f=0.5), else f=0,
    floored to BEACON_BG (spec 3.2 motion rule). Period 40 | 120."""
    t = phase % PHASE_PERIOD
    out = bytearray(NUM_SLOTS * 3)
    for i in RING10:
        out[3 * i:3 * i + 3] = bytes(METRO_BASE)
    x = 6.5 - 4.5 * math.cos(2.0 * math.pi * (t % 40) / 40.0)
    mark = METRO_ACCENT if t % 20 < 3 else METRO_MARK
    for i in RING10:
        b = _tri(i - x)
        if b > 0:
            _add_slot(out, i, int(mark[0] * b), int(mark[1] * b),
                      int(mark[2] * b))
    if t % 20 < 3:                            # downbeat: flash the ring edges
        for e in (RING10[0], RING10[-1]):
            out[3 * e:3 * e + 3] = bytes(METRO_ACCENT)
    # TOPFANS: dim base + f-blend flare toward gold (spec 3.2 motion
    # rule), floored to BEACON_BG
    s = int(x + 0.5)
    fl = 1.0 if s == 10 else (0.5 if s in (9, 11) else 0.0)
    top = _on_base(tuple(int(BEACON_BG[c]
                          + fl * (METRO_MARK[c] - BEACON_BG[c]))
                         for c in range(3)))
    for i in TOPFANS:
        out[3 * i:3 * i + 3] = bytes(top)
    return bytes(out)


# 17 ORBIT: a white planet (2-slot core + 4-slot halo) at the RING10
# center with three colored satellites orbiting at three radii and
# phases; satellites dim on the far side and hide when passing behind
# the planet.
ORBIT_PLANET = (255, 255, 240)
ORBIT_HALO = (64, 64, 60)
ORBIT_BASE = (16, 16, 14)
ORBIT_SATS = (                    # (color, radius, phase deg)
    ((0, 200, 255), 4.5, 0),
    ((255, 128, 0), 3.15, 240),
    ((160, 255, 160), 1.8, 120),
)


def orbit_frame(phase, params=None):
    """Option 17 ORBIT (re-based 2026-11-21 to RING10 + TOPFANS):
    x_k = 6.5 + r_k*sin(2pi*t/120 + phi_k) (radii 4.5/3.15/1.8 = the
    old 2.5/1.75/1.0 scaled x1.8 to the 10-slot ring, so the outer
    satellite reaches the ring edge 2/11 as before), depth_k =
    cos(...), brightness 0.25 + 0.75*max(depth, 0); a satellite is
    hidden when |x-6.5| <= 2.5 and depth < 0 (passes behind the
    planet); each satellite makes exactly one orbit per 120-frame
    cycle. The planet: 2-slot core 6/7 + halo 4/5/8/9. The old onboard
    pass lamps (slots 0/2/4) are dropped (board ring retired — 0/1 go
    black). TOPFANS 12/13 hold the dim base and flare toward the
    planet's white when a visible satellite is on slot 10 (f=1.0) or
    9/11 (f=0.5), f = max over elements, else f=0, floored to BEACON_BG
    (spec 3.2 motion rule). Period 120 | 120."""
    t = phase % PHASE_PERIOD
    out = bytearray(NUM_SLOTS * 3)
    for i in RING10:
        out[3 * i:3 * i + 3] = bytes(ORBIT_BASE)
    for i in RING10:
        d = abs(i - 6.5)
        if d <= 1.0:
            out[3 * i:3 * i + 3] = bytes(ORBIT_PLANET)
        elif d <= 2.5:
            out[3 * i:3 * i + 3] = bytes(ORBIT_HALO)
    fl = 0.0
    for color, radius, phi in ORBIT_SATS:
        th = 2.0 * math.pi * t / PHASE_PERIOD + math.radians(phi)
        x = 6.5 + radius * math.sin(th)
        depth = math.cos(th)
        if abs(x - 6.5) <= 2.5 and depth < 0:
            continue                           # behind the planet
        bright = 0.25 + 0.75 * max(depth, 0.0)
        for i in RING10:
            b = _tri(i - x) * bright
            if b > 0:
                _add_slot(out, i, int(color[0] * b), int(color[1] * b),
                          int(color[2] * b))
        s = int(x + 0.5)
        if s == 10:
            fl = max(fl, 1.0)
        elif s in (9, 11):
            fl = max(fl, 0.5)
    # TOPFANS: dim base + f-blend flare toward the planet's white
    # (spec 3.2 motion rule), floored to BEACON_BG
    top = _on_base(tuple(int(BEACON_BG[c]
                          + fl * (ORBIT_PLANET[c] - BEACON_BG[c]))
                         for c in range(3)))
    for i in TOPFANS:
        out[3 * i:3 * i + 3] = bytes(top)
    return bytes(out)


# 18 BITSTREAM: a fixed 12-bit payload as 4-slot chunks scrolls LEFT
# across the standard ring RING10 (2.5 chunks visible, clipped), 1
# chunk per 10 frames (12 shifts per 120-frame cycle = seamless lap);
# 12/13 blink as the start bit.
BITSTREAM_ONE = (0, 255, 220)
BITSTREAM_DIM = (0, 40, 36)
BITSTREAM_ZERO = (6, 16, 14)
BITSTREAM_LAMP = (0, 255, 220)
BITSTREAM_PAYLOAD = "010111001011"            # fixed 12-bit payload


def bitstream_frame(phase, params=None):
    """Option 18 BITSTREAM (re-based 2026-11-21 to RING10 + TOPFANS):
    "1" chunk = 2 bright (0,255,220) + 2 dim (0,40,36) slots; "0" chunk
    = 4 dark (6,16,14) slots; every 10 frames the stream shifts LEFT by
    exactly 1 chunk (P10); 12 shifts per 120-frame cycle = one seamless
    lap; the visible window is the standard ring RING10 (slots 2..11,
    2.5 chunks; chunk edges clip at the ring). The old onboard slot 5
    start-bit lamp is dropped (board ring retired — 0/1 go black): its
    character moves to TOPFANS 12/13, which blink as the start bit
    (5 on / 5 off) in the family color over the dim base, floored to
    BEACON_BG, never black. Period 120 | 120."""
    t = phase % PHASE_PERIOD
    out = bytearray(NUM_SLOTS * 3)
    step = (t // 10) % 12
    for i in RING10:
        bit = BITSTREAM_PAYLOAD[(i // 4 + step) % 12]
        o = i % 4
        if bit == "1":
            color = BITSTREAM_ONE if o < 2 else BITSTREAM_DIM
        else:
            color = BITSTREAM_ZERO
        out[3 * i:3 * i + 3] = bytes(color)
    # TOPFANS: start-bit blink, 5 on / 5 off (floor kept, never black)
    top = _on_base(BITSTREAM_LAMP) if t % 10 < 5 else BEACON_BG
    for i in TOPFANS:
        out[3 * i:3 * i + 3] = bytes(top)
    return bytes(out)


# 19 SPARKLER: three scheduled fireworks (t=0/40/80 on RING10 slots
# 7/9/11): 1-slot white flash 3 frames, 2-frame shock ring, 4 sparks per
# side flying out 1 slot/frame for 12 frames, wrapping the 10-slot ring;
# 12/13 = fuse (glows the 5 frames before each burst) + burst flare
# (f=0.5 on burst 9/11, 0 on 7 — spec 3.2); 3 fixed dim dots at the
# launch pads over the dim BEACON_BG base.
SPARK_BASEDOTS = (7, 9, 11)                # launch pads (RING10)
SPARK_DOT = (80, 56, 8)                   # dim launch-pad dot (>= floor)
SPARK_FLASH = (255, 255, 255)
SPARK_SHOCK = (255, 200, 120)
SPARK_SHOCK_LV = (0.5, 0.3, 0.15)          # shock brightness at d=1,2,3
SPARK_A = (255, 150, 0)                    # spark color at flight start
SPARK_B = (255, 60, 20)                    # spark color at flight end
SPARK_FUSE = (255, 191, 0)
SPARK_BURSTS = (7, 9, 11)                # burst slot per 40-frame slot


def _spark_pos(b, d):
    """RING10 position: burst slot b plus distance d, wrapping the 10-
    slot standard ring 2..11 (d is signed)."""
    return 2 + ((b - 2 + d) % 10)


def sparkler_frame(phase, params=None):
    """Option 19 SPARKLER (re-based 2026-11-21 to RING10 + TOPFANS): one
    burst every 40 frames at the next launch pad (7 -> 9 -> 11):
    3-frame 1-slot white flash, 2-frame shock ring at d=1..3
    (0.5/0.3/0.15), then 4 sparks per side flying out 1 slot/frame for
    12 frames, wrapping the standard ring RING10 (color lerp
    (255,150,0)->(255,60,20) inner->outer); dim BEACON_BG baseline on
    RING10 with 3 fixed dim dots at the launch pads. The old onboard
    slot 5 fuse lamp is dropped (board ring retired — 0/1 pure black):
    its countdown moves to TOPFANS 12/13, which glow SPARK_FUSE the 5
    frames before each burst, flare toward the white flash while a
    burst is live (spec 3.2 motion rule: f=0.5 on burst 9/11, 0 on 7),
    and hold the dim base otherwise. Period 40 | 120."""
    t = phase % PHASE_PERIOD
    out = bytearray(NUM_SLOTS * 3)
    for i in RING10:
        out[3 * i:3 * i + 3] = bytes(BEACON_BG)
    for i in SPARK_BASEDOTS:
        out[3 * i:3 * i + 3] = bytes(SPARK_DOT)
    tb = t % 40
    burst = SPARK_BURSTS[(t // 40) % 3]
    if tb <= 1:                             # 2-frame shock ring
        for k, lv in enumerate(SPARK_SHOCK_LV, 1):
            for s in (_spark_pos(burst, k), _spark_pos(burst, -k)):
                out[3 * s:3 * s + 3] = bytes(_on_base(
                    (int(SPARK_SHOCK[0] * lv), int(SPARK_SHOCK[1] * lv),
                     int(SPARK_SHOCK[2] * lv))))
    if tb <= 11:                            # 4 sparks per side, 12 frames
        for j in range(4):
            c = tuple(int(SPARK_A[ch] + (SPARK_B[ch] - SPARK_A[ch]) * j / 3.0)
                      for ch in range(3))
            for s in (_spark_pos(burst, 1 + j + tb),
                      _spark_pos(burst, -(1 + j + tb))):
                out[3 * s:3 * s + 3] = bytes(c)
    if tb <= 2:                             # 1-slot white flash, 3 frames
        out[3 * burst:3 * burst + 3] = bytes(SPARK_FLASH)
    # TOPFANS: fuse countdown (old onboard slot 5, moved here) + burst
    # flare (spec 3.2 motion rule); dim base otherwise, never black.
    if 35 <= tb <= 39:
        top = SPARK_FUSE
    elif tb <= 2:
        f = 0.5 if burst in (9, 11) else 0.0
        top = tuple(int(BEACON_BG[ch]
                         + f * (SPARK_FLASH[ch] - BEACON_BG[ch]))
                    for ch in range(3))
    else:
        top = BEACON_BG
    for i in TOPFANS:
        out[3 * i:3 * i + 3] = bytes(top)
    return bytes(out)


# 20 ECHO BOUNCE: a white 1-slot core bounces the standard ring RING10
# (full round trip 2 -> 11 -> 2 in 20 frames, one slot/frame) trailing
# two discrete amber ghosts lagged 5 and 10 frames (they overtake the
# core at every rebound); the ring edges (2/11) flash white 3 frames at
# each hit; RING10 sits on the dim BEACON_BG base; 12/13 flare toward
# the core color as the core crosses slot 10 (f=1.0) or 9/11 (f=0.5);
# 0/1 and 14..239 pure black.
ECHO_CORE = (255, 255, 255)
ECHO_G1 = (127, 95, 40)                     # (255,190,80) at 50%, 2 slots
ECHO_G2 = (63, 30, 0)                       # (255,120,0) at 25%, 4 slots


def _echo_pos(u):
    """Full round trip in 20 frames: 2 -> 11 -> 2 (u in 0..19)."""
    if u < 10:
        return 2.0 + 9.0 * (u / 10.0)
    return 11.0 - 9.0 * ((u - 10) / 10.0)


def echo_frame(phase, params=None):
    """Option 20 ECHO BOUNCE (re-based 2026-11-21 to RING10 + TOPFANS):
    core at pos(t), ghost 1 at pos(t-5) (2-slot, 50%), ghost 2 at
    pos(t-10) (4-slot, 25%); at every rebound the ghosts overtake the
    core (order inverts); the ring edges (slots 2/11) flash white for 3
    frames when the core hits an end (left t%20<3, right 10<=t%20<13);
    RING10 baseline dim BEACON_BG (old pure-black baseline retired —
    0/1 pure black); TOPFANS 12/13 flare toward the core color as the
    core crosses slot 10 (f=1.0) or 9/11 (f=0.5), dim base otherwise.
    Period 20 | 120."""
    t = phase % PHASE_PERIOD
    out = bytearray(NUM_SLOTS * 3)
    for i in RING10:
        out[3 * i:3 * i + 3] = bytes(BEACON_BG)
    u = t % 20
    for lag, color, w in ((10, ECHO_G2, 2.0), (5, ECHO_G1, 1.0)):
        p = _echo_pos((t - lag) % 20)
        for i in RING10:
            if abs(i - p) < w:
                out[3 * i:3 * i + 3] = bytes(color)
    p = _echo_pos(u)
    c = int(p + 0.5)                        # the 1 slot nearest the core
    out[3 * c:3 * c + 3] = bytes(ECHO_CORE)
    if u < 3:                                # left wall hit
        out[3 * RING10[0]:3 * RING10[0] + 3] = bytes(ECHO_CORE)
    elif 10 <= u < 13:                      # right wall hit
        out[3 * RING10[-1]:3 * RING10[-1] + 3] = bytes(ECHO_CORE)
    # TOPFANS: dim base + f-blend toward the core color (spec 3.2 motion)
    f = 1.0 if c == 10 else (0.5 if c in (9, 11) else 0.0)
    topfan = tuple(int(b0 + f * (h0 - b0))
                   for b0, h0 in zip(BEACON_BG, ECHO_CORE))
    for i in TOPFANS:
        out[3 * i:3 * i + 3] = bytes(topfan)
    return bytes(out)


# 21 ECG: a vital-signs trace scrolls RING10 at 1 slot/frame (beat
# period 30 frames, 4 beats per 120-frame cycle): dim BEACON_BG base,
# Q dip, white R spike (1.5-slot falloff), 40% T bump; the old onboard
# board 0..5 base and slot 4 rate lamp are dropped (0/1 pure black):
# the rate lamp moves to TOPFANS 12/13, which flare toward the R peak
# color (white) as the peak crosses slot 10 (f=1.0) or 9/11 (f=0.5).
ECG_R = (255, 255, 255)
ECG_Q = (0, 60, 40)
ECG_T = (0, 72, 48)                        # (0,180,120) at 40%
ECG_PEAK_U = 12.0                          # R peak at u = 0.4 x 30
ECG_SPACING = 30.0                         # slots per beat (1/frame)


def ecg_frame(phase, params=None):
    """Option 21 ECG (re-based 2026-11-21 to RING10 + TOPFANS): the
    trace scrolls RING10 at 1 slot/frame (beat period 30 frames, 4 per
    cycle); per slot u = (i - offset) % 30: Q dip u in [10,12), T bump
    u in [14,18) at 40%, white R spike centered at u=12 with 1.5-slot
    falloff each side, dim BEACON_BG base elsewhere (the old board 0..5
    base is retired — 0/1 pure black); TOPFANS 12/13 flare toward the
    R peak color (the old rate lamp) as the peak crosses slot 10
    (f=1.0) or 9/11 (f=0.5), dim base otherwise. Period 30 | 120."""
    t = phase % PHASE_PERIOD
    out = bytearray(NUM_SLOTS * 3)
    o = (t % 30) * (ECG_SPACING / 30.0)    # exact 30-frame periodicity
    for i in RING10:
        u = (i - o) % ECG_SPACING
        if 10.0 <= u < 12.0:
            c = ECG_Q
        elif 14.0 <= u < 18.0:
            c = ECG_T
        else:
            c = BEACON_BG
        out[3 * i:3 * i + 3] = bytes(_on_base(c))
        b = max(0.0, 1.0 - abs(u - ECG_PEAK_U) / 1.5)
        if b > 0:
            out[3 * i:3 * i + 3] = bytes(_on_base(
                (int(ECG_R[0] * b), int(ECG_R[1] * b), int(ECG_R[2] * b))))
    # TOPFANS: flare toward the R peak color (spec 3.2 motion rule)
    peak = (o + ECG_PEAK_U) % ECG_SPACING
    f = (1.0 if 10.0 <= peak < 11.0
         else 0.5 if 9.0 <= peak < 10.0 or 11.0 <= peak < 12.0 else 0.0)
    topfan = tuple(int(b0 + f * (h0 - b0))
                   for b0, h0 in zip(BEACON_BG, ECG_R))
    for i in TOPFANS:
        out[3 * i:3 * i + 3] = bytes(topfan)
    return bytes(out)


# 22 WALK CYCLE: a small green creature (2-slot body, 1-slot head ahead)
# walks the standard ring RING10 right (2 -> 11 in 45 frames), pauses to
# look around (10 frames, head white), walks back left (11 -> 2 in 45
# frames), looks around again (10 frames); body bobs (P=10), blinks
# once mid-way (t=20..27); the old onboard companion dot is dropped
# (0/1 pure black); TOPFANS 12/13 flare toward the head color as the
# head crosses slot 10 (f=1.0) or 9/11 (f=0.5).
WALK_BODY = (120, 255, 120)
WALK_HEAD = (255, 255, 120)
WALK_HEAD_BLINK = (51, 51, 31)             # head at 20%
WALK_HEAD_LOOK = (255, 255, 255)
WALK_BLINK = (20, 27)
WALK_WALK_END = 45
WALK_LOOK_END = 60
WALK_BACK_END = 105


def walk_frame(phase, params=None):
    """Option 22 WALK CYCLE (re-based 2026-11-21 to RING10 + TOPFANS):
    t<45: pos = 2 + 9*(t/45) walking right; 45..60: frozen at 11, head
    white (look around); 60..105: pos = 11 - 9*((t-60)/45) walking
    left; 105..120: frozen at 2, head white. Body bobs 1.0/0.75 at
    P=10 while walking; head (1 slot ahead in the travel direction,
    clamped to the RING10 ring 2..11) blinks at t=20..27; the old
    onboard companion dot is dropped (0/1 pure black); TOPFANS 12/13
    flare toward the head color as the head crosses slot 10 (f=1.0) or
    9/11 (f=0.5), dim BEACON_BG base otherwise. Period 120 | 120."""
    t = phase % PHASE_PERIOD
    out = bytearray(NUM_SLOTS * 3)
    for i in RING10:
        out[3 * i:3 * i + 3] = bytes(BEACON_BG)
    look = WALK_WALK_END <= t < WALK_LOOK_END or t >= WALK_BACK_END
    if t < WALK_WALK_END:
        pos = 2.0 + 9.0 * (t / WALK_WALK_END)
        dirn = 1.0
        body_b = 1.0 if (t % 10) < 5 else 0.75
    elif t < WALK_LOOK_END:
        pos, dirn, body_b = 11.0, -1.0, 1.0   # look around (frozen)
    elif t < WALK_BACK_END:
        pos = 11.0 - 9.0 * ((t - WALK_LOOK_END) /
                            (WALK_BACK_END - WALK_LOOK_END))
        dirn = -1.0
        body_b = 1.0 if (t % 10) < 5 else 0.75
    else:
        pos, dirn, body_b = 2.0, 1.0, 1.0     # look around (frozen)
    for i in RING10:
        b = _tri(i - pos) * body_b
        if b > 0:
            out[3 * i:3 * i + 3] = bytes(_on_base(
                (int(WALK_BODY[0] * b), int(WALK_BODY[1] * b),
                 int(WALK_BODY[2] * b))))
    hp = max(2.0, min(11.0, pos + dirn))     # head: 1 ahead, clamped 2..11
    if look:
        head = WALK_HEAD_LOOK
    elif WALK_BLINK[0] <= t <= WALK_BLINK[1]:
        head = WALK_HEAD_BLINK
    else:
        head = WALK_HEAD
    head_slot = int(hp + 0.5)
    out[3 * head_slot:3 * head_slot + 3] = bytes(head)
    # TOPFANS: dim base + f-blend toward the head color (spec 3.2 motion)
    f = 1.0 if head_slot == 10 else (0.5 if head_slot in (9, 11) else 0.0)
    topfan = tuple(int(b0 + f * (h0 - b0))
                   for b0, h0 in zip(BEACON_BG, WALK_HEAD))
    for i in TOPFANS:
        out[3 * i:3 * i + 3] = bytes(topfan)
    return bytes(out)


# ---- Options 23-25 GPU SPOTLIGHT (user 2026-11-16) ----------------------
# The top-fan slots 12/13 are the SPOTLIGHT SUBJECT: they shine down on
# the GPU and shift through the family palette. USER DECISION 2026-11-22
# (reverts the 2026-11-21 re-base wash for these three): these states
# light ONLY slots 12/13 — everything else is pure black (RING10 2..11,
# 0/1, dead 14..239).
SPOTLIGHT_SLOTS = (12, 13)

# 23 gpu-spotlight-idle: blue -> green -> purple -> blue, one full loop
# per 120-frame phase (6 s — "very slow shifting").
SPOT_IDLE_PALETTE = ((0, 64, 255), (0, 224, 128), (128, 0, 255))


def _spot_color(palette, t, period):
    """Loop `palette` over `period` frames, linear per-channel lerp
    between anchors; returns an (r, g, b) tuple. Pure."""
    n = len(palette)
    x = (t % period) / float(period) * n
    k = int(x)
    f = x - k
    a = palette[k]
    b = palette[(k + 1) % n]
    return (int(a[0] + (b[0] - a[0]) * f),
            int(a[1] + (b[1] - a[1]) * f),
            int(a[2] + (b[2] - a[2]) * f))


def gpu_spotlight_idle_frame(phase, params=None):
    """Option 23 gpu-spotlight-idle: ONLY slots 12/13 (the top-fan
    LEDs) light — the very-slow spotlight subject shining down on the
    GPU, blue -> green -> purple -> blue (one full palette loop per
    120-frame phase, 6 s). Everything else pure black: RING10 2..11,
    0/1, dead 14..239 (user 2026-11-22 — the 2026-11-21 RING10 wash is
    removed). Static: period 120 | 120."""
    t = phase % PHASE_PERIOD
    out = bytearray(NUM_SLOTS * 3)
    c = _spot_color(SPOT_IDLE_PALETTE, t, PHASE_PERIOD)
    for i in TOPFANS:
        out[3 * i:3 * i + 3] = bytes(c)
    return bytes(out)


# 24 gpu-spotlight-fire: red -> orange -> yellow -> red, period 15 frames
# (0.75 s full loop, 0.25 s per color — "fast shifting"). 15 | 120.
SPOT_FIRE_PALETTE = ((255, 32, 0), (255, 144, 0), (255, 224, 0))
SPOT_FIRE_PERIOD = 15


def gpu_spotlight_fire_frame(phase, params=None):
    """Option 24 gpu-spotlight-fire: ONLY slots 12/13 (the top-fan
    LEDs) light — the fast spotlight subject shining down on the GPU,
    red -> orange -> yellow (full loop period 15 frames, 0.75 s; 0.25
    s per color). Everything else pure black: RING10 2..11, 0/1, dead
    14..239 (user 2026-11-22 — the 2026-11-21 RING10 wash is removed).
    Static: period 120 | 120."""
    t = phase % PHASE_PERIOD
    out = bytearray(NUM_SLOTS * 3)
    c = _spot_color(SPOT_FIRE_PALETTE, t, SPOT_FIRE_PERIOD)
    for i in TOPFANS:
        out[3 * i:3 * i + 3] = bytes(c)
    return bytes(out)


# 25 gpu-spotlight-somewhat-active: yellow -> white -> orange -> yellow,
# period 30 frames (1 s full loop, 1/3 s per color — "medium fast").
# 30 | 120.
SPOT_ACTIVE_PALETTE = ((255, 224, 0), (255, 255, 255), (255, 144, 0))
SPOT_ACTIVE_PERIOD = 30


def gpu_spotlight_somewhat_active_frame(phase, params=None):
    """Option 25 gpu-spotlight-somewhat-active: ONLY slots 12/13 (the
    top-fan LEDs) light — the medium-fast spotlight subject shining
    down on the GPU, yellow -> white -> orange (full loop period 30
    frames, 1 s; 1/3 s per color). Everything else pure black: RING10
    2..11, 0/1, dead 14..239 (user 2026-11-22 — the 2026-11-21 RING10
    wash is removed). Static: period 120 | 120."""
    t = phase % PHASE_PERIOD
    out = bytearray(NUM_SLOTS * 3)
    c = _spot_color(SPOT_ACTIVE_PALETTE, t, SPOT_ACTIVE_PERIOD)
    for i in TOPFANS:
        out[3 * i:3 * i + 3] = bytes(c)
    return bytes(out)


# 26 red_white (user 2026-11-16): static test — RING10 (slots 2..11)
# pure RED, slots 12/13 (the top-fan 2/3 o'clock LEDs) pure WHITE,
# slots 0..1 (live, unassigned) and 14..239 (dead) pure black.
RED_WHITE_RED = (255, 0, 0)
RED_WHITE_WHITE = (255, 255, 255)


def red_white_frame(phase, params=None):
    """Option 26 red_white (re-based 2026-11-21 to RING10 + TOPFANS):
    static test — RING10 (slots 2..11) pure red, slots 12/13 (the
    top-fan 2/3 o'clock LEDs) pure white; slots 0..1 (live, unassigned)
    and 14..239 (dead) pure black. Static: period 120 | 120."""
    out = bytearray(NUM_SLOTS * 3)
    for i in RING10:
        out[3 * i:3 * i + 3] = bytes(RED_WHITE_RED)
    for i in TOPFANS:
        out[3 * i:3 * i + 3] = bytes(RED_WHITE_WHITE)
    return bytes(out)


# 27 rainbow (user 2026-11-17; re-based 2026-11-21 to RING10 +
# TOPFANS): a multi-colored wheel slowly SPINNING around the standard
# ring RING10 (slots 2..11). The six wheel colors repeat over the 10
# slots (at phase 0: 2=red 2 o'clock, 3=orange 4, 4=yellow 5,
# 5=green 6, 6=blue 7, 7=violet 8 o'clock, then 8=red 10, 9=orange 11,
# 10=yellow 12, 11=green 1 o'clock) and the whole wheel ROTATES: every
# slot's color slides clockwise (the +slot direction) along the color
# wheel — one slot position per 20 frames (1 s), one full rotation per
# 120-frame phase (6 s/rev, the slowest full rotation whose period
# divides 120 — seamless: frame(120) == frame(0)). Smooth continuous
# rotation — every slot stays fully lit every frame (no blink). The
# old board ring 0..5 mirror is dropped — slots 0/1 pure black. Slots
# 12/13 (the GPU LEDs — the subject) keep slowly cycling the same six
# colors, one color at a time: 12-frame hold + 8-frame fade per color
# (1 s per color, one full loop per phase). 14..239 dead, never
# painted.
RAINBOW_COLORS = (
    (255, 0, 0),      # wheel 0: red     (at t=0 on ring slot 2)
    (255, 160, 0),    # wheel 1: orange  (slot 3)
    (255, 240, 0),    # wheel 2: yellow  (slot 4)
    (0, 220, 0),      # wheel 3: green   (slot 5)
    (0, 80, 255),     # wheel 4: blue    (slot 6)
    (160, 0, 255),    # wheel 5: violet  (slot 7; wheel repeats on 8..11)
)
RAINBOW_ROT_FRAMES = 120     # one full wheel rotation per 120-frame phase
RAINBOW_STEP_FRAMES = 20    # 1 slot position (one color step) per 20 frames
RAINBOW_HOLD = 12            # 12/13: frames held on each color
RAINBOW_FADE = 8             # 12/13: fade frames to the next color (12+8=20)


def _rainbow_wheel_color(s, t):
    """Color of wheel slot s (0..9 over RING10) at phase t (0..119). The
    wheel rotates clockwise: slot s shows wheel index (s*20 - t) mod 120
    / 20 (the six colors repeat over the 10 slots), linear per-channel
    lerp between the adjacent anchors; at t=0 slot s is exactly
    RAINBOW_COLORS[s % 6]. Pure."""
    m = (s * RAINBOW_STEP_FRAMES - t) % RAINBOW_ROT_FRAMES
    k = m // RAINBOW_STEP_FRAMES
    f = (m % RAINBOW_STEP_FRAMES) / float(RAINBOW_STEP_FRAMES)
    a = RAINBOW_COLORS[k]
    b = RAINBOW_COLORS[(k + 1) % len(RAINBOW_COLORS)]
    return (int(a[0] + (b[0] - a[0]) * f),
            int(a[1] + (b[1] - a[1]) * f),
            int(a[2] + (b[2] - a[2]) * f))


def _rainbow_cycle(t):
    """Slots 12/13 color at phase t (0..119): the six wheel colors one at
    a time — RAINBOW_HOLD frames held on each color, then RAINBOW_FADE
    frames fading to the next (20 frames per color, one full loop per
    120-frame phase; continuous, never black). Pure."""
    step = t // RAINBOW_STEP_FRAMES
    local = t % RAINBOW_STEP_FRAMES
    a = RAINBOW_COLORS[step % len(RAINBOW_COLORS)]
    if local < RAINBOW_HOLD:
        return a
    b = RAINBOW_COLORS[(step + 1) % len(RAINBOW_COLORS)]
    f = (local - RAINBOW_HOLD) / float(RAINBOW_FADE)
    return (int(a[0] + (b[0] - a[0]) * f),
            int(a[1] + (b[1] - a[1]) * f),
            int(a[2] + (b[2] - a[2]) * f))


def rainbow_frame(phase, params=None):
    """Pattern 27 RAINBOW (re-based 2026-11-21 to RING10 + TOPFANS): a
    multi-colored wheel slowly rotating around the standard ring RING10
    (slots 2..11) — the six wheel colors repeat over the 10 slots
    (at phase 0: 2=red, 3=orange, 4=yellow, 5=green, 6=blue, 7=violet,
    8=red, 9=orange, 10=yellow, 11=green); the wheel slides clockwise,
    one slot position per 20 frames, one full rotation per 120-frame
    phase (seamless: frame(120) == frame(0)) — smooth, every slot fully
    lit every frame. The old board ring 0..5 mirror is dropped — slots
    0/1 pure black. Slots 12/13 (the GPU LEDs — the subject) keep
    slowly cycling the same six colors, one at a time (12-frame hold +
    8-frame fade per color). 14..239 dead, never painted."""
    t = phase % PHASE_PERIOD
    out = bytearray(NUM_SLOTS * 3)
    for s, i in enumerate(RING10):
        out[3 * i:3 * i + 3] = bytes(_rainbow_wheel_color(s, t))
    c12 = _rainbow_cycle(t)
    for i in TOPFANS:
        out[3 * i:3 * i + 3] = bytes(c12)
    return bytes(out)


# 28 limelight (user 2026-11-17; re-based 2026-11-21 to RING10 +
# TOPFANS): the GPU gets the limelight. Slots 12/13 (the top-fan LEDs
# that shine down on the GPU — the star) breathe a bright star: warm
# gold floor (90,60,20) at the trough -> bright white (255,255,250) at
# the peak, one full breath per 60 frames (3 s). The standard ring
# RING10 is the dim INVERSE-breathing backdrop — a true limelight: the
# room goes dark as the spotlight brightens. The ring is a deep-blue
# wash at its brightest (1-b) when the star is dimmest and at its
# floor when it peaks. The old board ring 0..5 wash is dropped — slots
# 0/1 pure black. The breath is a precomputed 60-entry table
# (LIMELIGHT_BREATH), indexed t % 60, so the frame is bit-identical
# every 60 frames (period 60 | 120). Live slots 2..13 never go fully
# black (liveness); 14..239 dead, never painted.
LIMELIGHT_PERIOD = 60                 # 3 s breath; 60 | 120
LIMELIGHT_GPU_FLOOR = (90, 60, 20)   # star trough (dim warm gold)
LIMELIGHT_GPU_TOP = (255, 255, 250)  # star peak (bright white)
LIMELIGHT_FAN_FLOOR = (8, 12, 40)    # ring wash floor (deep blue, b=1)
LIMELIGHT_FAN_TOP = (30, 48, 140)    # ring wash top (brighter blue, b=0)
LIMELIGHT_BREATH = tuple(            # 60-entry sine table (0..1); the
    0.5 + 0.5 * math.sin(2.0 * math.pi * i / LIMELIGHT_PERIOD)   # frame is
    for i in range(LIMELIGHT_PERIOD))                             # indexed t % 60 -> period 60 is bit-exact


def limelight_frame(phase, params=None):
    """Option 28 LIMELIGHT (re-based 2026-11-21 to RING10 + TOPFANS): the
    GPU (slots 12/13 — the top-fan LEDs that shine down on the GPU) is
    the star: a bright breathing star, warm gold (LIMELIGHT_GPU_FLOOR)
    at the trough -> bright white (LIMELIGHT_GPU_TOP) at the peak, one
    breath per 60 frames (3 s). The standard ring RING10 is the dim
    INVERSE-breathing backdrop (the room goes dark as the spotlight
    brightens): a deep-blue wash, brightest when the star is dimmest
    and at its floor when it peaks. The old board ring 0..5 wash is
    dropped — slots 0/1 pure black. Live slots 2..13 never fully black
    (liveness); 14..239 dead, never painted. Period 60 | 120
    bit-exact (precomputed breath table, indexed t % 60)."""
    t = phase % PHASE_PERIOD
    b = LIMELIGHT_BREATH[t % LIMELIGHT_PERIOD]
    inv = 1.0 - b
    out = bytearray(NUM_SLOTS * 3)
    g = tuple(int(LIMELIGHT_GPU_FLOOR[c]
                 + (LIMELIGHT_GPU_TOP[c] - LIMELIGHT_GPU_FLOOR[c]) * b)
              for c in range(3))
    for i in TOPFANS:
        out[3 * i:3 * i + 3] = bytes(g)
    f = tuple(int(LIMELIGHT_FAN_FLOOR[c]
                 + (LIMELIGHT_FAN_TOP[c] - LIMELIGHT_FAN_FLOOR[c]) * inv)
              for c in range(3))
    for i in RING10:
        out[3 * i:3 * i + 3] = bytes(f)
    return bytes(out)


# 29 catwalk (user 2026-11-17; re-based 2026-11-21 to RING10 +
# TOPFANS): an exciting RUNWAY with PHOTO FLASHES. The standard ring
# RING10 is the RUNWAY: a dim magenta carpet with a strutting light
# bar — a bright warm-white 1-slot "model" head + a 2-slot fading
# hot-pink afterglow tail — walking the ring 1 slot/frame (P10,
# 0.5 s/lap). Slots 12/13 (the GPU position — the subject) are the
# PHOTOGRAPHERS at the end of the runway: at every lap (t%10==0, as
# the model crosses the front) both fire a 2-frame near-white camera
# flash + a 1-frame afterglow, otherwise a dim warm-gray standby — one
# burst per lap (~2/s). The old board ring 0..5 AUDIENCE is dropped —
# slots 0/1 pure black. The flash is an intentional hard blink (a photo
# flash blinks — unlike the smoothness-bound breathing states). Period
# 10 | 120, bit-exact; live 2..13 colored every frame (liveness);
# 14..239 dead, never painted.
CATWALK_BASE = (70, 12, 60)         # magenta carpet on the runway (ring)
CATWALK_HEAD = (255, 255, 250)     # warm-white strutting model (1 slot)
CATWALK_TAIL1 = (255, 90, 190)     # hot pink, 1 slot behind the head
CATWALK_TAIL2 = (150, 30, 110)     # deep pink, 2 slots behind
CATWALK_PHOTO_BASE = (70, 70, 62)  # photographers standby (dim warm gray)
CATWALK_PHOTO_FLASH = (255, 255, 250)   # camera flash (near-white, 2 f)
CATWALK_PHOTO_AFTER = (150, 150, 145)   # flash afterglow (1 f)


def catwalk_frame(phase, params=None):
    """Option 29 CATWALK (re-based 2026-11-21 to RING10 + TOPFANS): the
    runway show — the standard ring RING10 is the runway: a dim magenta
    carpet with a strutting warm-white light bar (1-slot HEAD + 2-slot
    fading hot-pink tail) walking 1 slot/frame (P10, 0.5 s/lap). Slots
    12/13 are the photographers at the end of the runway: at every lap
    (t%10==0) both fire a 2-frame near-white camera flash + 1-frame
    afterglow over their dim warm-gray standby (one burst per lap,
    ~2/s). The old board ring 0..5 audience is dropped — slots 0/1 pure
    black. The flash is an intentional hard blink. Period 10 | 120
    bit-exact; live 2..13 colored every frame; 14..239 dead, never
    painted."""
    t = phase % PHASE_PERIOD
    out = bytearray(NUM_SLOTS * 3)
    h = t % 10                                   # head at RING10[h]
    for s in range(10):
        if s == h:
            c = CATWALK_HEAD
        elif s == (h - 1) % 10:
            c = CATWALK_TAIL1
        elif s == (h - 2) % 10:
            c = CATWALK_TAIL2
        else:
            c = CATWALK_BASE
        i = RING10[s]
        out[3 * i:3 * i + 3] = bytes(c)
    k = t % 10                                  # flash phase within the lap
    if k <= 1:
        p = CATWALK_PHOTO_FLASH
    elif k == 2:
        p = CATWALK_PHOTO_AFTER
    else:
        p = CATWALK_PHOTO_BASE
    for i in TOPFANS:
        out[3 * i:3 * i + 3] = bytes(p)
    return bytes(out)


# 30 supernova (user 2026-11-17; re-based 2026-11-21 to RING10 +
# TOPFANS): a 6-second cosmic story in one loop — the GPU itself
# (slots 12/13, the top-fan LEDs that shine down on the GPU) is a STAR.
# Timeline (t = phase % 120, 20 FPS, period 120 | 120):
#   BUILDUP t 0..39 (2 s): the star charges from its dim gold rest state
#     (SNOVA_NIGHT_STAR), rippling faster toward white-hot
#     (SNOVA_STAR_TOP); the standard ring RING10 swells a blue corona
#     (SNOVA_NIGHT_FAN -> SNOVA_FAN_BRIGHT). The old board ring 0..5
#     wash is dropped — slots 0/1 pure black.
#   DETONATION t 40..45 (0.3 s): the explosion — every live latch flashes
#     near-white (SNOVA_FLASH, 2 f) then cools to a hot-gold fireball
#     (SNOVA_FIREBALL). The intentional hard blink (the climax).
#   SHOCKWAVE t 46..85 (2 s): a bright band (1-slot head + 1-slot fading
#     tail) sweeps RING10 (P10, 1 slot/frame), cooling bright gold
#     (SNOVA_SHOCK_HOT) -> deep orange-red (SNOVA_SHOCK_COOL); the rest
#     of the ring is cooling ember (SNOVA_EMBER_HOT ->
#     SNOVA_EMBER_COOL); the band fades out over the last 10 frames; the
#     star is now a REMNANT PULSAR (fast near-white pulse,
#     SNOVA_REMNANT_DIM -> SNOVA_REMNANT_BRIGHT).
#   AFTERGLOW t 86..119 (1.7 s): the sky cools — the ring blends ember ->
#     calm night blue (SNOVA_NIGHT_FAN) and the pulsar settles back into
#     the dim gold rest star; t=119 is bit-identical to t=0 (seamless
#     loop: the night falls and a new star begins to charge).
# Live 2..13 colored every frame (liveness); 0/1 pure black; 14..239
# dead, never painted.
SNOVA_NIGHT_STAR = (89, 61, 39)      # star at rest (loop seam)
SNOVA_STAR_TOP = (255, 245, 225)    # white-hot peak (buildup)
SNOVA_NIGHT_FAN = (10, 14, 40)      # dim blue corona (loop seam)
SNOVA_FAN_BRIGHT = (40, 60, 160)    # swelled blue corona (buildup peak)
SNOVA_FLASH = (255, 255, 250)       # detonation near-white (2 f)
SNOVA_FIREBALL = (255, 205, 90)     # hot-gold fireball (detonation end)
SNOVA_SHOCK_HOT = (255, 210, 70)    # shockwave band, start (bright gold)
SNOVA_SHOCK_COOL = (210, 80, 30)    # shockwave band, end (deep orange-red)
SNOVA_EMBER_HOT = (90, 44, 18)      # cooling ember, start
SNOVA_EMBER_COOL = (50, 22, 10)     # cooling ember, end
SNOVA_REMNANT_DIM = (120, 110, 100)     # pulsar trough
SNOVA_REMNANT_BRIGHT = (255, 255, 250)  # pulsar peak
SNOVA_BUILD_END = 39        # last BUILDUP frame
SNOVA_DET_FLASH_END = 41    # last pure-white DETONATION frame
SNOVA_DET_END = 45          # last DETONATION frame
SNOVA_SHOCK_START = 46      # first SHOCKWAVE frame
SNOVA_SHOCK_END = 85        # last SHOCKWAVE frame
SNOVA_AFTER_START = 86      # first AFTERGLOW frame
SNOVA_FADE_START = 76       # band fade-out begins


def _slerp(a, b, t):
    """Lerp (r,g,b) a -> b by t (clamped 0..1), int-truncated. Pure."""
    t = max(0.0, min(1.0, t))
    return (int(a[0] + (b[0] - a[0]) * t),
            int(a[1] + (b[1] - a[1]) * t),
            int(a[2] + (b[2] - a[2]) * t))


def supernova_frame(phase, params=None):
    """Option 30 SUPERNOVA (re-based 2026-11-21 to RING10 + TOPFANS): a
    6-second cosmic story in one loop — the GPU itself (slots 12/13) is
    a STAR. BUILDUP (t 0..39): the star charges from its dim gold rest
    state, rippling toward white-hot, while the standard ring RING10
    swells a blue corona (the old board ring 0..5 wash is dropped —
    slots 0/1 pure black). DETONATION (t 40..45): the explosion — the
    ring 2..11 and the star flash near-white (2 f) then cool to a
    hot-gold fireball (the intentional hard blink, the climax).
    SHOCKWAVE (t 46..85): a bright band (1-slot head + 1-slot fading
    tail) sweeps RING10 (P10, 1 slot/frame), cooling bright gold ->
    deep orange-red over cooling ember; the band fades out over the last
    10 frames; the star is now a REMNANT PULSAR (fast near-white pulse).
    AFTERGLOW (t 86..119): the sky cools — the ring blends ember -> calm
    night blue and the pulsar settles back into the dim gold rest star;
    t=119 is bit-identical to t=0 (seamless loop). Period 120 | 120;
    live 2..13 colored every frame (liveness); 14..239 dead, never
    painted."""
    t = phase % PHASE_PERIOD
    if t <= SNOVA_BUILD_END:
        env = smoothstep(t / SNOVA_BUILD_END)
        ripple = 0.85 + 0.15 * math.sin(2.0 * math.pi * t / 8.0)
        star = _slerp(SNOVA_NIGHT_STAR, SNOVA_STAR_TOP, env * ripple)
        fb = env * (0.85 + 0.15 * math.sin(2.0 * math.pi * t / 12.0))
        ring = [_slerp(SNOVA_NIGHT_FAN, SNOVA_FAN_BRIGHT, fb)] * 10
    elif t <= SNOVA_DET_END:
        c = (SNOVA_FLASH if t <= SNOVA_DET_FLASH_END
             else _slerp(SNOVA_FLASH, SNOVA_FIREBALL,
                         (t - SNOVA_DET_FLASH_END) / 4.0))
        star, ring = c, [c] * 10
    elif t <= SNOVA_SHOCK_END:
        v = (t - SNOVA_SHOCK_START) / (SNOVA_SHOCK_END - SNOVA_SHOCK_START)
        band = _slerp(SNOVA_SHOCK_HOT, SNOVA_SHOCK_COOL, v)
        ember = _slerp(SNOVA_EMBER_HOT, SNOVA_EMBER_COOL, v)
        pos = (t - SNOVA_SHOCK_START) % 10
        fade = (1.0 if t < SNOVA_FADE_START
                else max(0.0, (SNOVA_SHOCK_END - t)
                         / (SNOVA_SHOCK_END - SNOVA_FADE_START)))
        ring = []
        for s in range(10):
            if s == pos:
                c = _slerp(ember, band, fade)
            elif s == (pos - 1) % 10:
                c = _slerp(ember, band, fade * 0.5)
            else:
                c = ember
            ring.append(c)
        pulse = 0.5 + 0.5 * math.sin(2.0 * math.pi * t / 6.0)
        star = _slerp(SNOVA_REMNANT_DIM, SNOVA_REMNANT_BRIGHT, pulse)
    else:
        v = (t - SNOVA_AFTER_START) / (PHASE_PERIOD - 1 - SNOVA_AFTER_START)
        pulse = 0.5 + 0.5 * math.sin(2.0 * math.pi * t / 6.0)
        pulsar = _slerp(SNOVA_REMNANT_DIM, SNOVA_REMNANT_BRIGHT, pulse)
        star = _slerp(pulsar, SNOVA_NIGHT_STAR, v)
        ring = [_slerp(SNOVA_EMBER_COOL, SNOVA_NIGHT_FAN, v)] * 10
    out = bytearray(NUM_SLOTS * 3)
    for i in TOPFANS:
        out[3 * i:3 * i + 3] = bytes(star)
    for s, i in enumerate(RING10):
        out[3 * i:3 * i + 3] = bytes(ring[s])
    return bytes(out)


# 31 inferno (user 2026-11-17; re-based 2026-11-21 to RING10 + TOPFANS):
# the GPU sits underneath a FURNACE.
#   Standard ring RING10 (slots 2..11): dark red <-> bright orange,
#     slowly cycling (INFERNO_BREATH, 60 frames = 3 s per half-cycle
#     pair, 60 | 120).
#   Top fans 12/13 (the subject): bright yellow/orange flames that pulse
#     RANDOMLY (deterministic 120-entry INFERNO_FLICKER table, period
#     120 | 120): base INFERNO_TOP_BASE lerp'd toward near-white by
#     0.35+0.5*v, and a hard near-white spark (INFERNO_TOP_WHITE)
#     whenever the flicker table crosses INFERNO_SPARK_LEVEL.
#   The old board ring 0..5 dim ember is dropped — slots 0/1 pure black.
# Live 2..13 colored every frame (liveness); 14..239 dead, never painted.
INFERNO_FAN_DARK = (120, 24, 6)      # RING10 trough: dark red
INFERNO_FAN_HOT = (255, 130, 24)    # RING10 peak: bright orange
INFERNO_TOP_BASE = (255, 170, 30)   # top-fan flame base (bright yellow/orange)
INFERNO_TOP_WHITE = (255, 255, 250)  # white spark (near-white)
INFERNO_SPARK_LEVEL = 0.82           # flicker table >= this = white spark
INFERNO_PERIOD = 60                  # ring slow cycle; 60 | 120
INFERNO_BREATH = tuple(             # 60-entry 0..1 table: t=0 trough
    0.5 - 0.5 * math.cos(2.0 * math.pi * i / INFERNO_PERIOD)
    for i in range(INFERNO_PERIOD))
# deterministic pseudo-random flame flicker: 120 entries in 0..1 (a
# fixed-seed LCG — "random" pulses, bit-exact across runs, period 120)
def _inferno_flicker():
    out, s = [], 31
    for _ in range(PHASE_PERIOD):
        s = (1103515245 * s + 12345) & 0x7FFFFFFF
        out.append(((s >> 16) & 0x7FFF) / 32767.0)
    return tuple(out)
INFERNO_FLICKER = _inferno_flicker()


def inferno_frame(phase, params=None):
    """Option 31 INFERNO (re-based 2026-11-21 to RING10 + TOPFANS): the
    GPU sits underneath a furnace. The standard ring RING10 (slots
    2..11) slowly cycles dark red (INFERNO_FAN_DARK) -> bright orange
    (INFERNO_FAN_HOT) over 60 frames (the hot cycle, 60 | 120); the top
    fans 12/13 (the subject) pulse randomly like flames — a bright
    yellow/orange base (INFERNO_TOP_BASE) lerp'd toward near-white by the
    deterministic INFERNO_FLICKER table, with a hard near-white spark
    (INFERNO_TOP_WHITE) whenever the table crosses INFERNO_SPARK_LEVEL.
    The old board ring 0..5 dim ember is dropped — slots 0/1 pure black.
    Period 120 | 120; live 2..13 colored every frame (liveness); 14..239
    dead, never painted."""
    t = phase % PHASE_PERIOD
    b = INFERNO_BREATH[t % INFERNO_PERIOD]
    fan = _slerp(INFERNO_FAN_DARK, INFERNO_FAN_HOT, b)
    v = INFERNO_FLICKER[t]
    if v >= INFERNO_SPARK_LEVEL:
        top = INFERNO_TOP_WHITE
    else:
        top = _slerp(INFERNO_TOP_BASE, INFERNO_TOP_WHITE, 0.35 + 0.5 * v)
    out = bytearray(NUM_SLOTS * 3)
    for i in SPOTLIGHT_SLOTS:
        out[3 * i:3 * i + 3] = bytes(top)
    for i in RING10:
        out[3 * i:3 * i + 3] = bytes(fan)
    return bytes(out)


# 32 reactor-core (user 2026-11-17; re-based 2026-11-21 to RING10 +
# TOPFANS): the GPU as the POWER SOURCE of a futuristic reactor.
#   Standard ring RING10 (slots 2..11): deep blue, slowly pulsing —
#     REACTOR_FAN_DARK -> REACTOR_FAN_BRIGHT on a 60-frame breath
#     (t=0 trough, t=30 peak; 60 | 120).
#   Top fans 12/13 (the subject, the LEDs that shine down on the GPU):
#     bright cyan (REACTOR_TOP_BASE) that flashes BRIGHTER toward
#     near-white (REACTOR_TOP_FLASH) every 60 frames (3 s) on a 13-frame
#     raised-cosine pulse (REACTOR_PULSE: 0 at u=0, 1 at u=6, 0 at
#     u=12) — as if energy is being transferred into the GPU.
#   The old board ring 0..5 dim blue wash is dropped — slots 0/1 pure
#     black.
# Period 120 | 120 (precomputed tables, bit-exact); live 2..13 colored
# every frame (liveness); 14..239 dead, never painted.
REACTOR_FAN_DARK = (8, 16, 70)        # RING10 trough: deep blue
REACTOR_FAN_BRIGHT = (36, 96, 230)   # RING10 peak: brighter deep blue
REACTOR_TOP_BASE = (150, 235, 255)   # top-fan rest: bright cyan
REACTOR_TOP_FLASH = (255, 255, 250)  # top-fan flash: near-white
REACTOR_PERIOD = 60                  # breath + flash period; 60 | 120
REACTOR_BREATH = tuple(             # 60-entry 0..1 table: i=0 trough,
    0.5 - 0.5 * math.cos(2.0 * math.pi * i / REACTOR_PERIOD)   # i=30 peak
    for i in range(REACTOR_PERIOD))
# 60-entry 0..1 raised-cosine flash pulse: 13 frames wide (u 0..12, peak
# at u=6), one every 60 frames (3 s) — 2 flashes per 120-frame phase.
REACTOR_PULSE = tuple(
    (0.5 + 0.5 * math.cos(math.pi * (i - 6.0) / 6.0)) if i <= 12 else 0.0
    for i in range(REACTOR_PERIOD))


def reactor_core_frame(phase, params=None):
    """Option 32 REACTOR-CORE (re-based 2026-11-21 to RING10 + TOPFANS):
    the GPU as the power source of a futuristic reactor. The standard
    ring RING10 (slots 2..11): deep blue, slowly pulsing
    (REACTOR_FAN_DARK -> REACTOR_FAN_BRIGHT, 60-frame breath, 60 | 120).
    Top fans 12/13 (the subject): bright cyan (REACTOR_TOP_BASE) that
    flashes brighter toward near-white (REACTOR_TOP_FLASH) every 60
    frames (3 s) on a 13-frame raised-cosine pulse (REACTOR_PULSE) — as
    if energy is being transferred into the GPU. The old board ring 0..5
    dim blue wash is dropped — slots 0/1 pure black. Period 120 | 120,
    bit-exact; live 2..13 colored every frame (liveness); 14..239 dead,
    never painted."""
    t = phase % PHASE_PERIOD
    b = REACTOR_BREATH[t % REACTOR_PERIOD]
    p = REACTOR_PULSE[t % REACTOR_PERIOD]
    fan = _slerp(REACTOR_FAN_DARK, REACTOR_FAN_BRIGHT, b)
    top = _slerp(REACTOR_TOP_BASE, REACTOR_TOP_FLASH, p)
    out = bytearray(NUM_SLOTS * 3)
    for i in SPOTLIGHT_SLOTS:
        out[3 * i:3 * i + 3] = bytes(top)
    for i in RING10:
        out[3 * i:3 * i + 3] = bytes(fan)
    return bytes(out)


# 33 matrix (user 2026-11-17; re-based 2026-11-21 to RING10 + TOPFANS):
# a computer running a MASSIVE SIMULATION.
#   Standard ring RING10 (slots 2..11): very dark green (MATRIX_FAN_DARK)
#     — the machine at rest — that flashes BRIGHT green (MATRIX_FLASH)
#     across the whole ring once per 120-frame phase (6 s,
#     "occasionally") on an 11-frame raised-cosine bump (u=0..10, 0 at
#     u=0 and u=10, peak 1 at u=5; 0.55 s) — a data burst. The flash is
#     uniform across RING10.
#   Top fans 12/13 (the subject, the LEDs that shine down on the GPU):
#     BRIGHT NEON GREEN, slowly pulsing — MATRIX_TOP_DIM ->
#     MATRIX_TOP_BRIGHT on a 60-frame cosine breath (t=0 trough,
#     t=30 peak; 60 | 120), and lerp'd toward MATRIX_TOP_FLASH by the
#     same flash bump (the burst hits the top fans too).
#   The old board ring 0..5 dim green wash is dropped — slots 0/1 pure
#     black.
# Period 120 | 120 (precomputed tables, bit-exact); live 2..13 colored
# every frame (liveness); 14..239 dead, never painted.
MATRIX_FAN_DARK = (0, 28, 10)        # RING10: very dark green (rest)
MATRIX_FLASH = (0, 255, 96)         # bright green flash across RING10
MATRIX_TOP_DIM = (0, 120, 28)       # top-fan slow-pulse trough (dim neon)
MATRIX_TOP_BRIGHT = (0, 255, 64)    # top-fan slow-pulse peak (bright neon)
MATRIX_TOP_FLASH = (0, 255, 128)    # top-fan flash peak (brighter green)
MATRIX_BREATH_PERIOD = 60           # top-fan slow pulse; 60 | 120
MATRIX_BREATH = tuple(             # 60-entry 0..1 table: i=0 trough,
    0.5 - 0.5 * math.cos(2.0 * math.pi * i / MATRIX_BREATH_PERIOD)   # i=30 peak
    for i in range(MATRIX_BREATH_PERIOD))
# 120-entry 0..1 raised-cosine flash bump: nonzero for u=0..10 (11
# frames, 0.55 s), exactly 0 at u=0 and u=10, peak 1 at u=5 — one flash
# per 120-frame phase (6 s, "occasionally"), bit-exact.
MATRIX_FLASH_PULSE = tuple(
    (0.5 + 0.5 * math.cos(math.pi * (i - 5.0) / 5.0)) if i <= 10 else 0.0
    for i in range(PHASE_PERIOD))


def matrix_frame(phase, params=None):
    """Option 33 MATRIX (re-based 2026-11-21 to RING10 + TOPFANS): a
    computer running a massive simulation. The standard ring RING10
    (slots 2..11): very dark green (MATRIX_FAN_DARK) that flashes bright
    green (MATRIX_FLASH) across the whole ring once per 120-frame phase
    (6 s) on an 11-frame raised-cosine bump (peak at t=5; 0.55 s) — the
    data burst. Top fans 12/13 (the subject): bright neon green, slowly
    pulsing (MATRIX_TOP_DIM -> MATRIX_TOP_BRIGHT, 60-frame breath,
    60 | 120), lerp'd toward MATRIX_TOP_FLASH by the same flash bump.
    The old board ring 0..5 dim green wash is dropped — slots 0/1 pure
    black. Period 120 | 120 (bit-exact); live 2..13 colored every frame
    (liveness); 14..239 dead, never painted."""
    t = phase % PHASE_PERIOD
    b = MATRIX_BREATH[t % MATRIX_BREATH_PERIOD]
    p = MATRIX_FLASH_PULSE[t]
    fan = _slerp(MATRIX_FAN_DARK, MATRIX_FLASH, p)
    top = _slerp(MATRIX_TOP_DIM, MATRIX_TOP_BRIGHT, b)
    top = _slerp(top, MATRIX_TOP_FLASH, p)
    out = bytearray(NUM_SLOTS * 3)
    for i in SPOTLIGHT_SLOTS:
        out[3 * i:3 * i + 3] = bytes(top)
    for i in RING10:
        out[3 * i:3 * i + 3] = bytes(fan)
    return bytes(out)


# 34 cyberpunk (user 2026-11-17; re-based 2026-11-21 to RING10 +
# TOPFANS): futuristic neon aesthetic.
#   Standard ring RING10 (slots 2..11): purple (CYBER_FAN_PURPLE) slowly
#     transitioning to electric blue (CYBER_FAN_BLUE) on a 60-frame
#     cosine breath (t=0 purple, t=30 blue, t=60 back; 60 | 120).
#   Top fans 12/13 (the subject, the LEDs that shine down on the GPU):
#     electric cyan (CYBER_TOP_BASE) that flashes MAGENTA
#     (CYBER_TOP_FLASH) once per 120-frame phase (6 s, "occasionally")
#     on a 27-frame raised-cosine pulse (u=0..26, exactly 0 at u=0 and
#     u=26, peak 1 at u=13; 1.35 s).
#   The old board ring 0..5 dim purple->blue wash is dropped — slots
#     0/1 pure black.
# Period 120 | 120 (precomputed tables, bit-exact); live 2..13 colored
# every frame (liveness); 14..239 dead, never painted.
CYBER_FAN_PURPLE = (150, 40, 240)     # RING10: purple (rest)
CYBER_FAN_BLUE = (40, 90, 255)        # RING10: electric blue
CYBER_TOP_BASE = (0, 220, 255)        # top fans: electric cyan
CYBER_TOP_FLASH = (255, 20, 220)      # top-fan magenta flash peak
CYBER_PERIOD = 60                     # breath period; 60 | 120
CYBER_BREATH = tuple(                # 60-entry 0..1 table: i=0 purple,
    0.5 - 0.5 * math.cos(2.0 * math.pi * i / CYBER_PERIOD)   # i=30 blue
    for i in range(CYBER_PERIOD))
# 120-entry 0..1 raised-cosine magenta-flash pulse: nonzero for u=0..26
# (27 frames, 1.35 s), exactly 0 at u=0 and u=26, peak 1 at u=13 — one
# flash per 120-frame phase (6 s, "occasionally"), bit-exact.
CYBER_FLASH_PULSE = tuple(
    (0.5 + 0.5 * math.cos(math.pi * (i - 13.0) / 13.0)) if i <= 26 else 0.0
    for i in range(PHASE_PERIOD))


def cyberpunk_frame(phase, params=None):
    """Option 34 CYBERPUNK (re-based 2026-11-21 to RING10 + TOPFANS):
    futuristic neon aesthetic. The standard ring RING10 (slots 2..11):
    purple (CYBER_FAN_PURPLE) slowly transitioning to electric blue
    (CYBER_FAN_BLUE) on a 60-frame cosine breath (t=0 purple, t=30 blue,
    back; 60 | 120). Top fans 12/13 (the subject): electric cyan
    (CYBER_TOP_BASE) that flashes magenta (CYBER_TOP_FLASH) once per
    120-frame phase (6 s, "occasionally") on a 27-frame raised-cosine
    pulse (u=0..26, peak u=13). The old board ring 0..5 dim purple->blue
    wash is dropped — slots 0/1 pure black. Period 120 | 120 (bit-exact);
    live 2..13 colored every frame (liveness); 14..239 dead, never
    painted."""
    t = phase % PHASE_PERIOD
    b = CYBER_BREATH[t % CYBER_PERIOD]
    p = CYBER_FLASH_PULSE[t]
    fan = _slerp(CYBER_FAN_PURPLE, CYBER_FAN_BLUE, b)
    top = _slerp(CYBER_TOP_BASE, CYBER_TOP_FLASH, p)
    out = bytearray(NUM_SLOTS * 3)
    for i in SPOTLIGHT_SLOTS:
        out[3 * i:3 * i + 3] = bytes(top)
    for i in RING10:
        out[3 * i:3 * i + 3] = bytes(fan)
    return bytes(out)


# 35 cryogenic (user 2026-11-17; re-based 2026-11-21 to RING10 +
# TOPFANS): FROZEN GPU COOLING CHAMBER.
#   Standard ring RING10 (slots 2..11): dim icy blue (CRYO_FAN_DARK)
#     slowly fading toward icy white (CRYO_FAN_WHITE) and back — a
#     60-frame cosine breath (t=0 blue trough, t=30 white peak;
#     60 | 120).
#   Top fans 12/13 (the subject, the LEDs that shine down on the GPU):
#     BRIGHT WHITE (CRYO_TOP_BASE) that flashes pure white
#     (CRYO_TOP_FLASH) once per 120-frame phase (6 s, "occasionally")
#     on an 11-frame raised-cosine pulse (u=0..10, exactly 0 at u=0
#     and u=10, peak 1 at u=5; 0.55 s).
#   The old board ring 0..5 dim icy-blue wash is dropped — slots 0/1
#     pure black.
# Period 120 | 120 (precomputed tables, bit-exact); live 2..13 colored
# every frame (liveness); 14..239 dead, never painted.
CRYO_FAN_DARK = (40, 84, 180)         # RING10: dim icy blue (rest)
CRYO_FAN_WHITE = (235, 245, 255)      # RING10: icy white (breath peak)
CRYO_TOP_BASE = (215, 230, 250)       # top fans: bright white (rest)
CRYO_TOP_FLASH = (255, 255, 255)      # top-fan white flash peak (pure)
CRYO_PERIOD = 60                      # breath period; 60 | 120
CRYO_BREATH = tuple(                 # 60-entry 0..1 table: i=0 blue,
    0.5 - 0.5 * math.cos(2.0 * math.pi * i / CRYO_PERIOD)   # i=30 white
    for i in range(CRYO_PERIOD))
# 120-entry 0..1 raised-cosine white-flash pulse: nonzero for u=0..10
# (11 frames, 0.55 s), exactly 0 at u=0 and u=10, peak 1 at u=5 — one
# flash per 120-frame phase (6 s, "occasionally"), bit-exact.
CRYO_FLASH_PULSE = tuple(
    (0.5 + 0.5 * math.cos(math.pi * (i - 5.0) / 5.0)) if i <= 10 else 0.0
    for i in range(PHASE_PERIOD))


def cryogenic_frame(phase, params=None):
    """Option 35 CRYOGENIC (re-based 2026-11-21 to RING10 + TOPFANS):
    frozen GPU cooling chamber. The standard ring RING10 (slots 2..11):
    dim icy blue (CRYO_FAN_DARK) slowly fading toward icy white
    (CRYO_FAN_WHITE) and back on a 60-frame cosine breath (t=0 blue
    trough, t=30 white peak; 60 | 120). Top fans 12/13 (the subject):
    bright white (CRYO_TOP_BASE) that flashes pure white
    (CRYO_TOP_FLASH) once per 120-frame phase (6 s, "occasionally") on
    an 11-frame raised-cosine pulse (u=0..10, exactly 0 at u=0 and
    u=10, peak 1 at u=5). The old board ring 0..5 dim icy-blue wash is
    dropped — slots 0/1 pure black. Period 120 | 120 (bit-exact);
    live 2..13 colored every frame (liveness); 14..239 dead, never
    painted."""
    t = phase % PHASE_PERIOD
    b = CRYO_BREATH[t % CRYO_PERIOD]
    p = CRYO_FLASH_PULSE[t]
    fan = _slerp(CRYO_FAN_DARK, CRYO_FAN_WHITE, b)
    top = _slerp(CRYO_TOP_BASE, CRYO_TOP_FLASH, p)
    out = bytearray(NUM_SLOTS * 3)
    for i in SPOTLIGHT_SLOTS:
        out[3 * i:3 * i + 3] = bytes(top)
    for i in RING10:
        out[3 * i:3 * i + 3] = bytes(fan)
    return bytes(out)


# 36 emergency-mode (user 2026-11-17; re-based 2026-11-21 to RING10 +
# TOPFANS): EMERGENCY RESPONSE SYSTEM.
#   Standard ring RING10 (slots 2..11): dark blue (EMG_FAN_DARK) that
#     strobes red/amber — hard blink (strobe), not a fade: t=0 red
#     (EMG_FAN_RED), t=1 amber (EMG_FLASH_AMBER), t=2 red — 3 frames
#     (150 ms) once per 120-frame phase (6 s, "occasionally").
#   Top fans 12/13 (the subject): strobe IN SYNC with the ring — the same
#     red/amber flash frames; otherwise the dim red/amber base
#     (EMG_TOP_BASE, the floor — never black, spec 3.2 strobe rule).
#   The old board ring 0..5 is dropped — slots 0/1 pure black.
# Period 120 | 120 (static + hard blinks, bit-exact); live 2..13 colored
# every frame (liveness); 14..239 dead, never painted.
EMG_FAN_DARK = (20, 40, 120)         # RING10: dark blue (rest)
EMG_FAN_RED = (255, 40, 20)         # RING10 + 12/13: flash frames t=0,2
EMG_FLASH_AMBER = (255, 165, 0)    # RING10 + 12/13: flash frame t=1
EMG_TOP_BASE = (96, 48, 0)          # 12/13: dim red/amber base (floor)
EMG_FLASH_LEN = 3                   # red/amber strobe frames per phase


def emergency_mode_frame(phase, params=None):
    """Option 36 EMERGENCY-MODE (re-based 2026-11-21 to RING10 + TOPFANS):
    emergency response system. The standard ring RING10 (slots 2..11)
    sits dark blue (EMG_FAN_DARK) and strobes red/amber — t=0 red
    (EMG_FAN_RED), t=1 amber (EMG_FLASH_AMBER), t=2 red: 3 frames
    (150 ms) once per 120-frame phase (6 s, "occasionally"); hard blink
    (strobe). Top fans 12/13 (the subject) strobe IN SYNC with the ring
    — the same red/amber flash frames; otherwise the dim red/amber base
    (EMG_TOP_BASE — the floor, never black, spec 3.2 strobe rule). The
    old board ring 0..5 is dropped — slots 0/1 pure black. Period 120 |
    120 (bit-exact); live 2..13 colored every frame (liveness); 14..239
    dead, never painted."""
    t = phase % PHASE_PERIOD
    flash = t < EMG_FLASH_LEN
    out = bytearray(NUM_SLOTS * 3)
    for i in RING10:
        if flash:
            c = (EMG_FAN_RED, EMG_FLASH_AMBER, EMG_FAN_RED)[t]
        else:
            c = EMG_FAN_DARK
        out[3 * i:3 * i + 3] = bytes(c)
    for i in TOPFANS:
        if flash:
            c = (EMG_FAN_RED, EMG_FLASH_AMBER, EMG_FAN_RED)[t]
        else:
            c = EMG_TOP_BASE
        out[3 * i:3 * i + 3] = bytes(c)
    return bytes(out)


# 37 lightning-strike (user 2026-11-18; re-based 2026-11-21 to RING10 +
# TOPFANS): a LIGHTNING STRIKE.
#   Top fans 12/13 (the strike point, the subject): dim blue
#     (LS_TOP_DARK, >= BEACON_BG floor) that flash pure white
#     (LS_TOP_FLASH) on t=0..1 — 2 frames (100 ms) — the strike itself
#     ("flare on strikes").
#   Standard ring RING10 (slots 2..11): dark blue (LS_FAN_DARK) that,
#     "a split second" later, flashes the cyan wash (LS_FAN_FLASH) on
#     t=2..3 — 2 frames (100 ms). Hard flash (not a fade).
#   "Everything returns to dark": after t=3 every ring holds its dark
#     rest color for the rest of the phase (never full black —
#     liveness).
#   The old board ring 0..5 wash/ripple is dropped — slots 0/1 pure
#     black.
# Period 120 | 120 (static + hard flashes, bit-exact); live 2..13 colored
# every frame (liveness); 14..239 dead, never painted.
LS_FAN_DARK = (18, 34, 110)          # RING10: dark blue (rest)
LS_FAN_FLASH = (90, 170, 255)       # RING10: cyan wash flash
LS_TOP_DARK = (48, 48, 130)         # 12/13: dim blue (rest, >= floor)
LS_TOP_FLASH = (255, 255, 255)      # 12/13: pure white flash (the strike)
LS_TOP_FLASH_LEN = 2                 # white flash frames: t=0..1 (100 ms)
LS_FAN_FLASH_DELAY = 2               # "split second" before the blue flash
LS_FAN_FLASH_LEN = 2                 # cyan flash frames: t=2..3 (100 ms)


def lightning_strike_frame(phase, params=None):
    """Option 37 LIGHTNING-STRIKE (re-based 2026-11-21 to RING10 +
    TOPFANS): a lightning strike. Top fans 12/13 (the strike point, the
    subject): dim blue (LS_TOP_DARK — the floor, >= BEACON_BG) that
    flare pure white (LS_TOP_FLASH) on t=0..1 — 2 frames (100 ms) —
    the strike itself. The standard ring RING10 (slots 2..11) sits dark
    blue (LS_FAN_DARK) and, "a split second" later, flashes the cyan
    wash (LS_FAN_FLASH) on t=2..3 — 2 frames (100 ms), hard flash (not
    a fade). The old board ring 0..5 dim blue wash + ripple is dropped —
    slots 0/1 pure black. Everything returns to dark: after t=3 every
    ring holds its dark rest color for the rest of the phase (never
    full black — liveness). Period 120 | 120 (bit-exact); live 2..13
    colored every frame; 14..239 dead, never painted."""
    t = phase % PHASE_PERIOD
    top_flash = t < LS_TOP_FLASH_LEN
    fan_flash = (LS_FAN_FLASH_DELAY <= t
                 < LS_FAN_FLASH_DELAY + LS_FAN_FLASH_LEN)
    top = LS_TOP_FLASH if top_flash else LS_TOP_DARK
    fan = LS_FAN_FLASH if fan_flash else LS_FAN_DARK
    out = bytearray(NUM_SLOTS * 3)
    for i in TOPFANS:
        out[3 * i:3 * i + 3] = bytes(top)
    for i in RING10:
        out[3 * i:3 * i + 3] = bytes(fan)
    return bytes(out)


# 38 sweeping-rainbow (user 2026-11-18; re-based 2026-11-21 to RING10 +
# TOPFANS): a SWEEPING RAINBOW.
#   Standard ring RING10 (slots 2..11): the fans start in the current
#     rainbow base color, and a slow radar-like sweep of white light
#     converts them to orange as it passes: the head slot is pure white
#     (SR_WHITE), the slot the head just left decays white -> orange
#     over 12 frames, everything already swept is orange (SR_ORANGE),
#     and the unswept slots ahead of the head show the current base
#     color. One slot per 12 frames (0.6 s/slot), one full pass around
#     RING10 per 120-frame phase (6 s) — the slow radar on the 10-slot
#     ring (120 | 120, seamless). The base color cycles through ALL
#     SEVEN colors of the rainbow in order (red -> orange -> yellow ->
#     green -> blue -> indigo -> violet): red 18 frames (t=0..17), then
#     6 x 17 frames (18 + 102 = 120, seamless loop); the last SR_BLEND
#     frames of each segment lerp to the next color. "This repeats for
#     all the colors of the rainbow": every color starts the unswept
#     ring, and the sweep converts it to orange.
#   Top fans 12/13 (the subject): track the head color DIM — half-
#     brightness of the base color the head is on, floored to BEACON_BG
#     (never black).
#   The old board ring 0..5 mirror is dropped — slots 0/1 pure black.
#   Period 120 | 120 (sweep pass 120 | 120); live 2..13 colored every
#   frame (liveness); 14..239 dead, never painted.
SR_RED = (255, 40, 0)            # rainbow 1: red
SR_ORANGE = (255, 165, 0)       # rainbow 2: orange (also the sweep result)
SR_YELLOW = (255, 230, 0)       # rainbow 3: yellow
SR_GREEN = (40, 220, 60)        # rainbow 4: green
SR_BLUE = (50, 90, 255)         # rainbow 5: blue
SR_INDIGO = (75, 0, 130)        # rainbow 6: indigo
SR_VIOLET = (190, 60, 220)      # rainbow 7: violet
SR_COLORS = (SR_RED, SR_ORANGE, SR_YELLOW, SR_GREEN,
             SR_BLUE, SR_INDIGO, SR_VIOLET)
SR_WHITE = (255, 255, 255)      # the radar sweep (head)
SR_SEG_START = (0, 18, 35, 52, 69, 86, 103)   # start frame of colors 1..7
SR_SWEEP_PERIOD = 120           # one full RING10 pass per phase; 120 | 120
SR_SWEEP_SLOT_FRAMES = 12       # 1 slot per 12 frames (0.6 s/slot, slow)
SR_BLEND = 4                    # fan base: fade frames to the next color
SR_TOP_DIM = 0.5                # 12/13: dim factor on the head/base color


def _sr_segment(t):
    """Index (0..6) of the base-color segment at phase t (0..119). Pure."""
    seg = 6
    for i, start in enumerate(SR_SEG_START):
        if t >= start:
            seg = i
    return seg


def _sr_segment_end(seg):
    """End frame (exclusive) of base-color segment seg (0..6). Pure."""
    return SR_SEG_START[seg + 1] if seg < 6 else PHASE_PERIOD


def _sr_base(t):
    """Base color of the unswept fans at phase t (0..119): the seven
    rainbow colors in order — red 18 frames, then 6 x 17 frames
    (18 + 102 = 120, seamless loop); the last SR_BLEND frames of each
    segment lerp to the next color. Pure."""
    seg = _sr_segment(t)
    end = _sr_segment_end(seg)
    a, b = SR_COLORS[seg], SR_COLORS[(seg + 1) % 7]
    if t >= end - SR_BLEND:
        return _slerp(a, b, (t - (end - SR_BLEND)) / float(SR_BLEND))
    return a


def sweeping_rainbow_frame(phase, params=None):
    """Option 38 SWEEPING-RAINBOW (re-based 2026-11-21 to RING10 +
    TOPFANS): the full rainbow sweep. The standard ring RING10 (slots
    2..11) starts in the current rainbow base color, and a slow
    radar-like sweep of white light converts them to orange as it
    passes — the head slot is pure white (SR_WHITE), the slot the head
    just left decays white -> orange over 12 frames, everything already
    swept is orange (SR_ORANGE), and the unswept slots ahead of the
    head show the current base color (one slot per 12 frames, one full
    pass around RING10 per 120-frame phase — 6 s). The base color
    cycles through all the colors of the rainbow in order (red ->
    orange -> yellow -> green -> blue -> indigo -> violet; red 18
    frames, then 6 x 17 frames, seamless 120-frame loop; each segment
    fades to the next over the last SR_BLEND frames) — "this repeats
    for all the colors of the rainbow". Top fans 12/13 (the subject)
    track the head color DIM: half-brightness of the base color the
    head is on, floored to BEACON_BG (never black). The old board ring
    0..5 mirror is dropped — slots 0/1 pure black. Period 120 | 120;
    live 2..13 colored every frame (liveness); 14..239 dead, never
    painted."""
    t = phase % PHASE_PERIOD
    h = t // SR_SWEEP_SLOT_FRAMES
    base = _sr_base(t)
    out = bytearray(NUM_SLOTS * 3)
    for s, i in enumerate(RING10):
        if s == h:
            c = SR_WHITE
        elif s > h:
            c = base
        else:
            age = t - (s + 1) * SR_SWEEP_SLOT_FRAMES
            c = _slerp(SR_ORANGE, SR_WHITE,
                       1.0 - age / float(SR_SWEEP_SLOT_FRAMES))
        out[3 * i:3 * i + 3] = bytes(c)
    c12 = _on_base(tuple(int(ch * SR_TOP_DIM) for ch in base))
    for i in TOPFANS:
        out[3 * i:3 * i + 3] = bytes(c12)
    return bytes(out)


# 39 plasma-storm (user 2026-11-18; re-based 2026-11-21 to RING10 +
# TOPFANS): a PLASMA STORM.
#   Standard ring RING10 (slots 2..11): every slot flickers
#     INDEPENDENTLY between dark violet (PS_FAN_DARK) and bright purple
#     (PS_FAN_BRIGHT) — one fixed-seed brightness walk per slot
#     (PS_BRIGHTNESS), so the ten ring positions flicker out of sync
#     with each other.
#   Top fans 12/13 (the subject): a dim violet base (PS_TOP_BASE,
#     >= BEACON_BG floor) that flares to the near-white flash
#     (PS_FLASH) on the bursts.
#   "Randomize brightness every 100-300 ms": each walk holds each
#     brightness for a random 2..6 frames (100..300 ms at 20 FPS);
#     deterministic fixed-seed LCG tables (120 entries, period 120 | 120,
#     bit-exact across runs — the creative-patterns "no runtime
#     randomness" convention), and the final hold of each walk takes the
#     FIRST brightness so the 120-frame wrap is seamless.
#   "Occasionally trigger a full-case flash": once per 120-frame phase
#     (6 s, the same "occasionally" rate as matrix 33 / cyberpunk 34)
#     at t = PS_FLASH_START..+PS_FLASH_LEN every live slot 2..13 flashes
#     near-white (PS_FLASH) — the intentional hard blink.
#   The old board ring 0..5 dim violet wash is dropped — slots 0/1 pure
#     black. Live 2..13 colored every frame (liveness); 14..239 dead,
#     never painted.
PS_FAN_DARK = (48, 8, 88)             # RING10 flicker trough: dark violet
PS_FAN_BRIGHT = (200, 60, 255)       # RING10 flicker peak: bright purple
PS_TOP_BASE = (52, 36, 120)          # 12/13: dim violet base (>= floor)
PS_FLASH = (255, 255, 250)           # full-case flash: near-white
PS_FLASH_START = 96                  # flash once per 120-frame phase (6 s)
PS_FLASH_LEN = 6                     # 6 frames (0.3 s) of full-case flash


def _ps_walk(seed):
    """One slot's 120-entry 0..1 brightness walk: each brightness is held
    for a random 2..6 frames (100..300 ms at 20 FPS), then a new random
    brightness — "randomize brightness every 100-300 ms". Fixed-seed
    LCG (bit-exact across runs; period 120 | 120). The final hold takes
    the FIRST brightness, so the table wraps seamlessly: v[-1] == v[0],
    and every circular run of equal values is 2..12 frames long."""
    s = seed

    def rnd():
        nonlocal s
        s = (1103515245 * s + 12345) & 0x7FFFFFFF
        return ((s >> 16) & 0x7FFF) / 32767.0
    first_len = 2 + int(rnd() * 5)           # 2..6 frames
    v0 = rnd()
    table = [v0] * first_len
    total = first_len
    while total + 1 < PHASE_PERIOD:
        d = 2 + int(rnd() * 5)               # next hold 2..6 frames
        v = rnd()
        if total + d > PHASE_PERIOD - 1:
            break                             # leave room for the seam hold
        table.extend([v] * d)
        total += d
    # seam hold: wrap into the first brightness -> v[-1] == v[0] (the
    # first hold simply continues across the 120-frame seam)
    table.extend([v0] * (PHASE_PERIOD - total))
    return tuple(table[:PHASE_PERIOD])


PS_BRIGHTNESS = tuple(_ps_walk(101 + 17 * i) for i in range(len(RING10)))


def plasma_storm_frame(phase, params=None):
    """Option 39 PLASMA-STORM (re-based 2026-11-21 to RING10 + TOPFANS):
    a plasma storm. The standard ring RING10 (slots 2..11): every slot
    flickers INDEPENDENTLY between dark violet (PS_FAN_DARK) and bright
    purple (PS_FAN_BRIGHT) — one fixed-seed brightness walk per slot
    (PS_BRIGHTNESS), so the ten ring positions flicker out of sync with
    each other. Top fans 12/13 (the subject): a dim violet base
    (PS_TOP_BASE — the floor, >= BEACON_BG) that flares to the near-white
    flash (PS_FLASH) on the bursts. "Randomize brightness every 100-300
    ms": each walk holds each brightness for a random 2..6 frames
    (100..300 ms at 20 FPS); all walks are deterministic fixed-seed 120-
    entry tables (period 120 | 120, seamless wrap v[-1] == v[0], no
    runtime randomness). "Occasionally trigger a full-case flash": once
    per 120-frame phase (6 s) at t = PS_FLASH_START..+PS_FLASH_LEN every
    live slot 2..13 is near-white (PS_FLASH) — the intentional hard
    blink. The old board ring 0..5 dim violet wash is dropped — slots
    0/1 pure black. Live 2..13 colored every frame (liveness); 14..239
    dead, never painted."""
    t = phase % PHASE_PERIOD
    out = bytearray(NUM_SLOTS * 3)
    if PS_FLASH_START <= t < PS_FLASH_START + PS_FLASH_LEN:
        c = PS_FLASH
        for i in range(2, 14):
            out[3 * i:3 * i + 3] = bytes(c)
        return bytes(out)
    for j, i in enumerate(RING10):
        v = PS_BRIGHTNESS[j][t]
        c = _slerp(PS_FAN_DARK, PS_FAN_BRIGHT, v)
        out[3 * i:3 * i + 3] = bytes(c)
    for i in TOPFANS:
        out[3 * i:3 * i + 3] = bytes(PS_TOP_BASE)
    return bytes(out)


# ---- Re-based preview features 1-5 (re-based 2026-11-21 to RING10 +
# TOPFANS) ------
# Ring = RING10 (slots 2..11, P10, +i, 1 slot/frame). TOPFANS 12/13 are
# painted every frame of every pattern (spec 3.2): static dim dark blue
# for 1/2, dim amber base + f-blend flare for 3/4/5. Slots 0..1 (live,
# unassigned) and 14..239 (dead) stay pure black. The old board ring
# (LIVE_ONBOARD 0..5) content is retired — 2..5 are ring positions; the
# old fan ring (SWEEP_FAN 6..11) is a subset of RING10. The legacy
# constants are retired (removed 2026-11-21, final task).
DARK_BLUE_DIM = (48, 24, 70)   # dim dark blue for 12/13 on patterns 1/2
                               # (>= BEACON_BG floor)
def darkblue_frame(phase, params=None):
    """Pattern 1 DARKBLUE (re-based 2026-11-21 to RING10 + TOPFANS):
    static dark blue on the standard ring RING10 (slots 2..11); TOPFANS
    12/13 hold a static dim dark blue (>= BEACON_BG floor); slots 0..1
    (live, unassigned) and 14..239 (dead) stay pure black. Static
    (phase-independent)."""
    out = bytearray(NUM_SLOTS * 3)
    for i in RING10:
        out[3 * i:3 * i + 3] = bytes(DARK_BLUE)
    for i in TOPFANS:
        out[3 * i:3 * i + 3] = bytes(DARK_BLUE_DIM)
    return bytes(out)


def perfan_frame(phase, params=None):
    """Pattern 2 PERFAN (re-based 2026-11-21 to RING10 + TOPFANS): the
    6-color ring recast on RING10 — 10 slots, the 6-color sequence
    repeats (ring slot s shows PERFAN[(s - ph) % 6]), +i, 1 slot/frame;
    pattern period 30 (LCM(10, 6) | 120). TOPFANS 12/13 hold a static
    dim blue (>= BEACON_BG floor); slots 0..1 (live, unassigned) and
    14..239 (dead) stay pure black."""
    out = bytearray(NUM_SLOTS * 3)
    for s, i in enumerate(RING10):
        out[3 * i:3 * i + 3] = bytes(PERFAN[(s - phase) % 6])
    for i in TOPFANS:
        out[3 * i:3 * i + 3] = bytes(DARK_BLUE_DIM)
    return bytes(out)


def chase_cool_frame(phase, params=None):
    """Pattern 3 CHASE_COOL (re-based 2026-11-21 to RING10 + TOPFANS): a
    3-color comet chase (blue/amber/yellow) on the standard ring RING10
    — three 2-slot color blocks + 4 dim base slots, +i, 1 slot/frame,
    period 10 (| 120). TOPFANS 12/13 hold the dim amber base and flare
    toward a block's color when a block is on slot 10 (f=1.0) or 9/11
    (f=0.5) — spec 3.2 motion rule, f = max over the three blocks. Slots
    0..1 (live, unassigned) and 14..239 (dead) stay pure black. The old
    'both rings' (board 0..5 + fan 6..11) version is retired: single
    RING10."""
    out = bytearray(NUM_SLOTS * 3)
    for s, i in enumerate(RING10):
        out[3 * i:3 * i + 3] = bytes(chase(s, phase, PALETTE_COOL))
    tf = _chase_topfan(phase, PALETTE_COOL)
    for i in TOPFANS:
        out[3 * i:3 * i + 3] = bytes(tf)
    return bytes(out)


def chase_warm_frame(phase, params=None):
    """Pattern 4 CHASE_WARM (re-based 2026-11-21 to RING10 + TOPFANS):
    same 3-color comet chase as pattern 3 but the warm palette
    (yellow/amber/red) on the standard ring RING10 — three 2-slot color
    blocks + 4 dim base slots, +i, 1 slot/frame, period 10 (| 120);
    TOPFANS 12/13 dim amber base + flare (f = max over the three blocks);
    slots 0..1 (live, unassigned) and 14..239 (dead) stay pure black."""
    out = bytearray(NUM_SLOTS * 3)
    for s, i in enumerate(RING10):
        out[3 * i:3 * i + 3] = bytes(chase(s, phase, PALETTE_WARM))
    tf = _chase_topfan(phase, PALETTE_WARM)
    for i in TOPFANS:
        out[3 * i:3 * i + 3] = bytes(tf)
    return bytes(out)


# Preview states: name -> (frame_fn, representative RGB for state-file
# logging). "off" is handled separately (BLACK_FRAME). Any new preview
# state MUST be registered here AND in the read_override() whitelist, or
# the daemon silently ignores it. NOTE: "sweep", the two
# gpu-spotlight-* states, and "red_white" are ALSO the temp/power drive
# model's levels 2, 0/1 and 3 (see LEVEL_FRAME_FNS below frame_colors).
PREVIEW_STATES = {
    "darkblue": (darkblue_frame, DARK_BLUE),
    "perfan": (perfan_frame, PERFAN[0]),
    "chase_cool": (chase_cool_frame, PALETTE_COOL[0]),
    "chase_warm": (chase_warm_frame, PALETTE_WARM[0]),
    "beacon": (beacon_frame, BEACON_ARC),
    "sweep": (sweep_frame, SWEEP_HEAD),
    "tandem": (tandem_frame, TANDEM_HEAD),
    "heartbeat": (heartbeat_frame, HEARTBEAT_RING),
    "comet": (comet_frame, COMET_HEAD),
    "hazard": (hazard_frame, HAZARD_HEAD_EVEN),
    "bitclock": (bitclock_frame, BITCLOCK_ON),
    "telegraph": (telegraph_frame, TELEGRAPH_DOT),
    "fireflies": (fireflies_frame, FIREFLY_CORE),
    "lava": (lava_frame, LAVA_BLOBS[0][0]),
    "sonar": (sonar_frame, SONAR_RING),
    "metronome": (metronome_frame, METRO_MARK),
    "orbit": (orbit_frame, ORBIT_PLANET),
    "bitstream": (bitstream_frame, BITSTREAM_ONE),
    "sparkler": (sparkler_frame, SPARK_FUSE),
    "echo": (echo_frame, ECHO_CORE),
    "ecg": (ecg_frame, ECG_R),
    "walk": (walk_frame, WALK_BODY),
    "gpu-spotlight-idle": (gpu_spotlight_idle_frame, SPOT_IDLE_PALETTE[0]),
    "gpu-spotlight-fire": (gpu_spotlight_fire_frame, SPOT_FIRE_PALETTE[1]),
    "gpu-spotlight-somewhat-active": (
        gpu_spotlight_somewhat_active_frame, SPOT_ACTIVE_PALETTE[2]),
    "red_white": (red_white_frame, RED_WHITE_RED),
    "rainbow": (rainbow_frame, RAINBOW_COLORS[0]),
    "limelight": (limelight_frame, LIMELIGHT_GPU_TOP),
    "catwalk": (catwalk_frame, CATWALK_HEAD),
    "supernova": (supernova_frame, SNOVA_FLASH),
    "inferno": (inferno_frame, INFERNO_FAN_HOT),
    "reactor-core": (reactor_core_frame, REACTOR_TOP_BASE),
    "matrix": (matrix_frame, MATRIX_FLASH),
    "cyberpunk": (cyberpunk_frame, CYBER_TOP_BASE),
    "cryogenic": (cryogenic_frame, CRYO_TOP_BASE),
    "emergency-mode": (emergency_mode_frame, EMG_FAN_DARK),
    "lightning-strike": (lightning_strike_frame, LS_FAN_DARK),
    "sweeping-rainbow": (sweeping_rainbow_frame, SR_RED),
    "plasma-storm": (plasma_storm_frame, PS_FAN_DARK),
    "ledprobe": (ledprobe_frame, LEDPROBE_ON),
}
PREVIEW_STATE_NAMES = frozenset(PREVIEW_STATES) | {"off"}


# ---- Drive model (user 2026-11-16): level -> state ----------------------
# The systemd gpu-rgb service drives these four states by level
# (level 0 = "zone 1", ..., level 3 = "zone 4"); adjacent levels
# crossfade smoothly per slot:
#   level 0 (zone 1): gpu-spotlight-idle          cold / low power
#   level 1 (zone 2): gpu-spotlight-somewhat-active
#   level 2 (zone 3): sweep (pattern 6)
#   level 3 (zone 4): red_white (pattern 26)      hot
# The old A/B/C/D temp states (darkblue/perfan/chase_cool/chase_warm)
# are no longer temp-driven — still previewable as patterns 1-4.
LEVEL_STATE_NAMES = ("gpu-spotlight-idle",
                     "gpu-spotlight-somewhat-active",
                     "sweep", "red_white")
LEVEL_STATE_RGB = (SPOT_IDLE_PALETTE[0], SPOT_ACTIVE_PALETTE[2],
                   SWEEP_HEAD, RED_WHITE_RED)
LEVEL_FRAME_FNS = (gpu_spotlight_idle_frame,
                   gpu_spotlight_somewhat_active_frame,
                   sweep_frame,
                   red_white_frame)

# ---- led-mapper config: level -> arbitrary pattern (2026-11-20) ----------
# level_map.json (written by led-mapper, no LLM) maps each drive level 0..3
# to ANY PREVIEW_STATES pattern name. Missing file, bad JSON, unknown level
# key, or unknown state name -> that level falls back to its default. The
# serve loop re-reads it every sensor poll (~5 s), so a `led-mapper set`
# applies live without a service restart.
LEVEL_MAP_FILE = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                              "level_map.json")


def load_level_map():
    """level_map.json -> {"0": "darkblue", ...}; {} on any problem. Pure."""
    try:
        with open(LEVEL_MAP_FILE) as f:
            data = json.load(f)
    except (OSError, ValueError):
        return {}
    out = {}
    if isinstance(data, dict):
        for lvl, name in data.items():
            if (str(lvl) in ("0", "1", "2", "3")
                    and isinstance(name, str)
                    and name in PREVIEW_STATES):
                out[str(lvl)] = name
    return out


def drive_level_states(level_map):
    """Per-level (frame_fn, name, rgb) for the drive model: the led-mapper
    pattern when set, else the level default. Pure."""
    states = []
    for i, default in enumerate(LEVEL_STATE_NAMES):
        name = level_map.get(str(i), default)
        fn, rgb = PREVIEW_STATES[name]
        states.append((fn, name, rgb))
    return states


def frame_colors(weights, phase, states=None):
    """720 bytes (240 RGB slots) for the 0x53 packet. Pure.
    Blends the four drive-model states by weight: at an exact level the
    frame is exactly that level's state's frame; between adjacent levels
    the two states crossfade per slot. states = drive_level_states(...)
    (led-mapper aware); None = the level defaults (offline checks)."""
    if states is None:
        states = drive_level_states({})
    wA, wB, wC, wD = weights
    frames = [fn(phase) for fn, _, _ in states]
    out = bytearray(NUM_SLOTS * 3)
    for i in range(NUM_SLOTS):
        b = 3 * i
        for ch in range(3):
            out[b + ch] = int(wA * frames[0][b + ch]
                              + wB * frames[1][b + ch]
                              + wC * frames[2][b + ch]
                              + wD * frames[3][b + ch])
    return bytes(out)


def build_perled_packet(frame):
    p = bytearray(PERLED_PKT)
    p[0] = PERLED_REPORT_ID
    p[1] = 0x25
    p[2] = 0x06
    p[3] = 0x00
    p[4] = 0x00
    p[5:5 + NUM_SLOTS * 3] = frame
    return bytes(p)


def state_label(weights, states=None):
    if states is None:
        states = drive_level_states({})
    idx = max(range(4), key=lambda k: weights[k])
    return states[idx][1], states[idx][2]


# ---- temp + power reading ----------------------------------------------------

def read_sensors():
    """(temp, power_watts) from one nvidia-smi call; None on failure."""
    try:
        out = subprocess.run(
            ["nvidia-smi", "--query-gpu=temperature.gpu,power.draw",
             "--format=csv,noheader"],
            capture_output=True, text=True, timeout=10)
        if out.returncode != 0:
            return None, None
        fields = out.stdout.strip().splitlines()[0].split(",")
        temp = int(fields[0].strip().split()[0])
        power = float(fields[1].strip().split()[0])
        return temp, power
    except (OSError, ValueError, IndexError, subprocess.TimeoutExpired):
        return None, None


def read_temp():
    t, _p = read_sensors()
    return t


# ---- state file --------------------------------------------------------------

# Fixed path, NOT env-dependent: the systemd unit (no XDG_RUNTIME_DIR) and
# CLI shells (XDG set) must always resolve the same file.
STATE_FILE = "/var/tmp/gpu_rgb.state"
OVERRIDE_FILE = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                             ".override")


def state_write(name, rgb, temp, extra=None):
    try:
        data = {"color": name, "rgb": list(rgb), "temp": temp,
                "updated": int(time.time())}
        if extra:
            data.update(extra)
        with open(STATE_FILE, "w") as f:
            json.dump(data, f)
    except OSError as e:
        log("warning: state file write failed: %s" % e)


def state_read():
    try:
        with open(STATE_FILE) as f:
            return json.load(f)
    except (OSError, ValueError):
        return None


# ---- commands ----------------------------------------------------------------

def read_override():
    """(state name, params) from the override file; (None, None) if absent.
    Preview-only states (beacon + the 6-10 beacon options, and off) are NOT
    in STATE_NAMES but are valid overrides; beacon's optional params
    ({"base": [r,g,b], "period": N, "arc": N}) let a preview tweak the look
    live without a restart.
    NOTE: any new preview-only state MUST be added to PREVIEW_STATES (and
    thus PREVIEW_STATE_NAMES) below, or the daemon silently ignores the
    override and renders temp-driven state.
    Preview states may carry live params under their own key (beacon:
    {"base": [r,g,b]}; ledprobe: {"index": i}); they flow through as the
    second return value."""
    try:
        with open(OVERRIDE_FILE) as f:
            data = json.load(f)
        name = data.get("state")
        if not (name in STATE_NAMES.values()
                or name in PREVIEW_STATE_NAMES):
            return None, None
        params = data.get(name) if name in PREVIEW_STATES else None
        return name, params
    except (OSError, ValueError):
        return None, None


def write_override(state_name):
    with open(OVERRIDE_FILE, "w") as f:
        json.dump({"state": state_name}, f)


def cmd_status():
    temp, pwr = read_sensors()
    st = state_read()
    cur = st.get("color", "unknown") if st else "unknown"
    rgb = st.get("rgb") if st else None
    rgb_s = " ".join(str(x) for x in rgb) if rgb else "?"
    when = st.get("updated") if st else None
    when_s = (time.strftime("%Y-%m-%d %H:%M:%S", time.localtime(when))
              if when else "?")
    print("gpu temp : %s" % ("%d C" % temp if temp is not None else "unavailable"))
    print("gpu power: %s" % ("%d W" % pwr if pwr is not None else "unavailable"))
    print("set color: %s  (RGB %s)" % (cur, rgb_s))
    if st:
        print("updated  : %s" % when_s)
    ov, ov_params = read_override()
    if ov:
        extra = ""
        if isinstance(ov_params, dict) and "index" in ov_params:
            extra = " (zone %s index %s)" % (
                ov_params.get("zone", "j_rainbow_1"),
                ov_params.get("index"))
        print("override : %s%s" % (ov, extra))
    return 0


def cmd_serve():
    fd = open_dev()
    if fd is None:
        return 1
    log("starting serve: %d FPS per-LED mode on %s" % (FPS, DEV))
    captured = None
    for attempt in range(10):
        try:
            captured = get_report(fd, REPORT_ID, PKT)
            break
        except Exception as e:
            log("warn: state read failed (%s); retrying" % e)
            time.sleep(1.0)
    if captured is None:
        log("error: could not read board state; aborting")
        os.close(fd)
        return 1
    enable_pkt = build_enable_msg(captured)
    try:
        set_report(fd, enable_pkt)
        log("per-LED sync mode enabled (j_rainbow_1: 40 slots)")
    except Exception as e:
        log("error: enable message failed: %s" % e)
        os.close(fd)
        return 1

    stopping = {"flag": False}

    def _stop(signum, frame):
        stopping["flag"] = True
        log("signal %d: shutting down (restoring static)" % signum)

    signal.signal(signal.SIGTERM, _stop)
    signal.signal(signal.SIGINT, _stop)

    last_enable = time.monotonic()
    phase = 0
    temp_smooth = None
    pwr_smooth = None
    frame_count = 0
    last_log = 0.0
    frame_dt = 1.0 / FPS
    level_states = drive_level_states(load_level_map())
    try:
        while not stopping["flag"]:
            if frame_count % 100 == 0:      # every ~5 s: poll sensors
                level_states = drive_level_states(load_level_map())
                t, p = read_sensors()
                if t is not None:
                    temp_smooth = (t if temp_smooth is None
                                   else temp_smooth + 0.25 * (t - temp_smooth))
                if p is not None:
                    pwr_smooth = (p if pwr_smooth is None
                                  else pwr_smooth + 0.25 * (p - pwr_smooth))
            ov_name, ov_params = read_override()
            if time.monotonic() - last_enable > 60:
                set_report(fd, enable_pkt)   # keep per-LED mode asserted
                last_enable = time.monotonic()
            # ---- frame selection: preview-only, off, or temp/power-driven
            if ov_name == "off":
                frame = BLACK_FRAME          # preview-only off: pure black
                disp_name, disp_rgb, disp_wts = "off", (0, 0, 0), \
                    [0.0, 0.0, 0.0, 0.0]
            elif ov_name in PREVIEW_STATES:
                # preview-only beacon option (beacon or 6-10)
                frame_fn, disp_rgb = PREVIEW_STATES[ov_name]
                frame = frame_fn(phase, ov_params)
                disp_name, disp_wts = ov_name, None
            else:
                if ov_name:
                    w = {"darkblue": (1, 0, 0, 0), "perfan": (0, 1, 0, 0),
                         "chase_cool": (0, 0, 1, 0),
                         "chase_warm": (0, 0, 0, 1)}[ov_name]
                else:
                    w = state_weights(temp_smooth if temp_smooth is not None
                                      else TEMP_A_MAX,
                                      pwr_smooth if pwr_smooth is not None
                                      else 0.0)
                frame = frame_colors(w, phase, level_states)
                disp_name, disp_rgb = state_label(w, level_states)
                disp_wts = [round(x, 3) for x in w]
            # Always write, even when unchanged: keeps the USB link active so
            # autosuspend never puts the RGB controller to sleep between
            # writes (a wake can drop per-LED mode).
            set_report(fd, build_perled_packet(frame))
            phase = (phase + 1) % PHASE_PERIOD
            frame_count += 1
            if frame_count % 100 == 0:
                t = int(round(temp_smooth)) if temp_smooth is not None else None
                p = (int(round(pwr_smooth)) if pwr_smooth is not None
                    else None)
                extra = {"weights": disp_wts, "power": p}
                if ov_name == "ledprobe" and isinstance(ov_params, dict):
                    _zone = ov_params.get("zone", "j_rainbow_1")
                    _start, _count = ledprobe_zone(_zone)
                    _raw = ov_params.get("index", 0)
                    if isinstance(_raw, str) and \
                            _raw.strip().lower() == "all":
                        extra["ledprobe_index"] = "all"
                    else:
                        try:
                            _idx = int(_raw)
                        except (TypeError, ValueError):
                            _idx = 0
                        extra["ledprobe_index"] = max(
                            0, min(_idx, _count - 1))
                    extra["ledprobe_zone"] = _zone
                state_write(disp_name, disp_rgb, t, extra)
                if time.monotonic() - last_log > 30:
                    if disp_wts is None:
                        log("temp %s C / %s W -> %s (preview override)"
                            % (t, p, disp_name))
                    elif disp_wts == [0.0, 0.0, 0.0, 0.0]:
                        log("temp %s C / %s W -> off (preview override)"
                            % (t, p))
                    else:
                        log("temp %s C / %s W -> %s (weights A/B/C/D=%s)"
                            % (t, p, disp_name,
                               "/".join("%.2f" % x for x in disp_wts)))
                    last_log = time.monotonic()
            time.sleep(frame_dt)
    finally:
        try:
            restore_static(fd, captured)
            log("board restored to static dark blue")
        except Exception as e:
            log("warn: static restore failed: %s" % e)
        os.close(fd)
    log("serve stopped")
    return 0


def cmd_preview(state_name, seconds=15.0, direct=False):
    weights_map = {"A": (1, 0, 0, 0), "B": (0, 1, 0, 0),
                   "C": (0, 0, 1, 0), "D": (0, 0, 0, 1)}
    inv = {v: k for k, v in STATE_NAMES.items()}
    valid = set(inv) | set(PREVIEW_STATES) | {"off"}
    if state_name not in valid:
        log("error: unknown state %r (darkblue|perfan|chase_cool|"
            "chase_warm|beacon|sweep|tandem|heartbeat|comet|hazard|"
            "bitclock|telegraph|fireflies|lava|sonar|metronome|orbit|"
            "bitstream|sparkler|echo|ecg|walk|ledprobe|off)" % state_name)
        return 2
    preview_fn = PREVIEW_STATES.get(state_name)
    off_mode = (state_name == "off")
    st = state_read()
    daemon_alive = bool(st) and (time.time() - float(st.get("updated", 0)) < 10)
    if daemon_alive and not direct:
        write_override(state_name)
        log("daemon is running: override %s active for %gs"
            % (state_name, seconds))
        try:
            time.sleep(seconds)
        finally:
            try:
                os.remove(OVERRIDE_FILE)
            except OSError:
                pass
        log("override cleared; daemon back to temp-driven")
        return 0
    fd = open_dev()
    if fd is None:
        return 1
    captured = None
    try:
        captured = get_report(fd, REPORT_ID, PKT)
    except Exception as e:
        log("warn: state read failed (%s)" % e)
    set_report(fd, build_enable_msg(captured or bytes(PKT)))
    log("preview %s for %gs" % (state_name, seconds))
    phase = 0
    last_frame = None
    deadline = time.monotonic() + seconds
    try:
        while time.monotonic() < deadline and not _preview_stop[0]:
            if off_mode:
                frame = BLACK_FRAME
            elif preview_fn is not None:
                frame = preview_fn(phase)
            else:
                frame = frame_colors(weights_map[inv[state_name]], phase)
            if frame != last_frame:
                set_report(fd, build_perled_packet(frame))
                last_frame = frame
            phase = (phase + 1) % PHASE_PERIOD
            time.sleep(1.0 / FPS)
    finally:
        try:
            restore_static(fd, captured)
        except Exception as e:
            log("warn: static restore failed: %s" % e)
        os.close(fd)
    log("preview done; board restored to static")
    return 0


_preview_stop = [False]


def cmd_check():
    # temp+power OR model (temp, power) pairs
    assert state_weights(40, 15) == (1, 0, 0, 0)          # both low -> A
    assert state_weights(55, 15) == (1, 0, 0, 0)
    assert state_weights(40, 25) == (0, 1, 0, 0)          # power only -> B
    w = state_weights(57.5, 15)
    assert abs(w[0] - 0.5) < 0.05 and abs(w[1] - 0.5) < 0.05, w
    assert state_weights(60, 15) == (0, 1, 0, 0)          # temp only -> B
    w = state_weights(62.5, 15)
    assert abs(w[1] - 0.5) < 0.05 and abs(w[2] - 0.5) < 0.05, w
    assert state_weights(65, 15) == (0, 0, 1, 0)
    assert state_weights(40, 310) == (0, 0, 1, 0)         # power only -> C
    w = state_weights(70, 15)
    assert abs(w[2] - 0.5) < 0.05 and abs(w[3] - 0.5) < 0.05, w
    assert state_weights(75, 15) == (0, 0, 0, 1)
    assert state_weights(95, 310) == (0, 0, 0, 1)         # temp wins over pwr
    assert state_weights(40, 100) == (0, 1, 0, 0)         # mid power = B
    assert state_weights(76, 310) == (0, 0, 0, 1)
    def _s(f, i):
        return tuple(f[3 * i:3 * i + 3])

    # 3/4 CHASE: 3 2-slot color blocks + 4 dim base slots on RING10;
    # 10 | 120; +i, 1 slot/frame (re-based 2026-11-21 to RING10 +
    # TOPFANS)
    assert PHASE_PERIOD % 10 == 0
    assert chase(0, 0, PALETTE_COOL) == PALETTE_COOL[0]
    assert chase(2, 0, PALETTE_COOL) == BEACON_BG
    assert chase(3, 0, PALETTE_COOL) == PALETTE_COOL[1]
    assert chase(6, 0, PALETTE_COOL) == PALETTE_COOL[2]
    assert chase(8, 0, PALETTE_COOL) == BEACON_BG
    assert chase(1, 5, PALETTE_COOL) == chase(1 + 10, 5, PALETTE_COOL)

    # 5 BEACON: 2-slot bulb + 1 fading tail (0.5) on the steady orange
    # ring; re-based 2026-11-21 to RING10 + TOPFANS: RING10 (slots
    # 2..11, P10, 0.5 s/rev), +i, head leading; TOPFANS 12/13 hold the
    # dim orange base and flare toward the yellow bulb (f=1.0 bulb on
    # 10, 0.5 on 9/11); slots 0/1 (live, unassigned) and 14..239 (dead)
    # stay pure black; period 10 | 120
    fb = beacon_frame(0)
    assert _s(fb, 2) == BEACON_ARC and _s(fb, 3) == BEACON_ARC   # bulb
    assert _s(fb, 11) == (255, 191, 0)                           # tail 0.5
    assert _s(fb, 4) == BEACON_BASE                               # ring base
    assert _s(fb, 12) == BEACON_BG and _s(fb, 13) == BEACON_BG   # 12/13
    assert _s(fb, 0) == (0, 0, 0) and _s(fb, 1) == (0, 0, 0)
    assert _s(fb, 14) == (0, 0, 0) and _s(fb, 239) == (0, 0, 0)
    assert beacon_frame(0) == beacon_frame(10)                   # period 10
    for ph in range(PHASE_PERIOD):
        fph = beacon_frame(ph)
        bulb = (RING10[ph % 10], RING10[(ph + 1) % 10])
        tail = RING10[(ph - 1) % 10]
        assert _s(fph, bulb[0]) == BEACON_ARC
        assert _s(fph, bulb[1]) == BEACON_ARC
        assert _s(fph, tail) == (255, 191, 0)                    # tail
        head = [i for i in RING10 if _s(fph, i) == BEACON_ARC]
        assert sorted(head) == sorted(bulb)
        for i in RING10:
            if i not in bulb and i != tail:
                assert _s(fph, i) == BEACON_BASE
        f = 1.0 if 10 in bulb else (0.5 if 9 in bulb or 11 in bulb
                                    else 0.0)
        exp = tuple(int(b + f * (h - b))
                    for b, h in zip(BEACON_BG, BEACON_ARC))
        for i in TOPFANS:
            assert _s(fph, i) == exp
        for i in (0, 1):
            assert _s(fph, i) == (0, 0, 0)
        for i in range(14, NUM_SLOTS):
            assert _s(fph, i) == (0, 0, 0)

    # beacon params (preview lab): base override works, bad values fall back
    assert len(beacon_frame(0, {"period": "bogus"})) == NUM_SLOTS * 3
    fb = beacon_frame(0)
    assert len(fb) == NUM_SLOTS * 3
    assert all(0 <= b <= 255 for b in fb)
    fb2 = beacon_frame(0, {"base": [10, 20, 30]})
    assert _s(fb2, 4) == (10, 20, 30)                 # ring base slot
    assert _s(fb2, 2) == BEACON_ARC                  # bulb is pure yellow
    assert _s(fb2, 11) == (132, 137, 15)             # tail blends base
    assert len(beacon_frame(0, {"base": "nope"})) == NUM_SLOTS * 3

    # 1-4 DARKBLUE/PERFAN/CHASE_COOL/CHASE_WARM: re-based 2026-11-21 to
    # RING10 + TOPFANS — ring = slots 2..11 (P10); 12/13 painted (static
    # dim blue for 1/2, dim amber base + flare for 3/4); slots 0/1 (live,
    # unassigned) and 14..239 (dead) pure black
    for st_name, fn in (("darkblue", darkblue_frame), ("perfan", perfan_frame),
                        ("chase_cool", chase_cool_frame),
                        ("chase_warm", chase_warm_frame)):
        for ph in range(PHASE_PERIOD):
            fph = fn(ph)
            assert len(fph) == NUM_SLOTS * 3
            assert all(0 <= b <= 255 for b in fph)
            assert all(b == 0 for b in fph[3 * 14:])
            for i in TOPFANS:
                assert (fph[3 * i] >= BEACON_BG[0]
                        and fph[3 * i + 1] >= BEACON_BG[1]
                        and fph[3 * i + 2] >= BEACON_BG[2])
            for i in (0, 1):
                assert _s(fph, i) == (0, 0, 0)
    # 1 DARKBLUE: static dark blue on RING10; 12/13 static dim dark blue
    fb0 = darkblue_frame(0)
    for i in RING10:
        assert _s(fb0, i) == DARK_BLUE
    assert _s(fb0, 12) == DARK_BLUE_DIM and _s(fb0, 13) == DARK_BLUE_DIM
    assert darkblue_frame(0) == darkblue_frame(37)
    # 2 PERFAN: 6-color ring recast on RING10 (colors repeat), +i,
    # 1 slot/frame; period 30 (LCM(10, 6) | 120); 12/13 static dim blue
    fp0 = perfan_frame(0)
    for s, i in enumerate(RING10):
        assert _s(fp0, i) == PERFAN[s % 6]
    assert _s(fp0, 12) == DARK_BLUE_DIM and _s(fp0, 13) == DARK_BLUE_DIM
    assert perfan_frame(0) == perfan_frame(30)         # period 30
    assert perfan_frame(0) != perfan_frame(1)          # it moves
    for ph in range(PHASE_PERIOD):
        fph = perfan_frame(ph)
        for s, i in enumerate(RING10):
            assert _s(fph, i) == PERFAN[(s - ph) % 6]
    # 3 CHASE_COOL / 4 CHASE_WARM: 3-color comet on RING10, P10, +i;
    # TOPFANS flare f = max over blocks (1.0 block on 10, 0.5 on 9/11),
    # floored to the dim base per channel
    for palette, fn in ((PALETTE_COOL, chase_cool_frame),
                        (PALETTE_WARM, chase_warm_frame)):
        f0 = fn(0)
        for s, i in enumerate(RING10):
            assert _s(f0, i) == chase(s, 0, palette)
        assert fn(0) == fn(10)                         # period 10 wrap
        for ph in range(PHASE_PERIOD):
            fph = fn(ph)
            for s, i in enumerate(RING10):
                assert _s(fph, i) == chase(s, ph, palette)
            exp = _chase_topfan(ph, palette)
            for i in TOPFANS:
                assert _s(fph, i) == exp

    # frames: at an exact level the blended frame is exactly that level
    # state's frame; between adjacent levels it is the per-slot average
    for temp in (40, 55, 57.5, 60, 62.5, 65, 70, 75, 95):
        w = state_weights(temp, 15.0)
        f = frame_colors(w, 3)
        assert len(f) == NUM_SLOTS * 3
        assert all(0 <= b <= 255 for b in f)
    level_frames = (gpu_spotlight_idle_frame,
                    gpu_spotlight_somewhat_active_frame,
                    sweep_frame,
                    red_white_frame)
    for k, fn in enumerate(level_frames):
        w = tuple(1 if j == k else 0 for j in range(4))
        for ph in range(0, PHASE_PERIOD, 12):
            assert frame_colors(w, ph) == fn(ph)
    f60 = frame_colors((0, 1, 0, 0), 0)
    assert f60 == level_frames[1](0)
    fm = frame_colors((0.5, 0.5, 0, 0), 0)
    for i in range(NUM_SLOTS):
        for ch in range(3):
            assert fm[3 * i + ch] == int(
                0.5 * level_frames[0](0)[3 * i + ch]
                + 0.5 * level_frames[1](0)[3 * i + ch])
    # packets
    p = build_perled_packet(f60)
    assert len(p) == PERLED_PKT and p[0] == 0x53 and p[1] == 0x25 and p[2] == 0x06
    st = build_static_state(bytes(PKT))
    assert len(st) == PKT and st[J_RAINBOW_1] == EFFECT_STATIC
    en = build_enable_msg(bytes(PKT))
    assert len(en) == PKT and en[J_RAINBOW_1 + 10] == 40
    # preview-only off state: pure (0,0,0) on every slot
    assert len(BLACK_FRAME) == NUM_SLOTS * 3
    assert all(b == 0 for b in BLACK_FRAME)
    # ---- preview-only beacon options 6-10 --------------------------------
    # every new state: right length, in-range bytes, slots 48..239 stay black
    for st_name in ("sweep", "tandem", "heartbeat", "comet", "hazard"):
        f0 = PREVIEW_STATES[st_name][0](0)
        assert len(f0) == NUM_SLOTS * 3
        assert all(0 <= b <= 255 for b in f0)
        assert all(b == 0 for b in f0[3 * (FAN_START + FAN_LEN):])
    # beacon family (options 7-10): all four states re-based 2026-11-21
    # to RING10 + TOPFANS — each is checked in its own block below (7
    # TANDEM, 8 HEARTBEAT, 9 COMET, 10 HAZARD). The old shared loop
    # ("board 0..5 + fan 6..11 live, 12..47 black") is retired with the
    # old slot map: 12/13 are painted now (>= floor), 0/1 are black.
    # Sweep (6) was re-based 2026-11-21 to RING10 + TOPFANS and is
    # checked in its own block below.

    # 6 SWEEP — THE TEMPLATE, re-based 2026-11-21 to RING10 + TOPFANS:
    # one 1-slot yellow radar dot on RING10 (slots 2..11, P10, 1
    # latch/frame, +i) with a 3-slot linear amber afterglow wrapping
    # the ring; TOPFANS 12/13 hold the dim amber base and flare toward
    # the head color when the head crosses slot 10 (f=1.0) or 9/11
    # (f=0.5); slots 0..1 (live, unassigned) and 14..239 (dead) stay
    # pure black; period 10 | 120
    fs = sweep_frame(0)
    assert tuple(fs[3 * 2:3 * 2 + 3]) == SWEEP_HEAD      # head slot 2
    assert tuple(fs[3 * 11:3 * 11 + 3]) == SWEEP_TAIL[0]  # tail1 wraps
    assert tuple(fs[3 * 10:3 * 10 + 3]) == SWEEP_TAIL[1]  # tail2
    assert tuple(fs[3 * 9:3 * 9 + 3]) == BEACON_BG        # tail3 -> base
    assert tuple(fs[3 * 3:3 * 3 + 3]) == BEACON_BG        # ring base
    assert tuple(fs[3 * 12:3 * 12 + 3]) == BEACON_BG      # 12/13 base
    assert tuple(fs[3 * 13:3 * 13 + 3]) == BEACON_BG      # (f=0, head on 2)
    assert sweep_frame(0) == sweep_frame(10)              # period wrap
    # every frame: head advances 1 latch/frame on RING10; TOPFANS flare
    # f=1.0 (head on 10) / 0.5 (9 or 11) / 0, per-channel int blend from
    # the dim base; ring >= floor; 0/1 and 14..239 pure black
    for ph in range(PHASE_PERIOD):
        fph = sweep_frame(ph)
        hs = RING10[ph % 10]
        assert tuple(fph[3 * hs:3 * hs + 3]) == SWEEP_HEAD
        head = [i for i in RING10
                if tuple(fph[3 * i:3 * i + 3]) == SWEEP_HEAD]
        assert head == [hs]
        f = (1.0 if hs == 10 else 0.5 if hs in (9, 11) else 0.0)
        exp = tuple(int(b + f * (h - b))
                    for b, h in zip(BEACON_BG, SWEEP_HEAD))
        for i in TOPFANS:
            assert tuple(fph[3 * i:3 * i + 3]) == exp
        for i in RING10:
            assert (fph[3 * i] >= BEACON_BG[0]
                    and fph[3 * i + 1] >= BEACON_BG[1]
                    and fph[3 * i + 2] >= BEACON_BG[2])
        for i in (0, 1):
            assert tuple(fph[3 * i:3 * i + 3]) == (0, 0, 0)
        for i in range(14, 240):
            assert tuple(fph[3 * i:3 * i + 3]) == (0, 0, 0)
    # 12/13 never dip below the floor (old "12/13 skipped/black" asserts
    # are gone — the 2026-11-16 skip decision is VOID per spec)
    for ph in range(PHASE_PERIOD):
        fph = sweep_frame(ph)
        for i in TOPFANS:
            assert (fph[3 * i] >= BEACON_BG[0]
                    and fph[3 * i + 1] >= BEACON_BG[1]
                    and fph[3 * i + 2] >= BEACON_BG[2])
    # slow factor (preview lab): slow N holds each position N frames
    assert sweep_frame(4, {"slow": 2}) == sweep_frame(2)
    assert sweep_frame(1, {"slow": 2}) == sweep_frame(0)
    assert sweep_frame(0, {"slow": "bogus"}) == sweep_frame(0)
    # 7 TANDEM: two 2-slot amber heads counter-rotating on RING10 (A +i,
    # B -i, 180 deg apart at phase 0), head leading / tail trailing;
    # re-based 2026-11-21 to RING10 + TOPFANS; period 10 | 120;
    # TOPFANS flare f = max over both bulbs (1.0 on 10, 0.5 on 9/11)
    ft0 = tandem_frame(0)
    for s in (2, 3, 7, 8):                # phase 0: A bulb 2/3, B bulb 7/8
        assert _s(ft0, s) == TANDEM_HEAD
    assert _s(ft0, 4) == BEACON_BG        # gap between the bulbs
    assert _s(ft0, 9) == TANDEM_TAIL[0]
    assert _s(ft0, 10) == TANDEM_TAIL[1]
    assert _s(ft0, 11) == TANDEM_TAIL[0]
    assert _s(ft0, 12) == BEACON_BG and _s(ft0, 13) == BEACON_BG  # f=0
    assert tandem_frame(0) == tandem_frame(10)     # period 10 wrap
    assert tandem_frame(0) != tandem_frame(1)      # it moves
    for ph in range(PHASE_PERIOD):
        fph = tandem_frame(ph)
        head = [i for i in RING10 if _s(fph, i) == TANDEM_HEAD]
        expect = sorted({RING10[ph % 10], RING10[(ph + 1) % 10],
                         RING10[(5 - ph) % 10], RING10[(6 - ph) % 10]})
        assert sorted(head) == expect
        f = 0.0
        for bulb in ((RING10[ph % 10], RING10[(ph + 1) % 10]),
                     (RING10[(5 - ph) % 10], RING10[(6 - ph) % 10])):
            if 10 in bulb:
                f = max(f, 1.0)
            elif 9 in bulb or 11 in bulb:
                f = max(f, 0.5)
        exp = tuple(int(b + f * (h - b))
                    for b, h in zip(BEACON_BG, TANDEM_HEAD))
        for i in TOPFANS:
            assert _s(fph, i) == exp
        for i in RING10:
            assert (fph[3 * i] >= BEACON_BG[0]
                    and fph[3 * i + 1] >= BEACON_BG[1]
                    and fph[3 * i + 2] >= BEACON_BG[2])
        for i in (0, 1):
            assert _s(fph, i) == (0, 0, 0)
        for i in range(14, NUM_SLOTS):
            assert _s(fph, i) == (0, 0, 0)

    # 8 HEARTBEAT: breathing ring (60) on RING10 + 1-slot yellow lap dot
    # (+i, P10) + 1 yellow->ring blend slot behind; re-based 2026-11-21
    # to RING10 + TOPFANS; TOPFANS 12/13 breathe in phase with the ring;
    # periods 60 | 120 (breath), 10 | 120 (dot)
    assert heartbeat_frame(0) == heartbeat_frame(60)  # both periods
    assert heartbeat_frame(0) != heartbeat_frame(24)  # breath differs
    fhb0 = heartbeat_frame(0)
    assert _s(fhb0, 2) == HEARTBEAT_HEAD              # dot on slot 2
    assert _s(fhb0, 11) == (210, 160, 0)             # blend behind
    assert _s(fhb0, 12) == _s(fhb0, 3)               # in-phase breath
    for ph in range(PHASE_PERIOD):
        fph = heartbeat_frame(ph)
        breath = (0.65 + 0.35 * math.sin(2.0 * math.pi * ph
                                         / HEARTBEAT_BREATH_PERIOD))
        ring = (int(HEARTBEAT_RING[0] * breath),
                int(HEARTBEAT_RING[1] * breath), 0)
        dot = RING10[ph % 10]
        back = RING10[(ph - 1) % 10]
        assert _s(fph, dot) == HEARTBEAT_HEAD
        assert _s(fph, back) == (int((255 + ring[0]) / 2),
                                 int((255 + ring[1]) / 2), 0)
        for i in RING10:
            if i not in (dot, back):
                assert _s(fph, i) == ring
        for i in TOPFANS:
            assert _s(fph, i) == ring
        for i in RING10:
            assert (fph[3 * i] >= BEACON_BG[0]
                    and fph[3 * i + 1] >= BEACON_BG[1]
                    and fph[3 * i + 2] >= BEACON_BG[2])
        for i in (0, 1):
            assert _s(fph, i) == (0, 0, 0)
        for i in range(14, NUM_SLOTS):
            assert _s(fph, i) == (0, 0, 0)

    # 9 COMET: 2-slot yellow bulb + smoothstep tail filling RING10, -i;
    # re-based 2026-11-21 to RING10 + TOPFANS; period 10 | 120; TOPFANS
    # flare f = 1.0 (bulb on 10) / 0.5 (bulb on 9 or 11)
    fc0 = comet_frame(0)
    for s in (2, 11):                     # phase 0: bulb slots 2/11
        assert _s(fc0, s) == COMET_HEAD
    assert comet_frame(0) == comet_frame(10)       # period 10 wrap
    assert comet_frame(0) != comet_frame(1)        # it moves
    for ph in range(PHASE_PERIOD):
        fph = comet_frame(ph)
        pos = (-ph) % 10
        bulb = (RING10[pos], RING10[(pos - 1) % 10])
        assert _s(fph, bulb[0]) == COMET_HEAD
        assert _s(fph, bulb[1]) == COMET_HEAD
        head = [i for i in RING10 if _s(fph, i) == COMET_HEAD]
        assert sorted(head) == sorted(bulb)
        for k in range(2, 10):
            i = RING10[(pos - k) % 10]
            assert _s(fph, i) == _on_base(_comet_tail(k - 1, 10))
        f = (1.0 if 10 in bulb else
             0.5 if 9 in bulb or 11 in bulb else 0.0)
        exp = tuple(int(b + f * (h - b))
                    for b, h in zip(BEACON_BG, COMET_HEAD))
        for i in TOPFANS:
            assert _s(fph, i) == exp
        for i in RING10:
            assert (fph[3 * i] >= BEACON_BG[0]
                    and fph[3 * i + 1] >= BEACON_BG[1]
                    and fph[3 * i + 2] >= BEACON_BG[2])
        for i in (0, 1):
            assert _s(fph, i) == (0, 0, 0)
        for i in range(14, NUM_SLOTS):
            assert _s(fph, i) == (0, 0, 0)

    # 10 HAZARD: 2-slot head strobing 10 Hz + blocky 4-tail on RING10,
    # +i; re-based 2026-11-21 to RING10 + TOPFANS; period 10 | 120
    # (blink is phase-parity); TOPFANS 12/13 strobe in sync (even =
    # bright amber, odd = dim base; floor = base, never black)
    fhz0 = hazard_frame(0)
    assert _s(fhz0, 2) == HAZARD_HEAD_EVEN and _s(fhz0, 3) == HAZARD_HEAD_EVEN
    assert _s(fhz0, 11) == HAZARD_TAIL[0]
    assert _s(fhz0, 10) == HAZARD_TAIL[1]
    assert _s(fhz0, 9) == HAZARD_TAIL[2]
    assert _s(fhz0, 8) == BEACON_BG          # tail4 quantized to base
    assert _s(fhz0, 4) == BEACON_BG          # ring base slot
    assert _s(fhz0, 12) == HAZARD_HEAD_EVEN and _s(fhz0, 13) == HAZARD_HEAD_EVEN
    assert _s(hazard_frame(1), 4) == HAZARD_HEAD_ODD
    assert _s(hazard_frame(1), 3) == HAZARD_HEAD_ODD   # head advances +i
    assert _s(hazard_frame(1), 12) == BEACON_BG        # 12/13 strobe odd
    assert hazard_frame(0) == hazard_frame(10)         # period 10
    for ph in range(PHASE_PERIOD):
        fph = hazard_frame(ph)
        head_c = (HAZARD_HEAD_EVEN if ph % 2 == 0 else HAZARD_HEAD_ODD)
        bulb = (RING10[ph % 10], RING10[(ph + 1) % 10])
        assert _s(fph, bulb[0]) == head_c and _s(fph, bulb[1]) == head_c
        for k in range(2, 6):
            i = RING10[(ph + 1 - k) % 10]
            assert _s(fph, i) == _on_base(HAZARD_TAIL[k - 2])
        exp = HAZARD_HEAD_EVEN if ph % 2 == 0 else BEACON_BG
        for i in TOPFANS:
            assert _s(fph, i) == exp
        for i in RING10:
            assert (fph[3 * i] >= BEACON_BG[0]
                    and fph[3 * i + 1] >= BEACON_BG[1]
                    and fph[3 * i + 2] >= BEACON_BG[2])
        for i in (0, 1):
            assert _s(fph, i) == (0, 0, 0)
        for i in range(14, NUM_SLOTS):
            assert _s(fph, i) == (0, 0, 0)

    # ---- preview-only creative options 11-22 ----------------------------
    # every state: right length, in-range bytes, slots 48..239 black,
    # and bit-identical across the 120-frame wrap seam
    creative = ("bitclock", "telegraph", "fireflies", "lava", "sonar",
                "metronome", "orbit", "bitstream", "sparkler", "echo",
                "ecg", "walk", "gpu-spotlight-idle", "gpu-spotlight-fire",
                "gpu-spotlight-somewhat-active", "red_white", "rainbow",
                "limelight")
    for st_name in creative:
        fn = PREVIEW_STATES[st_name][0]
        for p in (0, 37, 119):
            f0 = fn(p)
            assert len(f0) == NUM_SLOTS * 3
            assert all(0 <= b <= 255 for b in f0)
            assert all(b == 0 for b in f0[3 * (FAN_START + FAN_LEN):])
            assert fn(p) == fn(p + PHASE_PERIOD), st_name

    # 11 BITCLOCK: 6-bit counter on RING10 slots 2..7 (bit i -> slot
    # 2+i); lap flash (15-frame decay) on ring slots 8..11; re-based
    # 2026-11-21 to RING10 + TOPFANS: board ring 0..5 counter retired
    # (0/1 black), 12/13 breathe in phase with the lap (dim base ->
    # lap color, floored to the BEACON_BG floor); 14..239 dead, pure
    # black
    fbc0 = bitclock_frame(0)
    for i in (2, 3, 4, 5, 6, 7):
        assert _s(fbc0, i) == BITCLOCK_OFF        # t=0: all bits off
    assert _s(fbc0, 8) == (0, 255, 170)           # BASE + full LAP
    assert _s(fbc0, 11) == (0, 255, 170)
    assert _s(fbc0, 12) == (48, 255, 160)         # 12/13: LAP floored
    assert _s(fbc0, 13) == (48, 255, 160)
    fbc33 = bitclock_frame(33)                    # 33 = bits 0 and 5
    assert _s(fbc33, 2) == BITCLOCK_ON and _s(fbc33, 7) == BITCLOCK_ON
    assert _s(fbc33, 3) == BITCLOCK_OFF
    assert _s(fbc33, 8) == BITCLOCK_BASE          # lap fully decayed
    assert _s(fbc33, 11) == BITCLOCK_BASE
    assert _s(fbc33, 12) == BITCLOCK_TOP_BASE     # 12/13 back at base
    for ph in range(PHASE_PERIOD):
        fph = bitclock_frame(ph)
        t = ph % PHASE_PERIOD
        for i in range(6):
            assert _s(fph, 2 + i) == (BITCLOCK_ON if (t >> i) & 1
                                      else BITCLOCK_OFF)
        lap = 1.0 - smoothstep(t / 15.0)
        exp_lap = (min(255, BITCLOCK_BASE[0] + int(BITCLOCK_LAP[0] * lap)),
                   min(255, BITCLOCK_BASE[1] + int(BITCLOCK_LAP[1] * lap)),
                   min(255, BITCLOCK_BASE[2] + int(BITCLOCK_LAP[2] * lap)))
        for i in (8, 9, 10, 11):
            assert _s(fph, i) == exp_lap
        exp_top = _on_base(tuple(int(BITCLOCK_TOP_BASE[c]
                             + lap * (BITCLOCK_LAP[c] - BITCLOCK_TOP_BASE[c]))
                             for c in range(3)))
        for i in TOPFANS:
            assert _s(fph, i) == exp_top
            assert (fph[3 * i] >= BEACON_BG[0]
                    and fph[3 * i + 1] >= BEACON_BG[1]
                    and fph[3 * i + 2] >= BEACON_BG[2])
        for i in RING10:
            assert _s(fph, i) != (0, 0, 0)        # no pure black on ring
        for i in (0, 1):
            assert _s(fph, i) == (0, 0, 0)
        for i in range(14, NUM_SLOTS):
            assert _s(fph, i) == (0, 0, 0)

    # 12 TELEGRAPH: dot at ring slot 4; echo fills RING10 (10 frames
    # late); re-based 2026-11-21 to RING10 + TOPFANS: ring sits on the
    # dim BEACON_BG base (no pure black), 12/13 strobe between base and
    # echo in sync (floor = base, never black); 0/1 and 14..239 pure
    # black; period 30 | 120
    ftel0 = telegraph_frame(0)
    assert _s(ftel0, 4) == TELEGRAPH_DOT          # dot on at t=0
    assert _s(ftel0, 2) == BEACON_BG
    assert _s(ftel0, 11) == BEACON_BG
    assert _s(ftel0, 12) == BEACON_BG and _s(ftel0, 13) == BEACON_BG
    ftel10 = telegraph_frame(10)
    assert _s(ftel10, 2) == TELEGRAPH_ECHO
    assert _s(ftel10, 11) == TELEGRAPH_ECHO
    assert _s(ftel10, 4) == TELEGRAPH_DOT         # dot still on
    assert _s(ftel10, 12) == TELEGRAPH_ECHO       # 12/13 strobe on
    assert _s(telegraph_frame(25), 2) == BEACON_BG    # echo off, unit 2
    assert _s(telegraph_frame(25), 12) == BEACON_BG
    for ph in range(PHASE_PERIOD):
        fph = telegraph_frame(ph)
        t = ph % PHASE_PERIOD
        echo = ((t - 10) % 30) < 15
        dot = (t % 30) < 15
        for i in RING10:
            if dot and i == 4:
                assert _s(fph, i) == TELEGRAPH_DOT
            elif echo:
                assert _s(fph, i) == TELEGRAPH_ECHO
            else:
                assert _s(fph, i) == BEACON_BG
        exp_top = TELEGRAPH_ECHO if echo else BEACON_BG
        for i in TOPFANS:
            assert _s(fph, i) == exp_top
            assert (fph[3 * i] >= BEACON_BG[0]
                    and fph[3 * i + 1] >= BEACON_BG[1]
                    and fph[3 * i + 2] >= BEACON_BG[2])
        for i in (0, 1):
            assert _s(fph, i) == (0, 0, 0)
        for i in range(14, NUM_SLOTS):
            assert _s(fph, i) == (0, 0, 0)

    # 13 FIREFLIES: four flies drifting on RING10 (bases 3/5/7/9);
    # re-based 2026-11-21 to RING10 + TOPFANS: board-ring fifth fly
    # retired (0/1 black), 12/13 breathe in phase (dim base -> fly
    # core by the max envelope, floored to BEACON_BG); 14..239 dead,
    # pure black
    ff0 = fireflies_frame(0)
    # t=0: fly 0 at env 0, fly 3 (off 75) fading out at env 0.5, its
    # pos exactly slot 10 -> slot 10 = base + half core; 12/13 = base
    # + 0.5*(core - base)
    assert _s(ff0, 10) == (127, 102, 53)
    assert _s(ff0, 12) == (151, 110, 43) and _s(ff0, 13) == (151, 110, 43)
    for i in RING10:
        assert _s(ff0, i) != (0, 0, 0)
    ff30 = fireflies_frame(30)
    assert _s(ff30, 12) == FIREFLY_CORE           # fly 0 full at t=30
    assert _s(ff30, 13) == FIREFLY_CORE
    for ph in range(PHASE_PERIOD):
        fph = fireflies_frame(ph)
        t = ph % PHASE_PERIOD
        envs = [_firefly_env(t, off) for (_, _, _, off) in FIREFLY_FLIES]
        ring = [list(FIREFLY_BASE) for _ in RING10]
        for (base, amp, phi, off), env in zip(FIREFLY_FLIES, envs):
            if env <= 0:
                continue
            pos = base + amp * math.sin(2.0 * math.pi * t / PHASE_PERIOD
                                        + math.radians(phi))
            for s, i in enumerate(RING10):
                b = _tri(i - pos)
                if b > 0:
                    for c in range(3):
                        ring[s][c] = min(
                            255, ring[s][c] + int(FIREFLY_CORE[c] * b * env))
        for s, i in enumerate(RING10):
            assert _s(fph, i) == tuple(ring[s])
        env = max(envs)
        exp_top = _on_base(tuple(int(BEACON_BG[c]
                             + env * (FIREFLY_CORE[c] - BEACON_BG[c]))
                             for c in range(3)))
        for i in TOPFANS:
            assert _s(fph, i) == exp_top
            assert (fph[3 * i] >= BEACON_BG[0]
                    and fph[3 * i + 1] >= BEACON_BG[1]
                    and fph[3 * i + 2] >= BEACON_BG[2])
        for i in (0, 1):
            assert _s(fph, i) == (0, 0, 0)
        for i in range(14, NUM_SLOTS):
            assert _s(fph, i) == (0, 0, 0)

    # 14 LAVA: three additive blobs drifting on RING10 (center 6.5,
    # amp 4.0); re-based 2026-11-21 to RING10 + TOPFANS: board ring
    # 0..5 retired (0/1 black), 12/13 follow the motion rule (dim base
    # -> lava head, f = max over blobs: 1.0 on slot 10, 0.5 on 9/11,
    # floored to BEACON_BG); 14..239 dead, pure black
    fl0 = lava_frame(0)
    assert _s(fl0, 2) == LAVA_BASE                # no blob reaches 2
    assert _s(fl0, 12) == LAVA_HEAD               # blob 1 on slot 10
    assert _s(fl0, 13) == LAVA_HEAD
    for ph in range(PHASE_PERIOD):
        fph = lava_frame(ph)
        t = ph % PHASE_PERIOD
        f = 0.0
        for color, period, phi in LAVA_BLOBS:
            pos = LAVA_CENTER + LAVA_AMP * math.sin(
                2.0 * math.pi * (t % period) / period
                + math.radians(phi))
            if abs(pos - 10) <= 0.5:
                f = max(f, 1.0)
            elif abs(pos - 9) <= 0.5 or abs(pos - 11) <= 0.5:
                f = max(f, 0.5)
        exp_top = _on_base(tuple(int(LAVA_TOP_BASE[c]
                             + f * (LAVA_HEAD[c] - LAVA_TOP_BASE[c]))
                             for c in range(3)))
        for i in TOPFANS:
            assert _s(fph, i) == exp_top
            assert (fph[3 * i] >= BEACON_BG[0]
                    and fph[3 * i + 1] >= BEACON_BG[1]
                    and fph[3 * i + 2] >= BEACON_BG[2])
        for i in RING10:
            assert _s(fph, i) != (0, 0, 0)        # no pure black on ring
        for i in (0, 1):
            assert _s(fph, i) == (0, 0, 0)
        for i in range(14, NUM_SLOTS):
            assert _s(fph, i) == (0, 0, 0)

    # 15 SONAR: ripple expands from the RING10 center (6.5) to the ring
    # edges 2/11 over 8 frames; 0.85^d falloff; ping lamp on 6/7, 2
    # frames at launch; edge flash at t%8==7 (ring edges 2/11); 12/13
    # dim base + f-blend flare toward cyan (f=1.0 a ripple point on
    # slot 10, 0.5 on 9/11, floored to BEACON_BG); period 8 | 120
    # (re-based 2026-11-21 to RING10 + TOPFANS)
    fsn0 = sonar_frame(0)
    assert _s(fsn0, 6) == (0, 255, 255)              # ping + launch pulse
    assert _s(fsn0, 7) == (0, 255, 255)
    assert _s(fsn0, 12) == BEACON_BG                 # no point on 9/10/11
    assert _s(fsn0, 13) == BEACON_BG
    assert _s(sonar_frame(2), 6) != SONAR_RING       # lamp off at t=2
    fsn7 = sonar_frame(7)
    assert _s(fsn7, 2) == (255, 255, 255)            # edge hit flash
    assert _s(fsn7, 11) == (255, 255, 255)
    assert _s(sonar_frame(5), 12) == (48, 220, 255)  # flare f=1.0, floored
    assert sonar_frame(0) == sonar_frame(8)          # period 8 wrap
    for ph in range(PHASE_PERIOD):
        fph = sonar_frame(ph)
        t = ph % PHASE_PERIOD
        f = t % 8
        r = 4.5 * f / 7.0
        fl = 0.0
        for p in (SONAR_CENTER - r, SONAR_CENTER + r):
            s = int(p + 0.5)
            if s == 10:
                fl = max(fl, 1.0)
            elif s in (9, 11):
                fl = max(fl, 0.5)
        exp_top = _on_base(tuple(int(BEACON_BG[c]
                                    + fl * (SONAR_RING[c] - BEACON_BG[c]))
                                 for c in range(3)))
        for i in TOPFANS:
            assert _s(fph, i) == exp_top
            assert (fph[3 * i] >= BEACON_BG[0]
                    and fph[3 * i + 1] >= BEACON_BG[1]
                    and fph[3 * i + 2] >= BEACON_BG[2])
        for i in RING10:
            assert _s(fph, i) != (0, 0, 0)           # no pure black on ring
        for i in (0, 1):
            assert _s(fph, i) == (0, 0, 0)
        for i in range(14, NUM_SLOTS):
            assert _s(fph, i) == (0, 0, 0)

    # 16 METRONOME: x(t) = 6.5 - 4.5*cos(2pi*t/40) swings RING10 with
    # extrema every 20 frames (60 BPM) at the ring edges 2/11; 2-slot
    # Gaussian marker, white flash 3 frames + both edges at each
    # extremum; 12/13 dim base + f-blend flare toward gold (f=1.0
    # marker on slot 10, 0.5 on 9/11, floored to BEACON_BG); old 3-bit
    # tick counter dropped (board ring retired — 0/1 black); period 40
    # | 120 (re-based 2026-11-21 to RING10 + TOPFANS)
    fmr0 = metronome_frame(0)
    assert _s(fmr0, 2) == METRO_ACCENT              # marker at left edge
    assert _s(fmr0, 11) == METRO_ACCENT             # downbeat: both edges
    assert _s(fmr0, 12) == BEACON_BG                # marker on 2, f=0
    assert _s(fmr0, 13) == BEACON_BG
    fmr20 = metronome_frame(20)
    assert _s(fmr20, 11) == METRO_ACCENT            # marker at right edge
    assert _s(fmr20, 12) == (151, 119, 45)          # flare f=0.5 (on 11)
    assert metronome_frame(0) == metronome_frame(40)  # period 40 wrap
    for ph in range(PHASE_PERIOD):
        fph = metronome_frame(ph)
        t = ph % PHASE_PERIOD
        x = 6.5 - 4.5 * math.cos(2.0 * math.pi * (t % 40) / 40.0)
        s = int(x + 0.5)
        fl = 1.0 if s == 10 else (0.5 if s in (9, 11) else 0.0)
        exp_top = _on_base(tuple(int(BEACON_BG[c]
                                    + fl * (METRO_MARK[c] - BEACON_BG[c]))
                                 for c in range(3)))
        for i in TOPFANS:
            assert _s(fph, i) == exp_top
            assert (fph[3 * i] >= BEACON_BG[0]
                    and fph[3 * i + 1] >= BEACON_BG[1]
                    and fph[3 * i + 2] >= BEACON_BG[2])
        for i in RING10:
            assert _s(fph, i) != (0, 0, 0)          # no pure black on ring
        for i in (0, 1):
            assert _s(fph, i) == (0, 0, 0)
        for i in range(14, NUM_SLOTS):
            assert _s(fph, i) == (0, 0, 0)

    # 17 ORBIT: planet core 6/7 + halo 4/5/8/9 at the RING10 center;
    # three satellites at radii 4.5/3.15/1.8 (old x1.8), one orbit per
    # 120-frame cycle; hidden behind the planet when |x-6.5|<=2.5 and
    # depth<0; old onboard pass lamps 0/2/4 dropped (board ring retired
    # — 0/1 black); 12/13 dim base + f-blend flare toward the planet's
    # white (f = max over visible satellites: 1.0 on slot 10, 0.5 on
    # 9/11, floored to BEACON_BG); period 120 | 120 (re-based
    # 2026-11-21 to RING10 + TOPFANS)
    fob0 = orbit_frame(0)
    assert _s(fob0, 6) == (255, 255, 255)           # planet + sat A
    assert _s(fob0, 7) == (255, 255, 255)
    assert _s(fob0, 12) == BEACON_BG                # no satellite on 9/10/11
    assert _s(fob0, 13) == BEACON_BG
    fob40 = orbit_frame(40)
    assert _s(fob40, 12) == (255, 255, 240)         # sat A on slot 10: flare
    assert _s(fob40, 13) == (255, 255, 240)
    assert orbit_frame(0) != orbit_frame(1)         # it moves
    for ph in range(PHASE_PERIOD):
        fph = orbit_frame(ph)
        t = ph % PHASE_PERIOD
        fl = 0.0
        for color, radius, phi in ORBIT_SATS:
            th = 2.0 * math.pi * t / PHASE_PERIOD + math.radians(phi)
            x = 6.5 + radius * math.sin(th)
            if abs(x - 6.5) <= 2.5 and math.cos(th) < 0:
                continue                             # behind the planet
            s = int(x + 0.5)
            if s == 10:
                fl = max(fl, 1.0)
            elif s in (9, 11):
                fl = max(fl, 0.5)
        exp_top = _on_base(tuple(int(BEACON_BG[c]
                                    + fl * (ORBIT_PLANET[c] - BEACON_BG[c]))
                                 for c in range(3)))
        for i in TOPFANS:
            assert _s(fph, i) == exp_top
            assert (fph[3 * i] >= BEACON_BG[0]
                    and fph[3 * i + 1] >= BEACON_BG[1]
                    and fph[3 * i + 2] >= BEACON_BG[2])
        for i in RING10:
            assert _s(fph, i) != (0, 0, 0)          # no pure black on ring
        for i in (0, 1):
            assert _s(fph, i) == (0, 0, 0)
        for i in range(14, NUM_SLOTS):
            assert _s(fph, i) == (0, 0, 0)

    # 18 BITSTREAM: 12-bit payload scrolls LEFT as 4-slot chunks across
    # RING10 (2.5 chunks visible, clipped), 1 chunk per 10 frames
    # (P10), 12 shifts per 120-frame cycle = seamless lap; old onboard
    # slot 5 start-bit lamp dropped (board ring retired — 0/1 black):
    # 12/13 blink as the start bit (5 on / 5 off, floored to BEACON_BG);
    # period 120 | 120 (re-based 2026-11-21 to RING10 + TOPFANS)
    fsb0 = bitstream_frame(0)
    assert _s(fsb0, 2) == BITSTREAM_ZERO             # bit 0 "0", o=2
    assert _s(fsb0, 3) == BITSTREAM_ZERO             # bit 0 "0", o=3
    assert _s(fsb0, 4) == BITSTREAM_ONE              # bit 1 "1", o=0
    assert _s(fsb0, 5) == BITSTREAM_ONE              # bit 1 "1", o=1
    assert _s(fsb0, 6) == BITSTREAM_DIM              # bit 1 "1", o=2
    assert _s(fsb0, 7) == BITSTREAM_DIM              # bit 1 "1", o=3
    assert _s(fsb0, 8) == BITSTREAM_ZERO             # bit 2 "0", o=0
    assert _s(fsb0, 9) == BITSTREAM_ZERO             # bit 2 "0", o=1
    assert _s(fsb0, 10) == BITSTREAM_ZERO            # bit 2 "0", o=2
    assert _s(fsb0, 11) == BITSTREAM_ZERO            # bit 2 "0", o=3
    assert _s(fsb0, 12) == (48, 255, 220)            # start bit on, floored
    assert _s(fsb0, 13) == (48, 255, 220)
    assert _s(bitstream_frame(5), 12) == BEACON_BG   # start bit off
    assert _s(bitstream_frame(10), 2) == BITSTREAM_DIM  # shifted 1 chunk
    for ph in range(PHASE_PERIOD):
        fph = bitstream_frame(ph)
        t = ph % PHASE_PERIOD
        step = (t // 10) % 12
        for i in RING10:
            bit = BITSTREAM_PAYLOAD[(i // 4 + step) % 12]
            o = i % 4
            if bit == "1":
                c = BITSTREAM_ONE if o < 2 else BITSTREAM_DIM
            else:
                c = BITSTREAM_ZERO
            assert _s(fph, i) == c
            assert _s(fph, i) != (0, 0, 0)
        top = _on_base(BITSTREAM_LAMP) if t % 10 < 5 else BEACON_BG
        for i in TOPFANS:
            assert _s(fph, i) == top
            assert (fph[3 * i] >= BEACON_BG[0]
                    and fph[3 * i + 1] >= BEACON_BG[1]
                    and fph[3 * i + 2] >= BEACON_BG[2])
        for i in (0, 1):
            assert _s(fph, i) == (0, 0, 0)
        for i in range(14, NUM_SLOTS):
            assert _s(fph, i) == (0, 0, 0)

    # 19 SPARKLER: bursts at 7/9/11 (t=0/40/80); 4 sparks per side
    # flying out 1 slot/frame for 12 frames (inner SPARK_A -> outer
    # SPARK_B); fuse countdown 35..39 on 12/13; burst flare f=0.5 on
    # burst 9/11, 0 on 7; 3 dim dots at the pads over the BEACON_BG
    # base; re-based 2026-11-21 to RING10 + TOPFANS; 0/1 and 14..239
    # pure black; period 40 | 120
    fps0 = sparkler_frame(0)
    assert _s(fps0, 7) == SPARK_FLASH       # burst 7 flash
    assert _s(fps0, 8) == SPARK_A           # leading spark (j=0)
    assert _s(fps0, 3) == SPARK_B           # outer tip spark (j=3)
    assert _s(fps0, 2) == BEACON_BG         # plain ring slot (no dot)
    assert _s(fps0, 12) == BEACON_BG and _s(fps0, 13) == BEACON_BG
    fps20 = sparkler_frame(20)
    assert _s(fps20, 7) == SPARK_DOT and _s(fps20, 9) == SPARK_DOT
    assert _s(fps20, 11) == SPARK_DOT
    assert _s(fps20, 2) == BEACON_BG        # no dots off the pads
    assert _s(fps20, 12) == BEACON_BG       # fuse off between bursts
    assert _s(sparkler_frame(35), 12) == SPARK_FUSE   # fuse countdown
    assert _s(sparkler_frame(40), 9) == SPARK_FLASH   # burst 9
    assert _s(sparkler_frame(40), 12) == (151, 139, 127)   # f=0.5
    assert _s(sparkler_frame(80), 11) == SPARK_FLASH   # burst 11
    assert _s(sparkler_frame(80), 12) == (151, 139, 127)   # f=0.5
    for ph in range(PHASE_PERIOD):
        fph = sparkler_frame(ph)
        for i in RING10:
            assert (fph[3 * i] >= BEACON_BG[0]
                    and fph[3 * i + 1] >= BEACON_BG[1]
                    and fph[3 * i + 2] >= BEACON_BG[2])
        for i in TOPFANS:
            assert (fph[3 * i] >= BEACON_BG[0]
                    and fph[3 * i + 1] >= BEACON_BG[1]
                    and fph[3 * i + 2] >= BEACON_BG[2])
        for i in (0, 1):
            assert _s(fph, i) == (0, 0, 0)
        for i in range(14, NUM_SLOTS):
            assert _s(fph, i) == (0, 0, 0)

    # 20 ECHO: core bounces 2<->11 in 20 frames (1 slot/frame); ghosts
    # lagged 5/10 frames; wall flash 3 frames at the ring edges; 12/13
    # flare toward the core as it crosses slot 10 (f=1.0) / 9,11
    # (f=0.5); re-based 2026-11-21 to RING10 + TOPFANS; 0/1 and 14..239
    # pure black; period 20 | 120
    fe0 = echo_frame(0)
    assert _s(fe0, 2) == ECHO_CORE         # core at the left edge
    assert _s(fe0, 6) == ECHO_G1 and _s(fe0, 7) == ECHO_G1
    assert _s(fe0, 10) == ECHO_G2 and _s(fe0, 11) == ECHO_G2
    assert _s(fe0, 12) == BEACON_BG and _s(fe0, 13) == BEACON_BG
    fe10 = echo_frame(10)
    assert _s(fe10, 11) == ECHO_CORE and _s(fe10, 2) == ECHO_G2
    assert _s(fe10, 6) == ECHO_G1
    assert _s(fe10, 12) == (151, 139, 127)   # core on slot 11: f=0.5
    assert echo_frame(0) == echo_frame(20)
    for ph in range(PHASE_PERIOD):
        fph = echo_frame(ph)
        u = ph % 20
        p = _echo_pos(u)
        c = int(p + 0.5)
        f = 1.0 if c == 10 else (0.5 if c in (9, 11) else 0.0)
        exp_top = tuple(int(b0 + f * (h0 - b0))
                        for b0, h0 in zip(BEACON_BG, ECHO_CORE))
        for i in TOPFANS:
            assert _s(fph, i) == exp_top
        for i in RING10:
            assert (fph[3 * i] >= BEACON_BG[0]
                    and fph[3 * i + 1] >= BEACON_BG[1]
                    and fph[3 * i + 2] >= BEACON_BG[2])
        for i in (0, 1):
            assert _s(fph, i) == (0, 0, 0)
        for i in range(14, NUM_SLOTS):
            assert _s(fph, i) == (0, 0, 0)

    # 21 ECG: trace scrolls RING10 at 1 slot/frame (beat period 30,
    # 4 beats/120); Q dip [10,12), T bump [14,18), R peak at u=12;
    # 12/13 flare toward the R peak as it crosses slot 10/9,11;
    # re-based 2026-11-21 to RING10 + TOPFANS (board 0..5 base + slot 4
    # rate lamp dropped — 0/1 pure black); 14..239 pure black
    feg0 = ecg_frame(0)
    assert _s(feg0, 2) == BEACON_BG
    assert _s(feg0, 10) == (48, 60, 40)    # Q dip over the base
    assert _s(feg0, 11) == (85, 85, 85)    # R falloff tail at u=11
    assert _s(feg0, 12) == BEACON_BG and _s(feg0, 13) == BEACON_BG
    assert _s(ecg_frame(27), 9) == ECG_R    # R peak on slot 9
    assert _s(ecg_frame(27), 12) == (151, 139, 127)   # peak on 9: f=0.5
    assert _s(ecg_frame(28), 10) == ECG_R    # R peak on slot 10
    assert _s(ecg_frame(28), 12) == ECG_R    # peak on 10: f=1.0
    for ph in range(PHASE_PERIOD):
        fph = ecg_frame(ph)
        peak = ((ph % 30) * (ECG_SPACING / 30.0) + ECG_PEAK_U) % ECG_SPACING
        f = (1.0 if 10.0 <= peak < 11.0
             else 0.5 if 9.0 <= peak < 10.0 or 11.0 <= peak < 12.0
             else 0.0)
        exp_top = tuple(int(b0 + f * (h0 - b0))
                        for b0, h0 in zip(BEACON_BG, ECG_R))
        for i in TOPFANS:
            assert _s(fph, i) == exp_top
        for i in RING10:
            assert (fph[3 * i] >= BEACON_BG[0]
                    and fph[3 * i + 1] >= BEACON_BG[1]
                    and fph[3 * i + 2] >= BEACON_BG[2])
        for i in (0, 1):
            assert _s(fph, i) == (0, 0, 0)
        for i in range(14, NUM_SLOTS):
            assert _s(fph, i) == (0, 0, 0)

    # 22 WALK: continuous round trip on RING10 (2<->11, 45 frames per
    # leg, 10-frame looks at each end); body bobs P=10; head 1 slot
    # ahead clamped to the ring, blink t=20..27; 12/13 flare toward the
    # head color on slot 10/9,11; re-based 2026-11-21 to RING10 +
    # TOPFANS (companion dot dropped — 0/1 pure black); 14..239 pure
    # black; period 120 | 120
    fw0 = walk_frame(0)
    assert _s(fw0, 2) == WALK_BODY and _s(fw0, 3) == WALK_HEAD
    assert _s(fw0, 12) == BEACON_BG and _s(fw0, 13) == BEACON_BG
    fw22 = walk_frame(22)
    assert _s(fw22, 7) == WALK_HEAD_BLINK   # mid-leg blink
    fw50 = walk_frame(50)
    assert _s(fw50, 11) == WALK_BODY and _s(fw50, 10) == WALK_HEAD_LOOK
    assert _s(fw50, 12) == WALK_HEAD        # head on slot 10: f=1.0
    fw110 = walk_frame(110)
    assert _s(fw110, 2) == WALK_BODY and _s(fw110, 3) == WALK_HEAD_LOOK
    assert _s(fw110, 12) == BEACON_BG
    for ph in range(PHASE_PERIOD):
        fph = walk_frame(ph)
        for i in RING10:
            assert (fph[3 * i] >= BEACON_BG[0]
                    and fph[3 * i + 1] >= BEACON_BG[1]
                    and fph[3 * i + 2] >= BEACON_BG[2])
        for i in TOPFANS:
            assert (fph[3 * i] >= BEACON_BG[0]
                    and fph[3 * i + 1] >= BEACON_BG[1]
                    and fph[3 * i + 2] >= BEACON_BG[2])
        for i in (0, 1):
            assert _s(fph, i) == (0, 0, 0)
        for i in range(14, NUM_SLOTS):
            assert _s(fph, i) == (0, 0, 0)

    # 23-25 GPU SPOTLIGHT (subject family): 12/13 are the subject — the
    # palette loop at the exact phase; everything else pure black
    # (user 2026-11-22: spotlight states light ONLY 12/13 — RING10
    # 2..11, 0/1, dead 14..239 all black; the 2026-11-21 RING10 wash
    # is removed).
    for st_name, fn, pal, period in (
            ("gpu-spotlight-idle", gpu_spotlight_idle_frame,
             SPOT_IDLE_PALETTE, PHASE_PERIOD),
            ("gpu-spotlight-fire", gpu_spotlight_fire_frame,
             SPOT_FIRE_PALETTE, SPOT_FIRE_PERIOD),
            ("gpu-spotlight-somewhat-active",
             gpu_spotlight_somewhat_active_frame,
             SPOT_ACTIVE_PALETTE, SPOT_ACTIVE_PERIOD)):
        for ph in range(PHASE_PERIOD):
            fph = fn(ph)
            c = _spot_color(pal, ph, period)
            assert _s(fph, 12) == c and _s(fph, 13) == c
            for i in RING10:
                assert _s(fph, i) == (0, 0, 0)
            for i in (0, 1):
                assert _s(fph, i) == (0, 0, 0)
            for dead in range(14, NUM_SLOTS):
                assert _s(fph, dead) == (0, 0, 0)
    # palette anchors land at their phases (subject; ring stays black)
    assert _s(gpu_spotlight_idle_frame(0), 12) == SPOT_IDLE_PALETTE[0]
    assert _s(gpu_spotlight_idle_frame(0), 2) == (0, 0, 0)
    assert _s(gpu_spotlight_idle_frame(40), 13) == SPOT_IDLE_PALETTE[1]
    assert _s(gpu_spotlight_idle_frame(80), 12) == SPOT_IDLE_PALETTE[2]
    assert _s(gpu_spotlight_fire_frame(0), 12) == SPOT_FIRE_PALETTE[0]
    assert _s(gpu_spotlight_fire_frame(0), 3) == (0, 0, 0)
    assert _s(gpu_spotlight_fire_frame(5), 13) == SPOT_FIRE_PALETTE[1]
    assert _s(gpu_spotlight_fire_frame(10), 12) == SPOT_FIRE_PALETTE[2]
    assert _s(gpu_spotlight_somewhat_active_frame(0), 12) ==         SPOT_ACTIVE_PALETTE[0]
    assert _s(gpu_spotlight_somewhat_active_frame(10), 13) ==         SPOT_ACTIVE_PALETTE[1]
    assert _s(gpu_spotlight_somewhat_active_frame(20), 12) ==         SPOT_ACTIVE_PALETTE[2]

    # 26 red_white: static — RING10 2..11 pure red, 12/13 white;
    # 0/1 live-unassigned black; 14..239 dead. Re-based 2026-11-21 to
    # RING10 + TOPFANS (old 6..11 span extended to the full 10-slot
    # ring).
    for ph in (0, 1, 59, 60, 119):
        fph = red_white_frame(ph)
        for i in RING10:
            assert _s(fph, i) == RED_WHITE_RED
        assert _s(fph, 12) == RED_WHITE_WHITE
        assert _s(fph, 13) == RED_WHITE_WHITE
        for i in (0, 1):
            assert _s(fph, i) == (0, 0, 0)
        for dead in range(14, NUM_SLOTS):
            assert _s(fph, dead) == (0, 0, 0)

    # 27 RAINBOW: 6-color wheel on RING10 — the six colors repeat over
    # the 10 slots (at phase 0: 2=red ... 7=violet, 8=red, 9=orange,
    # 10=yellow, 11=green); the wheel slides clockwise, one slot
    # position per 20 frames, one full rotation per 120-frame phase
    # (seamless: frame(120)==frame(0)); board mirror dropped — 0/1 pure
    # black; 12/13 (subject) cycle the six colors one at a time
    # (12f hold + 8f fade per color); 14..239 dead. Re-based 2026-11-21
    # to RING10 + TOPFANS.
    fr0 = rainbow_frame(0)
    for s, i in enumerate(RING10):
        assert _s(fr0, i) == RAINBOW_COLORS[s % 6]
    assert _s(fr0, 12) == RAINBOW_COLORS[0] and _s(fr0, 13) == RAINBOW_COLORS[0]
    for i in (0, 1):
        assert _s(fr0, i) == (0, 0, 0)
    # clockwise rotation: the red anchor walks +1 slot position every
    # 20 frames; one full rotation per 120 frames
    for s in range(10):
        assert _s(rainbow_frame(20 * s), RING10[s]) == RAINBOW_COLORS[0]
    assert rainbow_frame(0) == rainbow_frame(120)      # seamless at 120
    # exact lerp midpoint (ring slot 2 at t=10: halfway violet->red)
    assert _s(rainbow_frame(10), 2) == (207, 0, 127)
    # every frame: ring 2..11 + 12/13 all fully lit (no blink —
    # liveness), 0/1 pure black, 14..239 dead
    for ph in range(PHASE_PERIOD):
        fph = rainbow_frame(ph)
        for i in RING10:
            assert any(fph[3 * i:3 * i + 3]), (ph, i)
        for i in TOPFANS:
            assert any(fph[3 * i:3 * i + 3]), (ph, i)
        for i in (0, 1):
            assert _s(fph, i) == (0, 0, 0)
        for dead in range(14, NUM_SLOTS):
            assert _s(fph, dead) == (0, 0, 0)
    # 12/13 one-color-at-a-time: anchor at the hold start, still held at
    # the last hold frame, first fade frame exactly at the hold color
    # (the ramp completes at the next step boundary — the next
    # anchor assertion above)
    for s in range(6):
        assert _s(rainbow_frame(20 * s), 12) == RAINBOW_COLORS[s]
        assert _s(rainbow_frame(20 * s + RAINBOW_HOLD - 1), 13) == RAINBOW_COLORS[s]
        assert _s(rainbow_frame(20 * s + RAINBOW_HOLD), 12) == RAINBOW_COLORS[s]
    # smoothness: no ring/topfan slot jumps more than 1/8 of the full
    # range per frame anywhere in the 120-frame phase (no blink)
    prev = rainbow_frame(0)
    for ph in range(1, PHASE_PERIOD + 1):
        cur = rainbow_frame(ph % PHASE_PERIOD)
        for i in RING10:
            for ch in range(3):
                assert abs(cur[3 * i + ch] - prev[3 * i + ch]) <= 32, (ph, i)
        for i in TOPFANS:
            for ch in range(3):
                assert abs(cur[3 * i + ch] - prev[3 * i + ch]) <= 32, (ph, i)
        prev = cur

    # 28 LIMELIGHT: GPU star breath on 12/13 (60-frame period) + inverse
    # RING10 house wash (deep blue). Anchors: t=15 star peak (b=1):
    # 12/13 white-hot, ring at floor; t=45 star trough (b=0): 12/13
    # warm-gold floor, ring at top; t=0 exact half-breath. Period 60
    # | 120 bit-exact (precomputed breath table, indexed t % 60).
    # Liveness: live 2..13 never fully black; 0/1 pure black; dead
    # 14..239 black; smooth (no >1/8-range jump per frame). Re-based
    # 2026-11-21 to RING10 + TOPFANS (board 0..5 wash dropped — 0/1
    # pure black).
    assert LIMELIGHT_PERIOD == 60 and PHASE_PERIOD % LIMELIGHT_PERIOD == 0
    fl15 = limelight_frame(15)
    assert _s(fl15, 12) == LIMELIGHT_GPU_TOP
    assert _s(fl15, 13) == LIMELIGHT_GPU_TOP
    for i in RING10:
        assert _s(fl15, i) == LIMELIGHT_FAN_FLOOR
    fl45 = limelight_frame(45)
    assert _s(fl45, 12) == LIMELIGHT_GPU_FLOOR
    assert _s(fl45, 13) == LIMELIGHT_GPU_FLOOR
    for i in RING10:
        assert _s(fl45, i) == LIMELIGHT_FAN_TOP
    fl0 = limelight_frame(0)
    assert _s(fl0, 12) == (172, 157, 135)     # half-breath on the star
    assert _s(fl0, 2) == (19, 30, 90)         # half-breath on the ring wash
    for i in (0, 1):
        assert _s(fl0, i) == (0, 0, 0)
    for ph in range(PHASE_PERIOD):            # period 60: bit-identical
        assert (limelight_frame(ph)
                == limelight_frame((ph + 60) % PHASE_PERIOD))
    prev = limelight_frame(0)
    for ph in range(1, PHASE_PERIOD + 1):
        fph = limelight_frame(ph % PHASE_PERIOD)
        for i in RING10:
            assert any(fph[3 * i:3 * i + 3]), (ph, i)
        for i in TOPFANS:
            assert any(fph[3 * i:3 * i + 3]), (ph, i)
        for i in (0, 1):
            assert _s(fph, i) == (0, 0, 0)
        for dead in range(14, NUM_SLOTS):
            assert _s(fph, dead) == (0, 0, 0)
        for i in RING10:
            for ch in range(3):
                assert abs(fph[3 * i + ch] - prev[3 * i + ch]) <= 32, (ph, i)
        for i in TOPFANS:
            for ch in range(3):
                assert abs(fph[3 * i + ch] - prev[3 * i + ch]) <= 32, (ph, i)
        prev = fph

    # 29 CATWALK: runway show — RING10 = magenta carpet + a strutting
    # warm-white 1-slot head (P10, 1 slot/frame) + 2-slot hot-pink
    # tail; every lap (t%10==0) photographers 12/13 fire a 2-frame
    # near-white flash + 1-frame afterglow over a dim standby; audience
    # dropped — 0/1 pure black. Period 10 | 120 bit-exact; the flash is
    # an INTENTIONAL hard blink (no smoothness bound — a photo flash
    # blinks); live 2..13 colored every frame; 14..239 dead. Re-based
    # 2026-11-21 to RING10 + TOPFANS.
    for ph in range(PHASE_PERIOD):            # period 10: bit-identical
        assert catwalk_frame(ph) == catwalk_frame((ph + 10) % PHASE_PERIOD)
    for t in range(20):                       # covers all 10 lap phases x2
        f = catwalk_frame(t)
        h = t % 10
        assert _s(f, RING10[h]) == CATWALK_HEAD
        assert _s(f, RING10[(h - 1) % 10]) == CATWALK_TAIL1
        assert _s(f, RING10[(h - 2) % 10]) == CATWALK_TAIL2
        for s in range(10):
            if s not in (h, (h - 1) % 10, (h - 2) % 10):
                assert _s(f, RING10[s]) == CATWALK_BASE
        k = t % 10
        if k <= 1:
            p = CATWALK_PHOTO_FLASH
        elif k == 2:
            p = CATWALK_PHOTO_AFTER
        else:
            p = CATWALK_PHOTO_BASE
        for i in TOPFANS:
            assert _s(f, i) == p
        for i in (0, 1):
            assert _s(f, i) == (0, 0, 0)
    # the flash is the point: near-white, and distinct from the standby
    for ch in range(3):
        assert CATWALK_PHOTO_FLASH[ch] >= 200
    assert CATWALK_PHOTO_FLASH != CATWALK_PHOTO_BASE
    assert CATWALK_PHOTO_AFTER != CATWALK_PHOTO_BASE
    for ph in range(PHASE_PERIOD):
        fph = catwalk_frame(ph)
        for i in RING10:
            assert any(fph[3 * i:3 * i + 3]), (ph, i)
        for i in TOPFANS:
            assert any(fph[3 * i:3 * i + 3]), (ph, i)
        for i in (0, 1):
            assert _s(fph, i) == (0, 0, 0)
        for dead in range(14, NUM_SLOTS):
            assert _s(fph, dead) == (0, 0, 0)

    # 30 SUPERNOVA: a 6-second cosmic story in one loop (period 120 |
    # 120) — the GPU itself (12/13) is a STAR. BUILDUP t0..39 (star
    # charges dim gold -> white-hot; RING10 swells a blue corona);
    # DETONATION t40..45 (ring 2..11 + star near-white 2 f -> hot-gold
    # fireball — the intentional hard blink, the climax); SHOCKWAVE
    # t46..85 (a bright 1-slot head + 1-slot fading tail band sweeps
    # RING10, P10, 1 slot/frame, cooling bright gold -> deep orange-red
    # over cooling ember; the band fades out over the last 10 f; the
    # star is a remnant pulsar); AFTERGLOW t86..119 (the ring blends
    # ember -> calm night blue; the pulsar settles back into the dim
    # gold rest star). SEAMLESS LOOP: frame(119)==frame(0) — the night
    # falls and a new star begins to charge. Board ring dropped — 0/1
    # pure black; 14..239 dead, never painted. Re-based 2026-11-21 to
    # RING10 + TOPFANS.
    assert supernova_frame(0) == supernova_frame(120)      # period 120
    assert supernova_frame(119) == supernova_frame(0)      # seamless loop
    # BUILDUP start = the night rest state (the loop seam)
    f0 = supernova_frame(0)
    assert _s(f0, 12) == (89, 61, 39) and _s(f0, 13) == (89, 61, 39)
    for i in RING10:
        assert _s(f0, i) == (10, 14, 40)
    for i in (0, 1):
        assert _s(f0, i) == (0, 0, 0)
    # DETONATION: the explosion — ring + star near-white, then fireball
    for i in list(RING10) + [12, 13]:
        assert _s(supernova_frame(40), i) == (255, 255, 250)
        assert _s(supernova_frame(41), i) == (255, 255, 250)
    for i in list(RING10) + [12, 13]:
        assert _s(supernova_frame(42), i) == (255, 242, 210)
    for i in list(RING10) + [12, 13]:
        assert _s(supernova_frame(45), i) == (255, 205, 90)
    # SHOCKWAVE start: band head + fading tail on RING10 (pos 0); the
    # rest cooling ember; the star has collapsed into a dim remnant
    # pulsar
    f46 = supernova_frame(46)
    assert _s(f46, 2) == (255, 210, 70)         # band head (pos 0)
    assert _s(f46, 11) == (172, 127, 44)       # band tail (pos 9)
    for i in RING10:
        if i not in (2, 11):
            assert _s(f46, i) == (90, 44, 18)  # cooling ember
    assert _s(f46, 12) == (129, 119, 110)      # remnant pulsar
    assert _s(f46, 13) == (129, 119, 110)
    for i in (0, 1):
        assert _s(f46, i) == (0, 0, 0)
    # SHOCKWAVE end: band fully faded -> ring uniform cooling ember
    f85 = supernova_frame(85)
    for i in RING10:
        assert _s(f85, i) == (50, 22, 10)
    # AFTERGLOW start: continuous with the shockwave end (no jump)
    f86 = supernova_frame(86)
    for i in RING10:
        assert _s(f86, i) == (50, 22, 10)
    # AFTERGLOW end = the loop seam = the BUILDUP start (seamless)
    f119 = supernova_frame(119)
    assert _s(f119, 12) == (89, 61, 39) and _s(f119, 13) == (89, 61, 39)
    for i in RING10:
        assert _s(f119, i) == (10, 14, 40)
    for i in (0, 1):
        assert _s(f119, i) == (0, 0, 0)
    # liveness: live 2..13 colored every frame; 0/1 pure black; dead
    # 14..239 black
    for ph in range(PHASE_PERIOD):
        fph = supernova_frame(ph)
        for i in RING10:
            assert any(fph[3 * i:3 * i + 3]), (ph, i)
        for i in TOPFANS:
            assert any(fph[3 * i:3 * i + 3]), (ph, i)
        for i in (0, 1):
            assert _s(fph, i) == (0, 0, 0)
        for dead in range(14, NUM_SLOTS):
            assert _s(fph, dead) == (0, 0, 0)

    # 31 INFERNO: the GPU sits underneath a furnace (re-based 2026-11-21
    # to RING10 + TOPFANS). RING10 (slots 2..11) slowly cycles dark red
    # (t=0) -> bright orange (t=30), back (period 60 | 120); top fans
    # 12/13 (the subject) pulse randomly like flames — bright
    # yellow/orange base lerp'd toward near-white (deterministic
    # INFERNO_FLICKER table) with hard near-white sparks whenever the
    # table crosses INFERNO_SPARK_LEVEL. The old board ring 0..5 dim
    # ember is dropped — 0/1 pure black. Period 120; live 2..13 colored
    # every frame; dead 14..239 black.
    assert INFERNO_PERIOD == 60 and PHASE_PERIOD % INFERNO_PERIOD == 0
    assert inferno_frame(0) == inferno_frame(120)      # period 120
    f0 = inferno_frame(0)                              # t=0: cycle trough
    for i in RING10:
        assert _s(f0, i) == INFERNO_FAN_DARK
    for i in (0, 1):
        assert _s(f0, i) == (0, 0, 0)
    f30 = inferno_frame(30)                            # t=30: cycle peak
    for i in RING10:
        assert _s(f30, i) == INFERNO_FAN_HOT
    # flame flicker: 12/13 always at/above the bright base, in sync,
    # white sparks occur but are the minority of frames
    sparks = []
    for ph in range(PHASE_PERIOD):
        fph = inferno_frame(ph)
        c = _s(fph, 12)
        assert c == _s(fph, 13)                        # both top fans sync
        assert (c[0] >= INFERNO_TOP_BASE[0]
                and c[1] >= INFERNO_TOP_BASE[1]
                and c[2] >= INFERNO_TOP_BASE[2])
        if c == INFERNO_TOP_WHITE:
            sparks.append(ph)
    assert 5 <= len(sparks) <= 30                     # occasional sparks
    # liveness: RING10 + 12/13 colored every frame; 0/1 pure black;
    # dead 14..239 black
    for ph in range(PHASE_PERIOD):
        fph = inferno_frame(ph)
        for i in RING10:
            assert any(fph[3 * i:3 * i + 3]), (ph, i)
        for i in TOPFANS:
            assert any(fph[3 * i:3 * i + 3]), (ph, i)
        for i in (0, 1):
            assert _s(fph, i) == (0, 0, 0)
        for dead in range(14, NUM_SLOTS):
            assert _s(fph, dead) == (0, 0, 0)

    # 32 REACTOR-CORE: the GPU as the power source of a futuristic
    # reactor (re-based 2026-11-21 to RING10 + TOPFANS). RING10 (slots
    # 2..11): deep blue, slowly pulsing (60-frame breath, t=0 trough,
    # t=30 peak, 60 | 120); top fans 12/13 (the subject): bright cyan
    # flashing brighter toward near-white every 60 frames (3 s) on a
    # 13-frame raised-cosine pulse (u=0..12, peak u=6) — the energy
    # transfer into the GPU. The old board ring 0..5 dim blue wash is
    # dropped — 0/1 pure black. Period 120 | 120 bit-exact; live 2..13
    # colored every frame; dead 14..239 black.
    assert REACTOR_PERIOD == 60 and PHASE_PERIOD % REACTOR_PERIOD == 0
    assert reactor_core_frame(0) == reactor_core_frame(120)  # period 120
    f0 = reactor_core_frame(0)             # t=0: breath trough, no flash
    for i in RING10:
        assert _s(f0, i) == REACTOR_FAN_DARK
    for i in (0, 1):
        assert _s(f0, i) == (0, 0, 0)
    assert _s(f0, 12) == REACTOR_TOP_BASE and _s(f0, 13) == REACTOR_TOP_BASE
    f30 = reactor_core_frame(30)           # t=30: breath peak, no flash
    for i in RING10:
        assert _s(f30, i) == REACTOR_FAN_BRIGHT
    assert _s(f30, 12) == REACTOR_TOP_BASE and _s(f30, 13) == REACTOR_TOP_BASE
    # flash: p=1 at u=6 of each 60-frame cycle (t=6, 66): near-white top
    for ph in (6, 66):
        fph = reactor_core_frame(ph)
        assert _s(fph, 12) == REACTOR_TOP_FLASH
        assert _s(fph, 13) == REACTOR_TOP_FLASH
    # top fans: both sync; R/G never below the bright cyan base; B in the
    # 250..255 band (the base is bluer than the near-white flash)
    for ph in range(PHASE_PERIOD):
        fph = reactor_core_frame(ph)
        c = _s(fph, 12)
        assert c == _s(fph, 13)                        # both top fans sync
        assert c[0] >= REACTOR_TOP_BASE[0] and c[1] >= REACTOR_TOP_BASE[1]
        assert c[2] >= REACTOR_TOP_FLASH[2]
    # liveness: RING10 + 12/13 colored every frame; 0/1 pure black;
    # dead 14..239 black
    for ph in range(PHASE_PERIOD):
        fph = reactor_core_frame(ph)
        for i in RING10:
            assert any(fph[3 * i:3 * i + 3]), (ph, i)
        for i in TOPFANS:
            assert any(fph[3 * i:3 * i + 3]), (ph, i)
        for i in (0, 1):
            assert _s(fph, i) == (0, 0, 0)
        for dead in range(14, NUM_SLOTS):
            assert _s(fph, dead) == (0, 0, 0)
    # smoothness: no slot on the live set jumps more than 1/8 of the
    # full range per frame anywhere in the 120-frame phase (no blink)
    prev = reactor_core_frame(0)
    for ph in range(1, PHASE_PERIOD + 1):
        fph = reactor_core_frame(ph % PHASE_PERIOD)
        for i in RING10 + TOPFANS:
            for ch in range(3):
                assert abs(fph[3 * i + ch] - prev[3 * i + ch]) <= 32, (ph, i)
        prev = fph

    # 33 MATRIX: a computer running a massive simulation (re-based
    # 2026-11-21 to RING10 + TOPFANS). RING10 (slots 2..11): very dark
    # green (MATRIX_FAN_DARK) at rest; one bright green flash
    # (MATRIX_FLASH) across the whole ring per 120-frame phase (6 s,
    # "occasionally") on an 11-frame raised-cosine bump (u=0..10,
    # exactly 0 at u=0 and u=10, peak 1 at u=5) — the data burst. Top
    # fans 12/13 (the subject): bright neon green, slowly pulsing
    # (t=0 trough, t=30 peak; 60 | 120), lerp'd toward MATRIX_TOP_FLASH
    # by the flash bump. The old board ring 0..5 dim green wash is
    # dropped — 0/1 pure black. Period 120 | 120 (bit-exact); live
    # 2..13 colored every frame; dead 14..239 black.
    assert (MATRIX_BREATH_PERIOD == 60
            and PHASE_PERIOD % MATRIX_BREATH_PERIOD == 0)
    assert matrix_frame(0) == matrix_frame(120)          # period 120
    f0 = matrix_frame(0)                  # t=0: flash off, pulse trough
    for i in RING10:
        assert _s(f0, i) == MATRIX_FAN_DARK
    for i in (0, 1):
        assert _s(f0, i) == (0, 0, 0)
    assert _s(f0, 12) == MATRIX_TOP_DIM and _s(f0, 13) == MATRIX_TOP_DIM
    f30 = matrix_frame(30)               # t=30: pulse peak, flash off
    for i in RING10:
        assert _s(f30, i) == MATRIX_FAN_DARK
    assert _s(f30, 12) == MATRIX_TOP_BRIGHT and _s(f30, 13) == MATRIX_TOP_BRIGHT
    f5 = matrix_frame(5)                 # t=5: flash peak (p=1)
    for i in RING10:
        assert _s(f5, i) == MATRIX_FLASH
    assert _s(f5, 12) == MATRIX_TOP_FLASH and _s(f5, 13) == MATRIX_TOP_FLASH
    # the flash hits ALL of RING10 uniformly (one color on 2..11) and
    # occurs exactly once per phase: nonzero for u=1..9, exactly
    # baseline at u=0 and u=10 (the ring moves only with the flash)
    flash_frames = []
    for ph in range(PHASE_PERIOD):
        fph = matrix_frame(ph)
        if _s(fph, 2)[1] != MATRIX_FAN_DARK[1]:
            flash_frames.append(ph)
        for i in RING10:
            assert _s(fph, i) == _s(fph, 2)              # uniform flash
    assert flash_frames == [1, 2, 3, 4, 5, 6, 7, 8, 9]
    # top fans: both sync; green never below the dim neon trough
    for ph in range(PHASE_PERIOD):
        fph = matrix_frame(ph)
        c = _s(fph, 12)
        assert c == _s(fph, 13)                          # both top sync
        assert c[1] >= MATRIX_TOP_DIM[1]
    # liveness: RING10 + 12/13 colored every frame; 0/1 pure black;
    # dead 14..239 black
    for ph in range(PHASE_PERIOD):
        fph = matrix_frame(ph)
        for i in RING10:
            assert any(fph[3 * i:3 * i + 3]), (ph, i)
        for i in TOPFANS:
            assert any(fph[3 * i:3 * i + 3]), (ph, i)
        for i in (0, 1):
            assert _s(fph, i) == (0, 0, 0)
        for dead in range(14, NUM_SLOTS):
            assert _s(fph, dead) == (0, 0, 0)

    # 34 CYBERPUNK: futuristic neon aesthetic (re-based 2026-11-21 to
    # RING10 + TOPFANS). RING10 (slots 2..11): purple slowly
    # transitioning to electric blue (60-frame cosine breath, t=0
    # purple, t=30 blue, back; 60 | 120); top fans 12/13 (the subject):
    # electric cyan that flashes magenta once per 120-frame phase
    # (6 s, "occasionally") on a 27-frame raised-cosine pulse (u=0..26,
    # peak u=13, exactly 0 at u=0 and u=26). The old board ring 0..5
    # dim purple->blue wash is dropped — 0/1 pure black. Period 120 |
    # 120 bit-exact; live 2..13 colored every frame; dead 14..239 black.
    assert CYBER_PERIOD == 60 and PHASE_PERIOD % CYBER_PERIOD == 0
    assert cyberpunk_frame(0) == cyberpunk_frame(120)      # period 120
    f0 = cyberpunk_frame(0)                  # t=0: purple, no flash
    for i in RING10:
        assert _s(f0, i) == CYBER_FAN_PURPLE
    for i in (0, 1):
        assert _s(f0, i) == (0, 0, 0)
    assert _s(f0, 12) == CYBER_TOP_BASE and _s(f0, 13) == CYBER_TOP_BASE
    f30 = cyberpunk_frame(30)               # t=30: blue peak, no flash
    for i in RING10:
        assert _s(f30, i) == CYBER_FAN_BLUE
    assert _s(f30, 12) == CYBER_TOP_BASE and _s(f30, 13) == CYBER_TOP_BASE
    # t=13: flash peak (p=1): top fans exactly magenta; RING10 is its
    # t=13 breath value
    f13 = cyberpunk_frame(13)
    assert _s(f13, 12) == CYBER_TOP_FLASH and _s(f13, 13) == CYBER_TOP_FLASH
    for i in RING10:
        assert _s(f13, i) == _slerp(CYBER_FAN_PURPLE, CYBER_FAN_BLUE,
                                    CYBER_BREATH[13])
    # the magenta flash occurs exactly once per phase: nonzero for
    # u=1..25, exactly baseline at u=0 and u=26 (top fans only; the
    # ring moves only with the breath)
    flash_frames = []
    for ph in range(PHASE_PERIOD):
        fph = cyberpunk_frame(ph)
        if _s(fph, 12) != CYBER_TOP_BASE:
            flash_frames.append(ph)
        assert _s(fph, 12) == _s(fph, 13)            # both top fans sync
    assert flash_frames == list(range(1, 26))
    # top fans: cyan->magenta, G/B never below the magenta-peak channels
    # (the flash is the extreme), R never below the cyan base
    for ph in range(PHASE_PERIOD):
        fph = cyberpunk_frame(ph)
        c = _s(fph, 12)
        assert c[0] >= CYBER_TOP_BASE[0]
        assert c[1] >= CYBER_TOP_FLASH[1]
        assert c[2] >= CYBER_TOP_FLASH[2]
    # liveness: RING10 + 12/13 colored every frame; 0/1 pure black;
    # dead 14..239 black
    for ph in range(PHASE_PERIOD):
        fph = cyberpunk_frame(ph)
        for i in RING10:
            assert any(fph[3 * i:3 * i + 3]), (ph, i)
        for i in TOPFANS:
            assert any(fph[3 * i:3 * i + 3]), (ph, i)
        for i in (0, 1):
            assert _s(fph, i) == (0, 0, 0)
        for dead in range(14, NUM_SLOTS):
            assert _s(fph, dead) == (0, 0, 0)
    # smoothness: no slot on the live set jumps more than 1/8 of the
    # full range per frame anywhere in the 120-frame phase (no blink)
    prev = cyberpunk_frame(0)
    for ph in range(1, PHASE_PERIOD + 1):
        fph = cyberpunk_frame(ph % PHASE_PERIOD)
        for i in RING10 + TOPFANS:
            for ch in range(3):
                assert abs(fph[3 * i + ch] - prev[3 * i + ch]) <= 32, (ph, i)
        prev = fph

    # 35 CRYOGENIC: frozen GPU cooling chamber (re-based 2026-11-21 to
    # RING10 + TOPFANS). RING10 (slots 2..11): dim icy blue slowly
    # fading toward icy white and back (60-frame cosine breath,
    # t=0 blue trough, t=30 white peak; 60 | 120); top fans 12/13
    # (the subject): bright white that flashes pure white once per
    # 120-frame phase (6 s, "occasionally") on an 11-frame raised-
    # cosine pulse (u=0..10, exactly 0 at u=0 and u=10, peak 1 at
    # u=5). The old board ring 0..5 dim icy-blue wash is dropped —
    # 0/1 pure black. Period 120 | 120 bit-exact; live 2..13 colored
    # every frame; dead 14..239 black.
    assert CRYO_PERIOD == 60 and PHASE_PERIOD % CRYO_PERIOD == 0
    assert cryogenic_frame(0) == cryogenic_frame(120)      # period 120
    f0 = cryogenic_frame(0)              # t=0: blue trough, no flash
    for i in RING10:
        assert _s(f0, i) == CRYO_FAN_DARK
    for i in (0, 1):
        assert _s(f0, i) == (0, 0, 0)
    assert _s(f0, 12) == CRYO_TOP_BASE and _s(f0, 13) == CRYO_TOP_BASE
    f30 = cryogenic_frame(30)            # t=30: white peak, no flash
    for i in RING10:
        assert _s(f30, i) == CRYO_FAN_WHITE
    assert _s(f30, 12) == CRYO_TOP_BASE and _s(f30, 13) == CRYO_TOP_BASE
    # t=5: flash peak (p=1): top fans exactly pure white; RING10 is
    # its t=5 breath value
    f5 = cryogenic_frame(5)
    assert _s(f5, 12) == CRYO_TOP_FLASH and _s(f5, 13) == CRYO_TOP_FLASH
    for i in RING10:
        assert _s(f5, i) == _slerp(CRYO_FAN_DARK, CRYO_FAN_WHITE,
                                   CRYO_BREATH[5])
    # the white flash occurs exactly once per phase: nonzero for
    # u=1..9, exactly baseline at u=0 and u=10 (top fans only; the
    # ring moves only with the breath)
    flash_frames = []
    for ph in range(PHASE_PERIOD):
        fph = cryogenic_frame(ph)
        if _s(fph, 12) != CRYO_TOP_BASE:
            flash_frames.append(ph)
        assert _s(fph, 12) == _s(fph, 13)            # both top fans sync
    assert flash_frames == list(range(1, 10))
    # top fans: both sync; R/G/B never below the bright white rest
    # (the pure white flash is >= the bright white on every channel)
    for ph in range(PHASE_PERIOD):
        fph = cryogenic_frame(ph)
        c = _s(fph, 12)
        assert c[0] >= CRYO_TOP_BASE[0]
        assert c[1] >= CRYO_TOP_BASE[1]
        assert c[2] >= CRYO_TOP_BASE[2]
    # liveness: RING10 + 12/13 colored every frame; 0/1 pure black;
    # dead 14..239 black
    for ph in range(PHASE_PERIOD):
        fph = cryogenic_frame(ph)
        for i in RING10:
            assert any(fph[3 * i:3 * i + 3]), (ph, i)
        for i in TOPFANS:
            assert any(fph[3 * i:3 * i + 3]), (ph, i)
        for i in (0, 1):
            assert _s(fph, i) == (0, 0, 0)
        for dead in range(14, NUM_SLOTS):
            assert _s(fph, dead) == (0, 0, 0)
    # smoothness: no slot on the live set jumps more than 1/8 of the
    # full range per frame anywhere in the 120-frame phase (no blink)
    prev = cryogenic_frame(0)
    for ph in range(1, PHASE_PERIOD + 1):
        fph = cryogenic_frame(ph % PHASE_PERIOD)
        for i in RING10 + TOPFANS:
            for ch in range(3):
                assert abs(fph[3 * i + ch] - prev[3 * i + ch]) <= 32, (ph, i)
        prev = fph

    # 36 EMERGENCY-MODE (re-based 2026-11-21 to RING10 + TOPFANS): the
    # standard ring RING10 (slots 2..11) sits dark blue (EMG_FAN_DARK)
    # and strobes red/amber — t=0 red (EMG_FAN_RED), t=1 amber
    # (EMG_FLASH_AMBER), t=2 red — 3 frames (150 ms) once per 120-frame
    # phase, a hard blink (strobe); top fans 12/13 (the subject) strobe
    # IN SYNC with the ring — the same red/amber flash frames — and hold
    # the dim red/amber base (EMG_TOP_BASE, the floor) otherwise, never
    # black. The old board ring 0..5 is dropped — 0/1 pure black.
    # Period 120 | 120 bit-exact; live 2..13 colored every frame; dead
    # 14..239 black.
    assert EMG_FLASH_LEN == 3
    assert emergency_mode_frame(0) == emergency_mode_frame(120)  # period
    f0 = emergency_mode_frame(0)          # t=0: red flash frame
    for i in RING10:
        assert _s(f0, i) == EMG_FAN_RED
    assert _s(f0, 12) == EMG_FAN_RED and _s(f0, 13) == EMG_FAN_RED
    f1 = emergency_mode_frame(1)          # t=1: amber flash frame
    for i in RING10:
        assert _s(f1, i) == EMG_FLASH_AMBER
    assert _s(f1, 12) == EMG_FLASH_AMBER and _s(f1, 13) == EMG_FLASH_AMBER
    f2 = emergency_mode_frame(2)          # t=2: red flash frame
    for i in RING10:
        assert _s(f2, i) == EMG_FAN_RED
    f3 = emergency_mode_frame(3)          # t=3: flash over, dark rest
    for i in RING10:
        assert _s(f3, i) == EMG_FAN_DARK
    assert _s(f3, 12) == EMG_TOP_BASE and _s(f3, 13) == EMG_TOP_BASE
    for i in (0, 1):
        assert _s(f3, i) == (0, 0, 0)     # old board ring dropped
    # the red/amber strobe fires exactly on t=0..2 per phase, in sync on
    # RING10 and 12/13; outside the flash RING10 holds the dark rest and
    # 12/13 hold the dim base floor (never black)
    for ph in range(PHASE_PERIOD):
        fph = emergency_mode_frame(ph)
        if ph < EMG_FLASH_LEN:
            exp = (EMG_FAN_RED, EMG_FLASH_AMBER, EMG_FAN_RED)[ph]
            for i in RING10:
                assert _s(fph, i) == exp, (ph, i)
            assert _s(fph, 12) == exp and _s(fph, 13) == exp, ph
        else:
            for i in RING10:
                assert _s(fph, i) == EMG_FAN_DARK, (ph, i)
            assert _s(fph, 12) == EMG_TOP_BASE, ph
            assert _s(fph, 13) == EMG_TOP_BASE, ph
        c = _s(fph, 12)
        assert (c[0] >= BEACON_BG[0] and c[1] >= BEACON_BG[1]
                and c[2] >= BEACON_BG[2]), ph
    # liveness: live 2..13 colored every frame; 0/1 pure black; dead
    # 14..239 black
    for ph in range(PHASE_PERIOD):
        fph = emergency_mode_frame(ph)
        for i in range(2, 14):
            assert any(fph[3 * i:3 * i + 3]), (ph, i)
        for i in (0, 1):
            assert _s(fph, i) == (0, 0, 0)
        for dead in range(14, NUM_SLOTS):
            assert _s(fph, dead) == (0, 0, 0)

    # 37 LIGHTNING-STRIKE (re-based 2026-11-21 to RING10 + TOPFANS): a
    # lightning strike. Top fans 12/13 (the strike point, the subject):
    # dim blue rest (LS_TOP_DARK, >= BEACON_BG floor) that flare pure
    # white (LS_TOP_FLASH) on t=0..1 (2 frames, 100 ms); the standard
    # ring RING10 (slots 2..11) sits dark blue (LS_FAN_DARK) and, "a
    # split second" later, flashes the cyan wash (LS_FAN_FLASH) on t=2..3
    # (2 frames, 100 ms) — sequential, non-overlapping; everything
    # returns to dark after t=3. The old board ring 0..5 wash/ripple is
    # dropped — 0/1 pure black. Period 120 | 120 bit-exact; live 2..13
    # colored every frame; dead 14..239 black.
    assert LS_TOP_FLASH_LEN == 2 and LS_FAN_FLASH_LEN == 2
    assert LS_FAN_FLASH_DELAY == LS_TOP_FLASH_LEN   # blue starts at white's end
    assert LS_FAN_FLASH_DELAY + LS_FAN_FLASH_LEN <= PHASE_PERIOD
    assert lightning_strike_frame(0) == lightning_strike_frame(120)  # period
    f1 = lightning_strike_frame(1)          # t=1: inside the white strike
    for i in TOPFANS:
        assert _s(f1, i) == LS_TOP_FLASH
    for i in RING10:
        assert _s(f1, i) == LS_FAN_DARK     # ring still dark blue
    f3 = lightning_strike_frame(3)          # t=3: inside the cyan wash
    for i in RING10:
        assert _s(f3, i) == LS_FAN_FLASH
    for i in TOPFANS:
        assert _s(f3, i) == LS_TOP_DARK     # top back to dim rest
    f4 = lightning_strike_frame(4)          # t=4: everything returned to dark
    for i in RING10:
        assert _s(f4, i) == LS_FAN_DARK
    for i in TOPFANS:
        assert _s(f4, i) == LS_TOP_DARK
    for i in (0, 1):
        assert _s(f4, i) == (0, 0, 0)       # old board ring dropped
    # the white strike fires exactly on t=0..1 and the cyan wash exactly
    # on t=2..3 per phase (sequential, non-overlapping)
    for ph in range(PHASE_PERIOD):
        fph = lightning_strike_frame(ph)
        top_flashing = all(_s(fph, i) == LS_TOP_FLASH for i in TOPFANS)
        assert top_flashing == (ph < LS_TOP_FLASH_LEN), ph
        fan_flashing = all(_s(fph, i) == LS_FAN_FLASH for i in RING10)
        assert fan_flashing == (
            LS_FAN_FLASH_DELAY <= ph
            < LS_FAN_FLASH_DELAY + LS_FAN_FLASH_LEN), ph
    # 12/13 never below the dim rest floor (never black)
    for ph in range(PHASE_PERIOD):
        fph = lightning_strike_frame(ph)
        c = _s(fph, 12)
        assert (c[0] >= BEACON_BG[0] and c[1] >= BEACON_BG[1]
                and c[2] >= BEACON_BG[2]), ph
    # liveness: live 2..13 colored every frame; 0/1 pure black; dead
    # 14..239 black
    for ph in range(PHASE_PERIOD):
        fph = lightning_strike_frame(ph)
        for i in range(2, 14):
            assert any(fph[3 * i:3 * i + 3]), (ph, i)
        for i in (0, 1):
            assert _s(fph, i) == (0, 0, 0)
        for dead in range(14, NUM_SLOTS):
            assert _s(fph, dead) == (0, 0, 0)

    # 38 SWEEPING-RAINBOW (re-based 2026-11-21 to RING10 + TOPFANS): the
    # full rainbow sweep on the standard ring RING10 (slots 2..11). The
    # ring starts in the current rainbow base color; a slow radar-like
    # white sweep (1 slot/12 frames, one full pass around RING10 per
    # 120-frame phase — 6 s) converts them to orange as it passes (head
    # slot pure white, the just-left slot decays white -> orange over 12
    # frames, everything swept orange, the unswept ahead slots show the
    # base color); the base color cycles all seven colors in order (red
    # 18f, then 6 x 17f = 120, seamless; last 4f of each segment fade to
    # the next). Top fans 12/13 (the subject) track the head color DIM —
    # half-brightness of the base color the head is on, floored to
    # BEACON_BG (never black). The old board ring 0..5 mirror is dropped
    # — 0/1 pure black. Period 120 | 120; live 2..13 colored every
    # frame; dead 14..239 black.
    assert len(RING10) * SR_SWEEP_SLOT_FRAMES == SR_SWEEP_PERIOD  # pass fits ring
    assert PHASE_PERIOD % SR_SWEEP_PERIOD == 0                  # period rule
    assert SR_SEG_START[0] == 0
    assert SR_SEG_START[6] + 17 == PHASE_PERIOD                # violet ends
    assert sum(SR_SEG_START[i + 1] - SR_SEG_START[i] for i in range(6)) \
        == PHASE_PERIOD - 17                                   # all 7 segments
    assert all(SR_SEG_START[i + 1] - SR_SEG_START[i] == 17
               for i in range(1, 6))                           # 5 x 17 frames
    assert SR_SEG_START[1] - SR_SEG_START[0] == 18             # red 18 frames
    assert len(set(SR_COLORS)) == 7                            # 7 colors
    assert sweeping_rainbow_frame(0) == sweeping_rainbow_frame(120)  # period
    f0 = sweeping_rainbow_frame(0)
    assert _s(f0, 2) == SR_WHITE                 # t=0: head at slot 2
    for s in range(1, 10):                      # unswept ring starts red
        assert _s(f0, RING10[s]) == SR_RED
    assert _s(f0, 12) == _on_base(
        tuple(int(c * SR_TOP_DIM) for c in SR_RED))
    for ph in range(PHASE_PERIOD):
        fph = sweeping_rainbow_frame(ph)
        h = ph // SR_SWEEP_SLOT_FRAMES
        assert _s(fph, RING10[h]) == SR_WHITE   # the white head, 1 slot
        base = _sr_base(ph)
        for s in range(10):
            if s > h:                            # ahead: unswept base color
                assert _s(fph, RING10[s]) == base, (ph, s)
            elif s < h:                          # behind: orange (+ decay)
                age = ph - (s + 1) * SR_SWEEP_SLOT_FRAMES
                assert _s(fph, RING10[s]) == _slerp(
                    SR_ORANGE, SR_WHITE,
                    1.0 - age / float(SR_SWEEP_SLOT_FRAMES)), (ph, s)
        if h >= 2:                               # 2+ slots behind slot 2
            assert _s(fph, 2) == SR_ORANGE      # -> converted to orange
        # 12/13 track the head color dim, in sync on both slots
        c12 = _s(fph, 12)
        assert _s(fph, 13) == c12
        assert c12 == _on_base(tuple(int(c * SR_TOP_DIM) for c in base)), ph
        # never black (floored to BEACON_BG)
        assert (c12[0] >= BEACON_BG[0] and c12[1] >= BEACON_BG[1]
                and c12[2] >= BEACON_BG[2]), ph
    # the ring base color holds each of the seven colors at its segment
    # start and covers all seven across the loop
    for seg, start in enumerate(SR_SEG_START):
        assert _sr_base(start) == SR_COLORS[seg]
        fs = sweeping_rainbow_frame(start)
        h = start // SR_SWEEP_SLOT_FRAMES
        for s in range(10):
            if s > h:
                assert _s(fs, RING10[s]) == SR_COLORS[seg]
    assert set(SR_COLORS) <= set(_sr_base(t) for t in range(PHASE_PERIOD))
    # 12/13 track each of the seven colors (dimmed + floored) at the
    # segment start, in sync with the base color
    held = set()
    for seg, start in enumerate(SR_SEG_START):
        fs = sweeping_rainbow_frame(start)
        c12 = _s(fs, 12)
        assert c12 == _on_base(
            tuple(int(c * SR_TOP_DIM) for c in SR_COLORS[seg]))
        held.add(c12)
    assert len(held) == 7
    # liveness: live 2..13 colored every frame; 0/1 pure black; dead
    # 14..239 black
    for ph in range(PHASE_PERIOD):
        fph = sweeping_rainbow_frame(ph)
        for i in range(2, 14):
            assert any(fph[3 * i:3 * i + 3]), (ph, i)
        for i in (0, 1):
            assert _s(fph, i) == (0, 0, 0)
        for dead in range(14, NUM_SLOTS):
            assert _s(fph, dead) == (0, 0, 0)

    # 39 PLASMA-STORM (re-based 2026-11-21 to RING10 + TOPFANS): RING10
    # (slots 2..11) every slot flickers independently dark violet ->
    # bright purple, top fans 12/13 (the subject) a dim violet base
    # (PS_TOP_BASE, >= BEACON_BG floor) that flares on the bursts; each
    # slot's brightness is randomized every 100-300 ms (2..6-frame
    # holds, fixed-seed LCG tables, period 120 | 120, seamless wrap
    # v[-1] == v[0]); once per 120-frame phase (6 s, "occasionally")
    # the FULL CASE flashes: at t = 96..101 every live slot 2..13 is
    # near-white. The old board ring 0..5 wash is dropped — 0/1 pure
    # black. Period 120 | 120; live 2..13 colored every frame; dead
    # 14..239 black.
    assert PS_FLASH_START + PS_FLASH_LEN <= PHASE_PERIOD
    assert len(PS_BRIGHTNESS) == len(RING10)
    assert plasma_storm_frame(0) == plasma_storm_frame(120)  # period
    # the full-case flash fires exactly on t=96..101 per phase
    for ph in range(PHASE_PERIOD):
        fph = plasma_storm_frame(ph)
        flashing = PS_FLASH_START <= ph < PS_FLASH_START + PS_FLASH_LEN
        for i in RING10:
            c = _s(fph, i)
            if flashing:
                assert c == PS_FLASH, (ph, i)
            else:
                assert all(
                    PS_FAN_DARK[ch] <= c[ch] <= PS_FAN_BRIGHT[ch]
                    for ch in range(3)), (ph, i, c)
        c12 = _s(fph, 12)
        assert _s(fph, 13) == c12
        if flashing:
            assert c12 == PS_FLASH, ph
        else:
            assert c12 == PS_TOP_BASE, ph
    # brightness walks: 10 independent 120-entry tables (one per ring
    # slot), each truly random (not static), and every circular run of
    # equal brightness is 2..12 frames (a re-randomization every
    # 100-300 ms; the seam run may merge with the first hold)
    for i in range(len(RING10)):
        v = PS_BRIGHTNESS[i]
        assert len(v) == PHASE_PERIOD
        assert len(set(v)) >= 4                        # randomized, not static
        assert v[-1] == v[0]                           # seamless 120-frame wrap
        runs, cur = [], 1
        for k in range(1, PHASE_PERIOD):
            if v[k] != v[k - 1]:
                runs.append(cur)
                cur = 1
            else:
                cur += 1
        runs.append(cur)
        if v[0] == v[-1]:                              # merge the seam run
            runs[0] += runs.pop()
        for r in runs:
            assert 2 <= r <= 12, (i, runs)
    # the slots flicker INDEPENDENTLY: no two ring slots share a walk
    assert len(set(PS_BRIGHTNESS)) == len(RING10)
    # 12/13: the dim base is the floor (never black), the flash on it
    for ch in range(3):
        assert PS_TOP_BASE[ch] >= BEACON_BG[ch]
    # liveness: live 2..13 colored every frame; 0/1 pure black; dead
    # 14..239 black
    for ph in range(PHASE_PERIOD):
        fph = plasma_storm_frame(ph)
        for i in range(2, 14):
            assert any(fph[3 * i:3 * i + 3]), (ph, i)
        for i in (0, 1):
            assert _s(fph, i) == (0, 0, 0)
        for dead in range(14, NUM_SLOTS):
            assert _s(fph, dead) == (0, 0, 0)

    # ledprobe (mystic-ledprobe): exactly ONE per-LED address lit per step,
    # every other slot pure (0,0,0) (no background); static
    # (phase-independent); index clamped to the zone's slot count; zone
    # defaults to j_rainbow_1 (old index-only fan probe stays intact)
    for idx in range(FAN_LEN):
        fp = ledprobe_frame(0, {"index": idx})
        lit = [i for i in range(NUM_SLOTS)
               if tuple(fp[3 * i:3 * i + 3]) == LEDPROBE_ON]
        assert lit == [FAN_START + idx], (idx, lit)
    assert ledprobe_frame(0, {"index": 0}) == ledprobe_frame(119, {"index": 0})
    assert _s(ledprobe_frame(0, {"index": 0}), 0) == (0, 0, 0)  # no background
    assert _s(ledprobe_frame(0, {"index": 39}), 47) == LEDPROBE_ON
    assert _s(ledprobe_frame(0, {"index": 99}), 47) == LEDPROBE_ON   # clamp hi
    assert _s(ledprobe_frame(0, {"index": -5}), 8) == LEDPROBE_ON    # clamp lo
    assert _s(ledprobe_frame(0, {"index": "bogus"}), 8) == LEDPROBE_ON
    assert _s(ledprobe_frame(0), 8) == LEDPROBE_ON                  # default 0
    # zone-aware: single index lights exactly slot start+index per zone
    for zone, (start, count) in LEDPROBE_ZONES.items():
        for idx in (0, count - 1):
            fp = ledprobe_frame(0, {"zone": zone, "index": idx})
            lit = [i for i in range(NUM_SLOTS)
                   if tuple(fp[3 * i:3 * i + 3]) == LEDPROBE_ON]
            assert lit == [start + idx], (zone, idx, lit)
    # explicit j_rainbow_1 zone == old index-only behavior
    assert ledprobe_frame(0, {"zone": "j_rainbow_1", "index": 3}) == \
        ledprobe_frame(0, {"index": 3})
    # index clamps per zone (j_corsair has 120 slots: 88..207)
    assert _s(ledprobe_frame(0, {"zone": "j_corsair", "index": 0}), 88) == LEDPROBE_ON
    assert _s(ledprobe_frame(0, {"zone": "j_corsair", "index": 119}), 207) == LEDPROBE_ON
    assert _s(ledprobe_frame(0, {"zone": "j_corsair", "index": 999}), 207) == LEDPROBE_ON
    # "all" lights every slot of the zone and nothing else
    for zone, (start, count) in LEDPROBE_ZONES.items():
        fp = ledprobe_frame(0, {"zone": zone, "index": "all"})
        lit = [i for i in range(NUM_SLOTS)
               if tuple(fp[3 * i:3 * i + 3]) == LEDPROBE_ON]
        assert lit == list(range(start, start + count)), (zone, lit)
    # onboard zone = slots 0..5; unknown zone falls back to j_rainbow_1
    assert _s(ledprobe_frame(0, {"zone": "onboard", "index": 5}), 5) == LEDPROBE_ON
    assert _s(ledprobe_frame(0, {"zone": "onboard", "index": 99}), 5) == LEDPROBE_ON
    assert _s(ledprobe_frame(0, {"zone": "nope", "index": 2}), 10) == LEDPROBE_ON

    # override whitelist: every preview name (incl. 6-10, 11-22, 23-24
    # and ledprobe) must be accepted by read_override (a missing name is
    # silently ignored by the daemon — the "invisible beacon" regression)
    import tempfile
    global OVERRIDE_FILE
    for st_name in ("darkblue", "perfan", "chase_cool", "chase_warm",
                    "beacon", "sweep", "tandem", "heartbeat", "comet",
                    "hazard", "off",
                    "bitclock", "telegraph", "fireflies", "lava", "sonar",
                    "metronome", "orbit", "bitstream", "sparkler", "echo",
                    "ecg", "walk", "gpu-spotlight-idle",
                    "gpu-spotlight-fire",
                    "gpu-spotlight-somewhat-active", "red_white",
                    "rainbow", "limelight", "catwalk", "supernova",
                    "inferno", "reactor-core", "matrix", "cyberpunk",
                    "cryogenic", "emergency-mode", "lightning-strike",
                    "sweeping-rainbow", "plasma-storm",
                    "ledprobe"):
        with tempfile.NamedTemporaryFile("w", suffix=".override") as tf:
            tf.write(json.dumps({"state": st_name}))
            tf.flush()
            saved = OVERRIDE_FILE
            try:
                OVERRIDE_FILE = tf.name
                got = read_override()
            finally:
                OVERRIDE_FILE = saved
            assert got[0] == st_name, (st_name, got)
    print("OK: temp model, chase periodicity, beacon options 6-10, "
          "creative options 11-22, gpu-spotlight 23-25, red_white 26, "
          "rainbow 27, limelight 28, catwalk 29, supernova 30, inferno 31, "
          "reactor-core 32, matrix 33, cyberpunk 34, cryogenic 35, "
          "emergency-mode 36, lightning-strike 37, sweeping-rainbow 38, "
          "plasma-storm 39, "
          "frames, and override "
          "whitelist all valid")
    return 0


def main(argv=None):
    argv = list(sys.argv[1:] if argv is None else argv)
    if argv and argv[0] == "--check":
        return cmd_check()
    if not argv:
        sys.stderr.write(
            "usage: gpu_rgb.py serve\n"
            "       gpu_rgb.py preview <darkblue|perfan|chase_cool|chase_warm|beacon|sweep|tandem|heartbeat|comet|hazard|bitclock|telegraph|fireflies|lava|sonar|metronome|orbit|bitstream|sparkler|echo|ecg|walk|gpu-spotlight-idle|gpu-spotlight-fire|gpu-spotlight-somewhat-active|red_white|rainbow|limelight|catwalk|supernova|inferno|reactor-core|matrix|cyberpunk|cryogenic|emergency-mode|lightning-strike|sweeping-rainbow|plasma-storm|ledprobe|off> [SECS]\n"
            "       gpu_rgb.py status\n"
            "       gpu_rgb.py --check\n")
        return 2
    mode = argv[0]
    if mode == "serve":
        return cmd_serve()
    if mode == "preview":
        if len(argv) < 2:
            sys.stderr.write(
                "usage: gpu_rgb.py preview <state> [SECS] [--direct]\n")
            return 2
        rest = [a for a in argv[2:] if a != "--direct"]
        direct = "--direct" in argv[2:]
        secs = float(rest[0]) if rest else 15.0
        return cmd_preview(argv[1], secs, direct=direct)
    if mode == "status":
        return cmd_status()
    sys.stderr.write("unknown mode: %s\n" % mode)
    return 2


if __name__ == "__main__":
    sys.exit(main())
