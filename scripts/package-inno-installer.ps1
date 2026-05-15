param(
    [string]$ReleaseRoot = (Join-Path $PSScriptRoot "..\artifacts\release"),
    [string]$Version = "",
    [string]$Runtime = "win-x64",
    [string]$InnoSetupCompiler = ""
)

$ErrorActionPreference = "Stop"

$repoRootPath = Resolve-Path (Join-Path $PSScriptRoot "..")
$repoRoot = $repoRootPath.Path
$releaseRootPath = Resolve-Path -LiteralPath $ReleaseRoot
$releaseRoot = $releaseRootPath.Path

if ([string]::IsNullOrWhiteSpace($Version)) {
    $manifestPath = Join-Path $releaseRoot "extension\manifest.json"
    if (-not (Test-Path -LiteralPath $manifestPath)) {
        throw "Extension manifest not found: $manifestPath"
    }

    $Version = (Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json).version
}

$bundleName = "YTMPresence-$Version-$Runtime"
$appFolder = Join-Path $releaseRoot "$bundleName-app"
$extensionFolder = Join-Path $releaseRoot "extension"
$bundleZip = Join-Path $releaseRoot "$bundleName.zip"
$extensionZip = Join-Path $releaseRoot "YTMPresence-extension.zip"
$setupExe = Join-Path $releaseRoot "$bundleName-setup.exe"
$summaryPath = Join-Path $releaseRoot "RELEASE.txt"
$issPath = Join-Path $repoRoot "installer\YTMPresence.iss"
$appIcon = Join-Path $repoRoot "YTMPresence.Tray.Wpf\Assets\app.ico"

foreach ($path in @($appFolder, $extensionFolder, $bundleZip, $extensionZip, $summaryPath, $issPath, $appIcon)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required installer input missing: $path"
    }
}

function Find-InnoSetupCompiler {
    param([string]$PreferredPath)

    if (-not [string]::IsNullOrWhiteSpace($PreferredPath)) {
        if (Test-Path -LiteralPath $PreferredPath) {
            return (Resolve-Path -LiteralPath $PreferredPath).Path
        }

        throw "Inno Setup compiler was not found: $PreferredPath"
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

    throw "ISCC.exe was not found. Install Inno Setup 6 or pass -InnoSetupCompiler."
}

function Wait-FileReady {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [int]$TimeoutSeconds = 30
    )

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    $lastError = $null

    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        if (Test-Path -LiteralPath $Path) {
            try {
                $stream = [System.IO.File]::Open($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::Read)
                $stream.Dispose()
                return
            }
            catch {
                $lastError = $_.Exception.Message
            }
        }

        Start-Sleep -Milliseconds 250
    }

    if ($lastError) {
        throw "File was not ready before timeout: $Path ($lastError)"
    }

    throw "File was not created before timeout: $Path"
}

$iscc = Find-InnoSetupCompiler -PreferredPath $InnoSetupCompiler

if (Test-Path -LiteralPath $setupExe) {
    Remove-Item -LiteralPath $setupExe -Force
}

$isccArgs = @(
    "/Qp",
    "/O$releaseRoot",
    "/F$bundleName-setup",
    "/DAppVersion=$Version",
    "/DRuntime=$Runtime",
    "/DAppSource=$appFolder",
    "/DExtensionSource=$extensionFolder",
    "/DReleaseSummary=$summaryPath",
    "/DAppIcon=$appIcon",
    $issPath
)

& $iscc @isccArgs
if ($null -ne $LASTEXITCODE -and $LASTEXITCODE -ne 0) {
    throw "ISCC.exe failed with exit code $LASTEXITCODE."
}

Wait-FileReady -Path $setupExe -TimeoutSeconds 30

$checksumPath = Join-Path $releaseRoot "SHA256SUMS.txt"
$checksumFiles = @($bundleZip, $extensionZip, $setupExe)
$checksumLines = foreach ($file in $checksumFiles) {
    $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $file
    "$($hash.Hash.ToLowerInvariant())  $(Split-Path -Leaf $file)"
}

Set-Content -LiteralPath $checksumPath -Value $checksumLines -Encoding UTF8

Write-Host "Inno Setup installer package created:"
Write-Host "  Compiler:  $iscc"
Write-Host "  Setup:     $setupExe"
Write-Host "  Checksums: $checksumPath"
