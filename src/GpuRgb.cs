// gpu-rgb — Windows app for the MSI Aegis ZS2 Mystic Light LEDs.
// Parity with the Linux gpu_rgb.py systemd daemon (ref\gpu_rgb.py = source of
// truth) + extensions. Phase 1: open the MSI MYSTIC LIGHT HID device,
// capture the 0x52 zone state, enter per-LED sync mode (185B 0x52 enable),
// render frames (725B 0x53 @ 20 FPS), clean exit (two-report write of the
// captured 0x52 state).
//
// Subcommands (Phase 1): probe, hold, restore, selftest, --check
// Subcommands (Phase 2): preview <name|N> [secs], cycle [N], off [secs]
//
// HID transport: hid.dll HidD_GetFeature/HidD_SetFeature (canonical user-mode
// feature-report API, same as OpenRGB/hidapi). The raw HID_IO_CONTROL IOCTL
// form from gpu-rgb.ps1 fails HOST-WIDE on this Windows build (Win32 error 1
// on every HID device incl. the live internal keyboard) while HidD_* works —
// proven 2026-11-20, see evidence\pathdiag-2026-11-20.txt. Packet bytes are
// unchanged (byte-identical to ref\gpu_rgb.py).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Globalization;
using System.Web.Script.Serialization;

namespace GpuRgb
{
    // -------------------------------------------------------------------
    // Protocol constants — byte-exact (ref\hid-protocol.md, ref\gpu_rgb.py)
    // -------------------------------------------------------------------
    static class Proto
    {
        public const int Fps = 20;
        public const int Pkt = 185;            // report 0x52 zone config
        public const int PerLedPkt = 725;      // report 0x53 per-LED frame
        public const byte ReportId = 0x52;
        public const byte PerLedReportId = 0x53;
        public const int NumSlots = 240;
        public const int PhasePeriod = 120;    // 6 s at 20 FPS

        // 0x52 zone offsets
        public const int JRgb1 = 1, JPipe1 = 11, JPipe2 = 21, JRainbow1 = 31,
                         JRainbow2 = 42, JCorsair = 53, JCorsairOuter = 64,
                         OnBoard = 74, OnBoard1 = 84, JRgb2 = 174, SaveOff = 184;

        public const byte EffectStatic = 0x01;
        public const byte EffectDirect = 0x25;
        public const byte FullSync = (byte)(0x80 | 0x01 | 0x02 | 0x04 | 0x08); // 0x8F

        public const uint IoctlSetFeature = 0x00220008;
        public const uint IoctlGetFeature = 0x0022000C;
        public static readonly Guid HidClassGuid =
            new Guid("4d1e55b2-f16f-11cf-88cb-001111000030");

        // Live map (ref\msi-led-slot-map.md, probe walk 2026-11-16; o'clock re-probe 2026-11-21):
        // 0/1 live-unassigned (never painted), 2..11 = RING10 (10 fan-ring LEDs),
        // 12/13 = top case fans. 14..239 are DEAD — never painted.

        public static string[] ZoneNames =
        {
            "j_rgb_1", "j_pipe_1", "j_pipe_2", "j_rainbow_1", "j_rainbow_2",
            "j_corsair", "j_corsair_outer", "on_board_led_0", "on_board_led_1",
            "on_board_led_2", "on_board_led_3", "on_board_led_4", "on_board_led_5",
            "j_rgb_2"
        };
        public static int[] ZoneOffsets =
        {
            JRgb1, JPipe1, JPipe2, JRainbow1, JRainbow2, JCorsair, JCorsairOuter,
            OnBoard, OnBoard + 10, OnBoard + 20, OnBoard + 30, OnBoard + 40,
            OnBoard + 50, OnBoard + 60, OnBoard + 70, OnBoard + 80, OnBoard + 90,
            JRgb2
        };
    }

    // -------------------------------------------------------------------
    // Packet builders (ported byte-for-byte from ref\gpu_rgb.py)
    // -------------------------------------------------------------------
    static class Packets
    {
        static void SetZone(byte[] m, int off, byte? flags, byte? cf)
        {
            if (flags.HasValue) m[off + 4] = flags.Value;
            if (cf.HasValue) m[off + 8] = cf.Value;
        }

        // The 185B 0x52 per-LED sync ENABLE message: captured state + template
        // overrides (build_enable_msg). One write enters per-LED sync mode.
        public static byte[] BuildEnable(byte[] captured)
        {
            byte[] m = new byte[Proto.Pkt];
            if (captured != null && captured.Length == Proto.Pkt)
                Buffer.BlockCopy(captured, 0, m, 0, Proto.Pkt);
            SetZone(m, Proto.JRgb1, 0x08, 0x80);
            SetZone(m, Proto.JPipe1, 0x2A, 0x80);
            SetZone(m, Proto.JPipe2, 0x2A, 0x80);
            SetZone(m, Proto.JRainbow1, 0x29, 0x80);
            m[Proto.JRainbow1 + 10] = 0x28;
            SetZone(m, Proto.JRainbow2, 0x29, 0x80);
            m[Proto.JRainbow2 + 10] = 0x28;
            m[Proto.JCorsair + 4] = 0x29;
            m[Proto.JCorsair + 5] = 0x00;
            m[Proto.JCorsair + 8] = 0x82;
            m[Proto.JCorsair + 10] = 0x78;
            SetZone(m, Proto.JCorsairOuter, 0x28, 0x80);
            m[Proto.OnBoard + 0] = Proto.EffectDirect;
            m[Proto.OnBoard + 4] = 0xA9;
            m[Proto.OnBoard + 8] = Proto.FullSync;
            for (int off = Proto.OnBoard1; off < 174; off += 10)
                SetZone(m, off, 0x28, 0x80);
            SetZone(m, Proto.JRgb2, 0x2A, 0x80);
            // sync-mode overrides
            m[Proto.JRainbow1 + 0] = Proto.EffectStatic;
            m[Proto.JRainbow1 + 10] = 40;
            m[Proto.JRainbow2 + 0] = Proto.EffectStatic;
            m[Proto.JRainbow2 + 10] = 40;
            m[Proto.JCorsair + 0] = Proto.EffectStatic;
            m[Proto.JCorsair + 10] = 120;
            m[Proto.OnBoard + 8] = Proto.FullSync;
            return m;
        }

        // The 725B 0x53 per-LED frame: [0]=0x53, [1..4]=25 06 00 00, then 240
        // RGB slots — slot i = bytes 5+3i .. 5+3i+2:
        // [0..5] onboard, [6] j_rgb_1, [7] j_rgb_2, [8..47] j_rainbow_1,
        // [48..87] j_rainbow_2, [88..207] j_corsair.
        public static byte[] BuildPerLed(byte[] frame720)
        {
            if (frame720 == null || frame720.Length != Proto.NumSlots * 3)
                throw new ArgumentException("frame must be 720 bytes");
            byte[] p = new byte[Proto.PerLedPkt];
            p[0] = Proto.PerLedReportId;
            p[1] = 0x25; p[2] = 0x06; p[3] = 0x00; p[4] = 0x00;
            Buffer.BlockCopy(frame720, 0, p, 5, 720);
            return p;
        }

        // Pure-black 720-byte frame (all 240 slots 0,0,0).
        public static byte[] BlackFrame()
        {
            return new byte[Proto.NumSlots * 3];
        }

        // SCREEN-ZONES (2026-11-23): 10-zone screen-mode frame. z[0..9] =
        // the 10 zone colors in ScreenZones.Hours order
        // [12,1,2,4,5,6,7,8,10,11]; z[10] = the overall screen average.
        // Slots 2..11 = the zone colors (o'clock map), 12/13 = overall,
        // slots 0/1 and 14..239 = pure black.
        public static byte[] ScreenZoneFrame(int[][] z)
        {
            byte[] f = new byte[Proto.NumSlots * 3];
            for (int i = 0; i < 10; i++)
            {
                int s = ScreenZones.Slots[i];
                f[3 * s] = (byte)z[i][0];
                f[3 * s + 1] = (byte)z[i][1];
                f[3 * s + 2] = (byte)z[i][2];
            }
            int[] ov = z[10];
            for (int s = 12; s <= 13; s++)
            {
                f[3 * s] = (byte)ov[0];
                f[3 * s + 1] = (byte)ov[1];
                f[3 * s + 2] = (byte)ov[2];
            }
            return f;
        }

        // 185B zone-static state at rgb over the captured base (exits
        // per-LED mode). Ported from build_static_state.
        public static byte[] BuildStatic(byte[] captured, byte[] rgb)
        {
            byte[] b = new byte[Proto.Pkt];
            if (captured != null && captured.Length == Proto.Pkt)
                Buffer.BlockCopy(captured, 0, b, 0, Proto.Pkt);
            b[0] = Proto.ReportId;
            foreach (int o in Proto.ZoneOffsets)
            {
                b[o + 0] = Proto.EffectStatic;
                b[o + 1] = rgb[0]; b[o + 2] = rgb[1]; b[o + 3] = rgb[2];
                b[o + 4] = 0x28;
                b[o + 5] = rgb[0]; b[o + 6] = rgb[1]; b[o + 7] = rgb[2];
                b[o + 8] = (o == Proto.OnBoard) ? (byte)0x81 : (byte)0x80;
                b[o + 9] = 0x00;
            }
            b[Proto.SaveOff] = 0;
            return b;
        }
    }

    // -------------------------------------------------------------------
    // Screen-zone math (SCREEN-ZONES batch, 2026-11-23): the monitor is
    // divided into 10 angular zones (a clock face with the 3 and 9
    // o'clock positions skipped); each zone's average screen color drives
    // the fan-ring LED at that o'clock position. PURE math only (no GDI)
    // so --check can test it with synthetic grids.
    //
    // Zone order (index -> hour): [12, 1, 2, 4, 5, 6, 7, 8, 10, 11];
    // index 10 = overall average. Packet slot i (720B frame: bytes 3i..3i+2,
    // wrapped by BuildPerLed into the 725B 0x53 packet at bytes 5+3i..5+3i+2):
    // 12->10, 1->11, 2->2, 4->3, 5->4, 6->5, 7->6, 8->7, 10->8, 11->9.
    // Grid: 64x36 (16:9) DIB, top-down. Cell angle = degrees CLOCKWISE from
    // straight up: atan2(dx, -dy), dx = x - W/2, dy = y - H/2 (y measured
    // downward from the top).
    // -------------------------------------------------------------------
    static class ScreenZones
    {
        public const int GridW = 64;
        public const int GridH = 36;
        // Zone index -> o'clock hour, in the order GridToZones returns.
        public static readonly int[] Hours = new int[] { 12, 1, 2, 4, 5, 6, 7, 8, 10, 11 };
        // Zone index -> packet slot in the 725B 0x53 frame.
        public static readonly int[] Slots = new int[] { 10, 11, 2, 3, 4, 5, 6, 7, 8, 9 };

        // Angle (deg, CW from straight up; normalized to [0,360)) ->
        // zone index. Half-open spans [lo,hi): 345..15 -> 12, 15..45 -> 1,
        // 45..90 -> 2, 90..135 -> 4, 135..165 -> 5, 165..195 -> 6,
        // 195..225 -> 7, 225..285 -> 8, 285..315 -> 10, 315..345 -> 11.
        public static int ZoneForAngle(double angleDeg)
        {
            double a = angleDeg % 360.0;
            if (a < 0) a += 360.0;
            if (a < 15.0) return 0;  // 12
            if (a < 45.0) return 1;  // 1
            if (a < 90.0) return 2;  // 2
            if (a < 135.0) return 3; // 4
            if (a < 165.0) return 4; // 5
            if (a < 195.0) return 5; // 6
            if (a < 225.0) return 6; // 7
            if (a < 285.0) return 7; // 8
            if (a < 315.0) return 8; // 10
            if (a < 345.0) return 9; // 11
            return 0;                // 12
        }

        // Grid cell (x right, y down, top-down origin) -> zone index.
        public static int ZoneForCell(int cx, int cy, int W, int H)
        {
            double dx = cx - W / 2;
            double dy = cy - H / 2;
            double angle = Math.Atan2(dx, -dy) * 180.0 / Math.PI;
            return ZoneForAngle(angle);
        }

        // pixels: W*H*4 bytes, DIB order (BGRA per pixel, rows top-down).
        // Returns 11 RGB vectors: [0..9] = the average color of the cells
        // assigned to each zone (zone index 0..9 = Hours order), [10] =
        // the overall average over ALL grid cells (not a mix of zone
        // colors).
        public static int[][] GridToZones(byte[] pixels, int W, int H)
        {
            long[] sum = new long[11 * 3];
            int[] cnt = new int[11];
            long ar = 0, ag = 0, ab = 0;
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    int o = (y * W + x) * 4;
                    int b = pixels[o];
                    int g = pixels[o + 1];
                    int r = pixels[o + 2];
                    int z = ZoneForCell(x, y, W, H);
                    sum[z * 3] += r;
                    sum[z * 3 + 1] += g;
                    sum[z * 3 + 2] += b;
                    cnt[z]++;
                    ar += r; ag += g; ab += b;
                }
            }
            int[][] zones = new int[11][];
            for (int i = 0; i < 11; i++)
            {
                zones[i] = new int[3];
                if (i < 10 && cnt[i] > 0)
                {
                    zones[i][0] = (int)(sum[i * 3] / cnt[i]);
                    zones[i][1] = (int)(sum[i * 3 + 1] / cnt[i]);
                    zones[i][2] = (int)(sum[i * 3 + 2] / cnt[i]);
                }
            }
            zones[10] = new int[]
            {
                (int)(ar / (W * H)), (int)(ag / (W * H)), (int)(ab / (W * H))
            };
            return zones;
        }
    }

    // -------------------------------------------------------------------
    // Drive-model math (ported from ref\gpu_rgb.py; used by Phases 3-4,
    // asserted by --check now). Level = max(temp level, power level).
    // -------------------------------------------------------------------
    static class Model
    {
        public const double TempAMax = 55, TempBMax = 60, TempCMax = 65, TempDMax = 75;
        public const double PwrBLow = 15.0, PwrBHigh = 25.0;
        public const double PwrCLow = 290.0, PwrCHigh = 310.0;

        // Drive-model level -> pattern (ref\gpu_rgb.py LEVEL_STATE_NAMES).
        public static readonly string[] LevelNames =
        {
            "gpu-spotlight-idle", "gpu-spotlight-somewhat-active", "sweep", "red_white"
        };
        public static double Level(double temp, double power)
        {
            return Math.Max(LevelFromTemp(temp), LevelFromPower(power));
        }
        // state_label: first index of the max weight (ties -> lower level).
        public static string Label(double[] w)
        {
            int idx = 0;
            for (int k = 1; k < 4; k++) if (w[k] > w[idx]) idx = k;
            return LevelNames[idx];
        }
        // state_weights at an explicit float level (drive-frame checks).
        public static double[] WeightsFromLevel(double level)
        {
            return new double[]
            {
                Math.Max(0.0, 1.0 - level),
                Math.Max(0.0, 1.0 - Math.Abs(level - 1.0)),
                Math.Max(0.0, 1.0 - Math.Abs(level - 2.0)),
                Math.Max(0.0, 1.0 - Math.Abs(level - 3.0)),
            };
        }

        public static double Smoothstep(double x)
        {
            x = Math.Max(0.0, Math.Min(1.0, x));
            return x * x * (3.0 - 2.0 * x);
        }

        public static double LevelFromTemp(double temp)
        {
            if (temp <= TempAMax) return 0.0;
            if (temp < TempBMax) return Smoothstep((temp - TempAMax) / (TempBMax - TempAMax));
            if (temp < TempCMax) return 1.0 + Smoothstep((temp - TempBMax) / (TempCMax - TempBMax));
            if (temp < TempDMax) return 2.0 + Smoothstep((temp - TempCMax) / (TempDMax - TempCMax));
            return 3.0;
        }

        public static double LevelFromPower(double pwr)
        {
            if (pwr <= PwrBLow) return 0.0;
            if (pwr < PwrBHigh) return Smoothstep((pwr - PwrBLow) / (PwrBHigh - PwrBLow));
            if (pwr <= PwrCLow) return 1.0;
            if (pwr < PwrCHigh) return 1.0 + Smoothstep((pwr - PwrCLow) / (PwrCHigh - PwrCLow));
            return 2.0;
        }

        // Blended weights (w0..w3) for a float temp and power (W).
        // OR semantics: the higher of the two drive levels wins.
        public static double[] Weights(double temp, double power)
        {
            double level = Math.Max(LevelFromTemp(temp), LevelFromPower(power));
            return new double[]
            {
                Math.Max(0.0, 1.0 - level),
                Math.Max(0.0, 1.0 - Math.Abs(level - 1.0)),
                Math.Max(0.0, 1.0 - Math.Abs(level - 2.0)),
                Math.Max(0.0, 1.0 - Math.Abs(level - 3.0)),
            };
        }
    }

    // -------------------------------------------------------------------
    // GPU sensors (nvidia-smi) + EMA — ref\gpu_rgb.py read_sensors and the
    // cmd_serve smoothing: poll ~5 s, alpha 0.25, first sample seeds,
    // failed polls keep the last value.
    // -------------------------------------------------------------------
    static class Sensors
    {
        public static bool Read(out double temp, out double power)
        {
            temp = 0.0; power = 0.0;
            try
            {
                Process p = new Process();
                p.StartInfo.FileName = "nvidia-smi";
                p.StartInfo.Arguments =
                    "--query-gpu=temperature.gpu,power.draw --format=csv,noheader";
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.CreateNoWindow = true;
                p.Start();
                string output = p.StandardOutput.ReadToEnd();
                if (!p.WaitForExit(10000))
                {
                    try { p.Kill(); } catch { }
                    return false;
                }
                if (p.ExitCode != 0) return false;
                string[] lines = output.Trim().Split('\n');
                if (lines.Length == 0) return false;
                string[] fields = lines[0].Trim().Split(',');
                if (fields.Length < 2) return false;
                temp = double.Parse(fields[0].Trim().Split(' ')[0],
                    CultureInfo.InvariantCulture);
                power = double.Parse(fields[1].Trim().Split(' ')[0],
                    CultureInfo.InvariantCulture);
                return true;
            }
            catch { return false; }
        }
    }

    class Smoothed
    {
        public double Temp, Power;
        public bool HasTemp, HasPower;
        public void Add(double t, double p)
        {
            Temp = HasTemp ? Temp + 0.25 * (t - Temp) : t; HasTemp = true;
            Power = HasPower ? Power + 0.25 * (p - Power) : p; HasPower = true;
        }
        // cmd_serve fallbacks before the first successful poll.
        public double TempOrFallback { get { return HasTemp ? Temp : Model.TempAMax; } }
        public double PowerOrFallback { get { return HasPower ? Power : 0.0; } }
    }

    // -------------------------------------------------------------------
    // Config (Phase 5): config.json in the project dir — {"mode":"gpu",
    // "levels":{"0":name,...,"3":name}}. Hot-reloaded by serve (~1 s).
    // Unknown names/modes fail LOUDLY (caller keeps old config + logs).
    // -------------------------------------------------------------------
    class Config
    {
        public string Mode = "gpu";
        public string[] Levels = new string[]
        {
            Model.LevelNames[0], Model.LevelNames[1], Model.LevelNames[2], Model.LevelNames[3]
        };
        // SCREEN-ZONES (2026-11-23): responsive flag. true (default when
        // the key is absent) = today's gentle fade (5 Hz capture, lerp
        // alpha 0.1 per 20 FPS frame); false = gaming responsive (10 Hz
        // capture, snap). Only affects the screen branch.
        public bool ScreenSmoothing = true;
        public static string CfgPath { get { return Path.Combine(App.Base, "config.json"); } }

        public static Config Load()
        {
            Config c = new Config();
            if (!File.Exists(CfgPath)) return c;
            Dictionary<string, object> d;
            try
            {
                d = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(
                    File.ReadAllText(CfgPath));
            }
            catch (Exception ex) { throw new Exception("config.json parse error: " + ex.Message); }
            if (d == null) throw new Exception("config.json is empty");
            return Parse(d);
        }

        // Pure parse of the deserialized JSON object -> Config (fail
        // LOUDLY like the other keys). Split out of Load so --check can
        // test it with synthetic dicts (SCREEN-ZONES batch).
        public static Config Parse(Dictionary<string, object> d)
        {
            Config c = new Config();
            object modeObj;
            if (d.TryGetValue("mode", out modeObj))
            {
                string m = Convert.ToString(modeObj);
                if (m != "gpu" && m != "screen")
                    throw new Exception("config.json: unknown mode '" + m + "' (gpu|screen)");
                c.Mode = m;
            }
            object lvObj;
            if (d.TryGetValue("levels", out lvObj))
            {
                Dictionary<string, object> lv = lvObj as Dictionary<string, object>;
                if (lv == null) throw new Exception("config.json: levels must be an object");
                for (int i = 0; i < 4; i++)
                {
                    object v;
                    if (!lv.TryGetValue(i.ToString(), out v))
                        throw new Exception("config.json: levels missing key '" + i + "'");
                    c.Levels[i] = Patterns.Resolve(Convert.ToString(v)).Name;
                }
            }
            object ssObj;
            if (d.TryGetValue("screen_smoothing", out ssObj))
            {
                bool v;
                if (ssObj is bool) v = (bool)ssObj;
                else throw new Exception("config.json: screen_smoothing must be a JSON bool");
                c.ScreenSmoothing = v;
            }
            return c;
        }

        // SCREEN-ZONES (2026-11-23): responsive flag -> (capture period ms,
        // lerp alpha per 20 FPS frame). true -> (200, 0.1); false ->
        // (100, 1.0). 10 Hz capture is the floor; the render loop stays
        // 20 FPS.
        public static void ScreenParams(bool smoothing, out int periodMs, out double alpha)
        {
            if (smoothing) { periodMs = 200; alpha = 0.1; }
            else { periodMs = 100; alpha = 1.0; }
        }

        public void Save()
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["mode"] = Mode;
            Dictionary<string, object> lv = new Dictionary<string, object>();
            for (int i = 0; i < 4; i++) lv[i.ToString()] = Levels[i];
            d["levels"] = lv;
            d["screen_smoothing"] = ScreenSmoothing;
            File.WriteAllText(CfgPath, new JavaScriptSerializer().Serialize(d));
        }

        public Func<int, byte[]>[] LevelFns()
        {
            Func<int, byte[]>[] fns = new Func<int, byte[]>[4];
            for (int i = 0; i < 4; i++) fns[i] = Patterns.Resolve(Levels[i]).Frame;
            return fns;
        }
    }

    // -------------------------------------------------------------------
    // Screen color (Phase 6 + SCREEN-ZONES 2026-11-23): GDI BitBlt of the
    // primary monitor, StretchBlt downscale to a 64x36 (16:9) 32-bit DIB,
    // per-zone averages (10 zones + overall) via the pure ScreenZones
    // math. Own thread: period config-driven via screen_smoothing
    // (200 ms smoothing / 100 ms responsive); the render side lerps each
    // of the 11 color vectors toward the newest capture at 20 FPS. CPU
    // cost of the capture thread is measured via GetThreadTimes.
    // -------------------------------------------------------------------
    class ScreenCapture
    {
        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("user32.dll")] public static extern int GetSystemMetrics(int nIndex);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr h);
        [DllImport("gdi32.dll")] public static extern bool StretchBlt(IntPtr hdcDest, int xD, int yD,
            int wD, int hD, IntPtr hdcSrc, int xS, int yS, int wS, int hS, uint rop);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO bmi,
            uint usage, out IntPtr bits, IntPtr section, uint offset);
        [DllImport("kernel32.dll")] public static extern bool GetThreadTimes(IntPtr h, out long create,
            out long exit, out long kernel, out long user);
        [DllImport("kernel32.dll")] public static extern IntPtr GetCurrentThread();

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER
        {
            public uint biSize; public int biWidth; public int biHeight;
            public ushort biPlanes; public ushort biBitCount; public uint biCompression;
            public uint biSizeImage; public int biXPelsPerMeter; public int biYPelsPerMeter;
            public uint biClrUsed; public uint biClrImportant;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFO { public BITMAPINFOHEADER bmiHeader; public uint bmiColors; }

        const uint SRCCOPY = 0x00CC0020;
        const uint CAPTUREBLT = 0x40000000;
        const uint DIB_RGB_COLORS = 0;

        Thread _thread;
        volatile bool _stop;
        readonly object _lock = new object();
        int[][] _zones; // 11 x 3, ScreenZones order (last = overall)
        bool _has;
        double _cpuPct;
        long _lastOkMs;
        readonly Stopwatch _life = Stopwatch.StartNew();
        // SCREEN-ZONES: source of the screen_smoothing flag, read each
        // capture loop iteration (null = true = smoothing on). The serve
        // loop wires it to the live Config so hot-reloaded changes apply
        // within one capture period (~100-200 ms) without a restart.
        public Func<bool> SmoothingSource;

        public bool TryGet(out int[][] zones)
        {
            lock (_lock) { zones = _zones; return _has; }
        }
        public double CpuPct { get { lock (_lock) { return _cpuPct; } } }
        // Stalled = had samples but none for >3 s (e.g. workstation locked:
        // StretchBlt from the screen DC is denied with Win32 error 5).
        public bool Stalled
        {
            get { lock (_lock) { return _has && _life.ElapsedMilliseconds - _lastOkMs > 3000; } }
        }
        public bool HasSample { get { lock (_lock) { return _has; } } }

        public void Start()
        {
            _thread = new Thread(Run);
            _thread.IsBackground = true;
            _thread.Start();
        }
        public void Stop()
        {
            _stop = true;
            if (_thread != null) _thread.Join(2000);
        }

        void Run()
        {
            IntPtr hThread = GetCurrentThread();
            long prevK, prevU, cc, ee;
            GetThreadTimes(hThread, out cc, out ee, out prevK, out prevU);
            long prevWall = 0;
            Stopwatch wall = Stopwatch.StartNew();
            while (!_stop)
            {
                // Read the period each iteration (config-driven):
                // screen_smoothing true -> 200 ms, false -> 100 ms.
                int period = 200;
                double alpha;
                bool smooth = SmoothingSource == null ? true : SmoothingSource();
                Config.ScreenParams(smooth, out period, out alpha);
                Stopwatch busy = Stopwatch.StartNew();
                int[][] zones;
                if (CaptureOnce(out zones))
                    lock (_lock)
                    {
                        _zones = zones; _has = true;
                        _lastOkMs = _life.ElapsedMilliseconds;
                    }
                busy.Stop();
                long k, u, c, e;
                if (GetThreadTimes(hThread, out c, out e, out k, out u))
                {
                    long dw = wall.ElapsedMilliseconds - prevWall;
                    if (dw >= 1000)
                    {
                        double cpu = ((k - prevK) + (u - prevU)) / 10000.0 / dw * 100.0;
                        lock (_lock) { _cpuPct = cpu; }
                        prevK = k; prevU = u; prevWall = wall.ElapsedMilliseconds;
                    }
                }
                int sleep = period - (int)busy.ElapsedMilliseconds;
                if (sleep > 0) Thread.Sleep(sleep);
            }
        }

        public static bool CaptureOnce(out int[][] zones)
        {
            zones = null;
            int w = GetSystemMetrics(0), h = GetSystemMetrics(1);
            if (w <= 0 || h <= 0) return false;
            IntPtr screen = GetDC(IntPtr.Zero);
            if (screen == IntPtr.Zero) return false;
            IntPtr mem = CreateCompatibleDC(screen);
            BITMAPINFO bmi = new BITMAPINFO();
            bmi.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
            bmi.bmiHeader.biWidth = ScreenZones.GridW;
            bmi.bmiHeader.biHeight = -ScreenZones.GridH; // top-down
            bmi.bmiHeader.biPlanes = 1;
            bmi.bmiHeader.biBitCount = 32;
            bmi.bmiHeader.biCompression = 0;
            IntPtr bits;
            IntPtr dib = CreateDIBSection(screen, ref bmi, DIB_RGB_COLORS, out bits, IntPtr.Zero, 0);
            if (dib == IntPtr.Zero) { DeleteDC(mem); ReleaseDC(IntPtr.Zero, screen); return false; }
            IntPtr old = SelectObject(mem, dib);
            bool ok = StretchBlt(mem, 0, 0, ScreenZones.GridW, ScreenZones.GridH,
                screen, 0, 0, w, h, SRCCOPY | CAPTUREBLT);
            if (ok)
            {
                byte[] px = new byte[ScreenZones.GridW * ScreenZones.GridH * 4];
                Marshal.Copy(bits, px, 0, px.Length);
                zones = ScreenZones.GridToZones(px, ScreenZones.GridW, ScreenZones.GridH);
            }
            SelectObject(mem, old);
            DeleteObject(dib);
            DeleteDC(mem);
            ReleaseDC(IntPtr.Zero, screen);
            return ok;
        }
    }

    // -------------------------------------------------------------------
    // HID I/O — hid.dll feature reports (raw IOCTL form broken host-wide)
    // -------------------------------------------------------------------
    static class Hid
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr CreateFileW(string lpFileName, uint dwDesiredAccess,
            uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition,
            uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeviceIoControl(IntPtr hDevice, uint dwIoControlCode,
            IntPtr lpInBuffer, uint nInBufferSize, IntPtr lpOutBuffer, uint nOutBufferSize,
            out uint lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("hid.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool HidD_GetFeature(IntPtr HidDeviceObject, byte[] lpReportBuffer, uint ReportBufferLength);

        [DllImport("hid.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool HidD_SetFeature(IntPtr HidDeviceObject, byte[] lpReportBuffer, uint ReportBufferLength);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern uint CM_Get_Device_Interface_List_SizeW(out uint pulLen,
            Guid InterfaceClassGuid, string pDeviceID, uint ulFlags);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern uint CM_Get_Device_Interface_ListW(Guid InterfaceClassGuid,
            string pDeviceID, StringBuilder buffer, uint bufferLen, uint ulFlags);

        // All HID interface paths on the machine (null-terminated list in
        // one buffer; entries separated by \0).
        public static List<string> HidInterfaceList()
        {
            List<string> results = new List<string>();
            uint len;
            uint rc = CM_Get_Device_Interface_List_SizeW(out len, Proto.HidClassGuid, null, 0);
            if (rc != 0 || len == 0) return results;
            var sb = new StringBuilder((int)len);
            rc = CM_Get_Device_Interface_ListW(Proto.HidClassGuid, null, sb, len, 0);
            if (rc != 0) return results;
            foreach (string p in sb.ToString().Split('\0'))
                if (p.Length > 0) results.Add(p);
            return results;
        }

        // Enumerate the PnP registry (Enum\HID) for the MSI VID/PID and
        // build candidate interface paths. Needed because on this machine
        // CM_Get_Device_Interface_List does NOT enumerate the MSI device,
        // but the CM-style path built from the registry still opens.
        public static List<string> RegistryCandidates()
        {
            List<string> results = new List<string>();
            try
            {
                Microsoft.Win32.RegistryKey hid = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    "SYSTEM\\CurrentControlSet\\Enum\\HID");
                if (hid == null) return results;
                foreach (string key in hid.GetSubKeyNames())
                {
                    // key = VID_0DB0&PID_0076, possibly with &MI_xx / &REV suffix
                    if (key.IndexOf("VID_0DB0&PID_0076", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    Microsoft.Win32.RegistryKey k = hid.OpenSubKey(key);
                    if (k == null) continue;
                    foreach (string inst in k.GetSubKeyNames())
                        results.Add("HID#" + key + "#" + inst);
                    k.Close();
                }
                hid.Close();
            }
            catch (Exception ex)
            {
                App.Warn("registry Enum\\HID scan failed: " + ex.Message);
            }
            return results;
        }

        public static string Sample(List<string> list, int n)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < list.Count && i < n; i++)
            {
                if (i > 0) sb.Append("; ");
                sb.Append(list[i]);
            }
            if (list.Count == 0) return "(none)";
            if (list.Count > n) sb.Append(" ...");
            return sb.ToString();
        }

        // Find + open the MSI MYSTIC LIGHT device (VID 0x0DB0, PID 0x0076).
        // Returns the handle; sets devicePath. Throws with a diagnosis.
        public static IntPtr Open(out string devicePath)
        {
            // (1) interface paths: \\?\HID#...#VID0DB0&PID0076#...#{HID GUID}
            //     (a bare \\.\HID\<instance> opens with Win32 error 3 — wrong form)
            List<string> paths = new List<string>();
            List<string> msifallback = new List<string>();
            foreach (string p in HidInterfaceList())
            {
                string up = p.ToUpperInvariant();
                // instance IDs carry the hardware ID as VIDxxxxx&PIDxxxxx
                if (up.IndexOf("VID0DB0&PID0076", StringComparison.Ordinal) >= 0)
                    if (!paths.Contains(p)) paths.Add(p);
                else if (up.IndexOf("MSI", StringComparison.Ordinal) >= 0)
                    if (!msifallback.Contains(p)) msifallback.Add(p);
            }
            // (2) PnP-registry candidates: \\?\HID#<key>#<instance>#{HID GUID}
            //     (CM enumeration misses this device on this machine, but the
            //     registry-constructed path opens)
            foreach (string instId in RegistryCandidates())
            {
                string path = @"\\?\" + instId + "#{" + Proto.HidClassGuid + "}";
                if (!paths.Contains(path)) paths.Add(path);
            }
            // (3) legacy WMI-style paths \\.\HID#instance (last resort)
            foreach (string p in msifallback)
            {
                int e = p.LastIndexOf('#');
                if (e > 4)
                {
                    string legacy = @"\\.\" + p.Substring(4, e - 4); // \\.\HID#instance
                    if (!paths.Contains(legacy)) paths.Add(legacy);
                }
            }
            if (paths.Count == 0)
            {
                List<string> hidAll = HidInterfaceList();
                throw new Exception(string.Format(
                    "no MSI MYSTIC LIGHT HID device found (looking for VID0DB0&PID0076; scanned " +
                    "{0} CM HID interfaces, {1} PnP registry candidates). Is the device present " +
                    "in Device Manager? Sample CM paths: {2}",
                    hidAll.Count, RegistryCandidates().Count, Sample(hidAll, 5)));
            }

            string lastErr = "no openable path";
            foreach (string path in paths)
            {
                // GENERIC_READ|GENERIC_WRITE = 0xC0000000 (the hidclass HID
                // IOCTLs are METHOD_BUFFERED and require both bits);
                // share R|W|DELETE = 7; OPEN_EXISTING = 3
                IntPtr h = CreateFileW(path, 0xC0000000u, 7u, IntPtr.Zero, 3u, 0u, IntPtr.Zero);
                if (h.ToInt64() == -1)
                {
                    int err = Marshal.GetLastWin32Error();
                    lastErr = "open " + path + " -> Win32 error " + err;
                    if (err == 5)
                        throw new Exception("another writer holds the MSI Mystic Light device " +
                            "(Win32 error 5 on open). Close MSI Center / Mystic Light (or the old " +
                            "gpu-rgb.ps1 / OpenRGB) and try again.");
                    continue;
                }
                byte[] tmpl = new byte[Proto.Pkt];
                tmpl[0] = Proto.ReportId;
                try
                {
                    byte[] st = GetFeature(h, tmpl);
                    if (st.Length != Proto.Pkt)
                        throw new Exception("GET 0x52 returned " + st.Length + " bytes");
                    devicePath = path;
                    return h;
                }
                catch (Exception ex)
                {
                    lastErr = "GET 0x52 on " + path + " failed: " + ex.Message;
                    CloseHandle(h);
                }
            }
            throw new Exception("could not open the MSI Mystic Light HID device. Last: " + lastErr +
                " (Win32 error 3 = wrong path form — use the CM interface list; error 5 = another " +
                "writer such as MSI Center)");
        }

        // Reports are FEATURE reports via DeviceIoControl. HID_IO_CONTROL
        // (x64, 44-byte header): [0..27] = "HidIOControl" + NUL as UTF-16,
        // [28..31] = io control code, [32..35] = output buffer length,
        // [36..43] = pointer to the report data (pinned at offset 44 of one
        // buffer; the whole pinned block is the in-buffer).
        public static void SetFeature(IntPtr h, byte[] report)
        {
            // report[0] = report id; length = full report (185B 0x52 / 725B 0x53).
            if (!HidD_SetFeature(h, report, (uint)report.Length))
                throw new Exception("HidD_SetFeature failed (Win32 error " +
                    Marshal.GetLastWin32Error() + ", report 0x" + report[0].ToString("X2") +
                    ", " + report.Length + "B)");
        }

        public static byte[] GetFeature(IntPtr h, byte[] template)
        {
            // template[0] = report id; HidD_GetFeature fills the whole buffer
            // (returned data includes the report id at [0]).
            if (!HidD_GetFeature(h, template, (uint)template.Length))
                throw new Exception("HidD_GetFeature failed (Win32 error " +
                    Marshal.GetLastWin32Error() + ")");
            return template;
        }
    }

    // -------------------------------------------------------------------
    // One device session: capture 0x52 at open; enter per-LED mode; clean
    // exit = two-report write of the captured state (save_data=0, never
    // persists, never flashes).
    // -------------------------------------------------------------------
    class Session
    {
        public IntPtr H = IntPtr.Zero;
        public string Path;
        public byte[] Captured;
        public byte[] Enable;

        static Mutex _singleWriter;
        public static Session Start(string what)
        {
            bool created;
            _singleWriter = new Mutex(true, "GpuRgbSingleWriter", out created);
            if (!created)
            {
                try { created = _singleWriter.WaitOne(0); }
                catch (AbandonedMutexException) { created = true; }
                if (!created)
                    throw new Exception("another gpu-rgb instance holds the device " +
                        "(single-writer mutex) - close it first");
            }
            Session s = new Session();
            string p;
            s.H = Hid.Open(out p);
            s.Path = p;
            byte[] tmpl = new byte[Proto.Pkt];
            tmpl[0] = Proto.ReportId;
            s.Captured = Hid.GetFeature(s.H, tmpl);
            s.Enable = Packets.BuildEnable(s.Captured);
            App.Log(what + ": opened " + p);
            return s;
        }

        public void EnterPerLed()
        {
            Hid.SetFeature(H, Enable);
            App.Log("per-LED sync mode enabled (j_rainbow_1: 40 slots)");
        }

        // Clean exit from per-LED mode: GET current -> SET(current, save=0)
        // -> SET(captured, save=0).
        public void Restore()
        {
            try
            {
                byte[] tmpl = new byte[Proto.Pkt];
                tmpl[0] = Proto.ReportId;
                byte[] cur = Hid.GetFeature(H, tmpl);
                cur[Proto.SaveOff] = 0;
                Hid.SetFeature(H, cur);
            }
            catch (Exception ex)
            {
                App.Warn("pre-restore read failed: " + ex.Message);
            }
            byte[] cap = (byte[])Captured.Clone();
            cap[Proto.SaveOff] = 0;
            Hid.SetFeature(H, cap);
            App.Log("board restored (two-report write of captured 0x52 state, save_data=0)");
        }

        public void Close()
        {
            if (H != IntPtr.Zero) { Hid.CloseHandle(H); H = IntPtr.Zero; }
            if (_singleWriter != null)
            {
                try { _singleWriter.ReleaseMutex(); } catch { }
                _singleWriter.Dispose(); _singleWriter = null;
            }
        }
    }

    // -------------------------------------------------------------------
    // Logging + app plumbing
    // -------------------------------------------------------------------
    static class App
    {
        public static string Base = AppDomain.CurrentDomain.BaseDirectory;
        public static string LogDir = Path.Combine(Base, "logs");
        public static string EvidenceDir = Path.Combine(Base, "evidence");
        public static bool Stopping;

        public static void Log(string msg)
        {
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + msg;
            Console.WriteLine(line);
            try
            {
                Directory.CreateDirectory(LogDir);
                File.AppendAllText(Path.Combine(LogDir, "gpurgb-" +
                    DateTime.Now.ToString("yyyy-MM-dd") + ".log"), line + "\r\n");
            }
            catch { }
        }

        public static void Warn(string msg)
        {
            Log("warn: " + msg);
        }

        public static void Error(string msg)
        {
            Log("error: " + msg);
        }

        public static string Hex(byte[] b)
        {
            return Hex(b, 0, b.Length);
        }

        public static string Hex(byte[] b, int start, int len)
        {
            var sb = new StringBuilder();
            for (int i = start; i < start + len; i++)
            {
                sb.Append(b[i].ToString("X2"));
                if (i < start + len - 1) sb.Append(' ');
            }
            return sb.ToString();
        }
    }

    // -------------------------------------------------------------------
    // Subcommands
    // -------------------------------------------------------------------
    static class Sub
    {
        // probe — open + GET 0x52, print the zone table (185B read-back).
        public static int Probe()
        {
            Session s = null;
            try
            {
                s = Session.Start("probe");
                byte[] st = s.Captured;
                Console.WriteLine("device        : " + s.Path);
                Console.WriteLine("GET 0x52 read-back: " + st.Length + " bytes");
                Console.WriteLine("  [0..15]     : " + App.Hex(st, 0, 16));
                Console.WriteLine("");
                Console.WriteLine("zone table (0x52):");
                for (int z = 0; z < Proto.ZoneNames.Length; z++)
                {
                    int o = Proto.ZoneOffsets[z];
                    Console.WriteLine("  {0,-16} effect=0x{1:X2} RGB({2},{3},{4}) flags=0x{5:X2} cf=0x{6:X2}",
                        Proto.ZoneNames[z], st[o], st[o + 1], st[o + 2], st[o + 3],
                        st[o + 4], st[o + 8]);
                    if (z == 3 || z == 4)
                        Console.WriteLine("    (j_rainbow led_num @+10 = {0})", st[o + 10]);
                }
                Console.WriteLine("  save_data     : " + st[Proto.SaveOff]);
                // save the capture as evidence (hex dump)
                Directory.CreateDirectory(App.EvidenceDir);
                string ev = Path.Combine(App.EvidenceDir, "probe-" +
                    DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".txt");
                File.WriteAllText(ev, "probe " + DateTime.Now + "\ndevice " + s.Path +
                    "\nGET 0x52 (" + st.Length + " bytes):\n" + App.Hex(st) +
                    "\nzone table:\n" + ZoneTable(st) + "\n");
                Console.WriteLine("evidence      : " + ev);
                Console.WriteLine("HID path OK: GET 0x52 (185B) works from Windows.");
                return 0;
            }
            catch (Exception ex)
            {
                App.Error("probe failed: " + ex.Message);
                return 1;
            }
            finally { if (s != null) s.Close(); }
        }

        public static string ZoneTable(byte[] st)
        {
            var sb = new StringBuilder();
            for (int z = 0; z < Proto.ZoneNames.Length; z++)
            {
                int o = Proto.ZoneOffsets[z];
                sb.AppendLine(string.Format("{0,-16} effect=0x{1:X2} RGB({2},{3},{4}) flags=0x{5:X2} cf=0x{6:X2}",
                    Proto.ZoneNames[z], st[o], st[o + 1], st[o + 2], st[o + 3], st[o + 4], st[o + 8]));
            }
            return sb.ToString();
        }

        // hold <slot> <R,G,B> [secs] — one slot lit, everything else black.
        public static int Hold(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("usage: GpuRgb.exe hold <slot> <R,G,B> [secs]   (default 10 s)");
                return 2;
            }
            int slot;
            if (!int.TryParse(args[0], out slot) || slot < 0 || slot >= Proto.NumSlots)
            {
                Console.WriteLine("slot must be an integer 0.." + (Proto.NumSlots - 1));
                return 2;
            }
            byte[] rgb = new byte[3];
            string[] parts = args[1].Split(',');
            if (parts.Length != 3)
            {
                Console.WriteLine("color must be R,G,B");
                return 2;
            }
            for (int i = 0; i < 3; i++)
            {
                int v;
                if (!int.TryParse(parts[i].Trim(), out v) || v < 0 || v > 255)
                {
                    Console.WriteLine("color channels must be 0..255");
                    return 2;
                }
                rgb[i] = (byte)v;
            }
            double secs = 10.0;
            if (args.Length >= 3 && !double.TryParse(args[2], out secs) || secs <= 0)
                secs = 10.0;

            Session s = null;
            try
            {
                s = Session.Start("hold");
                s.EnterPerLed();
                App.Log(string.Format("hold: slot {0} RGB({1},{2},{3}) for {4}s",
                    slot, rgb[0], rgb[1], rgb[2], secs));

                byte[] frame = Packets.BlackFrame();
                frame[3 * slot] = rgb[0];
                frame[3 * slot + 1] = rgb[1];
                frame[3 * slot + 2] = rgb[2];
                byte[] pkt = Packets.BuildPerLed(frame);

                int total = (int)Math.Round(secs * Proto.Fps);
                int f = 0;
                for (; f < total && !App.Stopping; f++)
                {
                    // HARD RULE: write EVERY frame (20 FPS), even unchanged;
                    // re-send the 185B enable every 60 s. Any write gap lets
                    // USB autosuspend sleep the link and drops per-LED mode.
                    Hid.SetFeature(s.H, pkt);
                    if (f > 0 && f % (Proto.Fps * 60) == 0)
                        Hid.SetFeature(s.H, s.Enable);
                    Thread.Sleep(1000 / Proto.Fps);
                }
                if (App.Stopping) App.Log("hold: interrupted (Ctrl+C)");
            }
            catch (Exception ex)
            {
                App.Error("hold failed: " + ex.Message);
                return 1;
            }
            finally
            {
                // clean exit: two-report write of the captured 0x52 state
                if (s != null)
                {
                    try { s.Restore(); }
                    catch (Exception ex) { App.Warn("restore failed: " + ex.Message); }
                    s.Close();
                }
                App.Log("hold: done");
            }
            return 0;
        }

        // restore — exit per-LED mode: two-report write of the captured 0x52
        // state (GET current -> SET current save=0 -> SET captured save=0).
        public static int Restore()
        {
            Session s = null;
            try
            {
                s = Session.Start("restore");
                s.Restore();
                return 0;
            }
            catch (Exception ex)
            {
                App.Error("restore failed: " + ex.Message);
                return 1;
            }
            finally { if (s != null) s.Close(); }
        }

        // selftest — verify the SET path without eyes: capture -> enable ->
        // GET 0x52 must reflect the enable template -> restore -> GET must
        // equal the capture. (GET 0x53 is unsupported; 0x52 read-back is the
        // only software-verifiable effect of a write.)
        public static int SelfTest()
        {
            Session s = null;
            int fails = 0;
            try
            {
                s = Session.Start("selftest");
                s.EnterPerLed();
                byte[] tmpl = new byte[Proto.Pkt];
                tmpl[0] = Proto.ReportId;
                byte[] st = Hid.GetFeature(s.H, tmpl);
                Console.WriteLine("after enable, GET 0x52 read-back:");
                fails += Expect(st[Proto.JRainbow1 + 0] == Proto.EffectStatic, "j_rainbow_1 effect=0x01", st[Proto.JRainbow1 + 0]);
                fails += Expect(st[Proto.JRainbow1 + 10] == 40, "j_rainbow_1 led_num=40", st[Proto.JRainbow1 + 10]);
                fails += Expect(st[Proto.JRainbow2 + 0] == Proto.EffectStatic, "j_rainbow_2 effect=0x01", st[Proto.JRainbow2 + 0]);
                fails += Expect(st[Proto.JRainbow2 + 10] == 40, "j_rainbow_2 led_num=40", st[Proto.JRainbow2 + 10]);
                fails += Expect(st[Proto.JCorsair + 0] == Proto.EffectStatic, "j_corsair effect=0x01", st[Proto.JCorsair + 0]);
                fails += Expect(st[Proto.JCorsair + 10] == 120, "j_corsair is_individual=120", st[Proto.JCorsair + 10]);
                fails += Expect(st[Proto.OnBoard + 0] == Proto.EffectDirect, "on_board_led_0 effect=0x25", st[Proto.OnBoard + 0]);
                s.Restore();
                byte[] st2 = Hid.GetFeature(s.H, tmpl);
                bool same = st2.Length == s.Captured.Length;
                if (same)
                    for (int i = 0; i < st2.Length; i++)
                        if (st2[i] != s.Captured[i]) { same = false; break; }
                fails += Expect(same, "post-restore GET 0x52 == capture", same ? 1 : 0);
                Directory.CreateDirectory(App.EvidenceDir);
                string ev = Path.Combine(App.EvidenceDir, "selftest-" +
                    DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".txt");
                File.WriteAllText(ev, "selftest " + DateTime.Now + "\ndevice " + s.Path +
                    "\nfailures=" + fails + "\nafter-enable 0x52:\n" + App.Hex(st) +
                    "\npost-restore 0x52:\n" + App.Hex(st2) + "\ncapture 0x52:\n" + App.Hex(s.Captured) + "\n");
                Console.WriteLine("evidence      : " + ev);
                Console.WriteLine(fails == 0 ? "SELFTEST PASS: SET path verified via 0x52 read-back; restore exact."
                                              : "SELFTEST FAIL: " + fails + " expectation(s) failed");
                return fails == 0 ? 0 : 1;
            }
            catch (Exception ex)
            {
                App.Error("selftest failed: " + ex.Message);
                return 1;
            }
            finally { if (s != null) s.Close(); }
        }

        static int Expect(bool ok, string what, int got)
        {
            Console.WriteLine("  " + (ok ? "PASS" : "FAIL") + "  " + what + "  (got " + got + ")");
            return ok ? 0 : 1;
        }

        // 20 FPS render loop (HARD): next-tick deadline (start +
        // frameIndex*50 ms), sleep the remainder, never skip writes —
        // HidD_SetFeature takes ~25 ms, so Sleep(50)+write only reaches
        // ~13 FPS. Write EVERY frame even when unchanged; re-send the 185B
        // enable every 60 s (any write gap lets USB autosuspend sleep the
        // link and drop per-LED mode). durMs = double.PositiveInfinity
        // means "until Ctrl+C".
        static void RenderFrames(Session s, Func<int, byte[]> frameAt, double durMs)
        {
            Stopwatch sw = Stopwatch.StartNew();
            long frameIndex = 0;
            while (!App.Stopping)
            {
                long now = sw.ElapsedMilliseconds;
                long deadline = frameIndex * (1000L / Proto.Fps);
                if (now < deadline)
                    Thread.Sleep((int)(deadline - now));
                byte[] frame = frameAt((int)(frameIndex % Proto.PhasePeriod));
                Hid.SetFeature(s.H, Packets.BuildPerLed(frame));
                if (frameIndex > 0 && frameIndex % (Proto.Fps * 60) == 0)
                    Hid.SetFeature(s.H, s.Enable);
                frameIndex++;
                if (sw.Elapsed.TotalMilliseconds >= durMs) break;
            }
        }

        static byte[] BlackAt(int t)
        {
            return Packets.BlackFrame();
        }

        // preview <name|N> [secs] — render one pattern at 20 FPS
        // (default 15 s), clean restore on exit.
        public static int Preview(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("usage: GpuRgb.exe preview <name|N> [secs]   (default 15 s)");
                return 2;
            }
            Patterns.Entry e;
            try
            {
                e = Patterns.Resolve(args[0]);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return 2;
            }
            double secs = 15.0;
            double parsed;
            if (args.Length >= 2 && double.TryParse(args[1], out parsed) && parsed > 0)
                secs = parsed;
            Session s = null;
            try
            {
                s = Session.Start(string.Format("preview {0} {1}", e.Number, e.Name));
                s.EnterPerLed();
                App.Log(string.Format("preview: pattern {0} ({1}) at 20 FPS for {2}s", e.Number, e.Name, secs));
                Func<int, byte[]> fn = delegate(int t) { return e.Frame(t); };
                RenderFrames(s, fn, secs * 1000.0);
                if (App.Stopping)
                    App.Log(string.Format("preview: pattern {0} ({1}) stopped (Ctrl+C)", e.Number, e.Name));
                App.Log(string.Format("preview: pattern {0} ({1}) stop", e.Number, e.Name));
                return 0;
            }
            catch (Exception ex)
            {
                App.Error("preview failed: " + ex.Message);
                return 1;
            }
            finally
            {
                if (s != null)
                {
                    try { s.Restore(); }
                    catch (Exception ex2) { App.Warn("restore failed: " + ex2.Message); }
                    s.Close();
                }
            }
        }

        // cycle [N] — no arg: all 32 in catalog order, 10 s each + 1 s
        // pure-black gap between, then exit; with N: that single pattern
        // until Ctrl+C.
        public static int Cycle(string[] args)
        {
            Session s = null;
            try
            {
                s = Session.Start("cycle");
                s.EnterPerLed();
                if (args.Length >= 1)
                {
                    Patterns.Entry e;
                    try
                    {
                        e = Patterns.Resolve(args[0]);
                    }
                    catch (Exception ex)
                    {
                        App.Error(ex.Message);
                        return 2;
                    }
                    App.Log(string.Format("cycle: pattern {0} ({1}) until Ctrl+C", e.Number, e.Name));
                    Func<int, byte[]> fn = delegate(int t) { return e.Frame(t); };
                    RenderFrames(s, fn, double.PositiveInfinity);
                    App.Log(string.Format("cycle: pattern {0} ({1}) stopped (Ctrl+C)", e.Number, e.Name));
                }
                else
                {
                    App.Log("cycle: all 32 patterns in catalog order, 10 s each + 1 s pure-black gap between");
                    foreach (Patterns.Entry e in Patterns.All)
                    {
                        if (App.Stopping) break;
                        App.Log(string.Format("cycle: pattern {0} ({1}) start", e.Number, e.Name));
                        Func<int, byte[]> fn = delegate(int t) { return e.Frame(t); };
                        RenderFrames(s, fn, 10000.0);
                        App.Log(string.Format("cycle: pattern {0} ({1}) stop", e.Number, e.Name));
                        if (App.Stopping) break;
                        RenderFrames(s, BlackAt, 1000.0);
                    }
                    App.Log("cycle: done");
                }
                return 0;
            }
            catch (Exception ex)
            {
                App.Error("cycle failed: " + ex.Message);
                return 1;
            }
            finally
            {
                if (s != null)
                {
                    try { s.Restore(); }
                    catch (Exception ex2) { App.Warn("restore failed: " + ex2.Message); }
                    s.Close();
                }
            }
        }

        // off [secs] — pure black on ALL 240 slots (Packets.BlackFrame),
        // preview-only (default 15 s).
        public static int Off(string[] args)
        {
            double secs = 15.0;
            double parsed;
            if (args.Length >= 1 && double.TryParse(args[0], out parsed) && parsed > 0)
                secs = parsed;
            Session s = null;
            try
            {
                s = Session.Start("off");
                s.EnterPerLed();
                App.Log(string.Format("off: pure black on all 240 slots for {0}s (preview-only)", secs));
                RenderFrames(s, BlackAt, secs * 1000.0);
                if (App.Stopping) App.Log("off: interrupted (Ctrl+C)");
                App.Log("off: done");
                return 0;
            }
            catch (Exception ex)
            {
                App.Error("off failed: " + ex.Message);
                return 1;
            }
            finally
            {
                if (s != null)
                {
                    try { s.Restore(); }
                    catch (Exception ex2) { App.Warn("restore failed: " + ex2.Message); }
                    s.Close();
                }
            }
        }

        // screen [secs] — print per capture: each zone as one line (o'clock,
        // packet slot, RGB + hex) plus the overall average, plus the active
        // capture period and capture CPU%. The mapping is verified here
        // without the LEDs. The period follows screen_smoothing in
        // config.json (200 ms smoothing / 100 ms responsive) and is
        // re-read live. Default: until Ctrl+C. No device writes.
        public static int Screen(string[] args)
        {
            double secs = 0.0;
            double parsed;
            if (args.Length >= 1 && double.TryParse(args[0], out parsed) && parsed > 0)
                secs = parsed;
            Config cfg = Config.Load(); // fail-loud: this is the debug aid
            ScreenCapture cap = new ScreenCapture();
            cap.SmoothingSource = delegate { return cfg.ScreenSmoothing; };
            cap.Start();
            int p0; double a0;
            Config.ScreenParams(cfg.ScreenSmoothing, out p0, out a0);
            App.Log(string.Format("screen: capturing primary monitor ({0}x{1} grid, period {2} ms, screen_smoothing {3})",
                ScreenZones.GridW, ScreenZones.GridH, p0, cfg.ScreenSmoothing ? "on" : "off"));
            Stopwatch sw = Stopwatch.StartNew();
            while (!App.Stopping)
            {
                int[][] z;
                if (cap.TryGet(out z))
                {
                    int pNow; double aNow;
                    Config.ScreenParams(cap.SmoothingSource(), out pNow, out aNow);
                    string stamp = DateTime.Now.ToString("HH:mm:ss");
                    string stall = cap.Stalled
                        ? "  [STALLED - workstation locked? holding last color]" : "";
                    for (int i = 0; i < 10; i++)
                    {
                        int r = z[i][0], g = z[i][1], b = z[i][2];
                        Console.WriteLine(string.Format(
                            "{0}  {1} o'clock   slot {2}  RGB({3},{4},{5})  #{6:X2}{7:X2}{8:X2}",
                            stamp, ScreenZones.Hours[i], ScreenZones.Slots[i], r, g, b, r, g, b));
                    }
                    int[] ov = z[10];
                    Console.WriteLine(string.Format(
                        "{0}  overall     slots 12/13  RGB({1},{2},{3})  #{4:X2}{5:X2}{6:X2}  [period {7} ms, capture CPU {8:0.0}%{9}]",
                        stamp, ov[0], ov[1], ov[2], ov[0], ov[1], ov[2], pNow, cap.CpuPct, stall));
                }
                else
                    Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") +
                        "  waiting for first capture... (workstation locked? screen capture needs an unlocked desktop)");
                if (secs > 0 && sw.Elapsed.TotalSeconds >= secs) break;
                Thread.Sleep(500);
            }
            cap.Stop();
            App.Log("screen: stopped");
            return 0;
        }

        // serve [secs] — the daemon: GPU temp/power drives the lights with
        // Linux parity. level = max(temp lvl, power lvl); adjacent levels
        // crossfade per slot (frame_colors). Config hot-reload ~1 s.
        // Single writer (mutex). Write EVERY frame @20 FPS; re-enable 60 s;
        // state log throttled ~30 s; clean restore on exit. 0 secs = Ctrl+C.
        public static int Serve(string[] args)
        {
            double secs = 0.0;
            double parsed;
            if (args.Length >= 1 && double.TryParse(args[0], out parsed) && parsed > 0)
                secs = parsed;
            Session s = null;
            ScreenCapture cap = null;
            double[] lerp = new double[11 * 3]; // 10 zones + overall, flat 33
            bool scrHas = false;
            EventWaitHandle stopEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "GpuRgbStopSignal");
            bool stallWarned = false;
            try
            {
                Config cfg = Config.Load();
                Func<int, byte[]>[] fns = cfg.LevelFns();
                s = Session.Start("serve");
                s.EnterPerLed();
                cap = new ScreenCapture();
                cap.SmoothingSource = delegate { return cfg.ScreenSmoothing; };
                cap.Start();
                App.Log(string.Format("serve: {0} FPS per-LED mode, mode {1}, screen_smoothing {2}, drive model active (levels {3}/{4}/{5}/{6})",
                    Proto.Fps, cfg.Mode, cfg.ScreenSmoothing ? "on" : "off",
                    cfg.Levels[0], cfg.Levels[1], cfg.Levels[2], cfg.Levels[3]));
                Smoothed sm = new Smoothed();
                double lastTemp = 0.0, lastPower = 0.0;
                bool haveRaw = false;
                Stopwatch sw = Stopwatch.StartNew();
                long frameIndex = 0;
                int phase = 0;
                long lastLogMs = -30000;
                DateTime cfgWrite = File.Exists(Config.CfgPath)
                    ? File.GetLastWriteTimeUtc(Config.CfgPath) : DateTime.MinValue;
                string statusPath = Path.Combine(App.Base, "status.json");
                while (!App.Stopping)
                {
                    if (stopEvent.WaitOne(0)) { App.Log("serve: stop signal received"); break; }
                    long now = sw.ElapsedMilliseconds;
                    long deadline = frameIndex * (1000L / Proto.Fps);
                    if (now < deadline) Thread.Sleep((int)(deadline - now));
                    if (frameIndex % 100 == 0)
                    {
                        if (cap.Stalled && !stallWarned)
                        {
                            App.Warn("serve: screen capture stalled (workstation locked?) - holding last color");
                            stallWarned = true;
                        }
                        else if (!cap.Stalled && stallWarned)
                        {
                            App.Log("serve: screen capture resumed");
                            stallWarned = false;
                        }
                        double t, p;
                        if (Sensors.Read(out t, out p))
                        { sm.Add(t, p); lastTemp = t; lastPower = p; haveRaw = true; }
                        else App.Warn("serve: sensor read failed (keeping last EMA)");
                    }
                    if (frameIndex % 20 == 0)
                    {
                        DateTime w = File.Exists(Config.CfgPath)
                            ? File.GetLastWriteTimeUtc(Config.CfgPath) : DateTime.MinValue;
                        if (w != cfgWrite)
                        {
                            cfgWrite = w;
                            try
                            {
                                Config nc = Config.Load();
                                fns = nc.LevelFns();
                                cfg = nc;
                                App.Log("serve: config reloaded (levels " + cfg.Levels[0] + "/" +
                                    cfg.Levels[1] + "/" + cfg.Levels[2] + "/" + cfg.Levels[3] +
                                    ", mode " + cfg.Mode +
                                    ", screen_smoothing " + (cfg.ScreenSmoothing ? "on" : "off") + ")");
                            }
                            catch (Exception ex)
                            { App.Error("serve: config reload rejected, keeping previous: " + ex.Message); }
                        }
                    }
                    double[] wts = Model.Weights(sm.TempOrFallback, sm.PowerOrFallback);
                    byte[] frame;
                    // Responsive flag, read per frame (config hot-reloads
                    // ~1 s): alpha 0.1 (smooth) or 1.0 (snap); the capture
                    // thread reads the period itself each loop iteration.
                    int periodMs; double alpha;
                    Config.ScreenParams(cfg.ScreenSmoothing, out periodMs, out alpha);
                    if (cfg.Mode == "screen")
                    {
                        int[][] z;
                        if (cap.TryGet(out z))
                        {
                            if (!scrHas)
                            {
                                for (int i = 0; i < 11; i++)
                                    for (int ch = 0; ch < 3; ch++)
                                        lerp[i * 3 + ch] = z[i][ch];
                                scrHas = true;
                            }
                            else
                            {
                                for (int i = 0; i < 11; i++)
                                    for (int ch = 0; ch < 3; ch++)
                                        lerp[i * 3 + ch] += alpha * (z[i][ch] - lerp[i * 3 + ch]);
                            }
                        }
                        int[][] lz = new int[11][];
                        for (int i = 0; i < 11; i++)
                            lz[i] = new int[]
                            {
                                (int)lerp[i * 3], (int)lerp[i * 3 + 1], (int)lerp[i * 3 + 2]
                            };
                        frame = Packets.ScreenZoneFrame(lz);
                    }
                    else
                    {
                        frame = Patterns.FrameColors(wts, phase, fns);
                    }
                    Hid.SetFeature(s.H, Packets.BuildPerLed(frame));
                    if (frameIndex > 0 && frameIndex % (Proto.Fps * 60) == 0)
                        Hid.SetFeature(s.H, s.Enable);
                    phase = (phase + 1) % Proto.PhasePeriod;
                    frameIndex++;
                    if (frameIndex % 100 == 0)
                    {
                        double lvl = Model.Level(sm.TempOrFallback, sm.PowerOrFallback);
                        int dom = 0;
                        for (int k = 1; k < 4; k++) if (wts[k] > wts[dom]) dom = k;
                        WriteStatus(statusPath, cfg, sm, lastTemp, lastPower, haveRaw, lvl, dom, wts, s.Path, cap, lerp, scrHas);
                        if (sw.ElapsedMilliseconds - lastLogMs > 30000)
                        {
                            App.Log(string.Format(CultureInfo.InvariantCulture,
                                "temp {0:0.0} C / {1:0.0} W -> {2} (level {3:0.00}, weights {4:0.00}/{5:0.00}/{6:0.00}/{7:0.00})",
                                sm.TempOrFallback, sm.PowerOrFallback, Model.LevelNames[dom],
                                lvl, wts[0], wts[1], wts[2], wts[3]));
                            lastLogMs = sw.ElapsedMilliseconds;
                        }
                    }
                    if (secs > 0 && sw.Elapsed.TotalSeconds >= secs) break;
                }
                App.Log("serve: stopping (restoring board)");
                return 0;
            }
            catch (Exception ex) { App.Error("serve failed: " + ex.Message); return 1; }
            finally
            {
                if (cap != null) cap.Stop();
                if (s != null)
                {
                    try { s.Restore(); }
                    catch (Exception ex2) { App.Warn("restore failed: " + ex2.Message); }
                    s.Close();
                }
                stopEvent.Dispose();
            }
        }

        // stop — signal a running serve daemon to exit gracefully (restores
        // the board). Uses the named event GpuRgbStopSignal.
        public static int Stop()
        {
            EventWaitHandle ev;
            try
            {
                ev = EventWaitHandle.OpenExisting("GpuRgbStopSignal");
            }
            catch (Exception)
            {
                Console.WriteLine("no running serve daemon (stop event not found)");
                return 1;
            }
            ev.Set();
            ev.Dispose();
            App.Log("stop: signal sent to serve daemon");
            Console.WriteLine("stop signal sent; daemon will restore the board and exit");
            return 0;
        }

        static void WriteStatus(string path, Config cfg, Smoothed sm, double rawT, double rawP,
            bool haveRaw, double lvl, int dom, double[] w, string devPath,
            ScreenCapture cap, double[] lerp, bool scrHas)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["pid"] = Process.GetCurrentProcess().Id;
            d["timestamp"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            d["mode"] = cfg.Mode;
            d["level"] = lvl;
            d["pattern"] = Model.LevelNames[dom];
            d["weights"] = w;
            d["temp_raw"] = haveRaw ? (object)rawT : null;
            d["power_raw"] = haveRaw ? (object)rawP : null;
            d["temp_ema"] = sm.HasTemp ? (object)sm.Temp : null;
            d["power_ema"] = sm.HasPower ? (object)sm.Power : null;
            d["levels"] = cfg.Levels;
            d["device"] = devPath;
            d["capture_cpu_pct"] = cap != null ? (object)cap.CpuPct : null;
            d["capture_stalled"] = cap != null && cap.Stalled;
            d["screen_rgb"] = scrHas ? (object)new int[] { (int)lerp[30], (int)lerp[31], (int)lerp[32] } : null;
            try
            {
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, new JavaScriptSerializer().Serialize(d));
                File.Copy(tmp, path, true);
                File.Delete(tmp);
            }
            catch { }
        }

        // status — daemon state (status.json freshness), config, autostart task.
        public static int Status()
        {
            string sp = Path.Combine(App.Base, "status.json");
            if (!File.Exists(sp)) Console.WriteLine("daemon    : not running (no status.json)");
            else
            {
                try
                {
                    Dictionary<string, object> d = new JavaScriptSerializer()
                        .Deserialize<Dictionary<string, object>>(File.ReadAllText(sp));
                    DateTime t;
                    DateTime.TryParse(Convert.ToString(d["timestamp"]), out t);
                    double age = (DateTime.Now - t).TotalSeconds;
                    Console.WriteLine("daemon    : " + (age < 15 ? "RUNNING" : "STALE") +
                        " (pid " + d["pid"] + ", state " + d["timestamp"] + ", " + age.ToString("0") + " s ago)");
                    Console.WriteLine("device    : " + d["device"]);
                    Console.WriteLine("mode      : " + d["mode"]);
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "level     : {0:0.00} -> {1}", Convert.ToDouble(d["level"]), d["pattern"]));
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "temp      : raw {0} C, EMA {1:0.0} C", d["temp_raw"], Convert.ToDouble(d["temp_ema"])));
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "power     : raw {0} W, EMA {1:0.0} W", d["power_raw"], Convert.ToDouble(d["power_ema"])));
                    object[] srgb = d.ContainsKey("screen_rgb") ? d["screen_rgb"] as object[] : null;
                    if (srgb != null)
                        Console.WriteLine(string.Format("screen    : RGB({0},{1},{2})",
                            Convert.ToInt32(srgb[0]), Convert.ToInt32(srgb[1]), Convert.ToInt32(srgb[2])));
                    if (d.ContainsKey("capture_cpu_pct") && d["capture_cpu_pct"] != null)
                        Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                            "capture   : CPU {0:0.0}%", Convert.ToDouble(d["capture_cpu_pct"])));
                    object[] w = d["weights"] as object[];
                    if (w != null)
                        Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                            "weights   : {0:0.00}/{1:0.00}/{2:0.00}/{3:0.00}",
                            Convert.ToDouble(w[0]), Convert.ToDouble(w[1]), Convert.ToDouble(w[2]), Convert.ToDouble(w[3])));
                }
                catch (Exception ex) { Console.WriteLine("status.json unreadable: " + ex.Message); }
            }
            try
            {
                Config c = Config.Load();
                Console.WriteLine("config    : mode=" + c.Mode + " levels=" + string.Join("/", c.Levels));
            }
            catch (Exception ex) { Console.WriteLine("config    : ERROR " + ex.Message); }
            try
            {
                Process p = new Process();
                p.StartInfo.FileName = "schtasks";
                p.StartInfo.Arguments = "/query /tn GpuRgb /fo LIST";
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.CreateNoWindow = true;
                p.Start();
                p.StandardOutput.ReadToEnd();
                p.WaitForExit(10000);
                Console.WriteLine("autostart : " + (p.ExitCode == 0
                    ? "scheduled task GpuRgb registered" : "not installed (see README)"));
            }
            catch { Console.WriteLine("autostart : unknown"); }
            return 0;
        }

        // assign <level 0-3> <pattern-name|N> — write config.json; a running
        // daemon hot-reloads within ~1 s. Unknown names fail loudly.
        public static int Assign(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("usage: GpuRgb.exe assign <level 0-3> <pattern-name|N>");
                return 2;
            }
            int level;
            if (!int.TryParse(args[0], out level) || level < 0 || level > 3)
            {
                Console.WriteLine("level must be 0..3");
                return 2;
            }
            Patterns.Entry e;
            try { e = Patterns.Resolve(args[1]); }
            catch (Exception ex) { Console.WriteLine(ex.Message); return 2; }
            Config c;
            try { c = Config.Load(); }
            catch (Exception ex) { Console.WriteLine("existing config invalid: " + ex.Message); return 1; }
            c.Levels[level] = e.Name;
            c.Save();
            App.Log(string.Format("assign: level {0} -> {1} ({2}) written to config.json",
                level, e.Number, e.Name));
            Console.WriteLine(string.Format("level {0} = {1} ({2}); running daemon hot-reloads within ~1 s",
                level, e.Number, e.Name));
            return 0;
        }

        // stats [secs] — poll nvidia-smi every 5 s, EMA alpha 0.25, print
        // raw + EMA temp/power and the derived level. Default: until Ctrl+C.
        public static int Stats(string[] args)
        {
            double secs = 0.0;
            double parsed;
            if (args.Length >= 1 && double.TryParse(args[0], out parsed) && parsed > 0)
                secs = parsed;
            Smoothed sm = new Smoothed();
            App.Log("stats: nvidia-smi every 5 s, EMA alpha 0.25" +
                (secs > 0 ? string.Format(CultureInfo.InvariantCulture, ", {0}s", secs)
                          : " (Ctrl+C to stop)"));
            Stopwatch sw = Stopwatch.StartNew();
            int lastLevel = -1;
            while (!App.Stopping)
            {
                double t, p;
                bool okr = Sensors.Read(out t, out p);
                if (okr) sm.Add(t, p);
                else App.Warn("stats: nvidia-smi read failed (keeping last EMA)");
                double[] w = Model.Weights(sm.TempOrFallback, sm.PowerOrFallback);
                double lvl = Model.Level(sm.TempOrFallback, sm.PowerOrFallback);
                string label = Model.Label(w);
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "{0}  raw {1} C {2} W | EMA {3:0.0} C {4:0.0} W | level {5:0.00} -> {6} (w {7:0.00}/{8:0.00}/{9:0.00}/{10:0.00})",
                    DateTime.Now.ToString("HH:mm:ss"),
                    okr ? t.ToString("0", CultureInfo.InvariantCulture) : "?",
                    okr ? p.ToString("0.00", CultureInfo.InvariantCulture) : "?",
                    sm.HasTemp ? sm.Temp : 0.0, sm.HasPower ? sm.Power : 0.0,
                    lvl, label, w[0], w[1], w[2], w[3]));
                int dom = 0;
                for (int k = 1; k < 4; k++) if (w[k] > w[dom]) dom = k;
                if (dom != lastLevel)
                {
                    App.Log(string.Format(CultureInfo.InvariantCulture,
                        "stats: level {0} -> {1} ({2})", lastLevel, dom, label));
                    lastLevel = dom;
                }
                if (secs > 0 && sw.Elapsed.TotalSeconds >= secs) break;
                for (int i = 0; i < 50 && !App.Stopping; i++) Thread.Sleep(100);
            }
            App.Log("stats: stopped");
            return 0;
        }

        // --check — offline validation (no device touched).
        public static int Check()
        {
            // Tee all --check output to evidence\p2-check-<timestamp>.txt
            // (kept even on failure, for post-mortem).
            TextWriter originalOut = Console.Out;
            StringWriter tee = new StringWriter();
            Console.SetOut(new TeeWriter(originalOut, tee));
            int rc = 0;
            bool hardFail = false;
            Tst.Collecting = true;
            Tst.Failures.Clear();
            try
            {
                try
                {
                    // 1. enable message
                    byte[] en = Packets.BuildEnable(new byte[Proto.Pkt]);
                    Tst.Assert(en.Length == Proto.Pkt, "enable length");
                    Tst.Assert(en[Proto.JRainbow1 + 10] == 40, "enable j_rainbow_1 led_num");
                    Tst.Assert(en[Proto.JRainbow2 + 10] == 40, "enable j_rainbow_2 led_num");
                    Tst.Assert(en[Proto.JCorsair + 10] == 120, "enable j_corsair is_individual");
                    Tst.Assert(en[Proto.OnBoard + 0] == Proto.EffectDirect, "enable on_board effect");
                    Tst.Assert(en[Proto.JRainbow1 + 0] == Proto.EffectStatic, "enable j_rainbow_1 effect");

                    // 2. per-LED packet
                    byte[] fr = Packets.BlackFrame();
                    fr[3 * 8] = 255; fr[3 * 8 + 1] = 0; fr[3 * 8 + 2] = 0;
                    byte[] p = Packets.BuildPerLed(fr);
                    Tst.Assert(p.Length == Proto.PerLedPkt, "per-LED packet length");
                    Tst.Assert(p[0] == 0x53, "per-LED report id");
                    Tst.Assert(p[1] == 0x25 && p[2] == 0x06, "per-LED header");
                    Tst.Assert(p[5 + 3 * 8] == 255, "slot 8 R lands at byte 5+3*8");
                    Tst.Assert(p[5 + 3 * 8 + 1] == 0 && p[5 + 3 * 8 + 2] == 0, "slot 8 G/B");

            // 2b. SCREEN-ZONES: ScreenZoneFrame — slots 2..11 = zone colors
            //     in slot order, 12/13 = overall, 0/1 and 14..239 black.
            {
                int[][] z = new int[11][];
                // distinct colors, all channels <= 255 at i = 9
                for (int i = 0; i < 10; i++)
                    z[i] = new int[] { 11 * i + 5, 17 * i + 9, 23 * i + 13 };
                z[10] = new int[] { 7, 8, 9 }; // overall
                byte[] zf = Packets.ScreenZoneFrame(z);
                Tst.Assert(zf.Length == Proto.NumSlots * 3, "zone frame length 720");
                for (int i = 0; i < 10; i++)
                {
                    int s = ScreenZones.Slots[i];
                    Tst.Assert(zf[3 * s] == 11 * i + 5 && zf[3 * s + 1] == 17 * i + 9 &&
                        zf[3 * s + 2] == 23 * i + 13,
                        "zone " + ScreenZones.Hours[i] + " o'clock lands in slot " + s);
                }
                for (int s = 12; s <= 13; s++)
                    Tst.Assert(zf[3 * s] == 7 && zf[3 * s + 1] == 8 && zf[3 * s + 2] == 9,
                        "overall lands in slot " + s);
                for (int s = 0; s < Proto.NumSlots; s++)
                {
                    bool zone = false;
                    for (int i = 0; i < 10; i++)
                        if (s == ScreenZones.Slots[i]) zone = true;
                    if (!zone && s != 12 && s != 13)
                        Tst.Assert(zf[3 * s] == 0 && zf[3 * s + 1] == 0 && zf[3 * s + 2] == 0,
                            "slot " + s + " black");
                }
            }

                    // 2c. screen-zone angle lookup (SCREEN-ZONES 2026-11-23):
                    //     angle (deg CW from top) -> zone hour. Half-open
                    //     spans; boundary values belong to the zone on the
                    //     right (45->2, 90->4, 225->8, 285->10, 345->12).
                    int[][] zoneCases =
                    {
                        new int[] { 0, 12 }, new int[] { 30, 1 }, new int[] { 60, 2 },
                        new int[] { 95, 4 }, new int[] { 180, 6 }, new int[] { 240, 8 },
                        new int[] { 270, 8 }, new int[] { 300, 10 }, new int[] { 330, 11 },
                        new int[] { 45, 2 }, new int[] { 90, 4 }, new int[] { 225, 8 },
                        new int[] { 285, 10 }, new int[] { 345, 12 },
                    };
                    foreach (int[] zc in zoneCases)
                    {
                        int zi = ScreenZones.ZoneForAngle(zc[0]);
                        Tst.Assert(ScreenZones.Hours[zi] == zc[1],
                            "zone lookup: angle " + zc[0] + " -> hour " + zc[1] +
                            " (got " + ScreenZones.Hours[zi] + ")");
                    }

                    // 2d. responsive flag -> (capture period ms, lerp alpha).
                    int ppm; double palpha;
                    Config.ScreenParams(true, out ppm, out palpha);
                    Tst.Assert(ppm == 200 && Math.Abs(palpha - 0.1) < 1e-9,
                        "screen_smoothing true -> (200, 0.1)");
                    Config.ScreenParams(false, out ppm, out palpha);
                    Tst.Assert(ppm == 100 && Math.Abs(palpha - 1.0) < 1e-9,
                        "screen_smoothing false -> (100, 1.0)");

                    // 2e. screen_smoothing config parse (fail-loud).
                    Config cdef = Config.Parse(new Dictionary<string, object>());
                    Tst.Assert(cdef.ScreenSmoothing, "screen_smoothing absent -> true (default)");
                    Dictionary<string, object> ds = new Dictionary<string, object>();
                    ds["screen_smoothing"] = true;
                    Tst.Assert(Config.Parse(ds).ScreenSmoothing, "screen_smoothing true parses");
                    ds["screen_smoothing"] = false;
                    Tst.Assert(!Config.Parse(ds).ScreenSmoothing, "screen_smoothing false parses");
                    Dictionary<string, object> dbad = new Dictionary<string, object>();
                    dbad["screen_smoothing"] = 1;
                    bool ssThrew = false;
                    try { Config.Parse(dbad); }
                    catch (Exception) { ssThrew = true; }
                    Tst.Assert(ssThrew, "screen_smoothing non-bool throws (fail-loud)");

                    // 2f. synthetic grids through the pure zone math + frame
                    //     builder (SCREEN-ZONES 2026-11-23).
                    int gw = ScreenZones.GridW, gh = ScreenZones.GridH;

                    // (a) 12 o'clock cap: white cells = EXACTLY the cells
                    //     assigned to zone 12 (the narrow top-center cap;
                    //     zone 12 spans |dx| <= 4 near the top, narrowing
                    //     to |dx| = 0 at the midline). The spec's literal
                    //     "|dx| <= 2 strip" cannot light slot 10 to exactly
                    //     white (zone 12 is wider than the strip), so the
                    //     cap is the binding realization of "white at
                    //     middle-top -> slot 10 white"; the literal strip is
                    //     asserted in (a2).
                    byte[] pxa = new byte[gw * gh * 4];
                    for (int y = 0; y < gh; y++)
                        for (int x = 0; x < gw; x++)
                            if (ScreenZones.ZoneForCell(x, y, gw, gh) == 0)
                            {
                                int o = (y * gw + x) * 4;
                                pxa[o] = 255; pxa[o + 1] = 255; pxa[o + 2] = 255; // B,G,R
                            }
                    int[][] za = ScreenZones.GridToZones(pxa, gw, gh);
                    byte[] zfa = Packets.ScreenZoneFrame(za);
                    Tst.Assert(zfa[3 * 10] == 255 && zfa[3 * 10 + 1] == 255 && zfa[3 * 10 + 2] == 255,
                        "cap grid: slot 10 (12 o'clock) white");
                    for (int s = 2; s <= 11; s++)
                        if (s != 10)
                            Tst.Assert(zfa[3 * s] == 0 && zfa[3 * s + 1] == 0 && zfa[3 * s + 2] == 0,
                                "cap grid: ring slot " + s + " black");
                    for (int s = 12; s <= 13; s++)
                        Tst.Assert(zfa[3 * s] <= 12 && zfa[3 * s + 1] <= 12 && zfa[3 * s + 2] <= 12,
                            "cap grid: overall slot " + s + " ~black");
                    for (int s = 0; s <= 1; s++)
                        Tst.Assert(zfa[3 * s] == 0 && zfa[3 * s + 1] == 0 && zfa[3 * s + 2] == 0,
                            "cap grid: slot " + s + " black");
                    for (int s = 14; s < Proto.NumSlots; s++)
                        Tst.Assert(zfa[3 * s] == 0 && zfa[3 * s + 1] == 0 && zfa[3 * s + 2] == 0,
                            "cap grid: dead slot " + s + " black");

                    // (a2) spec-literal narrow strip: white cells at
                    //     |dx| <= 2 (x = 30..34), top 2 rows (y = 0..1).
                    //     Slot 10 is the strictly brightest ring slot;
                    //     overall ~black (10 white cells / 2304).
                    byte[] pxs = new byte[gw * gh * 4];
                    for (int y = 0; y < 2; y++)
                        for (int x = gw / 2 - 2; x <= gw / 2 + 2; x++)
                        {
                            int o = (y * gw + x) * 4;
                            pxs[o] = 255; pxs[o + 1] = 255; pxs[o + 2] = 255;
                        }
                    int[][] zs = ScreenZones.GridToZones(pxs, gw, gh);
                    byte[] zfs = Packets.ScreenZoneFrame(zs);
                    Tst.Assert(zfs[3 * 10] > 0, "strip grid: slot 10 lit");
                    for (int s = 2; s <= 11; s++)
                        if (s != 10)
                            Tst.Assert(zfs[3 * s] == 0 && zfs[3 * s + 1] == 0 && zfs[3 * s + 2] == 0,
                                "strip grid: ring slot " + s + " black (slot 10 brightest)");
                    for (int s = 12; s <= 13; s++)
                        Tst.Assert(zfs[3 * s] <= 2 && zfs[3 * s + 1] <= 2 && zfs[3 * s + 2] <= 2,
                            "strip grid: overall slot " + s + " ~black");
                    for (int s = 0; s <= 1; s++)
                        Tst.Assert(zfs[3 * s] == 0 && zfs[3 * s + 1] == 0 && zfs[3 * s + 2] == 0,
                            "strip grid: slot " + s + " black");
                    for (int s = 14; s < Proto.NumSlots; s++)
                        Tst.Assert(zfs[3 * s] == 0 && zfs[3 * s + 1] == 0 && zfs[3 * s + 2] == 0,
                            "strip grid: dead slot " + s + " black");

                    // (b) all-white grid: every zone + overall white.
                    byte[] pxb = new byte[gw * gh * 4];
                    for (int i = 0; i < pxb.Length; i += 4)
                        { pxb[i] = 255; pxb[i + 1] = 255; pxb[i + 2] = 255; }
                    int[][] zb = ScreenZones.GridToZones(pxb, gw, gh);
                    byte[] zfb = Packets.ScreenZoneFrame(zb);
                    for (int s = 2; s <= 13; s++)
                        Tst.Assert(zfb[3 * s] == 255 && zfb[3 * s + 1] == 255 && zfb[3 * s + 2] == 255,
                            "white grid: slot " + s + " white");
                    for (int s = 0; s <= 1; s++)
                        Tst.Assert(zfb[3 * s] == 0 && zfb[3 * s + 1] == 0 && zfb[3 * s + 2] == 0,
                            "white grid: slot " + s + " black");
                    for (int s = 14; s < Proto.NumSlots; s++)
                        Tst.Assert(zfb[3 * s] == 0 && zfb[3 * s + 1] == 0 && zfb[3 * s + 2] == 0,
                            "white grid: dead slot " + s + " black");

                    // 3. static state
                    byte[] cap = new byte[Proto.Pkt];
                    byte[] st = Packets.BuildStatic(cap, new byte[] { 33, 33, 140 });
                    Tst.Assert(st.Length == Proto.Pkt, "static length");
                    for (int z = 0; z < Proto.ZoneOffsets.Length; z++)
                        Tst.Assert(st[Proto.ZoneOffsets[z]] == Proto.EffectStatic, "static zone effect " + z);
                    Tst.Assert(st[Proto.SaveOff] == 0, "static save_data=0");

                    // 4. drive model (ports of the gpu_rgb.py --check assertions)
                    double[][][] cases =
                    {
                        new double[][] { new double[] { 40, 15 }, new double[] { 1, 0, 0, 0 } },
                        new double[][] { new double[] { 55, 15 }, new double[] { 1, 0, 0, 0 } },
                        new double[][] { new double[] { 40, 25 }, new double[] { 0, 1, 0, 0 } },
                        new double[][] { new double[] { 60, 15 }, new double[] { 0, 1, 0, 0 } },
                        new double[][] { new double[] { 65, 15 }, new double[] { 0, 0, 1, 0 } },
                        new double[][] { new double[] { 40, 310 }, new double[] { 0, 0, 1, 0 } },
                        new double[][] { new double[] { 75, 15 }, new double[] { 0, 0, 0, 1 } },
                        new double[][] { new double[] { 95, 310 }, new double[] { 0, 0, 0, 1 } },
                        new double[][] { new double[] { 40, 100 }, new double[] { 0, 1, 0, 0 } },
                    };
                    foreach (double[][] c in cases)
                    {
                        double[] weights = Model.Weights(c[0][0], c[0][1]);
                        for (int i = 0; i < 4; i++)
                            Tst.Assert(Math.Abs(weights[i] - c[1][i]) < 0.001,
                                string.Format("model mismatch at temp={0} pwr={1}", c[0][0], c[0][1]));
                    }
                    double[] wh = Model.Weights(57.5, 15);
                    Tst.Assert(Math.Abs(wh[0] - 0.5) < 0.05 && Math.Abs(wh[1] - 0.5) < 0.05,
                        "smoothstep half-blend at 57.5 C");

                    // 5. patterns (Phase 2): registry + all 3840 frames
                    //    byte-exact vs tests\groundtruth.bin
                    //    (self-contained asserts are collected via Tst,
                    //    never thrown: the ground-truth comparison below
                    //    always runs to completion for all 32 patterns)
                    Patterns.CheckAll();
                    Patterns.CheckDrive();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    hardFail = true;
                    rc = 1;
                }
                // Report the collected assertion failures AFTER the
                // byte-exact ground-truth comparison has run to completion
                // (RING10 rebase foundation, 2026-11-22).
                if (Tst.Failures.Count > 0)
                {
                    foreach (string f in Tst.Failures)
                        Console.WriteLine("--check FAILED: " + f);
                    rc = 1;
                }
                else if (!hardFail)
                {
                    Console.WriteLine("OK: enable message, per-LED packet, static state, and drive model all valid");
                }
            }
            finally
            {
                Console.SetOut(originalOut);
                try
                {
                    Directory.CreateDirectory(App.EvidenceDir);
                    string ev = Path.Combine(App.EvidenceDir, "p2-check-" +
                        DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".txt");
                    File.WriteAllText(ev, tee.ToString());
                    Console.WriteLine("check output saved: " + ev);
                }
                catch (Exception ex2)
                {
                    App.Warn("could not save check evidence: " + ex2.Message);
                }
            }
            return rc;
        }
    }

    static class Tst
    {
        // RING10 rebase foundation (2026-11-22): --check COLLECTS assertion
        // failures instead of throwing on the first one, so the byte-exact
        // ground-truth comparison always runs to completion and reports all
        // per-pattern mismatches even when self-contained assertion blocks
        // fail. Check() enables Collecting, then reports every collected
        // failure after the comparison.
        internal static readonly List<string> Failures = new List<string>();
        internal static bool Collecting = false;

        public static void Assert(bool cond, string what)
        {
            if (!cond)
            {
                if (Collecting)
                {
                    Failures.Add(what);
                    return;
                }
                throw new Exception("--check FAILED: " + what);
            }
        }
    }

    // -------------------------------------------------------------------
    // TeeWriter — mirrors console output to an in-memory buffer so the
    // full --check output can be saved as evidence.
    // -------------------------------------------------------------------
    class TeeWriter : TextWriter
    {
        TextWriter _inner;
        StringWriter _sw;

        public TeeWriter(TextWriter inner, StringWriter sw)
        {
            _inner = inner;
            _sw = sw;
        }

        public override Encoding Encoding
        {
            get { return _inner.Encoding; }
        }

        public override void Write(char c)
        {
            _inner.Write(c);
            _sw.Write(c);
        }

        public override void WriteLine(string s)
        {
            _inner.WriteLine(s);
            _sw.WriteLine(s);
        }

        public override void Flush()
        {
        }
    }

    // -------------------------------------------------------------------
    // Main
    // -------------------------------------------------------------------
    static class MainApp
    {
        public static int Main(string[] args)
        {
            Console.CancelKeyPress += (s, e) =>
            {
                App.Stopping = true;
                e.Cancel = true; // let our loops finish and restore the board
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                Exception ue = e.ExceptionObject as Exception;
                App.Error("unhandled: " + (ue == null ? "<unknown>" : ue.Message));
            };

            try
            {
                if (args.Length == 0)
                {
                    Usage();
                    return 2;
                }
                string cmd = args[0];
                if (cmd == "--check") return Sub.Check();
                if (cmd == "preview") return Sub.Preview(Slice(args, 1));
                if (cmd == "cycle") return Sub.Cycle(Slice(args, 1));
                if (cmd == "off") return Sub.Off(Slice(args, 1));
                if (cmd == "stats") return Sub.Stats(Slice(args, 1));
                if (cmd == "screen") return Sub.Screen(Slice(args, 1));
                if (cmd == "stop") return Sub.Stop();
                if (cmd == "serve") return Sub.Serve(Slice(args, 1));
                if (cmd == "status") return Sub.Status();
                if (cmd == "assign") return Sub.Assign(Slice(args, 1));
                if (cmd == "probe") return Sub.Probe();
                if (cmd == "restore") return Sub.Restore();
                if (cmd == "selftest") return Sub.SelfTest();
                if (cmd == "hold") return Sub.Hold(Slice(args, 1));
                Usage();
                return 2;
            }
            catch (Exception ex)
            {
                App.Error(ex.Message);
                return 1;
            }
        }

        static string[] Slice(string[] args, int from)
        {
            string[] rest = new string[args.Length - from];
            for (int i = from; i < args.Length; i++) rest[i - from] = args[i];
            return rest;
        }

        public static void Usage()
        {
            Console.WriteLine("gpu-rgb (MSI Mystic Light, MSI PRO B650-VC WIFI III) — Phase 7");
            Console.WriteLine();
            Console.WriteLine("usage: GpuRgb.exe <subcommand>");
            Console.WriteLine("  probe                        open the device, GET 0x52, print the zone table");
            Console.WriteLine("  hold <slot> <R,G,B> [secs]   one slot lit, everything else black (default 10 s)");
            Console.WriteLine("  restore                      exit per-LED mode: two-report write of the captured 0x52 state");
            Console.WriteLine("  selftest                     verify SET path via 0x52 read-back + exact restore (no eyes needed)");
            Console.WriteLine("  preview <name|N> [secs]      render one pattern at 20 FPS (default 15 s), clean restore");
            Console.WriteLine("  cycle [N]                    all 32 in catalog order, 10 s each + 1 s pure-black gap, then exit; N = that pattern until Ctrl+C");
            Console.WriteLine("  off [secs]                   pure black on all 240 slots, preview-only (default 15 s)");
            Console.WriteLine("  stats [secs]                 nvidia-smi temp/power every 5 s, EMA 0.25, derived level (default: until Ctrl+C)");
            Console.WriteLine("  screen [secs]                per-zone screen capture: 10 zone lines (o'clock, slot, RGB+hex) + overall + active period + CPU (default: until Ctrl+C)");
            Console.WriteLine("  serve [secs]                 daemon: temp/power or screen-color drives lights, config hot-reload (default: until Ctrl+C)");
            Console.WriteLine("  stop                         signal the serve daemon to exit gracefully (restores board)");
            Console.WriteLine("  status                       daemon state, mode/level, temp/power, config, autostart task");
            Console.WriteLine("  assign <level> <name|N>      re-point a drive level (0-3) to any pattern; writes config.json");
            Console.WriteLine("  --check                      offline validation (no device touched)");
        }
    }
}
