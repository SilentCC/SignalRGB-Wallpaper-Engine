using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text.Json;
using Avalonia.Media;
using Wallpaper_UI;

internal static class ReceiverBenchmarks
{
    private delegate void EncodePixels(ReadOnlySpan<Color> colors, Span<byte> pixels);
    private sealed record Pipeline(Action<byte[], IPEndPoint> Process, Func<Color[], long> Copy, Func<long> Version);
    private const int Iterations = 300;
    private const int Rounds = 5;

    internal static void Run(string baselinePath)
    {
        var context = new AssemblyLoadContext("receiver-2.3.1-benchmark", isCollectible: true);
        context.Resolving += (_, name) => AssemblyLoadContext.Default.Assemblies
            .FirstOrDefault(assembly => assembly.GetName().Name == name.Name);
        var baseline = context.LoadFromAssemblyPath(Path.GetFullPath(baselinePath));
        if (baseline.GetName().Version != new Version(2, 3, 1, 0))
            throw new InvalidOperationException("The benchmark requires the unmodified 2.3.1 receiver.");
        var encodeBefore = baseline.GetType("Wallpaper_UI.LedCanvas", throwOnError: true)!
            .GetMethod("EncodeBgraRow", BindingFlags.Static | BindingFlags.NonPublic)!
            .CreateDelegate<EncodePixels>();
        var reports = new List<object>();
        foreach (var (aspect, width, height) in new[] { (4, 128, 72), (9, 72, 168) })
        {
            var sender = new IPEndPoint(IPAddress.Loopback, 12345);
            int leds = width * height;
            byte[] settings = new byte[19];
            settings[0] = 1; settings[1] = (byte)aspect; settings[2] = 3;
            settings[9] = 60; settings[10] = 2;
            var frames = new[] { Frame(leds, 11), Frame(leds, 12) };
            var frameReports = new List<object>();
            foreach (bool identical in new[] { false, true })
            {
                var before = CreateBaseline(baseline);
                var client = new RGBClient(new UserSave());
                var after = new Pipeline(client.ProcessRGBPacket, client.CopyColorsTo, () => client.FrameVersion);
                before.Process(settings, sender);
                after.Process(settings, sender);
                var snapshots = new[] { new Color[leds], new Color[leds] };
                var pipelines = new[] { before, after };
                var actions = new Action[2];
                for (int index = 0; index < 2; index++)
                {
                    int variant = index;
                    int sequence = 0;
                    long copiedVersion = -1;
                    actions[index] = () =>
                    {
                        var frame = frames[identical ? 0 : sequence++ & 1];
                        foreach (var packet in frame) pipelines[variant].Process(packet, sender);
                        if (pipelines[variant].Version() != copiedVersion)
                            copiedVersion = pipelines[variant].Copy(snapshots[variant]);
                    };
                }
                WarmUp(actions);
                long beforeVersion = before.Version(), afterVersion = after.Version();
                var timings = MeasurePair(actions);
                if (!snapshots[0].SequenceEqual(snapshots[1]))
                    throw new InvalidOperationException("Benchmark pipelines produced different RGB pixels.");
                long oldPublications = before.Version() - beforeVersion;
                long newPublications = after.Version() - afterVersion;
                if (oldPublications != Iterations * Rounds ||
                    newPublications != (identical ? 0 : Iterations * Rounds))
                    throw new InvalidOperationException("Benchmark publication counts are incorrect.");
                frameReports.Add(new
                {
                    scenario = identical ? "identical_frames" : "changing_frames",
                    baseline_ms_per_frame = timings[0],
                    current_ms_per_frame = timings[1],
                    speedup = Math.Round(timings[0] / timings[1], 2),
                    baseline_publications = oldPublications,
                    current_publications = newPublications
                });
            }

            var colors = Enumerable.Range(0, leds).Select(i =>
                Color.FromArgb((byte)(i & 255), (byte)((i * 3) & 255),
                    (byte)((i * 3 + 1) & 255), (byte)((i * 3 + 2) & 255))).ToArray();
            var row = new byte[width * 4];
            var pixels = new byte[leds * 4];
            IntPtr address = Marshal.AllocHGlobal(pixels.Length);
            double[] uploadTimings;
            try
            {
                Action[] upload =
                [
                    () =>
                    {
                        for (int y = 0; y < height; y++)
                        {
                            encodeBefore(colors.AsSpan(y * width, width), row);
                            Marshal.Copy(row, 0, IntPtr.Add(address, y * row.Length), row.Length);
                        }
                    },
                    () =>
                    {
                        LedCanvas.EncodeBgraPixels(colors, pixels);
                        LedCanvas.CopyBgraPixels(pixels, address, width * 4, width, height);
                    }
                ];
                upload[0]();
                var expected = new byte[pixels.Length];
                Marshal.Copy(address, expected, 0, expected.Length);
                upload[1]();
                var actual = new byte[pixels.Length];
                Marshal.Copy(address, actual, 0, actual.Length);
                if (!expected.SequenceEqual(actual))
                    throw new InvalidOperationException("Benchmark bitmap uploads produced different pixels.");
                WarmUp(upload);
                uploadTimings = MeasurePair(upload);
            }
            finally { Marshal.FreeHGlobal(address); }
            reports.Add(new
            {
                aspect = aspect == 4 ? "16:9 Landscape" : "21:9 Portrait",
                tier = "X Large",
                frame_pipeline = frameReports,
                packed_bitmap_upload = new
                {
                    baseline_ms_per_frame = uploadTimings[0],
                    current_ms_per_frame = uploadTimings[1],
                    speedup = Math.Round(uploadTimings[0] / uploadTimings[1], 2),
                    baseline_native_copies_per_frame = height,
                    current_native_copies_per_frame = 1
                }
            });
        }
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            caveat = "Offline, single-threaded receiver CPU microbenchmark against an actual assembly compiled from v2.3.1. Prebuilt datagrams, no socket IO, no UI/GPU rendering, no A/B traffic. Not SignalRGB throughput or B display FPS.",
            tiered_compilation = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation") ?? "runtime default",
            measured_frames_per_variant = Iterations * Rounds,
            reports
        }, new JsonSerializerOptions { WriteIndented = true }));
        context.Unload();
    }

    private static Pipeline CreateBaseline(Assembly assembly)
    {
        var type = assembly.GetType("Wallpaper_UI.RGBClient", throwOnError: true)!;
        var save = Activator.CreateInstance(assembly.GetType("Wallpaper_UI.UserSave", throwOnError: true)!)!;
        var client = Activator.CreateInstance(type, save, 8133)!;
        return new Pipeline(
            type.GetMethod("ProcessRGBPacket", BindingFlags.Instance | BindingFlags.NonPublic)!
                .CreateDelegate<Action<byte[], IPEndPoint>>(client),
            type.GetMethod("CopyColorsTo")!.CreateDelegate<Func<Color[], long>>(client),
            type.GetProperty("FrameVersion")!.GetMethod!.CreateDelegate<Func<long>>(client));
    }

    private static List<byte[]> Frame(int leds, int seed)
    {
        int count = (leds + 479) / 480;
        var packets = new List<byte[]>();
        for (int index = 0; index < count; index++)
        {
            var packet = new byte[3 + Math.Min(480, leds - index * 480) * 3];
            packet[1] = (byte)index; packet[2] = (byte)count;
            for (int i = 3; i < packet.Length; i++)
                packet[i] = (byte)((index * 1440 + i - 3 + seed) & 255);
            packets.Add(packet);
        }
        return packets;
    }

    private static void WarmUp(Action[] actions)
    {
        for (int i = 0; i < 1000; i++)
            foreach (var action in actions) action();
    }

    private static double[] MeasurePair(Action[] actions)
    {
        var samples = new[] { new List<double>(), new List<double>() };
        for (int round = 0; round < Rounds; round++)
            foreach (int index in round % 2 == 0 ? new[] { 0, 1 } : new[] { 1, 0 })
            {
                long start = Stopwatch.GetTimestamp();
                for (int i = 0; i < Iterations; i++) actions[index]();
                samples[index].Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds / Iterations);
            }
        return samples.Select(values => values.Order().ElementAt(Rounds / 2)).ToArray();
    }
}
