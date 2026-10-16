param([int]$TargetPid = 0)
Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public class WinEnum {
    public delegate bool EnumProc(IntPtr hWnd, IntPtr lparam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc proc, IntPtr lparam);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
    [DllImport("user32.dll")] public static extern int GetClassName(IntPtr hWnd, StringBuilder text, int count);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
"@
$results = New-Object System.Collections.ArrayList
$cb = [WinEnum+EnumProc]{
    param($hWnd, $lp)
    [uint32]$pid = 0
    [WinEnum]::GetWindowThreadProcessId($hWnd, [ref]$pid) | Out-Null
    if ($pid -eq $TargetPid) {
        $sb = New-Object System.Text.StringBuilder 256
        [WinEnum]::GetWindowText($hWnd, $sb, 256) | Out-Null
        $title = $sb.ToString()
        $cn = New-Object System.Text.StringBuilder 256
        [WinEnum]::GetClassName($hWnd, $cn, 256) | Out-Null
        $rect = New-Object WinEnum+RECT
        [WinEnum]::GetWindowRect($hWnd, [ref]$rect) | Out-Null
        $script:results.Add("hwnd=$hWnd class='$($cn.ToString())' title='$title' visible=$([WinEnum]::IsWindowVisible($hWnd)) rect=($($rect.Left),$($rect.Top))-($($rect.Right),$($rect.Bottom))") | Out-Null
    }
    return $true
}
[WinEnum]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
$results | ForEach-Object { Write-Output $_ }
