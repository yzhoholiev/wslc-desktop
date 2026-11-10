# Renders Assets\AppIcon.ico (multi-size) and Assets\AppIcon.png: the WSL penguin peeking over a shipping container, drawn on a 256-unit canvas.
param([string]$AssetsDir = "$PSScriptRoot\..\src\WslcDesktop.WinUI\Assets")

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
. "$PSScriptRoot\IconFile.ps1"

$blue = '#1E73D2'; $yellow = '#F7C21B'; $yellowDark = '#D9A100'; $orange = '#F58A1F'; $black = '#141414'

function Brush([string]$hex) { New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml($hex)) }

function New-Head([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($x, $y, $w, $w, 180, 180)
    $path.AddArc($x + $w - 2 * $r, $y + $h - 2 * $r, 2 * $r, 2 * $r, 0, 90)
    $path.AddArc($x, $y + $h - 2 * $r, 2 * $r, 2 * $r, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-Face {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.FillMode = 'Winding'
    $path.AddEllipse(48, 40, 104, 138)
    $path.AddEllipse(104, 40, 104, 138)
    $path.AddEllipse(50, 98, 156, 96)
    return $path
}

function Render([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.ScaleTransform($size / 256.0, $size / 256.0)
    $detailed = $size -ge 40

    $head = New-Head 54 14 148 200 30
    $g.FillPath((Brush $blue), $head)
    $state = $g.Save()
    $g.SetClip($head, [System.Drawing.Drawing2D.CombineMode]::Intersect)
    # The face is drawn at the WSL logo's proportions and scaled to the narrower head.
    $g.TranslateTransform(128, 0); $g.ScaleTransform(148 / 176.0, 148 / 176.0); $g.TranslateTransform(-128, 0)
    $g.FillPath((Brush '#FFFFFF'), (New-Face))
    $g.FillEllipse((Brush $black), 76, 80, 38, 42)
    $g.FillEllipse((Brush $black), 142, 80, 38, 42)
    if ($detailed) {
        $g.FillEllipse((Brush '#FFFFFF'), 96, 87, 11, 11)
        $g.FillEllipse((Brush '#FFFFFF'), 162, 87, 11, 11)
    }
    $g.FillEllipse((Brush $orange), 100, 116, 56, 50)
    $g.Restore($state)

    $g.FillRectangle((Brush $yellow), 16, 150, 224, 92)
    $g.FillRectangle((Brush $yellowDark), 16, 150, 224, 12)
    $g.FillRectangle((Brush $yellowDark), 16, 230, 224, 12)
    $g.FillRectangle((Brush $yellowDark), 16, 150, 12, 92)
    $g.FillRectangle((Brush $yellowDark), 228, 150, 12, 92)
    $ridge = New-Object System.Drawing.Pen ([System.Drawing.ColorTranslator]::FromHtml($yellowDark)), ($(if ($detailed) { 6 } else { 10 }))
    $ridge.StartCap = 'Round'; $ridge.EndCap = 'Round'
    for ($i = 1; $i -le 7; $i++) {
        $x = 28 + 200 * $i / 8
        $g.DrawLine($ridge, $x, 174, $x, 220)
    }

    $g.FillEllipse((Brush $black), 44, 136, 46, 26)
    $g.FillEllipse((Brush $black), 166, 136, 46, 26)

    $g.Dispose()
    return $bmp
}

Write-IconFile "$AssetsDir\AppIcon.ico" @(16, 20, 24, 32, 40, 48, 64, 256) { param($size) Render $size }
$png = Render 256
$png.Save("$AssetsDir\AppIcon.png", [System.Drawing.Imaging.ImageFormat]::Png)
$png.Dispose()
Write-Host "Wrote $AssetsDir\AppIcon.ico and AppIcon.png"
