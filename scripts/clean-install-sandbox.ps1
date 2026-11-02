# Läuft innerhalb der Windows Sandbox und wird von test-clean-install.ps1
# vorbereitet. Muss mit Windows PowerShell 5.1 laufen, weil die Sandbox kein
# PowerShell 7 enthält.
#
# Ablauf:
#   1. Frische Installation des neuen Setups, Start ohne .NET, Deinstallation
#      bei laufender App (prüft dabei das Shutdown-Event des Installers).
#   2. Optional: Installation des vorherigen Setups, danach Update auf das
#      neue Setup. Settings und Token aus Runde 1 müssen erhalten bleiben.
#
# Ergebnisse landen in output\result.json; der Host-Teil wertet sie aus.

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# Das Skript deinstalliert YTMPresence. Auf dem eigenen PC würde es die echte
# Installation entfernen, deshalb nur unter dem Sandbox-Benutzer ausführen.
if ($env:USERNAME -ne "WDAGUtilityAccount") {
  throw "This script uninstalls YTMPresence and only runs inside Windows Sandbox. Use test-clean-install.ps1 instead."
}

$testRoot = "C:\CleanInstallTest"
$inputDir = Join-Path $testRoot "input"
$outputDir = Join-Path $testRoot "output"
$newSetup = Join-Path $inputDir "new-setup.exe"
$previousSetup = Join-Path $inputDir "previous-setup.exe"
$keepOpenFlag = Join-Path $inputDir "keep-open"

$installRoot = Join-Path $env:LOCALAPPDATA "Programs\YTMPresence"
$appExe = Join-Path $installRoot "app\YTMPresence.exe"
$uninstaller = Join-Path $installRoot "unins000.exe"
$settingsPath = Join-Path $env:APPDATA "YTMPresence\settings.json"
$logPath = Join-Path $env:APPDATA "YTMPresence\logs\app.log"
$startMenuDir = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\YTMPresence"
$uninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{B83D5CB9-A9F4-49E6-9C2C-A206C44E7218}_is1"
$listenPort = 17373

$checks = New-Object System.Collections.Generic.List[object]

function Add-Check {
  param(
    [Parameter(Mandatory = $true)][string]$Name,
    [Parameter(Mandatory = $true)][bool]$Passed,
    [string]$Detail = "",
    [switch]$Informational
  )

  $checks.Add([ordered]@{
    name = $Name
    passed = $Passed
    informational = [bool]$Informational
    detail = $Detail
  })
  $state = if ($Informational) { "INFO" } elseif ($Passed) { "PASS" } else { "FAIL" }
  Write-Host "[$state] $Name $Detail"
}

function Test-PortOpen {
  $client = New-Object System.Net.Sockets.TcpClient
  try {
    $connect = $client.BeginConnect("127.0.0.1", $listenPort, $null, $null)
    if (-not $connect.AsyncWaitHandle.WaitOne(1000)) {
      return $false
    }
    $client.EndConnect($connect)
    return $true
  } catch {
    return $false
  } finally {
    $client.Close()
  }
}

function Wait-Until {
  param(
    [Parameter(Mandatory = $true)][scriptblock]$Condition,
    [int]$TimeoutSeconds = 60
  )

  $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
  while ((Get-Date) -lt $deadline) {
    if (& $Condition) {
      return $true
    }
    Start-Sleep -Milliseconds 500
  }
  return [bool](& $Condition)
}

# Zählt statt Prozesse zurückzugeben: PowerShell entpackt ein einelementiges
# Array beim return, und im Strict Mode hat ein einzelnes Objekt kein Count.
function Get-CompanionProcessCount {
  return @(Get-Process -Name "YTMPresence" -ErrorAction SilentlyContinue).Count
}

function Install-Setup {
  param([Parameter(Mandatory = $true)][string]$Path)

  $process = Start-Process -FilePath $Path `
    -ArgumentList "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/SP-" `
    -Wait -PassThru
  return $process.ExitCode
}

# Der Token selbst wird nie ausgegeben; verglichen wird nur sein Hash.
function Get-TokenHash {
  if (-not (Test-Path -LiteralPath $settingsPath -PathType Leaf)) {
    return $null
  }

  $settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
  if (-not ($settings.PSObject.Properties.Name -contains "SecurityToken") -or
      [string]::IsNullOrWhiteSpace($settings.SecurityToken)) {
    return $null
  }

  $sha = [System.Security.Cryptography.SHA256]::Create()
  try {
    $bytes = $sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($settings.SecurityToken))
    return ([System.BitConverter]::ToString($bytes) -replace "-", "").ToLowerInvariant()
  } finally {
    $sha.Dispose()
  }
}

function Test-NewInstallStarts {
  param([Parameter(Mandatory = $true)][string]$Round)

  Add-Check "$($Round): app installed" (Test-Path -LiteralPath $appExe -PathType Leaf) $appExe
  Add-Check "$($Round): .NET included in the app folder" (Test-Path -LiteralPath (Join-Path (Split-Path -Parent $appExe) "coreclr.dll") -PathType Leaf)

  Start-Process -FilePath $appExe -WorkingDirectory (Split-Path -Parent $appExe) | Out-Null
  $listening = Wait-Until -Condition { Test-PortOpen } -TimeoutSeconds 60
  Add-Check "$($Round): companion listens on 127.0.0.1:$listenPort" $listening
  Add-Check "$($Round): process still running" ((Get-CompanionProcessCount) -gt 0)
  Add-Check "$($Round): log written" (Test-Path -LiteralPath $logPath -PathType Leaf) $logPath
}

function Test-Uninstall {
  param([Parameter(Mandatory = $true)][string]$Round)

  if (-not (Test-Path -LiteralPath $uninstaller -PathType Leaf)) {
    Add-Check "$($Round): uninstaller present" $false $uninstaller
    return
  }

  $wasRunning = (Get-CompanionProcessCount) -gt 0
  Start-Process -FilePath $uninstaller -ArgumentList "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART" | Out-Null

  # Der Inno-Uninstaller startet eine Kopie aus %TEMP% und beendet sich sofort;
  # deshalb auf das Ergebnis warten statt auf den Prozess.
  $removed = Wait-Until -Condition { -not (Test-Path -LiteralPath (Join-Path $installRoot "app")) } -TimeoutSeconds 90
  if ($wasRunning) {
    $stopped = Wait-Until -Condition { (Get-CompanionProcessCount) -eq 0 } -TimeoutSeconds 30
    Add-Check "$($Round): uninstaller stopped the running companion" $stopped
  }

  Add-Check "$($Round): app folder removed" $removed
  Add-Check "$($Round): start menu entry removed" (-not (Test-Path -LiteralPath $startMenuDir))
  Add-Check "$($Round): uninstall entry removed" (-not (Test-Path -LiteralPath $uninstallKey))
  Add-Check "$($Round): settings kept for a later install" (Test-Path -LiteralPath $settingsPath -PathType Leaf) -Informational
}

Start-Transcript -LiteralPath (Join-Path $outputDir "sandbox.log") | Out-Null
$failure = $null

try {
  $dotnetShared = Join-Path $env:ProgramFiles "dotnet\shared"
  $runtimes = @()
  if (Test-Path -LiteralPath $dotnetShared) {
    $runtimes = @(Get-ChildItem -LiteralPath $dotnetShared -Directory | Select-Object -ExpandProperty Name)
  }
  Add-Check "Precondition: no .NET runtime installed" ($runtimes.Count -eq 0) ($runtimes -join ", ")

  # Runde 1: frische Installation
  Add-Check "Fresh install: setup exit code 0" ((Install-Setup -Path $newSetup) -eq 0)
  Test-NewInstallStarts -Round "Fresh install"
  $tokenBefore = Get-TokenHash
  Add-Check "Fresh install: settings with token created" ($null -ne $tokenBefore) $settingsPath
  Test-Uninstall -Round "Fresh install"

  # Runde 2: Update von der vorherigen Version
  if (Test-Path -LiteralPath $previousSetup -PathType Leaf) {
    Add-Check "Update: previous setup exit code 0" ((Install-Setup -Path $previousSetup) -eq 0)

    if (Test-Path -LiteralPath $appExe -PathType Leaf) {
      Start-Process -FilePath $appExe -WorkingDirectory (Split-Path -Parent $appExe) | Out-Null
      $previousListens = Wait-Until -Condition { Test-PortOpen } -TimeoutSeconds 20
      $detail = if ($previousListens) { "it started" } else { "it did not start" }
      Add-Check "Update: previous version without .NET" (-not $previousListens) $detail -Informational
      # Ohne Runtime bleibt nur der Fehlerdialog des App-Hosts offen.
      Get-Process -Name "YTMPresence" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
      Wait-Until -Condition { (Get-CompanionProcessCount) -eq 0 } -TimeoutSeconds 10 | Out-Null
    }

    Add-Check "Update: new setup exit code 0" ((Install-Setup -Path $newSetup) -eq 0)
    Test-NewInstallStarts -Round "Update"
    $tokenAfter = Get-TokenHash
    Add-Check "Update: settings and token kept" ($null -ne $tokenBefore -and $tokenBefore -eq $tokenAfter)
    Test-Uninstall -Round "Update"
  } else {
    Add-Check "Update: skipped, no previous setup given" $true -Informational
  }
} catch {
  $failure = $_.Exception.Message
  Add-Check "Test script finished without an exception" $false $failure
} finally {
  if (Test-Path -LiteralPath $logPath -PathType Leaf) {
    Copy-Item -LiteralPath $logPath -Destination (Join-Path $outputDir "app.log") -Force
  }

  $result = [ordered]@{
    finishedAt = (Get-Date).ToString("o")
    windows = [System.Environment]::OSVersion.VersionString
    checks = $checks
  }
  $json = $result | ConvertTo-Json -Depth 5
  [System.IO.File]::WriteAllText((Join-Path $outputDir "result.json"), $json, (New-Object System.Text.UTF8Encoding($false)))
  Stop-Transcript | Out-Null

  if (-not (Test-Path -LiteralPath $keepOpenFlag)) {
    shutdown.exe /s /f /t 0 | Out-Null
  }
}
