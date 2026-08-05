param(
    [switch]$RequireChangelog
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$propsPath = Join-Path $repoRoot "Directory.Build.props"
$manifestPath = Join-Path $repoRoot "YTMPresence\extension\manifest.json"
$changelogPath = Join-Path $repoRoot "CHANGELOG.md"

foreach ($path in @($propsPath, $manifestPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Version source not found: $path"
    }
}

[xml]$props = Get-Content -LiteralPath $propsPath -Raw
$propertyGroup = @($props.Project.PropertyGroup) |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_.Version) } |
    Select-Object -First 1

if ($null -eq $propertyGroup) {
    throw "Directory.Build.props does not define Version."
}

$appVersion = [string]$propertyGroup.Version
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$extensionVersion = [string]$manifest.version

if ($appVersion -notmatch '^\d+\.\d+\.\d+$') {
    throw "App version '$appVersion' must use major.minor.patch."
}

if ($extensionVersion -ne $appVersion) {
    throw "Extension version '$extensionVersion' does not match app version '$appVersion'."
}

$expectedAssemblyVersion = "$appVersion.0"
$versionProperties = @{
    AssemblyVersion      = $expectedAssemblyVersion
    FileVersion          = $expectedAssemblyVersion
    InformationalVersion = $appVersion
}

foreach ($name in $versionProperties.Keys) {
    $actual = [string]$propertyGroup.$name
    $expected = $versionProperties[$name]
    if ($actual -ne $expected) {
        throw "$name '$actual' does not match expected value '$expected'."
    }
}

if ($RequireChangelog) {
    if (-not (Test-Path -LiteralPath $changelogPath -PathType Leaf)) {
        throw "Changelog not found: $changelogPath"
    }

    $changelog = Get-Content -LiteralPath $changelogPath -Raw
    $headingPattern = "(?m)^## \[$([regex]::Escape($appVersion))\](?:\s|$)"
    if ($changelog -notmatch $headingPattern) {
        throw "CHANGELOG.md has no release heading for version '$appVersion'."
    }
}

Write-Host "Version consistency check passed: $appVersion"
