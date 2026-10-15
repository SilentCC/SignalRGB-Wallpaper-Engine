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

[ENGLISH README](README.md)

首款使用 ~~Visual Basic~~ C# 和 JavaScript 编写的 SignalRGB Wallpaper Engine 壁纸插件。
<img width="1920" height="1029" alt="image" src="https://github.com/user-attachments/assets/745d50f9-de6d-439f-8237-cb1a6435daad" />

https://github.com/user-attachments/assets/5fba7161-b260-4840-be5c-b965bf197007

## 🛠️ 安装说明
### 插件
[![点击此处将此仓库添加到 SignalRGB](https://github.com/qiangqiang101/SignalRGB-Wallpaper-Engine/blob/main/addtosrgbgithub.png?raw=true)](https://srgbmods.net/s?p=addon/install?url=https://github.com/qiangqiang101/SignalRGB-Wallpaper-Engine) | 
[![点击此处将此仓库添加到 SignalRGB](https://github.com/qiangqiang101/SignalRGB-Wallpaper-Engine/blob/main/addtosrgbgitlab.png?raw=true)](https://srgbmods.net/s?p=addon/install?url=https://gitlab.com/nollie/SignalRGB-Wallpaper-Engine)

### 壁纸客户端
[GitHub](https://github.com/qiangqiang101/RGB-Wallpaper/releases) | [GitLab](https://gitlab.com/nollie/RGB-Wallpaper/-/releases)

## 🔧 故障排除
### 将灯效发送到局域网另一台电脑

A 电脑运行 SignalRGB 和本分支插件；B 电脑运行 Wallpaper Engine 和
[RGB-Wallpaper 壁纸客户端](https://github.com/qiangqiang101/RGB-Wallpaper/releases)。
**B 不需要安装 SignalRGB。** 传输的是插件的灯效画布和显示设置，不是桌面投屏，
也不能用来控制任意创意工坊壁纸。

1. 在 B 上按照客户端的
   [安装说明](https://github.com/qiangqiang101/RGB-Wallpaper/blob/main/WPE.CN.md)
   安装并应用 RGB 壁纸，确认接收端 UDP 端口（默认 `8133`）。
2. 在 B 上运行 `ipconfig`，查看局域网 IPv4 地址，建议在路由器中为 B 设置 DHCP 地址保留。
3. 在 A 的 SignalRGB 中安装**本分支**插件，不要使用上方指向原作者仓库的安装链接。
   在插件服务页添加设备，填入 B 的 IPv4 地址和端口，再按原有方式配置该设备的布局和显示参数。
   本机壁纸仍使用 `127.0.0.1`；多台电脑分别添加对应的 IP/端口。
4. 在 B 上允许壁纸端口的入站 UDP，仅限**专用网络**和 A 的 IP。例如在管理员
   PowerShell 中运行以下命令，将示例 IP 替换为 A 的实际地址：

   ```powershell
   New-NetFirewallRule -DisplayName "SignalRGB wallpaper from PC A" -Direction Inbound -Action Allow -Protocol UDP -LocalPort 8133 -RemoteAddress 192.168.1.10 -Profile Private
   ```

仅在可信局域网使用：现有 UDP 协议没有身份认证或加密，**不要将端口映射到公网**。
访客 Wi-Fi 或无线客户端隔离可能阻止两台电脑通信。Cover Image 的本地路径指向
**B 的文件系统**，请使用 B 上存在的图片或 B 能访问的图片 URL。

插件在首帧前发送显示设置，并每五秒重发一次，使接收端重启后能恢复设置。
UDP 没有连接确认，设备出现在列表中不代表 B 已收到数据。

- <a href="https://github.com/qiangqiang101/SignalRGB-Wallpaper-Engine/wiki/Wallpapers">故障排除 Wiki (GitHub)</a>
- <a href="https://gitlab.com/nollie/SignalRGB-Wallpaper-Engine/-/wikis/Troubleshooting">故障排除 Wiki (GitLab)</a>
- <a href="https://github.com/qiangqiang101/SignalRGB-Wallpaper-Engine/discussions/categories/tech-support">咨询社区</a>

## 🖼️ 壁纸封面
- <a href="https://github.com/qiangqiang101/SignalRGB-Wallpaper-Engine/wiki/Wallpapers">官方壁纸封面 (GitHub)</a>
- <a href="https://gitlab.com/nollie/SignalRGB-Wallpaper-Engine/-/wikis/Wallpapers">官方壁纸封面 (GitLab)</a>
- <a href="https://github.com/qiangqiang101/SignalRGB-Wallpaper-Engine/discussions/categories/community-wallpapers">社区壁纸封面</a>

## 🚧 前置需求
- [.NET 10.0](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- [Wallpaper Engine](https://store.steampowered.com/app/431960/Wallpaper_Engine/) 或 [Lively Wallpaper](https://github.com/rocksdanister/lively/releases/latest)
- [SignalRGB](https://signalrgb.com/) 或 [RGBJunkie](https://rgbjunkie.com/RGBJunkieApp/)
- [壁纸客户端 (GitHub)](https://github.com/qiangqiang101/RGB-Wallpaper/releases) 或 [壁纸客户端 (GitLab)](https://gitlab.com/nollie/RGB-Wallpaper/-/releases)

## 🚀 类似项目
- [OpenRGB Wallpaper Engine 插件](https://gitlab.com/OpenRGBDevelopers/OpenRGBWallpaperEnginePlugin)
