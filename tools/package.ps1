# PaneSet installer packaging (Velopack).
# Usage: powershell tools/package.ps1 [-Version <x.y.z>] [-Channel <name>]
# Produces dist/<id>/Setup.exe + full package + .sha256 sidecars, matching the
# existing dist naming convention (PaneSet-<version>-win-x64-<shortsha>).
param(
    [string]$Version,
    [string]$Channel = "win-x64"
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$dotnet = Join-Path $root '.tools\dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) { throw "Local dotnet not found at $dotnet (run run.cmd once to bootstrap)." }
$env:DOTNET_ROOT = Split-Path $dotnet -Parent

$vpk = Join-Path $env:USERPROFILE '.dotnet\tools\vpk.exe'
if (-not (Test-Path $vpk)) {
    # Fall back to the extracted vpk.dll (nupkg layout) driven by the local dotnet.
    $vpkDll = Join-Path $env:USERPROFILE 'vpk-extract\tools\net10.0\any\vpk.dll'
    if (Test-Path $vpkDll) { $vpk = $vpkDll } else { throw "vpk not found. Install via 'dotnet tool install -g vpk' or extract vpk.nupkg to %USERPROFILE%\vpk-extract" }
}
$vpkIsDll = $vpk.EndsWith('.dll')

# Version: explicit param > csproj Version property.
if (-not $Version) {
    [xml]$csproj = Get-Content (Join-Path $root 'src\PaneSet\PaneSet.csproj')
    $Version = $csproj.Project.PropertyGroup.Version | Select-Object -First 1
}
$sha = git -C $root rev-parse --short=7 HEAD
$id = "PaneSet-$Version-$Channel-$sha"
$outDir = Join-Path $root "dist\$id"
if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }

# 1. Self-contained publish (same command as TRIAL.md's ZIP flow).
$publishDir = Join-Path $env:TEMP "paneset-publish-$sha"
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
& $dotnet publish (Join-Path $root 'src\PaneSet\PaneSet.csproj') -c Release -r win-x64 --self-contained -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "publish failed" }

# 2. Velopack pack (vpk 1.2.x CLI): pdb already excluded by the built-in default
# --exclude .*\.pdb; per-user install location, no UAC.
$vpkArgs = @('pack', '--packId', 'PaneSet', '--packVersion', $Version, '--packDir', $publishDir,
    '--mainExe', 'PaneSet.exe', '--packTitle', 'PaneSet', '--channel', $Channel,
    '--instLocation', 'Either', '--shortcuts', 'StartMenuRoot',
    '--icon', (Join-Path $root 'src\PaneSet\Assets\PaneSet.ico'),
    '--outputDir', $outDir)
if ($vpkIsDll) { & $dotnet $vpk @vpkArgs } else { & $vpk @vpkArgs }
if ($LASTEXITCODE -ne 0) { throw "vpk pack failed" }

# 3. sha256 sidecars for every artifact, matching the dist convention.
Get-ChildItem $outDir -File | ForEach-Object {
    $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLower()
    "$hash  $($_.Name)" | Set-Content -Path "$($_.FullName).sha256" -Encoding ascii
}

Remove-Item $publishDir -Recurse -Force
Write-Output "Packaged: $outDir"
Get-ChildItem $outDir -File | ForEach-Object { "  {0}  {1:N0} bytes" -f $_.Name, $_.Length }
