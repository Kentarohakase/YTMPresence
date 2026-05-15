param(
    [string]$ReleaseRoot = (Join-Path $PSScriptRoot "..\artifacts\release"),
    [string]$Version = "",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

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
$bundleZip = Join-Path $releaseRoot "$bundleName.zip"
$extensionZip = Join-Path $releaseRoot "YTMPresence-extension.zip"
$setupExe = Join-Path $releaseRoot "$bundleName-setup.exe"
$installerRoot = Join-Path $releaseRoot "installer"
$payloadRoot = Join-Path $installerRoot "payload"
$sedPath = Join-Path $installerRoot "YTMPresence-setup.sed"
$installScriptPath = Join-Path $payloadRoot "install.ps1"
$uninstallScriptPath = Join-Path $payloadRoot "uninstall.ps1"

if (-not (Test-Path -LiteralPath $bundleZip)) {
    throw "Bundle ZIP not found: $bundleZip"
}

if (-not (Test-Path -LiteralPath $extensionZip)) {
    throw "Extension ZIP not found: $extensionZip"
}

function Remove-InstallerPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $releaseFullPath = [System.IO.Path]::GetFullPath($releaseRoot)
    $targetFullPath = [System.IO.Path]::GetFullPath($Path)

    if (-not $targetFullPath.StartsWith($releaseFullPath, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove path outside release root: $targetFullPath"
    }

    Remove-Item -LiteralPath $targetFullPath -Recurse -Force
}

Remove-InstallerPath -Path $installerRoot
Remove-InstallerPath -Path $setupExe

New-Item -ItemType Directory -Path $payloadRoot -Force | Out-Null
Copy-Item -LiteralPath $bundleZip -Destination (Join-Path $payloadRoot (Split-Path -Leaf $bundleZip)) -Force

$installScript = @'
param(
    [switch]$StartApp
)

$ErrorActionPreference = "Stop"

$installDir = Join-Path $env:LOCALAPPDATA "Programs\YTMPresence"
$payloadZip = Join-Path $PSScriptRoot "{{BUNDLE_FILE}}"
$tempDir = Join-Path $env:TEMP ("YTMPresence-install-" + [Guid]::NewGuid().ToString("N"))
$appDir = Join-Path $installDir "app"
$appExe = Join-Path $appDir "YTMPresence.exe"
$programsDir = [Environment]::GetFolderPath("Programs")
$startMenuDir = Join-Path $programsDir "YTMPresence"

if (-not (Test-Path -LiteralPath $payloadZip)) {
    throw "Installationspaket nicht gefunden: $payloadZip"
}

Get-Process -Name "YTMPresence" -ErrorAction SilentlyContinue |
    Stop-Process -Force -ErrorAction SilentlyContinue

New-Item -ItemType Directory -Path $installDir -Force | Out-Null
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

try {
    Expand-Archive -LiteralPath $payloadZip -DestinationPath $tempDir -Force
    Copy-Item -Path (Join-Path $tempDir "*") -Destination $installDir -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "uninstall.ps1") -Destination (Join-Path $installDir "uninstall.ps1") -Force

    New-Item -ItemType Directory -Path $startMenuDir -Force | Out-Null

    $shell = New-Object -ComObject WScript.Shell

    $appShortcut = $shell.CreateShortcut((Join-Path $startMenuDir "YTMPresence.lnk"))
    $appShortcut.TargetPath = $appExe
    $appShortcut.WorkingDirectory = $appDir
    $appShortcut.IconLocation = "$appExe,0"
    $appShortcut.Save()

    $uninstallShortcut = $shell.CreateShortcut((Join-Path $startMenuDir "YTMPresence deinstallieren.lnk"))
    $uninstallShortcut.TargetPath = "powershell.exe"
    $uninstallShortcut.Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$installDir\uninstall.ps1`""
    $uninstallShortcut.WorkingDirectory = $installDir
    $uninstallShortcut.IconLocation = "$appExe,0"
    $uninstallShortcut.Save()

    $installInfo = @{
        version = "{{VERSION}}"
        installedAt = (Get-Date).ToString("o")
        installDir = $installDir
    } | ConvertTo-Json

    Set-Content -LiteralPath (Join-Path $installDir "install.json") -Value $installInfo -Encoding UTF8

    if ($StartApp -and (Test-Path -LiteralPath $appExe)) {
        Start-Process -FilePath $appExe -WorkingDirectory $appDir
    }

    Write-Host "YTMPresence wurde installiert: $installDir"
}
finally {
    Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue
}
'@

$installScript = $installScript.Replace("{{BUNDLE_FILE}}", (Split-Path -Leaf $bundleZip))
$installScript = $installScript.Replace("{{VERSION}}", $Version)

$uninstallScript = @'
$ErrorActionPreference = "Stop"

$installDir = Join-Path $env:LOCALAPPDATA "Programs\YTMPresence"
$programsDir = [Environment]::GetFolderPath("Programs")
$startMenuDir = Join-Path $programsDir "YTMPresence"
$runKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"

Get-Process -Name "YTMPresence" -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -like "$installDir*" } |
    Stop-Process -Force -ErrorAction SilentlyContinue

Remove-ItemProperty -Path $runKey -Name "YTM Presence" -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $startMenuDir -Recurse -Force -ErrorAction SilentlyContinue

if (Test-Path -LiteralPath $installDir) {
    $cleanupScript = Join-Path $env:TEMP ("YTMPresence-cleanup-" + [Guid]::NewGuid().ToString("N") + ".ps1")
    @"
Start-Sleep -Seconds 1
Remove-Item -LiteralPath '$installDir' -Recurse -Force -ErrorAction SilentlyContinue
"@ | Set-Content -LiteralPath $cleanupScript -Encoding UTF8

    Start-Process -FilePath "powershell.exe" -WindowStyle Hidden -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$cleanupScript`""
}

Write-Host "YTMPresence wurde deinstalliert."
'@

Set-Content -LiteralPath $installScriptPath -Value $installScript -Encoding UTF8
Set-Content -LiteralPath $uninstallScriptPath -Value $uninstallScript -Encoding UTF8

$iexpressCommand = Get-Command iexpress.exe -ErrorAction SilentlyContinue
$iexpress = if ($iexpressCommand) { $iexpressCommand.Source } else { "" }
if ([string]::IsNullOrWhiteSpace($iexpress)) {
    throw "iexpress.exe was not found. Cannot create setup EXE."
}

$bundleLeaf = Split-Path -Leaf $bundleZip
$setupTarget = [System.IO.Path]::GetFullPath($setupExe)
$payloadFullPath = [System.IO.Path]::GetFullPath($payloadRoot)

$sed = @"
[Version]
Class=IEXPRESS
SEDVersion=3
[Options]
PackagePurpose=InstallApp
ShowInstallProgramWindow=1
HideExtractAnimation=0
UseLongFileName=1
InsideCompressed=0
CAB_FixedSize=0
CAB_ResvCodeSigning=0
RebootMode=N
InstallPrompt=
DisplayLicense=
FinishMessage=YTMPresence wurde installiert.
TargetName=$setupTarget
FriendlyName=YTMPresence Setup
AppLaunched=powershell.exe -NoProfile -ExecutionPolicy Bypass -File install.ps1 -StartApp
PostInstallCmd=<None>
AdminQuietInstCmd=powershell.exe -NoProfile -ExecutionPolicy Bypass -File install.ps1
UserQuietInstCmd=powershell.exe -NoProfile -ExecutionPolicy Bypass -File install.ps1
SourceFiles=SourceFiles
[SourceFiles]
SourceFiles0=$payloadFullPath\
[SourceFiles0]
install.ps1=
uninstall.ps1=
$bundleLeaf=
"@

Set-Content -LiteralPath $sedPath -Value $sed -Encoding ASCII

& $iexpress /N /Q $sedPath
if ($null -ne $LASTEXITCODE -and $LASTEXITCODE -ne 0) {
    throw "iexpress.exe failed with exit code $LASTEXITCODE."
}

$deadline = [DateTimeOffset]::UtcNow.AddSeconds(20)
while (-not (Test-Path -LiteralPath $setupExe) -and [DateTimeOffset]::UtcNow -lt $deadline) {
    Start-Sleep -Milliseconds 250
}

if (-not (Test-Path -LiteralPath $setupExe)) {
    throw "Setup EXE was not created: $setupExe"
}

$checksumPath = Join-Path $releaseRoot "SHA256SUMS.txt"
$checksumFiles = @($bundleZip, $extensionZip, $setupExe)
$checksumLines = foreach ($file in $checksumFiles) {
    $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $file
    "$($hash.Hash.ToLowerInvariant())  $(Split-Path -Leaf $file)"
}

Set-Content -LiteralPath $checksumPath -Value $checksumLines -Encoding UTF8

Write-Host "Installer package created:"
Write-Host "  Setup:     $setupExe"
Write-Host "  Checksums: $checksumPath"
