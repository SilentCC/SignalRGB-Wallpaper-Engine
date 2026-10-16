# SignalRGB Wallpaper Engine 2
![Github All Releases](https://img.shields.io/github/downloads/qiangqiang101/SignalRGB-Wallpaper-Engine/total.svg)
![GitHub release (latest by date)](https://img.shields.io/github/v/release/qiangqiang101/SignalRGB-Wallpaper-Engine)
![GitHub](https://img.shields.io/github/license/qiangqiang101/SignalRGB-Wallpaper-Engine)
![GitHub branch checks state](https://img.shields.io/github/checks-status/qiangqiang101/SignalRGB-Wallpaper-Engine/master)
![GitHub issues](https://img.shields.io/github/issues/qiangqiang101/SignalRGB-Wallpaper-Engine)
![GitHub forks](https://img.shields.io/github/forks/qiangqiang101/SignalRGB-Wallpaper-Engine?style=social)
![GitHub Repo stars](https://img.shields.io/github/stars/qiangqiang101/SignalRGB-Wallpaper-Engine?style=social)
![YouTube Channel Subscribers](https://img.shields.io/youtube/channel/subscribers/UCAZlasvEy1euunP1M7nwj5Q?style=social)
[![Donate via PayPal](https://img.shields.io/badge/Donate-Paypal-brightgreen)](https://paypal.me/imnotmental)
[![Follow on Patreon](https://img.shields.io/badge/Donate-Patreon-orange)](https://www.patreon.com/imnotmental)

[中文 README](README.CN.md)

The first SignalRGB Wallpaper Engine Wallpaper coded with ~~Visual Basic~~ Csharp & Javascript.
<img width="1920" height="1029" alt="image" src="https://github.com/user-attachments/assets/745d50f9-de6d-439f-8237-cb1a6435daad" />

https://github.com/user-attachments/assets/5fba7161-b260-4840-be5c-b965bf197007

## 🛠️ Install
### Plugin
[Install the SilentCC fork in SignalRGB on computer A](https://srgbmods.net/s?p=addon/install?url=https://github.com/SilentCC/SignalRGB-Wallpaper-Engine).
Alternatively run `scripts\Install-A.ps1` from a checkout (`Install-A.ps1` in the release ZIP). Install only one
copy of the Wallpaper Engine 2 service; retain existing endpoints and layouts.

### Wallpaper Client
The sender **and receiver** are maintained in this repository. On computer B,
download the self-contained Windows x64 receiver ZIP from
[this fork's releases](https://github.com/SilentCC/SignalRGB-Wallpaper-Engine/releases),
extract it, and run the included script (change the Wallpaper Engine path):

```powershell
.\Install-B.ps1 -WallpaperEnginePath 'D:\SteamLibrary\steamapps\common\wallpaper_engine'
```

This installs two separate application wallpapers, **SignalRGB LAN - Display 1**
(UDP 8133) and **Display 2** (8134). Select them on the corresponding screens in
Wallpaper Engine. Use `-Displays 1` through `4` for a different screen count.
B needs Wallpaper Engine, but **not SignalRGB or a separately installed .NET runtime**.
The installer creates `projects\myprojects\srgb-lan-displayN`, leaves upstream
wallpaper folders alone, and backs up this fork's previous folder on update.
Switch a running fork wallpaper to another wallpaper before updating it.
Existing `usersave2.json` display settings are preserved.

If a release has not been published yet, build the same ZIP from a checkout
with the .NET 10 SDK:

```powershell
.\scripts\Build-Package.ps1
```

The ZIP and SHA-256 file are written under `artifacts`. A tagged `v*` GitHub push
builds and publishes the same package after the automated checks succeed.
These are fork builds, not official releases from the original RGB-Wallpaper author.

## 🔧 Troubleshooting
### Send SignalRGB effects to another LAN computer

Computer A runs SignalRGB with this fork's plugin; computer B runs Wallpaper
Engine with this repository's receiver described above.
B does not need SignalRGB. This sends the plugin's LED canvas and display settings,
not a desktop video stream or commands for arbitrary Workshop wallpapers.

1. On B, install the fork receiver using `Install-B.ps1` above and apply its
   application wallpapers. Note each listening UDP port (default `8133`, `8134`).
2. Use `ipconfig` on B to find its LAN IPv4 address. Prefer a DHCP reservation so
   the address does not change.
3. On A, install this fork using the link above. In its service page, add a device using B's IPv4 address and port.
   Configure the new device's layout and display settings in SignalRGB as usual.
   Keep `127.0.0.1` for local wallpapers; add one endpoint per target PC/port.
4. On B, allow inbound UDP for the client's port on the **Private** firewall profile,
   limited to A's IP. For example, in an administrator PowerShell, replace the
   example address with A's actual address:

   ```powershell
   New-NetFirewallRule -DisplayName "SignalRGB wallpaper from PC A" -Direction Inbound -Action Allow -Protocol UDP -LocalPort 8133 -RemoteAddress 192.168.1.10 -Profile Private
   ```

   Alternatively add `-ConfigureFirewall -SenderAddress 192.168.1.10` to the B
   installer in an administrator PowerShell. It creates Private-profile rules
   for the installed ports limited to that one sender address; it does not change
   existing broader firewall rules. File installation alone does not require elevation.

Use only a trusted LAN: the existing UDP protocol is unauthenticated and unencrypted.
Do not forward this port to the internet. Guest Wi-Fi/client isolation can block
traffic between the PCs. Local image paths in Cover Image refer to **B's** filesystem;
use an image available on B or a URL reachable by B.

The plugin sends settings before the first frame and every five seconds so a
restarted receiver can recover its settings. The fork receiver ignores byte-identical
settings, avoiding repeated configuration writes and timer resets. The old upstream
receiver does not deduplicate these packets: **update B as well as A** to obtain this fix.
UDP provides no connection
acknowledgement; a configured device is not proof that B is receiving it.

For a wireless LAN, higher Display Size tiers increase both packet count and
receiver rendering cost. **X Large remains fully supported**; the performance
changes do not reduce its sampling resolution, color depth or requested frame rate.
Compare with a wired connection when isolating wireless stutter. Target FPS below 60 also lowers the RGB sending rate;
values above 60 keep the existing 60 FPS sending cap while setting the receiver's
rendering FPS. A high link speed does not eliminate latency spikes or UDP loss.

### Performance changes in 2.3.2

Update **B's receiver** to obtain these optimizations. A's 2.3.0 and later senders
remain compatible; A does not need reinstalling or restarting for this release.
The 2.3.1 sharpness fix is included. X Large dimensions, every RGB sample, settings,
the legacy UDP format and the existing sending FPS cap are unchanged.

- Decode into a reusable spare color buffer outside the shared lock, then swap
  buffers under a short lock. Rendering copies a coherent snapshot only when its
  visual version changes; consumers never retain a buffer that the decoder recycles.
- Compare complete RGB frames byte for byte. Identical content skips decoding,
  publication and online invalidation, but still refreshes the valid-frame
  heartbeat. Missing/invalid packets and settings alone do not refresh it.
  Offline effects keep animating, and an identical returning frame restores the
  online pixels even when its visual version has not changed.
- Encode the complete BGRA image into reusable storage and use one native copy
  for a packed framebuffer, instead of 72 / 168 row copies for the 128 x 72
  landscape / 72 x 168 portrait X Large grids. Padded framebuffers retain a stride-aware path;
  alpha, nearest-neighbor scaling and vector-shape fallback are preserved.
- Reuse unchanged background brushes and blur effects during periodic settings
  refresh instead of rebuilding them every five seconds.

Sender sampling is intentionally unchanged: the maintained plugins' component
channel `getColors("Inline")` API is not a verified equivalent of this device's
existing 2D `device.color(x, y)` layout sampling. No guessed bulk API is used.
Receiver-side duplicate suppression does **not** reduce UDP traffic or repair
wireless packet loss. The legacy no-frame-ID limitation described below remains.
The FPS overlay counts redraws, so a static image may show zero while valid RGB
heartbeats continue arriving; the configured sending FPS has not been reduced.

Run `.\tests\benchmark-receiver.ps1` for the CPU microbenchmark. It builds the
actual **v2.3.1** source tag locally and compares frame processing/snapshots and
packed bitmap encoding/upload with this version, using prebuilt datagrams,
alternating variant order and the median of five 300-frame rounds. Both benchmark
variants compile up front with tiered JIT disabled in the child process only,
avoiding short-run tiering bias; the previous environment value is restored.
This is a **steady-state, single-threaded CPU comparison**, not socket IO,
GPU rendering, SignalRGB throughput, end-to-end latency or B's displayed FPS.
No live A/B capture or sampling is performed.
The baseline archive, build and `result.json` are retained under
`artifacts\receiver-benchmark-2.3.2` for inspection.

### Sharpness fix in 2.3.1

The actual wallpaper window now preserves nearest-neighbor bitmap scaling.
In 2.3.0 its XAML overrode the canvas default with a smoothing filter, softening
LED boundaries even when Blur Intensity was zero. Rendering checks now load the
real window XAML, not just an isolated canvas.

Update **B's receiver** for this fix; A's 2.3.0 sender is compatible and does not
need reinstalling. Grid size, layout, FPS, explicit blur settings and cover images
are unchanged.

X Large is a lighting grid, not a native-resolution desktop/video stream:
16:9 landscape is **128 x 72** samples and 21:9 portrait is **72 x 168**.
For example, a 1920 x 1080 screen displays each landscape sample as a 15 x 15
pixel cell. Disabling blur and smoothing sharpens cell edges; it cannot recover
text or fine details that were never sampled.

### Performance changes in 2.3.0

- Sender constructs each legacy UDP packet directly, avoiding whole-frame
  `splice`/`concat` copying. Canvas sampling still reads every LED. Datagrams remain
  at most 1,443 bytes with the original three-byte header; no compression or new protocol.
- Receiver reuses a fixed-size assembly buffer, verifies expected packet lengths
  and sequential indices, and publishes only intact, in-order frames. Missing or
  reordered packets discard the partial frame instead of shifting colors.
  Drop counts are exposed in code and logged at most once per five seconds.
  The legacy wire format has **no frame ID**: arbitrary delayed packets from a
  different frame cannot always be distinguished. This is not reliable delivery
  or full reordering support. Use only one sender per receiving port.
- Unchanged settings do not trigger persistence or UI updates. Genuine settings
  changes still apply. A receiver no longer requires a local RGB software process.
- Rectangle LEDs with zero padding use an exact nearest-neighbor grid bitmap
  (one draw operation rather than thousands), preserving cells, alpha, and blur.
  Rounded rectangles, spheres and padded grids retain the original vector drawing.
  Online refresh skips unchanged frame versions; offline effects continue animating.
- Fixed the Small 16:10 portrait sender grid and Large 21:9 portrait receiver height
  to match the advertised matrix dimensions.

Checks (Windows, Node 24, .NET 10):

```powershell
node --test tests\wallpaper2.test.cjs
dotnet run --project receiver\Receiver.Tests -c Release
.\tests\install.test.ps1
node tests\benchmark-sender.cjs
```

The benchmark compares against local commit `60610c8` using a mock canvas and no
network IO. It measures JavaScript packet-construction cost, **not real SignalRGB
throughput or B's displayed FPS**. Receiver checks cover actual offscreen rendered
pixels, duplicate settings, full X Large RGB ordering, dropped-frame recovery and
real UDP on loopback only. Do not use intrusive packet capture as an unqualified
normal-operation baseline: instrumentation can itself affect smoothness.

Receiver provenance and MIT attribution: [receiver/UPSTREAM.md](receiver/UPSTREAM.md).

- <a href="https://github.com/qiangqiang101/SignalRGB-Wallpaper-Engine/wiki/Wallpapers">Troubleshooting Wiki (GitHub)</a>
- <a href="https://gitlab.com/nollie/SignalRGB-Wallpaper-Engine/-/wikis/Troubleshooting">Troubleshooting Wiki (GitLab)</a>
- <a href="https://github.com/qiangqiang101/SignalRGB-Wallpaper-Engine/discussions/categories/tech-support">Ask the community</a>

## 🖼️ Wallpaper Covers
- <a href="https://github.com/qiangqiang101/SignalRGB-Wallpaper-Engine/wiki/Wallpapers">Official Wallpaper Covers (GitHub)</a>
- <a href="https://gitlab.com/nollie/SignalRGB-Wallpaper-Engine/-/wikis/Wallpapers">Official Wallpaper Covers (GitLab)</a>
- <a href="https://github.com/qiangqiang101/SignalRGB-Wallpaper-Engine/discussions/categories/community-wallpapers">Community Wallpaper Covers</a>

## 🚧 Prerequisite
- [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) only when building from source
- [Wallpaper Engine](https://store.steampowered.com/app/431960/Wallpaper_Engine/) or [Lively Wallpaper](https://github.com/rocksdanister/lively/releases/latest)
- [SignalRGB](https://signalrgb.com/) on A; B does not need it
- This repository's receiver package on B (the provided installer targets Wallpaper Engine on Windows x64)

## 🚀 Similar Projects
- [OpenRGB Wallpaper Engine Plugin](https://gitlab.com/OpenRGBDevelopers/OpenRGBWallpaperEnginePlugin)
