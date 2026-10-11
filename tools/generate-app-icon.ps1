[CmdletBinding()]
param(
    [string]$Destination
)

$ErrorActionPreference = 'Stop'
$Destination = if ([string]::IsNullOrWhiteSpace($Destination)) {
    Join-Path $PSScriptRoot '..\assets\sprout-studio.ico'
} else {
    $Destination
}
Add-Type -AssemblyName System.Drawing

function New-Color([string]$Html) {
    return [System.Drawing.ColorTranslator]::FromHtml($Html)
}

# Draws the Sprout seedling mark. Coordinates come from assets/sprout-studio.svg and
# Views/Shared/SproutLogo.axaml, which share a 64-unit grid; $Unit is pixels per unit.
function Draw-Seedling(
    [System.Drawing.Graphics]$Graphics,
    [single]$OriginX,
    [single]$OriginY,
    [single]$Unit,
    [bool]$Detail) {
    $point = { param([single]$x, [single]$y) [System.Drawing.PointF]::new($OriginX + ($x * $Unit), $OriginY + ($y * $Unit)) }
    $rect = { param([single]$x, [single]$y, [single]$w, [single]$h) [System.Drawing.RectangleF]::new($OriginX + ($x * $Unit), $OriginY + ($y * $Unit), $w * $Unit, $h * $Unit) }

    $stem = [System.Drawing.Drawing2D.GraphicsPath]::new()
    try {
        $stem.AddBezier((& $point 31 60), (& $point 31 50), (& $point 30 42), (& $point 33 31))
        $stemBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new((& $rect 28 30 6 31), (New-Color '#3CC37A'), (New-Color '#1E8A4C'), 90.0)
        try {
            $stemPen = [System.Drawing.Pen]::new($stemBrush, [Math]::Max(1.0, 4.2 * $Unit))
            $stemPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
            $stemPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
            try { $Graphics.DrawPath($stemPen, $stem) } finally { $stemPen.Dispose() }
        }
        finally { $stemBrush.Dispose() }
    }
    finally { $stem.Dispose() }

    $leftLeaf = [System.Drawing.Drawing2D.GraphicsPath]::new()
    try {
        $leftLeaf.AddBezier((& $point 31.5 39), (& $point 30 28), (& $point 20 20), (& $point 6 21.5))
        $leftLeaf.AddBezier((& $point 6 21.5), (& $point 7 34), (& $point 17.5 41.5), (& $point 31.5 39))
        $leftLeaf.CloseFigure()
        $leftBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new((& $rect 6 20 26 22), (New-Color '#58D18A'), (New-Color '#1F9254'), 45.0)
        try { $Graphics.FillPath($leftBrush, $leftLeaf) } finally { $leftBrush.Dispose() }
    }
    finally { $leftLeaf.Dispose() }

    $rightLeaf = [System.Drawing.Drawing2D.GraphicsPath]::new()
    try {
        $rightLeaf.AddBezier((& $point 33 31), (& $point 33.5 15), (& $point 45 5), (& $point 60 4))
        $rightLeaf.AddBezier((& $point 60 4), (& $point 60.5 20), (& $point 50 32), (& $point 33 31))
        $rightLeaf.CloseFigure()
        $rightBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new((& $rect 33 4 28 28), (New-Color '#9AF2BD'), (New-Color '#2DB368'), 135.0)
        try { $Graphics.FillPath($rightBrush, $rightLeaf) } finally { $rightBrush.Dispose() }
    }
    finally { $rightLeaf.Dispose() }

    if ($Detail) {
        $veins = @(
            @{ Points = @(@(35, 29), @(41, 21), @(47, 14), @(55, 9)); Color = [System.Drawing.Color]::FromArgb(191, 217, 251, 230); Width = 1.6 },
            @{ Points = @(@(29.5, 37), @(24, 32), @(17, 27.5), @(11, 25.5)); Color = [System.Drawing.Color]::FromArgb(153, 195, 242, 212); Width = 1.4 })
        foreach ($vein in $veins) {
            $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
            try {
                $p = $vein.Points
                $path.AddBezier((& $point $p[0][0] $p[0][1]), (& $point $p[1][0] $p[1][1]), (& $point $p[2][0] $p[2][1]), (& $point $p[3][0] $p[3][1]))
                $pen = [System.Drawing.Pen]::new($vein.Color, [Math]::Max(0.75, $vein.Width * $Unit))
                $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
                $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
                try { $Graphics.DrawPath($pen, $path) } finally { $pen.Dispose() }
            }
            finally { $path.Dispose() }
        }
    }
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$frames = [System.Collections.Generic.List[byte[]]]::new()

foreach ($size in $sizes) {
    $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.Clear([System.Drawing.Color]::Transparent)

            $scale = $size / 256.0
            $surface = [System.Drawing.Drawing2D.GraphicsPath]::new()
            try {
                $radius = 52.0 * $scale
                $diameter = $radius * 2
                $bounds = [System.Drawing.RectangleF]::new(8 * $scale, 8 * $scale, 240 * $scale, 240 * $scale)
                $surface.AddArc($bounds.Left, $bounds.Top, $diameter, $diameter, 180, 90)
                $surface.AddArc($bounds.Right - $diameter, $bounds.Top, $diameter, $diameter, 270, 90)
                $surface.AddArc($bounds.Right - $diameter, $bounds.Bottom - $diameter, $diameter, $diameter, 0, 90)
                $surface.AddArc($bounds.Left, $bounds.Bottom - $diameter, $diameter, $diameter, 90, 90)
                $surface.CloseFigure()
                $surfaceBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new($bounds, (New-Color '#202621'), (New-Color '#101411'), 45.0)
                try { $graphics.FillPath($surfaceBrush, $surface) } finally { $surfaceBrush.Dispose() }
                if ($size -ge 32) {
                    $borderPen = [System.Drawing.Pen]::new((New-Color '#3b463e'), [Math]::Max(1.0, 7.0 * $scale))
                    try { $graphics.DrawPath($borderPen, $surface) } finally { $borderPen.Dispose() }
                }
            }
            finally { $surface.Dispose() }

            # Small frames enlarge the seedling slightly so it stays legible in the taskbar.
            if ($size -le 24) {
                Draw-Seedling $graphics (16 * $scale) (16 * $scale) (3.5 * $scale) $false
            } else {
                Draw-Seedling $graphics (26 * $scale) (26 * $scale) (3.2 * $scale) ($size -ge 48)
            }
        }
        finally { $graphics.Dispose() }

        $stream = [System.IO.MemoryStream]::new()
        try {
            $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
            $frames.Add($stream.ToArray())
        }
        finally { $stream.Dispose() }
    }
    finally { $bitmap.Dispose() }
}

$destinationPath = [System.IO.Path]::GetFullPath($Destination)
[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($destinationPath)) | Out-Null
$file = [System.IO.File]::Create($destinationPath)
try {
    $writer = [System.IO.BinaryWriter]::new($file)
    try {
        $writer.Write([UInt16]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]$frames.Count)
        $offset = 6 + (16 * $frames.Count)
        for ($index = 0; $index -lt $frames.Count; $index++) {
            $size = $sizes[$index]
            $writer.Write([byte]($(if ($size -eq 256) { 0 } else { $size })))
            $writer.Write([byte]($(if ($size -eq 256) { 0 } else { $size })))
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([UInt16]1)
            $writer.Write([UInt16]32)
            $writer.Write([UInt32]$frames[$index].Length)
            $writer.Write([UInt32]$offset)
            $offset += $frames[$index].Length
        }
        foreach ($frame in $frames) { $writer.Write($frame) }
    }
    finally { $writer.Dispose() }
}
finally { $file.Dispose() }

Write-Output $destinationPath

$welcomePath = Join-Path ([System.IO.Path]::GetDirectoryName($destinationPath)) 'sprout-installer-welcome.bmp'
$welcome = [System.Drawing.Bitmap]::new(164, 314, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
try {
    $graphics = [System.Drawing.Graphics]::FromImage($welcome)
    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
        $backgroundBounds = [System.Drawing.Rectangle]::new(0, 0, 164, 314)
        $background = [System.Drawing.Drawing2D.LinearGradientBrush]::new($backgroundBounds, (New-Color '#101411'), (New-Color '#1B4A2D'), 90.0)
        try { $graphics.FillRectangle($background, $backgroundBounds) } finally { $background.Dispose() }

        $glow = [System.Drawing.Drawing2D.GraphicsPath]::new()
        try {
            $glow.AddEllipse(12, 26, 140, 140)
            $glowBrush = [System.Drawing.Drawing2D.PathGradientBrush]::new($glow)
            try {
                $glowBrush.CenterColor = [System.Drawing.Color]::FromArgb(62, 73, 201, 121)
                $glowBrush.SurroundColors = @([System.Drawing.Color]::FromArgb(0, 73, 201, 121))
                $graphics.FillPath($glowBrush, $glow)
            }
            finally { $glowBrush.Dispose() }
        }
        finally { $glow.Dispose() }

        # 64 units x 1.75 = 112 px, centred horizontally above the wordmark.
        Draw-Seedling $graphics 26 40 1.75 $true

        $centered = [System.Drawing.StringFormat]::new()
        try {
            $centered.Alignment = [System.Drawing.StringAlignment]::Center
            $centered.LineAlignment = [System.Drawing.StringAlignment]::Near
            $titleFont = [System.Drawing.Font]::new('Segoe UI', 15, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
            $captionFont = [System.Drawing.Font]::new('Segoe UI', 11, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
            try {
                $graphics.DrawString('SPROUT STUDIO', $titleFont, [System.Drawing.Brushes]::White, [System.Drawing.RectangleF]::new(4, 194, 156, 26), $centered)
                $captionBrush = [System.Drawing.SolidBrush]::new((New-Color '#B8DCC4'))
                try {
                    $graphics.DrawString('Learn to code.', $captionFont, $captionBrush, [System.Drawing.RectangleF]::new(4, 224, 156, 22), $centered)
                    $graphics.DrawString('Build what you imagine.', $captionFont, $captionBrush, [System.Drawing.RectangleF]::new(4, 244, 156, 22), $centered)
                }
                finally { $captionBrush.Dispose() }
            }
            finally {
                $titleFont.Dispose()
                $captionFont.Dispose()
            }
        }
        finally { $centered.Dispose() }
    }
    finally { $graphics.Dispose() }
    $welcome.Save($welcomePath, [System.Drawing.Imaging.ImageFormat]::Bmp)
}
finally { $welcome.Dispose() }

Write-Output $welcomePath
