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
    passed++;
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
LedCanvas.EncodeBgraRow(row, pixels);
Check(pixels.SequenceEqual(new byte[] { 3, 2, 1, 128, 252, 253, 254, 255 }), "Bitmap fast path must preserve BGRA channels and alpha.");

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
client = new RGBClient(new UserSave(), port);
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

    var canvas = new LedCanvas { Client = client };
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
Console.WriteLine($"Receiver checks passed: {passed} assertions (settings, X Large assembly, loss/recovery, bitmap encoding, real loopback UDP).");
Environment.CurrentDirectory = originalDirectory;
Directory.Delete(testState, recursive: true);
