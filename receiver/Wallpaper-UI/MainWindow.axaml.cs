using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace Wallpaper_UI;

public partial class MainWindow : Window
{
    public RGBClient? rgbClient = null;
    public Thread? rgbThread = null;

    private string? connectString = null;
    private bool drawErrorStringOnScreen = true;
    private DispatcherTimer tmConfig;
    private DispatcherTimer tmCheckRGBSoftware;
    private DispatcherTimer tmUpdate;

    private string? _currentLoadedImage = null;
    private IImage? _cachedImage = null;
    private string? _appliedBackgroundColor;
#if WINDOWS
    private DispatcherTimer? _startupTimer = null;
    private int _startupTicksCount = 0;
#endif

#if WINDOWS
    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetParent(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string? lpszClass, string? lpszWindow);

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;
    private const int WS_CHILD = 0x40000000;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;

    private static void AlignZOrder(IntPtr hwnd, IntPtr parent)
    {
        // 1. Check if SHELLDLL_DefView is a child of the same parent (sibling layout)
        IntPtr shellDlg = FindWindowEx(parent, IntPtr.Zero, "SHELLDLL_DefView", null);
        if (shellDlg != IntPtr.Zero)
        {
            // Sibling layout: place our window immediately behind the desktop icons container
            SetWindowPos(hwnd, shellDlg, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
            return;
        }

        // 2. Multi-parent layout (e.g. Windows 11 24H2): find the top-level window containing SHELLDLL_DefView
        IntPtr shellViewParent = IntPtr.Zero;
        EnumWindows((tophandle, topparamhandle) =>
        {
            IntPtr p = FindWindowEx(tophandle, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (p != IntPtr.Zero)
            {
                shellViewParent = tophandle;
                return false; // Stop enumeration
            }
            return true;
        }, IntPtr.Zero);

        if (shellViewParent != IntPtr.Zero && parent != shellViewParent)
        {
            // Place our parent window immediately behind the icons' parent window in top-level Z-order
            SetWindowPos(parent, shellViewParent, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
        }
    }
#endif

    public MainWindow()
    {
        InitializeComponent();

#if DEBUG
        WindowDecorations = global::Avalonia.Controls.WindowDecorations.BorderOnly;
#endif

        tmConfig = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5), IsEnabled = true };
        tmConfig.Tick += tmConfig_Tick;

        tmCheckRGBSoftware = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10), IsEnabled = false };
        tmCheckRGBSoftware.Tick += tmCheckRGBSoftware_Tick;

        tmUpdate = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1), IsEnabled = true };
        tmUpdate.Tick += tmUpdate_Tick;

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private void MainWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        try
        {
            Utils.LoadConfiguration();

            if (File.Exists(Utils.SaveFile))
            {
                Utils.MySave = UserSave.Load(Utils.SaveFile);
                Utils.ReadSaveValues(Utils.MySave);
            }

            UpdateVisuals();

#if WINDOWS
            var hwnd = this.TryGetPlatformHandle()?.Handle;
            if (hwnd.HasValue && hwnd.Value != IntPtr.Zero)
            {
                int extendedStyle = GetWindowLong(hwnd.Value, GWL_EXSTYLE);
                SetWindowLong(hwnd.Value, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
            }

            // Start a fast startup timer to check for Lively's parenting immediately
            _startupTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50),
                IsEnabled = true
            };
            _startupTimer.Tick += tmStartupCheck_Tick;
            _startupTimer.Start();
#endif

            Connect();
        }
        catch (Exception ex)
        {
            Logger.Log($"{ex.Message} {ex.StackTrace}");
        }
    }

    public void Connect()
    {
        try
        {
            if (rgbClient == null)
            {
                rgbClient = new RGBClient(Utils.MySave, Utils.Port);
                rgbClient.SettingsChanged += rgbClient_SettingsChanged;
                rgbClient.ListenerFailed += (_, message) => Dispatcher.UIThread.Post(() =>
                {
                    connectString = $"UDP listener failed: {message}";
                    UpdateStatusText();
                    tmCheckRGBSoftware.Start();
                });
            }

            tmCheckRGBSoftware.Stop();
            if (rgbThread != null && rgbThread.IsAlive)
            {
                rgbClient.StopListening();
                if (!rgbThread.Join(500))
                    throw new InvalidOperationException("Previous UDP listener has not stopped.");
            }

            rgbThread = new Thread(rgbClient.StartListening) { IsBackground = true };
            rgbThread.Start();
            connectString = null;
            UpdateStatusText();
            ledCanvas.Client = rgbClient;
        }
        catch (Exception ex)
        {
            Logger.Log($"{ex.Message} {ex.StackTrace}");
            connectString = $"{Environment.NewLine}UDP listener failed: {ex.Message}";
            UpdateStatusText();
            tmCheckRGBSoftware.Start();
        }
    }

    private void tmUpdate_Tick(object? sender, EventArgs e)
    {
        txtFPS.IsVisible = Utils.ShowFps != 0;
        if (Utils.ShowFps != 0)
        {
            txtFPS.Text = ledCanvas.CurrentFps;
        }

        if (connectString != null)
        {
            UpdateStatusText();
        }

#if WINDOWS
        // Check if Lively has parented our window, and if so, dynamically configure it as a Z-ordered desktop child
        var hwnd = this.TryGetPlatformHandle()?.Handle;
        if (hwnd.HasValue && hwnd.Value != IntPtr.Zero)
        {
            IntPtr parent = GetParent(hwnd.Value);
            int style = GetWindowLong(hwnd.Value, GWL_STYLE);

            if (parent != IntPtr.Zero)
            {
                if ((style & WS_CHILD) == 0)
                {
                    style |= WS_CHILD;
                    SetWindowLong(hwnd.Value, GWL_STYLE, style);
                }

                // Align the window Z-order so it is placed behind the desktop icons but in front of Windows wallpaper
                AlignZOrder(hwnd.Value, parent);
            }
        }
#endif
    }

#if WINDOWS
    private void tmStartupCheck_Tick(object? sender, EventArgs e)
    {
        _startupTicksCount++;

        var hwnd = this.TryGetPlatformHandle()?.Handle;
        if (hwnd.HasValue && hwnd.Value != IntPtr.Zero)
        {
            IntPtr parent = GetParent(hwnd.Value);
            if (parent != IntPtr.Zero)
            {
                int style = GetWindowLong(hwnd.Value, GWL_STYLE);
                if ((style & WS_CHILD) == 0)
                {
                    style |= WS_CHILD;
                    SetWindowLong(hwnd.Value, GWL_STYLE, style);
                }

                // Place the window immediately behind desktop icons
                AlignZOrder(hwnd.Value, parent);

                // Stop and clean up the startup timer
                _startupTimer?.Stop();
                _startupTimer = null;
                return;
            }
        }

        // Timeout after 10 seconds (200 ticks of 50ms) if no parent is found (e.g. debug mode)
        if (_startupTicksCount > 200)
        {
            _startupTimer?.Stop();
            _startupTimer = null;
        }
    }
#endif

    private void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
    {
        if (rgbClient != null && rgbClient.IsListening)
        {
            rgbClient.StopListening();
            rgbThread = null;
        }
    }

    private void tmCheckRGBSoftware_Tick(object? sender, EventArgs e)
    {
        connectString = $"{Environment.NewLine}Retrying the UDP listener...";
        UpdateStatusText();
        Connect();
    }

    internal void UpdateVisuals()
    {
        try
        {
            if (_appliedBackgroundColor != Utils.BackgroundColor)
            {
                Background = Utils.SetBrushColor(Utils.BackgroundColor);
                _appliedBackgroundColor = Utils.BackgroundColor;
            }
            ledCanvas.SetBlurIntensity(Utils.BlurIntensity);
            DiffuserImage.Stretch = Utils.Stretch;

            if (Utils.BackgroundImage != _currentLoadedImage)
            {
                _currentLoadedImage = Utils.BackgroundImage;
                if (_currentLoadedImage == null)
                {
                    _cachedImage = null;
                    DiffuserImage.Source = null;
                }
                else
                {
                    string imgPath = _currentLoadedImage;
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        var bmp = Utils.TryParseCoverImage(imgPath);
                        Dispatcher.UIThread.Post(() =>
                        {
                            if (Utils.BackgroundImage == imgPath)
                            {
                                _cachedImage = bmp;
                                DiffuserImage.Source = _cachedImage;
                            }
                        });
                    });
                }
            }
            else
            {
                DiffuserImage.Source = _cachedImage;
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"{ex.Message} {ex.StackTrace}");
        }
    }

    private void tmConfig_Tick(object? sender, EventArgs e)
    {
        UpdateVisuals();
    }

    private void rgbClient_SettingsChanged(object? sender, RGBSettingsChangedEventArgs e)
    {
        Utils.UpdateSRGBConfigValues(e);

        var s = Utils.MySave;
        s.MatrixSizeType = e.MatrixSizeType;
        s.MatrixSizeTier = e.MatrixSizeTier;
        s.ShutdownEffect = e.ShutdownEffect;
        s.ShowFps = e.ShowFps;
        s.BlurIntensity = e.BlurIntensity;
        s.LedShape = e.LEDShape;
        s.RoundedRectangleCornerRadius = e.RoundedRectangleCornerRadius;
        s.LedPadding = e.LEDPadding;
        s.FPS = e.FPS;
        s.CoverImageStretch = e.CoverImageStretch;
        s.BackgroundColor = e.BackgroundColor;
        s.CpuUsagePauseValue = e.CPUUsagePauseValue;
        s.CoverImage = e.CoverImage;
        s.ShutdownColor = e.ShutdownColor;

        Utils.MySave.Save(Utils.SaveFile);

        Dispatcher.UIThread.Post(() =>
        {
            ledCanvas.RefreshSettings();
            UpdateVisuals();
        });
    }

    private void UpdateStatusText()
    {
        if (drawErrorStringOnScreen)
        {
            txtConnectStatus.Text = connectString;
        }
    }
}