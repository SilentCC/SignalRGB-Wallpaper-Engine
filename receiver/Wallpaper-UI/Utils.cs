using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Wallpaper_UI;

public static class Utils
{
    public static string SaveFile = "usersave2.json";
    public static UserSave MySave = new UserSave();
    public static ShutdownEffect ShutdownEffect = ShutdownEffect.SolidColor;
    public static int ShowFps = 0;
    public static int BlurIntensity = 0;
    public static int FPS = 60;
    public static int CpuUsagePauseValue = 60;
    public static string? BackgroundImage = null;
    public static Stretch Stretch = Stretch.Uniform;
    public static string BackgroundColor = "#FF000000";
    public static int Port = 8133;

    private static readonly HttpClient _httpClient = new HttpClient();

    public static void UpdateSRGBConfigValues(RGBSettingsChangedEventArgs s)
    {
        try
        {
            ShutdownEffect = s.ShutdownEffect;
            ShowFps = s.ShowFps ? 1 : 0;
            BlurIntensity = s.BlurIntensity;
            FPS = s.FPS;
            Stretch = s.CoverImageStretch;
            BackgroundColor = ConvertToHex(s.BackgroundColor);
            CpuUsagePauseValue = s.CPUUsagePauseValue;
            BackgroundImage = s.CoverImage;
        }
        catch (Exception ex)
        {
            Logger.Log($"{ex.Message} {ex.StackTrace}");
        }
    }

    public static void LoadConfiguration()
    {
        try
        {
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
            if (File.Exists(configPath))
            {
                string json = File.ReadAllText(configPath);
                var config = Newtonsoft.Json.JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string, int>>(json);
                if (config != null && config.TryGetValue("Port", out int port))
                {
                    Port = port;
                }
            }
            else
            {
                var defaultConfig = new System.Collections.Generic.Dictionary<string, int> { { "Port", 8133 } };
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(defaultConfig, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(configPath, json);
                Port = 8133;
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"Error loading config.json: {ex.Message}");
            Port = 8133;
        }
    }

    public static void ReadSaveValues(UserSave s)
    {
        try
        {
            ShutdownEffect = s.ShutdownEffect;
            ShowFps = s.ShowFps ? 1 : 0;
            BlurIntensity = s.BlurIntensity;
            FPS = s.FPS;
            Stretch = s.CoverImageStretch;
            BackgroundColor = ConvertToHex(s.BackgroundColor);
            CpuUsagePauseValue = s.CpuUsagePauseValue;
            BackgroundImage = s.CoverImage;
        }
        catch (Exception ex)
        {
            Logger.Log($"{ex.Message} {ex.StackTrace}");
        }
    }

    public static string ConvertToHex(Color color, bool argb = false)
    {
        if (argb)
        {
            return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
        }
        else
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }
    }

    public static IBrush SetBrushColor(string hex)
    {
        try
        {
            if (Color.TryParse(hex, out var color))
            {
                return new SolidColorBrush(color);
            }
            return Brushes.Transparent;
        }
        catch
        {
            return Brushes.Transparent;
        }
    }

    public static Bitmap? TryParseCoverImage(string? src)
    {
        if (string.IsNullOrWhiteSpace(src)) return null;

        try
        {
            string cleanSrc = src.Trim().Replace("\"", "");

            if (cleanSrc.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                // Synchronously download the image for compatibility with WPF synchronous flow
                try
                {
                    byte[] data = Task.Run(() => _httpClient.GetByteArrayAsync(cleanSrc)).GetAwaiter().GetResult();
                    using var ms = new MemoryStream(data);
                    return new Bitmap(ms);
                }
                catch (Exception ex)
                {
                    Logger.Log($"Error downloading web image: {ex.Message}");
                    return null;
                }
            }
            else
            {
                string finalPath;
                if (Path.IsPathRooted(cleanSrc))
                {
                    finalPath = cleanSrc;
                }
                else
                {
                    finalPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, cleanSrc);
                }

                if (!File.Exists(finalPath))
                {
                    Debug.WriteLine($"File not found: {finalPath}");
                    return null;
                }

                return new Bitmap(finalPath);
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"{ex.Message} {ex.StackTrace}");
            return null;
        }
    }

    public static bool IsProcessRunning(string processName)
    {
        try
        {
            var processes = Process.GetProcesses();
            return Array.Exists(processes, p => !string.IsNullOrEmpty(p.ProcessName) && p.ProcessName.Contains(processName, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            Logger.Log($"Error getting processes: {ex.Message}");
            return false;
        }
    }

    public static void SetBlurIntensity(this Avalonia.Controls.Control control, int intensity)
    {
        if (intensity <= 0)
        {
            control.Effect = null;
        }
        else
        {
            if (control.Effect is BlurEffect blur && blur.Radius == intensity) return;
            control.Effect = new BlurEffect { Radius = intensity };
        }
    }

    public static int FpsToFrametime(int fps)
    {
        if (fps <= 0) return 1;
        return (int)(1000.0 / fps);
    }

    public static Color ColorFromHsl(double h, double s, double l)
    {
        double r, g, b;
        if (s == 0)
        {
            r = l;
            g = l;
            b = l;
        }
        else
        {
            double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            double p = 2 * l - q;
            r = HueToRgb(p, q, h + 1.0 / 3.0);
            g = HueToRgb(p, q, h);
            b = HueToRgb(p, q, h - 1.0 / 3.0);
        }
        return Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1.0 / 6.0) return p + (q - p) * 6 * t;
        if (t < 1.0 / 2.0) return q;
        if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6;
        return p;
    }

    public static Color ColorFromHsv(double h, double s, double v)
    {
        h = ((h % 360) + 360) % 360;
        s = Math.Clamp(s, 0.0, 1.0);
        v = Math.Clamp(v, 0.0, 1.0);

        double c = v * s;
        double x = c * (1.0 - Math.Abs(((h / 60.0) % 2.0) - 1.0));
        double m = v - c;

        double r1, g1, b1;

        if (h < 60)
        {
            r1 = c; g1 = x; b1 = 0;
        }
        else if (h < 120)
        {
            r1 = x; g1 = c; b1 = 0;
        }
        else if (h < 180)
        {
            r1 = 0; g1 = c; b1 = x;
        }
        else if (h < 240)
        {
            r1 = 0; g1 = x; b1 = c;
        }
        else if (h < 300)
        {
            r1 = x; g1 = 0; b1 = c;
        }
        else
        {
            r1 = c; g1 = 0; b1 = x;
        }

        return Color.FromRgb(
            (byte)((r1 + m) * 255),
            (byte)((g1 + m) * 255),
            (byte)((b1 + m) * 255)
        );
    }
}
