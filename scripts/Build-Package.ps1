[CmdletBinding()]
param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repo 'receiver\Wallpaper-UI\Wallpaper-UI.csproj'
$version = ([xml](Get-Content -LiteralPath $project -Raw)).Project.PropertyGroup.Version |
    Where-Object { $_ } | Select-Object -First 1
if (!$OutputDirectory) {
    $OutputDirectory = Join-Path $repo ('artifacts\receiver-' + $version + '-win-x64-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
}
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw "Output directory already exists; choose a new empty path: $output" }
New-Item -ItemType Directory -Path $output | Out-Null
$app = Join-Path $output 'app'
dotnet publish $project -c Release -r win-x64 --self-contained true -o $app --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw 'Receiver publish failed.' }
Copy-Item -LiteralPath (Join-Path $repo 'receiver\LICENSE') -Destination (Join-Path $app 'LICENSE')
Copy-Item -LiteralPath (Join-Path $repo 'receiver\UPSTREAM.md') -Destination (Join-Path $app 'UPSTREAM.md')
Copy-Item -LiteralPath (Join-Path $repo 'receiver\assets\preview.gif') -Destination (Join-Path $app 'preview.gif')
Copy-Item -LiteralPath (Join-Path $repo 'LICENSE') -Destination (Join-Path $output 'LICENSE')
Copy-Item -LiteralPath (Join-Path $repo 'README.md'), (Join-Path $repo 'README.CN.md') -Destination $output
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Install-A.ps1'), (Join-Path $PSScriptRoot 'Install-B.ps1') -Destination $output
Get-ChildItem -LiteralPath $app -Filter '*.pdb' -File -Recurse | Remove-Item -Force
$zip = $output + '.zip'
if (Test-Path -LiteralPath $zip) { throw "ZIP already exists: $zip" }
Compress-Archive -LiteralPath (Get-ChildItem -LiteralPath $output -Force).FullName -DestinationPath $zip
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText(($zip + '.sha256'), ($hash + '  ' + [IO.Path]::GetFileName($zip) + "`n"), [Text.UTF8Encoding]::new($false))
Write-Output "Built self-contained receiver package: $zip"
