param(
    [string]$ReleaseRoot = (Join-Path $PSScriptRoot "..\artifacts\release"),
    [string]$ExtractRoot = ""
)

# Prueft die gebauten Extension-Archive so, wie eine Store-Uploadverarbeitung
# sie sieht: Eintragsnamen, echtes Entpacken auf die Platte und Aufloesung der
# __MSG_*__-Platzhalter gegen die Standardsprache.
#
# Hintergrund: web-ext lint laeuft gegen den Quellordner, nie gegen das
# Archiv. Ein Verpackungsfehler - etwa Backslashes als Pfadtrenner, wie sie
# Compress-Archive unter Windows PowerShell 5.1 erzeugt - bleibt dabei
# unsichtbar, obwohl er den Upload bei Chrome Web Store und
# addons.mozilla.org zum Scheitern bringt.

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

Add-Type -AssemblyName System.IO.Compression | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem | Out-Null

$releaseRoot = (Resolve-Path -LiteralPath $ReleaseRoot).Path

if ([string]::IsNullOrWhiteSpace($ExtractRoot)) {
    $ExtractRoot = Join-Path $releaseRoot "archive-check"
}

$requiredEntries = @(
    "manifest.json",
    "background.js",
    "content.js",
    "page.js",
    "i18n.js",
    "connection-ui.js",
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

function Get-ArchiveEntryNames {
    param([Parameter(Mandatory = $true)][string]$ZipPath)

    $archive = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        return @($archive.Entries | ForEach-Object { $_.FullName })
    }
    finally {
        $archive.Dispose()
    }
}

function Assert-EntryNamesAreSafe {
    param(
        [Parameter(Mandatory = $true)][string]$ZipPath,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][string[]]$Names
    )

    # Die ZIP-Spezifikation (APPNOTE 4.4.17.1) schreibt Forward Slashes vor.
    $backslash = @($Names | Where-Object { $_.Contains("\") })
    if ($backslash.Count -gt 0) {
        $sample = ($backslash | Select-Object -First 5) -join ", "
        throw "Archive '$ZipPath' uses backslash path separators. Spec-compliant unpackers read these as flat file names instead of directories, which breaks extension store uploads. Offending entries ($($backslash.Count)): $sample"
    }

    $unsafe = @($Names | Where-Object {
        $_.StartsWith("/") -or
        $_.Contains(":") -or
        (($_ -split "/") -contains "..")
    })
    if ($unsafe.Count -gt 0) {
        $sample = ($unsafe | Select-Object -First 5) -join ", "
        throw "Archive '$ZipPath' contains absolute or traversing entry names: $sample"
    }
}

function Assert-RequiredEntries {
    param(
        [Parameter(Mandatory = $true)][string]$ZipPath,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][string[]]$Names
    )

    foreach ($entry in $requiredEntries) {
        if ($Names -notcontains $entry) {
            throw "Archive '$ZipPath' is missing required entry '$entry'."
        }
    }
}

function Assert-PlaceholdersResolve {
    param([Parameter(Mandatory = $true)][string]$ExtractedDir)

    $manifestPath = Join-Path $ExtractedDir "manifest.json"
    $manifestText = Get-Content -LiteralPath $manifestPath -Raw
    $manifest = $manifestText | ConvertFrom-Json

    $placeholders = @([regex]::Matches($manifestText, "__MSG_(?<key>[A-Za-z0-9_]+)__") |
        ForEach-Object { $_.Groups["key"].Value } |
        Select-Object -Unique)

    if ($placeholders.Count -eq 0) {
        return
    }

    if (-not ($manifest.PSObject.Properties.Name -contains "default_locale")) {
        throw "Manifest in '$ExtractedDir' uses __MSG_*__ placeholders but declares no default_locale."
    }

    $defaultLocale = [string]$manifest.default_locale
    $messagesPath = Join-Path $ExtractedDir "_locales\$defaultLocale\messages.json"

    if (-not (Test-Path -LiteralPath $messagesPath -PathType Leaf)) {
        throw "Default locale catalog not found after extraction: $messagesPath"
    }

    $messages = Get-Content -LiteralPath $messagesPath -Raw | ConvertFrom-Json
    $known = @($messages.PSObject.Properties.Name)

    foreach ($key in $placeholders) {
        if ($known -notcontains $key) {
            throw "Manifest placeholder '__MSG_${key}__' has no entry in the default locale catalog '$messagesPath'."
        }
    }

    Write-Host "    $($placeholders.Count) manifest placeholder(s) resolve against '$defaultLocale'."
}

function Assert-LocaleCatalogsAreValid {
    param([Parameter(Mandatory = $true)][string]$ExtractedDir)

    $localesDir = Join-Path $ExtractedDir "_locales"
    $catalogs = @(Get-ChildItem -LiteralPath $localesDir -Directory | Sort-Object Name)

    if ($catalogs.Count -eq 0) {
        throw "No locale catalogs found under: $localesDir"
    }

    foreach ($catalog in $catalogs) {
        $messagesPath = Join-Path $catalog.FullName "messages.json"
        if (-not (Test-Path -LiteralPath $messagesPath -PathType Leaf)) {
            throw "Locale '$($catalog.Name)' has no messages.json."
        }

        $messages = Get-Content -LiteralPath $messagesPath -Raw | ConvertFrom-Json
        foreach ($property in $messages.PSObject.Properties) {
            if (-not ($property.Value.PSObject.Properties.Name -contains "message")) {
                throw "Locale '$($catalog.Name)' entry '$($property.Name)' has no 'message' field."
            }
        }

        Write-Host "    Locale '$($catalog.Name)': $(@($messages.PSObject.Properties).Count) message(s)."
    }
}

function Test-ExtensionArchive {
    param([Parameter(Mandatory = $true)][string]$ZipPath)

    if (-not (Test-Path -LiteralPath $ZipPath -PathType Leaf)) {
        throw "Extension archive not found: $ZipPath"
    }

    $name = [System.IO.Path]::GetFileNameWithoutExtension($ZipPath)
    Write-Host "Checking $name ..."

    $names = Get-ArchiveEntryNames -ZipPath $ZipPath
    Assert-EntryNamesAreSafe -ZipPath $ZipPath -Names $names
    Assert-RequiredEntries -ZipPath $ZipPath -Names $names

    # Echtes Entpacken: erst hier zeigt sich, ob aus den Eintragsnamen
    # tatsaechlich Verzeichnisse werden.
    $target = Join-Path $ExtractRoot $name
    [System.IO.Compression.ZipFile]::ExtractToDirectory($ZipPath, $target)

    foreach ($entry in $requiredEntries) {
        $expected = Join-Path $target ($entry -replace "/", "\")
        if (-not (Test-Path -LiteralPath $expected -PathType Leaf)) {
            throw "Entry '$entry' did not extract to a real file: $expected"
        }
    }

    Assert-PlaceholdersResolve -ExtractedDir $target
    Assert-LocaleCatalogsAreValid -ExtractedDir $target

    Write-Host "    OK: $($names.Count) entries, extracted to $target"
    return $target
}

if (Test-Path -LiteralPath $ExtractRoot) {
    Remove-Item -LiteralPath $ExtractRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $ExtractRoot -Force | Out-Null

$chromiumDir = Test-ExtensionArchive -ZipPath (Join-Path $releaseRoot "YTMPresence-extension.zip")
$firefoxDir = Test-ExtensionArchive -ZipPath (Join-Path $releaseRoot "YTMPresence-extension-firefox.zip")

$firefoxManifest = Get-Content -LiteralPath (Join-Path $firefoxDir "manifest.json") -Raw | ConvertFrom-Json
if (-not ($firefoxManifest.PSObject.Properties.Name -contains "browser_specific_settings")) {
    throw "Firefox archive is missing browser_specific_settings."
}

Write-Host ""
Write-Host "Extension archives verified." -ForegroundColor Green
Write-Host "  Chromium: $chromiumDir"
Write-Host "  Firefox:  $firefoxDir"
