param([switch]$Check, [Parameter(ValueFromRemainingArguments = $true)][string[]]$TargetArgs)
$ErrorActionPreference = 'Stop'
$panesetRoot = Split-Path $PSScriptRoot -Parent
Set-Location $panesetRoot
& (Join-Path $PSScriptRoot 'bootstrap.ps1')
$env:DOTNET_ROOT = Join-Path $panesetRoot '.tools/dotnet'
$env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$panesetProject = if ($Check) { 'tests/PaneSet.Checks' } else { 'src/PaneSet' }
& (Join-Path $env:DOTNET_ROOT 'dotnet.exe') build $panesetProject -c Release --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$panesetExe = if ($Check) { 'PaneSet.Checks.exe' } else { 'PaneSet.exe' }
& (Join-Path $panesetProject "bin/Release/net10.0-windows/$panesetExe") @TargetArgs
exit $LASTEXITCODE
