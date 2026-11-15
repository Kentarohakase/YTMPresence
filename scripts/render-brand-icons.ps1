param(
    [string]$SvgPath,
    [string]$SmallSvgPath,
    [string]$BrowserPath
)

# Erzeugt alle Icons aus branding/ytmpresence-logo.svg:
#   - branding/ytmpresence-logo-512.png und -1024.png (Discord App-Icon und Asset ytm_large)
#   - branding/ytmpresence-play-1024.png und -pause-1024.png (Discord-Assets play und pause)
#   - YTMPresence/extension/icons/icon16|32|48|128.png
#   - YTMPresence.Tray.Wpf/Assets/app.ico (Tray, EXE und Installer)
# Jede Größe wird direkt aus der Vektorvorlage gerastert statt aus einem großen
# Bild verkleinert; das hält kleine Icons scharf. Bis 24 px kommt die
# vereinfachte Variante ytmpresence-logo-small.svg zum Einsatz, weil die
# dünnen Signalbögen des großen Logos dort zu blassen Pixeln zerfallen.
# Gerendert wird mit Edge oder Chrome im Headless-Modus, weil Windows SVG nicht
# selbst rastern kann.

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if (-not $SvgPath) {
    $SvgPath = Join-Path $repoRoot "branding\ytmpresence-logo.svg"
}
if (-not $SmallSvgPath) {
    $SmallSvgPath = Join-Path $repoRoot "branding\ytmpresence-logo-small.svg"
}
# Kleine Statussymbole, die Discord rund zugeschnitten über das Logo legt.
$statusIcons = @("play", "pause")
$statusSvgPaths = @{}
foreach ($name in $statusIcons) {
    $statusSvgPaths[$name] = Join-Path $repoRoot "branding\ytmpresence-$name.svg"
}

foreach ($path in @($SvgPath, $SmallSvgPath) + $statusSvgPaths.Values) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Logo SVG not found: $path"
    }
}
$smallSizeLimit = 24

if (-not $BrowserPath) {
    $BrowserPath = @(
        "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe",
        "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe",
        "$env:ProgramFiles\Google\Chrome\Application\chrome.exe"
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_ -PathType Leaf) } | Select-Object -First 1
}
if (-not $BrowserPath) {
    throw "Neither Edge nor Chrome was found. Pass -BrowserPath."
}

# 96 ist das Motiv im 128er-Store-Icon: Google empfiehlt 16 px transparenten Rand.
$sizes = @(16, 20, 24, 32, 40, 48, 64, 96, 128, 256, 512, 1024)
$gap = 8

function Get-DibBytes([System.Drawing.Bitmap]$Bitmap) {
    # ICO-Eintrag als 32-Bit-DIB: BITMAPINFOHEADER mit doppelter Höhe,
    # BGRA-Zeilen von unten nach oben, danach die 1-Bit-AND-Maske.
    $n = $Bitmap.Width
    $maskStride = [int][Math]::Ceiling($n / 32) * 4
    $stream = [IO.MemoryStream]::new()
    $writer = [IO.BinaryWriter]::new($stream)
    $writer.Write([int]40)
    $writer.Write([int]$n)
    $writer.Write([int]($n * 2))
    $writer.Write([int16]1)
    $writer.Write([int16]32)
    $writer.Write([int]0)
    $writer.Write([int]($n * $n * 4 + $maskStride * $n))
    $writer.Write([int]0)
    $writer.Write([int]0)
    $writer.Write([int]0)
    $writer.Write([int]0)

    for ($y = $n - 1; $y -ge 0; $y--) {
        for ($x = 0; $x -lt $n; $x++) {
            $c = $Bitmap.GetPixel($x, $y)
            $writer.Write([byte]$c.B)
            $writer.Write([byte]$c.G)
            $writer.Write([byte]$c.R)
            $writer.Write([byte]$c.A)
        }
    }

    for ($y = $n - 1; $y -ge 0; $y--) {
        $row = New-Object byte[] $maskStride
        for ($x = 0; $x -lt $n; $x++) {
            if ($Bitmap.GetPixel($x, $y).A -eq 0) {
                $index = [int][Math]::Floor($x / 8)
                $row[$index] = [byte]($row[$index] -bor (0x80 -shr ($x % 8)))
            }
        }
        $writer.Write($row)
    }

    $writer.Flush()
    return , $stream.ToArray()
}

function Get-PngBytes([System.Drawing.Bitmap]$Bitmap) {
    $stream = [IO.MemoryStream]::new()
    $Bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    return , $stream.ToArray()
}

function Write-Ico([string]$Path, [System.Drawing.Bitmap[]]$Frames) {
    # Bis 64 px als DIB, damit auch System.Drawing.Icon und ältere APIs sie
    # lesen; 256 px als PNG wie bei Windows-Icons üblich.
    $images = foreach ($frame in $Frames) {
        if ($frame.Width -ge 256) { , (Get-PngBytes $frame) } else { , (Get-DibBytes $frame) }
    }

    $stream = [IO.MemoryStream]::new()
    $writer = [IO.BinaryWriter]::new($stream)
    $writer.Write([int16]0)
    $writer.Write([int16]1)
    $writer.Write([int16]$Frames.Count)

    $offset = 6 + 16 * $Frames.Count
    for ($i = 0; $i -lt $Frames.Count; $i++) {
        $dimension = if ($Frames[$i].Width -ge 256) { 0 } else { $Frames[$i].Width }
        $writer.Write([byte]$dimension)
        $writer.Write([byte]$dimension)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([int16]1)
        $writer.Write([int16]32)
        $writer.Write([int]$images[$i].Length)
        $writer.Write([int]$offset)
        $offset += $images[$i].Length
    }
    foreach ($image in $images) {
        $writer.Write($image)
    }

    $writer.Flush()
    [IO.File]::WriteAllBytes($Path, $stream.ToArray())
}

$workDir = Join-Path ([IO.Path]::GetTempPath()) ("ytmpresence-icons-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $workDir | Out-Null
$sheet = $null
$crops = @{}

try {
    # Alle Größen nebeneinander auf einer transparenten Seite, ein Screenshot.
    $svgMarkup = Get-Content -LiteralPath $SvgPath -Raw
    $smallSvgMarkup = Get-Content -LiteralPath $SmallSvgPath -Raw
    $left = 0
    $positions = @{}
    $tiles = foreach ($size in $sizes) {
        $positions[$size] = $left
        $markup = if ($size -le $smallSizeLimit) { $smallSvgMarkup } else { $svgMarkup }
        "<div style='position:absolute;left:${left}px;top:0;width:${size}px;height:${size}px'>$markup</div>"
        $left += $size + $gap
    }
    $statusTiles = foreach ($name in $statusIcons) {
        $positions[$name] = $left
        $markup = Get-Content -LiteralPath $statusSvgPaths[$name] -Raw
        "<div style='position:absolute;left:${left}px;top:0;width:1024px;height:1024px'>$markup</div>"
        $left += 1024 + $gap
    }
    $width = $left
    $height = ($sizes | Measure-Object -Maximum).Maximum

    $htmlPath = Join-Path $workDir "sheet.html"
    $html = "<!doctype html><html><head><meta charset='utf-8'><style>html,body{margin:0;background:transparent;overflow:hidden}svg{display:block;width:100%;height:100%}</style></head><body>$($tiles -join '')$($statusTiles -join '')</body></html>"
    Set-Content -LiteralPath $htmlPath -Value $html -Encoding utf8

    $screenshot = Join-Path $workDir "sheet.png"
    $arguments = @(
        "--headless=new", "--disable-gpu", "--hide-scrollbars", "--no-first-run",
        "--user-data-dir=`"$(Join-Path $workDir 'profile')`"",
        "--default-background-color=00000000", "--force-device-scale-factor=1",
        "--window-size=$width,$height", "--screenshot=`"$screenshot`"",
        ("file:///" + ($htmlPath -replace '\\', '/'))
    )
    Start-Process -FilePath $BrowserPath -ArgumentList $arguments -Wait -WindowStyle Hidden
    if (-not (Test-Path -LiteralPath $screenshot -PathType Leaf)) {
        throw "The browser did not write a screenshot: $screenshot"
    }

    $sheet = [System.Drawing.Bitmap]::new($screenshot)
    if ($sheet.Width -lt $width -or $sheet.Height -lt $height) {
        throw "Screenshot is $($sheet.Width)x$($sheet.Height), expected at least ${width}x${height}."
    }
    foreach ($size in $sizes) {
        $rect = [System.Drawing.Rectangle]::new($positions[$size], 0, $size, $size)
        $crops[$size] = $sheet.Clone($rect, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    }
    foreach ($name in $statusIcons) {
        $rect = [System.Drawing.Rectangle]::new($positions[$name], 0, 1024, 1024)
        $crops[$name] = $sheet.Clone($rect, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    }

    $brandingDir = Join-Path $repoRoot "branding"
    $crops[512].Save((Join-Path $brandingDir "ytmpresence-logo-512.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $crops[1024].Save((Join-Path $brandingDir "ytmpresence-logo-1024.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    foreach ($name in $statusIcons) {
        $crops[$name].Save((Join-Path $brandingDir "ytmpresence-$name-1024.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    }

    $iconDir = Join-Path $repoRoot "YTMPresence\extension\icons"
    foreach ($size in 16, 32, 48) {
        $crops[$size].Save((Join-Path $iconDir "icon$size.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    }

    $storeIcon = [System.Drawing.Bitmap]::new(128, 128, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($storeIcon)
        try {
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.DrawImageUnscaled($crops[96], 16, 16)
        }
        finally {
            $graphics.Dispose()
        }
        $storeIcon.Save((Join-Path $iconDir "icon128.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $storeIcon.Dispose()
    }

    $icoPath = Join-Path $repoRoot "YTMPresence.Tray.Wpf\Assets\app.ico"
    Write-Ico $icoPath @($crops[16], $crops[20], $crops[24], $crops[32], $crops[40], $crops[48], $crops[64], $crops[256])

    Write-Host "Icons written from $SvgPath (up to $smallSizeLimit px: $SmallSvgPath)"
}
finally {
    foreach ($crop in $crops.Values) { $crop.Dispose() }
    if ($sheet) { $sheet.Dispose() }
    Remove-Item -LiteralPath $workDir -Recurse -Force -ErrorAction SilentlyContinue
}
