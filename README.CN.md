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
[A 电脑：将 SilentCC 的本仓库安装到 SignalRGB](https://srgbmods.net/s?p=addon/install?url=https://github.com/SilentCC/SignalRGB-Wallpaper-Engine)。
也可运行源码中的 `scripts\Install-A.ps1`（发行 ZIP 中为根目录 `Install-A.ps1`）打开安装入口。
Wallpaper Engine 2 服务只保留一个来源，
已有的设备端点和布局不需要删除。

### 壁纸客户端
**发送端和接收端均由本仓库维护。** B 电脑从
[本仓库 Releases](https://github.com/SilentCC/SignalRGB-Wallpaper-Engine/releases)
下载 Windows x64 接收端 ZIP，解压后运行（替换为实际 Wallpaper Engine 路径）：

```powershell
.\Install-B.ps1 -WallpaperEnginePath 'D:\SteamLibrary\steamapps\common\wallpaper_engine'
```

默认安装两份应用壁纸：**SignalRGB LAN - Display 1** 使用 UDP `8133`，
**Display 2** 使用 `8134`。在 Wallpaper Engine 中分别应用到对应屏幕。
`-Displays` 支持 1～4。B **不需要 SignalRGB，也不需要另装 .NET 运行时**。
安装目录为 `projects\myprojects\srgb-lan-displayN`，不会覆盖原作者的壁纸目录。
更新本分支时会备份旧目录、保留 `usersave2.json` 显示设置；先将正在运行的本分支壁纸切换为其他壁纸再更新。

如果还未发布 Release，可在本仓库检出目录中用 .NET 10 SDK 生成同样的安装包：

```powershell
.\scripts\Build-Package.ps1
```

ZIP 和 SHA-256 文件位于 `artifacts`。推送 `v*` 标签后，GitHub 工作流在检查通过后自动构建并发布。
这是本分支的构建，并非 RGB-Wallpaper 原作者的官方发行版。

## 🔧 故障排除
### 将灯效发送到局域网另一台电脑

A 电脑运行 SignalRGB 和本分支插件；B 电脑运行 Wallpaper Engine 和
上文的本仓库壁纸接收端。
**B 不需要安装 SignalRGB。** 传输的是插件的灯效画布和显示设置，不是桌面投屏，
也不能用来控制任意创意工坊壁纸。

1. 在 B 上通过上述 `Install-B.ps1` 安装并应用本分支的应用壁纸，确认接收端 UDP 端口
   （默认主屏 `8133`、第二屏 `8134`）。
2. 在 B 上运行 `ipconfig`，查看局域网 IPv4 地址，建议在路由器中为 B 设置 DHCP 地址保留。
3. 在 A 的 SignalRGB 中通过上方链接安装**本分支**插件。
   在插件服务页添加设备，填入 B 的 IPv4 地址和端口，再按原有方式配置该设备的布局和显示参数。
   本机壁纸仍使用 `127.0.0.1`；多台电脑分别添加对应的 IP/端口。
4. 在 B 上允许壁纸端口的入站 UDP，仅限**专用网络**和 A 的 IP。例如在管理员
   PowerShell 中运行以下命令，将示例 IP 替换为 A 的实际地址：

   ```powershell
   New-NetFirewallRule -DisplayName "SignalRGB wallpaper from PC A" -Direction Inbound -Action Allow -Protocol UDP -LocalPort 8133 -RemoteAddress 192.168.1.10 -Profile Private
   ```

   也可在管理员 PowerShell 中给 B 安装脚本添加
   `-ConfigureFirewall -SenderAddress 192.168.1.10`，按已安装端口创建仅限 A 的专用网络规则；
   不会修改其他已有的宽泛规则。只安装文件不需要管理员权限。

仅在可信局域网使用：现有 UDP 协议没有身份认证或加密，**不要将端口映射到公网**。
访客 Wi-Fi 或无线客户端隔离可能阻止两台电脑通信。Cover Image 的本地路径指向
**B 的文件系统**，请使用 B 上存在的图片或 B 能访问的图片 URL。

插件在首帧前发送显示设置，并每五秒重发一次，使接收端重启后能恢复设置。
**本分支接收端会忽略完全相同的重复设置**，不会因此反复写配置或重建刷新定时器。
原版接收端没有这个去重逻辑，必须 **A、B 都更新** 才能完整获得该修复。
UDP 没有连接确认，设备出现在列表中不代表 B 已收到数据。

无线局域网中，Display Size 档位越高，发送的数据包数量和接收端绘制开销也越大。
**本次优化保留完整 X Large，不减少取色点、不降低色深或目标帧率**。
定位无线抖动时可保持同样设置与有线连接对比。Target FPS 低于 60 时，
RGB 发送频率也会同步降低；高于 60 时，发送端仍保持原有的 60 FPS 上限，
该设置值用于接收端的渲染帧率。无线连接速率高并不代表没有延迟波动或 UDP 丢包。

### 2.3.0 性能改进

- A 直接填充每个 UDP 包，移除整帧大数组的 `splice`、`concat` 搬移；仍逐点读取所有颜色。
  保持原三字节包头、最大 1,443 字节数据报，不压缩、不改协议。
- B 使用可复用的固定大小组帧缓冲，校验包长和顺序，仅提交完整、按序到达的帧。
  缺包或乱序时丢弃残帧，避免颜色错位；提供丢帧计数，最多每五秒记录一次丢帧日志。
  **旧协议没有帧编号**，不能保证识别所有跨帧迟到包，也不等于可靠传输或完整乱序重组；
  每个接收端口只配置一个发送端。
- 重复设置不写盘、不更新 UI；真正变化的设置仍正常应用。B 不再依赖本机 RGB 软件进程。
- 矩形、零间距灯点通过最近邻网格位图一次绘制，保留灯点边界、透明度与模糊效果；
  圆角、球形或带间距的灯点继续使用原矢量绘制。在线没有新帧时跳过重复刷新，离线灯效继续动画。
- 修正发送端 Small 16:10 竖屏点阵和接收端 Large 21:9 竖屏高度，使之与声明的尺寸一致。

本地检查（Windows、Node 24、.NET 10）：

```powershell
node --test tests\wallpaper2.test.cjs
dotnet run --project receiver\Receiver.Tests -c Release
.\tests\install.test.ps1
node tests\benchmark-sender.cjs
```

基准测试对比本地提交 `60610c8`，使用模拟画布且不发送网络流量，**只反映 JavaScript 打包开销，
不代表 SignalRGB 的实际吞吐量或 B 的显示帧率**。接收端检查包括真实离屏绘制像素、
重复设置、X Large 全部 RGB 顺序、丢帧恢复和本机回环 UDP，不向 B 发送测试流量。
抓包工具本身可能引起卡顿，不应把有干扰的采样当作日常性能基线。

接收端来源与 MIT 授权见 [receiver/UPSTREAM.md](receiver/UPSTREAM.md)。

- <a href="https://github.com/qiangqiang101/SignalRGB-Wallpaper-Engine/wiki/Wallpapers">故障排除 Wiki (GitHub)</a>
- <a href="https://gitlab.com/nollie/SignalRGB-Wallpaper-Engine/-/wikis/Troubleshooting">故障排除 Wiki (GitLab)</a>
- <a href="https://github.com/qiangqiang101/SignalRGB-Wallpaper-Engine/discussions/categories/tech-support">咨询社区</a>

## 🖼️ 壁纸封面
- <a href="https://github.com/qiangqiang101/SignalRGB-Wallpaper-Engine/wiki/Wallpapers">官方壁纸封面 (GitHub)</a>
- <a href="https://gitlab.com/nollie/SignalRGB-Wallpaper-Engine/-/wikis/Wallpapers">官方壁纸封面 (GitLab)</a>
- <a href="https://github.com/qiangqiang101/SignalRGB-Wallpaper-Engine/discussions/categories/community-wallpapers">社区壁纸封面</a>

## 🚧 前置需求
- [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)，仅从源码构建时需要
- [Wallpaper Engine](https://store.steampowered.com/app/431960/Wallpaper_Engine/) 或 [Lively Wallpaper](https://github.com/rocksdanister/lively/releases/latest)
- A 安装 [SignalRGB](https://signalrgb.com/)，B 不需要
- B 安装本仓库接收端（提供的安装脚本面向 Windows x64 的 Wallpaper Engine）

## 🚀 类似项目
- [OpenRGB Wallpaper Engine 插件](https://gitlab.com/OpenRGBDevelopers/OpenRGBWallpaperEnginePlugin)
