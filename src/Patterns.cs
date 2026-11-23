// Patterns.cs — Phase 2: all 32 LED patterns (mystic-preview catalog 1-32),
// ported byte-for-byte from ref\gpu_rgb.py (PREVIEW_STATES, first 32
// entries in insertion order). Frame functions are PURE in integer phase
// t in [0,120): 720 bytes (240 RGB slots, slot i = bytes 3i..3i+2),
// 120-periodic, dead slots 14..239 always black.
//
// Porting rules honored:
//  - Python % is FLOORED modulo -> ModFloor / ModFloorD.
//  - Python int(x) truncates toward zero == C# (int) cast on double.
//  - Python round() is banker's rounding == Math.Round default.
//  - math.sin/cos == Math.Sin/Cos (identical IEEE-754 double math).
//  - Precomputed tables (LIMELIGHT_BREATH, INFERNO_BREATH, INFERNO_FLICKER
//    fixed-seed LCG, REACTOR_BREATH, REACTOR_PULSE) ported EXACTLY: same
//    seeds, same recurrence, same order.
//  - Each fn reduces phase % 120 first; for phase in 0..119 that is an
//    identity, so frames match the Linux reference (which is also called
//    only with t in [0,120)) while Frame(t+120) == Frame(t) bit-exactly.
using System;
using System.IO;

namespace GpuRgb
{
    static class Patterns
    {
        // -----------------------------------------------------------------
        // Shared constants (ref\gpu_rgb.py)
        // -----------------------------------------------------------------
        internal const int PhasePeriod = 120;
        const int FanStart = 8;     // j_rainbow_1 stream start (canvas guard)
        const int FanLen = 40;      // sync-mode cap

        // Spotlight 12/13 slots (the GPU LED subject). The old board-ring
        // (0..5) and fan-ring (6..11) arrays were removed 2026-11-23 with
        // the final RING10 rebase patterns (31/32) — nothing references
        // them anymore.
        static readonly int[] SpotlightSlots = { 12, 13 };

        // LIVE LATCH MAP (re-probed 2026-11-21; ref\ring10-rebase-spec.md
        // section 2, ref\gpu_rgb.py LIVE LATCH MAP, ref\msi-led-slot-map.md
        // section 2): RING10 = packet slots 2..11 — 10 LEDs, period P10;
        // o'clock 12->10, 1->11, 2->2, 4->3, 5->4, 6->5, 7->6, 8->7, 10->8,
        // 11->9 (dark gaps at 3/9 o'clock). TOPFANS = slots 12/13 — top case
        // fans, painted every frame of every pattern (never black; floor
        // BEACON_BG (48,24,0) unless a pattern design says otherwise); NOT
        // ring slots. Slots 0/1 are live but individually dead -> pure black
        // in every pattern; 14..239 dead -> pure black.
        static readonly int[] Ring10 = { 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
        static readonly int[] TopFans = { 12, 13 };

        static readonly int[] DarkBlueC = { 33, 33, 140 };
        static readonly int[] DarkBlueDim = { 48, 24, 70 };   // dim dark blue for 12/13 on patterns 1/2 (>= BEACON_BG floor)
        static readonly int[][] PerfanC = new int[][]
        {
            new int[] { 255, 0, 0 }, new int[] { 0, 255, 0 },
            new int[] { 0, 0, 255 }, new int[] { 255, 255, 0 },
            new int[] { 255, 0, 255 }, new int[] { 0, 255, 255 },
            new int[] { 255, 255, 255 }, new int[] { 255, 128, 0 }
        };
        static readonly int[][] PaletteCool = new int[][]
        {
            new int[] { 0, 0, 255 }, new int[] { 255, 128, 0 }, new int[] { 255, 255, 0 }
        };
        static readonly int[][] PaletteWarm = new int[][]
        {
            new int[] { 255, 255, 0 }, new int[] { 255, 128, 0 }, new int[] { 255, 0, 0 }
        };

        static readonly int[] BeaconBase = { 255, 128, 0 };
        static readonly int[] BeaconArc = { 255, 255, 0 };

        static readonly int[] BeconBgC = { 48, 24, 0 };   // BEACON_BG
        static readonly int[] SweepHeadC = { 255, 255, 0 };
        static readonly int[][] SweepTailC = new int[][]
        {
            new int[] { 170, 127, 0 }, new int[] { 85, 63, 0 }, new int[] { 0, 0, 0 }
        };
        static readonly int[] TandemHeadC = { 255, 191, 0 };
        static readonly int[][] TandemTailC = new int[][]
        {
            new int[] { 215, 161, 0 }, new int[] { 127, 95, 0 },
            new int[] { 39, 29, 0 }, new int[] { 0, 0, 0 }
        };
        const int HeartbeatBreathPeriod = 60;
        static readonly int[] HeartbeatRing = { 255, 102, 0 };
        static readonly int[] HeartbeatHead = { 255, 255, 0 };
        static readonly int[] CometHeadC = { 255, 255, 0 };
        static readonly int[] CometHueStart = { 255, 191, 0 };
        static readonly int[] CometHueEnd = { 255, 102, 0 };
        static readonly int[] HazardHeadEven = { 255, 191, 0 };
        static readonly int[] HazardHeadOdd = { 255, 255, 0 };
        static readonly int[][] HazardTailC = new int[][]
        {
            new int[] { 255, 128, 0 }, new int[] { 153, 76, 0 },
            new int[] { 76, 38, 0 }, new int[] { 0, 0, 0 }
        };

        static readonly int[] BitclockOn = { 0, 255, 120 };
        static readonly int[] BitclockOff = { 10, 30, 18 };
        static readonly int[] BitclockBase = { 0, 20, 10 };
        static readonly int[] BitclockLap = { 0, 255, 160 };
        static readonly int[] BitclockTopBase = { 48, 30, 18 };   // 12/13 dim base: BITCLOCK_OFF floored to the BEACON_BG floor (>= (48,24,0))
        static readonly int[] TelegraphDot = { 255, 244, 214 };
        static readonly int[] TelegraphEcho = { 89, 85, 75 };

        static readonly int[] FireflyCore = { 255, 196, 87 };
        static readonly int[] FireflyBase = { 0, 4, 10 };
        // (base slot, drift amp, phase deg, off) — re-based 2026-11-21 onto
        // RING10: bases 3/5/7/9 so every fly stays within 2..11 at amp <= 1.25
        static readonly double[][] FireflyFlies = new double[][]
        {
            new double[] { 3.0, 1.0, 0, 0 },
            new double[] { 5.0, 1.25, 60, 25 },
            new double[] { 7.0, 1.25, 30, 50 },
            new double[] { 9.0, 1.0, 90, 75 }
        };

        static readonly int[] LavaBase = { 12, 6, 20 };
        const double LavaCenter = 6.5;                 // RING10 center (2..11)
        const double LavaAmp = 4.0;                   // blob drift amplitude (span 2.5..10.5)
        // (color, period, phase deg)
        static readonly int[][] LavaColors = new int[][]
        {
            new int[] { 255, 110, 40 }, new int[] { 255, 60, 150 }, new int[] { 255, 170, 60 }
        };
        static readonly int[] LavaPeriods = { 120, 60, 40 };
        static readonly int[] LavaPhis = { 0, 80, 40 };
        static readonly int[] LavaHead = { 255, 113, 83 };        // mean of the three blob colors — the 12/13 flare target (motion rule)
        static readonly int[] LavaTopBase = { 48, 24, 20 };       // 12/13 dim base: LAVA_BASE floored to the BEACON_BG floor (>= (48,24,0))

        static readonly int[] SonarRing = { 0, 220, 255 };
        static readonly int[] SonarBase = { 0, 6, 14 };
        static readonly int[] SonarPingSlots = { 6, 7 };
        const double SonarCenter = 6.5;

        static readonly int[] MetroMark = { 255, 214, 90 };
        static readonly int[] MetroAccent = { 255, 255, 255 };
        static readonly int[] MetroBase = { 16, 12, 6 };

        static readonly int[] OrbitPlanet = { 255, 255, 240 };
        static readonly int[] OrbitHalo = { 64, 64, 60 };
        static readonly int[] OrbitBase = { 16, 16, 14 };
        // (color, radius, phase deg)
        static readonly int[][] OrbitSatColors = new int[][]
        {
            new int[] { 0, 200, 255 }, new int[] { 255, 128, 0 }, new int[] { 160, 255, 160 }
        };
        static readonly double[] OrbitSatRadii = { 4.5, 3.15, 1.8 };
        static readonly int[] OrbitSatPhis = { 0, 240, 120 };

        static readonly int[] BitstreamOne = { 0, 255, 220 };
        static readonly int[] BitstreamDim = { 0, 40, 36 };
        static readonly int[] BitstreamZero = { 6, 16, 14 };
        static readonly int[] BitstreamLamp = { 0, 255, 220 };
        const string BitstreamPayload = "010111001011";   // fixed 12-bit payload

        static readonly int[] SparkBaseDots = { 7, 9, 11 };
        static readonly int[] SparkDot = { 80, 56, 8 };
        static readonly int[] SparkFlash = { 255, 255, 255 };
        static readonly int[] SparkShock = { 255, 200, 120 };
        static readonly double[] SparkShockLv = { 0.5, 0.3, 0.15 };
        static readonly int[] SparkA = { 255, 150, 0 };
        static readonly int[] SparkB = { 255, 60, 20 };
        static readonly int[] SparkFuse = { 255, 191, 0 };
        static readonly int[] SparkBursts = { 7, 9, 11 };

        static readonly int[] EchoCore = { 255, 255, 255 };
        static readonly int[] EchoG1 = { 127, 95, 40 };
        static readonly int[] EchoG2 = { 63, 30, 0 };

        static readonly int[] EcgR = { 255, 255, 255 };
        static readonly int[] EcgQ = { 0, 60, 40 };
        static readonly int[] EcgT = { 0, 72, 48 };
        const double EcgPeakU = 12.0;
        const double EcgSpacing = 30.0;

        static readonly int[] WalkBody = { 120, 255, 120 };
        static readonly int[] WalkHead = { 255, 255, 120 };
        static readonly int[] WalkHeadBlink = { 51, 51, 31 };
        static readonly int[] WalkHeadLook = { 255, 255, 255 };
        const int WalkBlinkLo = 20, WalkBlinkHi = 27;
        const int WalkWalkEnd = 45;
        const int WalkLookEnd = 60;
        const int WalkBackEnd = 105;

        static readonly int[][] SpotIdlePalette = new int[][]
        {
            new int[] { 0, 64, 255 }, new int[] { 0, 224, 128 }, new int[] { 128, 0, 255 }
        };
        static readonly int[][] SpotFirePalette = new int[][]
        {
            new int[] { 255, 32, 0 }, new int[] { 255, 144, 0 }, new int[] { 255, 224, 0 }
        };
        const int SpotFirePeriod = 15;
        static readonly int[][] SpotActivePalette = new int[][]
        {
            new int[] { 255, 224, 0 }, new int[] { 255, 255, 255 }, new int[] { 255, 144, 0 }
        };
        const int SpotActivePeriod = 30;

        static readonly int[] RedWhiteRed = { 255, 0, 0 };
        static readonly int[] RedWhiteWhite = { 255, 255, 255 };

        static readonly int[][] RainbowColors = new int[][]
        {
            new int[] { 255, 0, 0 },     // wheel 0 red
            new int[] { 255, 160, 0 },   // wheel 1 orange
            new int[] { 255, 240, 0 },   // wheel 2 yellow
            new int[] { 0, 220, 0 },     // wheel 3 green
            new int[] { 0, 80, 255 },    // wheel 4 blue
            new int[] { 160, 0, 255 }    // wheel 5 violet
        };
        const int RainbowRotFrames = 120;
        const int RainbowStepFrames = 20;
        const int RainbowHold = 12;
        const int RainbowFade = 8;

        const int LimelightPeriod = 60;
        static readonly int[] LimelightGpuFloor = { 90, 60, 20 };
        static readonly int[] LimelightGpuTop = { 255, 255, 250 };
        static readonly int[] LimelightFanFloor = { 8, 12, 40 };
        static readonly int[] LimelightFanTop = { 30, 48, 140 };
        static readonly double[] LimelightBreath = new double[60];

        static readonly int[] CatwalkBase = { 70, 12, 60 };
        static readonly int[] CatwalkHead = { 255, 255, 250 };
        static readonly int[] CatwalkTail1 = { 255, 90, 190 };
        static readonly int[] CatwalkTail2 = { 150, 30, 110 };
        static readonly int[] CatwalkPhotoBase = { 70, 70, 62 };
        static readonly int[] CatwalkPhotoFlash = { 255, 255, 250 };
        static readonly int[] CatwalkPhotoAfter = { 150, 150, 145 };

        static readonly int[] SnoNightStar = { 89, 61, 39 };
        static readonly int[] SnoStarTop = { 255, 245, 225 };
        static readonly int[] SnoNightFan = { 10, 14, 40 };
        static readonly int[] SnoFanBright = { 40, 60, 160 };
        static readonly int[] SnoFlash = { 255, 255, 250 };
        static readonly int[] SnoFireball = { 255, 205, 90 };
        static readonly int[] SnoShockHot = { 255, 210, 70 };
        static readonly int[] SnoShockCool = { 210, 80, 30 };
        static readonly int[] SnoEmberHot = { 90, 44, 18 };
        static readonly int[] SnoEmberCool = { 50, 22, 10 };
        static readonly int[] SnoRemnantDim = { 120, 110, 100 };
        static readonly int[] SnoRemnantBright = { 255, 255, 250 };
        const int SnoBuildEnd = 39;
        const int SnoDetFlashEnd = 41;
        const int SnoDetEnd = 45;
        const int SnoShockStart = 46;
        const int SnoShockEnd = 85;
        const int SnoAfterStart = 86;
        const int SnoFadeStart = 76;

        static readonly int[] InfernoFanDark = { 120, 24, 6 };
        static readonly int[] InfernoFanHot = { 255, 130, 24 };
        static readonly int[] InfernoTopBase = { 255, 170, 30 };
        static readonly int[] InfernoTopWhite = { 255, 255, 250 };
        const double InfernoSparkLevel = 0.82;
        const int InfernoPeriod = 60;
        static readonly double[] InfernoBreath = new double[60];
        static readonly double[] InfernoFlicker = new double[120];

        static readonly int[] ReactorFanDark = { 8, 16, 70 };
        static readonly int[] ReactorFanBright = { 36, 96, 230 };
        static readonly int[] ReactorTopBase = { 150, 235, 255 };
        static readonly int[] ReactorTopFlash = { 255, 255, 250 };
        const int ReactorPeriod = 60;
        static readonly double[] ReactorBreath = new double[60];
        static readonly double[] ReactorPulse = new double[60];

        static Patterns()
        {
            // 60-entry sine table (0..1), i in 0..59 (limelight star breath)
            for (int i = 0; i < 60; i++)
                LimelightBreath[i] = 0.5 + 0.5 * Math.Sin(2.0 * Math.PI * i / LimelightPeriod);
            // 60-entry 0..1 tables: i=0 trough, i=30 peak
            for (int i = 0; i < 60; i++)
            {
                InfernoBreath[i] = 0.5 - 0.5 * Math.Cos(2.0 * Math.PI * i / InfernoPeriod);
                ReactorBreath[i] = 0.5 - 0.5 * Math.Cos(2.0 * Math.PI * i / ReactorPeriod);
                ReactorPulse[i] = i <= 12
                    ? 0.5 + 0.5 * Math.Cos(Math.PI * (i - 6.0) / 6.0)
                    : 0.0;
            }
            // deterministic pseudo-random flame flicker: 120 entries in 0..1
            // (fixed-seed LCG, bit-exact)
            long s = 31;
            for (int i = 0; i < 120; i++)
            {
                s = (1103515245L * s + 12345L) & 0x7FFFFFFFL;
                InfernoFlicker[i] = ((s >> 16) & 0x7FFFL) / 32767.0;
            }
        }

        // -----------------------------------------------------------------
        // Math helpers (exact ports of the ref\gpu_rgb.py helpers)
        // -----------------------------------------------------------------
        // Python % is floored modulo: -1 % 6 == 5 (C# % truncates).
        static int ModFloor(int a, int n)
        {
            int r = a % n;
            if (r < 0) r += n;
            return r;
        }

        static double ModFloorD(double a, double n)
        {
            double r = a % n;
            if (r < 0) r += n;
            return r;
        }

        static void SetSlot(byte[] o, int i, int[] c)
        {
            o[3 * i] = (byte)c[0];
            o[3 * i + 1] = (byte)c[1];
            o[3 * i + 2] = (byte)c[2];
        }

        static void FillRange(byte[] o, int lo, int hi, int[] c)
        {
            for (int i = lo; i <= hi; i++)
                SetSlot(o, i, c);
        }

        // Additive (cap 255) paint of one slot; ignores out-of-canvas slots.
        static void AddSlot(byte[] o, int i, int r, int g, int b)
        {
            if (!(i >= 0 && i < FanStart + FanLen)) return;
            o[3 * i] = (byte)Math.Min(255, o[3 * i] + r);
            o[3 * i + 1] = (byte)Math.Min(255, o[3 * i + 1] + g);
            o[3 * i + 2] = (byte)Math.Min(255, o[3 * i + 2] + b);
        }

        static double Smoothstep(double x)
        {
            x = Math.Max(0.0, Math.Min(1.0, x));
            return x * x * (3.0 - 2.0 * x);
        }

        // Triangular falloff: 1.0 at 0, 0.5 at 1, 0 at 2.
        static double Tri(double d)
        {
            return Math.Max(0.0, 1.0 - Math.Abs(d) / 2.0);
        }

        // 6-slot raised-cosine bump: full at |d|<=2, 0 at |d|>=4.
        static double LavaBump(double d)
        {
            d = Math.Abs(d);
            if (d <= 2) return 1.0;
            if (d < 4) return 0.5 + 0.5 * Math.Cos(Math.PI * (d - 2.0) / 2.0);
            return 0.0;
        }

        // Raised-cosine envelope: 0->1->0 over 60 frames, 0 for the rest.
        static double FireflyEnv(int t, int off)
        {
            int u = ModFloor(t - off, PhasePeriod);
            if (u >= 60) return 0.0;
            return 0.5 + 0.5 * Math.Cos(Math.PI * (u - 30.0) / 30.0);
        }

        // Lerp (r,g,b) a -> b by t (clamped 0..1), int-truncated.
        static int[] Slerp(int[] a, int[] b, double t)
        {
            t = Math.Max(0.0, Math.Min(1.0, t));
            return new int[]
            {
                (int)(a[0] + (b[0] - a[0]) * t),
                (int)(a[1] + (b[1] - a[1]) * t),
                (int)(a[2] + (b[2] - a[2]) * t)
            };
        }

        // Per-channel max blend of a slot color over the dim amber base.
        static int[] OnBase(int[] c)
        {
            return new int[]
            {
                Math.Max(BeconBgC[0], c[0]),
                Math.Max(BeconBgC[1], c[1]),
                Math.Max(BeconBgC[2], c[2])
            };
        }

        // Slot k (1..n-2) of the COMET tail on an n-slot ring (ref
        // _comet_tail, re-based 2026-11-21): smoothstep ease-out
        // brightness with a hue ramp amber (255,191,0) -> deep orange
        // (255,102,0).
        static int[] CometTail(int k, int n)
        {
            double s = 1.0 - k / (n + 1.0);
            double brightness = s * s * (3.0 - 2.0 * s);
            double u = k / (double)n;
            double gRamp = CometHueStart[1] - (CometHueStart[1] - CometHueEnd[1]) * u;
            return new int[] { (int)(255 * brightness), (int)(gRamp * brightness), 0 };
        }

        // Loop `palette` over `period` frames, linear per-channel lerp
        // between anchors.
        static int[] SpotColor(int[][] palette, int t, int period)
        {
            int n = palette.Length;
            double x = (t % period) / (double)period * n;
            int k = (int)x;
            double f = x - k;
            int[] a = palette[k];
            int[] b = palette[(k + 1) % n];
            return new int[]
            {
                (int)(a[0] + (b[0] - a[0]) * f),
                (int)(a[1] + (b[1] - a[1]) * f),
                (int)(a[2] + (b[2] - a[2]) * f)
            };
        }

        // Full round trip in 20 frames: 2 -> 11 -> 2 (u in 0..19).
        static double EchoPos(int u)
        {
            if (u < 10) return 2.0 + 9.0 * (u / 10.0);
            return 11.0 - 9.0 * ((u - 10) / 10.0);
        }

        // RING10 position: burst slot b plus distance d, wrapping the
        // 10-slot standard ring 2..11 (d is signed).
        static int SparkPos(int b, int d)
        {
            return 2 + ModFloor(b - 2 + d, 10);
        }

        // Color of wheel slot s (0..5) at phase t (0..119).
        static int[] RainbowWheelColor(int s, int t)
        {
            int m = ModFloor(s * RainbowStepFrames - t, RainbowRotFrames);
            int k = m / RainbowStepFrames;
            double f = (m % RainbowStepFrames) / (double)RainbowStepFrames;
            int[] a = RainbowColors[k];
            int[] b = RainbowColors[(k + 1) % RainbowColors.Length];
            return new int[]
            {
                (int)(a[0] + (b[0] - a[0]) * f),
                (int)(a[1] + (b[1] - a[1]) * f),
                (int)(a[2] + (b[2] - a[2]) * f)
            };
        }

        // Slots 12/13 color at phase t: six wheel colors one at a time —
        // 12-frame hold + 8-frame fade per color.
        static int[] RainbowCycle(int t)
        {
            int step = t / RainbowStepFrames;
            int local = t % RainbowStepFrames;
            int[] a = RainbowColors[step % RainbowColors.Length];
            if (local < RainbowHold) return a;
            int[] b = RainbowColors[(step + 1) % RainbowColors.Length];
            double f = (local - RainbowHold) / (double)RainbowFade;
            return new int[]
            {
                (int)(a[0] + (b[0] - a[0]) * f),
                (int)(a[1] + (b[1] - a[1]) * f),
                (int)(a[2] + (b[2] - a[2]) * f)
            };
        }

        // One slot of the 3-color comet chase on RING10 (ref chase(),
        // re-based 2026-11-21): three 2-slot color blocks (the palette) +
        // 4 dim BEACON_BG base slots, period 10 (0.5 s/lap), +i, 1
        // slot/frame. s = ring index 0..9. (The old 12-slot/8-latch
        // ChaseRing helper is retired with the old slot model.)
        static int[] ChaseSlot(int s, int t, int[][] palette)
        {
            int j = ModFloor(s - t, 10);
            if (j == 0 || j == 1) return palette[0];
            if (j == 3 || j == 4) return palette[1];
            if (j == 6 || j == 7) return palette[2];
            return BeconBgC;
        }

        // TOPFANS 12/13 color for patterns 3/4 (ref _chase_topfan, spec
        // 3.2 motion rule, re-based 2026-11-21): dim BEACON_BG base; flare
        // toward a block's palette color when a 2-slot block is on slot 10
        // (f=1.0) or 9/11 (f=0.5); f = max over the three blocks. The
        // blend is floored to the dim base per channel (OnBase) so 12/13
        // never dip below the floor.
        static int[] ChaseTopfan(int t, int[][] palette)
        {
            double f = 0.0;
            int[] color = BeconBgC;
            for (int k = 0; k < 3; k++)
            {
                int sa = Ring10[ModFloor(k * 3 + t, 10)];
                int sb = Ring10[ModFloor(k * 3 + 1 + t, 10)];
                if (sa == 10 || sb == 10)
                {
                    f = 1.0; color = palette[k];
                }
                else if (f < 0.5 && (sa == 9 || sb == 9 || sa == 11 || sb == 11))
                {
                    f = 0.5; color = palette[k];
                }
            }
            int[] blend = new int[]
            {
                (int)(BeconBgC[0] + f * (color[0] - BeconBgC[0])),
                (int)(BeconBgC[1] + f * (color[1] - BeconBgC[1])),
                (int)(BeconBgC[2] + f * (color[2] - BeconBgC[2]))
            };
            return OnBase(blend);
        }

        // -----------------------------------------------------------------
        // Pattern 1 darkblue (re-based 2026-11-21 to RING10 + TOPFANS):
        // static dark blue on the standard ring RING10 (slots 2..11);
        // TOPFANS 12/13 hold a static dim dark blue (>= BEACON_BG floor);
        // slots 0..1 (live, unassigned) and 14..239 (dead) stay pure
        // black. Static (phase-independent).
        // -----------------------------------------------------------------
        public static byte[] DarkBlueFrame(int phase)
        {
            byte[] o = new byte[720];
            for (int j = 0; j < Ring10.Length; j++)
                SetSlot(o, Ring10[j], DarkBlueC);
            for (int j = 0; j < TopFans.Length; j++)
                SetSlot(o, TopFans[j], DarkBlueDim);
            return o;
        }

        // -----------------------------------------------------------------
        // Pattern 2 perfan (re-based 2026-11-21 to RING10 + TOPFANS): the
        // 6-color ring recast on RING10 — 10 slots, the 6-color sequence
        // repeats (ring slot s shows PERFAN[(s - ph) % 6]), +i, 1 slot/frame;
        // pattern period 30 (LCM(10, 6) | 120). TOPFANS 12/13 hold a static
        // dim blue (>= BEACON_BG floor); slots 0..1 (live, unassigned) and
        // 14..239 (dead) stay pure black.
        // -----------------------------------------------------------------
        public static byte[] PerfAnFrame(int phase)
        {
            int t = phase % PhasePeriod;
            byte[] o = new byte[720];
            for (int s = 0; s < Ring10.Length; s++)
                SetSlot(o, Ring10[s], PerfanC[ModFloor(s - t, 6)]);
            for (int j = 0; j < TopFans.Length; j++)
                SetSlot(o, TopFans[j], DarkBlueDim);
            return o;
        }

        // -----------------------------------------------------------------
        // Pattern 3 chase_cool (re-based 2026-11-21 to RING10 + TOPFANS):
        // a 3-color comet chase (blue/amber/yellow) on the standard ring
        // RING10 — three 2-slot color blocks + 4 dim base slots, +i, 1
        // slot/frame, period 10 (| 120). TOPFANS 12/13 hold the dim amber
        // base and flare toward a block's color when a block is on slot 10
        // (f=1.0) or 9/11 (f=0.5) — spec 3.2 motion rule, f = max over the
        // three blocks. Slots 0..1 (live, unassigned) and 14..239 (dead)
        // stay pure black. The old 'both rings' (board 0..5 + fan 6..11)
        // version is retired: single RING10.
        // -----------------------------------------------------------------
        public static byte[] ChaseCoolFrame(int phase)
        {
            int t = phase % PhasePeriod;
            byte[] o = new byte[720];
            for (int s = 0; s < Ring10.Length; s++)
                SetSlot(o, Ring10[s], ChaseSlot(s, t, PaletteCool));
            int[] tf = ChaseTopfan(t, PaletteCool);
            for (int j = 0; j < TopFans.Length; j++)
                SetSlot(o, TopFans[j], tf);
            return o;
        }

        // -----------------------------------------------------------------
        // Pattern 4 chase_warm (re-based 2026-11-21 to RING10 + TOPFANS):
        // same 3-color comet chase as pattern 3 but the warm palette
        // (yellow/amber/red) on the standard ring RING10 — three 2-slot
        // color blocks + 4 dim base slots, +i, 1 slot/frame, period 10
        // (| 120); TOPFANS 12/13 dim amber base + flare (f = max over the
        // three blocks); slots 0..1 and 14..239 stay pure black.
        // -----------------------------------------------------------------
        public static byte[] ChaseWarmFrame(int phase)
        {
            int t = phase % PhasePeriod;
            byte[] o = new byte[720];
            for (int s = 0; s < Ring10.Length; s++)
                SetSlot(o, Ring10[s], ChaseSlot(s, t, PaletteWarm));
            int[] tf = ChaseTopfan(t, PaletteWarm);
            for (int j = 0; j < TopFans.Length; j++)
                SetSlot(o, TopFans[j], tf);
            return o;
        }

        // -----------------------------------------------------------------
        // Pattern 5 beacon (re-based 2026-11-21 to RING10 + TOPFANS): one
        // 2-slot yellow beacon bulb with a 1-slot fading tail (0.5, behind)
        // spinning on RING10 (P10, +i, head leading) over a steady orange
        // ring. TOPFANS 12/13 hold the dim orange base and flare toward
        // the yellow bulb when the bulb is on slot 10 (f=1.0) or 9/11
        // (f=0.5) — spec 3.2 motion rule. Slots 0..1 (live, unassigned)
        // and 14..239 (dead) stay pure black. Period 10 | 120. The old
        // two-ring (board 0..5 + fan 6..11) version is retired: single
        // RING10.
        // -----------------------------------------------------------------
        public static byte[] BeaconFrame(int phase)
        {
            int t = phase % PhasePeriod;
            byte[] o = new byte[720];
            for (int j = 0; j < Ring10.Length; j++)
                SetSlot(o, Ring10[j], BeaconBase);
            for (int kk = 0; kk < 3; kk++)
            {
                int k = kk == 0 ? 0 : (kk == 1 ? 1 : -1);
                double w = kk == 2 ? 0.5 : 1.0;
                int i = Ring10[ModFloor(t + k, 10)];
                int[] c = new int[]
                {
                    (int)(BeaconBase[0] + (BeaconArc[0] - BeaconBase[0]) * w),
                    (int)(BeaconBase[1] + (BeaconArc[1] - BeaconBase[1]) * w),
                    (int)(BeaconBase[2] + (BeaconArc[2] - BeaconBase[2]) * w)
                };
                SetSlot(o, i, c);
            }
            // TOPFANS: dim orange base + f-blend flare toward the bulb
            // (spec 3.2 motion rule); the bulb is brighter than the base on
            // every channel, so the blend never dips below the floor.
            int b0 = Ring10[t % 10];
            int b1 = Ring10[ModFloor(t + 1, 10)];
            double f = (b0 == 10 || b1 == 10)
                ? 1.0
                : ((b0 == 9 || b1 == 9 || b0 == 11 || b1 == 11) ? 0.5 : 0.0);
            int[] topfan = new int[]
            {
                (int)(BeconBgC[0] + f * (BeaconArc[0] - BeconBgC[0])),
                (int)(BeconBgC[1] + f * (BeaconArc[1] - BeconBgC[1])),
                (int)(BeconBgC[2] + f * (BeaconArc[2] - BeconBgC[2]))
            };
            for (int j = 0; j < TopFans.Length; j++)
                SetSlot(o, TopFans[j], topfan);
            return o;
        }

        // -----------------------------------------------------------------
        // Pattern 6 sweep — THE TEMPLATE (re-based 2026-11-21 to RING10 +
        // TOPFANS): one 1-slot yellow radar dot on RING10 (slots 2..11,
        // P10, 0.5 s/rev), 1 live latch/frame, +i, with a 3-slot linear
        // amber afterglow wrapping the ring; TOPFANS slots 12/13 hold the
        // dim amber sweep base and flare toward the head color when the
        // head crosses slot 10 (f=1.0) or 9/11 (f=0.5) — spec 3.2 motion
        // rule, f=0 otherwise. Slots 0..1 (live, unassigned) and 14..239
        // (dead) stay pure black. Period 10 | 120.
        // -----------------------------------------------------------------
        public static byte[] SweepFrame(int phase)
        {
            int t = phase % PhasePeriod;
            int ph = t / 1;
            byte[] o = new byte[720];
            for (int j = 0; j < Ring10.Length; j++)
                SetSlot(o, Ring10[j], BeconBgC);
            for (int k = 1; k <= SweepTailC.Length; k++)
                SetSlot(o, Ring10[ModFloor(ph - k, 10)], OnBase(SweepTailC[k - 1]));
            int headSlot = Ring10[ph % 10];
            SetSlot(o, headSlot, SweepHeadC);
            // TOPFANS: dim sweep base + f-blend flare (spec 3.2 motion
            // rule): f=1.0 when the head is on slot 10, f=0.5 on slot 9
            // or 11, f=0 otherwise; topfan = base + f * (head - base)
            // per channel, int-truncated.
            double f = headSlot == 10 ? 1.0 : (headSlot == 9 || headSlot == 11) ? 0.5 : 0.0;
            int[] topfan = new int[]
            {
                (int)(BeconBgC[0] + f * (SweepHeadC[0] - BeconBgC[0])),
                (int)(BeconBgC[1] + f * (SweepHeadC[1] - BeconBgC[1])),
                (int)(BeconBgC[2] + f * (SweepHeadC[2] - BeconBgC[2]))
            };
            for (int j = 0; j < TopFans.Length; j++)
                SetSlot(o, TopFans[j], topfan);
            return o;
        }

        // -----------------------------------------------------------------
        // Pattern 7 tandem (re-based 2026-11-21 to RING10 + TOPFANS): two
        // amber bulbs (2-slot heads, 4-slot smoothstep tails) racing each
        // other on the standard ring RING10 in opposite directions, 180
        // deg apart at phase 0: head A +i, head B -i, 1 slot/frame (P10,
        // 0.5 s/rev); per-channel max blend, head leading, tail trailing.
        // The old board 0..5 + fan 6..11 double ring is retired: 0/1 go
        // black. TOPFANS 12/13 hold the dim amber base and flare toward
        // the head color, f = max over both bulbs (1.0 on slot 10, 0.5
        // on 9/11, spec 3.2 motion rule). Period 10 | 120.
        // -----------------------------------------------------------------
        public static byte[] TandemFrame(int phase)
        {
            int t = phase % PhasePeriod;
            byte[] o = new byte[720];
            int n = Ring10.Length;
            int half = n / 2;
            for (int j = 0; j < n; j++)
                SetSlot(o, Ring10[j], BeconBgC);
            for (int s = 0; s < n; s++)
            {
                int i = Ring10[s];
                int kA = ModFloor(s - t, n);
                int kB = ModFloor(s + t - half, n);
                int[] ca;
                if (kA <= 1) ca = TandemHeadC;
                else if (n - 4 <= kA && kA <= n - 1) ca = TandemTailC[n - 1 - kA];
                else ca = BeconBgC;
                int[] cb;
                if (kB <= 1) cb = TandemHeadC;
                else if (kB <= 1 + TandemTailC.Length) cb = TandemTailC[kB - 2];
                else cb = BeconBgC;
                int[] c = new int[]
                {
                    Math.Max(ca[0], cb[0]),
                    Math.Max(ca[1], cb[1]),
                    Math.Max(ca[2], cb[2])
                };
                SetSlot(o, i, OnBase(c));
            }
            // TOPFANS: dim amber base + f-blend flare, f = max over both
            // bulbs (spec 3.2 motion rule): bulb A on RING10[t]..[t+1],
            // bulb B 180 deg apart; 1.0 on slot 10, 0.5 on 9/11.
            double f = 0.0;
            int[] bulbA = new int[] { Ring10[t % n], Ring10[ModFloor(t + 1, n)] };
            int[] bulbB = new int[] { Ring10[ModFloor(half - t, n)], Ring10[ModFloor(half - t + 1, n)] };
            int[][] bulbs = new int[][] { bulbA, bulbB };
            for (int b = 0; b < bulbs.Length; b++)
            {
                int[] bulb = bulbs[b];
                if (bulb[0] == 10 || bulb[1] == 10) f = Math.Max(f, 1.0);
                else if (bulb[0] == 9 || bulb[1] == 9 || bulb[0] == 11 || bulb[1] == 11) f = Math.Max(f, 0.5);
            }
            int[] topfan = new int[]
            {
                (int)(BeconBgC[0] + f * (TandemHeadC[0] - BeconBgC[0])),
                (int)(BeconBgC[1] + f * (TandemHeadC[1] - BeconBgC[1])),
                (int)(BeconBgC[2] + f * (TandemHeadC[2] - BeconBgC[2]))
            };
            for (int j = 0; j < TopFans.Length; j++)
                SetSlot(o, TopFans[j], topfan);
            return o;
        }

        // -----------------------------------------------------------------
        // Pattern 8 heartbeat (re-based 2026-11-21 to RING10 + TOPFANS):
        // a deep-orange ring that breathes (60-frame / 3 s period, floor
        // 0.3 so the ring dims to (76,30,0), never black) while a sharp
        // 1-slot yellow dot laps the standard ring RING10 (+i, P10,
        // 0.5 s/rev) with a 1-slot yellow->ring blend behind it. The old
        // board 0..5 + fan 6..11 double ring is retired: 0/1 go black.
        // TOPFANS 12/13 breathe in phase with the ring (spec 3.2
        // breathing rule). Periods 60 | 120 (breath), 10 | 120 (dot).
        // -----------------------------------------------------------------
        public static byte[] HeartbeatFrame(int phase)
        {
            int t = phase % PhasePeriod;
            double breath = 0.65 + 0.35 * Math.Sin(2.0 * Math.PI * t
                                                    / HeartbeatBreathPeriod);
            int[] ring = new int[]
            {
                (int)(HeartbeatRing[0] * breath),
                (int)(HeartbeatRing[1] * breath),
                0
            };
            byte[] o = new byte[720];
            for (int j = 0; j < Ring10.Length; j++)
                SetSlot(o, Ring10[j], ring);
            int i = Ring10[t % 10];
            SetSlot(o, i, HeartbeatHead);
            // slot behind the head blends yellow -> current ring color
            i = Ring10[ModFloor(t - 1, 10)];
            int[] c = new int[]
            {
                (255 + ring[0]) / 2,
                (255 + ring[1]) / 2,
                0
            };
            SetSlot(o, i, c);
            // TOPFANS: breathe in phase with the ring (spec 3.2)
            for (int j = 0; j < TopFans.Length; j++)
                SetSlot(o, TopFans[j], ring);
            return o;
        }

        // -----------------------------------------------------------------
        // Pattern 9 comet (re-based 2026-11-21 to RING10 + TOPFANS): a
        // 2-slot yellow bulb trailing a smoothstep gradient tail that
        // fills its own ring, -i direction, dim amber base: the standard
        // ring RING10 (2 head + 8 tail, P10, 0.5 s/rev). The old board
        // 0..5 + fan 6..11 double ring is retired: 0/1 go black. TOPFANS
        // 12/13 hold the dim amber base and flare toward the yellow bulb
        // (f=1.0 bulb on slot 10, 0.5 on 9/11, spec 3.2 motion rule).
        // The whole ring is lit every frame — the comet never leaves the
        // ring, so there is never a dark frame. Period 10 | 120.
        // -----------------------------------------------------------------
        public static byte[] CometFrame(int phase)
        {
            int t = phase % PhasePeriod;
            byte[] o = new byte[720];
            int n = Ring10.Length;
            for (int j = 0; j < n; j++)
                SetSlot(o, Ring10[j], BeconBgC);
            int pos = ModFloor(-t, n);              // -i: head leading edge
            for (int k = 0; k < n; k++)
            {
                int i = Ring10[ModFloor(pos - k, n)];   // k 0..1 head, rest tail
                int[] c;
                if (k <= 1) c = CometHeadC;
                else c = CometTail(k - 1, n);           // k-1 = 1..n-2
                SetSlot(o, i, OnBase(c));
            }
            // TOPFANS: dim amber base + f-blend flare toward the yellow
            // bulb (spec 3.2 motion rule)
            int[] bulb = new int[] { Ring10[pos], Ring10[ModFloor(pos - 1, n)] };
            double f = (bulb[0] == 10 || bulb[1] == 10) ? 1.0
                : ((bulb[0] == 9 || bulb[1] == 9 || bulb[0] == 11 || bulb[1] == 11) ? 0.5 : 0.0);
            int[] topfan = new int[]
            {
                (int)(BeconBgC[0] + f * (CometHeadC[0] - BeconBgC[0])),
                (int)(BeconBgC[1] + f * (CometHeadC[1] - BeconBgC[1])),
                (int)(BeconBgC[2] + f * (CometHeadC[2] - BeconBgC[2]))
            };
            for (int j = 0; j < TopFans.Length; j++)
                SetSlot(o, TopFans[j], topfan);
            return o;
        }

        // -----------------------------------------------------------------
        // Pattern 10 hazard (re-based 2026-11-21 to RING10 + TOPFANS): a
        // 2-slot head strobing at 10 Hz (frame parity: even = amber, odd
        // = pure yellow) trailing a blocky 4-slot quantized orange tail,
        // on the standard ring RING10 (+i, P10, 0.5 s/rev), dim amber
        // base. The old board 0..5 + fan 6..11 double ring is retired:
        // 0/1 go black. TOPFANS 12/13 strobe in sync with the head (spec
        // 3.2 strobe rule: even frame bright amber, odd frame dim amber
        // base; floor = BEACON_BG, never black). The blink is phase-
        // parity (120 is even), independent of the rotation period.
        // Period 10 | 120.
        // -----------------------------------------------------------------
        public static byte[] HazardFrame(int phase)
        {
            int t = phase % PhasePeriod;
            byte[] o = new byte[720];
            int n = Ring10.Length;
            int[] head = (t % 2 == 0) ? HazardHeadEven : HazardHeadOdd;
            for (int j = 0; j < n; j++)
                SetSlot(o, Ring10[j], BeconBgC);
            for (int k = 0; k < 6; k++)
            {
                int i = Ring10[ModFloor(t + 1 - k, n)];   // k0/1 head, k2..5 tail
                int[] c = (k <= 1) ? head : HazardTailC[k - 2];
                SetSlot(o, i, OnBase(c));
            }
            // TOPFANS: strobe in sync (spec 3.2 strobe rule): even frame
            // bright amber, odd frame dim base — floor = BEACON_BG, never
            // black.
            int[] topfan = (t % 2 == 0) ? HazardHeadEven : BeconBgC;
            for (int j = 0; j < TopFans.Length; j++)
                SetSlot(o, TopFans[j], topfan);
            return o;
        }

        // -----------------------------------------------------------------
        // Pattern 11 bitclock (re-based 2026-11-21 to RING10 + TOPFANS): a
        // 6-bit binary counter lights RING10 slots 2..7 (bit i lights when
        // (t >> i) & 1, bit i -> slot 2+i). Ring slots 8..11 hold the dim
        // baseline and flash the green lap indicator (BITCLOCK_LAP) that
        // decays over 15 frames after each 120-frame wrap. The board-ring
        // counter 0..5 is retired (0/1 go black). TOPFANS 12/13 breathe in
        // phase with the lap flash (spec 3.2 breathing rule): the dim base
        // blended toward the lap color, floored to the BEACON_BG floor
        // (>= (48,24,0)); slots 14..239 (dead) stay pure black. Period
        // 120 | 120.
        // -----------------------------------------------------------------
        public static byte[] BitclockFrame(int phase)
        {
            int t = phase % PhasePeriod;
            byte[] o = new byte[720];
            for (int i = 0; i < 6; i++)
            {
                int[] c = (((t >> i) & 1) != 0) ? BitclockOn : BitclockOff;
                SetSlot(o, 2 + i, c);
            }
            double lap = 1.0 - Smoothstep(t / 15.0);
            for (int i = 8; i <= 11; i++)
            {
                AddSlot(o, i, BitclockBase[0], BitclockBase[1], BitclockBase[2]);
                AddSlot(o, i, (int)(BitclockLap[0] * lap),
                    (int)(BitclockLap[1] * lap), (int)(BitclockLap[2] * lap));
            }
            // TOPFANS: breathe in phase with the lap (spec 3.2): dim base ->
            // lap color, per-channel int blend, floored to BEACON_BG
            int[] top = OnBase(new int[]
            {
                (int)(BitclockTopBase[0] + lap * (BitclockLap[0] - BitclockTopBase[0])),
                (int)(BitclockTopBase[1] + lap * (BitclockLap[1] - BitclockTopBase[1])),
                (int)(BitclockTopBase[2] + lap * (BitclockLap[2] - BitclockTopBase[2]))
            });
            for (int j = 0; j < TopFans.Length; j++)
                SetSlot(o, TopFans[j], top);
            return o;
        }

        // -----------------------------------------------------------------
        // Pattern 12 telegraph (re-based 2026-11-21 to RING10 + TOPFANS):
        // one ring LED (slot 4, now a RING10 position) transmits Morse "H"
        // (4 dots at a 15-frame unit: on-15/off-15 x4, the final off is the
        // 15-frame tail) as warm white (255,244,214); the whole standard
        // ring RING10 echoes it 10 frames late at 35% warm white (89,85,75)
        // over a dim BEACON_BG base (the old pure-black baseline is gone —
        // no pure black on the ring). The dot overwrites slot 4 even during
        // the echo. The old fan ring 6..11 echo is retired (0/1 go black).
        // TOPFANS 12/13 strobe between the dim base and the echo color in
        // sync with the echo (spec 3.2 strobe rule: floor = base, never
        // black); slots 14..239 (dead) stay pure black. Period 30 | 120.
        // -----------------------------------------------------------------
        public static byte[] TelegraphFrame(int phase)
        {
            int t = phase % PhasePeriod;
            byte[] o = new byte[720];
            for (int j = 0; j < Ring10.Length; j++)
                SetSlot(o, Ring10[j], BeconBgC);
            bool echo = ModFloor(t - 10, 30) < 15;          // echo, 10 frames late
            if (echo)
                for (int j = 0; j < Ring10.Length; j++)
                    SetSlot(o, Ring10[j], TelegraphEcho);
            for (int j = 0; j < TopFans.Length; j++)
                SetSlot(o, TopFans[j], echo ? TelegraphEcho : BeconBgC);
            if (t % 30 < 15)                                 // transmitter: 4 dots
                SetSlot(o, 4, TelegraphDot);
            return o;
        }

        // -----------------------------------------------------------------
        // Pattern 13 fireflies (re-based 2026-11-21 to RING10 + TOPFANS):
        // four warm amber points drift slowly on the standard ring RING10
        // (pos_k = base_k + amp_k*sin(2pi*t/120 + phi_k); bases 3/5/7/9 so
        // every fly stays within 2..11 with amp <= 1.25), each fading in
        // and out on a staggered 60-on/60-off envelope (raised-cosine);
        // 3-slot falloff (full at 0, 50% at 1, 0 at 2); near-black base
        // (0,4,10). The board-ring fifth fly (onboard slot 3) is retired
        // (0/1 go black). TOPFANS 12/13 breathe in phase with the flies
        // (spec 3.2 breathing rule): the dim BEACON_BG base blended toward
        // the fly core by the max envelope across the four flies, floored
        // to BEACON_BG; slots 14..239 (dead) stay pure black. Period
        // 120 | 120.
        // -----------------------------------------------------------------
        public static byte[] FirefliesFrame(int phase)
        {
            int t = phase % PhasePeriod;
            byte[] o = new byte[720];
            for (int j = 0; j < Ring10.Length; j++)
                SetSlot(o, Ring10[j], FireflyBase);
            double maxEnv = 0.0;
            for (int f = 0; f < FireflyFlies.Length; f++)
            {
                double baseSlot = FireflyFlies[f][0];
                double amp = FireflyFlies[f][1];
                double phi = FireflyFlies[f][2];
                int off = (int)FireflyFlies[f][3];
                double env = FireflyEnv(t, off);
                if (env > maxEnv) maxEnv = env;
                if (env <= 0) continue;
                double pos = baseSlot + amp * Math.Sin(2.0 * Math.PI * t / PhasePeriod
                                                      + Math.PI * phi / 180.0);
                for (int j = 0; j < Ring10.Length; j++)
                {
                    int slot = Ring10[j];
                    double b = Tri(slot - pos);
                    if (b > 0)
                    {
                        AddSlot(o, slot, (int)(FireflyCore[0] * b * env),
                            (int)(FireflyCore[1] * b * env), (int)(FireflyCore[2] * b * env));
                    }
                }
            }
            // TOPFANS: breathe in phase (spec 3.2): max envelope across the
            // flies, dim base -> fly core, floored to BEACON_BG
            int[] top = OnBase(new int[]
            {
                (int)(BeconBgC[0] + maxEnv * (FireflyCore[0] - BeconBgC[0])),
                (int)(BeconBgC[1] + maxEnv * (FireflyCore[1] - BeconBgC[1])),
                (int)(BeconBgC[2] + maxEnv * (FireflyCore[2] - BeconBgC[2]))
            });
            for (int j = 0; j < TopFans.Length; j++)
                SetSlot(o, TopFans[j], top);
            return o;
        }

        // -----------------------------------------------------------------
        // Pattern 14 lava lamp (re-based 2026-11-21 to RING10 + TOPFANS):
        // three soft blobs (pos = LAVA_CENTER + LAVA_AMP*sin(2pi*t/P + phi);
        // P = 120/60/40) drift across the standard ring RING10 and blend
        // additively where they overlap; 6-slot raised-cosine bumps;
        // deep-purple baseline (12,6,20) on the ring. The board ring 0..5
        // near-black is retired (0/1 go black). TOPFANS 12/13 follow the
        // motion rule (spec 3.2): the dim base blends toward the lava head
        // color, f = max over the three blobs (1.0 a blob on slot 10, 0.5
        // on 9/11), floored to BEACON_BG; slots 14..239 (dead) stay pure
        // black. Periods 120/60/40 | 120.
        // -----------------------------------------------------------------
        public static byte[] LavaFrame(int phase)
        {
            int t = phase % PhasePeriod;
            byte[] o = new byte[720];
            for (int j = 0; j < Ring10.Length; j++)
                SetSlot(o, Ring10[j], LavaBase);
            double f = 0.0;
            for (int bidx = 0; bidx < 3; bidx++)
            {
                int[] color = LavaColors[bidx];
                int period = LavaPeriods[bidx];
                int phi = LavaPhis[bidx];
                double pos = LavaCenter + LavaAmp * Math.Sin(2.0 * Math.PI * (t % period) / period
                                                              + Math.PI * phi / 180.0);
                if (Math.Abs(pos - 10) <= 0.5) f = Math.Max(f, 1.0);
                else if (Math.Abs(pos - 9) <= 0.5 || Math.Abs(pos - 11) <= 0.5) f = Math.Max(f, 0.5);
                for (int j = 0; j < Ring10.Length; j++)
                {
                    int slot = Ring10[j];
                    double b = LavaBump(slot - pos);
                    if (b > 0)
                    {
                        AddSlot(o, slot, (int)(color[0] * b),
                            (int)(color[1] * b), (int)(color[2] * b));
                    }
                }
            }
            // TOPFANS: dim base + f-blend toward the lava head (spec 3.2
            // motion rule); both blend endpoints are >= floor, floored
            // anyway
            int[] top = OnBase(new int[]
            {
                (int)(LavaTopBase[0] + f * (LavaHead[0] - LavaTopBase[0])),
                (int)(LavaTopBase[1] + f * (LavaHead[1] - LavaTopBase[1])),
                (int)(LavaTopBase[2] + f * (LavaHead[2] - LavaTopBase[2]))
            });
            for (int j = 0; j < TopFans.Length; j++)
                SetSlot(o, TopFans[j], top);
            return o;
        }

        // -----------------------------------------------------------------
        // Pattern 15 sonar (re-based 2026-11-22 to RING10 + TOPFANS):
        // two ripple points at 6.5-r and 6.5+r (r = 4.5*(t % 8)/7) expand
        // symmetrically from the standard ring RING10's center; 0.85^d
        // falloff over 3 slots each side; when t%8==7 the outer points
        // reach the ring's physical edges (slots 2/11), which flash white
        // for 1 frame; the ring-center slots 6/7 are the ping lamp, 2
        // frames at launch. The old onboard slot 4 ping lamp is dropped
        // (board ring retired — 0/1 go black). TOPFANS 12/13 hold the dim
        // base and flare toward the cyan family color when a ripple point
        // is on slot 10 (f=1.0) or 9/11 (f=0.5), else f=0, floored to
        // BEACON_BG (spec 3.2 motion rule). Period 8 | 120.
        // -----------------------------------------------------------------
        public static byte[] SonarFrame(int phase)
        {
            int t = phase % PhasePeriod;
            byte[] o = new byte[720];
            for (int j = 0; j < Ring10.Length; j++)
                SetSlot(o, Ring10[j], SonarBase);
            int f = t % 8;
            if (f < 2)                              // ping lamp, 2 frames at launch
                for (int j = 0; j < SonarPingSlots.Length; j++)
                    SetSlot(o, SonarPingSlots[j], SonarRing);
            double r = 4.5 * f / 7.0;
            double[] pts = new double[] { SonarCenter - r, SonarCenter + r };
            for (int pi = 0; pi < 2; pi++)
            {
                double p = pts[pi];
                for (int j = 0; j < Ring10.Length; j++)
                {
                    int slot = Ring10[j];
                    double b = Math.Pow(0.85, Math.Abs(slot - p));
                    AddSlot(o, slot, (int)(SonarRing[0] * b),
                        (int)(SonarRing[1] * b), (int)(SonarRing[2] * b));
                }
            }
            if (f == 7)                             // edge hit: 1-frame white flash
            {
                int[] white = { 255, 255, 255 };
                SetSlot(o, Ring10[0], white);
                SetSlot(o, Ring10[9], white);
            }
            // TOPFANS: dim base + f-blend flare toward cyan (spec 3.2
            // motion rule), floored to BEACON_BG
            double fl = 0.0;
            for (int pi = 0; pi < 2; pi++)
            {
                int s = (int)(pts[pi] + 0.5);
                if (s == 10) fl = Math.Max(fl, 1.0);
                else if (s == 9 || s == 11) fl = Math.Max(fl, 0.5);
            }
            int[] top = OnBase(new int[]
            {
                (int)(BeconBgC[0] + fl * (SonarRing[0] - BeconBgC[0])),
                (int)(BeconBgC[1] + fl * (SonarRing[1] - BeconBgC[1])),
                (int)(BeconBgC[2] + fl * (SonarRing[2] - BeconBgC[2]))
            });
            for (int j = 0; j < TopFans.Length; j++)
                SetSlot(o, TopFans[j], top);
            return o;
        }

        // -----------------------------------------------------------------
        // Pattern 16 metronome (re-based 2026-11-22 to RING10 + TOPFANS):
        // x(t) = 6.5 - 4.5*cos(2pi*t/40) swings the standard ring RING10
        // left<->right with extrema every 20 frames (60 BPM) at the ring's
        // edges 2/11; the 2-slot Gaussian marker flashes white for 3 frames
        // at each extremum, together with the ring's two edges; the old
        // 3-bit tick counter on onboard 0..2 is dropped (board ring
        // retired — 0/1 go black). TOPFANS 12/13 hold the dim base and
        // flare toward gold when the marker is on slot 10 (f=1.0) or 9/11
        // (f=0.5), else f=0, floored to BEACON_BG (spec 3.2 motion rule).
        // Period 40 | 120.
        // -----------------------------------------------------------------
        public static byte[] MetronomeFrame(int phase)
        {
            int t = phase % PhasePeriod;
            byte[] o = new byte[720];
            for (int j = 0; j < Ring10.Length; j++)
                SetSlot(o, Ring10[j], MetroBase);
            double x = 6.5 - 4.5 * Math.Cos(2.0 * Math.PI * (t % 40) / 40.0);
            int[] mark = (t % 20 < 3) ? MetroAccent : MetroMark;
            for (int j = 0; j < Ring10.Length; j++)
            {
                int slot = Ring10[j];
                double b = Tri(slot - x);
                if (b > 0)
                {
                    AddSlot(o, slot, (int)(mark[0] * b), (int)(mark[1] * b),
                        (int)(mark[2] * b));
                }
            }
            if (t % 20 < 3)                         // downbeat: flash the ring edges
            {
                SetSlot(o, Ring10[0], MetroAccent);
                SetSlot(o, Ring10[9], MetroAccent);
            }
            // TOPFANS: dim base + f-blend flare toward gold (spec 3.2
            // motion rule), floored to BEACON_BG
            int s = (int)(x + 0.5);
            double fl = s == 10 ? 1.0 : (s == 9 || s == 11) ? 0.5 : 0.0;
            int[] top = OnBase(new int[]
            {
                (int)(BeconBgC[0] + fl * (MetroMark[0] - BeconBgC[0])),
                (int)(BeconBgC[1] + fl * (MetroMark[1] - BeconBgC[1])),
                (int)(BeconBgC[2] + fl * (MetroMark[2] - BeconBgC[2]))
            });
            for (int j = 0; j < TopFans.Length; j++)
                SetSlot(o, TopFans[j], top);
            return o;
        }

        // -----------------------------------------------------------------
        // Pattern 17 orbit (re-based 2026-11-22 to RING10 + TOPFANS):
        // x_k = 6.5 + r_k*sin(2pi*t/120 + phi_k) (radii 4.5/3.15/1.8 =
        // the old 2.5/1.75/1.0 scaled x1.8 to the 10-slot ring, so the
        // outer satellite reaches the ring edge 2/11 as before),
        // depth_k = cos(...), brightness 0.25 + 0.75*max(depth, 0); a
        // satellite is hidden when |x-6.5| <= 2.5 and depth < 0 (passes
        // behind the planet); each satellite makes exactly one orbit per
        // 120-frame cycle. The planet: 2-slot core 6/7 + halo 4/5/8/9.
        // The old onboard pass lamps (slots 0/2/4) are dropped (board ring
        // retired — 0/1 go black). TOPFANS 12/13 hold the dim base and
        // flare toward the planet's white when a visible satellite is on
        // slot 10 (f=1.0) or 9/11 (f=0.5), f = max over elements, else
        // f=0, floored to BEACON_BG (spec 3.2 motion rule). Period
        // 120 | 120.
        // -----------------------------------------------------------------
        public static byte[] OrbitFrame(int phase)
        {
            int t = phase % PhasePeriod;
            byte[] o = new byte[720];
            for (int j = 0; j < Ring10.Length; j++)
                SetSlot(o, Ring10[j], OrbitBase);
            for (int j = 0; j < Ring10.Length; j++)
            {
                int slot = Ring10[j];
                double d = Math.Abs(slot - 6.5);
                if (d <= 1.0) SetSlot(o, slot, OrbitPlanet);
                else if (d <= 2.5) SetSlot(o, slot, OrbitHalo);
            }
            double fl = 0.0;
            for (int sidx = 0; sidx < OrbitSatColors.Length; sidx++)
            {
                int[] color = OrbitSatColors[sidx];
                double radius = OrbitSatRadii[sidx];
                int phi = OrbitSatPhis[sidx];
                double th = 2.0 * Math.PI * t / PhasePeriod + Math.PI * phi / 180.0;
                double x = 6.5 + radius * Math.Sin(th);
                double depth = Math.Cos(th);
                if (Math.Abs(x - 6.5) <= 2.5 && depth < 0) continue;   // behind the planet
                double bright = 0.25 + 0.75 * Math.Max(depth, 0.0);
                for (int j = 0; j < Ring10.Length; j++)
                {
                    int slot = Ring10[j];
                    double b = Tri(slot - x) * bright;
                    if (b > 0)
                    {
                        AddSlot(o, slot, (int)(color[0] * b), (int)(color[1] * b),
                            (int)(color[2] * b));
                    }
                }
                int s = (int)(x + 0.5);
                if (s == 10) fl = Math.Max(fl, 1.0);
                else if (s == 9 || s == 11) fl = Math.Max(fl, 0.5);
            }
            // TOPFANS: dim base + f-blend flare toward the planet's white
            // (spec 3.2 motion rule), floored to BEACON_BG
            int[] top = OnBase(new int[]
            {
                (int)(BeconBgC[0] + fl * (OrbitPlanet[0] - BeconBgC[0])),
                (int)(BeconBgC[1] + fl * (OrbitPlanet[1] - BeconBgC[1])),
                (int)(BeconBgC[2] + fl * (OrbitPlanet[2] - BeconBgC[2]))
            });
            for (int j = 0; j < TopFans.Length; j++)
                SetSlot(o, TopFans[j], top);
            return o;
        }

        // -----------------------------------------------------------------
        // Pattern 18 bitstream (re-based 2026-11-22 to RING10 + TOPFANS):
        // "1" chunk = 2 bright (0,255,220) + 2 dim (0,40,36) slots; "0"
        // chunk = 4 dark (6,16,14) slots; every 10 frames the stream
        // shifts LEFT by exactly 1 chunk (P10); 12 shifts per 120-frame
        // cycle = one seamless lap; the visible window is the standard
        // ring RING10 (slots 2..11, 2.5 chunks; chunk edges clip at the
        // ring). The old onboard slot 5 start-bit lamp is dropped (board
        // ring retired — 0/1 go black): its character moves to TOPFANS
        // 12/13, which blink as the start bit (5 on / 5 off) in the
        // family color over the dim base, floored to BEACON_BG, never
        // black. Period 120 | 120.
        // -----------------------------------------------------------------
        public static byte[] BitstreamFrame(int phase)
        {
            int t = phase % PhasePeriod;
            byte[] o = new byte[720];
            int step = (t / 10) % 12;
            for (int j = 0; j < Ring10.Length; j++)
            {
                int slot = Ring10[j];
                char bit = BitstreamPayload[(slot / 4 + step) % 12];
                int oo = slot % 4;
                int[] color;
                if (bit == '1') color = (oo < 2) ? BitstreamOne : BitstreamDim;
                else color = BitstreamZero;
                SetSlot(o, slot, color);
            }
            // TOPFANS: start-bit blink, 5 on / 5 off (floor kept, never black)
            int[] top = (t % 10 < 5) ? OnBase(BitstreamLamp) : BeconBgC;
            for (int j = 0; j < TopFans.Length; j++)
                SetSlot(o, TopFans[j], top);
            return o;
        }

        // -----------------------------------------------------------------
        // Pattern 19 sparkler (re-based 2026-11-21 to RING10 + TOPFANS)
        // -----------------------------------------------------------------
        public static byte[] SparklerFrame(int phase)
        {
            int t = phase % PhasePeriod;
            byte[] o = new byte[720];
            for (int j = 0; j < Ring10.Length; j++)
                SetSlot(o, Ring10[j], BeconBgC);
            for (int j = 0; j < SparkBaseDots.Length; j++)
                SetSlot(o, SparkBaseDots[j], SparkDot);
            int tb = t % 40;
            int burst = SparkBursts[(t / 40) % 3];
            if (tb <= 1)                                // 2-frame shock ring
            {
                for (int k = 1; k <= 3; k++)
                {
                    double lv = SparkShockLv[k - 1];
                    int[] sc = new int[]
                    {
                        (int)(SparkShock[0] * lv),
                        (int)(SparkShock[1] * lv),
                        (int)(SparkShock[2] * lv)
                    };
                    SetSlot(o, SparkPos(burst, k), OnBase(sc));
                    SetSlot(o, SparkPos(burst, -k), OnBase(sc));
                }
            }
            if (tb <= 11)                               // 4 sparks per side, 12 frames
            {
                for (int j = 0; j < 4; j++)
                {
                    int[] c = new int[]
                    {
                        (int)(SparkA[0] + (SparkB[0] - SparkA[0]) * j / 3.0),
                        (int)(SparkA[1] + (SparkB[1] - SparkA[1]) * j / 3.0),
                        (int)(SparkA[2] + (SparkB[2] - SparkA[2]) * j / 3.0)
                    };
                    SetSlot(o, SparkPos(burst, 1 + j + tb), c);
                    SetSlot(o, SparkPos(burst, -(1 + j + tb)), c);
                }
            }
            if (tb <= 2)                                // 1-slot white flash, 3 frames
                SetSlot(o, burst, SparkFlash);
            // TOPFANS: fuse countdown (old onboard slot 5, moved here) +
            // burst flare (spec 3.2 motion rule: f=0.5 on burst 9/11,
            // 0 on 7); dim base otherwise, never black.
            int[] top;
            if (35 <= tb && tb <= 39)
                top = SparkFuse;
            else if (tb <= 2)
            {
                double f = (burst == 9 || burst == 11) ? 0.5 : 0.0;
                top = new int[]
                {
                    (int)(BeconBgC[0] + f * (SparkFlash[0] - BeconBgC[0])),
                    (int)(BeconBgC[1] + f * (SparkFlash[1] - BeconBgC[1])),
                    (int)(BeconBgC[2] + f * (SparkFlash[2] - BeconBgC[2]))
                };
            }
            else
                top = BeconBgC;
            for (int j = 0; j < TopFans.Length; j++)
                SetSlot(o, TopFans[j], top);
            return o;
        }

        // -----------------------------------------------------------------
        // Pattern 20 echo (re-based 2026-11-21 to RING10 + TOPFANS)
        // -----------------------------------------------------------------
        public static byte[] EchoFrame(int phase)
        {
            int t = phase % PhasePeriod;
            byte[] o = new byte[720];
            for (int j = 0; j < Ring10.Length; j++)
                SetSlot(o, Ring10[j], BeconBgC);
            int u = t % 20;
            // ghost 2 (lag 10, 4-slot, 25%) first, then ghost 1 (lag 5, 2-slot, 50%)
            int[] ghostLags = new int[] { 10, 5 };
            int[][] ghostColors = new int[][] { EchoG2, EchoG1 };
            double[] ghostWidths = new double[] { 2.0, 1.0 };
            for (int g = 0; g < 2; g++)
            {
                double p = EchoPos(ModFloor(t - ghostLags[g], 20));
                for (int j = 0; j < Ring10.Length; j++)
                {
                    int slot = Ring10[j];
                    if (Math.Abs(slot - p) < ghostWidths[g])
                        SetSlot(o, slot, ghostColors[g]);
                }
            }
            double pc = EchoPos(u);
            int c = (int)(pc + 0.5);                  // the 1 slot nearest the core
            SetSlot(o, c, EchoCore);
            if (u < 3)                                // left wall hit
                SetSlot(o, Ring10[0], EchoCore);
            else if (10 <= u && u < 13)              // right wall hit
                SetSlot(o, Ring10[9], EchoCore);
            // TOPFANS: dim base + f-blend toward the core color (spec 3.2
            // motion rule): f=1.0 core on slot 10, 0.5 on 9/11, else 0
            double f = c == 10 ? 1.0 : (c == 9 || c == 11) ? 0.5 : 0.0;
            int[] topfan = new int[]
            {
                (int)(BeconBgC[0] + f * (EchoCore[0] - BeconBgC[0])),
                (int)(BeconBgC[1] + f * (EchoCore[1] - BeconBgC[1])),
                (int)(BeconBgC[2] + f * (EchoCore[2] - BeconBgC[2]))
            };
            for (int j = 0; j < TopFans.Length; j++)
                SetSlot(o, TopFans[j], topfan);
            return o;
        }

        // -----------------------------------------------------------------
        // Pattern 21 ecg (re-based 2026-11-21 to RING10 + TOPFANS)
        // -----------------------------------------------------------------
        public static byte[] EcgFrame(int phase)
        {
            int t = phase % PhasePeriod;
            byte[] o = new byte[720];
            double oo = (t % 30) * (EcgSpacing / 30.0);
            for (int j = 0; j < Ring10.Length; j++)
            {
                int slot = Ring10[j];
                double u = ModFloorD(slot - oo, EcgSpacing);
                int[] c;
                if (10.0 <= u && u < 12.0) c = EcgQ;
                else if (14.0 <= u && u < 18.0) c = EcgT;
                else c = BeconBgC;
                SetSlot(o, slot, OnBase(c));
                double b = Math.Max(0.0, 1.0 - Math.Abs(u - EcgPeakU) / 1.5);
                if (b > 0)
                {
                    SetSlot(o, slot, OnBase(new int[]
                    {
                        (int)(EcgR[0] * b), (int)(EcgR[1] * b), (int)(EcgR[2] * b)
                    }));
                }
            }
            // TOPFANS: flare toward the R peak color (the old rate lamp,
            // spec 3.2 motion rule): f=1.0 peak crossing slot 10, 0.5 on
            // 9/11, else 0; dim base otherwise.
            double peak = ModFloorD(oo + EcgPeakU, EcgSpacing);
            double f = (10.0 <= peak && peak < 11.0) ? 1.0
                : ((9.0 <= peak && peak < 10.0) || (11.0 <= peak && peak < 12.0)) ? 0.5 : 0.0;
            int[] topfan = new int[]
            {
                (int)(BeconBgC[0] + f * (EcgR[0] - BeconBgC[0])),
                (int)(BeconBgC[1] + f * (EcgR[1] - BeconBgC[1])),
                (int)(BeconBgC[2] + f * (EcgR[2] - BeconBgC[2]))
            };
            for (int j = 0; j < TopFans.Length; j++)
                SetSlot(o, TopFans[j], topfan);
            return o;
        }

        // -----------------------------------------------------------------
        // Pattern 22 walk (re-based 2026-11-21 to RING10 + TOPFANS)
        // -----------------------------------------------------------------
        public static byte[] WalkFrame(int phase)
        {
            int t = phase % PhasePeriod;
            byte[] o = new byte[720];
            for (int j = 0; j < Ring10.Length; j++)
                SetSlot(o, Ring10[j], BeconBgC);
            bool look = (WalkWalkEnd <= t && t < WalkLookEnd) || t >= WalkBackEnd;
            double pos;
            double dirn;
            double bodyB;
            if (t < WalkWalkEnd)
            {
                pos = 2.0 + 9.0 * (t / (double)WalkWalkEnd);
                dirn = 1.0;
                bodyB = ((t % 10) < 5) ? 1.0 : 0.75;
            }
            else if (t < WalkLookEnd)
            {
                pos = 11.0; dirn = -1.0; bodyB = 1.0;   // look around (frozen)
            }
            else if (t < WalkBackEnd)
            {
                pos = 11.0 - 9.0 * ((t - WalkLookEnd) / (double)(WalkBackEnd - WalkLookEnd));
                dirn = -1.0;
                bodyB = ((t % 10) < 5) ? 1.0 : 0.75;
            }
            else
            {
                pos = 2.0; dirn = 1.0; bodyB = 1.0;     // look around (frozen)
            }
            for (int j = 0; j < Ring10.Length; j++)
            {
                int slot = Ring10[j];
                double b = Tri(slot - pos) * bodyB;
                if (b > 0)
                {
                    SetSlot(o, slot, OnBase(new int[]
                    {
                        (int)(WalkBody[0] * b),
                        (int)(WalkBody[1] * b), (int)(WalkBody[2] * b)
                    }));
                }
            }
            double hp = Math.Max(2.0, Math.Min(11.0, pos + dirn));  // head: 1 ahead, clamped 2..11
            int[] head;
            if (look) head = WalkHeadLook;
            else if (WalkBlinkLo <= t && t <= WalkBlinkHi) head = WalkHeadBlink;
            else head = WalkHead;
            int headSlot = (int)(hp + 0.5);
            SetSlot(o, headSlot, head);
            // TOPFANS: dim base + f-blend toward the head color (spec 3.2
            // motion rule): f=1.0 head on slot 10, 0.5 on 9/11, else 0
            double f = headSlot == 10 ? 1.0 : (headSlot == 9 || headSlot == 11) ? 0.5 : 0.0;
            int[] topfan = new int[]
            {
                (int)(BeconBgC[0] + f * (WalkHead[0] - BeconBgC[0])),
                (int)(BeconBgC[1] + f * (WalkHead[1] - BeconBgC[1])),
                (int)(BeconBgC[2] + f * (WalkHead[2] - BeconBgC[2]))
            };
            for (int j = 0; j < TopFans.Length; j++)
                SetSlot(o, TopFans[j], topfan);
            return o;
        }

        // -----------------------------------------------------------------
        // Pattern 23 gpu-spotlight-idle
        // -----------------------------------------------------------------
        public static byte[] GpuSpotlightIdleFrame(int phase)
        {
            int t = phase % PhasePeriod;
            byte[] o = new byte[720];
            int[] c = SpotColor(SpotIdlePalette, t, PhasePeriod);
            for (int i = 0; i < SpotlightSlots.Length; i++)
                SetSlot(o, SpotlightSlots[i], c);
            return o;
        }

        // -----------------------------------------------------------------
        // Pattern 24 gpu-spotlight-fire
        // -----------------------------------------------------------------
        public static byte[] GpuSpotlightFireFrame(int phase)
        {
            int t = phase % PhasePeriod;
            byte[] o = new byte[720];
            int[] c = SpotColor(SpotFirePalette, t, SpotFirePeriod);
            for (int i = 0; i < SpotlightSlots.Length; i++)
                SetSlot(o, SpotlightSlots[i], c);
            return o;
        }

        // -----------------------------------------------------------------
        // Pattern 25 gpu-spotlight-somewhat-active
        // -----------------------------------------------------------------
        public static byte[] GpuSpotlightSomewhatActiveFrame(int phase)
        {
            int t = phase % PhasePeriod;
            byte[] o = new byte[720];
            int[] c = SpotColor(SpotActivePalette, t, SpotActivePeriod);
            for (int i = 0; i < SpotlightSlots.Length; i++)
                SetSlot(o, SpotlightSlots[i], c);
            return o;
        }

        // -----------------------------------------------------------------
        // Pattern 26 red_white — re-based 2026-11-21 to RING10 + TOPFANS
        // (old 6..11 span extended to the full 10-slot ring 2..11):
        // static test — RING10 (slots 2..11) pure RED, slots 12/13 (the
        // top-fan 2/3 o'clock LEDs) pure WHITE; slots 0/1 (live,
        // unassigned) and 14..239 (dead) pure black.
        // -----------------------------------------------------------------
        public static byte[] RedWhiteFrame(int phase)
        {
            byte[] o = new byte[720];
            for (int j = 0; j < Ring10.Length; j++)
                SetSlot(o, Ring10[j], RedWhiteRed);
            for (int i = 0; i < SpotlightSlots.Length; i++)
                SetSlot(o, SpotlightSlots[i], RedWhiteWhite);
            return o;
        }

        // -----------------------------------------------------------------
        // Pattern 27 rainbow (re-based 2026-11-23 to RING10 + TOPFANS): a
        // multi-colored wheel slowly rotating around the standard ring
        // RING10 (slots 2..11) — the six wheel colors repeat over the
        // 10 slots (at phase 0: 2=red, 3=orange, 4=yellow, 5=green,
        // 6=blue, 7=violet, 8=red, 9=orange, 10=yellow, 11=green); the
        // wheel slides clockwise, one slot position per 20 frames, one
        // full rotation per 120-frame phase (seamless:
        // frame(120) == frame(0)) — smooth, every slot fully lit every
        // frame. The old board ring 0..5 mirror is dropped — slots 0/1
        // pure black. Slots 12/13 (the GPU LEDs — the subject) keep
        // slowly cycling the same six colors, one at a time (12-frame
        // hold + 8-frame fade per color). 14..239 dead, never painted.
        // -----------------------------------------------------------------
        public static byte[] RainbowFrame(int phase)
        {
            int t = phase % PhasePeriod;
            byte[] o = new byte[720];
            for (int s = 0; s < Ring10.Length; s++)
                SetSlot(o, Ring10[s], RainbowWheelColor(s, t));
            int[] c12 = RainbowCycle(t);
            for (int j = 0; j < TopFans.Length; j++)
                SetSlot(o, TopFans[j], c12);
            return o;
        }

        // -----------------------------------------------------------------
        // Pattern 28 limelight (re-based 2026-11-23 to RING10 + TOPFANS):
        // the GPU (slots 12/13 — the top-fan LEDs that shine down on the
        // GPU) is the star: a bright breathing star, warm gold
        // (LimelightGpuFloor) at the trough -> bright white
        // (LimelightGpuTop) at the peak, one breath per 60 frames (3 s).
        // The standard ring RING10 is the dim INVERSE-breathing backdrop
        // (the room goes dark as the spotlight brightens): a deep-blue
        // wash, brightest when the star is dimmest and at its floor when
        // it peaks. The old board ring 0..5 wash is dropped — slots 0/1
        // pure black. Live slots 2..13 never go fully black (liveness);
        // 14..239 dead, never painted. Period 60 | 120 bit-exact
        // (precomputed breath table, indexed t % 60).
        // -----------------------------------------------------------------
        public static byte[] LimelightFrame(int phase)
        {
            int t = phase % PhasePeriod;
            byte[] o = new byte[720];
            double b = LimelightBreath[t % LimelightPeriod];
            double inv = 1.0 - b;
            int[] g = new int[]
            {
                (int)(LimelightGpuFloor[0] + (LimelightGpuTop[0] - LimelightGpuFloor[0]) * b),
                (int)(LimelightGpuFloor[1] + (LimelightGpuTop[1] - LimelightGpuFloor[1]) * b),
                (int)(LimelightGpuFloor[2] + (LimelightGpuTop[2] - LimelightGpuFloor[2]) * b)
            };
            for (int j = 0; j < TopFans.Length; j++)
                SetSlot(o, TopFans[j], g);
            int[] f = new int[]
            {
                (int)(LimelightFanFloor[0] + (LimelightFanTop[0] - LimelightFanFloor[0]) * inv),
                (int)(LimelightFanFloor[1] + (LimelightFanTop[1] - LimelightFanFloor[1]) * inv),
                (int)(LimelightFanFloor[2] + (LimelightFanTop[2] - LimelightFanFloor[2]) * inv)
            };
            for (int j = 0; j < Ring10.Length; j++)
                SetSlot(o, Ring10[j], f);
            return o;
        }

        // -----------------------------------------------------------------
        // Pattern 29 catwalk (re-based 2026-11-23 to RING10 + TOPFANS): a
        // bright head walks the standard ring RING10 (slots 2..11) once
        // per 10 frames (1 latch/frame, +1 slot, seamless wrap), with a
        // hot tail1 directly behind and a dimmer tail2 behind that; the
        // rest of the ring is the deep-purple carpet. Slots 12/13 are
        // the photographers: a near-white flash for 2 frames, a soft
        // afterglow for 1, dim standby for the rest (period 10 | 120).
        // The old board ring 0..5 audience is dropped — slots 0/1 pure
        // black. 14..239 dead, never painted.
        // -----------------------------------------------------------------
        public static byte[] CatwalkFrame(int phase)
        {
            int t = phase % PhasePeriod;
            byte[] o = new byte[720];
            int h = t % 10;
            for (int s = 0; s < 10; s++)
            {
                int[] c;
                if (s == h) c = CatwalkHead;
                else if (s == ModFloor(h - 1, 10)) c = CatwalkTail1;
                else if (s == ModFloor(h - 2, 10)) c = CatwalkTail2;
                else c = CatwalkBase;
                SetSlot(o, Ring10[s], c);
            }
            int k = t % 10;
            int[] p;
            if (k <= 1) p = CatwalkPhotoFlash;
            else if (k == 2) p = CatwalkPhotoAfter;
            else p = CatwalkPhotoBase;
            for (int j = 0; j < TopFans.Length; j++)
                SetSlot(o, TopFans[j], p);
            return o;
        }

        // -----------------------------------------------------------------
        // Pattern 30 supernova (re-based 2026-11-23 to RING10 + TOPFANS):
        // a star builds up (smoothstep envelope + 8-frame sine ripple)
        // over RING10 + 12/13, detonates into a near-white flash ->
        // orange fireball, then a shockwave band (hot head + fading
        // tail) walks RING10 once over the 39-frame decay (1 slot/frame,
        // wrap) while the whole ring cools hot->cool ember and the
        // remnant star pulses; the afterglow fades the ring back to the
        // night-fan rest color exactly at the 120-frame loop seam
        // (seamless). The old board ring 0..5 wash is dropped — slots
        // 0/1 pure black. 14..239 dead, never painted.
        // -----------------------------------------------------------------
        public static byte[] SupernovaFrame(int phase)
        {
            int t = phase % PhasePeriod;
            byte[] o = new byte[720];
            int[] star;
            int[][] ring;
            if (t <= SnoBuildEnd)
            {
                double env = Smoothstep(t / (double)SnoBuildEnd);
                double ripple = 0.85 + 0.15 * Math.Sin(2.0 * Math.PI * t / 8.0);
                star = Slerp(SnoNightStar, SnoStarTop, env * ripple);
                double fb = env * (0.85 + 0.15 * Math.Sin(2.0 * Math.PI * t / 12.0));
                int[] fc = Slerp(SnoNightFan, SnoFanBright, fb);
                ring = new int[10][];
                for (int s = 0; s < 10; s++)
                    ring[s] = fc;
            }
            else if (t <= SnoDetEnd)
            {
                int[] c;
                if (t <= SnoDetFlashEnd) c = SnoFlash;
                else c = Slerp(SnoFlash, SnoFireball, (t - SnoDetFlashEnd) / 4.0);
                star = c;
                ring = new int[10][];
                for (int s = 0; s < 10; s++)
                    ring[s] = c;
            }
            else if (t <= SnoShockEnd)
            {
                double v = (t - SnoShockStart) / (double)(SnoShockEnd - SnoShockStart);
                int[] band = Slerp(SnoShockHot, SnoShockCool, v);
                int[] ember = Slerp(SnoEmberHot, SnoEmberCool, v);
                int pos = (t - SnoShockStart) % 10;
                double fade = (t < SnoFadeStart)
                    ? 1.0
                    : Math.Max(0.0, (SnoShockEnd - t) / (double)(SnoShockEnd - SnoFadeStart));
                ring = new int[10][];
                for (int s = 0; s < 10; s++)
                {
                    int[] c;
                    if (s == pos) c = Slerp(ember, band, fade);
                    else if (s == ModFloor(pos - 1, 10)) c = Slerp(ember, band, fade * 0.5);
                    else c = ember;
                    ring[s] = c;
                }
                double pulse = 0.5 + 0.5 * Math.Sin(2.0 * Math.PI * t / 6.0);
                star = Slerp(SnoRemnantDim, SnoRemnantBright, pulse);
            }
            else
            {
                double v = (t - SnoAfterStart) / (double)(PhasePeriod - 1 - SnoAfterStart);
                double pulse = 0.5 + 0.5 * Math.Sin(2.0 * Math.PI * t / 6.0);
                int[] pulsar = Slerp(SnoRemnantDim, SnoRemnantBright, pulse);
                star = Slerp(pulsar, SnoNightStar, v);
                int[] fc = Slerp(SnoEmberCool, SnoNightFan, v);
                ring = new int[10][];
                for (int s = 0; s < 10; s++)
                    ring[s] = fc;
            }
            for (int j = 0; j < TopFans.Length; j++)
                SetSlot(o, TopFans[j], star);
            for (int s = 0; s < 10; s++)
                SetSlot(o, Ring10[s], ring[s]);
            return o;
        }

        // -----------------------------------------------------------------
        // Pattern 31 inferno (re-based 2026-11-23 to RING10 + TOPFANS):
        // the GPU sits underneath a FURNACE. RING10 (slots 2..11) slowly
        // cycles dark red (InfernoFanDark) -> bright orange (InfernoFanHot)
        // over 60 frames (the hot cycle, 60 | 120); TOPFANS 12/13 (the
        // subject) pulse randomly like flames — a bright yellow/orange base
        // (InfernoTopBase) lerp'd toward near-white by the deterministic
        // InfernoFlicker table (0.35 + 0.5*v), with a hard near-white spark
        // (InfernoTopWhite) whenever the table crosses InfernoSparkLevel.
        // The old board ring 0..5 dim ember is dropped — slots 0/1 pure
        // black. Period 120 | 120; live 2..13 colored every frame
        // (liveness); 14..239 dead, never painted.
        // -----------------------------------------------------------------
        public static byte[] InfernoFrame(int phase)
        {
            int t = phase % PhasePeriod;
            byte[] o = new byte[720];
            double b = InfernoBreath[t % InfernoPeriod];
            int[] fan = Slerp(InfernoFanDark, InfernoFanHot, b);
            double v = InfernoFlicker[t];
            int[] top;
            if (v >= InfernoSparkLevel) top = InfernoTopWhite;
            else top = Slerp(InfernoTopBase, InfernoTopWhite, 0.35 + 0.5 * v);
            for (int j = 0; j < TopFans.Length; j++)
                SetSlot(o, TopFans[j], top);
            for (int j = 0; j < Ring10.Length; j++)
                SetSlot(o, Ring10[j], fan);
            return o;
        }

        // -----------------------------------------------------------------
        // Pattern 32 reactor-core (re-based 2026-11-23 to RING10 +
        // TOPFANS): the GPU as the POWER SOURCE of a futuristic reactor.
        // RING10 (slots 2..11): deep blue, slowly pulsing
        // (ReactorFanDark -> ReactorFanBright, 60-frame breath,
        // t=0 trough, t=30 peak; 60 | 120). TOPFANS 12/13 (the subject,
        // the LEDs that shine down on the GPU): bright cyan
        // (ReactorTopBase) that flashes BRIGHTER toward near-white
        // (ReactorTopFlash) every 60 frames (3 s) on a 13-frame
        // raised-cosine pulse (ReactorPulse: 0 at u=0, 1 at u=6, 0 at
        // u=12) — as if energy is being transferred into the GPU. The old
        // board ring 0..5 dim blue wash is dropped — slots 0/1 pure black.
        // Period 120 | 120 (precomputed tables, bit-exact); live 2..13
        // colored every frame (liveness); 14..239 dead, never painted.
        // -----------------------------------------------------------------
        public static byte[] ReactorCoreFrame(int phase)
        {
            int t = phase % PhasePeriod;
            byte[] o = new byte[720];
            double b = ReactorBreath[t % ReactorPeriod];
            double p = ReactorPulse[t % ReactorPeriod];
            int[] fan = Slerp(ReactorFanDark, ReactorFanBright, b);
            int[] top = Slerp(ReactorTopBase, ReactorTopFlash, p);
            for (int j = 0; j < TopFans.Length; j++)
                SetSlot(o, TopFans[j], top);
            for (int j = 0; j < Ring10.Length; j++)
                SetSlot(o, Ring10[j], fan);
            return o;
        }

        // frame_colors port (ref\gpu_rgb.py): blend the four drive-model
        // level frames per slot by weight; int() truncation == (int) cast.
        public static byte[] FrameColors(double[] w, int phase, Func<int, byte[]>[] fns)
        {
            byte[][] frames = new byte[4][];
            for (int k = 0; k < 4; k++) frames[k] = fns[k](phase);
            byte[] outb = new byte[Proto.NumSlots * 3];
            for (int i = 0; i < Proto.NumSlots * 3; i++)
                outb[i] = (byte)(int)(w[0] * frames[0][i] + w[1] * frames[1][i] +
                                      w[2] * frames[2][i] + w[3] * frames[3][i]);
            return outb;
        }

        // Crossfade ground truth: tests\driveframes.bin = 7 levels
        // (0,0.5,...,3) x 120 phases x 720B from Linux frame_colors.
        public static void CheckDrive()
        {
            double[] levels = new double[] { 0.0, 0.5, 1.0, 1.5, 2.0, 2.5, 3.0 };
            byte[] gt = File.ReadAllBytes(Path.Combine(App.Base, "tests", "driveframes.bin"));
            Tst.Assert(gt.Length == levels.Length * 120 * 720, "driveframes.bin size");
            Func<int, byte[]>[] fns = new Func<int, byte[]>[4];
            for (int i = 0; i < 4; i++) fns[i] = Resolve(Model.LevelNames[i]).Frame;
            int n = 0;
            for (int li = 0; li < levels.Length; li++)
            {
                double[] w = Model.WeightsFromLevel(levels[li]);
                for (int t = 0; t < 120; t++)
                {
                    byte[] f = FrameColors(w, t, fns);
                    int off = (li * 120 + t) * 720;
                    for (int b = 0; b < 720; b++)
                        if (f[b] != gt[off + b])
                            throw new Exception(string.Format(
                                "drive crossfade mismatch level={0} phase={1} byte={2}: got {3} want {4}",
                                levels[li], t, b, f[b], gt[off + b]));
                    n++;
                }
            }
            Console.WriteLine("OK: " + n + " crossfade frames (7 levels x 120 phases) byte-exact vs Linux frame_colors");
        }

        // -----------------------------------------------------------------
        // Registry (ordered 1-32, catalog order; base_rgb per
        // tests\groundtruth.json)
        // -----------------------------------------------------------------
        public class Entry
        {
            public int Number;
            public string Name;
            public Func<int, byte[]> Frame;
            public int[] BaseRgb;
            public Entry(int number, string name, Func<int, byte[]> frame, int[] baseRgb)
            {
                Number = number;
                Name = name;
                Frame = frame;
                BaseRgb = baseRgb;
            }
        }

        public static readonly Entry[] All = new Entry[]
        {
            new Entry(1, "darkblue", DarkBlueFrame, new int[] { 33, 33, 140 }),
            new Entry(2, "perfan", PerfAnFrame, new int[] { 255, 0, 0 }),
            new Entry(3, "chase_cool", ChaseCoolFrame, new int[] { 0, 0, 255 }),
            new Entry(4, "chase_warm", ChaseWarmFrame, new int[] { 255, 255, 0 }),
            new Entry(5, "beacon", BeaconFrame, new int[] { 255, 255, 0 }),
            new Entry(6, "sweep", SweepFrame, new int[] { 255, 255, 0 }),
            new Entry(7, "tandem", TandemFrame, new int[] { 255, 191, 0 }),
            new Entry(8, "heartbeat", HeartbeatFrame, new int[] { 255, 102, 0 }),
            new Entry(9, "comet", CometFrame, new int[] { 255, 255, 0 }),
            new Entry(10, "hazard", HazardFrame, new int[] { 255, 191, 0 }),
            new Entry(11, "bitclock", BitclockFrame, new int[] { 0, 255, 120 }),
            new Entry(12, "telegraph", TelegraphFrame, new int[] { 255, 244, 214 }),
            new Entry(13, "fireflies", FirefliesFrame, new int[] { 255, 196, 87 }),
            new Entry(14, "lava", LavaFrame, new int[] { 255, 110, 40 }),
            new Entry(15, "sonar", SonarFrame, new int[] { 0, 220, 255 }),
            new Entry(16, "metronome", MetronomeFrame, new int[] { 255, 214, 90 }),
            new Entry(17, "orbit", OrbitFrame, new int[] { 255, 255, 240 }),
            new Entry(18, "bitstream", BitstreamFrame, new int[] { 0, 255, 220 }),
            new Entry(19, "sparkler", SparklerFrame, new int[] { 255, 191, 0 }),
            new Entry(20, "echo", EchoFrame, new int[] { 255, 255, 255 }),
            new Entry(21, "ecg", EcgFrame, new int[] { 255, 255, 255 }),
            new Entry(22, "walk", WalkFrame, new int[] { 120, 255, 120 }),
            new Entry(23, "gpu-spotlight-idle", GpuSpotlightIdleFrame, new int[] { 0, 64, 255 }),
            new Entry(24, "gpu-spotlight-fire", GpuSpotlightFireFrame, new int[] { 255, 144, 0 }),
            new Entry(25, "gpu-spotlight-somewhat-active", GpuSpotlightSomewhatActiveFrame, new int[] { 255, 144, 0 }),
            new Entry(26, "red_white", RedWhiteFrame, new int[] { 255, 0, 0 }),
            new Entry(27, "rainbow", RainbowFrame, new int[] { 255, 0, 0 }),
            new Entry(28, "limelight", LimelightFrame, new int[] { 255, 255, 250 }),
            new Entry(29, "catwalk", CatwalkFrame, new int[] { 255, 255, 250 }),
            new Entry(30, "supernova", SupernovaFrame, new int[] { 255, 255, 250 }),
            new Entry(31, "inferno", InfernoFrame, new int[] { 255, 130, 24 }),
            new Entry(32, "reactor-core", ReactorCoreFrame, new int[] { 150, 235, 255 }),
        };

        // Resolve a pattern by name or number (1-32). FAILS LOUDLY on
        // unknown input — never a silent fallback (the Linux "invisible
        // beacon" whitelist regression).
        public static Entry Resolve(string nameOrNumber)
        {
            if (nameOrNumber != null)
            {
                int n;
                if (int.TryParse(nameOrNumber.Trim(), out n))
                {
                    if (n >= 1 && n <= All.Length) return All[n - 1];
                    throw new Exception(string.Format(
                        "unknown pattern number {0} (valid: 1-32, see the mystic-preview catalog)",
                        n));
                }
                foreach (Entry e in All)
                {
                    if (string.Equals(e.Name, nameOrNumber.Trim(), StringComparison.Ordinal))
                        return e;
                }
            }
            throw new Exception(string.Format(
                "unknown pattern '{0}' (valid: names 1-32 from the mystic-preview catalog, " +
                "e.g. 'sweep', 'rainbow', or a number 1-32)",
                nameOrNumber == null ? "" : nameOrNumber));
        }

        // -----------------------------------------------------------------
        // --check: offline pattern validation against tests\groundtruth.bin
        // (pattern-major: offset = ((n-1)*120 + t)*720)
        //
        // Self-contained asserts (registry, dead slots, periodicity, the
        // final mismatch count) go through Tst.Assert, which --check runs
        // in COLLECT mode: failures are recorded, never thrown, so the
        // byte-exact ground-truth comparison always runs to completion for
        // all 32 patterns and reports every per-pattern mismatch even when
        // an assertion block fails (RING10 rebase foundation, 2026-11-22).
        // -----------------------------------------------------------------
        public static int CheckAll()
        {
            // 1. registry: exactly 32 names in catalog order
            string[] catalog = new string[]
            {
                "darkblue", "perfan", "chase_cool", "chase_warm", "beacon", "sweep",
                "tandem", "heartbeat", "comet", "hazard", "bitclock", "telegraph",
                "fireflies", "lava", "sonar", "metronome", "orbit", "bitstream",
                "sparkler", "echo", "ecg", "walk", "gpu-spotlight-idle",
                "gpu-spotlight-fire", "gpu-spotlight-somewhat-active", "red_white",
                "rainbow", "limelight", "catwalk", "supernova", "inferno",
                "reactor-core"
            };
            Tst.Assert(All.Length == 32, "registry has exactly 32 patterns");
            for (int i = 0; i < All.Length; i++)
            {
                Tst.Assert(All[i].Number == i + 1,
                    "registry number order at index " + i);
                Tst.Assert(All[i].Name == catalog[i],
                    "registry name at index " + i + " in catalog order");
            }

            // 2. Resolve rejects unknown names/numbers (fails loudly)
            AssertThrows("bogus-name", () => { Patterns.Resolve("bogus-name"); });
            AssertThrows("empty name", () => { Patterns.Resolve(""); });
            AssertThrows("number 0", () => { Patterns.Resolve("0"); });
            AssertThrows("number 33", () => { Patterns.Resolve("33"); });
            AssertThrows("number 99", () => { Patterns.Resolve("99"); });
            AssertThrows("number -5", () => { Patterns.Resolve("-5"); });
            Tst.Assert(Patterns.Resolve("6").Number == 6, "Resolve('6') -> pattern 6");
            Tst.Assert(Patterns.Resolve("sweep").Number == 6, "Resolve('sweep') -> pattern 6");
            Tst.Assert(Patterns.Resolve("32").Number == 32, "Resolve('32') -> pattern 32");
            Tst.Assert(Patterns.Resolve("reactor-core").Number == 32,
                "Resolve('reactor-core') -> pattern 32");

            // 3. groundtruth
            string gtPath = Path.Combine(App.Base, "tests", "groundtruth.bin");
            byte[] gt;
            try
            {
                gt = File.ReadAllBytes(gtPath);
            }
            catch (Exception ex)
            {
                throw new Exception("--check: cannot load " + gtPath + ": " + ex.Message);
            }
            Tst.Assert(gt.Length == 32 * 120 * 720,
                "groundtruth.bin is " + 32 * 120 * 720 + " bytes (32x120x720)");

            // 3a. patterns 1-5 (darkblue/perfan/chase_cool/chase_warm/
            // beacon) — re-based 2026-11-21 to RING10 + TOPFANS:
            // self-contained assertion blocks (mirror the check blocks in
            // ref\gpu_rgb.py)
            CheckDarkBlue();
            CheckPerfan();
            CheckChase("chase_cool", PaletteCool, ChaseCoolFrame);
            CheckChase("chase_warm", PaletteWarm, ChaseWarmFrame);
            CheckBeacon();

            // 3b. pattern 6 sweep — THE TEMPLATE: self-contained RING10 +
            // TOPFANS assertions (re-based 2026-11-21, mirrors the sweep
            // check block in ref\gpu_rgb.py)
            CheckSweep();
            // 3c. patterns 7-10 (tandem/heartbeat/comet/hazard) — re-based
            // 2026-11-21 to RING10 + TOPFANS: self-contained assertion
            // blocks mirroring the check blocks in ref\gpu_rgb.py
            CheckTandem();
            CheckHeartbeat();
            CheckComet();
            CheckHazard();

            // 3d. patterns 11-14 (bitclock/telegraph/fireflies/lava) —
            // re-based 2026-11-21 to RING10 + TOPFANS: self-contained
            // assertion blocks mirroring the check blocks in ref\gpu_rgb.py
            CheckBitclock();
            CheckTelegraph();
            CheckFireflies();
            CheckLava();

            // 3e. patterns 15-18 (sonar/metronome/orbit/bitstream) —
            // re-based 2026-11-22 to RING10 + TOPFANS: self-contained
            // assertion blocks mirroring the check blocks in ref\gpu_rgb.py
            CheckSonar();
            CheckMetronome();
            CheckOrbit();
            CheckBitstream();

            // 3f. patterns 19-22 (sparkler/echo/ecg/walk) — re-based
            // 2026-11-21 to RING10 + TOPFANS: self-contained assertion
            // blocks mirroring the check blocks in ref\gpu_rgb.py
            CheckSparkler();
            CheckEcho();
            CheckEcg();
            CheckWalk();

            // 3g. patterns 23-25 (gpu-spotlight-idle/-fire/-
            // somewhat-active) — user 2026-11-22: spotlight states light
            // ONLY 12/13; RING10 2..11, 0/1, dead 14..239 all pure black.
            // Pattern 26 red_white — static: RING10 2..11 pure red, 12/13
            // white (re-based 2026-11-21 to RING10 + TOPFANS).
            // Self-contained assertion blocks mirroring the check blocks
            // in ref\gpu_rgb.py.
            CheckGpuSpotlight();
            CheckRedWhite();

            // 3h. patterns 27-30 (rainbow/limelight/catwalk/supernova) —
            // re-based 2026-11-23 to RING10 + TOPFANS: self-contained
            // assertion blocks mirroring the check blocks in
            // ref\gpu_rgb.py
            CheckRainbow();
            CheckLimelight();
            CheckCatwalk();
            CheckSupernova();

            // 3i. patterns 31-32 (inferno/reactor-core) — re-based
            // 2026-11-23 to RING10 + TOPFANS: self-contained assertion
            // blocks mirroring the check blocks in ref\gpu_rgb.py
            CheckInferno();
            CheckReactorCore();

            int framesOk = 0;
            int mismatches = 0;
            int printed = 0;
            for (int n = 1; n <= 32; n++)
            {
                Entry e = All[n - 1];
                for (int t = 0; t < 120; t++)
                {
                    byte[] frame = e.Frame(t);
                    Tst.Assert(frame != null && frame.Length == 720,
                        string.Format("pattern {0} ({1}) phase {2}: frame length 720",
                            n, e.Name, t));
                    // A broken frame is collected above; substitute pure
                    // black so the dead-slot / periodicity asserts and the
                    // byte-exact comparison still run to completion.
                    if (frame == null || frame.Length != 720)
                        frame = new byte[720];
                    // dead slots 14..239 exactly (0,0,0)
                    for (int i = 14; i < 240; i++)
                    {
                        if (frame[3 * i] != 0 || frame[3 * i + 1] != 0 || frame[3 * i + 2] != 0)
                            Tst.Assert(false, string.Format(
                                "pattern {0} ({1}) phase {2}: dead slot {3} not black",
                                n, e.Name, t, i));
                    }
                    // periodicity: frame(t) == frame(t+120)
                    byte[] frame120 = e.Frame(t + 120);
                    Tst.Assert(SameBytes(frame, frame120),
                        string.Format("pattern {0} ({1}) phase {2}: not 120-periodic",
                            n, e.Name, t));
                    // byte-exact vs the Linux reference
                    int off = (n - 1) * 120 * 720 + t * 720;
                    if (!SameBytesAt(frame, gt, off, 720))
                    {
                        mismatches++;
                        if (printed < 20)
                        {
                            int slot = -1;
                            for (int i = 0; i < 240; i++)
                            {
                                if (frame[3 * i] != gt[off + 3 * i]
                                    || frame[3 * i + 1] != gt[off + 3 * i + 1]
                                    || frame[3 * i + 2] != gt[off + 3 * i + 2])
                                {
                                    slot = i;
                                    break;
                                }
                            }
                            Console.WriteLine(string.Format(
                                "MISMATCH pattern {0} ({1}) phase {2}: first diff at slot {3}",
                                n, e.Name, t, slot));
                            Console.WriteLine("  expected: " + App.Hex(gt, off + 3 * slot, 12));
                            Console.WriteLine("  got:      " + App.Hex(frame, 3 * slot, 12));
                            printed++;
                        }
                    }
                    else
                    {
                        framesOk++;
                    }
                }
                Console.WriteLine(string.Format(
                    "  pattern {0,2} {1,-36} 120/120 phases checked", n, e.Name));
            }
            Tst.Assert(mismatches == 0,
                string.Format("{0} frame(s) differ from tests\\groundtruth.bin", mismatches));
            if (mismatches == 0)
                Console.WriteLine("OK: 32 patterns x 120 phases = 3840 frames byte-exact vs Linux reference");
            return 0;
        }

        static void AssertThrows(string what, Action a)
        {
            bool threw = false;
            try { a(); }
            catch (Exception) { threw = true; }
            Tst.Assert(threw, "Resolve must reject " + what);
        }

        // True iff frame slot `slot` equals color (r,g,b).
        static bool SlotEq(byte[] f, int slot, int[] c)
        {
            return f[3 * slot] == c[0] && f[3 * slot + 1] == c[1] && f[3 * slot + 2] == c[2];
        }

        // -----------------------------------------------------------------
        // 6 SWEEP — THE TEMPLATE: self-contained assertion block, re-based
        // 2026-11-21 to RING10 + TOPFANS (mirrors the sweep check block in
        // ref\gpu_rgb.py): one 1-slot yellow radar dot on RING10 (slots
        // 2..11, P10, 1 latch/frame, +i) with a 3-slot linear amber
        // afterglow wrapping the ring; TOPFANS 12/13 hold the dim amber
        // base and flare toward the head color when the head crosses slot
        // 10 (f=1.0) or 9/11 (f=0.5); slots 0..1 (live, unassigned) and
        // 14..239 (dead) stay pure black; period 10 | 120.
        // -----------------------------------------------------------------
        static void CheckSweep()
        {
            byte[] fs = SweepFrame(0);
            Tst.Assert(SlotEq(fs, 2, SweepHeadC), "sweep phase 0: head on slot 2");
            Tst.Assert(SlotEq(fs, 11, SweepTailC[0]), "sweep phase 0: tail1 wraps to slot 11");
            Tst.Assert(SlotEq(fs, 10, SweepTailC[1]), "sweep phase 0: tail2 on slot 10");
            Tst.Assert(SlotEq(fs, 9, BeconBgC), "sweep phase 0: tail3 falls into the base on slot 9");
            Tst.Assert(SlotEq(fs, 3, BeconBgC), "sweep phase 0: ring base on slot 3");
            Tst.Assert(SlotEq(fs, 12, BeconBgC), "sweep phase 0: topfan 12 holds the dim base (f=0, head on 2)");
            Tst.Assert(SlotEq(fs, 13, BeconBgC), "sweep phase 0: topfan 13 holds the dim base (f=0, head on 2)");
            Tst.Assert(SameBytes(SweepFrame(0), SweepFrame(10)),
                "sweep: period wrap fn(0) == fn(10)");
            // every frame: head advances 1 latch/frame on RING10; TOPFANS
            // flare f=1.0 (head on 10) / 0.5 (9 or 11) / 0, per-channel
            // int blend from the dim base; ring >= floor; 0/1 and 14..239
            // pure black
            for (int ph = 0; ph < PhasePeriod; ph++)
            {
                byte[] fph = SweepFrame(ph);
                int hs = Ring10[ph % 10];
                Tst.Assert(SlotEq(fph, hs, SweepHeadC),
                    string.Format("sweep phase {0}: head on slot {1}", ph, hs));
                int headCount = 0;
                int headSlot = -1;
                for (int j = 0; j < Ring10.Length; j++)
                    if (SlotEq(fph, Ring10[j], SweepHeadC)) { headCount++; headSlot = Ring10[j]; }
                Tst.Assert(headCount == 1 && headSlot == hs,
                    string.Format("sweep phase {0}: exactly one head slot on RING10 (want {1}, got {2} hit(s))",
                        ph, hs, headCount));
                double f = hs == 10 ? 1.0 : (hs == 9 || hs == 11) ? 0.5 : 0.0;
                int[] exp = new int[]
                {
                    (int)(BeconBgC[0] + f * (SweepHeadC[0] - BeconBgC[0])),
                    (int)(BeconBgC[1] + f * (SweepHeadC[1] - BeconBgC[1])),
                    (int)(BeconBgC[2] + f * (SweepHeadC[2] - BeconBgC[2]))
                };
                for (int j = 0; j < TopFans.Length; j++)
                    Tst.Assert(SlotEq(fph, TopFans[j], exp),
                        string.Format("sweep phase {0}: topfan slot {1} == base + f*({2}) head blend", ph, TopFans[j], f));
                for (int j = 0; j < Ring10.Length; j++)
                {
                    int i = Ring10[j];
                    Tst.Assert(fph[3 * i] >= BeconBgC[0]
                               && fph[3 * i + 1] >= BeconBgC[1]
                               && fph[3 * i + 2] >= BeconBgC[2],
                        string.Format("sweep phase {0}: ring slot {1} below floor (no pure black)", ph, i));
                }
                for (int i = 0; i <= 1; i++)
                    Tst.Assert(fph[3 * i] == 0 && fph[3 * i + 1] == 0 && fph[3 * i + 2] == 0,
                        string.Format("sweep phase {0}: slot {1} (live, unassigned) not black", ph, i));
                for (int i = 14; i < 240; i++)
                    Tst.Assert(fph[3 * i] == 0 && fph[3 * i + 1] == 0 && fph[3 * i + 2] == 0,
                        string.Format("sweep phase {0}: dead slot {1} not black", ph, i));
            }
            // 12/13 never dip below the floor (old "12/13 skipped/black"
            // asserts are gone — the 2026-11-16 skip decision is VOID
            // per spec)
            for (int ph = 0; ph < PhasePeriod; ph++)
            {
                byte[] fph = SweepFrame(ph);
                for (int j = 0; j < TopFans.Length; j++)
                {
                    int i = TopFans[j];
                    Tst.Assert(fph[3 * i] >= BeconBgC[0]
                               && fph[3 * i + 1] >= BeconBgC[1]
                               && fph[3 * i + 2] >= BeconBgC[2],
                        string.Format("sweep phase {0}: topfan slot {1} below floor", ph, i));
                }
            }
        }

        // -----------------------------------------------------------------
        // Patterns 1-5 (darkblue/perfan/chase_cool/chase_warm/beacon) —
        // re-based 2026-11-21 to RING10 + TOPFANS: self-contained assertion
        // blocks mirroring the check blocks in ref\gpu_rgb.py. New model:
        // ring = slots 2..11 (P10); TOPFANS 12/13 painted EVERY frame
        // (>= BEACON_BG (48,24,0) floor, never black); slots 0/1 (live,
        // unassigned) and 14..239 (dead) pure black. (The old asserts that
        // 12/13 are black are gone — the 2026-11-16 skip decision is VOID
        // per spec.)
        // -----------------------------------------------------------------

        // Common per-frame asserts for the re-based patterns 1-5 (new
        // model): frame is 720 bytes; ring slots 2..11 all >= floor (no
        // pure black); TOPFANS 12/13 >= BEACON_BG (48,24,0) per channel;
        // slots 0/1 == (0,0,0); dead slots 14..239 == (0,0,0).
        static void CheckRebasedCommon(string label, Func<int, byte[]> fn)
        {
            for (int ph = 0; ph < PhasePeriod; ph++)
            {
                byte[] fph = fn(ph);
                Tst.Assert(fph != null && fph.Length == 720,
                    string.Format("{0} phase {1}: frame length 720", label, ph));
                if (fph == null || fph.Length != 720) continue;
                // every checked frame: ring slots 2..11 no pure black
                for (int j = 0; j < Ring10.Length; j++)
                {
                    int i = Ring10[j];
                    Tst.Assert(fph[3 * i] != 0 || fph[3 * i + 1] != 0 || fph[3 * i + 2] != 0,
                        string.Format("{0} phase {1}: ring slot {2} pure black (>= floor)", label, ph, i));
                }
                // every checked frame: TOPFANS 12/13 >= (48,24,0)
                for (int j = 0; j < TopFans.Length; j++)
                {
                    int i = TopFans[j];
                    Tst.Assert(fph[3 * i] >= BeconBgC[0]
                               && fph[3 * i + 1] >= BeconBgC[1]
                               && fph[3 * i + 2] >= BeconBgC[2],
                        string.Format("{0} phase {1}: topfan slot {2} below floor (48,24,0)", label, ph, i));
                }
                // every checked frame: slots 0/1 pure black
                for (int i = 0; i <= 1; i++)
                    Tst.Assert(fph[3 * i] == 0 && fph[3 * i + 1] == 0 && fph[3 * i + 2] == 0,
                        string.Format("{0} phase {1}: slot {2} (live, unassigned) not black", label, ph, i));
                // every checked frame: dead slots 14..239 pure black
                for (int i = 14; i < 240; i++)
                    Tst.Assert(fph[3 * i] == 0 && fph[3 * i + 1] == 0 && fph[3 * i + 2] == 0,
                        string.Format("{0} phase {1}: dead slot {2} not black", label, ph, i));
            }
        }

        // 1 DARKBLUE — static: 2..11 DARK_BLUE; 12/13 static dim dark blue
        // (>= BEACON_BG floor). Mirrors the darkblue block in
        // ref\gpu_rgb.py: ring = slots 2..11; 12/13 painted; slots 0/1 and
        // 14..239 pure black.
        static void CheckDarkBlue()
        {
            CheckRebasedCommon("darkblue", DarkBlueFrame);
            byte[] fb0 = DarkBlueFrame(0);
            for (int j = 0; j < Ring10.Length; j++)
                Tst.Assert(SlotEq(fb0, Ring10[j], DarkBlueC),
                    string.Format("darkblue phase 0: ring slot {0} == DARK_BLUE", Ring10[j]));
            Tst.Assert(SlotEq(fb0, 12, DarkBlueDim), "darkblue phase 0: topfan 12 == dim dark blue");
            Tst.Assert(SlotEq(fb0, 13, DarkBlueDim), "darkblue phase 0: topfan 13 == dim dark blue");
            Tst.Assert(SameBytes(DarkBlueFrame(0), DarkBlueFrame(37)),
                "darkblue: static (fn(0) == fn(37))");
            for (int ph = 0; ph < PhasePeriod; ph++)
                Tst.Assert(SameBytes(DarkBlueFrame(0), DarkBlueFrame(ph)),
                    string.Format("darkblue phase {0}: static frame (== fn(0))", ph));
        }

        // 2 PERFAN — 6-color ring recast on RING10 (10 slots, colors
        // repeat), +i, 1 slot/frame; period 30 (LCM(10, 6) | 120); 12/13
        // static dim blue. Mirrors the perfan block in ref\gpu_rgb.py.
        static void CheckPerfan()
        {
            CheckRebasedCommon("perfan", PerfAnFrame);
            byte[] fp0 = PerfAnFrame(0);
            for (int s = 0; s < Ring10.Length; s++)
                Tst.Assert(SlotEq(fp0, Ring10[s], PerfanC[s % 6]),
                    string.Format("perfan phase 0: ring slot {0} == PERFAN[{1}]", Ring10[s], s % 6));
            Tst.Assert(SlotEq(fp0, 12, DarkBlueDim), "perfan phase 0: topfan 12 == dim blue");
            Tst.Assert(SlotEq(fp0, 13, DarkBlueDim), "perfan phase 0: topfan 13 == dim blue");
            Tst.Assert(SameBytes(PerfAnFrame(0), PerfAnFrame(30)),
                "perfan: period 30 (LCM(10, 6) | 120)");
            Tst.Assert(!SameBytes(PerfAnFrame(0), PerfAnFrame(1)),
                "perfan: it moves (fn(0) != fn(1))");
            for (int ph = 0; ph < PhasePeriod; ph++)
            {
                byte[] fph = PerfAnFrame(ph);
                for (int s = 0; s < Ring10.Length; s++)
                    Tst.Assert(SlotEq(fph, Ring10[s], PerfanC[ModFloor(s - ph, 6)]),
                        string.Format("perfan phase {0}: ring slot {1} == PERFAN[(s-ph)%6]", ph, Ring10[s]));
            }
        }

        // 3 CHASE_COOL / 4 CHASE_WARM — 3-color comet on RING10, P10, +i
        // (three 2-slot color blocks + 4 dim base slots); TOPFANS flare
        // f = max over blocks (1.0 block on 10, 0.5 on 9/11), floored to
        // the dim base per channel. Mirrors the chase block in
        // ref\gpu_rgb.py.
        static void CheckChase(string label, int[][] palette, Func<int, byte[]> fn)
        {
            CheckRebasedCommon(label, fn);
            byte[] f0 = fn(0);
            for (int s = 0; s < Ring10.Length; s++)
                Tst.Assert(SlotEq(f0, Ring10[s], ChaseSlot(s, 0, palette)),
                    string.Format("{0} phase 0: ring slot {1} == chase(s,0,palette)", label, Ring10[s]));
            Tst.Assert(SameBytes(fn(0), fn(10)), label + ": period 10 wrap (fn(0) == fn(10))");
            for (int ph = 0; ph < PhasePeriod; ph++)
            {
                byte[] fph = fn(ph);
                for (int s = 0; s < Ring10.Length; s++)
                    Tst.Assert(SlotEq(fph, Ring10[s], ChaseSlot(s, ph, palette)),
                        string.Format("{0} phase {1}: ring slot {2} == chase(s,ph,palette)", label, ph, Ring10[s]));
                int[] exp = ChaseTopfan(ph, palette);
                for (int j = 0; j < TopFans.Length; j++)
                    Tst.Assert(SlotEq(fph, TopFans[j], exp),
                        string.Format("{0} phase {1}: topfan slot {2} == _chase_topfan blend", label, ph, TopFans[j]));
            }
        }

        // 5 BEACON — 2-slot yellow bulb + 1 tail (0.5, behind) on the
        // orange base over RING10, P10, +i; TOPFANS 12/13 hold the dim
        // orange base and flare toward the yellow bulb when the bulb is on
        // slot 10 (f=1.0) or 9/11 (f=0.5). Mirrors the beacon block in
        // ref\gpu_rgb.py (which previously asserted 12/13 black — flipped
        // to the painted 12/13).
        static void CheckBeacon()
        {
            CheckRebasedCommon("beacon", BeaconFrame);
            int[] tailColor = { 255, 191, 0 };   // 0.5 blend of base -> arc
            byte[] fb = BeaconFrame(0);
            Tst.Assert(SlotEq(fb, 2, BeaconArc), "beacon phase 0: bulb slot 2 == BEACON_ARC");
            Tst.Assert(SlotEq(fb, 3, BeaconArc), "beacon phase 0: bulb slot 3 == BEACON_ARC");
            Tst.Assert(SlotEq(fb, 11, tailColor), "beacon phase 0: tail (0.5) on slot 11");
            Tst.Assert(SlotEq(fb, 4, BeaconBase), "beacon phase 0: ring base on slot 4");
            Tst.Assert(SlotEq(fb, 12, BeconBgC), "beacon phase 0: topfan 12 == BEACON_BG (f=0)");
            Tst.Assert(SlotEq(fb, 13, BeconBgC), "beacon phase 0: topfan 13 == BEACON_BG (f=0)");
            Tst.Assert(SlotEq(fb, 0, new int[] { 0, 0, 0 }), "beacon phase 0: slot 0 black");
            Tst.Assert(SlotEq(fb, 1, new int[] { 0, 0, 0 }), "beacon phase 0: slot 1 black");
            Tst.Assert(SlotEq(fb, 14, new int[] { 0, 0, 0 }), "beacon phase 0: dead slot 14 black");
            Tst.Assert(SlotEq(fb, 239, new int[] { 0, 0, 0 }), "beacon phase 0: dead slot 239 black");
            Tst.Assert(SameBytes(BeaconFrame(0), BeaconFrame(10)),
                "beacon: period 10 (fn(0) == fn(10))");
            for (int ph = 0; ph < PhasePeriod; ph++)
            {
                byte[] fph = BeaconFrame(ph);
                int b0 = Ring10[ph % 10];
                int b1 = Ring10[ModFloor(ph + 1, 10)];
                int tail = Ring10[ModFloor(ph - 1, 10)];
                Tst.Assert(SlotEq(fph, b0, BeaconArc),
                    string.Format("beacon phase {0}: bulb slot {1} == BEACON_ARC", ph, b0));
                Tst.Assert(SlotEq(fph, b1, BeaconArc),
                    string.Format("beacon phase {0}: bulb slot {1} == BEACON_ARC", ph, b1));
                Tst.Assert(SlotEq(fph, tail, tailColor),
                    string.Format("beacon phase {0}: tail slot {1} == 0.5 blend", ph, tail));
                int headCount = 0;
                for (int j = 0; j < Ring10.Length; j++)
                    if (SlotEq(fph, Ring10[j], BeaconArc)) headCount++;
                Tst.Assert(headCount == 2,
                    string.Format("beacon phase {0}: exactly two bulb slots on RING10 (got {1})", ph, headCount));
                for (int j = 0; j < Ring10.Length; j++)
                {
                    int i = Ring10[j];
                    if (i != b0 && i != b1 && i != tail)
                        Tst.Assert(SlotEq(fph, i, BeaconBase),
                            string.Format("beacon phase {0}: ring slot {1} == BEACON_BASE", ph, i));
                }
                double f = (b0 == 10 || b1 == 10)
                    ? 1.0
                    : ((b0 == 9 || b1 == 9 || b0 == 11 || b1 == 11) ? 0.5 : 0.0);
                int[] exp = new int[]
                {
                    (int)(BeconBgC[0] + f * (BeaconArc[0] - BeconBgC[0])),
                    (int)(BeconBgC[1] + f * (BeaconArc[1] - BeconBgC[1])),
                    (int)(BeconBgC[2] + f * (BeaconArc[2] - BeconBgC[2]))
                };
                for (int j = 0; j < TopFans.Length; j++)
                    Tst.Assert(SlotEq(fph, TopFans[j], exp),
                        string.Format("beacon phase {0}: topfan slot {1} == base + f*({2}) bulb blend", ph, TopFans[j], f));
                for (int i = 0; i <= 1; i++)
                    Tst.Assert(fph[3 * i] == 0 && fph[3 * i + 1] == 0 && fph[3 * i + 2] == 0,
                        string.Format("beacon phase {0}: slot {1} (live, unassigned) not black", ph, i));
                for (int i = 14; i < 240; i++)
                    Tst.Assert(fph[3 * i] == 0 && fph[3 * i + 1] == 0 && fph[3 * i + 2] == 0,
                        string.Format("beacon phase {0}: dead slot {1} not black", ph, i));
            }
        }

        // 7 TANDEM — two 2-slot amber heads counter-rotating on RING10
        // (A +i, B -i, 180 deg apart at phase 0), head leading / tail
        // trailing; re-based 2026-11-21 to RING10 + TOPFANS; period 10
        // | 120; TOPFANS flare f = max over both bulbs (1.0 on 10, 0.5
        // on 9/11). Mirrors the tandem check block in ref\gpu_rgb.py.
        static void CheckTandem()
        {
            CheckRebasedCommon("tandem", TandemFrame);
            byte[] ft0 = TandemFrame(0);
            int[] phase0Heads = new int[] { 2, 3, 7, 8 };   // A bulb 2/3, B bulb 7/8
            for (int j = 0; j < phase0Heads.Length; j++)
                Tst.Assert(SlotEq(ft0, phase0Heads[j], TandemHeadC),
                    "tandem phase 0: bulb slot " + phase0Heads[j] + " == TANDEM_HEAD");
            Tst.Assert(SlotEq(ft0, 4, BeconBgC), "tandem phase 0: gap between the bulbs on slot 4");
            Tst.Assert(SlotEq(ft0, 9, TandemTailC[0]), "tandem phase 0: slot 9 == TANDEM_TAIL[0]");
            Tst.Assert(SlotEq(ft0, 10, TandemTailC[1]), "tandem phase 0: slot 10 == TANDEM_TAIL[1]");
            Tst.Assert(SlotEq(ft0, 11, TandemTailC[0]), "tandem phase 0: slot 11 == TANDEM_TAIL[0]");
            Tst.Assert(SlotEq(ft0, 12, BeconBgC), "tandem phase 0: topfan 12 holds the dim base (f=0)");
            Tst.Assert(SlotEq(ft0, 13, BeconBgC), "tandem phase 0: topfan 13 holds the dim base (f=0)");
            Tst.Assert(SameBytes(TandemFrame(0), TandemFrame(10)), "tandem: period 10 wrap (fn(0) == fn(10))");
            Tst.Assert(!SameBytes(TandemFrame(0), TandemFrame(1)), "tandem: it moves (fn(0) != fn(1))");
            for (int ph = 0; ph < PhasePeriod; ph++)
            {
                byte[] fph = TandemFrame(ph);
                // the four bulb slots (2 per head, heads 180 deg apart)
                int[] expect = new int[]
                {
                    Ring10[ph % 10], Ring10[ModFloor(ph + 1, 10)],
                    Ring10[ModFloor(5 - ph, 10)], Ring10[ModFloor(6 - ph, 10)]
                };
                for (int j = 0; j < expect.Length; j++)
                {
                    bool seen = false;
                    for (int k2 = 0; k2 < j; k2++) seen = seen || expect[k2] == expect[j];
                    if (seen) continue;
                    Tst.Assert(SlotEq(fph, expect[j], TandemHeadC),
                        string.Format("tandem phase {0}: bulb slot {1} == TANDEM_HEAD", ph, expect[j]));
                }
                for (int j = 0; j < Ring10.Length; j++)
                {
                    bool isHead = false;
                    for (int k2 = 0; k2 < expect.Length; k2++) isHead = isHead || expect[k2] == Ring10[j];
                    if (!isHead)
                        Tst.Assert(!SlotEq(fph, Ring10[j], TandemHeadC),
                            string.Format("tandem phase {0}: non-bulb slot {1} == TANDEM_HEAD", ph, Ring10[j]));
                }
                // TOPFANS: f = max over both bulbs, int blend from the dim base
                double f = 0.0;
                int[][] bulbs = new int[][]
                {
                    new int[] { Ring10[ph % 10], Ring10[ModFloor(ph + 1, 10)] },
                    new int[] { Ring10[ModFloor(5 - ph, 10)], Ring10[ModFloor(6 - ph, 10)] }
                };
                for (int b = 0; b < bulbs.Length; b++)
                {
                    int[] bulb = bulbs[b];
                    if (bulb[0] == 10 || bulb[1] == 10) f = Math.Max(f, 1.0);
                    else if (bulb[0] == 9 || bulb[1] == 9 || bulb[0] == 11 || bulb[1] == 11) f = Math.Max(f, 0.5);
                }
                int[] exp = new int[]
                {
                    (int)(BeconBgC[0] + f * (TandemHeadC[0] - BeconBgC[0])),
                    (int)(BeconBgC[1] + f * (TandemHeadC[1] - BeconBgC[1])),
                    (int)(BeconBgC[2] + f * (TandemHeadC[2] - BeconBgC[2]))
                };
                for (int j = 0; j < TopFans.Length; j++)
                    Tst.Assert(SlotEq(fph, TopFans[j], exp),
                        string.Format("tandem phase {0}: topfan slot {1} == base + f*({2}) head blend", ph, TopFans[j], f));
            }
        }

        // 8 HEARTBEAT — breathing ring (60) on RING10 + 1-slot yellow lap
        // dot (+i, P10) + 1 yellow->ring blend slot behind; re-based
        // 2026-11-21 to RING10 + TOPFANS; TOPFANS 12/13 breathe in phase
        // with the ring; periods 60 | 120 (breath), 10 | 120 (dot).
        // Mirrors the heartbeat check block in ref\gpu_rgb.py.
        static void CheckHeartbeat()
        {
            CheckRebasedCommon("heartbeat", HeartbeatFrame);
            Tst.Assert(SameBytes(HeartbeatFrame(0), HeartbeatFrame(60)),
                "heartbeat: both periods (fn(0) == fn(60))");
            Tst.Assert(!SameBytes(HeartbeatFrame(0), HeartbeatFrame(24)),
                "heartbeat: breath differs (fn(0) != fn(24))");
            byte[] fhb0 = HeartbeatFrame(0);
            Tst.Assert(SlotEq(fhb0, 2, HeartbeatHead), "heartbeat phase 0: dot on slot 2");
            Tst.Assert(SlotEq(fhb0, 11, new int[] { 210, 160, 0 }), "heartbeat phase 0: blend behind on slot 11");
            Tst.Assert(fhb0[36] == fhb0[9] && fhb0[37] == fhb0[10] && fhb0[38] == fhb0[11],
                "heartbeat phase 0: topfan 12 in phase with ring slot 3");
            for (int ph = 0; ph < PhasePeriod; ph++)
            {
                byte[] fph = HeartbeatFrame(ph);
                double breath = 0.65 + 0.35 * Math.Sin(2.0 * Math.PI * ph / HeartbeatBreathPeriod);
                int[] ring = new int[]
                {
                    (int)(HeartbeatRing[0] * breath),
                    (int)(HeartbeatRing[1] * breath),
                    0
                };
                int dot = Ring10[ph % 10];
                int back = Ring10[ModFloor(ph - 1, 10)];
                Tst.Assert(SlotEq(fph, dot, HeartbeatHead),
                    string.Format("heartbeat phase {0}: dot on slot {1}", ph, dot));
                int[] blend = new int[] { (255 + ring[0]) / 2, (255 + ring[1]) / 2, 0 };
                Tst.Assert(SlotEq(fph, back, blend),
                    string.Format("heartbeat phase {0}: blend slot {1} == (255+ring)/2", ph, back));
                for (int j = 0; j < Ring10.Length; j++)
                {
                    if (Ring10[j] != dot && Ring10[j] != back)
                        Tst.Assert(SlotEq(fph, Ring10[j], ring),
                            string.Format("heartbeat phase {0}: ring slot {1} == breath ring color", ph, Ring10[j]));
                }
                for (int j = 0; j < TopFans.Length; j++)
                    Tst.Assert(SlotEq(fph, TopFans[j], ring),
                        string.Format("heartbeat phase {0}: topfan slot {1} breathes in phase with the ring", ph, TopFans[j]));
            }
        }

        // 9 COMET — 2-slot yellow bulb + smoothstep tail filling RING10,
        // -i; re-based 2026-11-21 to RING10 + TOPFANS; period 10 | 120;
        // TOPFANS flare f = 1.0 (bulb on 10) / 0.5 (bulb on 9 or 11).
        // Mirrors the comet check block in ref\gpu_rgb.py.
        static void CheckComet()
        {
            CheckRebasedCommon("comet", CometFrame);
            byte[] fc0 = CometFrame(0);
            Tst.Assert(SlotEq(fc0, 2, CometHeadC), "comet phase 0: bulb slot 2 == COMET_HEAD");
            Tst.Assert(SlotEq(fc0, 11, CometHeadC), "comet phase 0: bulb slot 11 == COMET_HEAD");
            Tst.Assert(SameBytes(CometFrame(0), CometFrame(10)), "comet: period 10 wrap (fn(0) == fn(10))");
            Tst.Assert(!SameBytes(CometFrame(0), CometFrame(1)), "comet: it moves (fn(0) != fn(1))");
            for (int ph = 0; ph < PhasePeriod; ph++)
            {
                byte[] fph = CometFrame(ph);
                int pos = ModFloor(-ph, 10);
                int[] bulb = new int[] { Ring10[pos], Ring10[ModFloor(pos - 1, 10)] };
                Tst.Assert(SlotEq(fph, bulb[0], CometHeadC),
                    string.Format("comet phase {0}: bulb slot {1} == COMET_HEAD", ph, bulb[0]));
                Tst.Assert(SlotEq(fph, bulb[1], CometHeadC),
                    string.Format("comet phase {0}: bulb slot {1} == COMET_HEAD", ph, bulb[1]));
                int headCount = 0;
                for (int j = 0; j < Ring10.Length; j++)
                    if (SlotEq(fph, Ring10[j], CometHeadC)) headCount++;
                Tst.Assert(headCount == 2,
                    string.Format("comet phase {0}: exactly two bulb slots on RING10 (got {1})", ph, headCount));
                for (int k = 2; k < 10; k++)
                {
                    int i = Ring10[ModFloor(pos - k, 10)];
                    Tst.Assert(SlotEq(fph, i, OnBase(CometTail(k - 1, 10))),
                        string.Format("comet phase {0}: tail slot {1} == _on_base(_comet_tail({2},10))", ph, i, k - 1));
                }
                double f = (bulb[0] == 10 || bulb[1] == 10) ? 1.0
                    : ((bulb[0] == 9 || bulb[1] == 9 || bulb[0] == 11 || bulb[1] == 11) ? 0.5 : 0.0);
                int[] exp = new int[]
                {
                    (int)(BeconBgC[0] + f * (CometHeadC[0] - BeconBgC[0])),
                    (int)(BeconBgC[1] + f * (CometHeadC[1] - BeconBgC[1])),
                    (int)(BeconBgC[2] + f * (CometHeadC[2] - BeconBgC[2]))
                };
                for (int j = 0; j < TopFans.Length; j++)
                    Tst.Assert(SlotEq(fph, TopFans[j], exp),
                        string.Format("comet phase {0}: topfan slot {1} == base + f*({2}) bulb blend", ph, TopFans[j], f));
            }
        }

        // 10 HAZARD — 2-slot head strobing 10 Hz + blocky 4-tail on
        // RING10, +i; re-based 2026-11-21 to RING10 + TOPFANS; period
        // 10 | 120 (blink is phase-parity); TOPFANS 12/13 strobe in sync
        // (even = bright amber, odd = dim base; floor = base, never
        // black). Mirrors the hazard check block in ref\gpu_rgb.py.
        static void CheckHazard()
        {
            CheckRebasedCommon("hazard", HazardFrame);
            byte[] fhz0 = HazardFrame(0);
            Tst.Assert(SlotEq(fhz0, 2, HazardHeadEven) && SlotEq(fhz0, 3, HazardHeadEven),
                "hazard phase 0: head slots 2/3 == HAZARD_HEAD_EVEN");
            Tst.Assert(SlotEq(fhz0, 11, HazardTailC[0]), "hazard phase 0: tail1 on slot 11");
            Tst.Assert(SlotEq(fhz0, 10, HazardTailC[1]), "hazard phase 0: tail2 on slot 10");
            Tst.Assert(SlotEq(fhz0, 9, HazardTailC[2]), "hazard phase 0: tail3 on slot 9");
            Tst.Assert(SlotEq(fhz0, 8, BeconBgC), "hazard phase 0: tail4 quantized to base on slot 8");
            Tst.Assert(SlotEq(fhz0, 4, BeconBgC), "hazard phase 0: ring base slot 4");
            Tst.Assert(SlotEq(fhz0, 12, HazardHeadEven) && SlotEq(fhz0, 13, HazardHeadEven),
                "hazard phase 0: topfans 12/13 strobe even (bright amber)");
            byte[] fhz1 = HazardFrame(1);
            Tst.Assert(SlotEq(fhz1, 4, HazardHeadOdd), "hazard phase 1: head slot 4 == HAZARD_HEAD_ODD");
            Tst.Assert(SlotEq(fhz1, 3, HazardHeadOdd), "hazard phase 1: head slot 3 == HAZARD_HEAD_ODD (advances +i)");
            Tst.Assert(SlotEq(fhz1, 12, BeconBgC), "hazard phase 1: 12/13 strobe odd (dim base)");
            Tst.Assert(SameBytes(HazardFrame(0), HazardFrame(10)), "hazard: period 10 (fn(0) == fn(10))");
            for (int ph = 0; ph < PhasePeriod; ph++)
            {
                byte[] fph = HazardFrame(ph);
                int[] headC = (ph % 2 == 0) ? HazardHeadEven : HazardHeadOdd;
                int[] bulb = new int[] { Ring10[ph % 10], Ring10[ModFloor(ph + 1, 10)] };
                Tst.Assert(SlotEq(fph, bulb[0], headC) && SlotEq(fph, bulb[1], headC),
                    string.Format("hazard phase {0}: bulb slots {1}/{2} == parity head", ph, bulb[0], bulb[1]));
                for (int k = 2; k < 6; k++)
                {
                    int i = Ring10[ModFloor(ph + 1 - k, 10)];
                    Tst.Assert(SlotEq(fph, i, OnBase(HazardTailC[k - 2])),
                        string.Format("hazard phase {0}: tail slot {1} == _on_base(HAZARD_TAIL[{2}])", ph, i, k - 2));
                }
                int[] exp = (ph % 2 == 0) ? HazardHeadEven : BeconBgC;
                for (int j = 0; j < TopFans.Length; j++)
                    Tst.Assert(SlotEq(fph, TopFans[j], exp),
                        string.Format("hazard phase {0}: topfan slot {1} == sync strobe (parity)", ph, TopFans[j]));
            }
        }

        // -----------------------------------------------------------------
        // Patterns 11-14 (bitclock/telegraph/fireflies/lava) — re-based
        // 2026-11-21 to RING10 + TOPFANS: self-contained assertion blocks
        // mirroring the check blocks in ref\gpu_rgb.py. New model:
        // ring = slots 2..11 (P10); TOPFANS 12/13 painted EVERY frame
        // (>= BEACON_BG (48,24,0) floor, never black); slots 0/1 (live,
        // unassigned) and 14..239 (dead) pure black. Old board-ring
        // displays (counter 0..5, board fifth fly, board near-black) are
        // DROPPED.
        // -----------------------------------------------------------------

        // 11 BITCLOCK — 6-bit counter on RING10 slots 2..7 (bit i -> slot
        // 2+i); lap flash (15-frame decay) on ring slots 8..11; board ring
        // 0..5 counter retired (0/1 black), 12/13 breathe in phase with
        // the lap (dim base -> lap color, floored to the BEACON_BG floor);
        // 14..239 dead, pure black. Mirrors the bitclock check block in
        // ref\gpu_rgb.py.
        static void CheckBitclock()
        {
            CheckRebasedCommon("bitclock", BitclockFrame);
            byte[] fb0 = BitclockFrame(0);
            for (int i = 2; i <= 7; i++)
                Tst.Assert(SlotEq(fb0, i, BitclockOff),
                    "bitclock phase 0: counter slot " + i + " == BITCLOCK_OFF (all bits off)");
            Tst.Assert(SlotEq(fb0, 8, new int[] { 0, 255, 170 }),
                "bitclock phase 0: lap slot 8 == BASE + full LAP");
            Tst.Assert(SlotEq(fb0, 11, new int[] { 0, 255, 170 }),
                "bitclock phase 0: lap slot 11 == BASE + full LAP");
            Tst.Assert(SlotEq(fb0, 12, new int[] { 48, 255, 160 }),
                "bitclock phase 0: topfan 12 == LAP floored to BEACON_BG");
            Tst.Assert(SlotEq(fb0, 13, new int[] { 48, 255, 160 }),
                "bitclock phase 0: topfan 13 == LAP floored to BEACON_BG");
            byte[] fb33 = BitclockFrame(33);          // 33 = bits 0 and 5
            Tst.Assert(SlotEq(fb33, 2, BitclockOn) && SlotEq(fb33, 7, BitclockOn),
                "bitclock phase 33: bits 0 and 5 on (slots 2 and 7)");
            Tst.Assert(SlotEq(fb33, 3, BitclockOff),
                "bitclock phase 33: bit 1 off (slot 3)");
            Tst.Assert(SlotEq(fb33, 8, BitclockBase) && SlotEq(fb33, 11, BitclockBase),
                "bitclock phase 33: lap fully decayed on slots 8..11");
            Tst.Assert(SlotEq(fb33, 12, BitclockTopBase) && SlotEq(fb33, 13, BitclockTopBase),
                "bitclock phase 33: topfans 12/13 back at the dim base");
            for (int ph = 0; ph < PhasePeriod; ph++)
            {
                byte[] fph = BitclockFrame(ph);
                int t = ph % PhasePeriod;
                for (int i = 0; i < 6; i++)
                {
                    int[] c = (((t >> i) & 1) != 0) ? BitclockOn : BitclockOff;
                    Tst.Assert(SlotEq(fph, 2 + i, c),
                        string.Format("bitclock phase {0}: bit {1} slot {2} == {3}",
                            ph, i, 2 + i, (((t >> i) & 1) != 0) ? "ON" : "OFF"));
                }
                double lap = 1.0 - Smoothstep(t / 15.0);
                int[] expLap = new int[]
                {
                    Math.Min(255, BitclockBase[0] + (int)(BitclockLap[0] * lap)),
                    Math.Min(255, BitclockBase[1] + (int)(BitclockLap[1] * lap)),
                    Math.Min(255, BitclockBase[2] + (int)(BitclockLap[2] * lap))
                };
                for (int i = 8; i <= 11; i++)
                    Tst.Assert(SlotEq(fph, i, expLap),
                        string.Format("bitclock phase {0}: lap slot {1} == BASE + int(LAP*lap)", ph, i));
                int[] expTop = OnBase(new int[]
                {
                    (int)(BitclockTopBase[0] + lap * (BitclockLap[0] - BitclockTopBase[0])),
                    (int)(BitclockTopBase[1] + lap * (BitclockLap[1] - BitclockTopBase[1])),
                    (int)(BitclockTopBase[2] + lap * (BitclockLap[2] - BitclockTopBase[2]))
                });
                for (int j = 0; j < TopFans.Length; j++)
                    Tst.Assert(SlotEq(fph, TopFans[j], expTop),
                        string.Format("bitclock phase {0}: topfan slot {1} == base + lap blend, floored", ph, TopFans[j]));
            }
        }

        // 12 TELEGRAPH — dot at ring slot 4; echo fills RING10 (10 frames
        // late); ring sits on the dim BEACON_BG base (no pure black), 12/13
        // strobe between base and echo in sync (floor = base, never black);
        // 0/1 and 14..239 pure black; period 30 | 120. Mirrors the
        // telegraph check block in ref\gpu_rgb.py.
        static void CheckTelegraph()
        {
            CheckRebasedCommon("telegraph", TelegraphFrame);
            byte[] f0 = TelegraphFrame(0);
            Tst.Assert(SlotEq(f0, 4, TelegraphDot),
                "telegraph phase 0: dot on slot 4 (on at t=0)");
            Tst.Assert(SlotEq(f0, 2, BeconBgC),
                "telegraph phase 0: ring slot 2 == BEACON_BG (echo off)");
            Tst.Assert(SlotEq(f0, 11, BeconBgC),
                "telegraph phase 0: ring slot 11 == BEACON_BG (echo off)");
            Tst.Assert(SlotEq(f0, 12, BeconBgC) && SlotEq(f0, 13, BeconBgC),
                "telegraph phase 0: topfans 12/13 == BEACON_BG (echo off)");
            byte[] f10 = TelegraphFrame(10);
            Tst.Assert(SlotEq(f10, 2, TelegraphEcho),
                "telegraph phase 10: echo on slot 2");
            Tst.Assert(SlotEq(f10, 11, TelegraphEcho),
                "telegraph phase 10: echo on slot 11");
            Tst.Assert(SlotEq(f10, 4, TelegraphDot),
                "telegraph phase 10: dot still on slot 4");
            Tst.Assert(SlotEq(f10, 12, TelegraphEcho) && SlotEq(f10, 13, TelegraphEcho),
                "telegraph phase 10: 12/13 strobe on (echo)");
            Tst.Assert(SlotEq(TelegraphFrame(25), 2, BeconBgC),
                "telegraph phase 25: echo off, unit 2 (slot 2 base)");
            Tst.Assert(SlotEq(TelegraphFrame(25), 12, BeconBgC),
                "telegraph phase 25: echo off (12 back at base)");
            for (int ph = 0; ph < PhasePeriod; ph++)
            {
                byte[] fph = TelegraphFrame(ph);
                int t = ph % PhasePeriod;
                bool echo = ModFloor(t - 10, 30) < 15;
                bool dot = (t % 30) < 15;
                for (int j = 0; j < Ring10.Length; j++)
                {
                    int i = Ring10[j];
                    if (dot && i == 4)
                        Tst.Assert(SlotEq(fph, i, TelegraphDot),
                            string.Format("telegraph phase {0}: dot slot 4 == TELEGRAPH_DOT", ph));
                    else if (echo)
                        Tst.Assert(SlotEq(fph, i, TelegraphEcho),
                            string.Format("telegraph phase {0}: echo ring slot {1} == TELEGRAPH_ECHO", ph, i));
                    else
                        Tst.Assert(SlotEq(fph, i, BeconBgC),
                            string.Format("telegraph phase {0}: ring slot {1} == BEACON_BG (echo off)", ph, i));
                }
                int[] expTop = echo ? TelegraphEcho : BeconBgC;
                for (int j = 0; j < TopFans.Length; j++)
                    Tst.Assert(SlotEq(fph, TopFans[j], expTop),
                        string.Format("telegraph phase {0}: topfan slot {1} == sync strobe (echo)", ph, TopFans[j]));
            }
        }

        // 13 FIREFLIES — four flies drifting on RING10 (bases 3/5/7/9);
        // board-ring fifth fly retired (0/1 black), 12/13 breathe in phase
        // (dim base -> fly core by the max envelope, floored to BEACON_BG);
        // 14..239 dead, pure black. Mirrors the fireflies check block in
        // ref\gpu_rgb.py.
        static void CheckFireflies()
        {
            CheckRebasedCommon("fireflies", FirefliesFrame);
            byte[] ff0 = FirefliesFrame(0);
            // t=0: fly 0 at env 0, fly 3 (off 75) fading out at env 0.5,
            // its pos exactly slot 10 -> slot 10 = base + half core; 12/13 =
            // base + 0.5*(core - base)
            Tst.Assert(SlotEq(ff0, 10, new int[] { 127, 102, 53 }),
                "fireflies phase 0: slot 10 == base + half core (fly 3 fading out)");
            Tst.Assert(SlotEq(ff0, 12, new int[] { 151, 110, 43 })
                    && SlotEq(ff0, 13, new int[] { 151, 110, 43 }),
                "fireflies phase 0: topfans 12/13 == base + 0.5*(core - base)");
            for (int j = 0; j < Ring10.Length; j++)
                Tst.Assert(!SlotEq(ff0, Ring10[j], new int[] { 0, 0, 0 }),
                    "fireflies phase 0: ring slot " + Ring10[j] + " not pure black");
            byte[] ff30 = FirefliesFrame(30);
            Tst.Assert(SlotEq(ff30, 12, FireflyCore) && SlotEq(ff30, 13, FireflyCore),
                "fireflies phase 30: topfans 12/13 == FLY core (fly 0 full)");
            for (int ph = 0; ph < PhasePeriod; ph++)
            {
                byte[] fph = FirefliesFrame(ph);
                int t = ph % PhasePeriod;
                double[] envs = new double[FireflyFlies.Length];
                for (int f = 0; f < FireflyFlies.Length; f++)
                    envs[f] = FireflyEnv(t, (int)FireflyFlies[f][3]);
                int[][] ring = new int[Ring10.Length][];
                for (int s = 0; s < Ring10.Length; s++)
                    ring[s] = new int[] { FireflyBase[0], FireflyBase[1], FireflyBase[2] };
                for (int f = 0; f < FireflyFlies.Length; f++)
                {
                    if (envs[f] <= 0) continue;
                    double pos = FireflyFlies[f][0]
                        + FireflyFlies[f][1] * Math.Sin(2.0 * Math.PI * t / PhasePeriod
                                                       + Math.PI * FireflyFlies[f][2] / 180.0);
                    for (int s = 0; s < Ring10.Length; s++)
                    {
                        double b = Tri(Ring10[s] - pos);
                        if (b > 0)
                            for (int c = 0; c < 3; c++)
                                ring[s][c] = Math.Min(255, ring[s][c] + (int)(FireflyCore[c] * b * envs[f]));
                    }
                }
                for (int s = 0; s < Ring10.Length; s++)
                    Tst.Assert(SlotEq(fph, Ring10[s], ring[s]),
                        string.Format("fireflies phase {0}: ring slot {1} == accumulated fly blend", ph, Ring10[s]));
                double maxEnv = 0.0;
                for (int f = 0; f < envs.Length; f++) maxEnv = Math.Max(maxEnv, envs[f]);
                int[] expTop = OnBase(new int[]
                {
                    (int)(BeconBgC[0] + maxEnv * (FireflyCore[0] - BeconBgC[0])),
                    (int)(BeconBgC[1] + maxEnv * (FireflyCore[1] - BeconBgC[1])),
                    (int)(BeconBgC[2] + maxEnv * (FireflyCore[2] - BeconBgC[2]))
                });
                for (int j = 0; j < TopFans.Length; j++)
                    Tst.Assert(SlotEq(fph, TopFans[j], expTop),
                        string.Format("fireflies phase {0}: topfan slot {1} == base + maxEnv blend, floored", ph, TopFans[j]));
            }
        }

        // 14 LAVA — three additive blobs drifting on RING10 (center 6.5,
        // amp 4.0); board ring 0..5 retired (0/1 black), 12/13 follow the
        // motion rule (dim base -> lava head, f = max over blobs: 1.0 on
        // slot 10, 0.5 on 9/11, floored to BEACON_BG); 14..239 dead, pure
        // black. Mirrors the lava check block in ref\gpu_rgb.py.
        static void CheckLava()
        {
            CheckRebasedCommon("lava", LavaFrame);
            byte[] fl0 = LavaFrame(0);
            Tst.Assert(SlotEq(fl0, 2, LavaBase),
                "lava phase 0: slot 2 == LAVA_BASE (no blob reaches 2)");
            Tst.Assert(SlotEq(fl0, 12, LavaHead) && SlotEq(fl0, 13, LavaHead),
                "lava phase 0: topfans 12/13 == LAVA_HEAD (blob 1 on slot 10)");
            for (int ph = 0; ph < PhasePeriod; ph++)
            {
                byte[] fph = LavaFrame(ph);
                int t = ph % PhasePeriod;
                double f = 0.0;
                for (int b = 0; b < 3; b++)
                {
                    double pos = LavaCenter + LavaAmp * Math.Sin(
                        2.0 * Math.PI * (t % LavaPeriods[b]) / LavaPeriods[b]
                        + Math.PI * LavaPhis[b] / 180.0);
                    if (Math.Abs(pos - 10) <= 0.5) f = Math.Max(f, 1.0);
                    else if (Math.Abs(pos - 9) <= 0.5 || Math.Abs(pos - 11) <= 0.5) f = Math.Max(f, 0.5);
                }
                int[] expTop = OnBase(new int[]
                {
                    (int)(LavaTopBase[0] + f * (LavaHead[0] - LavaTopBase[0])),
                    (int)(LavaTopBase[1] + f * (LavaHead[1] - LavaTopBase[1])),
                    (int)(LavaTopBase[2] + f * (LavaHead[2] - LavaTopBase[2]))
                });
                for (int j = 0; j < TopFans.Length; j++)
                    Tst.Assert(SlotEq(fph, TopFans[j], expTop),
                        string.Format("lava phase {0}: topfan slot {1} == base + f blend, floored", ph, TopFans[j]));
            }
        }

        // -----------------------------------------------------------------
        // Patterns 15-18 (sonar/metronome/orbit/bitstream) — re-based
        // 2026-11-22 to RING10 + TOPFANS: self-contained assertion blocks
        // mirroring the check blocks in ref\gpu_rgb.py. New model:
        // ring = slots 2..11 (P10); TOPFANS 12/13 painted EVERY frame
        // (>= BEACON_BG (48,24,0) floor, never black); slots 0/1 (live,
        // unassigned) and 14..239 (dead) pure black. Old board-ring
        // displays (onboard ping lamp, 3-bit tick counter, pass lamps,
        // slot 5 start-bit lamp) are DROPPED — 0/1 go black.
        // -----------------------------------------------------------------

        // 15 SONAR — ripple expands from the RING10 center (6.5) to the
        // ring edges 2/11 over 8 frames; 0.85^d falloff; ping lamp on
        // 6/7, 2 frames at launch; edge flash at t%8==7 (ring edges 2/11);
        // 12/13 dim base + f-blend flare toward cyan (f=1.0 a ripple point
        // on slot 10, 0.5 on 9/11, floored to BEACON_BG); period 8 | 120.
        // Mirrors the sonar check block in ref\gpu_rgb.py.
        static void CheckSonar()
        {
            CheckRebasedCommon("sonar", SonarFrame);
            byte[] fsn0 = SonarFrame(0);
            Tst.Assert(SlotEq(fsn0, 6, new int[] { 0, 255, 255 }),
                "sonar phase 0: slot 6 == cyan (ping + launch pulse)");
            Tst.Assert(SlotEq(fsn0, 7, new int[] { 0, 255, 255 }),
                "sonar phase 0: slot 7 == cyan (ping + launch pulse)");
            Tst.Assert(SlotEq(fsn0, 12, BeconBgC) && SlotEq(fsn0, 13, BeconBgC),
                "sonar phase 0: topfans 12/13 hold the dim base (no point on 9/10/11)");
            Tst.Assert(!SlotEq(SonarFrame(2), 6, SonarRing),
                "sonar phase 2: ping lamp off (slot 6 != SONAR_RING)");
            byte[] fsn7 = SonarFrame(7);
            Tst.Assert(SlotEq(fsn7, 2, new int[] { 255, 255, 255 }),
                "sonar phase 7: edge hit flash on slot 2");
            Tst.Assert(SlotEq(fsn7, 11, new int[] { 255, 255, 255 }),
                "sonar phase 7: edge hit flash on slot 11");
            Tst.Assert(SlotEq(SonarFrame(5), 12, new int[] { 48, 220, 255 }),
                "sonar phase 5: topfan 12 == flare f=1.0, floored");
            Tst.Assert(SameBytes(SonarFrame(0), SonarFrame(8)),
                "sonar: period 8 wrap (fn(0) == fn(8))");
            for (int ph = 0; ph < PhasePeriod; ph++)
            {
                byte[] fph = SonarFrame(ph);
                int t = ph % PhasePeriod;
                int f = t % 8;
                double r = 4.5 * f / 7.0;
                double fl = 0.0;
                double[] pts = new double[] { SonarCenter - r, SonarCenter + r };
                for (int pi = 0; pi < 2; pi++)
                {
                    int s = (int)(pts[pi] + 0.5);
                    if (s == 10) fl = Math.Max(fl, 1.0);
                    else if (s == 9 || s == 11) fl = Math.Max(fl, 0.5);
                }
                int[] expTop = OnBase(new int[]
                {
                    (int)(BeconBgC[0] + fl * (SonarRing[0] - BeconBgC[0])),
                    (int)(BeconBgC[1] + fl * (SonarRing[1] - BeconBgC[1])),
                    (int)(BeconBgC[2] + fl * (SonarRing[2] - BeconBgC[2]))
                });
                for (int j = 0; j < TopFans.Length; j++)
                    Tst.Assert(SlotEq(fph, TopFans[j], expTop),
                        string.Format("sonar phase {0}: topfan slot {1} == base + f blend, floored", ph, TopFans[j]));
            }
        }

        // 16 METRONOME — x(t) = 6.5 - 4.5*cos(2pi*t/40) swings RING10
        // with extrema every 20 frames (60 BPM) at the ring edges 2/11;
        // 2-slot Gaussian marker; white flash 3 frames + both edges at
        // each extremum; 12/13 dim base + f-blend flare toward gold
        // (f=1.0 marker on slot 10, 0.5 on 9/11, floored to BEACON_BG);
        // old 3-bit tick counter dropped (board ring retired — 0/1 black);
        // period 40 | 120. Mirrors the metronome check block in
        // ref\gpu_rgb.py.
        static void CheckMetronome()
        {
            CheckRebasedCommon("metronome", MetronomeFrame);
            byte[] fmr0 = MetronomeFrame(0);
            Tst.Assert(SlotEq(fmr0, 2, MetroAccent),
                "metronome phase 0: marker at the left edge");
            Tst.Assert(SlotEq(fmr0, 11, MetroAccent),
                "metronome phase 0: downbeat — both edges");
            Tst.Assert(SlotEq(fmr0, 12, BeconBgC) && SlotEq(fmr0, 13, BeconBgC),
                "metronome phase 0: topfans 12/13 hold the dim base (marker on 2, f=0)");
            byte[] fmr20 = MetronomeFrame(20);
            Tst.Assert(SlotEq(fmr20, 11, MetroAccent),
                "metronome phase 20: marker at the right edge");
            Tst.Assert(SlotEq(fmr20, 12, new int[] { 151, 119, 45 }),
                "metronome phase 20: topfan 12 == flare f=0.5 (on 11)");
            Tst.Assert(SameBytes(MetronomeFrame(0), MetronomeFrame(40)),
                "metronome: period 40 wrap (fn(0) == fn(40))");
            for (int ph = 0; ph < PhasePeriod; ph++)
            {
                byte[] fph = MetronomeFrame(ph);
                int t = ph % PhasePeriod;
                double x = 6.5 - 4.5 * Math.Cos(2.0 * Math.PI * (t % 40) / 40.0);
                int s = (int)(x + 0.5);
                double fl = s == 10 ? 1.0 : (s == 9 || s == 11) ? 0.5 : 0.0;
                int[] expTop = OnBase(new int[]
                {
                    (int)(BeconBgC[0] + fl * (MetroMark[0] - BeconBgC[0])),
                    (int)(BeconBgC[1] + fl * (MetroMark[1] - BeconBgC[1])),
                    (int)(BeconBgC[2] + fl * (MetroMark[2] - BeconBgC[2]))
                });
                for (int j = 0; j < TopFans.Length; j++)
                    Tst.Assert(SlotEq(fph, TopFans[j], expTop),
                        string.Format("metronome phase {0}: topfan slot {1} == base + f blend, floored", ph, TopFans[j]));
            }
        }

        // 17 ORBIT — planet core 6/7 + halo 4/5/8/9 at the RING10 center;
        // three satellites at radii 4.5/3.15/1.8 (old x1.8), one orbit
        // per 120-frame cycle; hidden behind the planet when
        // |x-6.5|<=2.5 and depth<0; old onboard pass lamps 0/2/4 dropped
        // (board ring retired — 0/1 black); 12/13 dim base + f-blend
        // flare toward the planet's white (f = max over visible
        // satellites: 1.0 on slot 10, 0.5 on 9/11, floored to
        // BEACON_BG); period 120 | 120. Mirrors the orbit check block in
        // ref\gpu_rgb.py.
        static void CheckOrbit()
        {
            CheckRebasedCommon("orbit", OrbitFrame);
            byte[] fob0 = OrbitFrame(0);
            Tst.Assert(SlotEq(fob0, 6, new int[] { 255, 255, 255 }),
                "orbit phase 0: slot 6 == planet + sat A");
            Tst.Assert(SlotEq(fob0, 7, new int[] { 255, 255, 255 }),
                "orbit phase 0: slot 7 == planet + sat A");
            Tst.Assert(SlotEq(fob0, 12, BeconBgC) && SlotEq(fob0, 13, BeconBgC),
                "orbit phase 0: topfans 12/13 hold the dim base (no satellite on 9/10/11)");
            byte[] fob40 = OrbitFrame(40);
            Tst.Assert(SlotEq(fob40, 12, new int[] { 255, 255, 240 })
                    && SlotEq(fob40, 13, new int[] { 255, 255, 240 }),
                "orbit phase 40: topfans 12/13 == planet white (sat A on slot 10: flare)");
            Tst.Assert(!SameBytes(OrbitFrame(0), OrbitFrame(1)),
                "orbit: it moves (fn(0) != fn(1))");
            for (int ph = 0; ph < PhasePeriod; ph++)
            {
                byte[] fph = OrbitFrame(ph);
                int t = ph % PhasePeriod;
                double fl = 0.0;
                for (int sidx = 0; sidx < OrbitSatColors.Length; sidx++)
                {
                    double th = 2.0 * Math.PI * t / PhasePeriod + Math.PI * OrbitSatPhis[sidx] / 180.0;
                    double x = 6.5 + OrbitSatRadii[sidx] * Math.Sin(th);
                    if (Math.Abs(x - 6.5) <= 2.5 && Math.Cos(th) < 0) continue;   // behind the planet
                    int s = (int)(x + 0.5);
                    if (s == 10) fl = Math.Max(fl, 1.0);
                    else if (s == 9 || s == 11) fl = Math.Max(fl, 0.5);
                }
                int[] expTop = OnBase(new int[]
                {
                    (int)(BeconBgC[0] + fl * (OrbitPlanet[0] - BeconBgC[0])),
                    (int)(BeconBgC[1] + fl * (OrbitPlanet[1] - BeconBgC[1])),
                    (int)(BeconBgC[2] + fl * (OrbitPlanet[2] - BeconBgC[2]))
                });
                for (int j = 0; j < TopFans.Length; j++)
                    Tst.Assert(SlotEq(fph, TopFans[j], expTop),
                        string.Format("orbit phase {0}: topfan slot {1} == base + f blend, floored", ph, TopFans[j]));
            }
        }

        // 18 BITSTREAM — 12-bit payload scrolls LEFT as 4-slot chunks
        // across RING10 (2.5 chunks visible, clipped), 1 chunk per 10
        // frames (P10), 12 shifts per 120-frame cycle = seamless lap; old
        // onboard slot 5 start-bit lamp dropped (board ring retired — 0/1
        // black): 12/13 blink as the start bit (5 on / 5 off, floored to
        // BEACON_BG); period 120 | 120. Mirrors the bitstream check block
        // in ref\gpu_rgb.py.
        static void CheckBitstream()
        {
            CheckRebasedCommon("bitstream", BitstreamFrame);
            byte[] fsb0 = BitstreamFrame(0);
            Tst.Assert(SlotEq(fsb0, 2, BitstreamZero) && SlotEq(fsb0, 3, BitstreamZero),
                "bitstream phase 0: slots 2/3 == BITSTREAM_ZERO (bit 0)");
            Tst.Assert(SlotEq(fsb0, 4, BitstreamOne) && SlotEq(fsb0, 5, BitstreamOne),
                "bitstream phase 0: slots 4/5 == BITSTREAM_ONE (bit 1, o<2)");
            Tst.Assert(SlotEq(fsb0, 6, BitstreamDim) && SlotEq(fsb0, 7, BitstreamDim),
                "bitstream phase 0: slots 6/7 == BITSTREAM_DIM (bit 1, o>=2)");
            Tst.Assert(SlotEq(fsb0, 8, BitstreamZero) && SlotEq(fsb0, 9, BitstreamZero)
                    && SlotEq(fsb0, 10, BitstreamZero) && SlotEq(fsb0, 11, BitstreamZero),
                "bitstream phase 0: slots 8..11 == BITSTREAM_ZERO (bit 2)");
            Tst.Assert(SlotEq(fsb0, 12, new int[] { 48, 255, 220 })
                    && SlotEq(fsb0, 13, new int[] { 48, 255, 220 }),
                "bitstream phase 0: topfans 12/13 == start bit on, floored");
            Tst.Assert(SlotEq(BitstreamFrame(5), 12, BeconBgC),
                "bitstream phase 5: topfan 12 == BEACON_BG (start bit off)");
            Tst.Assert(SlotEq(BitstreamFrame(10), 2, BitstreamDim),
                "bitstream phase 10: slot 2 == BITSTREAM_DIM (shifted 1 chunk)");
            for (int ph = 0; ph < PhasePeriod; ph++)
            {
                byte[] fph = BitstreamFrame(ph);
                int t = ph % PhasePeriod;
                int step = (t / 10) % 12;
                for (int j = 0; j < Ring10.Length; j++)
                {
                    int slot = Ring10[j];
                    char bit = BitstreamPayload[(slot / 4 + step) % 12];
                    int oo = slot % 4;
                    int[] c = bit == '1' ? ((oo < 2) ? BitstreamOne : BitstreamDim) : BitstreamZero;
                    Tst.Assert(SlotEq(fph, slot, c),
                        string.Format("bitstream phase {0}: ring slot {1} == payload chunk", ph, slot));
                }
                int[] top = (t % 10 < 5) ? OnBase(BitstreamLamp) : BeconBgC;
                for (int j = 0; j < TopFans.Length; j++)
                    Tst.Assert(SlotEq(fph, TopFans[j], top),
                        string.Format("bitstream phase {0}: topfan slot {1} == start-bit blink, floored", ph, TopFans[j]));
            }
        }

        // 19 SPARKLER — bursts at 7/9/11 (t=0/40/80); 4 sparks per side
        // fly outward 1 slot/frame over 12 frames (inner SPARK_A -> outer
        // SPARK_B); fuse countdown on 12/13 at 35..39; burst flare f=0.5
        // on bursts 9/11, 0 on 7; three dim dots on the pads over the
        // BEACON_BG base; re-based 2026-11-21 to RING10 + TOPFANS (old
        // slot-5 fuse moved to 12/13); 0/1 and 14..239 pure black; period
        // 40 | 120. Mirrors the sparkler check block in ref\gpu_rgb.py.
        static void CheckSparkler()
        {
            CheckRebasedCommon("sparkler", SparklerFrame);
            byte[] f0 = SparklerFrame(0);
            Tst.Assert(SlotEq(f0, 7, SparkFlash), "sparkler phase 0: burst 7 white flash");
            Tst.Assert(SlotEq(f0, 8, SparkA), "sparkler phase 0: leading spark (j=0) at 8");
            Tst.Assert(SlotEq(f0, 3, SparkB), "sparkler phase 0: outermost tip spark (j=3) at 3");
            Tst.Assert(SlotEq(f0, 2, BeconBgC), "sparkler phase 0: plain ring slot (no dot)");
            Tst.Assert(SlotEq(f0, 12, BeconBgC) && SlotEq(f0, 13, BeconBgC),
                "sparkler phase 0: topfans 12/13 hold the dim base");
            byte[] f20 = SparklerFrame(20);
            Tst.Assert(SlotEq(f20, 7, SparkDot) && SlotEq(f20, 9, SparkDot) && SlotEq(f20, 11, SparkDot),
                "sparkler phase 20: three dim dots at the pads");
            Tst.Assert(SlotEq(f20, 2, BeconBgC), "sparkler phase 20: no dot off the pads");
            Tst.Assert(SlotEq(f20, 12, BeconBgC), "sparkler phase 20: fuse off between bursts");
            Tst.Assert(SlotEq(SparklerFrame(35), 12, SparkFuse),
                "sparkler phase 35: fuse countdown on 12/13");
            Tst.Assert(SlotEq(SparklerFrame(40), 9, SparkFlash), "sparkler phase 40: burst 9");
            Tst.Assert(SlotEq(SparklerFrame(40), 12, new int[] { 151, 139, 127 }),
                "sparkler phase 40: topfan 12 == f=0.5 burst flare");
            Tst.Assert(SlotEq(SparklerFrame(80), 11, SparkFlash), "sparkler phase 80: burst 11");
            Tst.Assert(SlotEq(SparklerFrame(80), 12, new int[] { 151, 139, 127 }),
                "sparkler phase 80: topfan 12 == f=0.5 burst flare");
            for (int ph = 0; ph < PhasePeriod; ph++)
            {
                byte[] fph = SparklerFrame(ph);
                for (int j = 0; j < Ring10.Length; j++)
                {
                    int i = Ring10[j];
                    Tst.Assert(fph[3 * i] >= BeconBgC[0] && fph[3 * i + 1] >= BeconBgC[1]
                               && fph[3 * i + 2] >= BeconBgC[2],
                        string.Format("sparkler phase {0}: ring slot {1} below floor (no pure black)", ph, i));
                }
            }
        }

        // 20 ECHO — core bounces 2 -> 11 -> 2 in 20 frames; ghost 2 (lag
        // 10, 4-slot, 25%) then ghost 1 (lag 5, 2-slot, 50%); wall flash
        // at u<3 (slot 2) / 10<=u<13 (slot 11); topfans dim base + f-
        // blend toward the core (f=1.0 on 10, 0.5 on 9/11); re-based
        // 2026-11-21 to RING10 + TOPFANS; 0/1 and 14..239 pure black;
        // period 20 | 120. Mirrors the echo check block in ref\gpu_rgb.py.
        static void CheckEcho()
        {
            CheckRebasedCommon("echo", EchoFrame);
            byte[] fe0 = EchoFrame(0);
            Tst.Assert(SlotEq(fe0, 2, EchoCore), "echo phase 0: core at the left edge");
            Tst.Assert(SlotEq(fe0, 6, EchoG1) && SlotEq(fe0, 7, EchoG1),
                "echo phase 0: ghost 1 (50%) on slots 6/7");
            Tst.Assert(SlotEq(fe0, 10, EchoG2) && SlotEq(fe0, 11, EchoG2),
                "echo phase 0: ghost 2 (25%) on slots 10/11");
            Tst.Assert(SlotEq(fe0, 12, BeconBgC) && SlotEq(fe0, 13, BeconBgC),
                "echo phase 0: topfans 12/13 hold the dim base (core on 2)");
            byte[] fe10 = EchoFrame(10);
            Tst.Assert(SlotEq(fe10, 11, EchoCore), "echo phase 10: core at the right edge");
            Tst.Assert(SlotEq(fe10, 2, EchoG2), "echo phase 10: ghost 2 at the left edge");
            Tst.Assert(SlotEq(fe10, 6, EchoG1), "echo phase 10: ghost 1 mid-ring");
            Tst.Assert(SlotEq(fe10, 12, new int[] { 151, 139, 127 }),
                "echo phase 10: topfan 12 == f=0.5 (core on slot 11)");
            Tst.Assert(SameBytes(EchoFrame(0), EchoFrame(20)),
                "echo: period 20 wrap (fn(0) == fn(20))");
            for (int ph = 0; ph < PhasePeriod; ph++)
            {
                byte[] fph = EchoFrame(ph);
                int u = ph % 20;
                double p = EchoPos(u);
                int c = (int)(p + 0.5);
                double f = c == 10 ? 1.0 : (c == 9 || c == 11) ? 0.5 : 0.0;
                int[] expTop = new int[]
                {
                    (int)(BeconBgC[0] + f * (EchoCore[0] - BeconBgC[0])),
                    (int)(BeconBgC[1] + f * (EchoCore[1] - BeconBgC[1])),
                    (int)(BeconBgC[2] + f * (EchoCore[2] - BeconBgC[2]))
                };
                for (int j = 0; j < TopFans.Length; j++)
                {
                    int slot = TopFans[j];
                    Tst.Assert(SlotEq(fph, slot, expTop),
                        string.Format("echo phase {0}: topfan slot {1} == base + f*{2} core blend", ph, slot, f));
                }
                for (int j = 0; j < Ring10.Length; j++)
                {
                    int i = Ring10[j];
                    Tst.Assert(fph[3 * i] >= BeconBgC[0] && fph[3 * i + 1] >= BeconBgC[1]
                               && fph[3 * i + 2] >= BeconBgC[2],
                        string.Format("echo phase {0}: ring slot {1} below floor (no pure black)", ph, i));
                }
            }
        }

        // 21 ECG — Q dip + R spike + T bump sweep the 30-unit period
        // across RING10 (o advances 1 unit/frame, 30 | 120); the old
        // rate lamp re-based onto 12/13 as an f-blend toward ECG_R
        // (f=1.0 peak crossing slot 10, 0.5 on 9/11); re-based
        // 2026-11-21 to RING10 + TOPFANS; 0/1 and 14..239 pure black.
        // Mirrors the ecg check block in ref\gpu_rgb.py.
        static void CheckEcg()
        {
            CheckRebasedCommon("ecg", EcgFrame);
            byte[] fe0 = EcgFrame(0);
            Tst.Assert(SlotEq(fe0, 2, BeconBgC), "ecg phase 0: slot 2 == BEACON_BG");
            Tst.Assert(SlotEq(fe0, 10, new int[] { 48, 60, 40 }),
                "ecg phase 0: slot 10 == Q dip on base (48,60,40)");
            Tst.Assert(SlotEq(fe0, 11, new int[] { 85, 85, 85 }),
                "ecg phase 0: slot 11 == R falloff tail at u=11");
            Tst.Assert(SlotEq(fe0, 12, BeconBgC) && SlotEq(fe0, 13, BeconBgC),
                "ecg phase 0: topfans 12/13 hold the dim base");
            Tst.Assert(SlotEq(EcgFrame(27), 9, EcgR), "ecg phase 27: R peak on slot 9");
            Tst.Assert(SlotEq(EcgFrame(27), 12, new int[] { 151, 139, 127 }),
                "ecg phase 27: topfan 12 == f=0.5 (peak crossing 9)");
            Tst.Assert(SlotEq(EcgFrame(28), 10, EcgR), "ecg phase 28: R peak on slot 10");
            Tst.Assert(SlotEq(EcgFrame(28), 12, EcgR),
                "ecg phase 28: topfan 12 == ECG_R (peak on 10: f=1.0)");
            for (int ph = 0; ph < PhasePeriod; ph++)
            {
                byte[] fph = EcgFrame(ph);
                double peak = ModFloorD((ph % 30) * (EcgSpacing / 30.0) + EcgPeakU, EcgSpacing);
                double f = (10.0 <= peak && peak < 11.0) ? 1.0
                    : ((9.0 <= peak && peak < 10.0) || (11.0 <= peak && peak < 12.0)) ? 0.5 : 0.0;
                int[] expTop = new int[]
                {
                    (int)(BeconBgC[0] + f * (EcgR[0] - BeconBgC[0])),
                    (int)(BeconBgC[1] + f * (EcgR[1] - BeconBgC[1])),
                    (int)(BeconBgC[2] + f * (EcgR[2] - BeconBgC[2]))
                };
                for (int j = 0; j < TopFans.Length; j++)
                {
                    int slot = TopFans[j];
                    Tst.Assert(SlotEq(fph, slot, expTop),
                        string.Format("ecg phase {0}: topfan slot {1} == base + f*{2} ECG_R blend", ph, slot, f));
                }
                for (int j = 0; j < Ring10.Length; j++)
                {
                    int i = Ring10[j];
                    Tst.Assert(fph[3 * i] >= BeconBgC[0] && fph[3 * i + 1] >= BeconBgC[1]
                               && fph[3 * i + 2] >= BeconBgC[2],
                        string.Format("ecg phase {0}: ring slot {1} below floor (no pure black)", ph, i));
                }
            }
        }

        // 22 WALK — body sweeps 2 -> 11 -> 2 (45+15+45), head 1 ahead
        // clamped 2..11, blink 20..27, look-around frozen at the right
        // (45..60) and left (105..120) edges; topfans dim base + f-
        // blend toward the head (f=1.0 head on 10, 0.5 on 9/11); the
        // old companion (slots 0/3) is dropped; re-based 2026-11-21 to
        // RING10 + TOPFANS; 0/1 and 14..239 pure black. Mirrors the
        // walk check block in ref\gpu_rgb.py.
        static void CheckWalk()
        {
            CheckRebasedCommon("walk", WalkFrame);
            byte[] fw0 = WalkFrame(0);
            Tst.Assert(SlotEq(fw0, 2, WalkBody), "walk phase 0: body on slot 2");
            Tst.Assert(SlotEq(fw0, 3, WalkHead), "walk phase 0: head on slot 3");
            Tst.Assert(SlotEq(fw0, 12, BeconBgC) && SlotEq(fw0, 13, BeconBgC),
                "walk phase 0: topfans 12/13 hold the dim base (head on 3)");
            Tst.Assert(SlotEq(WalkFrame(22), 7, WalkHeadBlink),
                "walk phase 22: mid-leg blink on slot 7");
            byte[] fw50 = WalkFrame(50);
            Tst.Assert(SlotEq(fw50, 11, WalkBody), "walk phase 50: body on slot 11 (frozen)");
            Tst.Assert(SlotEq(fw50, 10, WalkHeadLook),
                "walk phase 50: head on slot 10 looking around");
            Tst.Assert(SlotEq(fw50, 12, WalkHead),
                "walk phase 50: topfan 12 == WALK_HEAD (head on 10: f=1.0)");
            byte[] fw110 = WalkFrame(110);
            Tst.Assert(SlotEq(fw110, 2, WalkBody) && SlotEq(fw110, 3, WalkHeadLook),
                "walk phase 110: body 2 + head 3 (frozen at the left edge)");
            Tst.Assert(SlotEq(fw110, 12, BeconBgC) && SlotEq(fw110, 13, BeconBgC),
                "walk phase 110: topfans 12/13 hold the dim base (head on 3)");
            for (int ph = 0; ph < PhasePeriod; ph++)
            {
                byte[] fph = WalkFrame(ph);
                for (int j = 0; j < Ring10.Length; j++)
                {
                    int i = Ring10[j];
                    Tst.Assert(fph[3 * i] >= BeconBgC[0] && fph[3 * i + 1] >= BeconBgC[1]
                               && fph[3 * i + 2] >= BeconBgC[2],
                        string.Format("walk phase {0}: ring slot {1} below floor (no pure black)", ph, i));
                }
                for (int j = 0; j < TopFans.Length; j++)
                {
                    int i = TopFans[j];
                    Tst.Assert(fph[3 * i] >= BeconBgC[0] && fph[3 * i + 1] >= BeconBgC[1]
                               && fph[3 * i + 2] >= BeconBgC[2],
                        string.Format("walk phase {0}: topfan slot {1} below floor (12/13 never black)", ph, i));
                }
            }
        }

        // -----------------------------------------------------------------
        // 23-25 GPU SPOTLIGHT (subject family): 12/13 are the subject —
        // the palette loop at the exact phase; everything else pure black
        // (user 2026-11-22: spotlight states light ONLY 12/13 — RING10
        // 2..11, 0/1, dead 14..239 all black; the 2026-11-21 RING10 wash
        // is removed). Mirrors the 23-25 check block in ref\gpu_rgb.py.
        // -----------------------------------------------------------------
        static void CheckGpuSpotlight()
        {
            string[] names = new string[]
            {
                "gpu-spotlight-idle", "gpu-spotlight-fire",
                "gpu-spotlight-somewhat-active"
            };
            Func<int, byte[]>[] fns = new Func<int, byte[]>[]
            {
                GpuSpotlightIdleFrame, GpuSpotlightFireFrame,
                GpuSpotlightSomewhatActiveFrame
            };
            int[][][] pals = new int[][][]
            {
                SpotIdlePalette, SpotFirePalette, SpotActivePalette
            };
            int[] periods = new int[]
            {
                PhasePeriod, SpotFirePeriod, SpotActivePeriod
            };
            for (int s = 0; s < fns.Length; s++)
            {
                for (int ph = 0; ph < PhasePeriod; ph++)
                {
                    byte[] fph = fns[s](ph);
                    int[] c = SpotColor(pals[s], ph, periods[s]);
                    Tst.Assert(SlotEq(fph, 12, c) && SlotEq(fph, 13, c),
                        string.Format("{0} phase {1}: topfans 12/13 == palette loop color", names[s], ph));
                    for (int j = 0; j < Ring10.Length; j++)
                    {
                        int i = Ring10[j];
                        Tst.Assert(fph[3 * i] == 0 && fph[3 * i + 1] == 0 && fph[3 * i + 2] == 0,
                            string.Format("{0} phase {1}: ring slot {2} not black (spotlight states light ONLY 12/13)",
                                names[s], ph, i));
                    }
                    for (int i = 0; i <= 1; i++)
                        Tst.Assert(fph[3 * i] == 0 && fph[3 * i + 1] == 0 && fph[3 * i + 2] == 0,
                            string.Format("{0} phase {1}: slot {2} (live, unassigned) not black",
                                names[s], ph, i));
                    for (int i = 14; i < 240; i++)
                        Tst.Assert(fph[3 * i] == 0 && fph[3 * i + 1] == 0 && fph[3 * i + 2] == 0,
                            string.Format("{0} phase {1}: dead slot {2} not black",
                                names[s], ph, i));
                }
            }
            // palette anchors land at their phases (subject; ring stays black)
            Tst.Assert(SlotEq(GpuSpotlightIdleFrame(0), 12, SpotIdlePalette[0]),
                "gpu-spotlight-idle phase 0: slot 12 == SPOT_IDLE_PALETTE[0]");
            Tst.Assert(SlotEq(GpuSpotlightIdleFrame(0), 2, new int[] { 0, 0, 0 }),
                "gpu-spotlight-idle phase 0: slot 2 black (ring stays black)");
            Tst.Assert(SlotEq(GpuSpotlightIdleFrame(40), 13, SpotIdlePalette[1]),
                "gpu-spotlight-idle phase 40: slot 13 == SPOT_IDLE_PALETTE[1]");
            Tst.Assert(SlotEq(GpuSpotlightIdleFrame(80), 12, SpotIdlePalette[2]),
                "gpu-spotlight-idle phase 80: slot 12 == SPOT_IDLE_PALETTE[2]");
            Tst.Assert(SlotEq(GpuSpotlightFireFrame(0), 12, SpotFirePalette[0]),
                "gpu-spotlight-fire phase 0: slot 12 == SPOT_FIRE_PALETTE[0]");
            Tst.Assert(SlotEq(GpuSpotlightFireFrame(0), 3, new int[] { 0, 0, 0 }),
                "gpu-spotlight-fire phase 0: slot 3 black (ring stays black)");
            Tst.Assert(SlotEq(GpuSpotlightFireFrame(5), 13, SpotFirePalette[1]),
                "gpu-spotlight-fire phase 5: slot 13 == SPOT_FIRE_PALETTE[1]");
            Tst.Assert(SlotEq(GpuSpotlightFireFrame(10), 12, SpotFirePalette[2]),
                "gpu-spotlight-fire phase 10: slot 12 == SPOT_FIRE_PALETTE[2]");
            Tst.Assert(SlotEq(GpuSpotlightSomewhatActiveFrame(0), 12, SpotActivePalette[0]),
                "gpu-spotlight-somewhat-active phase 0: slot 12 == SPOT_ACTIVE_PALETTE[0]");
            Tst.Assert(SlotEq(GpuSpotlightSomewhatActiveFrame(10), 13, SpotActivePalette[1]),
                "gpu-spotlight-somewhat-active phase 10: slot 13 == SPOT_ACTIVE_PALETTE[1]");
            Tst.Assert(SlotEq(GpuSpotlightSomewhatActiveFrame(20), 12, SpotActivePalette[2]),
                "gpu-spotlight-somewhat-active phase 20: slot 12 == SPOT_ACTIVE_PALETTE[2]");
        }

        // -----------------------------------------------------------------
        // 26 red_white: static — RING10 2..11 pure red, 12/13 white;
        // 0/1 live-unassigned black; 14..239 dead. Re-based 2026-11-21 to
        // RING10 + TOPFANS (old 6..11 span extended to the full 10-slot
        // ring). Mirrors the 26 check block in ref\gpu_rgb.py.
        // -----------------------------------------------------------------
        static void CheckRedWhite()
        {
            int[] phases = new int[] { 0, 1, 59, 60, 119 };
            for (int pi = 0; pi < phases.Length; pi++)
            {
                int ph = phases[pi];
                byte[] fph = RedWhiteFrame(ph);
                for (int j = 0; j < Ring10.Length; j++)
                {
                    int i = Ring10[j];
                    Tst.Assert(SlotEq(fph, i, RedWhiteRed),
                        string.Format("red_white phase {0}: ring slot {1} == RED_WHITE_RED (pure red)",
                            ph, i));
                }
                Tst.Assert(SlotEq(fph, 12, RedWhiteWhite),
                    string.Format("red_white phase {0}: slot 12 == RED_WHITE_WHITE (pure white)", ph));
                Tst.Assert(SlotEq(fph, 13, RedWhiteWhite),
                    string.Format("red_white phase {0}: slot 13 == RED_WHITE_WHITE (pure white)", ph));
                for (int i = 0; i <= 1; i++)
                    Tst.Assert(fph[3 * i] == 0 && fph[3 * i + 1] == 0 && fph[3 * i + 2] == 0,
                        string.Format("red_white phase {0}: slot {1} (live, unassigned) not black",
                            ph, i));
                for (int i = 14; i < 240; i++)
                    Tst.Assert(fph[3 * i] == 0 && fph[3 * i + 1] == 0 && fph[3 * i + 2] == 0,
                        string.Format("red_white phase {0}: dead slot {1} not black",
                            ph, i));
            }
        }

        // -----------------------------------------------------------------
        // 27 rainbow — re-based 2026-11-23 to RING10 + TOPFANS: mirrors
        // the rainbow check block in ref\gpu_rgb.py. The 12/13 design is
        // the six-color wheel cycle (not a BEACON_BG blend), so the
        // per-frame loop mirrors the ref exactly: ring 2..11 + 12/13
        // never pure black; 0/1 and 14..239 pure black; motion asserts
        // re-derived for the 10-slot geometry (one slot position per 20
        // frames, one full rotation per 120-frame phase).
        // -----------------------------------------------------------------
        static void CheckRainbow()
        {
            byte[] fr0 = RainbowFrame(0);
            for (int s = 0; s < Ring10.Length; s++)
                Tst.Assert(SlotEq(fr0, Ring10[s], RainbowColors[s % RainbowColors.Length]),
                    string.Format("rainbow phase 0: ring slot {0} == wheel color {1}",
                        Ring10[s], s % RainbowColors.Length));
            Tst.Assert(SlotEq(fr0, 12, RainbowColors[0]) && SlotEq(fr0, 13, RainbowColors[0]),
                "rainbow phase 0: 12/13 hold wheel color 0 (red)");
            for (int i = 0; i <= 1; i++)
                Tst.Assert(SlotEq(fr0, i, new int[] { 0, 0, 0 }),
                    "rainbow phase 0: slot " + i + " black");
            // clockwise rotation: the red anchor moves +1 slot position
            // per 20 frames; one full rotation per 120-frame phase
            for (int s = 0; s < 10; s++)
                Tst.Assert(SlotEq(RainbowFrame(20 * s), Ring10[s], RainbowColors[0]),
                    string.Format("rainbow phase {0}: red anchor on ring slot {1}",
                        20 * s, Ring10[s]));
            Tst.Assert(SameBytes(RainbowFrame(0), RainbowFrame(120)),
                "rainbow: seamless at 120");
            // exact lerp midpoint (ring slot 2 at t=10: midway between
            // violet -> red)
            Tst.Assert(SlotEq(RainbowFrame(10), 2, new int[] { 207, 0, 127 }),
                "rainbow phase 10: ring slot 2 == (207,0,127) lerp midpoint");
            // every frame: ring 2..11 + 12/13 all fully lit (no blink —
            // liveness), 0/1 pure black, 14..239 dead
            for (int ph = 0; ph < PhasePeriod; ph++)
            {
                byte[] fph = RainbowFrame(ph);
                for (int j = 0; j < Ring10.Length; j++)
                {
                    int i = Ring10[j];
                    Tst.Assert(fph[3 * i] != 0 || fph[3 * i + 1] != 0 || fph[3 * i + 2] != 0,
                        string.Format("rainbow phase {0}: ring slot {1} pure black (liveness)", ph, i));
                }
                for (int j = 0; j < TopFans.Length; j++)
                {
                    int i = TopFans[j];
                    Tst.Assert(fph[3 * i] != 0 || fph[3 * i + 1] != 0 || fph[3 * i + 2] != 0,
                        string.Format("rainbow phase {0}: topfan slot {1} pure black (liveness)", ph, i));
                }
                for (int i = 0; i <= 1; i++)
                    Tst.Assert(fph[3 * i] == 0 && fph[3 * i + 1] == 0 && fph[3 * i + 2] == 0,
                        string.Format("rainbow phase {0}: slot {1} (live, unassigned) not black", ph, i));
                for (int i = 14; i < 240; i++)
                    Tst.Assert(fph[3 * i] == 0 && fph[3 * i + 1] == 0 && fph[3 * i + 2] == 0,
                        string.Format("rainbow phase {0}: dead slot {1} not black", ph, i));
            }
            // 12/13 one color at a time: anchor at start of hold, still
            // holding at last hold frame, first fade frame exactly at
            // hold color
            for (int s = 0; s < 6; s++)
            {
                Tst.Assert(SlotEq(RainbowFrame(20 * s), 12, RainbowColors[s]),
                    string.Format("rainbow phase {0}: 12/13 anchor color {1}", 20 * s, s));
                Tst.Assert(SlotEq(RainbowFrame(20 * s + RainbowHold - 1), 13, RainbowColors[s]),
                    string.Format("rainbow phase {0}: 12/13 last hold frame color {1}",
                        20 * s + RainbowHold - 1, s));
                Tst.Assert(SlotEq(RainbowFrame(20 * s + RainbowHold), 12, RainbowColors[s]),
                    string.Format("rainbow phase {0}: 12/13 first fade frame color {1}",
                        20 * s + RainbowHold, s));
            }
            // smoothness: no ring/topfan slot jumps more than 1/8 of
            // full range per frame anywhere in the 120-frame phase
            // (no blink)
            byte[] prev = RainbowFrame(0);
            for (int ph = 1; ph <= PhasePeriod; ph++)
            {
                byte[] cur = RainbowFrame(ph % PhasePeriod);
                for (int j = 0; j < Ring10.Length; j++)
                    for (int ch = 0; ch < 3; ch++)
                        Tst.Assert(Math.Abs(cur[3 * Ring10[j] + ch] - prev[3 * Ring10[j] + ch]) <= 32,
                            string.Format("rainbow phase {0}: ring slot {1} jumps > 32 (smoothness)",
                                ph, Ring10[j]));
                for (int j = 0; j < TopFans.Length; j++)
                    for (int ch = 0; ch < 3; ch++)
                        Tst.Assert(Math.Abs(cur[3 * TopFans[j] + ch] - prev[3 * TopFans[j] + ch]) <= 32,
                            string.Format("rainbow phase {0}: topfan slot {1} jumps > 32 (smoothness)",
                                ph, TopFans[j]));
                prev = cur;
            }
        }

        // -----------------------------------------------------------------
        // 28 limelight — re-based 2026-11-23 to RING10 + TOPFANS:
        // mirrors the limelight check block in ref\gpu_rgb.py (12/13 star
        // breaths GPU_FLOOR->GPU_TOP over 60 frames; RING10 inverse-
        // breathing FAN_FLOOR<->FAN_TOP backdrop; period 60 | 120
        // bit-exact; liveness + no-blink smoothness).
        // -----------------------------------------------------------------
        static void CheckLimelight()
        {
            CheckRebasedCommon("limelight", LimelightFrame);
            Tst.Assert(LimelightPeriod == 60 && PhasePeriod % LimelightPeriod == 0,
                "limelight: period 60 | 120");
            byte[] fl15 = LimelightFrame(15);
            Tst.Assert(SlotEq(fl15, 12, LimelightGpuTop) && SlotEq(fl15, 13, LimelightGpuTop),
                "limelight t=15: 12/13 star peak (b=1) == GPU_TOP");
            for (int j = 0; j < Ring10.Length; j++)
                Tst.Assert(SlotEq(fl15, Ring10[j], LimelightFanFloor),
                    string.Format("limelight t=15: ring slot {0} at floor (b=1)", Ring10[j]));
            byte[] fl45 = LimelightFrame(45);
            Tst.Assert(SlotEq(fl45, 12, LimelightGpuFloor) && SlotEq(fl45, 13, LimelightGpuFloor),
                "limelight t=45: 12/13 star trough (b=0) == GPU_FLOOR");
            for (int j = 0; j < Ring10.Length; j++)
                Tst.Assert(SlotEq(fl45, Ring10[j], LimelightFanTop),
                    string.Format("limelight t=45: ring slot {0} at top (b=0)", Ring10[j]));
            byte[] fl0 = LimelightFrame(0);
            Tst.Assert(SlotEq(fl0, 12, new int[] { 172, 157, 135 }),
                "limelight t=0: 12/13 == (172,157,135) half-breath on the star");
            Tst.Assert(SlotEq(fl0, 2, new int[] { 19, 30, 90 }),
                "limelight t=0: ring slot 2 == (19,30,90) half-breath on the wash");
            for (int i = 0; i <= 1; i++)
                Tst.Assert(SlotEq(fl0, i, new int[] { 0, 0, 0 }),
                    "limelight t=0: slot " + i + " black");
            // period 60: bit-identical
            for (int ph = 0; ph < PhasePeriod; ph++)
                Tst.Assert(SameBytes(LimelightFrame(ph), LimelightFrame((ph + 60) % PhasePeriod)),
                    string.Format("limelight: period 60 (fn({0}) == fn({1}))",
                        ph, (ph + 60) % PhasePeriod));
            // liveness + smoothness: no blink; every 2..13 slot within
            // 1/8 full range of the previous frame
            byte[] prev = LimelightFrame(0);
            for (int ph = 1; ph <= PhasePeriod; ph++)
            {
                byte[] fph = LimelightFrame(ph % PhasePeriod);
                for (int j = 0; j < Ring10.Length; j++)
                {
                    int i = Ring10[j];
                    Tst.Assert(fph[3 * i] != 0 || fph[3 * i + 1] != 0 || fph[3 * i + 2] != 0,
                        string.Format("limelight phase {0}: ring slot {1} pure black (liveness)", ph, i));
                }
                for (int j = 0; j < TopFans.Length; j++)
                {
                    int i = TopFans[j];
                    Tst.Assert(fph[3 * i] != 0 || fph[3 * i + 1] != 0 || fph[3 * i + 2] != 0,
                        string.Format("limelight phase {0}: topfan slot {1} pure black (liveness)", ph, i));
                }
                for (int j = 0; j < Ring10.Length; j++)
                    for (int ch = 0; ch < 3; ch++)
                        Tst.Assert(Math.Abs(fph[3 * Ring10[j] + ch] - prev[3 * Ring10[j] + ch]) <= 32,
                            string.Format("limelight phase {0}: ring slot {1} jumps > 32 (smoothness)",
                                ph, Ring10[j]));
                for (int j = 0; j < TopFans.Length; j++)
                    for (int ch = 0; ch < 3; ch++)
                        Tst.Assert(Math.Abs(fph[3 * TopFans[j] + ch] - prev[3 * TopFans[j] + ch]) <= 32,
                            string.Format("limelight phase {0}: topfan slot {1} jumps > 32 (smoothness)",
                                ph, TopFans[j]));
                prev = fph;
            }
        }

        // -----------------------------------------------------------------
        // 29 catwalk — re-based 2026-11-23 to RING10 + TOPFANS: mirrors
        // the catwalk check block in ref\gpu_rgb.py (head/tail1/tail2 +
        // carpet on RING10, period 10 | 120; 12/13 photographers flash
        // cycle; the old 0..5 audience display is dropped — 0/1 pure
        // black).
        // -----------------------------------------------------------------
        static void CheckCatwalk()
        {
            CheckRebasedCommon("catwalk", CatwalkFrame);
            for (int ph = 0; ph < PhasePeriod; ph++)
                Tst.Assert(SameBytes(CatwalkFrame(ph), CatwalkFrame((ph + 10) % PhasePeriod)),
                    string.Format("catwalk: period 10 (fn({0}) == fn({1}))",
                        ph, (ph + 10) % PhasePeriod));
            // covers all 10 lap phases x2
            for (int t = 0; t < 20; t++)
            {
                byte[] f = CatwalkFrame(t);
                int h = t % 10;
                Tst.Assert(SlotEq(f, Ring10[h], CatwalkHead),
                    string.Format("catwalk t={0}: head on ring slot {1}", t, Ring10[h]));
                Tst.Assert(SlotEq(f, Ring10[ModFloor(h - 1, 10)], CatwalkTail1),
                    string.Format("catwalk t={0}: tail1 behind head", t));
                Tst.Assert(SlotEq(f, Ring10[ModFloor(h - 2, 10)], CatwalkTail2),
                    string.Format("catwalk t={0}: tail2", t));
                for (int s = 0; s < 10; s++)
                    if (s != h && s != ModFloor(h - 1, 10) && s != ModFloor(h - 2, 10))
                        Tst.Assert(SlotEq(f, Ring10[s], CatwalkBase),
                            string.Format("catwalk t={0}: carpet ring slot {1}", t, Ring10[s]));
                int k = t % 10;
                int[] p;
                if (k <= 1) p = CatwalkPhotoFlash;
                else if (k == 2) p = CatwalkPhotoAfter;
                else p = CatwalkPhotoBase;
                for (int j = 0; j < TopFans.Length; j++)
                    Tst.Assert(SlotEq(f, TopFans[j], p),
                        string.Format("catwalk t={0}: 12/13 photographers == photo phase", t));
                for (int i = 0; i <= 1; i++)
                    Tst.Assert(SlotEq(f, i, new int[] { 0, 0, 0 }),
                        "catwalk t=" + t + ": slot " + i + " black");
            }
            // flash is the point: near-white and distinct from standby
            Tst.Assert(CatwalkPhotoFlash[0] >= 200 && CatwalkPhotoFlash[1] >= 200
                       && CatwalkPhotoFlash[2] >= 200,
                "catwalk: PHOTO_FLASH near-white");
            Tst.Assert(CatwalkPhotoFlash[0] != CatwalkPhotoBase[0]
                       || CatwalkPhotoFlash[1] != CatwalkPhotoBase[1]
                       || CatwalkPhotoFlash[2] != CatwalkPhotoBase[2],
                "catwalk: flash != standby");
            Tst.Assert(CatwalkPhotoAfter[0] != CatwalkPhotoBase[0]
                       || CatwalkPhotoAfter[1] != CatwalkPhotoBase[1]
                       || CatwalkPhotoAfter[2] != CatwalkPhotoBase[2],
                "catwalk: afterglow != standby");
        }

        // -----------------------------------------------------------------
        // 30 supernova — re-based 2026-11-23 to RING10 + TOPFANS: mirrors
        // the supernova check block in ref\gpu_rgb.py (buildup rest
        // state == loop seam; detonation flash/fireball; shockwave band
        // head+tail on RING10 over the 39-frame decay with the whole
        // ring cooling; afterglow fades back to the rest state exactly
        // at frame 119 — seamless).
        // -----------------------------------------------------------------
        static void CheckSupernova()
        {
            CheckRebasedCommon("supernova", SupernovaFrame);
            Tst.Assert(SameBytes(SupernovaFrame(0), SupernovaFrame(120)),
                "supernova: period 120");
            Tst.Assert(SameBytes(SupernovaFrame(119), SupernovaFrame(0)),
                "supernova: seamless loop");
            // BUILDUP start = night rest state (loop seam)
            byte[] f0 = SupernovaFrame(0);
            Tst.Assert(SlotEq(f0, 12, SnoNightStar) && SlotEq(f0, 13, SnoNightStar),
                "supernova t=0: 12/13 == NIGHT_STAR rest state");
            for (int j = 0; j < Ring10.Length; j++)
                Tst.Assert(SlotEq(f0, Ring10[j], SnoNightFan),
                    string.Format("supernova t=0: ring slot {0} == NIGHT_FAN", Ring10[j]));
            for (int i = 0; i <= 1; i++)
                Tst.Assert(SlotEq(f0, i, new int[] { 0, 0, 0 }),
                    "supernova t=0: slot " + i + " black");
            // DETONATION: explosion — ring + star near-white, then
            // fireball
            int[] detSlots = new int[] { 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 };
            for (int j = 0; j < detSlots.Length; j++)
            {
                int i = detSlots[j];
                Tst.Assert(SlotEq(SupernovaFrame(40), i, SnoFlash),
                    string.Format("supernova t=40: slot {0} == FLASH", i));
                Tst.Assert(SlotEq(SupernovaFrame(41), i, SnoFlash),
                    string.Format("supernova t=41: slot {0} == FLASH", i));
                Tst.Assert(SlotEq(SupernovaFrame(42), i, new int[] { 255, 242, 210 }),
                    string.Format("supernova t=42: slot {0} == fireball lerp midpoint", i));
                Tst.Assert(SlotEq(SupernovaFrame(45), i, SnoFireball),
                    string.Format("supernova t=45: slot {0} == FIREBALL", i));
            }
            // SHOCKWAVE start: band head + fading tail on RING10 (pos
            // 0); the rest is cooling ember; star = dim remnant pulsar
            byte[] f46 = SupernovaFrame(46);
            Tst.Assert(SlotEq(f46, 2, SnoShockHot),
                "supernova t=46: band head (pos 0) on slot 2");
            Tst.Assert(SlotEq(f46, 11, new int[] { 172, 127, 44 }),
                "supernova t=46: band tail (pos 9) on slot 11");
            for (int j = 0; j < Ring10.Length; j++)
            {
                int i = Ring10[j];
                if (i != 2 && i != 11)
                    Tst.Assert(SlotEq(f46, i, SnoEmberHot),
                        string.Format("supernova t=46: cooling ember on slot {0}", i));
            }
            Tst.Assert(SlotEq(f46, 12, new int[] { 129, 119, 110 })
                       && SlotEq(f46, 13, new int[] { 129, 119, 110 }),
                "supernova t=46: 12/13 == (129,119,110) remnant pulsar");
            for (int i = 0; i <= 1; i++)
                Tst.Assert(SlotEq(f46, i, new int[] { 0, 0, 0 }),
                    "supernova t=46: slot " + i + " black");
            // SHOCKWAVE end: band fully faded -> uniform cooling ember
            // ring
            byte[] f85 = SupernovaFrame(85);
            for (int j = 0; j < Ring10.Length; j++)
                Tst.Assert(SlotEq(f85, Ring10[j], SnoEmberCool),
                    string.Format("supernova t=85: ring slot {0} == EMBER_COOL (band faded)",
                        Ring10[j]));
            // AFTERGLOW start: continuous with shockwave end (no jump)
            byte[] f86 = SupernovaFrame(86);
            for (int j = 0; j < Ring10.Length; j++)
                Tst.Assert(SlotEq(f86, Ring10[j], SnoEmberCool),
                    string.Format(
                        "supernova t=86: ring slot {0} == EMBER_COOL (afterglow start, continuous)",
                        Ring10[j]));
            // AFTERGLOW end = loop seam = BUILDUP start (seamless)
            byte[] f119 = SupernovaFrame(119);
            Tst.Assert(SlotEq(f119, 12, SnoNightStar) && SlotEq(f119, 13, SnoNightStar),
                "supernova t=119: 12/13 == NIGHT_STAR (seamless seam)");
            for (int j = 0; j < Ring10.Length; j++)
                Tst.Assert(SlotEq(f119, Ring10[j], SnoNightFan),
                    string.Format("supernova t=119: ring slot {0} == NIGHT_FAN (seamless seam)",
                        Ring10[j]));
            for (int i = 0; i <= 1; i++)
                Tst.Assert(SlotEq(f119, i, new int[] { 0, 0, 0 }),
                    "supernova t=119: slot " + i + " black");
        }

        // -----------------------------------------------------------------
        // 31 inferno — re-based 2026-11-23 to RING10 + TOPFANS: mirrors
        // the inferno check block in ref\gpu_rgb.py (RING10 slowly cycles
        // dark red -> bright orange, period 60 | 120; 12/13 the subject —
        // flame flicker in sync, always at/above the bright base, hard
        // near-white sparks whenever the flicker table crosses
        // INFERNO_SPARK_LEVEL, sparks occur but are the minority of
        // frames; the old board ring 0..5 dim ember is dropped — 0/1 pure
        // black; 14..239 dead).
        // -----------------------------------------------------------------
        static void CheckInferno()
        {
            CheckRebasedCommon("inferno", InfernoFrame);
            Tst.Assert(InfernoPeriod == 60 && PhasePeriod % InfernoPeriod == 0,
                "inferno: period 60 | 120");
            Tst.Assert(SameBytes(InfernoFrame(0), InfernoFrame(120)),
                "inferno: period 120 (fn(0) == fn(120))");
            byte[] f0 = InfernoFrame(0);   // t=0: cycle trough
            for (int j = 0; j < Ring10.Length; j++)
                Tst.Assert(SlotEq(f0, Ring10[j], InfernoFanDark),
                    string.Format("inferno t=0: ring slot {0} == INFERNO_FAN_DARK (cycle trough)",
                        Ring10[j]));
            for (int i = 0; i <= 1; i++)
                Tst.Assert(SlotEq(f0, i, new int[] { 0, 0, 0 }),
                    "inferno t=0: slot " + i + " black");
            byte[] f30 = InfernoFrame(30);  // t=30: cycle peak
            for (int j = 0; j < Ring10.Length; j++)
                Tst.Assert(SlotEq(f30, Ring10[j], InfernoFanHot),
                    string.Format("inferno t=30: ring slot {0} == INFERNO_FAN_HOT (cycle peak)",
                        Ring10[j]));
            // flame flicker: 12/13 always at/above the bright base, in
            // sync; white sparks occur but are the minority of frames
            int sparks = 0;
            for (int ph = 0; ph < PhasePeriod; ph++)
            {
                byte[] fph = InfernoFrame(ph);
                Tst.Assert(fph[36] == fph[39] && fph[37] == fph[40] && fph[38] == fph[41],
                    string.Format("inferno phase {0}: top fans 12/13 in sync", ph));
                Tst.Assert(fph[36] >= InfernoTopBase[0] && fph[37] >= InfernoTopBase[1]
                           && fph[38] >= InfernoTopBase[2],
                    string.Format("inferno phase {0}: topfan 12 at/above INFERNO_TOP_BASE", ph));
                if (fph[36] == InfernoTopWhite[0] && fph[37] == InfernoTopWhite[1]
                    && fph[38] == InfernoTopWhite[2])
                    sparks++;
            }
            Tst.Assert(sparks >= 5 && sparks <= 30,
                string.Format("inferno: sparks occur but are the minority ({0} of 120 frames)",
                    sparks));
        }

        // -----------------------------------------------------------------
        // 32 reactor-core — re-based 2026-11-23 to RING10 + TOPFANS:
        // mirrors the reactor-core check block in ref\gpu_rgb.py (RING10
        // breathes REACTOR_FAN_DARK -> REACTOR_FAN_BRIGHT, 60-frame
        // breath, t=0 trough, t=30 peak, 60 | 120; 12/13 the subject:
        // bright cyan flashing brighter toward near-white every 60 frames
        // on a 13-frame raised-cosine pulse (u=0..12, peak u=6, at
        // t=6/66); the old board ring 0..5 dim blue wash is dropped — 0/1
        // pure black; 14..239 dead; smoothness: no live-set slot jumps
        // more than 1/8 of the full range per frame).
        // -----------------------------------------------------------------
        static void CheckReactorCore()
        {
            CheckRebasedCommon("reactor-core", ReactorCoreFrame);
            Tst.Assert(ReactorPeriod == 60 && PhasePeriod % ReactorPeriod == 0,
                "reactor-core: period 60 | 120");
            Tst.Assert(SameBytes(ReactorCoreFrame(0), ReactorCoreFrame(120)),
                "reactor-core: period 120 (fn(0) == fn(120))");
            byte[] f0 = ReactorCoreFrame(0);   // t=0: breath trough, no flash
            for (int j = 0; j < Ring10.Length; j++)
                Tst.Assert(SlotEq(f0, Ring10[j], ReactorFanDark),
                    string.Format("reactor-core t=0: ring slot {0} == REACTOR_FAN_DARK (breath trough)",
                        Ring10[j]));
            for (int i = 0; i <= 1; i++)
                Tst.Assert(SlotEq(f0, i, new int[] { 0, 0, 0 }),
                    "reactor-core t=0: slot " + i + " black");
            Tst.Assert(SlotEq(f0, 12, ReactorTopBase) && SlotEq(f0, 13, ReactorTopBase),
                "reactor-core t=0: 12/13 == REACTOR_TOP_BASE (no flash)");
            byte[] f30 = ReactorCoreFrame(30);  // t=30: breath peak, no flash
            for (int j = 0; j < Ring10.Length; j++)
                Tst.Assert(SlotEq(f30, Ring10[j], ReactorFanBright),
                    string.Format("reactor-core t=30: ring slot {0} == REACTOR_FAN_BRIGHT (breath peak)",
                        Ring10[j]));
            Tst.Assert(SlotEq(f30, 12, ReactorTopBase) && SlotEq(f30, 13, ReactorTopBase),
                "reactor-core t=30: 12/13 == REACTOR_TOP_BASE (no flash)");
            // flash: p=1 at u=6 of each 60-frame cycle (t=6, 66):
            // near-white top
            for (int k = 0; k < 2; k++)
            {
                int[] peakPhases = new int[] { 6, 66 };
                byte[] fph = ReactorCoreFrame(peakPhases[k]);
                Tst.Assert(SlotEq(fph, 12, ReactorTopFlash) && SlotEq(fph, 13, ReactorTopFlash),
                    "reactor-core t=" + peakPhases[k] + ": 12/13 == REACTOR_TOP_FLASH (pulse peak)");
            }
            // top fans: both sync; R/G never below the bright cyan base;
            // B in the 250..255 band (the base is bluer than the
            // near-white flash)
            for (int ph = 0; ph < PhasePeriod; ph++)
            {
                byte[] fph = ReactorCoreFrame(ph);
                Tst.Assert(fph[36] == fph[39] && fph[37] == fph[40] && fph[38] == fph[41],
                    string.Format("reactor-core phase {0}: top fans 12/13 in sync", ph));
                Tst.Assert(fph[36] >= ReactorTopBase[0] && fph[37] >= ReactorTopBase[1]
                           && fph[38] >= ReactorTopFlash[2],
                    string.Format("reactor-core phase {0}: topfan 12 in the base..flash band", ph));
            }
            // smoothness: no slot on the live set jumps more than 1/8 of
            // the full range per frame anywhere in the 120-frame phase
            // (no blink)
            byte[] prev = ReactorCoreFrame(0);
            for (int ph = 1; ph <= PhasePeriod; ph++)
            {
                byte[] fph = ReactorCoreFrame(ph % PhasePeriod);
                for (int j = 0; j < Ring10.Length; j++)
                    for (int ch = 0; ch < 3; ch++)
                        Tst.Assert(Math.Abs(fph[3 * Ring10[j] + ch] - prev[3 * Ring10[j] + ch]) <= 32,
                            string.Format("reactor-core phase {0}: ring slot {1} jumps > 32 (smoothness)",
                                ph, Ring10[j]));
                for (int j = 0; j < TopFans.Length; j++)
                    for (int ch = 0; ch < 3; ch++)
                        Tst.Assert(Math.Abs(fph[3 * TopFans[j] + ch] - prev[3 * TopFans[j] + ch]) <= 32,
                            string.Format("reactor-core phase {0}: topfan slot {1} jumps > 32 (smoothness)",
                                ph, TopFans[j]));
                prev = fph;
            }
        }

        static bool SameBytes(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        static bool SameBytesAt(byte[] a, byte[] b, int off, int len)
        {
            if (a == null || b == null || a.Length != len || off + len > b.Length) return false;
            for (int i = 0; i < len; i++)
                if (a[i] != b[off + i]) return false;
            return true;
        }
    }
}
