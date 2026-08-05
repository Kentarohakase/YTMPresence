param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SelfContained,
    [switch]$KeepOldArtifacts,
    [switch]$SkipInstaller,
    [string]$InnoSetupCompiler = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$artifactsRoot = Join-Path $repoRoot "artifacts"
$releaseRoot = Join-Path $artifactsRoot "release"
$extensionSource = Join-Path $repoRoot "YTMPresence\extension"
$extensionOutput = Join-Path $releaseRoot "extension"
$extensionZip = Join-Path $releaseRoot "YTMPresence-extension.zip"
$extensionFirefoxOutput = Join-Path $releaseRoot "extension-firefox"
$extensionFirefoxZip = Join-Path $releaseRoot "YTMPresence-extension-firefox.zip"
$bundleRoot = Join-Path $releaseRoot "bundle"
$project = Join-Path $repoRoot "YTMPresence.Tray.Wpf\YTMPresence.Tray.Wpf.csproj"
$manifestPath = Join-Path $extensionSource "manifest.json"
$verifyScript = Join-Path $PSScriptRoot "verify-release.ps1"
$versionConsistencyScript = Join-Path $PSScriptRoot "verify-version-consistency.ps1"
$innoInstallerScript = Join-Path $PSScriptRoot "package-inno-installer.ps1"

if (-not (Test-Path -LiteralPath $versionConsistencyScript)) {
    throw "Version consistency script not found: $versionConsistencyScript"
}

& $versionConsistencyScript -RequireChangelog

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
$bundleName = "YTMPresence-$extensionVersion-$Runtime"
$bundleDir = Join-Path $bundleRoot $bundleName
$bundleAppDir = Join-Path $bundleDir "app"
$bundleExtensionDir = Join-Path $bundleDir "extension"
$bundleZip = Join-Path $releaseRoot "$bundleName.zip"
$setupPath = Join-Path $releaseRoot "$bundleName-setup.exe"

function Test-PathIsUnderRoot {
    param(
        [Parameter(Mandatory = $true)][string]$TargetPath,
        [Parameter(Mandatory = $true)][string]$RootPath
    )

    $rootFullPath = [System.IO.Path]::GetFullPath($RootPath).TrimEnd([char[]]@('\', '/'))
    $targetFullPath = [System.IO.Path]::GetFullPath($TargetPath)

    return $targetFullPath.Equals($rootFullPath, [System.StringComparison]::OrdinalIgnoreCase) -or
        $targetFullPath.StartsWith($rootFullPath + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)
}

function Remove-ReleasePath {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $releaseFullPath = [System.IO.Path]::GetFullPath($releaseRoot)
    $targetFullPath = [System.IO.Path]::GetFullPath($Path)

    if (-not (Test-PathIsUnderRoot -TargetPath $targetFullPath -RootPath $releaseFullPath)) {
        throw "Refusing to remove path outside release root: $targetFullPath"
    }

    Remove-Item -LiteralPath $targetFullPath -Recurse -Force
}

New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null

if (-not $KeepOldArtifacts) {
    Get-ChildItem -LiteralPath $releaseRoot -Directory -Filter "YTMPresence-*-$Runtime-app" -ErrorAction SilentlyContinue |
        Where-Object { [System.IO.Path]::GetFullPath($_.FullName) -ne [System.IO.Path]::GetFullPath($appOutput) } |
        ForEach-Object { Remove-ReleasePath -Path $_.FullName }

    Get-ChildItem -LiteralPath $releaseRoot -File -Filter "YTMPresence-*-$Runtime.zip" -ErrorAction SilentlyContinue |
        Where-Object { [System.IO.Path]::GetFullPath($_.FullName) -ne [System.IO.Path]::GetFullPath($bundleZip) } |
        ForEach-Object { Remove-ReleasePath -Path $_.FullName }

    Get-ChildItem -LiteralPath $releaseRoot -File -Filter "YTMPresence-*-$Runtime-setup.exe" -ErrorAction SilentlyContinue |
        Where-Object { [System.IO.Path]::GetFullPath($_.FullName) -ne [System.IO.Path]::GetFullPath($setupPath) } |
        ForEach-Object { Remove-ReleasePath -Path $_.FullName }

    Remove-ReleasePath -Path (Join-Path $releaseRoot "YTMPresence-$Runtime")
    Remove-ReleasePath -Path (Join-Path $releaseRoot "YTMPresence-$Runtime.zip")
    Remove-ReleasePath -Path (Join-Path $releaseRoot "YTMPresence-$Runtime-setup.exe")
}

foreach ($path in @($appOutput, $extensionOutput, $extensionZip, $extensionFirefoxOutput, $extensionFirefoxZip, $bundleRoot)) {
    Remove-ReleasePath -Path $path
}

$selfContainedValue = if ($SelfContained) { "true" } else { "false" }

dotnet publish $project `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained:$selfContainedValue `
    --output $appOutput

Copy-Item -LiteralPath $extensionSource -Destination $extensionOutput -Recurse
Compress-Archive -Path (Join-Path $extensionOutput "*") -DestinationPath $extensionZip -Force

# Firefox-Variante: identische Dateien, Manifest um browser_specific_settings
# und background.scripts-Fallback ergaenzt. Version bleibt automatisch synchron.
& (Join-Path $PSScriptRoot "make-firefox-extension.ps1") -SourceDir $extensionSource -DestDir $extensionFirefoxOutput
Compress-Archive -Path (Join-Path $extensionFirefoxOutput "*") -DestinationPath $extensionFirefoxZip -Force

Remove-ReleasePath -Path $bundleZip

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

Installation:
1. Starte YTMPresence.exe aus dem app-Ordner.
2. Kopiere den Token aus dem Tray-Menü.
3. Lade den extension-Ordner als entpackte Erweiterung.
4. Füge den Token in den Extension-Optionen ein.

Runtime:
Dieses Paket ist framework-dependent, außer es wurde mit -SelfContained gebaut.
Framework-dependent Pakete benötigen die .NET 10 Desktop Runtime auf dem Ziel-PC.
"@

Set-Content -LiteralPath $summaryPath -Value $summary -Encoding UTF8
Copy-Item -LiteralPath $summaryPath -Destination (Join-Path $bundleDir "RELEASE.txt") -Force
Compress-Archive -Path (Join-Path $bundleDir "*") -DestinationPath $bundleZip -Force

if ($SkipInstaller) {
    Remove-ReleasePath -Path $setupPath
    Write-Host "Installer packaging skipped."
}
else {
    if (-not (Test-Path -LiteralPath $innoInstallerScript)) {
        throw "Inno installer script not found: $innoInstallerScript"
    }

    & $innoInstallerScript -ReleaseRoot $releaseRoot -Version $extensionVersion -Runtime $Runtime -InnoSetupCompiler $InnoSetupCompiler
}

# Reihenfolge im Release-Prozess: erst alle Artefakte fertigstellen, danach
# die Prüfsummenliste erzeugen. Ab hier darf kein Artefakt mehr verändert
# werden; verify-release.ps1 verifiziert die Liste nur noch (kein Neuschreiben).
$checksumPath = Join-Path $releaseRoot "SHA256SUMS.txt"
$checksumFiles = @($bundleZip, $extensionZip, $extensionFirefoxZip)
if (-not $SkipInstaller) {
    $checksumFiles += $setupPath
}

$checksumLines = foreach ($file in $checksumFiles) {
    $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $file
    "$($hash.Hash.ToLowerInvariant())  $(Split-Path -Leaf $file)"
}

# Deterministic ASCII avoids a UTF-8 BOM on Windows PowerShell 5.1. A BOM in
# front of the first hash would make the strict updater parser reject it.
[System.IO.File]::WriteAllLines($checksumPath, [string[]]$checksumLines, [System.Text.Encoding]::ASCII)
Write-Host "Checksums written: $checksumPath"

if (Test-Path -LiteralPath $verifyScript) {
    if ($SkipInstaller) {
        & $verifyScript -ReleaseRoot $releaseRoot -Version $extensionVersion -Runtime $Runtime
    }
    else {
        & $verifyScript -ReleaseRoot $releaseRoot -Version $extensionVersion -Runtime $Runtime -RequireInstaller
    }
}

Write-Host ""
Write-Host "Release package created:"
Write-Host "  App:       $appOutput"
Write-Host "  Extension: $extensionZip"
Write-Host "  Firefox:   $extensionFirefoxZip"
Write-Host "  Bundle:    $bundleZip"
if (-not $SkipInstaller) {
    Write-Host "  Setup:     $setupPath"
    Write-Host "  Installer: Inno Setup"
}
Write-Host "  Notes:     $summaryPath"
