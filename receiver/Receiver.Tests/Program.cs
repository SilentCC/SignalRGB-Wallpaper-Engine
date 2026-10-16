using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Wallpaper_UI;

if (args.Length == 2 && args[0] == "--benchmark")
{
    ReceiverBenchmarks.Run(args[1]);
    return;
}

string originalDirectory = Environment.CurrentDirectory;
string testState = Path.Combine(AppContext.BaseDirectory, "test-state-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(testState);
Environment.CurrentDirectory = testState;
AppBuilder.Configure<Application>()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .UseSkia()
    .SetupWithoutStarting();
var sender = new IPEndPoint(IPAddress.Loopback, 12345);
int passed = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    Interlocked.Increment(ref passed);
}

byte[] Settings(int aspect = 4, int tier = 3, string image = "")
{
    byte[] text = Encoding.UTF8.GetBytes(image);
    byte[] packet = new byte[19 + text.Length];
    packet[0] = 1; packet[1] = (byte)aspect; packet[2] = (byte)tier;
    packet[9] = 60; packet[10] = 2; packet[18] = (byte)(text.Length & 255);
    text.CopyTo(packet, 19);
    return packet;
}

List<byte[]> Frame(int leds, int seed = 0)
{
    int count = (leds + 479) / 480;
    var packets = new List<byte[]>();
    for (int index = 0; index < count; index++)
    {
        var packet = new byte[3 + Math.Min(480, leds - index * 480) * 3];
        packet[1] = (byte)index; packet[2] = (byte)count;
        for (int offset = 3; offset < packet.Length; offset++)
            packet[offset] = (byte)((index * 1440 + offset - 3 + seed) & 255);
        packets.Add(packet);
    }
    return packets;
}

var client = new RGBClient(new UserSave());
int changes = 0;
client.SettingsChanged += (_, _) => changes++;
var settings = Settings();
for (int i = 0; i < 200; i++) client.ProcessRGBPacket(settings, sender);
Check(changes == 1, "Identical settings must not trigger repeated saves/UI timer updates.");
settings[5] = 20;
client.ProcessRGBPacket(settings, sender);
Check(changes == 2, "A real setting change must be delivered.");
var unicode = Settings(image: new string('\u4e2d', 100));
client.ProcessRGBPacket(unicode, sender);
Check(client.CoverImage == new string('\u4e2d', 100), "Keep compatibility with wrapped legacy UTF-8 image lengths.");
client.ProcessRGBPacket(new byte[] { 1, 4, 3 }, sender);
Check(client.MatrixSizeTier == MatrixSizeTier.XLarge && changes == 3, "Truncated settings must not change state.");

foreach (var (aspect, leds, width, height) in new[] { (4, 9216, 128, 72), (9, 12096, 72, 168) })
{
    client = new RGBClient(new UserSave());
    client.ProcessRGBPacket(Settings(aspect), sender);
    Check(client.MatrixSize.Width == width && client.MatrixSize.Height == height, "Keep exact X Large geometry.");
    var packets = Frame(leds);
    long version = client.FrameVersion;
    foreach (var packet in packets.Take(packets.Count - 1)) client.ProcessRGBPacket(packet, sender);
    Check(client.FrameVersion == version, "A partial frame must never be published.");
    client.ProcessRGBPacket(Settings(aspect), sender);
    client.ProcessRGBPacket(packets[^1], sender);
    Check(client.FrameVersion == version + 1, "Unchanged settings must not interrupt an in-progress frame.");
    var colors = new Color[leds];
    client.CopyColorsTo(colors);
    for (int i = 0; i < colors.Length; i++)
        Check(colors[i] == Color.FromRgb((byte)(i * 3 & 255), (byte)((i * 3 + 1) & 255), (byte)((i * 3 + 2) & 255)),
            "Frame assembly changed RGB order.");

    version = client.FrameVersion;
    client.ProcessRGBPacket(packets[0], sender);
    foreach (var packet in packets.Skip(2)) client.ProcessRGBPacket(packet, sender);
    Check(client.FrameVersion == version, "Missing packets must not publish a shifted frame.");
    packets = Frame(leds, 1);
    foreach (var packet in packets) client.ProcessRGBPacket(packet, sender);
    Check(client.FrameVersion == version + 1, "Recover on the next intact frame.");
    version = client.FrameVersion;
    client.ProcessRGBPacket(packets[0], sender);
    client.ProcessRGBPacket(packets[2], sender);
    client.ProcessRGBPacket(packets[1], sender);
    foreach (var packet in packets.Skip(3)) client.ProcessRGBPacket(packet, sender);
    Check(client.FrameVersion == version, "Out-of-order packets must not corrupt the displayed frame.");
    client.ProcessRGBPacket(packets[0], sender);
    client.ProcessRGBPacket(packets[1], new IPEndPoint(IPAddress.Parse("127.0.0.2"), 54321));
    foreach (var packet in packets.Skip(2)) client.ProcessRGBPacket(packet, sender);
    Check(client.FrameVersion == version, "Do not combine packets from different sender addresses.");
    packets = Frame(leds, 2);
    client.ProcessRGBPacket(packets[0], sender);
    foreach (var packet in packets.Skip(1))
        client.ProcessRGBPacket(packet, new IPEndPoint(sender.Address, 54321));
    Check(client.FrameVersion == version + 1, "Do not require senders to reuse a UDP source port.");
    version = client.FrameVersion;
    var wrongLength = (byte[])packets[0].Clone();
    Array.Resize(ref wrongLength, wrongLength.Length - 1);
    client.ProcessRGBPacket(wrongLength, sender);
    Check(client.FrameVersion == version, "Reject malformed packet sizes.");
    client.ProcessRGBPacket(packets[0], sender);
    client.ProcessRGBPacket(Settings(aspect, 1), sender);
    foreach (var packet in packets.Skip(1)) client.ProcessRGBPacket(packet, sender);
    Check(client.FrameVersion == version, "A geometry change must discard in-flight data.");
    Check(client.DroppedFrames > 0, "Expose loss/invalid assembly via a diagnostic counter.");
}

client = new RGBClient(new UserSave());
client.ProcessRGBPacket(Settings(9, 2), sender);
Check(client.MatrixSize.Width == 36 && client.MatrixSize.Height == 84, "Fix legacy 21:9 portrait Large height mismatch.");
var row = new[] { Color.FromArgb(128, 1, 2, 3), Color.FromRgb(254, 253, 252) };
var pixels = new byte[8];
LedCanvas.EncodeBgraPixels(row, pixels);
Check(pixels.SequenceEqual(new byte[] { 3, 2, 1, 128, 252, 253, 254, 255 }), "Bitmap fast path must preserve BGRA channels and alpha.");

var heartbeatClock = new ManualTimeProvider();
var heartbeat = new RGBClient(new UserSave(), timeProvider: heartbeatClock);
heartbeat.ProcessRGBPacket(Settings(), sender);
var heartbeatFrame = Frame(9216, 13);
foreach (var packet in heartbeatFrame) heartbeat.ProcessRGBPacket(packet, sender);
for (int i = 0; i < 8; i++)
{
    heartbeatClock.Advance(TimeSpan.FromSeconds(1));
    foreach (var packet in heartbeatFrame) heartbeat.ProcessRGBPacket(packet, sender);
    Check(heartbeat.FrameVersion == 1, "Identical complete RGB frames must not publish new visual versions.");
    Check(!heartbeat.IsOffline && heartbeat.LastPacketTime == heartbeatClock.GetLocalNow().DateTime,
        "Repeated RGB frames must still refresh the valid-frame heartbeat.");
}
heartbeatClock.Advance(TimeSpan.FromSeconds(3));
heartbeat.ProcessRGBPacket(Settings(), sender);
foreach (var packet in heartbeatFrame.SkipLast(1)) heartbeat.ProcessRGBPacket(packet, sender);
Check(heartbeat.IsOffline, "Settings and incomplete frames must not keep the sender online.");
heartbeat.ProcessRGBPacket(heartbeatFrame[^1], sender);
Check(!heartbeat.IsOffline && heartbeat.FrameVersion == 1,
    "An identical complete frame must restore online status without a false color change.");
var detachedColors = heartbeat.Colors;
detachedColors[0] = Colors.Transparent;
var snapshot = new Color[9223];
heartbeat.CopyColorsTo(snapshot);
Check(snapshot[0] == Color.FromRgb(13, 14, 15), "Public color snapshots must not expose recyclable frame storage.");
Check(snapshot.AsSpan(9216).ToArray().All(c => c == default), "Snapshot tails must be cleared.");
heartbeat.ProcessRGBPacket(Settings(12, 0), sender);
foreach (var packet in Frame(16, 21)) heartbeat.ProcessRGBPacket(packet, sender);
heartbeat.CopyColorsTo(snapshot);
Check(snapshot[0] == Color.FromRgb(21, 22, 23) && snapshot.AsSpan(16).ToArray().All(c => c == default),
    "A smaller geometry must clear colors left over from the previous frame.");
var blackFrame = Frame(16);
Array.Clear(blackFrame[0], 3, blackFrame[0].Length - 3);
foreach (var packet in blackFrame) heartbeat.ProcessRGBPacket(packet, sender);
long blackVersion = heartbeat.CopyColorsTo(snapshot);
Check(snapshot.Take(16).All(c => c == Colors.Black), "All-black frames must preserve opaque RGB pixels.");
foreach (var packet in blackFrame) heartbeat.ProcessRGBPacket(packet, sender);
Check(heartbeat.FrameVersion == blackVersion, "Identical black frames must also be deduplicated.");

foreach (var (width, height) in new[] { (3, 2), (128, 72), (72, 168) })
{
    var colors = Enumerable.Range(0, width * height)
        .Select(i => Color.FromArgb((byte)(i & 255), (byte)((i * 3) & 255),
            (byte)((i * 3 + 1) & 255), (byte)((i * 3 + 2) & 255))).ToArray();
    var encoded = new byte[colors.Length * 4];
    LedCanvas.EncodeBgraPixels(colors, encoded);
    foreach (int padding in new[] { 0, 16 })
    {
        int stride = width * 4 + padding;
        var guard = Enumerable.Repeat((byte)0xcd, stride * height + 32).ToArray();
        IntPtr address = Marshal.AllocHGlobal(guard.Length);
        try
        {
            Marshal.Copy(guard, 0, address, guard.Length);
            LedCanvas.CopyBgraPixels(encoded, IntPtr.Add(address, 16), stride, width, height);
            Marshal.Copy(address, guard, 0, guard.Length);
            Check(guard.Take(16).All(b => b == 0xcd) && guard.TakeLast(16).All(b => b == 0xcd),
                "Bitmap uploads must not write outside the framebuffer.");
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x;
                    int offset = 16 + y * stride + x * 4;
                    Check(guard[offset] == colors[i].B && guard[offset + 1] == colors[i].G &&
                        guard[offset + 2] == colors[i].R && guard[offset + 3] == colors[i].A,
                        "Packed and padded bitmap uploads must preserve every pixel and alpha byte.");
                }
                Check(guard.AsSpan(16 + y * stride + width * 4, padding).ToArray().All(b => b == 0xcd),
                    "Bitmap uploads must leave row padding untouched.");
            }
        }
        finally { Marshal.FreeHGlobal(address); }
    }
}

void Reject<T>(Action action, string message) where T : Exception
{
    bool rejected = false;
    try { action(); }
    catch (T) { rejected = true; }
    Check(rejected, message);
}
Reject<ArgumentException>(() => LedCanvas.EncodeBgraPixels(row, new byte[7]), "Reject mismatched encoded pixel buffers.");
IntPtr validationBuffer = Marshal.AllocHGlobal(64);
try
{
    Reject<ArgumentOutOfRangeException>(() => LedCanvas.CopyBgraPixels(new byte[16], validationBuffer, 8, 0, 2), "Reject zero width.");
    Reject<ArgumentOutOfRangeException>(() => LedCanvas.CopyBgraPixels(new byte[16], validationBuffer, 8, 2, 0), "Reject zero height.");
    Reject<ArgumentOutOfRangeException>(() => LedCanvas.CopyBgraPixels(new byte[16], validationBuffer, 7, 2, 2), "Reject truncated row strides.");
    Reject<ArgumentException>(() => LedCanvas.CopyBgraPixels(new byte[15], validationBuffer, 8, 2, 2), "Reject truncated upload buffers.");
    Reject<ArgumentException>(() => LedCanvas.CopyBgraPixels(new byte[16], IntPtr.Zero, 8, 2, 2), "Reject null framebuffer addresses.");
}
finally { Marshal.FreeHGlobal(validationBuffer); }

foreach (var (aspect, leds) in new[] { (4, 9216), (9, 12096) })
{
    var concurrent = new RGBClient(new UserSave());
    concurrent.ProcessRGBPacket(Settings(aspect), sender);
    var frames = Enumerable.Range(1, 64).Select(seed => Frame(leds, seed)).ToArray();
    using var gate = new ManualResetEventSlim();
    var producer = Task.Factory.StartNew(() =>
    {
        gate.Wait();
        foreach (var frame in frames)
            foreach (var packet in frame) concurrent.ProcessRGBPacket(packet, sender);
    }, TaskCreationOptions.LongRunning);
    var readers = Enumerable.Range(0, 2).Select(_ => Task.Factory.StartNew(() =>
    {
        gate.Wait();
        var copied = new Color[leds + 7];
        long previousVersion = 0;
        int reads = 0;
        do
        {
            long version = concurrent.CopyColorsTo(copied);
            bool intact = true;
            for (int i = 0; i < leds; i++)
            {
                var expected = version == 0 ? default :
                    Color.FromRgb((byte)((i * 3 + version) & 255), (byte)((i * 3 + 1 + version) & 255),
                        (byte)((i * 3 + 2 + version) & 255));
                if (copied[i] != expected) { intact = false; break; }
            }
            Check(intact && version >= previousVersion, "Concurrent snapshots must have one coherent frame and matching version.");
            Check(copied.Skip(leds).All(c => c == default), "Concurrent snapshots must clear unused tails.");
            previousVersion = version;
            reads++;
            Thread.Yield();
        } while (!producer.IsCompleted || reads < 64);
    }, TaskCreationOptions.LongRunning)).ToArray();
    gate.Set();
    Task.WaitAll(readers.Append(producer).ToArray());
    Check(concurrent.FrameVersion == 64, "All changed frames must publish exactly once during concurrent rendering.");

    var first = Frame(leds, 101);
    var second = Frame(leds, 102);
    var copiedColors = new Color[leds];
    for (int i = 0; i < 32; i++)
    {
        foreach (var packet in i % 2 == 0 ? first : second) concurrent.ProcessRGBPacket(packet, sender);
        concurrent.CopyColorsTo(copiedColors);
    }
    long before = GC.GetAllocatedBytesForCurrentThread();
    for (int i = 0; i < 128; i++)
    {
        foreach (var packet in i % 2 == 0 ? first : second) concurrent.ProcessRGBPacket(packet, sender);
        concurrent.CopyColorsTo(copiedColors);
    }
    long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
    Check(allocated == 0, $"Warmed frame assembly, decode and snapshot must reuse storage; allocated {allocated} bytes.");
}

using (var occupied = new UdpClient())
{
    occupied.Client.ExclusiveAddressUse = true;
    occupied.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
    int occupiedPort = ((IPEndPoint)occupied.Client.LocalEndPoint!).Port;
    var blocked = new RGBClient(new UserSave(), occupiedPort);
    string? failure = null;
    blocked.ListenerFailed += (_, message) => failure = message;
    var blockedThread = new Thread(blocked.StartListening) { IsBackground = true };
    blockedThread.Start();
    try
    {
        Check(blockedThread.Join(3000), "Port-conflict handling must finish promptly.");
        Check(!blocked.IsListening && !string.IsNullOrEmpty(failure), "A port conflict must report failure rather than pretend to listen.");
    }
    finally
    {
        blocked.StopListening();
        blockedThread.Join(3000);
    }
}

// Exercise the real UDP listener only on loopback; never send test traffic to B.
int port;
using (var reservation = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
    port = ((IPEndPoint)reservation.Client.LocalEndPoint!).Port;
var renderClock = new ManualTimeProvider();
client = new RGBClient(new UserSave(), port, renderClock);
var listener = new Thread(client.StartListening) { IsBackground = true };
listener.Start();
try
{
    Check(SpinWait.SpinUntil(() => client.IsListening, 3000), "Listener must start without a local RGB application.");
    using var udp = new UdpClient();
    var endpoint = new IPEndPoint(IPAddress.Loopback, port);
    udp.Send(Settings(12, 0), endpoint);
    var frame = Frame(16, 7);
    udp.Send(frame[0], endpoint);
    Check(SpinWait.SpinUntil(() => client.FrameVersion == 1, 3000), "Real UDP packets must produce a complete frame.");
    var colors = new Color[16];
    client.CopyColorsTo(colors);
    Check(colors[0] == Color.FromRgb(7, 8, 9), "Real UDP listener must preserve colors.");

    var window = new MainWindow();
    var canvas = window.FindControl<LedCanvas>("ledCanvas") ??
        throw new Exception("The actual wallpaper window is missing its LED canvas.");
    Check(RenderOptions.GetBitmapInterpolationMode(canvas) == BitmapInterpolationMode.None,
        "Window XAML must not override nearest-neighbor bitmap scaling with a smoothing filter.");
    string originalBackground = Utils.BackgroundColor;
    int originalBlur = Utils.BlurIntensity;
    try
    {
        Utils.BackgroundColor = "#FF123456";
        Utils.BlurIntensity = 7;
        window.UpdateVisuals();
        var background = window.Background;
        var blur = canvas.Effect;
        for (int i = 0; i < 10; i++) window.UpdateVisuals();
        Check(ReferenceEquals(background, window.Background) && ReferenceEquals(blur, canvas.Effect),
            "Periodic visual refresh must reuse unchanged background brushes and blur effects.");
        Utils.BackgroundColor = "#FF234567";
        Utils.BlurIntensity = 9;
        window.UpdateVisuals();
        Check(window.Background is SolidColorBrush brush && brush.Color == Color.Parse("#FF234567") &&
            canvas.Effect is BlurEffect effect && effect.Radius == 9,
            "Real background and blur changes must still apply.");
        Utils.BlurIntensity = 0;
        window.UpdateVisuals();
        Check(canvas.Effect == null, "Disabling blur must still remove the effect.");
    }
    finally
    {
        Utils.BackgroundColor = originalBackground;
        Utils.BlurIntensity = originalBlur;
        window.UpdateVisuals();
    }
    canvas.Client = client;
    canvas.Effect = null;
    canvas.Measure(new Size(80, 20));
    canvas.Arrange(new Rect(0, 0, 80, 20));
    using var rendered = new RenderTargetBitmap(new PixelSize(80, 20), new Vector(96, 96));
    rendered.Render(canvas);
    IntPtr buffer = Marshal.AllocHGlobal(80 * 20 * 4);
    try
    {
        rendered.CopyPixels(new PixelRect(0, 0, 80, 20), buffer, 80 * 20 * 4, 80 * 4);
        var bytes = new byte[80 * 20 * 4];
        Marshal.Copy(buffer, bytes, 0, bytes.Length);
        for (int y = 0; y < 20; y++)
            for (int x = 0; x < 80; x++)
            {
                int led = y / 10 * 8 + x / 10;
                int offset = (y * 80 + x) * 4;
                Check(bytes[offset] == (byte)((led * 3 + 9) & 255) &&
                    bytes[offset + 1] == (byte)((led * 3 + 8) & 255) &&
                    bytes[offset + 2] == (byte)((led * 3 + 7) & 255) &&
                    bytes[offset + 3] == 255, "Bitmap drawing must preserve exact LED cells and RGB without interpolation.");
            }
    }
    finally { Marshal.FreeHGlobal(buffer); }

    Check(canvas.RefreshFrame(), "The first online frame must request drawing.");
    foreach (var packet in frame) client.ProcessRGBPacket(packet, sender);
    Check(!canvas.RefreshFrame(), "Identical complete RGB frames must skip online invalidation.");
    long onlineVersion = client.FrameVersion;
    renderClock.Advance(TimeSpan.FromSeconds(3));
    Check(canvas.RefreshFrame(), "The timeout must request the offline effect even without a changed frame.");
    using (var offlineImage = new RenderTargetBitmap(new PixelSize(80, 20), new Vector(96, 96)))
    {
        offlineImage.Render(canvas);
        IntPtr address = Marshal.AllocHGlobal(80 * 20 * 4);
        try
        {
            offlineImage.CopyPixels(new PixelRect(0, 0, 80, 20), address, 80 * 20 * 4, 80 * 4);
            var bytes = new byte[80 * 20 * 4];
            Marshal.Copy(address, bytes, 0, bytes.Length);
            Check(bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 0 && bytes[3] == 255,
                "Offline rendering must replace the online bitmap.");
        }
        finally { Marshal.FreeHGlobal(address); }
    }
    Check(canvas.RefreshFrame(), "Offline effects must keep refreshing, not freeze on one frame.");
    foreach (var packet in frame) client.ProcessRGBPacket(packet, sender);
    Check(client.FrameVersion == onlineVersion && canvas.RefreshFrame(),
        "An unchanged returning frame must request recovery from offline rendering.");
    rendered.Render(canvas);
    IntPtr recoveredBuffer = Marshal.AllocHGlobal(80 * 20 * 4);
    try
    {
        rendered.CopyPixels(new PixelRect(0, 0, 80, 20), recoveredBuffer, 80 * 20 * 4, 80 * 4);
        var bytes = new byte[80 * 20 * 4];
        Marshal.Copy(recoveredBuffer, bytes, 0, bytes.Length);
        for (int i = 0; i < 80 * 20; i++)
        {
            int led = i / 80 / 10 * 8 + i % 80 / 10;
            Check(bytes[i * 4] == (byte)((led * 3 + 9) & 255) &&
                bytes[i * 4 + 1] == (byte)((led * 3 + 8) & 255) &&
                bytes[i * 4 + 2] == (byte)((led * 3 + 7) & 255) && bytes[i * 4 + 3] == 255,
                "An unchanged returning frame must restore every online pixel, not retain the offline bitmap.");
        }
    }
    finally { Marshal.FreeHGlobal(recoveredBuffer); }
    Check(!canvas.RefreshFrame(), "Unchanged online recovery must not disable subsequent duplicate suppression.");

    foreach (var (aspect, width, height) in new[] { (4, 128, 72), (9, 72, 168) })
    {
        client.ProcessRGBPacket(Settings(aspect), sender);
        foreach (var packet in Frame(width * height, 11)) client.ProcessRGBPacket(packet, sender);
        canvas.RefreshSettings();
        canvas.Measure(new Size(width, height));
        canvas.Arrange(new Rect(0, 0, width, height));
        using var grid = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        grid.Render(canvas);
        int length = width * height * 4;
        IntPtr address = Marshal.AllocHGlobal(length);
        try
        {
            grid.CopyPixels(new PixelRect(0, 0, width, height), address, length, width * 4);
            var bytes = new byte[length];
            Marshal.Copy(address, bytes, 0, length);
            for (int i = 0; i < width * height; i++)
                Check(bytes[i * 4] == (byte)((i * 3 + 13) & 255) &&
                    bytes[i * 4 + 1] == (byte)((i * 3 + 12) & 255) &&
                    bytes[i * 4 + 2] == (byte)((i * 3 + 11) & 255) &&
                    bytes[i * 4 + 3] == 255, "Every actual X Large bitmap pixel must retain its RGB value.");
        }
        finally { Marshal.FreeHGlobal(address); }
    }

    var sphere = Settings(12, 0);
    sphere[6] = 2; sphere[8] = 2;
    client.ProcessRGBPacket(sphere, sender);
    foreach (var packet in Frame(16, 7)) client.ProcessRGBPacket(packet, sender);
    canvas.RefreshSettings();
    canvas.Measure(new Size(80, 20));
    canvas.Arrange(new Rect(0, 0, 80, 20));
    using var vector = new RenderTargetBitmap(new PixelSize(80, 20), new Vector(96, 96));
    vector.Render(canvas);
    IntPtr vectorBuffer = Marshal.AllocHGlobal(80 * 20 * 4);
    try
    {
        vector.CopyPixels(new PixelRect(0, 0, 80, 20), vectorBuffer, 80 * 20 * 4, 80 * 4);
        var bytes = new byte[80 * 20 * 4];
        Marshal.Copy(vectorBuffer, bytes, 0, bytes.Length);
        Check(bytes[3] == 0 && bytes[(5 * 80 + 5) * 4 + 3] == 255,
            "Sphere/padding settings must retain vector geometry rather than use the rectangular bitmap.");
    }
    finally { Marshal.FreeHGlobal(vectorBuffer); }
}
finally
{
    client.StopListening();
    Check(listener.Join(3000), "Listener must stop cleanly.");
}
Console.WriteLine($"Receiver checks passed: {passed} assertions (settings, X Large assembly, loss/recovery, pixel/stride, concurrent snapshots, duplicate/offline recovery, real loopback UDP).");
Environment.CurrentDirectory = originalDirectory;
Directory.Delete(testState, recursive: true);

sealed class ManualTimeProvider : TimeProvider
{
    private long _ticks = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).Ticks;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);
    public override long GetTimestamp() => Interlocked.Read(ref _ticks);
    public void Advance(TimeSpan duration) => Interlocked.Add(ref _ticks, duration.Ticks);
}
