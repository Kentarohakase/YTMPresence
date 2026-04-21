param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SelfContained
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$artifactsRoot = Join-Path $repoRoot "artifacts"
$releaseRoot = Join-Path $artifactsRoot "release"
$appOutput = Join-Path $releaseRoot "YTMPresence-$Runtime"
$extensionSource = Join-Path $repoRoot "YTMPresence\extension"
$extensionOutput = Join-Path $releaseRoot "extension"
$extensionZip = Join-Path $releaseRoot "YTMPresence-extension.zip"
$project = Join-Path $repoRoot "YTMPresence.Tray.Wpf\YTMPresence.Tray.Wpf.csproj"

if (-not (Test-Path -LiteralPath $project)) {
    throw "Tray project not found: $project"
}

if (-not (Test-Path -LiteralPath $extensionSource)) {
    throw "Extension folder not found: $extensionSource"
}

New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null

foreach ($path in @($appOutput, $extensionOutput, $extensionZip)) {
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Recurse -Force
    }
}

$selfContainedValue = if ($SelfContained) { "true" } else { "false" }

dotnet publish $project `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained:$selfContainedValue `
    --output $appOutput

Copy-Item -LiteralPath $extensionSource -Destination $extensionOutput -Recurse
Compress-Archive -Path (Join-Path $extensionOutput "*") -DestinationPath $extensionZip -Force

$summaryPath = Join-Path $releaseRoot "RELEASE.txt"
$summary = @"
YTMPresence Release
===================

App:
$appOutput

Extension folder:
$extensionOutput

Extension zip:
$extensionZip

Install:
1. Start YTMPresence.exe from the app folder.
2. Copy the token from the tray menu.
3. Load the extension folder as an unpacked extension.
4. Paste the token into the extension options.

Runtime:
This package is framework-dependent unless it was built with -SelfContained.
Framework-dependent packages require the .NET 10 Desktop Runtime on the target PC.
"@

Set-Content -LiteralPath $summaryPath -Value $summary -Encoding UTF8

Write-Host ""
Write-Host "Release package created:"
Write-Host "  App:       $appOutput"
Write-Host "  Extension: $extensionZip"
Write-Host "  Notes:     $summaryPath"
