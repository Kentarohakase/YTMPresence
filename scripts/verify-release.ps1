param(
    [string]$ReleaseRoot = (Join-Path $PSScriptRoot "..\artifacts\release"),
    [string]$Version = "",
    [string]$Runtime = "win-x64",
    [Alias("RequireSetup")]
    [switch]$RequireInstaller
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
$extensionFirefoxZip = Join-Path $releaseRoot "YTMPresence-extension-firefox.zip"
$setupExe = Join-Path $releaseRoot "YTMPresence-$releaseVersion-$Runtime-setup.exe"
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
    (Join-Path $extensionFolder "icons\icon16.png"),
    (Join-Path $extensionFolder "icons\icon32.png"),
    (Join-Path $extensionFolder "icons\icon48.png"),
    (Join-Path $extensionFolder "icons\icon128.png"),
    $extensionZip,
    $extensionFirefoxZip,
    $bundleZip,
    $summaryPath
)

if ($RequireInstaller) {
    $requiredPaths += $setupExe
}

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
        return @($zip.Entries | ForEach-Object { $_.FullName.TrimStart("./") })
    }
    finally {
        $zip.Dispose()
    }
}

<#
.SYNOPSIS
Stellt sicher, dass ein ZIP Forward Slashes als Pfadtrenner nutzt.

.DESCRIPTION
Die ZIP-Spezifikation (APPNOTE 4.4.17.1) schreibt Forward Slashes vor.
Backslash-Eintraege - wie sie Compress-Archive unter Windows PowerShell 5.1
erzeugt - werden von spezifikationstreuen Entpackern als flache Dateinamen
gelesen, nicht als Verzeichnisse. Fuer die Extension bedeutet das, dass
`_locales/<lang>/messages.json` beim Store-Upload nicht gefunden wird.

Diese Pruefung darf die Trenner nicht normalisieren, sonst meldet sie
genau den Defekt als in Ordnung, den sie finden soll.
#>
function Assert-ZipUsesForwardSlashes {
    param([Parameter(Mandatory = $true)][string]$ZipPath)

    $offending = @(Get-ZipEntries -ZipPath $ZipPath | Where-Object { $_.Contains("\") })
    if ($offending.Count -gt 0) {
        $sample = ($offending | Select-Object -First 5) -join ", "
        throw "ZIP '$ZipPath' uses backslash path separators, which violates the ZIP specification and breaks extension store uploads. Offending entries ($($offending.Count)): $sample"
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

$extensionEntries = @(
    "manifest.json",
    "background.js",
    "content.js",
    "page.js",
    "i18n.js",
    "options.html",
    "options.js",
    "popup.html",
    "popup.js",
    "_locales/en/messages.json",
    "_locales/de/messages.json",
    "icons/icon16.png",
    "icons/icon32.png",
    "icons/icon48.png",
    "icons/icon128.png"
)

Assert-ZipUsesForwardSlashes -ZipPath $extensionZip
Assert-ZipUsesForwardSlashes -ZipPath $extensionFirefoxZip
Assert-ZipUsesForwardSlashes -ZipPath $bundleZip

Assert-ZipEntries -ZipPath $extensionZip -Entries $extensionEntries
Assert-ZipEntries -ZipPath $extensionFirefoxZip -Entries $extensionEntries

Assert-ZipEntries -ZipPath $bundleZip -Entries @(
    "app/YTMPresence.exe",
    "app/YTMPresence.dll",
    "app/YTMPresence.Core.dll",
    "app/YTMPresence.runtimeconfig.json",
    "extension/manifest.json",
    "extension/background.js",
    "extension/content.js",
    "extension/page.js",
    "extension/i18n.js",
    "extension/options.html",
    "extension/options.js",
    "extension/popup.html",
    "extension/popup.js",
    "extension/_locales/en/messages.json",
    "extension/_locales/de/messages.json",
    "extension/icons/icon16.png",
    "extension/icons/icon32.png",
    "extension/icons/icon48.png",
    "extension/icons/icon128.png",
    "RELEASE.txt"
)

# --- SHA256SUMS.txt verifizieren (niemals neu schreiben) -------------------
# Die Liste entsteht ausschließlich in package-release.ps1, nachdem alle
# Artefakte final sind. Hier wird geprüft, dass:
#   - die Datei existiert,
#   - jedes veröffentlichte Artefakt genau eine gültige Zeile hat,
#   - jeder Hash exakt zur Datei auf der Platte passt,
#   - keine veralteten, doppelten oder unbekannten Zeilen enthalten sind.

$checksumPath = Join-Path $releaseRoot "SHA256SUMS.txt"
if (-not (Test-Path -LiteralPath $checksumPath -PathType Leaf)) {
    throw "SHA256SUMS.txt is missing: $checksumPath (run package-release.ps1 first)."
}

$checksumFiles = @($bundleZip, $extensionZip, $extensionFirefoxZip)
if ($RequireInstaller) {
    $checksumFiles += $setupExe
}
elseif (Test-Path -LiteralPath $setupExe -PathType Leaf) {
    # Ein vorhandenes Setup muss auch ohne -RequireInstaller korrekt gelistet sein.
    $checksumFiles += $setupExe
}

$expectedNames = @($checksumFiles | ForEach-Object { Split-Path -Leaf $_ })

# Ordinal (case-sensitiv): "ytmpresence-…-SETUP.exe" darf keine gültige
# Zeile für "YTMPresence-…-setup.exe" sein.
$parsedEntries = New-Object 'System.Collections.Generic.Dictionary[string,string]' ([System.StringComparer]::Ordinal)
$lineNumber = 0
foreach ($rawLine in (Get-Content -LiteralPath $checksumPath)) {
    $lineNumber++
    # Get-Content already removes the line ending. Do not trim the checksum
    # line: leading/trailing whitespace around an artifact name must not turn
    # a different name into an exact match.
    $line = $rawLine
    if ([string]::IsNullOrWhiteSpace($line)) {
        continue
    }

    $match = [regex]::Match($line, '^(?<hash>[0-9a-fA-F]{64})[ \t]+\*?(?<name>\S.*?)$')
    if (-not $match.Success) {
        throw "SHA256SUMS.txt line $lineNumber is not a valid checksum line: '$rawLine'"
    }

    $entryName = $match.Groups["name"].Value
    $entryHash = $match.Groups["hash"].Value.ToLowerInvariant()

    if ($parsedEntries.ContainsKey($entryName)) {
        throw "SHA256SUMS.txt contains a duplicate line for '$entryName' (line $lineNumber)."
    }

    $parsedEntries[$entryName] = $entryHash
}

foreach ($staleName in @($parsedEntries.Keys | Where-Object { $expectedNames -cnotcontains $_ })) {
    throw "SHA256SUMS.txt contains a stale or unexpected entry: '$staleName'."
}

foreach ($file in $checksumFiles) {
    $name = Split-Path -Leaf $file
    if (-not $parsedEntries.ContainsKey($name)) {
        throw "SHA256SUMS.txt is missing the entry for '$name'."
    }

    $actualHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $file).Hash.ToLowerInvariant()
    if ($actualHash -cne $parsedEntries[$name]) {
        throw "SHA256 mismatch for '$name': file is $actualHash, SHA256SUMS.txt says $($parsedEntries[$name])."
    }
}

Write-Host "Release verification passed:"
Write-Host "  Version:   $releaseVersion"
Write-Host "  Runtime:   $Runtime"
Write-Host "  Bundle:    $bundleZip"
Write-Host "  Extension: $extensionZip"
Write-Host "  Firefox:   $extensionFirefoxZip"
Write-Host "  Checksums: $checksumPath"
