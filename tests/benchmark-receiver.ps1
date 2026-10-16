$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $repo 'artifacts\receiver-benchmark-2.3.2'
$previousTieredCompilation = $env:DOTNET_TieredCompilation
New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
try {
    $archive = Join-Path $artifacts 'baseline-v2.3.1.zip'
    git -C $repo archive --format=zip --output=$archive v2.3.1 -- receiver/Wallpaper-UI
    if ($LASTEXITCODE -ne 0) { throw 'Could not archive the local v2.3.1 source tag.' }
    $source = Join-Path $artifacts 'source'
    Expand-Archive -LiteralPath $archive -DestinationPath $source -Force
    $project = Join-Path $source 'receiver\Wallpaper-UI\Wallpaper-UI.csproj'
    $binaryDirectory = Join-Path $artifacts 'bin'
    dotnet build $project -c Release --output $binaryDirectory --disable-build-servers -p:UseSharedCompilation=false --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw 'Could not build the unmodified v2.3.1 baseline.' }
    $baseline = Join-Path $binaryDirectory 'Wallpaper-UI.dll'
    $tests = Join-Path $repo 'receiver\Receiver.Tests'
    dotnet build $tests -c Release --disable-build-servers -p:UseSharedCompilation=false --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw 'Could not build the current receiver benchmark.' }
    # Compile both variants equally up front; short runs otherwise compare different JIT tiers.
    $env:DOTNET_TieredCompilation = '0'
    $report = Join-Path $artifacts 'result.json'
    dotnet run --project $tests -c Release --no-build --verbosity quiet -- --benchmark $baseline |
        Tee-Object -FilePath $report
    if ($LASTEXITCODE -ne 0) { throw 'Receiver benchmark failed.' }
}
finally {
    $env:DOTNET_TieredCompilation = $previousTieredCompilation
}
