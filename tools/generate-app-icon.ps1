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

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$frames = [System.Collections.Generic.List[byte[]]]::new()

foreach ($size in $sizes) {
    $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
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
                $surfaceBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
                    $bounds,
                    [System.Drawing.ColorTranslator]::FromHtml('#202621'),
                    [System.Drawing.ColorTranslator]::FromHtml('#101411'),
                    45.0)
                try { $graphics.FillPath($surfaceBrush, $surface) } finally { $surfaceBrush.Dispose() }
                if ($size -ge 32) {
                    $borderPen = [System.Drawing.Pen]::new([System.Drawing.ColorTranslator]::FromHtml('#3b463e'), [Math]::Max(1.0, 7.0 * $scale))
                    try { $graphics.DrawPath($borderPen, $surface) } finally { $borderPen.Dispose() }
                }
            }
            finally { $surface.Dispose() }

            $leaf = [System.Drawing.Drawing2D.GraphicsPath]::new()
            try {
                $leaf.StartFigure()
                $leaf.AddBezier(190 * $scale, 49 * $scale, 112 * $scale, 54 * $scale, 64 * $scale, 95 * $scale, 64 * $scale, 154 * $scale)
                $leaf.AddBezier(64 * $scale, 154 * $scale, 64 * $scale, 179 * $scale, 74 * $scale, 196 * $scale, 93 * $scale, 207 * $scale)
                $leaf.AddBezier(93 * $scale, 207 * $scale, 95 * $scale, 159 * $scale, 117 * $scale, 120 * $scale, 160 * $scale, 89 * $scale)
                $leaf.AddBezier(160 * $scale, 89 * $scale, 129 * $scale, 120 * $scale, 110 * $scale, 158 * $scale, 103 * $scale, 205 * $scale)
                $leaf.AddBezier(103 * $scale, 205 * $scale, 160 * $scale, 200 * $scale, 194 * $scale, 161 * $scale, 194 * $scale, 106 * $scale)
                $leaf.AddBezier(194 * $scale, 106 * $scale, 194 * $scale, 84 * $scale, 192 * $scale, 65 * $scale, 190 * $scale, 49 * $scale)
                $leaf.CloseFigure()
                $leafBounds = [System.Drawing.RectangleF]::new(60 * $scale, 45 * $scale, 140 * $scale, 166 * $scale)
                $leafBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
                    $leafBounds,
                    [System.Drawing.ColorTranslator]::FromHtml('#67df94'),
                    [System.Drawing.ColorTranslator]::FromHtml('#34b96b'),
                    45.0)
                try { $graphics.FillPath($leafBrush, $leaf) } finally { $leafBrush.Dispose() }
            }
            finally { $leaf.Dispose() }

            if ($size -ge 24) {
                $stem = [System.Drawing.Drawing2D.GraphicsPath]::new()
                try {
                    $stem.AddBezier(73 * $scale, 213 * $scale, 89 * $scale, 159 * $scale, 118 * $scale, 114 * $scale, 164 * $scale, 81 * $scale)
                    $stemPen = [System.Drawing.Pen]::new([System.Drawing.ColorTranslator]::FromHtml('#9cf0b9'), [Math]::Max(1.25, 9.0 * $scale))
                    $stemPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
                    $stemPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
                    try { $graphics.DrawPath($stemPen, $stem) } finally { $stemPen.Dispose() }
                }
                finally { $stem.Dispose() }
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
        $backgroundBounds = [System.Drawing.Rectangle]::new(0, 0, 164, 314)
        $background = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
            $backgroundBounds,
            [System.Drawing.ColorTranslator]::FromHtml('#101411'),
            [System.Drawing.ColorTranslator]::FromHtml('#1B4A2D'),
            90.0)
        try { $graphics.FillRectangle($background, $backgroundBounds) } finally { $background.Dispose() }

        $glow = [System.Drawing.Drawing2D.GraphicsPath]::new()
        try {
            $glow.AddEllipse(12, 22, 140, 140)
            $glowBrush = [System.Drawing.Drawing2D.PathGradientBrush]::new($glow)
            try {
                $glowBrush.CenterColor = [System.Drawing.Color]::FromArgb(62, 73, 201, 121)
                $glowBrush.SurroundColors = @([System.Drawing.Color]::FromArgb(0, 73, 201, 121))
                $graphics.FillPath($glowBrush, $glow)
            }
            finally { $glowBrush.Dispose() }
        }
        finally { $glow.Dispose() }

        $leaf = [System.Drawing.Drawing2D.GraphicsPath]::new()
        try {
            $leaf.StartFigure()
            $leaf.AddBezier(126, 43, 78, 46, 48, 72, 48, 108)
            $leaf.AddBezier(48, 108, 48, 124, 55, 136, 67, 143)
            $leaf.AddBezier(67, 143, 69, 113, 82, 88, 109, 69)
            $leaf.AddBezier(109, 69, 90, 88, 78, 113, 74, 142)
            $leaf.AddBezier(74, 142, 111, 139, 130, 115, 130, 80)
            $leaf.AddBezier(130, 80, 130, 66, 128, 53, 126, 43)
            $leaf.CloseFigure()
            $leafBounds = [System.Drawing.RectangleF]::new(44, 40, 90, 106)
            $leafBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
                $leafBounds,
                [System.Drawing.ColorTranslator]::FromHtml('#78E89F'),
                [System.Drawing.ColorTranslator]::FromHtml('#35B969'),
                45.0)
            try { $graphics.FillPath($leafBrush, $leaf) } finally { $leafBrush.Dispose() }
        }
        finally { $leaf.Dispose() }

        $stem = [System.Drawing.Drawing2D.GraphicsPath]::new()
        try {
            $stem.AddBezier(54, 151, 65, 118, 84, 88, 112, 66)
            $stemPen = [System.Drawing.Pen]::new([System.Drawing.ColorTranslator]::FromHtml('#B1F4C8'), 5)
            $stemPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
            $stemPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
            try { $graphics.DrawPath($stemPen, $stem) } finally { $stemPen.Dispose() }
        }
        finally { $stem.Dispose() }

        $centered = [System.Drawing.StringFormat]::new()
        try {
            $centered.Alignment = [System.Drawing.StringAlignment]::Center
            $centered.LineAlignment = [System.Drawing.StringAlignment]::Near
            $titleFont = [System.Drawing.Font]::new('Segoe UI', 15, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
            $captionFont = [System.Drawing.Font]::new('Segoe UI', 11, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
            try {
                $graphics.DrawString('SPROUT STUDIO', $titleFont, [System.Drawing.Brushes]::White, [System.Drawing.RectangleF]::new(4, 194, 156, 26), $centered)
                $captionBrush = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#B8DCC4'))
                try {
                    $graphics.DrawString('Grow your ideas.', $captionFont, $captionBrush, [System.Drawing.RectangleF]::new(4, 224, 156, 22), $centered)
                    $graphics.DrawString('Learn. Build. Create.', $captionFont, $captionBrush, [System.Drawing.RectangleF]::new(4, 244, 156, 22), $centered)
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
