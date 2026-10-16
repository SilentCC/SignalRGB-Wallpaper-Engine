using Avalonia.Media;

namespace Wallpaper_UI;

public enum LEDShape
{
    Rectangle,
    RoundedRectangle,
    Sphere
}

public enum MatrixSizeType
{
    Landscape4_3,
    Portrait4_3,
    Landscape5_4,
    Portrait5_4,
    Landscape16_9,
    Portrait16_9,
    Landscape16_10,
    Portrait16_10,
    Landscape21_9,
    Portrait21_9,
    Landscape32_9,
    Portrait32_9,
    Landscape4_1,
    Portrait4_1
}

public enum MatrixSizeTier
{
    Small,
    Normal,
    Large,
    XLarge
}

public enum ShutdownEffect
{
    SolidColor,
    Aurora,
    Breathing,
    RainbowLeft,
    RainbowRight,
    NeonLeft,
    NeonRight,
    SunsetLeft,
    SunsetRight,
    AudioParty,
    RainbowCycle,
    RainbowPinWheel,
    Fire
}

public struct ColorScheme
{
    public Color C1 { get; set; }
    public Color C2 { get; set; }
    public Color C3 { get; set; }
    public Color C4 { get; set; }
    public Color C5 { get; set; }
    public Color C6 { get; set; }
    public Color C7 { get; set; }
    public int Version { get; set; }
}
