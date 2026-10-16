using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace Wallpaper_UI;

public class LedCanvas : Control
{
    private RGBClient? _client;
    public RGBClient? Client
    {
        get => _client;
        set
        {
            if (ReferenceEquals(_client, value)) return;
            _client = value;
            RefreshSettings();
        }
    }

    private string _currentFpsDisplay = "0";
    public string CurrentFps => _currentFpsDisplay;

    private Color[] _stableColorSnapshot = Array.Empty<Color>();
    private readonly Dictionary<Color, IBrush> _brushCache = new();
    private readonly Stopwatch _fpsWatch = new();

    private double _lastFpsUpdateTime = 0;
    private int _frameCount = 0;
    private DispatcherTimer? _timer;
    private int _timerFps;
    private long _lastInvalidatedVersion = -1;
    private bool _wasOffline;
    private WriteableBitmap? _bitmap;
    private byte[] _pixelRow = Array.Empty<byte>();
    private long _bitmapVersion = -1;

    public LedCanvas()
    {
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        RestartTimer();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _timer?.Stop();
        _bitmap?.Dispose();
        _bitmap = null;
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _timer?.Start();
        RefreshSettings();
    }

    public void RefreshSettings()
    {
        _lastInvalidatedVersion = -1;
        _bitmapVersion = -1;
        RestartTimer();
        InvalidateVisual();
    }

    private void RestartTimer()
    {
        int targetFps = _client != null && _client.FPS > 0 ? _client.FPS : 60;
        if (_timer != null && _timerFps == targetFps) return;
        if (_timer != null)
        {
            _timer.Stop();
            _timer.Tick -= OnTimerTick;
        }
        _timerFps = targetFps;

        // Set interval to target frame time
        double intervalMs = 1000.0 / targetFps;
        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(Math.Max(1, intervalMs))
        };
        _timer.Tick += OnTimerTick;
        _timer.Start();

        if (!_fpsWatch.IsRunning)
        {
            _fpsWatch.Start();
        }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (_client == null || !_client.IsListening) return;

        double currentTime = _fpsWatch.Elapsed.TotalMilliseconds;
        if (currentTime - _lastFpsUpdateTime >= 500)
        {
            double actualFps = (_frameCount * 1000.0) / (currentTime - _lastFpsUpdateTime);
            _currentFpsDisplay = $"{(int)Math.Ceiling(actualFps)}";

            _frameCount = 0;
            _lastFpsUpdateTime = currentTime;
        }

        bool offline = (DateTime.Now - _client.LastPacketTime).TotalSeconds > 2;
        long version = _client.FrameVersion;
        if (!offline && !_wasOffline && version == _lastInvalidatedVersion) return;
        _wasOffline = offline;
        _lastInvalidatedVersion = version;
        InvalidateVisual();
    }

    private IBrush GetBrush(Color color)
    {
        if (!_brushCache.TryGetValue(color, out var brush))
        {
            if (_brushCache.Count > 2000) _brushCache.Clear();
            brush = new SolidColorBrush(color);
            _brushCache[color] = brush;
        }
        return brush;
    }

    public override void Render(DrawingContext dc)
    {
        base.Render(dc);

        if (_client == null || !_client.IsListening) return;

        int w = (int)_client.MatrixSize.Width;
        int h = (int)_client.MatrixSize.Height;
        if (w < 1 || h < 1) return;

        _frameCount++;
        long version = _client.FrameVersion;
        bool offline = (DateTime.Now - _client.LastPacketTime).TotalSeconds > 2;

        if (offline)
        {
            // Offline/Shutdown effects
            int totalLeds = w * h;
            if (_stableColorSnapshot.Length != totalLeds)
            {
                _stableColorSnapshot = new Color[totalLeds];
            }

            switch (_client.ShutdownEffect)
            {
                case ShutdownEffect.Aurora:
                    _stableColorSnapshot.RenderAurora(w, h);
                    break;
                case ShutdownEffect.Breathing:
                    _stableColorSnapshot.RenderBreathingColor(w, h, _client.ShutdownColor);
                    break;
                case ShutdownEffect.RainbowLeft:
                    _stableColorSnapshot.RenderSunsetNeon(w, h, Effects.RainbowScheme, "left");
                    break;
                case ShutdownEffect.RainbowRight:
                    _stableColorSnapshot.RenderSunsetNeon(w, h, Effects.RainbowScheme, "right");
                    break;
                case ShutdownEffect.NeonLeft:
                    _stableColorSnapshot.RenderSunsetNeon(w, h, Effects.NeonScheme, "left");
                    break;
                case ShutdownEffect.NeonRight:
                    _stableColorSnapshot.RenderSunsetNeon(w, h, Effects.NeonScheme, "right");
                    break;
                case ShutdownEffect.SunsetLeft:
                    _stableColorSnapshot.RenderSunsetNeon(w, h, Effects.SunsetScheme, "left");
                    break;
                case ShutdownEffect.SunsetRight:
                    _stableColorSnapshot.RenderSunsetNeon(w, h, Effects.SunsetScheme, "right");
                    break;
                case ShutdownEffect.AudioParty:
                    if (!AudioEngine.IsActive) AudioEngine.Start();
                    _stableColorSnapshot.RenderAudioParty(w, h);
                    break;
                case ShutdownEffect.RainbowCycle:
                    _stableColorSnapshot.RenderRainbowCycle(w, h, Effects.RainbowScheme);
                    break;
                case ShutdownEffect.RainbowPinWheel:
                    _stableColorSnapshot.RenderRainbowPinwheel(w, h, Effects.RainbowScheme);
                    break;
                case ShutdownEffect.Fire:
                    _stableColorSnapshot.RenderFireShader(w, h);
                    break;
                default:
                    _stableColorSnapshot.RenderSolidColor(w, h, _client.ShutdownColor);
                    break;
            }
        }
        else
        {
            if (AudioEngine.IsActive) AudioEngine.Stop();
            int totalLeds = w * h;
            if (_stableColorSnapshot.Length != totalLeds)
                _stableColorSnapshot = new Color[totalLeds];
            version = _client.CopyColorsTo(_stableColorSnapshot);
        }

        if (_client.LEDShape == LEDShape.Rectangle && _client.LEDPadding == 0)
        {
            DrawPixelGrid(dc, w, h, version, offline);
            return;
        }

        double cellW = Bounds.Width / w;
        double cellH = Bounds.Height / h;
        int count = 0;

        for (int j = 0; j < h; j++)
        {
            for (int i = 0; i < w; i++)
            {
                if (count >= _stableColorSnapshot.Length) break;

                var col = _stableColorSnapshot[count];

                if (col.A > 0 || col.R > 0 || col.G > 0 || col.B > 0)
                {
                    DrawLed(dc, i, j, col, cellW, cellH);
                }
                count++;
            }
        }
    }

    private void DrawPixelGrid(DrawingContext dc, int width, int height, long version, bool offline)
    {
        if (_bitmap == null || _bitmap.PixelSize.Width != width || _bitmap.PixelSize.Height != height)
        {
            _bitmap?.Dispose();
            _bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96),
                PixelFormat.Bgra8888, AlphaFormat.Unpremul);
            _pixelRow = new byte[width * 4];
            _bitmapVersion = -1;
        }
        if (offline || version != _bitmapVersion)
        {
            using var framebuffer = _bitmap.Lock();
            for (int y = 0; y < height; y++)
            {
                EncodeBgraRow(_stableColorSnapshot.AsSpan(y * width, width), _pixelRow);
                Marshal.Copy(_pixelRow, 0, IntPtr.Add(framebuffer.Address, y * framebuffer.RowBytes), _pixelRow.Length);
            }
            _bitmapVersion = version;
        }
        dc.DrawImage(_bitmap, new Rect(0, 0, width, height), new Rect(Bounds.Size));
    }

    internal static void EncodeBgraRow(ReadOnlySpan<Color> colors, Span<byte> pixels)
    {
        if (pixels.Length != colors.Length * 4)
            throw new ArgumentException("Pixel row length does not match the color count.");
        for (int i = 0; i < colors.Length; i++)
        {
            pixels[i * 4] = colors[i].B;
            pixels[i * 4 + 1] = colors[i].G;
            pixels[i * 4 + 2] = colors[i].R;
            pixels[i * 4 + 3] = colors[i].A;
        }
    }

    private void DrawLed(DrawingContext dc, int i, int j, Color color, double cellW, double cellH)
    {
        var brush = GetBrush(color);
        double padding = _client != null ? _client.LEDPadding : 0;

        double x = (cellW * i) + (padding / 2.0);
        double y = (cellH * j) + (padding / 2.0);
        double drawW = Math.Max(0.0, cellW - padding);
        double drawH = Math.Max(0.0, cellH - padding);

        var rect = new Rect(x, y, drawW, drawH);

        var shape = _client != null ? _client.LEDShape : LEDShape.Rectangle;
        switch (shape)
        {
            case LEDShape.Rectangle:
                dc.DrawRectangle(brush, null, rect);
                break;
            case LEDShape.RoundedRectangle:
                double radius = _client != null ? _client.RoundedRectangleCornerRadius : 4.0;
                dc.DrawRectangle(brush, null, new RoundedRect(rect, radius));
                break;
            case LEDShape.Sphere:
                var center = new Point(x + (drawW / 2.0), y + (drawH / 2.0));
                dc.DrawEllipse(brush, null, center, drawW / 2.0, drawH / 2.0);
                break;
        }
    }
}
