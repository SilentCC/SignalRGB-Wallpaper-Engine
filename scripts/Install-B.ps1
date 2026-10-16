[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][string]$WallpaperEnginePath,
    [ValidateRange(1, 4)][int]$Displays = 2,
    [ValidateRange(1024, 65532)][int]$BasePort = 8133,
    [string]$ReceiverPath = (Join-Path $PSScriptRoot 'app'),
    [switch]$ConfigureFirewall,
    [string]$SenderAddress
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $WallpaperEnginePath).Path
$app = (Resolve-Path -LiteralPath $ReceiverPath).Path
if (!(Test-Path -LiteralPath (Join-Path $root 'wallpaper64.exe')) -and
    !(Test-Path -LiteralPath (Join-Path $root 'wallpaper32.exe'))) {
    throw 'WallpaperEnginePath must be the wallpaper_engine directory containing wallpaper64.exe or wallpaper32.exe.'
}
foreach ($file in @('Wallpaper-UI.exe', 'Wallpaper-UI.dll', 'LICENSE', 'preview.gif')) {
    if (!(Test-Path -LiteralPath (Join-Path $app $file) -PathType Leaf)) {
        throw "Receiver package is missing $file. Extract a complete release ZIP or run Build-Package.ps1."
    }
}
if ($ConfigureFirewall) {
    $address = $null
    if (![Net.IPAddress]::TryParse($SenderAddress, [ref]$address) -or
        $address.AddressFamily -ne [Net.Sockets.AddressFamily]::InterNetwork -or
        $SenderAddress -eq '0.0.0.0' -or $address.GetAddressBytes()[0] -ge 224) {
        throw 'ConfigureFirewall requires the sender computer IPv4 address, not a hostname or subnet.'
    }
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    if (!([Security.Principal.WindowsPrincipal]::new($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'ConfigureFirewall requires an administrator PowerShell. Omit it to install files only.'
    }
}

$projects = Join-Path $root 'projects\myprojects'
$utf8 = [Text.UTF8Encoding]::new($false)
for ($display = 1; $display -le $Displays; $display++) {
    $destination = Join-Path $projects "srgb-lan-display$display"
    $exe = Join-Path $destination 'Wallpaper-UI.exe'
    $running = @(Get-Process -Name 'Wallpaper-UI' -ErrorAction SilentlyContinue | Where-Object {
        if (!$_.Path) { throw 'Cannot inspect a running receiver path; close RGB wallpapers before updating.' }
        $_.Path -eq $exe
    })
    if ($running.Count) { throw "Display $display is running. Switch it to another wallpaper before updating; no process was killed." }
}
for ($display = 1; $display -le $Displays; $display++) {
    $port = $BasePort + $display - 1
    $destination = Join-Path $projects "srgb-lan-display$display"
    if (!$PSCmdlet.ShouldProcess($destination, "Install fork receiver display $display on UDP $port (preserving the previous folder as a backup)")) {
        continue
    }
    New-Item -ItemType Directory -Path $projects -Force | Out-Null
    $staging = Join-Path $projects ('.srgb-lan-stage-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $staging | Out-Null
    foreach ($file in Get-ChildItem -LiteralPath $app -Force) {
        Copy-Item -LiteralPath $file.FullName -Destination $staging -Recurse
    }
    $save = Join-Path $destination 'usersave2.json'
    if (Test-Path -LiteralPath $save) {
        Copy-Item -LiteralPath $save -Destination (Join-Path $staging 'usersave2.json') -Force
    }
    [IO.File]::WriteAllText((Join-Path $staging 'config.json'), (@{ Port = $port } | ConvertTo-Json), $utf8)
    $project = [ordered]@{
        title = "SignalRGB LAN - Display $display"
        description = "SilentCC fork, UDP $port. Source: https://github.com/SilentCC/SignalRGB-Wallpaper-Engine"
        type = 'application'
        file = 'Wallpaper-UI.exe'
        preview = 'preview.gif'
        version = 0
        contentrating = 'Everyone'
        general = @{ properties = @{
            credits = @{ type = 'text'; text = 'RGB Wallpaper copyright Bartholomew Ho (MIT). LAN performance fork: SilentCC.' }
            port = @{ type = 'text'; text = "UDP port $port" }
        } }
    }
    [IO.File]::WriteAllText((Join-Path $staging 'project.json'), ($project | ConvertTo-Json -Depth 6), $utf8)
    $backup = $null
    if (Test-Path -LiteralPath $destination) {
        $backup = $destination + '-backup-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 6)
        Move-Item -LiteralPath $destination -Destination $backup
    }
    try {
        Move-Item -LiteralPath $staging -Destination $destination
    } catch {
        if ($backup -and !(Test-Path -LiteralPath $destination)) {
            Move-Item -LiteralPath $backup -Destination $destination
        }
        throw
    }
    Write-Output "Installed display $display at $destination (UDP $port)."
    if ($backup) { Write-Output "Previous files retained at $backup." }
}
if ($ConfigureFirewall) {
    for ($display = 1; $display -le $Displays; $display++) {
        $port = $BasePort + $display - 1
        $rule = "SilentCC-RGBWallpaper-UDP-$port"
        if ($PSCmdlet.ShouldProcess($rule, "Allow inbound UDP $port only from $SenderAddress on Private networks")) {
            $existing = Get-NetFirewallRule -Name $rule -ErrorAction SilentlyContinue
            if ($existing) { Remove-NetFirewallRule -Name $rule }
            New-NetFirewallRule -Name $rule -DisplayName $rule -Direction Inbound -Action Allow `
                -Protocol UDP -LocalPort $port -RemoteAddress $SenderAddress -Profile Private | Out-Null
        }
    }
} else {
    Write-Warning 'Firewall rules were not changed. Allow these UDP ports on Private networks only from computer A.'
}
Write-Output 'Select SignalRGB LAN - Display 1 / Display 2 in Wallpaper Engine. B does not need SignalRGB.'
