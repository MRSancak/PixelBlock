using System;
using System.Collections.Generic;
using Godot;

namespace PixelBlock.Core;

public static class SoundBank
{
    public const int Rate = 22050;

    public sealed class Clip
    {
        public float[] Samples;
        public bool Loop;
    }

    private sealed class Rng
    {
        private uint _s;
        public Rng(uint seed) => _s = seed == 0 ? 0x9E3779B9 : seed;
        public float White()
        {
            _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5;
            return (_s / (float)uint.MaxValue) * 2f - 1f;
        }
    }

    public static Dictionary<string, Clip> BuildSfx()
    {
        var d = new Dictionary<string, Clip>
        {
            ["place"]  = Place(),
            ["break"]  = Break(),
            ["step"]   = Step(),
            ["jump"]   = Jump(),
            ["land"]   = Land(),
            ["splash"] = Splash(),
            ["hurt"]   = Hurt(),
            ["swing"]  = Swing(),
            ["shoot"]  = Shoot(),
            ["hit"]    = Hit(),
            ["defeat"] = Defeat(),
            ["click"]  = Blip(660, 0.045, 0.35),
            ["select"] = Blip(880, 0.032, 0.30),
            ["talk"]   = Blip(520, 0.040, 0.22),
            ["chime"]  = Chime(),
            ["fanfare"]= Fanfare(),

            ["moo"]    = Blip(140, 0.55, 0.40),
            ["baa"]    = Blip(380, 0.40, 0.30),
            ["cluck"]  = Blip(1100, 0.06, 0.25),
            ["oink"]   = Blip(230, 0.22, 0.35),
            ["neigh"]  = Blip(520, 0.45, 0.30),
            ["growl"]  = Blip(90, 0.60, 0.45),
            ["howl"]   = Blip(330, 1.00, 0.35),
            ["squeak"] = Blip(1500, 0.05, 0.20),
        };

        foreach (MaterialSound m in System.Enum.GetValues<MaterialSound>())
        {
            d[$"step_{m}"]  = StepFor(m);
            d[$"break_{m}"] = BreakFor(m);
            d[$"mine_{m}"]  = MineFor(m);
        }
        return d;
    }

    private static Clip StepFor(MaterialSound m)
    {
        var b = Buf(0.08);
        var rng = new Rng(37 + (uint)m * 7u);
        switch (m)
        {
            case MaterialSound.Grass: Noise(b, 0, 0.06, 0.26, rng, 2400, 900, 0.020); break;
            case MaterialSound.Dirt:  Noise(b, 0, 0.06, 0.30, rng, 1300, 380, 0.025); break;
            case MaterialSound.Sand:  Noise(b, 0, 0.07, 0.28, rng, 3600, 1500, 0.030); break;
            case MaterialSound.Wood:  Perc(b, 0, 0.05, 220, 160, 0.22, Wave.Tri, 0.030);
                                      Noise(b, 0, 0.03, 0.12, rng, 2000, 800, 0.015); break;
            case MaterialSound.Snow:  Noise(b, 0, 0.075, 0.30, rng, 2700, 1000, 0.035); break;
            case MaterialSound.Metal: Perc(b, 0, 0.06, 1200, 950, 0.16, Wave.Sine, 0.040);
                                      Noise(b, 0, 0.02, 0.08, rng, 5000, 3000, 0.010); break;
            case MaterialSound.Cloth: Noise(b, 0, 0.06, 0.20, rng, 850, 300, 0.030); break;
            default:                  Noise(b, 0, 0.06, 0.30, rng, 1500, 380, 0.025); break;
        }
        return Done(b);
    }

    private static Clip BreakFor(MaterialSound m)
    {
        var b = Buf(0.18);
        var rng = new Rng(23 + (uint)m * 13u);
        switch (m)
        {
            case MaterialSound.Grass: Noise(b, 0, 0.14, 0.45, rng, 3600, 1200, 0.06); break;
            case MaterialSound.Dirt:  Noise(b, 0, 0.13, 0.50, rng, 3000, 500, 0.06);
                                      Perc(b, 0, 0.06, 90, 70, 0.25, Wave.Tri, 0.04); break;
            case MaterialSound.Sand:  Noise(b, 0, 0.16, 0.50, rng, 5000, 1500, 0.07); break;
            case MaterialSound.Wood:  Perc(b, 0, 0.06, 300, 120, 0.40, Wave.Tri, 0.03);
                                      Noise(b, 0, 0.12, 0.40, rng, 2500, 600, 0.05); break;
            case MaterialSound.Snow:  Noise(b, 0, 0.14, 0.42, rng, 2800, 800, 0.06); break;
            case MaterialSound.Metal: Perc(b, 0, 0.10, 900, 520, 0.40, Wave.Square, 0.07);
                                      Perc(b, 0, 0.13, 1450, 1350, 0.18, Wave.Sine, 0.09); break;
            case MaterialSound.Cloth: Noise(b, 0, 0.13, 0.38, rng, 1200, 400, 0.06); break;
            default:                  Noise(b, 0, 0.15, 0.60, rng, 4500, 700, 0.06);
                                      Perc(b, 0, 0.07, 95, 70, 0.32, Wave.Tri, 0.04); break;
        }
        return Done(b);
    }

    private static Clip MineFor(MaterialSound m)
    {
        var b = Buf(0.08);
        var rng = new Rng(53 + (uint)m * 17u);
        switch (m)
        {
            case MaterialSound.Grass: Noise(b, 0, 0.045, 0.24, rng, 2200, 900, 0.020); break;
            case MaterialSound.Dirt:  Noise(b, 0, 0.05, 0.28, rng, 1500, 500, 0.025); break;
            case MaterialSound.Sand:  Noise(b, 0, 0.05, 0.26, rng, 3400, 1400, 0.025); break;
            case MaterialSound.Wood:  Perc(b, 0, 0.05, 260, 180, 0.28, Wave.Tri, 0.030); break;
            case MaterialSound.Snow:  Noise(b, 0, 0.05, 0.26, rng, 2600, 900, 0.030); break;
            case MaterialSound.Metal: Perc(b, 0, 0.05, 1300, 1000, 0.20, Wave.Sine, 0.035); break;
            case MaterialSound.Cloth: Noise(b, 0, 0.05, 0.18, rng, 900, 320, 0.030); break;
            default:                  Perc(b, 0, 0.05, 420, 260, 0.28, Wave.Square, 0.030);
                                      Noise(b, 0, 0.03, 0.12, rng, 3000, 800, 0.015); break;
        }
        return Done(b);
    }

    public static Dictionary<string, Clip> BuildAll()
    {
        var d = BuildSfx();
        d["music_Calm"]   = BuildMusic(MusicMood.Calm);
        d["music_Hope"]   = BuildMusic(MusicMood.Hope);
        d["music_Solemn"] = BuildMusic(MusicMood.Solemn);
        d["amb_day"]   = BuildAmbienceDay();
        d["amb_night"] = BuildAmbienceNight();
        d["amb_rain"]  = BuildAmbienceRain();
        return d;
    }

    private static Clip Place()
    {
        var b = Buf(0.10);
        Perc(b, 0.0, 0.10, 175, 150, 0.55, Wave.Tri, 0.030);
        Noise(b, 0.0, 0.02, 0.18, new Rng(11), 5000, 2500, 0.012);
        return Done(b);
    }

    private static Clip Break()
    {
        var b = Buf(0.16);
        Noise(b, 0.0, 0.15, 0.6, new Rng(23), 4500, 700, 0.06);
        Perc(b, 0.0, 0.07, 95, 70, 0.32, Wave.Tri, 0.04);
        return Done(b);
    }

    private static Clip Step()
    {
        var b = Buf(0.07);
        Noise(b, 0.0, 0.06, 0.32, new Rng(37), 1500, 380, 0.025);
        return Done(b);
    }

    private static Clip Jump()
    {
        var b = Buf(0.12);
        Perc(b, 0.0, 0.11, 240, 380, 0.42, Wave.Sine, 0.06);
        return Done(b);
    }

    private static Clip Land()
    {
        var b = Buf(0.14);
        Noise(b, 0.0, 0.12, 0.42, new Rng(41), 900, 200, 0.05);
        Perc(b, 0.0, 0.08, 130, 90, 0.34, Wave.Tri, 0.04);
        return Done(b);
    }

    private static Clip Splash()
    {
        var b = Buf(0.30);
        Noise(b, 0.0, 0.28, 0.5, new Rng(53), 5500, 900, 0.10);
        Noise(b, 0.0, 0.10, 0.22, new Rng(59), 8000, 4000, 0.05);
        return Done(b);
    }

    private static Clip Hurt()
    {
        var b = Buf(0.17);
        Perc(b, 0.0, 0.16, 320, 150, 0.4, Wave.Saw, 0.07);
        return Done(b);
    }

    private static Clip Swing()
    {
        var b = Buf(0.16);

        Noise(b, 0.0, 0.15, 0.4, new Rng(67), 700, 3500, 0.05);
        return Done(b);
    }

    private static Clip Shoot()
    {
        var b = Buf(0.14);
        Perc(b, 0.0, 0.05, 620, 300, 0.4, Wave.Tri, 0.03);
        Noise(b, 0.0, 0.12, 0.3, new Rng(71), 5000, 1500, 0.05);
        return Done(b);
    }

    private static Clip Hit()
    {
        var b = Buf(0.11);
        Noise(b, 0.0, 0.09, 0.5, new Rng(83), 3200, 700, 0.04);
        Perc(b, 0.0, 0.06, 185, 120, 0.3, Wave.Square, 0.03);
        return Done(b);
    }

    private static Clip Defeat()
    {
        var b = Buf(0.36);
        Perc(b, 0.0, 0.34, 420, 120, 0.4, Wave.Tri, 0.14);
        Noise(b, 0.04, 0.30, 0.32, new Rng(97), 4200, 500, 0.13);
        return Done(b);
    }

    private static Clip Blip(double freq, double dur, double amp)
    {
        var b = Buf(dur);
        Perc(b, 0.0, dur, freq, freq, amp, Wave.Sine, dur * 0.6);
        return Done(b);
    }

    private static Clip Chime()
    {
        var b = Buf(0.55);
        int[] notes = { 72, 76, 79 };
        for (int i = 0; i < notes.Length; i++)
            Perc(b, i * 0.085, 0.34, Midi(notes[i]), Midi(notes[i]), 0.30, Wave.Sine, 0.16);
        return Done(b, peak: 0.8);
    }

    private static Clip Fanfare()
    {
        var b = Buf(1.2);
        int[] notes = { 67, 72, 76, 79, 84 };
        double[] at = { 0.0, 0.11, 0.22, 0.33, 0.50 };
        for (int i = 0; i < notes.Length; i++)
        {
            bool last = i == notes.Length - 1;
            Perc(b, at[i], last ? 0.55 : 0.22, Midi(notes[i]), Midi(notes[i]),
                 last ? 0.34 : 0.26, Wave.Tri, last ? 0.30 : 0.12);
        }
        Perc(b, 0.50, 0.55, Midi(91), Midi(91), 0.12, Wave.Sine, 0.24);
        return Done(b, peak: 0.85);
    }

    public static Clip BuildMusic(MusicMood mood) => mood switch
    {

        MusicMood.Hope => BuildMusicMood(new[]
        {
            new[] { 67, 71, 74, 79 },
            new[] { 62, 66, 69, 74 },
            new[] { 64, 67, 71, 76 },
            new[] { 60, 64, 67, 72 },
        }, bpm: 88),

        MusicMood.Solemn => BuildMusicMood(new[]
        {
            new[] { 62, 65, 69, 74 },
            new[] { 57, 60, 64, 69 },
            new[] { 58, 62, 65, 70 },
            new[] { 55, 58, 62, 67 },
        }, bpm: 58),

        _ => BuildMusicMood(new[]
        {
            new[] { 62, 66, 69, 74 },
            new[] { 57, 61, 64, 69 },
            new[] { 59, 62, 66, 71 },
            new[] { 55, 59, 62, 67 },
        }, bpm: 72),
    };

    private static Clip BuildMusicMood(int[][] chords, double bpm)
    {
        double beat = 60.0 / bpm;
        double loop = beat * 4 * chords.Length;
        var b = Buf(loop);

        for (int c = 0; c < chords.Length; c++)
        {
            double t0 = c * beat * 4;
            Pad(b, t0, beat * 4, Midi(chords[c][0] - 12), 0.12, beat * 0.15, beat * 0.6);
            for (int n = 0; n < 4; n++)
                Perc(b, t0 + n * beat, beat * 1.6, Midi(chords[c][n]), Midi(chords[c][n]),
                     0.26, Wave.Tri, beat * 0.7);
        }

        var clip = Done(b, peak: 0.7);
        clip.Loop = true;
        return clip;
    }

    public static Clip BuildAmbienceDay()
    {
        const double loop = 16.0;
        var b = Buf(loop);
        int n = b.Length;
        var rng = new Rng(0xA11CE);

        double brown = 0, lp = 0, gustCycles = 3;
        for (int i = 0; i < n; i++)
        {
            double w = rng.White();
            brown = brown * 0.985 + w * 0.03;
            double a = 1 - Math.Exp(-2 * Math.PI * 480 / Rate);
            lp += a * (brown - lp);
            double t = i / (double)Rate;
            double gust = 0.6 + 0.4 * Math.Sin(2 * Math.PI * gustCycles * t / loop);
            b[i] += (float)(lp * 4.0 * gust * 0.5);
        }

        double[] chirpAt = { 2.3, 5.1, 9.7, 13.2 };
        foreach (double ct in chirpAt)
        {
            Perc(b, ct, 0.07, 2600, 3200, 0.10, Wave.Sine, 0.04);
            Perc(b, ct + 0.09, 0.06, 3100, 2700, 0.08, Wave.Sine, 0.035);
        }

        var clip = Done(b, peak: 0.6);
        clip.Loop = true;
        return clip;
    }

    public static Clip BuildAmbienceNight()
    {
        const double loop = 8.0;
        var b = Buf(loop);
        int n = b.Length;
        var rng = new Rng(0xC0FFEE);

        double brown = 0, lp = 0;
        for (int i = 0; i < n; i++)
        {
            double w = rng.White();
            brown = brown * 0.985 + w * 0.03;
            double a = 1 - Math.Exp(-2 * Math.PI * 300 / Rate);
            lp += a * (brown - lp);
            b[i] += (float)(lp * 4.0 * 0.28);
        }

        for (double t = 0.15; t < loop - 0.05; t += 0.5)
        {
            Perc(b, t, 0.018, 4300, 4300, 0.09, Wave.Sine, 0.010);
            Perc(b, t + 0.05, 0.018, 4300, 4300, 0.08, Wave.Sine, 0.010);
        }

        Perc(b, 2.0, 0.22, 380, 360, 0.14, Wave.Sine, 0.16);
        Perc(b, 2.32, 0.22, 360, 340, 0.12, Wave.Sine, 0.16);

        var clip = Done(b, peak: 0.5);
        clip.Loop = true;
        return clip;
    }

    public static Clip BuildAmbienceRain()
    {
        const double loop = 8.0;
        var b = Buf(loop);
        int n = b.Length;
        var rng = new Rng(0x5A1175);

        double lp = 0;
        for (int i = 0; i < n; i++)
        {
            double w = rng.White();
            double a = 1 - Math.Exp(-2 * Math.PI * 3200 / Rate);
            lp += a * (w - lp);
            b[i] += (float)(lp * 0.5);
        }

        Perc(b, 1.2, 1.4, 70, 45, 0.16, Wave.Sine, 0.6);
        Perc(b, 5.3, 1.5, 60, 40, 0.14, Wave.Sine, 0.7);

        var clip = Done(b, peak: 0.55);
        clip.Loop = true;
        return clip;
    }

    private enum Wave { Sine, Tri, Saw, Square }

    private static float[] Buf(double seconds) => new float[Math.Max(1, (int)(seconds * Rate))];

    private static double Midi(int note) => 440.0 * Math.Pow(2.0, (note - 69) / 12.0);

    private static void Perc(float[] buf, double t0, double dur, double f0, double f1,
                             double amp, Wave wave, double tau)
    {
        int start = (int)(t0 * Rate), len = (int)(dur * Rate);
        double phase = 0;
        for (int i = 0; i < len; i++)
        {
            int idx = start + i;
            if (idx < 0 || idx >= buf.Length) continue;
            double t = i / (double)Rate;
            double f = f0 + (f1 - f0) * (i / (double)len);
            phase += 2 * Math.PI * f / Rate;
            double atk = Math.Min(1.0, t / 0.003);
            double env = atk * Math.Exp(-t / tau);
            buf[idx] += (float)(Osc(wave, phase) * amp * env);
        }
    }

    private static void Pad(float[] buf, double t0, double dur, double freq,
                            double amp, double atk, double rel)
    {
        int start = (int)(t0 * Rate), len = (int)(dur * Rate);
        double phase = 0;
        for (int i = 0; i < len; i++)
        {
            int idx = start + i;
            if (idx < 0 || idx >= buf.Length) continue;
            double t = i / (double)Rate, tEnd = (len - i) / (double)Rate;
            phase += 2 * Math.PI * freq / Rate;
            double env = Math.Min(1.0, t / atk) * Math.Min(1.0, tEnd / rel);

            double s = Math.Sin(phase) + 0.25 * Math.Sin(phase * 2.0);
            buf[idx] += (float)(s * amp * env);
        }
    }

    private static void Noise(float[] buf, double t0, double dur, double amp, Rng rng,
                              double fcStart, double fcEnd, double tau)
    {
        int start = (int)(t0 * Rate), len = (int)(dur * Rate);
        double lp = 0;
        for (int i = 0; i < len; i++)
        {
            int idx = start + i;
            if (idx < 0 || idx >= buf.Length) continue;
            double t = i / (double)Rate;
            double fc = fcStart + (fcEnd - fcStart) * (i / (double)len);
            double a = 1 - Math.Exp(-2 * Math.PI * fc / Rate);
            lp += a * (rng.White() - lp);
            double atk = Math.Min(1.0, t / 0.002);
            double env = atk * Math.Exp(-t / tau);
            buf[idx] += (float)(lp * amp * env);
        }
    }

    private static double Osc(Wave w, double phase)
    {
        double p = phase % (2 * Math.PI);
        if (p < 0) p += 2 * Math.PI;
        return w switch
        {
            Wave.Sine => Math.Sin(p),
            Wave.Tri => 2.0 / Math.PI * Math.Asin(Math.Sin(p)),
            Wave.Saw => p / Math.PI - 1.0,
            Wave.Square => p < Math.PI ? 1.0 : -1.0,
            _ => Math.Sin(p),
        };
    }

    private static Clip Done(float[] buf, double peak = 0.9)
    {
        float max = 1e-6f;
        foreach (float v in buf) max = Math.Max(max, Math.Abs(v));
        float g = (float)(max > peak ? peak / max : 1.0);
        for (int i = 0; i < buf.Length; i++)
            buf[i] = (float)Math.Tanh(buf[i] * g * 1.05);
        return new Clip { Samples = buf, Loop = false };
    }

    public static AudioStreamWav ToStream(Clip c)
    {
        var bytes = ToPcm16(c.Samples);
        var w = new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = Rate,
            Stereo = false,
            Data = bytes,
        };
        if (c.Loop)
        {
            w.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
            w.LoopBegin = 0;
            w.LoopEnd = c.Samples.Length;
        }
        return w;
    }

    public static byte[] ToPcm16(float[] s)
    {
        var bytes = new byte[s.Length * 2];
        for (int i = 0; i < s.Length; i++)
        {
            int v = (int)Math.Round(Mathf.Clamp(s[i], -1f, 1f) * 32767f);
            bytes[i * 2] = (byte)(v & 0xFF);
            bytes[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
        }
        return bytes;
    }
}
