# Renders the jump list's task icons (Assets\JumpList\*.ico) from the Segoe Fluent Icons glyphs the app uses for the same pages and actions.
param([string]$AssetsDir = "$PSScriptRoot\..\src\WslcDesktop.WinUI\Assets\JumpList")

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
. "$PSScriptRoot\IconFile.ps1"

# The app icon's container amber, readable on both the light and the dark jump list.
$color = [System.Drawing.ColorTranslator]::FromHtml('#D9A100')

$icons = [ordered]@{
    containers = 0xE7B8
    images = 0xE8B9
    volumes = 0xEDA2
    wsl = 0xE756
    start = 0xE768
    stop = 0xE7E8
    cleanup = 0xEA99
}

function Render([int]$glyph, [int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.TextRenderingHint = 'AntiAliasGridFit'
    $g.Clear([System.Drawing.Color]::Transparent)
    $font = New-Object System.Drawing.Font 'Segoe Fluent Icons', ($size * 0.8), ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
    $format = New-Object System.Drawing.StringFormat
    $format.Alignment = 'Center'; $format.LineAlignment = 'Center'
    $brush = New-Object System.Drawing.SolidBrush $color
    $g.DrawString([string][char]$glyph, $font, $brush, (New-Object System.Drawing.RectangleF 0, 0, $size, $size), $format)
    $g.Dispose()
    return $bmp
}

if ('Segoe Fluent Icons' -notin (New-Object System.Drawing.Text.InstalledFontCollection).Families.Name) {
    throw 'Segoe Fluent Icons is not installed; it ships with Windows 11.'
}

New-Item -ItemType Directory -Force $AssetsDir | Out-Null
$sizes = 16, 20, 24, 32, 48
foreach ($name in $icons.Keys) {
    Write-IconFile "$AssetsDir\$name.ico" $sizes { param($size) Render $icons[$name] $size }
}

Write-Host "Wrote $($icons.Count) icons to $AssetsDir"
