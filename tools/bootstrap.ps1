$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$panesetRoot = Split-Path $PSScriptRoot -Parent
$panesetSdk = Join-Path $panesetRoot '.tools/dotnet'
$panesetExe = Join-Path $panesetSdk 'dotnet.exe'
if (Test-Path $panesetExe) { return }
$panesetVersion = '10.0.401'
$panesetHash = '24b670ad3d923bfcf47df6c3b034152398b42f6dbc388e10d783aee1cfb5e5817d399fc0ae2a12cfa822a55e61d34830ccb15c50ef6efee437ab874bb7c79430'
$panesetArchive = Join-Path $panesetRoot '.tools/sdk.zip'
New-Item -ItemType Directory -Force (Split-Path $panesetArchive) | Out-Null
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
if (!(Test-Path $panesetArchive)) {
    Write-Output 'Downloading the local .NET SDK...'
    Invoke-WebRequest -UseBasicParsing "https://builds.dotnet.microsoft.com/dotnet/Sdk/$panesetVersion/dotnet-sdk-$panesetVersion-win-x64.zip" -OutFile $panesetArchive
}
if ((Get-FileHash $panesetArchive -Algorithm SHA512).Hash.ToLowerInvariant() -ne $panesetHash) {
    Remove-Item $panesetArchive
    throw 'SDK SHA-512 verification failed. Run bootstrap again to download a fresh archive.'
}
Write-Output 'SHA-512 verified. Extracting the local SDK...'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$panesetStage = Join-Path $panesetRoot '.tools/dotnet.staging'
if (Test-Path $panesetStage) { Remove-Item $panesetStage -Recurse -Force }
[IO.Compression.ZipFile]::ExtractToDirectory($panesetArchive, $panesetStage)
& (Join-Path $panesetStage 'dotnet.exe') --version
if ($LASTEXITCODE -ne 0) { throw 'The downloaded SDK cannot run on this host.' }
Move-Item $panesetStage $panesetSdk
Remove-Item $panesetArchive
