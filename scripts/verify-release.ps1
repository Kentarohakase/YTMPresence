param(
    [string]$ReleaseRoot = (Join-Path $PSScriptRoot "..\artifacts\release"),
    [string]$Version = "",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$releaseRootPath = Resolve-Path -LiteralPath $ReleaseRoot
$releaseRoot = $releaseRootPath.Path

$extensionFolder = Join-Path $releaseRoot "extension"
$extensionManifestPath = Join-Path $extensionFolder "manifest.json"

if (-not (Test-Path -LiteralPath $extensionManifestPath)) {
    throw "Extension manifest not found: $extensionManifestPath"
}

$manifest = Get-Content -LiteralPath $extensionManifestPath -Raw | ConvertFrom-Json
$releaseVersion = if ([string]::IsNullOrWhiteSpace($Version)) { $manifest.version } else { $Version }

if ($manifest.version -ne $releaseVersion) {
    throw "Manifest version '$($manifest.version)' does not match release version '$releaseVersion'."
}

$appFolder = Join-Path $releaseRoot "YTMPresence-$releaseVersion-$Runtime-app"
$bundleZip = Join-Path $releaseRoot "YTMPresence-$releaseVersion-$Runtime.zip"
$extensionZip = Join-Path $releaseRoot "YTMPresence-extension.zip"
$summaryPath = Join-Path $releaseRoot "RELEASE.txt"

$requiredPaths = @(
    $appFolder,
    (Join-Path $appFolder "YTMPresence.exe"),
    (Join-Path $appFolder "YTMPresence.dll"),
    (Join-Path $appFolder "YTMPresence.Core.dll"),
    (Join-Path $appFolder "YTMPresence.runtimeconfig.json"),
    $extensionFolder,
    $extensionManifestPath,
    (Join-Path $extensionFolder "background.js"),
    (Join-Path $extensionFolder "content.js"),
    (Join-Path $extensionFolder "page.js"),
    (Join-Path $extensionFolder "options.html"),
    (Join-Path $extensionFolder "options.js"),
    (Join-Path $extensionFolder "popup.html"),
    (Join-Path $extensionFolder "popup.js"),
    $extensionZip,
    $bundleZip,
    $summaryPath
)

foreach ($path in $requiredPaths) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required release item missing: $path"
    }
}

Add-Type -AssemblyName System.IO.Compression.FileSystem

function Get-ZipEntries {
    param([Parameter(Mandatory = $true)][string]$ZipPath)

    $zip = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        return @($zip.Entries | ForEach-Object { $_.FullName.Replace("\", "/").TrimStart("./") })
    }
    finally {
        $zip.Dispose()
    }
}

function Assert-ZipEntries {
    param(
        [Parameter(Mandatory = $true)][string]$ZipPath,
        [Parameter(Mandatory = $true)][string[]]$Entries
    )

    $zipEntries = Get-ZipEntries -ZipPath $ZipPath
    foreach ($entry in $Entries) {
        if ($zipEntries -notcontains $entry) {
            throw "ZIP '$ZipPath' is missing entry '$entry'."
        }
    }
}

Assert-ZipEntries -ZipPath $extensionZip -Entries @(
    "manifest.json",
    "background.js",
    "content.js",
    "page.js",
    "options.html",
    "options.js",
    "popup.html",
    "popup.js"
)

Assert-ZipEntries -ZipPath $bundleZip -Entries @(
    "app/YTMPresence.exe",
    "app/YTMPresence.dll",
    "app/YTMPresence.Core.dll",
    "app/YTMPresence.runtimeconfig.json",
    "extension/manifest.json",
    "extension/background.js",
    "extension/content.js",
    "extension/page.js",
    "extension/options.html",
    "extension/options.js",
    "extension/popup.html",
    "extension/popup.js",
    "RELEASE.txt"
)

$checksumPath = Join-Path $releaseRoot "SHA256SUMS.txt"
$checksumFiles = @($bundleZip, $extensionZip)
$checksumLines = foreach ($file in $checksumFiles) {
    $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $file
    "$($hash.Hash.ToLowerInvariant())  $(Split-Path -Leaf $file)"
}

Set-Content -LiteralPath $checksumPath -Value $checksumLines -Encoding UTF8

Write-Host "Release verification passed:"
Write-Host "  Version:   $releaseVersion"
Write-Host "  Runtime:   $Runtime"
Write-Host "  Bundle:    $bundleZip"
Write-Host "  Extension: $extensionZip"
Write-Host "  Checksums: $checksumPath"
