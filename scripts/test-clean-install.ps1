param(
  [Parameter(Mandatory = $true)]
  [string]$SetupPath,

  # Setup der zuletzt veröffentlichten Version für den Update-Test. Ohne diesen
  # Parameter wird nur frisch installiert und deinstalliert.
  [string]$PreviousSetupPath,

  [ValidateRange(1, 60)]
  [int]$TimeoutMinutes = 15,

  # Lässt die Sandbox nach dem Test offen, etwa um einen Fehler anzusehen.
  [switch]$KeepOpen
)

# Prüft Installation, Start, Update und Deinstallation in der Windows Sandbox.
# Die Sandbox ist ein frisches Windows ohne die .NET-Runtimes des Host-PCs und
# ohne Netzwerk; ein Setup, das dort startet, bringt alles Nötige selbst mit.
# Die eigentlichen Prüfungen stehen in clean-install-sandbox.ps1.

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$sandboxExe = Join-Path $env:SystemRoot "System32\WindowsSandbox.exe"
if (-not (Test-Path -LiteralPath $sandboxExe -PathType Leaf)) {
  throw "Windows Sandbox is not available. Enable the Windows feature 'Windows Sandbox' and restart Windows."
}

if (@(Get-Process -Name "WindowsSandbox*" -ErrorAction SilentlyContinue).Count -gt 0) {
  throw "Windows Sandbox is already running. Close it first; only one sandbox can run at a time."
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$innerScript = Join-Path $PSScriptRoot "clean-install-sandbox.ps1"
$workRoot = Join-Path $repoRoot "artifacts\clean-install-test"
$inputDir = Join-Path $workRoot "input"
$outputDir = Join-Path $workRoot "output"
$resultPath = Join-Path $outputDir "result.json"
$configPath = Join-Path $workRoot "clean-install.wsb"

$resolvedSetup = (Resolve-Path -LiteralPath $SetupPath).Path
$resolvedPrevious = $null
if (-not [string]::IsNullOrWhiteSpace($PreviousSetupPath)) {
  $resolvedPrevious = (Resolve-Path -LiteralPath $PreviousSetupPath).Path
}

if (Test-Path -LiteralPath $workRoot) {
  Remove-Item -LiteralPath $workRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $inputDir, $outputDir -Force | Out-Null

Copy-Item -LiteralPath $innerScript -Destination (Join-Path $inputDir "clean-install-sandbox.ps1")
Copy-Item -LiteralPath $resolvedSetup -Destination (Join-Path $inputDir "new-setup.exe")
if ($null -ne $resolvedPrevious) {
  Copy-Item -LiteralPath $resolvedPrevious -Destination (Join-Path $inputDir "previous-setup.exe")
}
if ($KeepOpen) {
  New-Item -ItemType File -Path (Join-Path $inputDir "keep-open") | Out-Null
}

$escapedInput = [System.Security.SecurityElement]::Escape($inputDir)
$escapedOutput = [System.Security.SecurityElement]::Escape($outputDir)
$config = @"
<Configuration>
  <Networking>Disable</Networking>
  <MappedFolders>
    <MappedFolder>
      <HostFolder>$escapedInput</HostFolder>
      <SandboxFolder>C:\CleanInstallTest\input</SandboxFolder>
      <ReadOnly>true</ReadOnly>
    </MappedFolder>
    <MappedFolder>
      <HostFolder>$escapedOutput</HostFolder>
      <SandboxFolder>C:\CleanInstallTest\output</SandboxFolder>
      <ReadOnly>false</ReadOnly>
    </MappedFolder>
  </MappedFolders>
  <LogonCommand>
    <Command>powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\CleanInstallTest\input\clean-install-sandbox.ps1</Command>
  </LogonCommand>
</Configuration>
"@
[System.IO.File]::WriteAllText($configPath, $config, [System.Text.UTF8Encoding]::new($false))

Write-Host "Starting Windows Sandbox. The test runs on its own; do not close the window."
Write-Host "  Setup:    $resolvedSetup"
if ($null -ne $resolvedPrevious) {
  Write-Host "  Previous: $resolvedPrevious"
}
Start-Process -FilePath $sandboxExe -ArgumentList "`"$configPath`"" | Out-Null

$deadline = (Get-Date).AddMinutes($TimeoutMinutes)
while (-not (Test-Path -LiteralPath $resultPath -PathType Leaf) -and (Get-Date) -lt $deadline) {
  Start-Sleep -Seconds 5
}

if (-not (Test-Path -LiteralPath $resultPath -PathType Leaf)) {
  throw "No result after $TimeoutMinutes minutes. See $outputDir\sandbox.log if it exists."
}

# Die Sandbox schreibt die Datei in einem Zug; kurz warten, falls sie noch offen ist.
Start-Sleep -Seconds 2
$result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json

# Die Sandbox fährt nach dem Test selbst herunter. Bleibt das Fenster trotzdem
# offen, wird es geschlossen; der Inhalt der Sandbox wird ohnehin verworfen.
if (-not $KeepOpen) {
  $closeDeadline = (Get-Date).AddSeconds(30)
  while (@(Get-Process -Name "WindowsSandbox*" -ErrorAction SilentlyContinue).Count -gt 0 -and (Get-Date) -lt $closeDeadline) {
    Start-Sleep -Seconds 2
  }
  Get-Process -Name "WindowsSandboxRemoteSession", "WindowsSandboxClient" -ErrorAction SilentlyContinue |
    Stop-Process -Force -ErrorAction SilentlyContinue
}

Write-Host ""
Write-Host "Clean install test ($($result.windows))"
$failed = 0
foreach ($check in $result.checks) {
  if ($check.informational) {
    $state = "INFO"
    $color = "Gray"
  } elseif ($check.passed) {
    $state = "PASS"
    $color = "Green"
  } else {
    $state = "FAIL"
    $color = "Red"
    $failed++
  }

  $line = "[$state] $($check.name)"
  if (-not [string]::IsNullOrWhiteSpace($check.detail)) {
    $line += " ($($check.detail))"
  }
  Write-Host $line -ForegroundColor $color
}

Write-Host ""
Write-Host "Details: $outputDir"
if ($failed -gt 0) {
  Write-Host "$failed check(s) failed." -ForegroundColor Red
  exit 1
}

Write-Host "All checks passed." -ForegroundColor Green
