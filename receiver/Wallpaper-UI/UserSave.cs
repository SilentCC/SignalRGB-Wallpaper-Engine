using System.IO;
using Avalonia.Media;
using Newtonsoft.Json;

namespace Wallpaper_UI;

public class UserSave
{
    public MatrixSizeType MatrixSizeType;
    public MatrixSizeTier MatrixSizeTier;
    public ShutdownEffect ShutdownEffect;
    public bool ShowFps;
    public int BlurIntensity;
    public LEDShape LedShape;
    public int RoundedRectangleCornerRadius;
    public float LedPadding;
    public int FPS;
    public Stretch CoverImageStretch;
    public Color BackgroundColor;
    public Color ShutdownColor;
    public int CpuUsagePauseValue;
    public string? CoverImage;

    public static UserSave Load(string filename)
    {
        var json = File.ReadAllText(filename);
        return JsonConvert.DeserializeObject<UserSave>(json) ?? new UserSave();
    }

    public void Save(string filename)
    {
        var json = JsonConvert.SerializeObject(this, Formatting.Indented);
        File.WriteAllText(filename, json);
    }
}
