using System;
using System.IO;

namespace Wallpaper_UI;

public static class Logger
{
    public static void Log(string message)
    {
        try
        {
            string filename = Path.Combine(".", $"Wallpaper-UI.log");
            File.AppendAllText(filename, $"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}: {message}{Environment.NewLine}");
        }
        catch
        {
            // Ignore logging errors to prevent crash
        }
    }

    public static void Capture(string message)
    {
        try
        {
            string filename = Path.Combine(".", "Captures.log");
            File.AppendAllText(filename, $"{DateTime.Now.ToShortTimeString()}: {message}{Environment.NewLine}");
        }
        catch
        {
            // Ignore logging errors to prevent crash
        }
    }
}
