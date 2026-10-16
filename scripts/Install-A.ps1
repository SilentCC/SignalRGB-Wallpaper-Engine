[CmdletBinding(SupportsShouldProcess)]
param()
$ErrorActionPreference = 'Stop'
$url = 'https://srgbmods.net/s?p=addon/install?url=https://github.com/SilentCC/SignalRGB-Wallpaper-Engine'
if ($PSCmdlet.ShouldProcess('SignalRGB', 'Open the SilentCC repository addon installer')) {
    Start-Process $url
    Write-Output 'Install only this fork of Wallpaper Engine 2. Keep your existing device endpoints and layouts.'
    Write-Output 'The addon is downloaded from GitHub, not from this local script directory.'
}
