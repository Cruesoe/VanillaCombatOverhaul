param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\1.6\Textures\UI\Commands')
)

Add-Type -AssemblyName System.Drawing

$Scale = 4
$Cream = [System.Drawing.Color]::FromArgb(255, 237, 229, 216)
$Coral = [System.Drawing.Color]::FromArgb(255, 216, 81, 59)
$Charcoal = [System.Drawing.Color]::FromArgb(255, 5, 5, 4)

function New-Pen([System.Drawing.Color]$Color, [float]$Width) {
    $pen = [System.Drawing.Pen]::new($Color, $Width)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    return $pen
}

function Draw-OutlinedPath($Graphics, $Path, [System.Drawing.Color]$Fill) {
    $brush = [System.Drawing.SolidBrush]::new($Fill)
    $outline = New-Pen $Charcoal 5
    $Graphics.FillPath($brush, $Path)
    $Graphics.DrawPath($outline, $Path)
    $brush.Dispose()
    $outline.Dispose()
}

function New-PawnPath {
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $path.AddEllipse(49, 14, 30, 30)
    $body = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(57, 43),
        [System.Drawing.PointF]::new(47, 48),
        [System.Drawing.PointF]::new(41, 59),
        [System.Drawing.PointF]::new(41, 72),
        [System.Drawing.PointF]::new(47, 85),
        [System.Drawing.PointF]::new(44, 112),
        [System.Drawing.PointF]::new(59, 112),
        [System.Drawing.PointF]::new(64, 95),
        [System.Drawing.PointF]::new(69, 112),
        [System.Drawing.PointF]::new(84, 112),
        [System.Drawing.PointF]::new(81, 85),
        [System.Drawing.PointF]::new(87, 72),
        [System.Drawing.PointF]::new(87, 59),
        [System.Drawing.PointF]::new(81, 48),
        [System.Drawing.PointF]::new(71, 43)
    )
    $path.AddClosedCurve($body, 0.18)
    return $path
}

function Draw-Pawn($Graphics) {
    $path = New-PawnPath
    Draw-OutlinedPath $Graphics $path $Cream
    $path.Dispose()
}

function Draw-Reticle($Graphics, [float]$CenterX, [float]$CenterY, [float]$Radius, [bool]$CenterDot = $true) {
    $dark = New-Pen $Charcoal 10
    $coralPen = New-Pen $Coral 5.5
    foreach ($pen in @($dark, $coralPen)) {
        $Graphics.DrawEllipse($pen, $CenterX - $Radius, $CenterY - $Radius, $Radius * 2, $Radius * 2)
        $Graphics.DrawLine($pen, $CenterX, $CenterY - $Radius - 9, $CenterX, $CenterY - $Radius + 7)
        $Graphics.DrawLine($pen, $CenterX, $CenterY + $Radius - 7, $CenterX, $CenterY + $Radius + 9)
        $Graphics.DrawLine($pen, $CenterX - $Radius - 9, $CenterY, $CenterX - $Radius + 7, $CenterY)
        $Graphics.DrawLine($pen, $CenterX + $Radius - 7, $CenterY, $CenterX + $Radius + 9, $CenterY)
    }
    if ($CenterDot) {
        $darkBrush = [System.Drawing.SolidBrush]::new($Charcoal)
        $coralBrush = [System.Drawing.SolidBrush]::new($Coral)
        $Graphics.FillEllipse($darkBrush, $CenterX - 9, $CenterY - 9, 18, 18)
        $Graphics.FillEllipse($coralBrush, $CenterX - 5.5, $CenterY - 5.5, 11, 11)
        $darkBrush.Dispose()
        $coralBrush.Dispose()
    }
    $dark.Dispose()
    $coralPen.Dispose()
}

function Draw-Brackets($Graphics, [float]$Inset = 18) {
    $dark = New-Pen $Charcoal 9
    $cream = New-Pen $Cream 5
    $short = 15
    $corners = [System.Drawing.PointF[][]]@(
        [System.Drawing.PointF[]]@([System.Drawing.PointF]::new($Inset + $short, $Inset), [System.Drawing.PointF]::new($Inset, $Inset), [System.Drawing.PointF]::new($Inset, $Inset + $short)),
        [System.Drawing.PointF[]]@([System.Drawing.PointF]::new(128 - $Inset - $short, $Inset), [System.Drawing.PointF]::new(128 - $Inset, $Inset), [System.Drawing.PointF]::new(128 - $Inset, $Inset + $short)),
        [System.Drawing.PointF[]]@([System.Drawing.PointF]::new($Inset, 128 - $Inset - $short), [System.Drawing.PointF]::new($Inset, 128 - $Inset), [System.Drawing.PointF]::new($Inset + $short, 128 - $Inset)),
        [System.Drawing.PointF[]]@([System.Drawing.PointF]::new(128 - $Inset - $short, 128 - $Inset), [System.Drawing.PointF]::new(128 - $Inset, 128 - $Inset), [System.Drawing.PointF]::new(128 - $Inset, 128 - $Inset - $short))
    )
    foreach ($corner in $corners) {
        foreach ($pen in @($dark, $cream)) {
            $Graphics.DrawLines($pen, $corner)
        }
    }
    $dark.Dispose()
    $cream.Dispose()
}

function New-CartridgePath([float]$X, [float]$Y, [float]$Width, [float]$Height) {
    $radius = $Width / 2
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $path.AddArc($X, $Y, $Width, $Width, 180, 180)
    $path.AddLine($X + $Width, $Y + $radius, $X + $Width, $Y + $Height)
    $path.AddLine($X + $Width, $Y + $Height, $X, $Y + $Height)
    $path.AddLine($X, $Y + $Height, $X, $Y + $radius)
    $path.CloseFigure()
    return $path
}

function Draw-Cartridge($Graphics, [float]$X, [float]$Y, [float]$Width, [float]$Height) {
    $path = New-CartridgePath $X $Y $Width $Height
    Draw-OutlinedPath $Graphics $path $Cream
    $seam = New-Pen $Charcoal 3.5
    $Graphics.DrawLine($seam, $X + 2.5, $Y + ($Height * 0.62), $X + $Width - 2.5, $Y + ($Height * 0.62))
    $seam.Dispose()
    $path.Dispose()
}

function Draw-RotatedCartridge($Graphics, [float]$Angle, [float]$X, [float]$Y, [float]$Width, [float]$Height) {
    $state = $Graphics.Save()
    $Graphics.TranslateTransform(64, 106)
    $Graphics.RotateTransform($Angle)
    $Graphics.TranslateTransform(-64, -106)
    Draw-Cartridge $Graphics $X $Y $Width $Height
    $Graphics.Restore($state)
}

function New-Icon([string]$Name, [scriptblock]$Draw) {
    $large = [System.Drawing.Bitmap]::new(128 * $Scale, 128 * $Scale, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($large)
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.ScaleTransform($Scale, $Scale)
    & $Draw $graphics
    $graphics.Dispose()

    $small = [System.Drawing.Bitmap]::new(128, 128, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $resize = [System.Drawing.Graphics]::FromImage($small)
    $resize.Clear([System.Drawing.Color]::Transparent)
    $resize.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
    $resize.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $resize.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $resize.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $resize.DrawImage($large, 0, 0, 128, 128)
    $resize.Dispose()
    $large.Dispose()

    $path = Join-Path $OutputDirectory "$Name.png"
    $small.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $small.Dispose()
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

New-Icon 'VCO_AimHead' { param($g) Draw-Pawn $g; Draw-Reticle $g 64 30 15 }
New-Icon 'VCO_AimTorso' { param($g) Draw-Pawn $g; Draw-Reticle $g 64 63 15 }
New-Icon 'VCO_AimLegs' { param($g) Draw-Pawn $g; Draw-Reticle $g 64 91 15 }
New-Icon 'VCO_AimNone' { param($g) Draw-Pawn $g; Draw-Brackets $g 16 }

New-Icon 'VCO_FireDefault' { param($g) Draw-Brackets $g 16; Draw-Cartridge $g 51 27 26 74 }
New-Icon 'VCO_FirePrecision' { param($g) Draw-Reticle $g 64 64 38 $false; Draw-Cartridge $g 54 40 20 48 }
New-Icon 'VCO_FireShortBurst' { param($g) Draw-Cartridge $g 18 35 24 64; Draw-Cartridge $g 52 29 24 70; Draw-Cartridge $g 86 35 24 64 }
New-Icon 'VCO_FireSuppression' {
    param($g)
    $dark = New-Pen $Charcoal 10
    $coralPen = New-Pen $Coral 5.5
    foreach ($pen in @($dark, $coralPen)) {
        $g.DrawLine($pen, 64, 72, 64, 18)
        $g.DrawLine($pen, 45, 76, 21, 29)
        $g.DrawLine($pen, 83, 76, 107, 29)
    }
    $dark.Dispose(); $coralPen.Dispose()
    Draw-RotatedCartridge $g -40 56 53 16 53
    Draw-RotatedCartridge $g -20 56 47 16 59
    Draw-RotatedCartridge $g 0 56 41 16 65
    Draw-RotatedCartridge $g 20 56 47 16 59
    Draw-RotatedCartridge $g 40 56 53 16 53
}
New-Icon 'VCO_FireAuto' {
    param($g)
    Draw-Reticle $g 64 64 38 $false
    $font = [System.Drawing.Font]::new('Arial', 42, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $format = [System.Drawing.StringFormat]::new()
    $format.Alignment = [System.Drawing.StringAlignment]::Center
    $format.LineAlignment = [System.Drawing.StringAlignment]::Center
    $darkBrush = [System.Drawing.SolidBrush]::new($Charcoal)
    $creamBrush = [System.Drawing.SolidBrush]::new($Cream)
    $g.DrawString('A', $font, $darkBrush, [System.Drawing.RectangleF]::new(27, 30, 74, 74), $format)
    $g.DrawString('A', $font, $creamBrush, [System.Drawing.RectangleF]::new(29, 28, 70, 74), $format)
    $darkBrush.Dispose(); $creamBrush.Dispose(); $format.Dispose(); $font.Dispose()
}
