param(
    [string]$WindowTitle = "Six Console",
    [string]$OutPath = "$env:TEMP\opencode\appshot.png"
)
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class WinCap {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
"@ -ErrorAction SilentlyContinue

$proc = Get-Process | Where-Object { $_.MainWindowTitle -like "*$WindowTitle*" } | Select-Object -First 1
if (-not $proc) { Write-Output "NOWINDOW"; exit 1 }
$hwnd = $proc.MainWindowHandle
[WinCap]::ShowWindow($hwnd, 9) | Out-Null
[WinCap]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds 700
$rect = New-Object WinCap+RECT
[WinCap]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
$w = $rect.Right - $rect.Left
$h = $rect.Bottom - $rect.Top
if ($w -le 0 -or $h -le 0) { Write-Output "BADRECT"; exit 1 }
$bmp = New-Object System.Drawing.Bitmap($w, $h)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $g.GetHdc()
$ok = [WinCap]::PrintWindow($hwnd, $hdc, 2)  # PW_RENDERFULLCONTENT
$g.ReleaseHdc($hdc)
$g.Dispose()
# PrintWindow of WinUI content can yield black; fall back to CopyFromScreen of that rect
$allBlack = $true
$bmp2 = $null
if ($ok) {
    for ($x = 0; $x -lt $w; $x += 61) {
        for ($y = 0; $y -lt $h; $y += 61) {
            if ($bmp.GetPixel($x, $y).R -ne 0 -or $bmp.GetPixel($x, $y).G -ne 0) { $allBlack = $false; break }
        }
        if (-not $allBlack) { break }
    }
}
if (-not $ok -or $allBlack) {
    $bmp2 = New-Object System.Drawing.Bitmap($w, $h)
    $g2 = [System.Drawing.Graphics]::FromImage($bmp2)
    $g2.CopyFromScreen($rect.Left, $rect.Top, 0, 0, (New-Object System.Drawing.Size($w, $h)))
    $g2.Dispose()
    $bmp2.Save($OutPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp2.Dispose()
    Write-Output "Saved (screen-rect fallback): $OutPath"
} else {
    $bmp.Save($OutPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Output "Saved (PrintWindow): $OutPath"
}
Write-Output "SIZE: ${w}x${h}"
