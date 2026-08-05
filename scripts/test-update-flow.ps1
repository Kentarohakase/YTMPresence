param(
  [Parameter(Mandatory = $true)]
  [string]$SetupPath,

  [string]$Version,

  [ValidateRange(1024, 65535)]
  [int]$Port = 18111,

  [switch]$NonInteractive
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$installerScript = Join-Path $repoRoot "installer\YTMPresence.iss"
$resolvedSetup = (Resolve-Path -LiteralPath $SetupPath).Path
$setupFileName = Split-Path -Leaf $resolvedSetup

if (-not (Test-Path -LiteralPath $installerScript -PathType Leaf)) {
  throw "Installer script not found: $installerScript"
}

$installerText = Get-Content -LiteralPath $installerScript -Raw
if ($installerText -match "(?im)taskkill[^\r\n]*/T(?:\s|>|$)") {
  throw "Unsafe installer command found: taskkill must not use /T because it can terminate the running update setup."
}

if ([string]::IsNullOrWhiteSpace($Version)) {
  $match = [regex]::Match($setupFileName, "^YTMPresence-(?<version>\d+\.\d+\.\d+)-.+-setup\.exe$")
  if (-not $match.Success) {
    throw "Could not infer the version from '$setupFileName'. Pass -Version explicitly."
  }

  $Version = $match.Groups["version"].Value
}

if ($Version -notmatch "^\d+\.\d+\.\d+$") {
  throw "Version must use the format major.minor.patch."
}

$baseUrl = "http://127.0.0.1:$Port"
$tempBase = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$tempRoot = [System.IO.Path]::GetFullPath((Join-Path $tempBase "YTMPresence-update-flow-$PID"))
if (-not $tempRoot.StartsWith($tempBase, [System.StringComparison]::OrdinalIgnoreCase)) {
  throw "Temporary test directory escaped the system temp directory."
}

$assetRoot = Join-Path $tempRoot "assets"
$releaseJsonPath = Join-Path $tempRoot "release.json"
$checksumsPath = Join-Path $assetRoot "SHA256SUMS.txt"
$serverScriptPath = Join-Path $tempRoot "server.ps1"
$serverOutputPath = Join-Path $tempRoot "server-output.log"
$serverErrorPath = Join-Path $tempRoot "server-error.log"
$settingsDir = Join-Path $env:APPDATA "YTMPresence"
$settingsPath = Join-Path $settingsDir "settings.json"
$settingsBackupPath = Join-Path $tempRoot "settings.json.backup"
$settingsExisted = Test-Path -LiteralPath $settingsPath -PathType Leaf
$serverProcess = $null

try {
  New-Item -ItemType Directory -Path $assetRoot -Force | Out-Null
  Copy-Item -LiteralPath $resolvedSetup -Destination (Join-Path $assetRoot $setupFileName)

  $setupHash = (Get-FileHash -LiteralPath $resolvedSetup -Algorithm SHA256).Hash.ToLowerInvariant()
  Set-Content -LiteralPath $checksumsPath -Value "$setupHash  $setupFileName" -Encoding ASCII

  $release = [ordered]@{
    tag_name = "v$Version"
    html_url = "$baseUrl/release"
    draft = $false
    prerelease = $false
    assets = @(
      [ordered]@{
        name = $setupFileName
        browser_download_url = "$baseUrl/assets/$([uri]::EscapeDataString($setupFileName))"
      },
      [ordered]@{
        name = "SHA256SUMS.txt"
        browser_download_url = "$baseUrl/assets/SHA256SUMS.txt"
      }
    )
  }
  $releaseJson = $release | ConvertTo-Json -Depth 5
  [System.IO.File]::WriteAllText($releaseJsonPath, $releaseJson, [System.Text.UTF8Encoding]::new($false))

  New-Item -ItemType Directory -Path $settingsDir -Force | Out-Null
  if ($settingsExisted) {
    Copy-Item -LiteralPath $settingsPath -Destination $settingsBackupPath
    $settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
  } else {
    $settings = [pscustomobject]@{}
  }

  if ($settings.PSObject.Properties.Name -contains "UpdateApiUrl") {
    $settings.UpdateApiUrl = "$baseUrl/releases/latest"
  } else {
    $settings | Add-Member -NotePropertyName UpdateApiUrl -NotePropertyValue "$baseUrl/releases/latest"
  }

  # Explizite Testkonfiguration: Nur mit diesem Flag akzeptiert die App eine
  # HTTP-Loopback-Updatequelle. Die Original-settings.json wird am Ende
  # wiederhergestellt, damit das Flag nicht in Produktion aktiv bleibt.
  if ($settings.PSObject.Properties.Name -contains "AllowLoopbackUpdateSource") {
    $settings.AllowLoopbackUpdateSource = $true
  } else {
    $settings | Add-Member -NotePropertyName AllowLoopbackUpdateSource -NotePropertyValue $true
  }

  $settings | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $settingsPath -Encoding UTF8

  $serverScript = @'
param(
  [int]$Port,
  [string]$ReleaseJsonPath,
  [string]$AssetRoot
)

$ErrorActionPreference = "Stop"
$listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $Port)
$listener.Start()

try {
  while ($true) {
    $client = $listener.AcceptTcpClient()

    try {
      $stream = $client.GetStream()
      $reader = [System.IO.StreamReader]::new($stream, [System.Text.Encoding]::ASCII, $false, 1024, $true)
      $requestLine = $reader.ReadLine()

      while (($line = $reader.ReadLine()) -ne "") {
        if ($null -eq $line) {
          break
        }
      }

      $path = ""
      if (-not [string]::IsNullOrWhiteSpace($requestLine)) {
        $parts = $requestLine.Split(" ")
        if ($parts.Length -ge 2) {
          $path = [uri]::UnescapeDataString(($parts[1] -split "\?")[0])
        }
      }

      $status = "200 OK"
      $contentType = "text/plain; charset=utf-8"
      $bytes = $null

      if ($path -eq "/releases/latest") {
        $contentType = "application/json; charset=utf-8"
        $bytes = [System.IO.File]::ReadAllBytes($ReleaseJsonPath)
      } elseif ($path -eq "/release") {
        $contentType = "text/html; charset=utf-8"
        $bytes = [System.Text.Encoding]::UTF8.GetBytes("<html><body><h1>Local YTMPresence update test</h1></body></html>")
      } elseif ($path.StartsWith("/assets/", [System.StringComparison]::OrdinalIgnoreCase)) {
        $assetName = [System.IO.Path]::GetFileName($path.Substring("/assets/".Length))
        $assetPath = Join-Path $AssetRoot $assetName

        if (Test-Path -LiteralPath $assetPath -PathType Leaf) {
          $contentType = "application/octet-stream"
          $bytes = [System.IO.File]::ReadAllBytes($assetPath)
        }
      }

      if ($null -eq $bytes) {
        $status = "404 Not Found"
        $bytes = [System.Text.Encoding]::UTF8.GetBytes("Not found")
      }

      $headers = "HTTP/1.1 $status`r`nContent-Type: $contentType`r`nContent-Length: $($bytes.Length)`r`nConnection: close`r`n`r`n"
      $headerBytes = [System.Text.Encoding]::ASCII.GetBytes($headers)
      $stream.Write($headerBytes, 0, $headerBytes.Length)
      $stream.Write($bytes, 0, $bytes.Length)
      $stream.Flush()
    } finally {
      $client.Close()
    }
  }
} finally {
  $listener.Stop()
}
'@
  [System.IO.File]::WriteAllText($serverScriptPath, $serverScript, [System.Text.UTF8Encoding]::new($false))

  $serverArguments = @(
    "-NoProfile",
    "-ExecutionPolicy", "Bypass",
    "-File", "`"$serverScriptPath`"",
    "-Port", $Port,
    "-ReleaseJsonPath", "`"$releaseJsonPath`"",
    "-AssetRoot", "`"$assetRoot`""
  )
  $serverProcess = Start-Process -FilePath "powershell.exe" `
    -ArgumentList $serverArguments `
    -WindowStyle Hidden `
    -RedirectStandardOutput $serverOutputPath `
    -RedirectStandardError $serverErrorPath `
    -PassThru

  $response = $null
  for ($attempt = 0; $attempt -lt 20 -and $null -eq $response; $attempt++) {
    if ($serverProcess.HasExited) {
      $details = Get-Content -LiteralPath $serverErrorPath -Raw -ErrorAction SilentlyContinue
      throw "Local update server exited before it was ready. $details"
    }

    try {
      $response = Invoke-RestMethod -Uri "$baseUrl/releases/latest" -TimeoutSec 2
    } catch {
      Start-Sleep -Milliseconds 250
    }
  }

  if ($null -eq $response) {
    throw "Local update server did not respond."
  }

  if (-not ($response.PSObject.Properties.Name -contains "tag_name") -or $response.tag_name -ne "v$Version") {
    throw "Local update API returned unexpected release metadata."
  }

  if ($NonInteractive) {
    Write-Host "QA helper self-test passed and settings will be restored." -ForegroundColor Green
    return
  }

  Write-Host ""
  Write-Host "Local updater test is ready." -ForegroundColor Green
  Write-Host "  API:      $baseUrl/releases/latest"
  Write-Host "  Setup:    $resolvedSetup"
  Write-Host "  Settings: $settingsPath"
  Write-Host ""
  Write-Host "1. Start the installed older YTMPresence version."
  Write-Host "2. Use the tray menu to search for updates."
  Write-Host "3. Click 'Herunterladen und installieren'."
  Write-Host "4. Verify that YTMPresence closes but the setup window stays open."
  Write-Host "5. Finish the setup and verify the installed version."
  Write-Host ""
  Read-Host "Press Enter after the manual test to restore the original settings"
} finally {
  if ($null -ne $serverProcess -and -not $serverProcess.HasExited) {
    Stop-Process -Id $serverProcess.Id -Force -ErrorAction SilentlyContinue
    $serverProcess.WaitForExit()
  }

  if ($settingsExisted -and (Test-Path -LiteralPath $settingsBackupPath -PathType Leaf)) {
    Copy-Item -LiteralPath $settingsBackupPath -Destination $settingsPath -Force
  } elseif (-not $settingsExisted -and (Test-Path -LiteralPath $settingsPath -PathType Leaf)) {
    Remove-Item -LiteralPath $settingsPath -Force
  }

  if (Test-Path -LiteralPath $tempRoot) {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force
  }
}
