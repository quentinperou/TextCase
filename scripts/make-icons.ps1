<#
.SYNOPSIS
    Generates the application icon (.ico), its large in-app image, the README icon (.png), an SVG copy of the design
    (docs\icons) and the logos of the Store package (store\Assets).
.DESCRIPTION
    The generated files are committed: run this script again only to change the design.
    Design (chosen by the author among four proposals, October 22, 2026): a white "Aa" (Segoe UI Bold) on a rounded
    square with an emerald gradient (#34D399 to #047857).
    The small sizes are drawn for their pixels: 16 and 20 px (notification area, File Explorer lists) letter by letter on
    whole pixels, 24 px with the hinting of the font; from 30 px the outline of the 256 px design.
    Requires Windows PowerShell 5.1 (System.Drawing).
.PARAMETER Preview
    Also writes .local\maquettes\icone-finale.png: the small sizes enlarged, on a light and a dark taskbar.
#>
param([switch]$Preview)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot

$gradientStart = [System.Drawing.Color]::FromArgb(255, 0x34, 0xD3, 0x99)
$gradientEnd = [System.Drawing.Color]::FromArgb(255, 0x04, 0x78, 0x57)

# The 256 px design: "Aa" fitted in 172 x 112, centred a little under the middle (the A is taller than the a);
# background radius 23 % of its side, inside a 4 % transparent margin.
$design = @{ Text = 'Aa'; MaxW = 172; MaxH = 112; Cx = 128; Cy = 132; Bg = 0.23; Margin = 0.04 }

# The "a" of the small sizes, drawn pixel by pixel: 0 transparent to 9 white.
$smallA = @{
    16 = @('39981', '00199', '28999', '96099', '97299', '39989')
    20 = @('289960', '000299', '000099', '389999', '992199', '980399', '499989')
}
# Where the letters go at each small size: the A (its width, the cap height) from (X, Y), the a from (AX, Y + cap - its height).
$smallLayout = @{
    16 = @{ X = 1; Y = 4; W = 7; H = 8; Stroke = 2.0; Bar = @(5.0, 6.0); AX = 9 }
    20 = @{ X = 2; Y = 5; W = 9; H = 10; Stroke = 2.4; Bar = @(6.0, 7.3); AX = 12 }
}

function New-RoundedRectPath([System.Drawing.RectangleF]$r, [float]$radius) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    if ($radius -le 0) { $p.AddRectangle($r); return $p }
    $d = [Math]::Min($radius * 2, [Math]::Min($r.Width, $r.Height))
    $p.AddArc($r.X, $r.Y, $d, $d, 180, 90)
    $p.AddArc($r.Right - $d, $r.Y, $d, $d, 270, 90)
    $p.AddArc($r.Right - $d, $r.Bottom - $d, $d, $d, 0, 90)
    $p.AddArc($r.X, $r.Bottom - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

# The letters of the 256 px design as an outline, in the 256 design space.
function Get-LettersPath {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $ff = New-Object System.Drawing.FontFamily 'Segoe UI'
    $p.AddString($design.Text, $ff, [int][System.Drawing.FontStyle]::Bold, 200, (New-Object System.Drawing.PointF 0, 0), [System.Drawing.StringFormat]::GenericTypographic)
    $b = $p.GetBounds()
    $k = [Math]::Min($design.MaxW / $b.Width, $design.MaxH / $b.Height)
    $m = New-Object System.Drawing.Drawing2D.Matrix
    $m.Translate(-($b.X + $b.Width / 2), -($b.Y + $b.Height / 2), [System.Drawing.Drawing2D.MatrixOrder]::Append)
    $m.Scale($k, $k, [System.Drawing.Drawing2D.MatrixOrder]::Append)
    $m.Translate($design.Cx, $design.Cy, [System.Drawing.Drawing2D.MatrixOrder]::Append)
    $p.Transform($m)
    return $p
}

# Coverage (0..1) of one pixel of the small A: two legs of a given horizontal width meeting at a flat apex, and a
# crossbar; 4 x 4 samples per pixel.
function Get-ACoverage([float]$px, [float]$py, $spec) {
    $inside = 0
    for ($sy = 0; $sy -lt 4; $sy++) {
        for ($sx = 0; $sx -lt 4; $sx++) {
            $x = $px + ($sx + 0.5) / 4
            $y = $py + ($sy + 0.5) / 4
            $apex = ($spec.W - $spec.Stroke) / 2
            $left = $apex * (1 - $y / $spec.H)
            $right = $spec.W - $left
            $inLeg = ($x -ge $left -and $x -le $left + $spec.Stroke) -or ($x -ge $right - $spec.Stroke -and $x -le $right)
            $inBar = $y -ge $spec.Bar[0] -and $y -le $spec.Bar[1] -and $x -ge $left -and $x -le $right
            if ($inLeg -or $inBar) { $inside++ }
        }
    }
    return $inside / 16
}

function New-IconBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    # Under 48 px no transparent margin, as in the sister apps.
    if ($size -lt 48) { $m = [float]0 } else { $m = [float]($size * $design.Margin) }
    $s = [float]($size - 2 * $m)
    $rect = New-Object System.Drawing.RectangleF $m, $m, $s, $s
    $bg = New-RoundedRectPath $rect ([float]([Math]::Round($s * $design.Bg * 2) / 2))
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, $gradientStart, $gradientEnd, ([float]45)
    $g.FillPath($brush, $bg)

    if ($smallLayout.ContainsKey($size)) {
        $g.Dispose()
        $spec = $smallLayout[$size]
        for ($y = 0; $y -lt $spec.H; $y++) {
            for ($x = 0; $x -lt $spec.W; $x++) {
                Add-White $bmp ($spec.X + $x) ($spec.Y + $y) (Get-ACoverage $x $y $spec)
            }
        }
        $rows = $smallA[$size]
        $top = $spec.Y + $spec.H - $rows.Count
        for ($y = 0; $y -lt $rows.Count; $y++) {
            for ($x = 0; $x -lt $rows[$y].Length; $x++) {
                Add-White $bmp ($spec.AX + $x) ($top + $y) ([int][string]$rows[$y][$x] / 9)
            }
        }
    }
    elseif ($size -lt 30) {
        # The font's hinting puts the stems on whole pixels.
        $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
        $font = New-Object System.Drawing.Font 'Segoe UI', ([float]($size * 0.62)), ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
        $format = New-Object System.Drawing.StringFormat
        $format.Alignment = [System.Drawing.StringAlignment]::Center
        $format.LineAlignment = [System.Drawing.StringAlignment]::Center
        $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
        $g.DrawString($design.Text, $font, $white, (New-Object System.Drawing.RectangleF 0, 0, $size, $size), $format)
        $white.Dispose(); $font.Dispose(); $g.Dispose()
    }
    else {
        $letters = Get-LettersPath
        $mx = New-Object System.Drawing.Drawing2D.Matrix
        $mx.Scale($s / 256, $s / 256, [System.Drawing.Drawing2D.MatrixOrder]::Append)
        $mx.Translate($m, $m, [System.Drawing.Drawing2D.MatrixOrder]::Append)
        $letters.Transform($mx)
        $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
        $g.FillPath($white, $letters)
        $white.Dispose(); $letters.Dispose(); $g.Dispose()
    }
    $brush.Dispose(); $bg.Dispose()
    return $bmp
}

# White over the pixel, by its coverage.
function Add-White([System.Drawing.Bitmap]$bmp, [int]$x, [int]$y, [double]$coverage) {
    if ($coverage -le 0) { return }
    $c = $bmp.GetPixel($x, $y)
    $k = [Math]::Min(1.0, $coverage)
    $r = [int]($c.R + (255 - $c.R) * $k); $gr = [int]($c.G + (255 - $c.G) * $k); $b = [int]($c.B + (255 - $c.B) * $k)
    $bmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($c.A, $r, $gr, $b))
}

# ---------------------------------------------------------------- Files

function Get-PngBytes([System.Drawing.Bitmap]$bmp) {
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    return $ms.ToArray()
}

function Save-Png([int]$size, [string]$path) {
    New-Item -ItemType Directory -Force (Split-Path -Parent $path) | Out-Null
    $bmp = New-IconBitmap $size
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "  $path"
}

# ICO file made of PNG-compressed entries (supported since Windows Vista).
function Save-Ico([int[]]$sizes, [string]$path) {
    New-Item -ItemType Directory -Force (Split-Path -Parent $path) | Out-Null
    $images = foreach ($size in $sizes) { $b = New-IconBitmap $size; , (Get-PngBytes $b); $b.Dispose() }
    $fs = [System.IO.File]::Create($path)
    $bw = New-Object System.IO.BinaryWriter $fs
    $bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
        $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
        $bw.Write([UInt16]1); $bw.Write([UInt16]32)
        $bw.Write([UInt32]$images[$i].Length); $bw.Write([UInt32]$offset)
        $offset += $images[$i].Length
    }
    foreach ($img in $images) { $bw.Write([byte[]]$img) }
    $bw.Dispose()
    Write-Host "  $path"
}

# The 256 px design as SVG (docs\icons), to rework the icon in any vector editor: the letters as their outline.
function Get-Hex([System.Drawing.Color]$c) { '#' + $c.R.ToString('X2') + $c.G.ToString('X2') + $c.B.ToString('X2') }

function Get-PathData([System.Drawing.Drawing2D.GraphicsPath]$path) {
    $inv = [System.Globalization.CultureInfo]::InvariantCulture
    $points = $path.PathPoints; $types = $path.PathTypes
    $d = New-Object System.Text.StringBuilder
    $i = 0
    while ($i -lt $points.Length) {
        $t = $types[$i] -band 0x07
        $p = $points[$i]
        if ($t -eq 0) { [void]$d.Append(('M{0:0.##} {1:0.##} ' -f $p.X, $p.Y).Replace(',', '.')); $i++ }
        elseif ($t -eq 1) { [void]$d.Append(('L{0:0.##} {1:0.##} ' -f $p.X, $p.Y).Replace(',', '.')); $i++ }
        else {
            $a = $points[$i]; $b = $points[$i + 1]; $c = $points[$i + 2]
            [void]$d.Append([string]::Format($inv, 'C{0:0.##} {1:0.##} {2:0.##} {3:0.##} {4:0.##} {5:0.##} ', $a.X, $a.Y, $b.X, $b.Y, $c.X, $c.Y))
            $i += 2
            $t = $types[$i]; $i++
            if ($t -band 0x80) { [void]$d.Append('Z ') }
            continue
        }
        if ($types[$i - 1] -band 0x80) { [void]$d.Append('Z ') }
    }
    return $d.ToString().Trim()
}

function Get-AppSvg {
    $m = 256 * $design.Margin
    $k = (256 - 2 * $m) / 256
    $letters = Get-LettersPath
    $data = Get-PathData $letters
    $letters.Dispose()
    @"
<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256">
  <!-- TextCase app icon, 256 px design. Written by scripts/make-icons.ps1: change the design there. -->
  <defs>
    <linearGradient id="gradient" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0" stop-color="$(Get-Hex $gradientStart)" />
      <stop offset="1" stop-color="$(Get-Hex $gradientEnd)" />
    </linearGradient>
  </defs>
  <g transform="translate($m $m) scale($k)">
    <rect width="256" height="256" rx="$(256 * $design.Bg)" fill="url(#gradient)" />
    <path fill="#FFFFFF" fill-rule="nonzero" d="$data" />
  </g>
</svg>
"@
}

function Save-Svg([string]$path, [string]$svg) {
    New-Item -ItemType Directory -Force (Split-Path -Parent $path) | Out-Null
    [System.IO.File]::WriteAllText($path, $svg.Replace("`r`n", "`n") + "`n", (New-Object System.Text.UTF8Encoding $false))
    Write-Host "  $path"
}

# The small sizes enlarged (nearest neighbour) and at their size on a light and a dark taskbar.
function Save-Preview([string]$path) {
    $sizes = @(16, 20, 24, 32, 48)
    $canvas = New-Object System.Drawing.Bitmap (60 + 260 * $sizes.Count), 420
    $g = [System.Drawing.Graphics]::FromImage($canvas)
    $g.Clear([System.Drawing.Color]::FromArgb(250, 250, 250))
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $light = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(238, 238, 238))
    $dark = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(31, 31, 31))
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $bmp = New-IconBitmap $sizes[$i]
        $x = 30 + $i * 260
        $g.DrawImage($bmp, $x, 20, 240, 240)
        $g.FillRectangle($light, $x, 280, 240, 56)
        $g.FillRectangle($dark, $x, 346, 240, 56)
        $g.DrawImageUnscaled($bmp, [int]($x + 120 - $sizes[$i] / 2), [int](308 - $sizes[$i] / 2))
        $g.DrawImageUnscaled($bmp, [int]($x + 120 - $sizes[$i] / 2), [int](374 - $sizes[$i] / 2))
        $bmp.Dispose()
    }
    $canvas.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $canvas.Dispose()
    Write-Host "  $path"
}

Write-Host 'Generated icons:'
Save-Ico @(16, 20, 24, 30, 32, 36, 40, 48, 64, 256) (Join-Path $root 'src\TextCase\Assets\TextCase.ico')
Save-Png 256 (Join-Path $root 'docs\icon-256.png')
# Large image shown inside the app (About card, Settings title): an .ico would display its first, 16 px, frame.
Save-Png 256 (Join-Path $root 'src\TextCase\Assets\TextCase.png')
Save-Svg (Join-Path $root 'docs\icons\TextCase.svg') (Get-AppSvg)
# Logos of the Store package (store\AppxManifest.xml, scripts\build-store.ps1). Windows picks the size it needs: a
# scale for the Start menu and the Store, a target size for the taskbar and File Explorer ("unplated": the icon as it
# is, on no colored plate).
$store = Join-Path $root 'store\Assets'
New-Item -ItemType Directory -Force $store | Out-Null
foreach ($scale in 100, 200) {
    Save-Png (50 * $scale / 100) (Join-Path $store "StoreLogo.scale-$scale.png")
    Save-Png (44 * $scale / 100) (Join-Path $store "Square44x44Logo.scale-$scale.png")
    Save-Png (150 * $scale / 100) (Join-Path $store "Square150x150Logo.scale-$scale.png")
}
foreach ($size in 16, 20, 24, 30, 32, 36, 40, 48, 64, 256) {
    Save-Png $size (Join-Path $store "Square44x44Logo.targetsize-$size.png")
    Save-Png $size (Join-Path $store "Square44x44Logo.targetsize-$($size)_altform-unplated.png")
}
if ($Preview) { Save-Preview (Join-Path $root '.local\maquettes\icone-finale.png') }
