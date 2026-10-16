# UIA driver for Six Console: enumerate or interact with named elements.
param(
    [string]$Action = "Dump",
    [string]$Target = "",
    [string]$Value = ""
)
$ErrorActionPreference = "Continue"
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

Add-Type @"
using System;
using System.Runtime.InteropServices;
public class WinUia {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
"@ -ErrorAction SilentlyContinue

$proc = Get-Process | Where-Object { $_.MainWindowTitle -like "*Six Console*" } | Select-Object -First 1
if (-not $proc) { [Console]::Error.WriteLine("NOWINDOW"); exit 1 }
$hwnd = $proc.MainWindowHandle
[WinUia]::ShowWindow($hwnd, 9) | Out-Null
[WinUia]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds 700

$root = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)

function Find-Element([string]$byName) {
    $findName = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $byName)
    $el = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $findName)
    if ($el) { return $el }
    $findId = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $byName)
    return $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $findId)
}

if ($Action -eq "Dump") {
    $queue = New-Object System.Collections.Queue
    $queue.Enqueue($root)
    $seen = 0
    while ($queue.Count -gt 0 -and $seen -lt 250) {
        $el = $queue.Dequeue()
        try {
            $cur = $el.Current
            $name = $cur.Name
            $id = $cur.AutomationId
            if ($name -or $id) {
                [Console]::WriteLine("($seen) $($cur.ControlType.ProgrammaticName) name='$name' id='$id' enabled=$($cur.IsEnabled)")
                $seen++
            }
            $kids = $el.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
            foreach ($k in $kids) { $queue.Enqueue($k) }
        } catch { }
    }
    [Console]::Error.WriteLine("total visible: $seen")
    exit 0
}

$el = Find-Element $Target
if (-not $el) { [Console]::Error.WriteLine("NOTFOUND: $Target"); exit 1 }

switch ($Action) {
    "Invoke" {
        try { ($el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)).Invoke(); [Console]::Error.WriteLine("INVOKED: $Target"); exit 0 }
        catch { [Console]::Error.WriteLine("INVOKE-FAIL: $($_.Exception.Message)"); exit 1 }
    }
    "Select" {
        try { ($el.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)).Select(); [Console]::Error.WriteLine("SELECTED: $Target"); exit 0 }
        catch { [Console]::Error.WriteLine("SELECT-FAIL: $($_.Exception.Message)"); exit 1 }
    }
    "Toggle" {
        try {
            $tp = $el.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
            $state = $tp.Current.ToggleState
            if ($Value -ne "" -and $Value -eq $state.ToString()) { [Console]::Error.WriteLine("SKIPPED: $Target already $state"); exit 0 }
            $tp.Toggle(); [Console]::Error.WriteLine("TOGGLED: $Target (was $state)"); exit 0
        } catch { [Console]::Error.WriteLine("TOGGLE-FAIL: $($_.Exception.Message)"); exit 1 }
    }
    "SetValue" {
        try { ($el.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)).SetValue($Value); [Console]::Error.WriteLine("SETVALUE: $Target = $Value"); exit 0 }
        catch { [Console]::Error.WriteLine("SETVALUE-FAIL: $($_.Exception.Message)"); exit 1 }
    }
    "SetRange" {
        try { ($el.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern)).SetValue([double]$Value); [Console]::Error.WriteLine("SETRANGE: $Target = $Value"); exit 0 }
        catch { [Console]::Error.WriteLine("SETRANGE-FAIL: $($_.Exception.Message)"); exit 1 }
    }
    "Click" {
        # Real input click at the element's bounding-rect center (exercises the true hit-test path).
        try {
            Add-Type @'
using System;using System.Runtime.InteropServices;
public class Clicker {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern bool GetCursorPos(out P p);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint data, IntPtr extra);
  public struct P { public int X; public int Y; }
}
'@ -ErrorAction SilentlyContinue
            $rect = $el.Current.BoundingRectangle
            $cx = [int]($rect.X + $rect.Width / 2)
            $cy = [int]($rect.Y + $rect.Height / 2)
            # calibrate DPI virtualization: probe where SetCursorPos actually lands, then scale up
            $probe = New-Object Clicker+P
            [Clicker]::SetCursorPos($cx, $cy) | Out-Null
            Start-Sleep -Milliseconds 120
            [Clicker]::GetCursorPos([ref]$probe) | Out-Null
            if ($probe.X -gt 0 -and $cx -gt 0) { $cx = [int]([double]$cx * $cx / $probe.X) }
            if ($probe.Y -gt 0 -and $cy -gt 0) { $cy = [int]([double]$cy * $cy / $probe.Y) }
            [Clicker]::SetCursorPos($cx, $cy) | Out-Null
            Start-Sleep -Milliseconds 120
            [Clicker]::mouse_event(2,0,0,0,[IntPtr]::Zero)  # LEFTDOWN
            Start-Sleep -Milliseconds 70
            [Clicker]::mouse_event(4,0,0,0,[IntPtr]::Zero)  # LEFTUP
            [Console]::Error.WriteLine("CLICKED at ($cx,$cy)"); exit 0
        } catch { [Console]::Error.WriteLine("CLICK-FAIL: $($_.Exception.Message)"); exit 1 }
    }
    "ClickAll" {
        # Try patterns on the matched element and its ancestors until one reacts.
        try {
            $cur = $el
            $depth = 0
            while ($cur -ne $null -and $depth -lt 6) {
                $depth++
                $t = $cur.Current.ControlType.ProgrammaticName
                try { ($cur.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)).Invoke(); [Console]::Error.WriteLine("ANCESTOR[$depth] INVOKE ok type=$t"); exit 0 } catch { }
                try { ($cur.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)).Toggle(); [Console]::Error.WriteLine("ANCESTOR[$depth] TOGGLE ok type=$t"); exit 0 } catch { }
                try { ($cur.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)).Select(); [Console]::Error.WriteLine("ANCESTOR[$depth] SELECT ok type=$t"); exit 0 } catch { }
                try { $cur = [System.Windows.Automation.TreeWalker]::ControlViewWalker.GetParent($cur) } catch { $cur = $null }
            }
            [Console]::Error.WriteLine("CLICKALL: no ancestor reacted"); exit 1
        } catch { [Console]::Error.WriteLine("CLICKALL-FAIL: $($_.Exception.Message)"); exit 1 }
    }
    "Read" {
        try { [Console]::Error.WriteLine("READ: name='$($el.Current.Name)' id='$($el.Current.AutomationId)' enabled=$($el.Current.IsEnabled) type=$($el.Current.ControlType.ProgrammaticName)"); exit 0 }
        catch { [Console]::Error.WriteLine("READ-FAIL"); exit 1 }
    }
    default { [Console]::Error.WriteLine("UNKNOWN ACTION: $Action"); exit 1 }
}

