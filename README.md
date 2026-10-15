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
[![Click here to add this repo to SignalRGB](https://github.com/qiangqiang101/SignalRGB-Wallpaper-Engine/blob/main/addtosrgbgithub.png?raw=true)](https://srgbmods.net/s?p=addon/install?url=https://github.com/qiangqiang101/SignalRGB-Wallpaper-Engine) | 
[![Click here to add this repo to SignalRGB](https://github.com/qiangqiang101/SignalRGB-Wallpaper-Engine/blob/main/addtosrgbgitlab.png?raw=true)](https://srgbmods.net/s?p=addon/install?url=https://gitlab.com/nollie/SignalRGB-Wallpaper-Engine)

### Wallpaper Client
[GitHub](https://github.com/qiangqiang101/RGB-Wallpaper/releases) | [GitLab](https://gitlab.com/nollie/RGB-Wallpaper/-/releases)

## 🔧 Troubleshooting
### Send SignalRGB effects to another LAN computer

Computer A runs SignalRGB with this fork's plugin; computer B runs Wallpaper
Engine with the [RGB-Wallpaper client](https://github.com/qiangqiang101/RGB-Wallpaper/releases).
B does not need SignalRGB. This sends the plugin's LED canvas and display settings,
not a desktop video stream or commands for arbitrary Workshop wallpapers.

1. On B, install the Wallpaper Engine client following its
   [installation instructions](https://github.com/qiangqiang101/RGB-Wallpaper/blob/main/WPE.CN.md)
   and apply the RGB wallpaper. Note its listening UDP port (default `8133`).
2. Use `ipconfig` on B to find its LAN IPv4 address. Prefer a DHCP reservation so
   the address does not change.
3. On A, install this fork as the SignalRGB addon, rather than the upstream addon
   linked above. In its service page, add a device using B's IPv4 address and port.
   Configure the new device's layout and display settings in SignalRGB as usual.
   Keep `127.0.0.1` for local wallpapers; add one endpoint per target PC/port.
4. On B, allow inbound UDP for the client's port on the **Private** firewall profile,
   limited to A's IP. For example, in an administrator PowerShell, replace the
   example address with A's actual address:

   ```powershell
   New-NetFirewallRule -DisplayName "SignalRGB wallpaper from PC A" -Direction Inbound -Action Allow -Protocol UDP -LocalPort 8133 -RemoteAddress 192.168.1.10 -Profile Private
   ```

Use only a trusted LAN: the existing UDP protocol is unauthenticated and unencrypted.
Do not forward this port to the internet. Guest Wi-Fi/client isolation can block
traffic between the PCs. Local image paths in Cover Image refer to **B's** filesystem;
use an image available on B or a URL reachable by B.

The plugin sends settings before the first frame and every five seconds so a
restarted receiver can recover its settings. UDP provides no connection
acknowledgement; a configured device is not proof that B is receiving it.

For a wireless LAN, higher Display Size tiers increase both packet count and
receiver rendering cost. Start with Normal when diagnosing stutter and compare
with a wired connection. Target FPS below 60 also lowers the RGB sending rate;
values above 60 keep the existing 60 FPS sending cap while setting the receiver's
rendering FPS. A high link speed does not eliminate latency spikes or UDP loss.

- <a href="https://github.com/qiangqiang101/SignalRGB-Wallpaper-Engine/wiki/Wallpapers">Troubleshooting Wiki (GitHub)</a>
- <a href="https://gitlab.com/nollie/SignalRGB-Wallpaper-Engine/-/wikis/Troubleshooting">Troubleshooting Wiki (GitLab)</a>
- <a href="https://github.com/qiangqiang101/SignalRGB-Wallpaper-Engine/discussions/categories/tech-support">Ask the community</a>

## 🖼️ Wallpaper Covers
- <a href="https://github.com/qiangqiang101/SignalRGB-Wallpaper-Engine/wiki/Wallpapers">Official Wallpaper Covers (GitHub)</a>
- <a href="https://gitlab.com/nollie/SignalRGB-Wallpaper-Engine/-/wikis/Wallpapers">Official Wallpaper Covers (GitLab)</a>
- <a href="https://github.com/qiangqiang101/SignalRGB-Wallpaper-Engine/discussions/categories/community-wallpapers">Community Wallpaper Covers</a>

## 🚧 Prerequisite
- [.NET 10.0 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- [Wallpaper Engine](https://store.steampowered.com/app/431960/Wallpaper_Engine/) or [Lively Wallpaper](https://github.com/rocksdanister/lively/releases/latest)
- [SignalRGB](https://signalrgb.com/) or [RGBJunkie](https://rgbjunkie.com/RGBJunkieApp/)
- [Wallpaper Client (GitHub)](https://github.com/qiangqiang101/RGB-Wallpaper/releases) or [Wallpaper Client (GitLab)](https://gitlab.com/nollie/RGB-Wallpaper/-/releases)

## 🚀 Similar Projects
- [OpenRGB Wallpaper Engine Plugin](https://gitlab.com/OpenRGBDevelopers/OpenRGBWallpaperEnginePlugin)
