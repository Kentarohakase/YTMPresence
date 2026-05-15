param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SelfContained,
    [switch]$KeepOldArtifacts,
    [switch]$SkipInstaller,
    [ValidateSet("Auto", "Inno", "Legacy", "None")]
    [string]$InstallerMode = "Auto",
    [string]$InnoSetupCompiler = ""
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
$verifyScript = Join-Path $PSScriptRoot "verify-release.ps1"
$innoInstallerScript = Join-Path $PSScriptRoot "package-inno-installer.ps1"
$legacyInstallerScript = Join-Path $PSScriptRoot "package-installer.ps1"

if ($SkipInstaller) {
    $InstallerMode = "None"
}

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

function Find-InnoSetupCompiler {
    param([string]$PreferredPath)

    if (-not [string]::IsNullOrWhiteSpace($PreferredPath)) {
        if (Test-Path -LiteralPath $PreferredPath) {
            return (Resolve-Path -LiteralPath $PreferredPath).Path
        }

        return ""
    }

    $command = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $candidates = @()
    if (-not [string]::IsNullOrWhiteSpace(${env:ProgramFiles(x86)})) {
        $candidates += (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe")
    }

    if (-not [string]::IsNullOrWhiteSpace($env:ProgramFiles)) {
        $candidates += (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe")
    }

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    return ""
}

New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null

if (-not $KeepOldArtifacts) {
    Get-ChildItem -LiteralPath $releaseRoot -Directory -Filter "YTMPresence-*-$Runtime-app" -ErrorAction SilentlyContinue |
        Where-Object { [System.IO.Path]::GetFullPath($_.FullName) -ne [System.IO.Path]::GetFullPath($appOutput) } |
        ForEach-Object { Remove-ReleasePath -Path $_.FullName }

    Get-ChildItem -LiteralPath $releaseRoot -File -Filter "YTMPresence-*-$Runtime.zip" -ErrorAction SilentlyContinue |
        Where-Object { [System.IO.Path]::GetFullPath($_.FullName) -ne [System.IO.Path]::GetFullPath($bundleZip) } |
        ForEach-Object { Remove-ReleasePath -Path $_.FullName }

    Remove-ReleasePath -Path (Join-Path $releaseRoot "YTMPresence-$Runtime")
    Remove-ReleasePath -Path (Join-Path $releaseRoot "YTMPresence-$Runtime.zip")
}

foreach ($path in @($appOutput, $extensionOutput, $extensionZip, $bundleRoot)) {
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

if (Test-Path -LiteralPath $verifyScript) {
    & $verifyScript -ReleaseRoot $releaseRoot -Version $extensionVersion -Runtime $Runtime
}

$setupPath = Join-Path $releaseRoot "$bundleName-setup.exe"
$installerUsed = "None"

switch ($InstallerMode) {
    "None" {
        Write-Host "Installer packaging skipped."
    }
    "Inno" {
        if (-not (Test-Path -LiteralPath $innoInstallerScript)) {
            throw "Inno installer script not found: $innoInstallerScript"
        }

        & $innoInstallerScript -ReleaseRoot $releaseRoot -Version $extensionVersion -Runtime $Runtime -InnoSetupCompiler $InnoSetupCompiler
        $installerUsed = "Inno Setup"
    }
    "Legacy" {
        if (-not (Test-Path -LiteralPath $legacyInstallerScript)) {
            throw "Legacy installer script not found: $legacyInstallerScript"
        }

        & $legacyInstallerScript -ReleaseRoot $releaseRoot -Version $extensionVersion -Runtime $Runtime
        $installerUsed = "Legacy IExpress"
    }
    "Auto" {
        $iscc = Find-InnoSetupCompiler -PreferredPath $InnoSetupCompiler
        if (-not [string]::IsNullOrWhiteSpace($iscc) -and (Test-Path -LiteralPath $innoInstallerScript)) {
            & $innoInstallerScript -ReleaseRoot $releaseRoot -Version $extensionVersion -Runtime $Runtime -InnoSetupCompiler $iscc
            $installerUsed = "Inno Setup"
        }
        elseif (Test-Path -LiteralPath $legacyInstallerScript) {
            Write-Warning "ISCC.exe was not found. Falling back to legacy IExpress installer. Use -InstallerMode Inno for release builds."
            & $legacyInstallerScript -ReleaseRoot $releaseRoot -Version $extensionVersion -Runtime $Runtime
            $installerUsed = "Legacy IExpress"
        }
        else {
            throw "No installer builder is available. Install Inno Setup 6 or restore $legacyInstallerScript."
        }
    }
}

Write-Host ""
Write-Host "Release package created:"
Write-Host "  App:       $appOutput"
Write-Host "  Extension: $extensionZip"
Write-Host "  Bundle:    $bundleZip"
if ($InstallerMode -ne "None") {
    Write-Host "  Setup:     $setupPath"
    Write-Host "  Installer: $installerUsed"
}
Write-Host "  Notes:     $summaryPath"
