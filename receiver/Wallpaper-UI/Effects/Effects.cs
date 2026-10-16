using System;
using System.Collections.Generic;
using System.Numerics;
using Avalonia.Media;

namespace Wallpaper_UI;

public static class Effects
{
    private static double _animStep = 0;
    private static double _xShift = 0;
    private static double _colorShift = 0;
    private static int _effectIdx = 0;
    private static DateTime _lastTriggerTime = DateTime.MinValue;

    public static readonly ColorScheme NeonScheme = new ColorScheme
    {
        C1 = Color.FromRgb(255, 0, 255),  // Magenta
        C2 = Color.FromRgb(255, 0, 129),  // Deep Pink
        C3 = Color.FromRgb(0, 117, 255),  // Azure Blue
        C4 = Color.FromRgb(0, 255, 255),   // Cyan
        Version = 4
    };

    public static readonly ColorScheme SunsetScheme = new ColorScheme
    {
        C1 = Color.FromRgb(255, 80, 0),   // Orange
        C2 = Color.FromRgb(255, 0, 100),  // Pink/Red
        C3 = Color.FromRgb(80, 0, 200),   // Purple
        C4 = Color.FromRgb(0, 0, 50),     // Deep Night Blue
        Version = 4
    };

    public static readonly ColorScheme RainbowScheme = new ColorScheme
    {
        C1 = Color.FromRgb(255, 0, 0),
        C2 = Color.FromRgb(255, 127, 0),
        C3 = Color.FromRgb(255, 255, 0),
        C4 = Color.FromRgb(0, 255, 0),
        C5 = Color.FromRgb(0, 255, 255),
        C6 = Color.FromRgb(0, 0, 255),
        C7 = Color.FromRgb(255, 0, 255),
        Version = 7
    };

    // ----------------------------------------------------
    // OfflineEffects
    // ----------------------------------------------------

    public static void RenderAurora(this Color[] snapshot, int w, int h)
    {
        double speed = 0.05;
        _animStep += speed;

        for (int count = 0; count < snapshot.Length; count++)
        {
            int x = count % w;
            int y = count / w;

            double noiseH = FastNoise.GetNoise(x * 0.1, y * 0.1, _animStep);
            double noiseB = FastNoise.GetNoise(x * 0.05, y * 0.2, _animStep * 0.5);

            double hue = (220 + (noiseH * 40)) / 360.0;
            double intensity = Math.Clamp((noiseB + 1.0) / 2.0, 0.0, 1.0);

            snapshot[count] = Utils.ColorFromHsl(hue, 0.8, intensity * 0.5);
        }
    }

    public static void RenderSolidColor(this Color[] snapshot, int w, int h, Color targetColor)
    {
        int totalLeds = w * h;

        if (snapshot.Length != totalLeds)
        {
            // Note: Since snapshot is passed by reference, updating it directly inside method doesn't propagate
            // to the caller if we re-instantiate it without 'ref'. But the caller ensures snapshot length matches,
            // so this is a fallback. To match VB.NET exactly, we modify the array elements.
            Array.Resize(ref snapshot, totalLeds);
        }

        for (int i = 0; i < snapshot.Length; i++)
        {
            snapshot[i] = targetColor;
        }
    }

    public static void RenderBreathingColor(this Color[] snapshot, int w, int h, Color targetColor)
    {
        double time = DateTime.Now.TimeOfDay.TotalSeconds;
        double pulse = (Math.Sin(time * 2.0) + 1.0) / 2.0;

        Color breathingColor = Color.FromRgb(
            (byte)(targetColor.R * pulse),
            (byte)(targetColor.G * pulse),
            (byte)(targetColor.B * pulse)
        );

        RenderSolidColor(snapshot, w, h, breathingColor);
    }

    public static void RenderSunsetNeon(this Color[] snapshot, int w, int h, ColorScheme scheme, string direction = "left")
    {
        _animStep += 0.01;

        for (int count = 0; count < snapshot.Length; count++)
        {
            int x = count % w;
            int y = count / w;

            double progress = 0;
            switch (direction.ToLower())
            {
                case "left":
                    progress = ((double)x / w) + _animStep;
                    break;
                case "right":
                    progress = (1.0 - ((double)x / w)) + _animStep;
                    break;
                case "up":
                    progress = ((double)y / h) + _animStep;
                    break;
                case "down":
                    progress = (1.0 - ((double)y / h)) + _animStep;
                    break;
            }

            progress = progress % 1.0;
            if (progress < 0) progress += 1.0;

            snapshot[count] = GetLerpedColor(scheme, progress);
        }
    }

    public static void RenderRainbowCycle(this Color[] snapshot, int w, int h, ColorScheme scheme)
    {
        int totalLeds = w * h;
        double offset = DateTime.Now.TimeOfDay.TotalSeconds * 60;

        if (snapshot.Length != totalLeds)
        {
            Array.Resize(ref snapshot, totalLeds);
        }

        Color[] colors = new Color[] { scheme.C1, scheme.C2, scheme.C3, scheme.C4, scheme.C5, scheme.C6, scheme.C7 };
        int numColors = 7;
        double pos = (offset / 320.0) % 1.0;
        if (pos < 0) pos += 1.0;

        double colorPos = pos * numColors;
        int index1 = (int)Math.Floor(colorPos) % numColors;
        int index2 = (index1 + 1) % numColors;
        double blend = colorPos - Math.Floor(colorPos);

        Color targetColor = Interpolate(colors[index1], colors[index2], blend);

        for (int i = 0; i < snapshot.Length; i++)
        {
            snapshot[i] = targetColor;
        }
    }

    public static void RenderRainbowPinwheel(this Color[] snapshot, int w, int h, ColorScheme scheme)
    {
        Color[] colors = new Color[] { scheme.C1, scheme.C2, scheme.C3, scheme.C4, scheme.C5, scheme.C6, scheme.C7 };
        double centerX = w / 2.0;
        double centerY = h / 2.0;
        double offset = DateTime.Now.TimeOfDay.TotalSeconds * 60.0;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                double dx = x - centerX;
                double dy = y - centerY;
                double angle = Math.Atan2(dy, dx);
                if (angle < 0) angle += 2 * Math.PI;

                double pos = ((angle / (2 * Math.PI)) + (offset / 360.0)) % 1.0;

                double colorPos = pos * 7;
                int idx1 = (int)Math.Floor(colorPos) % 7;
                int idx2 = (idx1 + 1) % 7;
                double blend = colorPos - Math.Floor(colorPos);

                snapshot[y * w + x] = Interpolate(colors[idx1], colors[idx2], blend);
            }
        }
    }

    private static Color GetLerpedColor(ColorScheme s, double t)
    {
        if (s.Version == 4)
        {
            if (t < 0.25)
                return LerpColor(s.C1, s.C2, t / 0.25);
            else if (t < 0.5)
                return LerpColor(s.C2, s.C3, (t - 0.25) / 0.25);
            else if (t < 0.75)
                return LerpColor(s.C3, s.C4, (t - 0.5) / 0.25);
            else
                return LerpColor(s.C4, s.C1, (t - 0.75) / 0.25);
        }
        else
        {
            if (t < 0.143)
                return LerpColor(s.C1, s.C2, t / 0.143);
            else if (t < 0.286)
                return LerpColor(s.C2, s.C3, (t - 0.143) / 0.143);
            else if (t < 0.429)
                return LerpColor(s.C3, s.C4, (t - 0.286) / 0.143);
            else if (t < 0.572)
                return LerpColor(s.C4, s.C5, (t - 0.429) / 0.143);
            else if (t < 0.715)
                return LerpColor(s.C5, s.C6, (t - 0.572) / 0.143);
            else if (t < 0.858)
                return LerpColor(s.C6, s.C7, (t - 0.715) / 0.143);
            else
                return LerpColor(s.C7, s.C1, (t - 0.858) / 0.142);
        }
    }

    private static Color LerpColor(Color c1, Color c2, double amount)
    {
        byte r = CalcChannel(c1.R, c2.R, amount);
        byte g = CalcChannel(c1.G, c2.G, amount);
        byte b = CalcChannel(c1.B, c2.B, amount);
        return Color.FromRgb(r, g, b);
    }

    private static byte CalcChannel(byte startVal, byte endVal, double amount)
    {
        double startD = startVal;
        double endD = endVal;
        double result = startD + (endD - startD) * amount;

        if (result < 0) result = 0;
        if (result > 255) result = 255;

        return (byte)Math.Round(result);
    }

    private static Color Interpolate(Color c1, Color c2, double t)
    {
        t = Math.Clamp(t, 0.0, 1.0);
        double r = c1.R + ((double)c2.R - c1.R) * t;
        double g = c1.G + ((double)c2.G - c1.G) * t;
        double b = c1.B + ((double)c2.B - c1.B) * t;

        return Color.FromRgb(
            (byte)Math.Round(r),
            (byte)Math.Round(g),
            (byte)Math.Round(b)
        );
    }

    // ----------------------------------------------------
    // FireShader
    // ----------------------------------------------------

    public static void RenderFireShader(this Color[] snapshot, int w, int h)
    {
        Color color4 = Color.FromRgb(64, 5, 0); // Base
        Color color1 = Color.FromRgb(255, 77, 0); // Body
        Color color2 = Color.FromRgb(255, 154, 0); // Heat
        Color color3 = Color.FromRgb(255, 213, 128); // Core
        double speed1 = 120;
        double speed2 = 10;
        double distance = 353;
        double amplitude = 47;
        double itime = DateTime.Now.TimeOfDay.TotalSeconds * 2.0;

        Vector2 speed = new Vector2((float)(speed1 / 100.0), (float)(speed2 / 100.0));
        double distBase = (distance / 100.0) - Math.Sin(itime * 0.4) / 1.89;
        double shift = 1.327 + Math.Sin(itime * 2.0) / 2.4;
        double amp = amplitude;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Vector2 p = new Vector2((float)(x * distBase / w), (float)(y * distBase / w));
                p.X -= (float)(itime / 1.1);

                double q = FBM(p - new Vector2((float)(itime * 0.01 + Math.Sin(itime) / 10.0), 0), amp);
                double qb = FBM(p - new Vector2((float)(itime * 0.002 + Math.Cos(itime) / 5.0), 0), amp);
                double q2 = FBM(p - new Vector2((float)(itime * 0.44 + 5.0 * Math.Cos(itime) / 7.0), 0), amp) - 6.0;
                double q3 = FBM(p - new Vector2((float)(itime * 0.9 + 10.0 * Math.Cos(itime) / 30.0), 0), amp) - 4.0;
                double q4 = FBM(p - new Vector2((float)(itime * 2.0 + 20.0 * Math.Sin(itime) / 20.0), 0), amp) + 2.0;

                double finalQ = (q + qb - 0.4 * q2 - 2.0 * q3 + 0.6 * q4) / 3.8;

                Vector2 r = new Vector2(
                    (float)FBM(p + new Vector2((float)(finalQ / 2.0 + itime * speed.X - p.X - p.Y), 0), amp),
                    (float)FBM(p + new Vector2((float)(finalQ - itime * speed.Y), 0), amp)
                );

                double fbmPR = FBM(p + r, amp);
                double resR = Lerp(color1.R, color2.R, fbmPR) + Lerp(color3.R, color4.R, r.X) - Lerp(25, 230, r.Y);
                double resG = Lerp(color1.G, color2.G, fbmPR) + Lerp(color3.G, color4.G, r.X) - Lerp(25, 230, r.Y);
                double resB = Lerp(color1.B, color2.B, fbmPR) + Lerp(color3.B, color4.B, r.X) - Lerp(25, 230, r.Y);

                double intensity = Math.Cos(shift * y / h);

                snapshot[y * w + x] = Color.FromRgb(
                    (byte)Math.Clamp(resR * intensity, 0, 255),
                    (byte)Math.Clamp(resG * intensity, 0, 255),
                    (byte)Math.Clamp(resB * intensity, 0, 255)
                );
            }
        }
    }

    private static double Rand(Vector2 n)
    {
        double dotProduct = Vector2.Dot(n, new Vector2(12.9898f, 12.1414f));
        return (Math.Sin(Math.Cos(dotProduct)) * 83758.5453) % 1.0;
    }

    private static double Noise(Vector2 n)
    {
        Vector2 b = new Vector2((float)Math.Floor(n.X), (float)Math.Floor(n.Y));
        Vector2 f = n - b;

        f = new Vector2(f.X * f.X * (3f - 2f * f.X), f.Y * f.Y * (3f - 2f * f.Y));

        Vector2 d = new Vector2(0f, 1f);
        return Lerp(
            Lerp(Rand(b), Rand(b + new Vector2(d.Y, d.X)), f.X),
            Lerp(Rand(b + new Vector2(d.X, d.Y)), Rand(b + new Vector2(d.Y, d.Y)), f.X),
            f.Y
        );
    }

    private static double FBM(Vector2 n, double amplitudeSetting)
    {
        double total = 0.0;
        double amp = 1.0;
        Vector2 p = n;

        for (int i = 0; i < 5; i++)
        {
            total += Noise(p) * amp;
            p *= 1.7f;
            amp *= (amplitudeSetting / 100.0);
        }
        return total;
    }

    private static double Lerp(double a, double b, double t)
    {
        return a + (b - a) * t;
    }

    // ----------------------------------------------------
    // AudioParty
    // ----------------------------------------------------

    public static void RenderAudioParty(this Color[] snapshot, int w, int h)
    {
        int divisions = 4;
        double gain = 65;
        double speed = 50;
        double colorSpeed = 25;
        double threshold = 20;

        float[] filteredData = GetOpenRGBSpectrum(AudioEngine.AudioBins, gain);

        float bassLevel = 0;
        for (int i = 0; i < 50; i++)
        {
            bassLevel = Math.Max(bassLevel, filteredData[i]);
        }
        _xShift += bassLevel * (speed / 1000.0);

        if (_animStep >= 1.0 && (DateTime.Now - _lastTriggerTime).TotalMilliseconds > 1500)
        {
            float trebleLevel = 0;
            for (int i = 150; i < 200; i++)
            {
                trebleLevel = Math.Max(trebleLevel, filteredData[i]);
            }

            if ((trebleLevel * (gain / 10.0)) > (threshold / 100.0))
            {
                _effectIdx = Random.Shared.Next(0, 7);
                _animStep = 0;
                _lastTriggerTime = DateTime.Now;
            }
        }

        float midLevel = 0;
        for (int i = 50; i < 150; i++)
        {
            midLevel = Math.Max(midLevel, filteredData[i]);
        }
        _colorShift += midLevel * (colorSpeed / 2000.0);

        if (_animStep < 1.0)
        {
            _animStep += 0.01 * (speed / 15.0);
        }

        for (int y = 0; y < h; y++)
        {
            double posY = (double)y / h;
            for (int x = 0; x < w; x++)
            {
                double posX = (double)x / w;

                double s = 0.5 * (1.0 + Math.Sin(posX * divisions * Math.PI + _xShift));
                double hue = (180.0 + Math.Sin(posY + _colorShift) * 180.0) % 360.0;
                Color baseCol = Utils.ColorFromHsv(hue, 1.0, 1.0);
                Color overlay = GetEffectOverlay(x, y, w, h);
                int idx = (y * w) + x;

                snapshot[idx] = Color.FromRgb(
                    ScreenBlend((byte)(baseCol.R * s), overlay.R),
                    ScreenBlend((byte)(baseCol.G * s), overlay.G),
                    ScreenBlend((byte)(baseCol.B * s), overlay.B)
                );
            }
        }
    }

    private static float[] GetOpenRGBSpectrum(float[] rawFreq, double gain)
    {
        float[] spectrum = new float[200];
        float maxVal = 1.0f;
        float sumVal = 1.0f;
        double linearLevel = Math.Pow(10, (3.0 + gain - 50.0) * 0.1);

        for (int i = 0; i < 200; i++)
        {
            sumVal += rawFreq[i];
            if (rawFreq[i] > maxVal) maxVal = rawFreq[i];
        }

        float fac = 2.0f / (maxVal + sumVal);
        for (int i = 0; i < 200; i++)
        {
            spectrum[i] = (float)(rawFreq[i] * fac * linearLevel);
        }
        return spectrum;
    }

    private static byte ScreenBlend(byte a, byte b)
    {
        return (byte)Math.Min(255f, (float)a + b - ((float)a * b / 255f));
    }

    private static Color GetEffectOverlay(int x, int y, int w, int h)
    {
        if (_animStep >= 1.0) return Colors.Black;
        switch (_effectIdx)
        {
            case 0: // Moving bar Left to Right
                return Math.Abs(x - _animStep * w) <= 1 ? Colors.White : Colors.Black;
            case 1: // Moving bar Right to Left
                return Math.Abs(x - (w - _animStep * w)) <= 1 ? Colors.White : Colors.Black;
            case 2: // Moving bar Top to Bottom
                return Math.Abs(y - _animStep * h) <= 1 ? Colors.White : Colors.Black;
            case 3: // Moving bar Bottom to Top
                return Math.Abs(y - (h - _animStep * h)) <= 1 ? Colors.White : Colors.Black;
            case 4: // Random RGB
                return Color.FromRgb((byte)Random.Shared.Next(0, 256), (byte)Random.Shared.Next(0, 256), (byte)Random.Shared.Next(0, 256));
            case 5: // Random White
                byte br = (byte)Random.Shared.Next(0, 256);
                return Color.FromRgb(br, br, br);
            case 6: // Blink/Flash
                byte val = (byte)(255 * Math.Max(0.0, 1.0 - _animStep));
                return Color.FromRgb(val, val, val);
            default:
                return Colors.Black;
        }
    }
}

public static class FastNoise
{
    public static double GetNoise(double x, double y, double z)
    {
        return (Math.Sin(x * 0.5 + z) + Math.Cos(y * 0.3 - z) + Math.Sin((x + y) * 0.2 + z * 0.5)) / 3.0;
    }
}
