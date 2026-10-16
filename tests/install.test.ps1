$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$fixture = Join-Path $repo ('artifacts\install-test-' + [Guid]::NewGuid().ToString('N'))
$wallpaper = Join-Path $fixture 'wallpaper_engine'
$app = Join-Path $fixture 'app'
New-Item -ItemType Directory -Path $wallpaper, $app -Force | Out-Null
New-Item -ItemType File -Path (Join-Path $wallpaper 'wallpaper64.exe') | Out-Null
foreach ($name in @('Wallpaper-UI.exe', 'Wallpaper-UI.dll', 'LICENSE', 'preview.gif')) {
    [IO.File]::WriteAllText((Join-Path $app $name), 'fixture-v1')
}
$installer = Join-Path $repo 'scripts\Install-B.ps1'
function Check($condition, $message) {
    if (!$condition) { throw $message }
}
& (Join-Path $repo 'scripts\Install-A.ps1') -WhatIf
& $installer -WallpaperEnginePath $wallpaper -ReceiverPath $app -WhatIf
Check (!(Test-Path -LiteralPath (Join-Path $wallpaper 'projects'))) 'WhatIf must not write installation files.'
& $installer -WallpaperEnginePath $wallpaper -ReceiverPath $app
for ($display = 1; $display -le 2; $display++) {
    $installed = Join-Path $wallpaper "projects\myprojects\srgb-lan-display$display"
    $config = Get-Content -LiteralPath (Join-Path $installed 'config.json') -Raw | ConvertFrom-Json
    Check ($config.Port -eq 8132 + $display) 'Wrong per-screen UDP port.'
    $project = Get-Content -LiteralPath (Join-Path $installed 'project.json') -Raw | ConvertFrom-Json
    Check ($project.file -eq 'Wallpaper-UI.exe' -and $project.type -eq 'application') 'Wrong Wallpaper Engine project format.'
    Check (Test-Path -LiteralPath (Join-Path $installed 'LICENSE')) 'Receiver license must be distributed.'
    [IO.File]::WriteAllText((Join-Path $installed 'usersave2.json'), '{"FPS":45}')
}
[IO.File]::WriteAllText((Join-Path $app 'Wallpaper-UI.dll'), 'fixture-v2')
& $installer -WallpaperEnginePath $wallpaper -ReceiverPath $app
for ($display = 1; $display -le 2; $display++) {
    $installed = Join-Path $wallpaper "projects\myprojects\srgb-lan-display$display"
    Check ((Get-Content -LiteralPath (Join-Path $installed 'Wallpaper-UI.dll') -Raw) -eq 'fixture-v2') 'Update did not install the new files.'
    Check ((Get-Content -LiteralPath (Join-Path $installed 'usersave2.json') -Raw) -eq '{"FPS":45}') 'Update lost user display settings.'
    $backups = @(Get-ChildItem -LiteralPath (Join-Path $wallpaper 'projects\myprojects') -Directory |
        Where-Object Name -Like "srgb-lan-display$display-backup-*")
    Check ($backups.Count -eq 1) 'Update must retain one previous installation backup.'
    Check ((Get-Content -LiteralPath (Join-Path $backups[0].FullName 'Wallpaper-UI.dll') -Raw) -eq 'fixture-v1') 'Backup must contain previous receiver binaries.'
}
$failed = $false
try { & $installer -WallpaperEnginePath $fixture -ReceiverPath $app } catch { $failed = $true }
Check $failed 'An invalid Wallpaper Engine directory must fail explicitly.'
$failed = $false
try { & $installer -WallpaperEnginePath $wallpaper -ReceiverPath $app -ConfigureFirewall -SenderAddress '0.0.0.0' } catch { $failed = $true }
Check $failed 'Firewall configuration must reject wildcard sender addresses.'

# Remove only the resolved, uniquely named fixture created by this test.
Remove-Item -LiteralPath $fixture -Recurse -Force
Write-Output 'Installer checks passed: WhatIf, project/port generation, update backups, settings preservation and invalid paths.'
