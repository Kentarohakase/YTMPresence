param(
    [Parameter(Mandatory = $true)][string]$SourceDir,
    [Parameter(Mandatory = $true)][string]$DestDir
)

# Erzeugt aus der Chromium-Extension eine Firefox-taugliche Variante.
# Unterschiede zum Basismanifest:
#   - browser_specific_settings.gecko (id, strict_min_version)
#   - background.scripts statt des von Firefox ignorierten service_worker
#   - explizite Mozilla-Datenerhebungsdeklaration für die lokale Übertragung
# Version und alle übrigen Felder bleiben aus dem Basismanifest erhalten.
# Firefox 140 ist die Mindestversion, weil erst sie data_collection_permissions
# kennt und die eingebaute Consent-Anzeige verwendet.

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $SourceDir)) {
    throw "Extension source not found: $SourceDir"
}

if (Test-Path -LiteralPath $DestDir) {
    Remove-Item -LiteralPath $DestDir -Recurse -Force
}

Copy-Item -LiteralPath $SourceDir -Destination $DestDir -Recurse

$manifestPath = Join-Path $DestDir "manifest.json"
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json

# Das Paket ist Firefox-spezifisch; das wirkungslose service_worker-Feld würde
# dort nur eine web-ext-Warnung erzeugen.
$manifest.background.PSObject.Properties.Remove("service_worker")
$manifest.background | Add-Member -NotePropertyName "scripts" -NotePropertyValue @("background.js") -Force

$gecko = [PSCustomObject]@{
    id                 = "ytmpresence@kentarohakase"
    strict_min_version = "140.0"
    data_collection_permissions = [PSCustomObject]@{
        required = @("browsingActivity", "websiteContent")
    }
}

$manifest | Add-Member -NotePropertyName "browser_specific_settings" -NotePropertyValue ([PSCustomObject]@{
    gecko = $gecko
}) -Force

$manifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $manifestPath -Encoding UTF8

Write-Host "Firefox extension manifest written: $manifestPath"
