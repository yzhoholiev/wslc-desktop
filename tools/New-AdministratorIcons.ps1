# Renders the elevated instance's icons (Assets\Administrator\*.ico): the app icon with a shield badge for the notification area, and the
# shield alone for the taskbar button's overlay, each in the runtime-state colors of the tray menu's dots.
param(
    [string]$AppIcon = "$PSScriptRoot\..\src\WslcDesktop.WinUI\Assets\AppIcon.png",
    [string]$AssetsDir = "$PSScriptRoot\..\src\WslcDesktop.WinUI\Assets\Administrator"
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
. "$PSScriptRoot\IconFile.ps1"

# The RunningDot, StoppedDot and ErrorDot colors in MainWindow.Tray.cs.
$states = [ordered]@{
    running = '#3FB950'
    stopped = '#8A8A8A'
    error = '#E5484D'
}

$outline = [System.Drawing.ColorTranslator]::FromHtml('#1A1A1A')
$source = [System.Drawing.Image]::FromFile((Resolve-Path $AppIcon))

function New-Canvas([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.InterpolationMode = 'HighQualityBicubic'
    $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)
    return $bmp, $g
}

# A shield on a 24-unit grid, scaled into the given square.
function Add-Shield($g, [float]$x, [float]$y, [float]$size, [string]$color) {
    $s = $size / 24
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddLine($x + 12 * $s, $y + 1.5 * $s, $x + 3.5 * $s, $y + 4.6 * $s)
    $path.AddLine($x + 3.5 * $s, $y + 4.6 * $s, $x + 3.5 * $s, $y + 10.8 * $s)
    $path.AddBezier($x + 3.5 * $s, $y + 10.8 * $s, $x + 3.5 * $s, $y + 16 * $s, $x + 7.1 * $s, $y + 20.7 * $s, $x + 12 * $s, $y + 22.5 * $s)
    $path.AddBezier($x + 12 * $s, $y + 22.5 * $s, $x + 16.9 * $s, $y + 20.7 * $s, $x + 20.5 * $s, $y + 16 * $s, $x + 20.5 * $s, $y + 10.8 * $s)
    $path.AddLine($x + 20.5 * $s, $y + 10.8 * $s, $x + 20.5 * $s, $y + 4.6 * $s)
    $path.CloseFigure()

    $fill = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml($color))
    $pen = New-Object System.Drawing.Pen $outline, ([Math]::Max(1.0, 1.8 * $s))
    $g.FillPath($fill, $path)
    $g.DrawPath($pen, $path)
}

function Render-Badged([int]$size, [string]$color) {
    $bmp, $g = New-Canvas $size
    $g.DrawImage($source, 0, 0, $size, $size)
    $badge = [Math]::Round($size * 0.62)
    Add-Shield $g ($size - $badge + 1) ($size - $badge + 1) $badge $color
    $g.Dispose()
    return $bmp
}

function Render-Shield([int]$size, [string]$color) {
    $bmp, $g = New-Canvas $size
    Add-Shield $g 0 0 $size $color
    $g.Dispose()
    return $bmp
}

New-Item -ItemType Directory -Force $AssetsDir | Out-Null
foreach ($state in $states.Keys) {
    $color = $states[$state]
    # The tray asks for the small-icon size at the primary display's scale, 16 px at 100 % up to 40 px at 250 %.
    Write-IconFile "$AssetsDir\tray-$state.ico" @(16, 20, 24, 32, 40) { param($size) Render-Badged $size $color }
    # Windows draws taskbar overlays at the small-icon size.
    Write-IconFile "$AssetsDir\overlay-$state.ico" @(16, 20, 24, 32) { param($size) Render-Shield $size $color }
}

$source.Dispose()
Get-ChildItem $AssetsDir -Filter *.ico | ForEach-Object { '{0} ({1:N1} KB)' -f $_.Name, ($_.Length / 1KB) }
