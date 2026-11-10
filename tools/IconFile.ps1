# Writes .ico files whose frames are PNGs; dot-sourced by the scripts that render the app's icons.

function ConvertTo-Png([System.Drawing.Bitmap]$Bitmap) {
    $ms = New-Object System.IO.MemoryStream
    $Bitmap.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    return , $ms.ToArray()
}

# Renders one bitmap per size with $Render and writes them as the frames of an .ico file.
function Write-IconFile([string]$Path, [int[]]$Sizes, [scriptblock]$Render) {
    $pngs = foreach ($size in $Sizes) {
        $bmp = & $Render $size
        ConvertTo-Png $bmp
        $bmp.Dispose()
    }

    $ico = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter $ico
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$Sizes.Count)
    $offset = 6 + 16 * $Sizes.Count
    for ($i = 0; $i -lt $Sizes.Count; $i++) {
        # The directory stores 256 px as 0.
        $dim = [byte]($Sizes[$i] % 256)
        $writer.Write($dim); $writer.Write($dim); $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$pngs[$i].Length); $writer.Write([uint32]$offset)
        $offset += $pngs[$i].Length
    }
    foreach ($png in $pngs) { $writer.Write($png) }
    [System.IO.File]::WriteAllBytes($Path, $ico.ToArray())
}
