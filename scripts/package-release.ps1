param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SelfContained
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$artifactsRoot = Join-Path $repoRoot "artifacts"
$releaseRoot = Join-Path $artifactsRoot "release"
$extensionSource = Join-Path $repoRoot "YTMPresence\extension"
$extensionOutput = Join-Path $releaseRoot "extension"
$extensionZip = Join-Path $releaseRoot "YTMPresence-extension.zip"
$bundleRoot = Join-Path $releaseRoot "bundle"
$project = Join-Path $repoRoot "YTMPresence.Tray.Wpf\YTMPresence.Tray.Wpf.csproj"
$manifestPath = Join-Path $extensionSource "manifest.json"

if (-not (Test-Path -LiteralPath $project)) {
    throw "Tray project not found: $project"
}

if (-not (Test-Path -LiteralPath $extensionSource)) {
    throw "Extension folder not found: $extensionSource"
}

$extensionVersion = "unknown"
if (Test-Path -LiteralPath $manifestPath) {
    $extensionVersion = (Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json).version
}

$appOutput = Join-Path $releaseRoot "YTMPresence-$extensionVersion-$Runtime-app"

New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null

foreach ($path in @($appOutput, $extensionOutput, $extensionZip, $bundleRoot)) {
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

$bundleName = "YTMPresence-$extensionVersion-$Runtime"
$bundleDir = Join-Path $bundleRoot $bundleName
$bundleAppDir = Join-Path $bundleDir "app"
$bundleExtensionDir = Join-Path $bundleDir "extension"
$bundleZip = Join-Path $releaseRoot "$bundleName.zip"

if (Test-Path -LiteralPath $bundleZip) {
    Remove-Item -LiteralPath $bundleZip -Force
}

New-Item -ItemType Directory -Path $bundleAppDir -Force | Out-Null
Copy-Item -Path (Join-Path $appOutput "*") -Destination $bundleAppDir -Recurse -Force
Copy-Item -LiteralPath $extensionOutput -Destination $bundleExtensionDir -Recurse

$summaryPath = Join-Path $releaseRoot "RELEASE.txt"
$summary = @"
YTMPresence Release
===================

Version:
$extensionVersion

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
Copy-Item -LiteralPath $summaryPath -Destination (Join-Path $bundleDir "RELEASE.txt") -Force
Compress-Archive -Path (Join-Path $bundleDir "*") -DestinationPath $bundleZip -Force

Write-Host ""
Write-Host "Release package created:"
Write-Host "  App:       $appOutput"
Write-Host "  Extension: $extensionZip"
Write-Host "  Bundle:    $bundleZip"
Write-Host "  Notes:     $summaryPath"
