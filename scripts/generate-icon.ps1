$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$assetDir = Join-Path $projectRoot 'assets'
$icoPath = Join-Path $assetDir 'pizarra-workflows.ico'
Add-Type -AssemblyName PresentationCore, WindowsBase

function New-Brush([string] $hex) {
    $color = [System.Windows.Media.ColorConverter]::ConvertFromString($hex)
    $brush = [System.Windows.Media.SolidColorBrush]::new($color)
    $brush.Freeze()
    return $brush
}

function New-IconPng([int] $size) {
    $visual = [System.Windows.Media.DrawingVisual]::new()
    $context = $visual.RenderOpen()
    $context.PushTransform([System.Windows.Media.ScaleTransform]::new($size / 256.0, $size / 256.0))

    $context.DrawRoundedRectangle((New-Brush '#0B1020'), $null, [System.Windows.Rect]::new(8, 8, 240, 240), 48, 48)
    $context.DrawRoundedRectangle((New-Brush '#F8D871'), $null, [System.Windows.Rect]::new(43, 30, 154, 186), 14, 14)

    $fold = [System.Windows.Media.StreamGeometry]::new()
    $geometryContext = $fold.Open()
    $geometryContext.BeginFigure([System.Windows.Point]::new(163, 30), $true, $true)
    $geometryContext.LineTo([System.Windows.Point]::new(197, 30), $true, $false)
    $geometryContext.LineTo([System.Windows.Point]::new(197, 69), $true, $false)
    $geometryContext.LineTo([System.Windows.Point]::new(163, 69), $true, $false)
    $geometryContext.Close()
    $fold.Freeze()
    $context.DrawGeometry((New-Brush '#E6BF56'), $null, $fold)

    $flowPen = [System.Windows.Media.Pen]::new((New-Brush '#6675E8'), 10)
    $flowPen.StartLineCap = [System.Windows.Media.PenLineCap]::Round
    $flowPen.EndLineCap = [System.Windows.Media.PenLineCap]::Round
    $flowPen.LineJoin = [System.Windows.Media.PenLineJoin]::Round
    $context.DrawLine($flowPen, [System.Windows.Point]::new(73,108), [System.Windows.Point]::new(111,108))
    $context.DrawLine($flowPen, [System.Windows.Point]::new(111,108), [System.Windows.Point]::new(111,150))
    $context.DrawLine($flowPen, [System.Windows.Point]::new(111,150), [System.Windows.Point]::new(151,150))
    foreach ($point in @(@(73,108), @(111,150), @(151,150))) {
        $context.DrawEllipse((New-Brush '#0B1020'), [System.Windows.Media.Pen]::new((New-Brush '#AAB4FF'), 8), [System.Windows.Point]::new($point[0], $point[1]), 12, 12)
    }

    $pencil = [System.Windows.Media.StreamGeometry]::new()
    $geometryContext = $pencil.Open()
    $geometryContext.BeginFigure([System.Windows.Point]::new(130,194), $true, $true)
    $geometryContext.LineTo([System.Windows.Point]::new(186,138), $true, $false)
    $geometryContext.LineTo([System.Windows.Point]::new(205,157), $true, $false)
    $geometryContext.LineTo([System.Windows.Point]::new(149,213), $true, $false)
    $geometryContext.LineTo([System.Windows.Point]::new(124,219), $true, $false)
    $geometryContext.Close()
    $pencil.Freeze()
    $context.DrawGeometry((New-Brush '#AAB4FF'), [System.Windows.Media.Pen]::new((New-Brush '#0B1020'), 7), $pencil)

    $tip = [System.Windows.Media.StreamGeometry]::new()
    $geometryContext = $tip.Open()
    $geometryContext.BeginFigure([System.Windows.Point]::new(124,219), $true, $true)
    $geometryContext.LineTo([System.Windows.Point]::new(130,194), $true, $false)
    $geometryContext.LineTo([System.Windows.Point]::new(149,213), $true, $false)
    $geometryContext.Close()
    $tip.Freeze()
    $context.DrawGeometry((New-Brush '#F8D871'), [System.Windows.Media.Pen]::new((New-Brush '#0B1020'), 4), $tip)

    $context.Pop()
    $context.Close()
    $bitmap = [System.Windows.Media.Imaging.RenderTargetBitmap]::new($size, $size, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = [System.Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [System.IO.MemoryStream]::new()
    $encoder.Save($stream)
    $bytes = $stream.ToArray()
    $stream.Dispose()
    return ,$bytes
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$images = foreach ($size in $sizes) { New-IconPng $size }
$file = [System.IO.File]::Open($icoPath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
$writer = [System.IO.BinaryWriter]::new($file)
try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $dimension = if ($sizes[$i] -eq 256) { [byte]0 } else { [byte]$sizes[$i] }
        $writer.Write($dimension)
        $writer.Write($dimension)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$images[$i].Length)
        $writer.Write([uint32]$offset)
        $offset += $images[$i].Length
    }
    foreach ($image in $images) { $writer.Write($image) }
}
finally {
    $writer.Dispose()
    $file.Dispose()
}
Write-Host "Icono creado: $icoPath"
