param(
    [string]$OutputDir = "docs\assets",
    [string]$AppAssetsDir = "src\SixConsole.App\Assets"
)

Add-Type -AssemblyName System.Drawing

if (-not (Test-Path $OutputDir)) { New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null }
if (-not (Test-Path $AppAssetsDir)) { New-Item -ItemType Directory -Path $AppAssetsDir -Force | Out-Null }

function Build-LogoBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $fSize = [float]$size
    $center = $fSize / 2.0

    # 1. Outer dark container circle with subtle gradient feel
    $radius = $fSize * 0.45
    $rect = New-Object System.Drawing.RectangleF(($center - $radius), ($center - $radius), ($radius * 2.0), ($radius * 2.0))
    $bgBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 24, 24, 27)) # Zinc-900
    $g.FillEllipse($bgBrush, $rect)

    # 2. Outer rim border
    $borderPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 45, 45, 52), [float]($fSize * 0.02))
    $g.DrawEllipse($borderPen, $rect)

    # 3. Reticle accent ring (Fluent Cyan #60CDFF)
    $innerRadius = $radius * 0.78
    $innerRect = New-Object System.Drawing.RectangleF(($center - $innerRadius), ($center - $innerRadius), ($innerRadius * 2.0), ($innerRadius * 2.0))
    $ringPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(220, 96, 205, 255), [float]($fSize * 0.035))
    $g.DrawEllipse($ringPen, $innerRect)

    # 4. Precision crosshair ticks (N, S, W, E)
    $tickPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 96, 205, 255), [float]($fSize * 0.035))
    $tickStart = $radius * 0.88
    $tickEnd = $radius * 1.02

    $g.DrawLine($tickPen, $center, ($center - $tickEnd), $center, ($center - $tickStart))
    $g.DrawLine($tickPen, $center, ($center + $tickStart), $center, ($center + $tickEnd))
    $g.DrawLine($tickPen, ($center - $tickEnd), $center, ($center - $tickStart), $center)
    $g.DrawLine($tickPen, ($center + $tickStart), $center, ($center + $tickEnd), $center)

    # 5. Tactical numeral "6"
    $fontFamily = New-Object System.Drawing.FontFamily("Segoe UI")
    $fontSize = $fSize * 0.44
    $font = New-Object System.Drawing.Font($fontFamily, $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $textBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 255, 255, 255))
    $sf = New-Object System.Drawing.StringFormat
    $sf.Alignment = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center

    $textRect = New-Object System.Drawing.RectangleF(0, (-$fSize * 0.02), $fSize, $fSize)
    $g.DrawString("6", $font, $textBrush, $textRect, $sf)

    $font.Dispose()
    $fontFamily.Dispose()
    $textBrush.Dispose()
    $tickPen.Dispose()
    $ringPen.Dispose()
    $borderPen.Dispose()
    $bgBrush.Dispose()
    $g.Dispose()

    return $bmp
}

# Generate App Icons
$logo512 = Build-LogoBitmap 512
$logo512.Save((Join-Path $OutputDir "logo.png"), [System.Drawing.Imaging.ImageFormat]::Png)

$logo150 = Build-LogoBitmap 150
$logo150.Save((Join-Path $AppAssetsDir "Square150x150Logo.png"), [System.Drawing.Imaging.ImageFormat]::Png)

$logo44 = Build-LogoBitmap 44
$logo44.Save((Join-Path $AppAssetsDir "Square44x44Logo.png"), [System.Drawing.Imaging.ImageFormat]::Png)

$logo50 = Build-LogoBitmap 50
$logo50.Save((Join-Path $AppAssetsDir "StoreLogo.png"), [System.Drawing.Imaging.ImageFormat]::Png)

# Generate 1280x640 Social Preview Card
function Build-SocialPreview() {
    $w = 1280
    $h = 640
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

    # Background: deep dark slate
    $bgBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 18, 18, 20))
    $g.FillRectangle($bgBrush, 0, 0, $w, $h)

    # Subtle accent glow in top-left
    $glowPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(20, 96, 205, 255), 180)
    $g.DrawEllipse($glowPen, -80, -80, 500, 500)
    $glowPen.Dispose()

    # Draw logo icon on left
    $iconBmp = Build-LogoBitmap 110
    $g.DrawImage($iconBmp, 60, 60, 110, 110)
    $iconBmp.Dispose()

    # Title: Six Console
    $titleFont = New-Object System.Drawing.Font("Segoe UI", 42, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $whiteBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $g.DrawString("Six Console", $titleFont, $whiteBrush, 190, 80)

    # Subtitle
    $subFont = New-Object System.Drawing.Font("Segoe UI", 18, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
    $mutedBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 180, 180, 190))
    $g.DrawString("Standalone Windows Desktop Recoil & Loadout Console", $subFont, $mutedBrush, 60, 190)

    # Bullet / Key points
    $bodyFont = New-Object System.Drawing.Font("Segoe UI", 15, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
    $cyanBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 96, 205, 255))
    
    $features = @(
        "• Native Windows 11 / 10 desktop application with Fluent UI",
        "• 76 operators and 142 weapons preserved with 1:1 math integrity",
        "• Full-auto recoil control and rapid-fire modes with live preview",
        "• Dedicated low-level hook engine with instant MMB toggle & emergency stop",
        "• No admin rights required · No injection · Zero telemetry"
    )
    $y = 235
    foreach ($feat in $features) {
        $g.DrawString($feat, $bodyFont, $mutedBrush, 60, $y)
        $y += 32
    }

    # Tech Badges
    $badgeFont = New-Object System.Drawing.Font("Segoe UI", 12, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $badgeBg = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 34, 34, 40))
    $badgeBorder = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 60, 60, 72), 1)
    
    $badges = @("C# / .NET 10", "WinUI 3", "Windows App SDK", "Win32 Raw Input", "x64")
    $bx = 60
    foreach ($b in $badges) {
        $bSize = $g.MeasureString($b, $badgeFont)
        $bw = $bSize.Width + 20
        $g.FillRectangle($badgeBg, $bx, 420, $bw, 28)
        $g.DrawRectangle($badgeBorder, $bx, 420, $bw, 28)
        $g.DrawString($b, $badgeFont, $cyanBrush, ($bx + 10), 426)
        $bx += [int]($bw + 12)
    }

    # App Screenshot Preview on Right
    $screenshotPath = "docs\screenshots\profile-dark.png"
    if (Test-Path $screenshotPath) {
        $screenBmp = [System.Drawing.Image]::FromFile($screenshotPath)
        # Scaled to fit in ~580x300
        $targetW = 560
        $targetH = [int]($targetW * ($screenBmp.Height / $screenBmp.Width))
        $sx = 660
        $sy = 150

        # Border/Shadow frame
        $frameBorder = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 55, 55, 65), 2)
        $g.DrawRectangle($frameBorder, ($sx - 2), ($sy - 2), ($targetW + 4), ($targetH + 4))
        $g.DrawImage($screenBmp, $sx, $sy, $targetW, $targetH)

        $frameBorder.Dispose()
        $screenBmp.Dispose()
    }

    # Footer banner
    $footerBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 120, 120, 130))
    $footerFont = New-Object System.Drawing.Font("Segoe UI", 13, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
    $g.DrawString("Rainbow Six Siege Logitech Lua Profile Standalone Migration", $footerFont, $footerBrush, 60, 580)

    $bmp.Save((Join-Path $OutputDir "social-preview.png"), [System.Drawing.Imaging.ImageFormat]::Png)

    $footerFont.Dispose()
    $footerBrush.Dispose()
    $badgeBorder.Dispose()
    $badgeBg.Dispose()
    $badgeFont.Dispose()
    $cyanBrush.Dispose()
    $bodyFont.Dispose()
    $mutedBrush.Dispose()
    $subFont.Dispose()
    $whiteBrush.Dispose()
    $titleFont.Dispose()
    $bgBrush.Dispose()
    $g.Dispose()
    $bmp.Dispose()
}

Build-SocialPreview
Write-Output "Assets and Social Preview built successfully."
