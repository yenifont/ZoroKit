# Generate wizard BMP images for Inno Setup from app.png
# Usage: .\generate-images.ps1 [-Version "1.0.9"]  (version sol panelde gorunur; CI tag'den verilir)
param([string]$Version = "1.0.0")

Add-Type -AssemblyName System.Drawing

$root = Split-Path $PSScriptRoot -Parent
$appPng = Join-Path $root "src\ZaraGON.UI\Resources\app.png"
$outDir = $PSScriptRoot
$versionText = if ($Version -match "^\d") { "v$Version" } else { $Version }

$logo = [System.Drawing.Image]::FromFile($appPng)

# --- Large wizard image (164x314) - left panel ---
$w = 164; $h = 314
$bmp = New-Object System.Drawing.Bitmap($w, $h)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

# Gradient-like blue background (matching app icon #3898EC)
$topColor = [System.Drawing.Color]::FromArgb(33, 128, 220)
$bottomColor = [System.Drawing.Color]::FromArgb(56, 152, 236)
$gradBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    (New-Object System.Drawing.Point(0, 0)),
    (New-Object System.Drawing.Point(0, $h)),
    $topColor, $bottomColor)
$g.FillRectangle($gradBrush, 0, 0, $w, $h)

# Draw logo centered (100x100)
$logoSize = 100
$x = [int](($w - $logoSize) / 2)
$y = [int](($h - $logoSize) / 2) - 30
$g.DrawImage($logo, $x, $y, $logoSize, $logoSize)

# Draw app name below logo
$font = New-Object System.Drawing.Font("Segoe UI", 16, [System.Drawing.FontStyle]::Bold)
$textBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
$sf = New-Object System.Drawing.StringFormat
$sf.Alignment = [System.Drawing.StringAlignment]::Center
$rect = New-Object System.Drawing.RectangleF(0, ($y + $logoSize + 12), $w, 30)
$g.DrawString("ZaraGON", $font, $textBrush, $rect, $sf)

# Version text (parametreden; CI'da tag ile guncellenir)
$fontSmall = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Regular)
$mutedBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(200, 255, 255, 255))
$rect2 = New-Object System.Drawing.RectangleF(0, ($y + $logoSize + 40), $w, 20)
$g.DrawString($versionText, $fontSmall, $mutedBrush, $rect2, $sf)

$g.Dispose()
$bmp.Save((Join-Path $outDir "wizard.bmp"), [System.Drawing.Imaging.ImageFormat]::Bmp)
$bmp.Dispose()
Write-Host "wizard.bmp olusturuldu" -ForegroundColor Green

# --- Small wizard image (55x58) - top right corner ---
$bmp2 = New-Object System.Drawing.Bitmap(55, 58)
$g2 = [System.Drawing.Graphics]::FromImage($bmp2)
$g2.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$g2.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g2.Clear([System.Drawing.Color]::White)
$g2.DrawImage($logo, 2, 3, 51, 51)
$g2.Dispose()
$bmp2.Save((Join-Path $outDir "wizard-small.bmp"), [System.Drawing.Imaging.ImageFormat]::Bmp)
$bmp2.Dispose()
Write-Host "wizard-small.bmp olusturuldu" -ForegroundColor Green

# Cleanup
$logo.Dispose()
$gradBrush.Dispose()
$font.Dispose()
$fontSmall.Dispose()
$textBrush.Dispose()
$mutedBrush.Dispose()
$sf.Dispose()

Write-Host "Wizard gorselleri hazir!" -ForegroundColor Cyan
