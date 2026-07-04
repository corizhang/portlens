Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName UIAutomationClient

Add-Type @"
using System;
using System.Runtime.InteropServices;
public class WinAPI {
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll", SetLastError=true)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll", SetLastError=true)]
    public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);
    [DllImport("user32.dll")]
    public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);
    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_SHOWWINDOW = 0x0040;
    public const int SW_SHOW = 5;
    public const uint PW_CLIENTONLY = 0x00000001;
    public const uint PW_RENDERFULLCONTENT = 0x00000002;
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
}
"@

function CaptureWindow($hWnd, $path) {
    $rect = New-Object WinAPI+RECT
    [WinAPI]::GetWindowRect($hWnd, [ref]$rect) | Out-Null
    $w = $rect.Right - $rect.Left
    $h = $rect.Bottom - $rect.Top
    if ($w -le 0 -or $h -le 0) { return }
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $gfx = [System.Drawing.Graphics]::FromImage($bmp)
    $hdc = $gfx.GetHdc()
    [WinAPI]::PrintWindow($hWnd, $hdc, [WinAPI]::PW_RENDERFULLCONTENT) | Out-Null
    $gfx.ReleaseHdc($hdc)
    $gfx.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

function Click($x, $y) {
    [System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point($x, $y)
    [WinAPI]::mouse_event(0x0002, 0, 0, 0, 0)
    [WinAPI]::mouse_event(0x0004, 0, 0, 0, 0)
}

function FindSettingsButton($window) {
    $buttonCondition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Button)
    $buttons = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $buttonCondition)
    $windowRect = $window.Current.BoundingRectangle
    $candidates = @()
    foreach ($btn in $buttons) {
        $rect = $btn.Current.BoundingRectangle
        $top = $rect.Top - $windowRect.Top
        $right = $windowRect.Right - $rect.Right
        $w = $rect.Right - $rect.Left
        $h = $rect.Bottom - $rect.Top
        # Settings is a small icon button in the top-right caption strip
        if ($top -ge 10 -and $top -le 50 -and $right -ge 0 -and $right -le 150 -and $w -lt 45 -and $h -lt 45) {
            $candidates += $btn
        }
    }
    if ($candidates.Count -eq 0) { return $null }
    # Settings is the leftmost of the three top-right window buttons
    return $candidates | Sort-Object { $_.Current.BoundingRectangle.Left } | Select-Object -First 1
}

function FindTabButtons($window) {
    $buttonCondition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Button)
    $buttons = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $buttonCondition)
    $windowRect = $window.Current.BoundingRectangle
    $candidates = @()
    foreach ($btn in $buttons) {
        $rect = $btn.Current.BoundingRectangle
        $top = $rect.Top - $windowRect.Top
        $left = $rect.Left - $windowRect.Left
        # Tab buttons are in the upper dialog area, below the title but above the content controls
        if ($top -ge 100 -and $top -le 220 -and $left -ge 50 -and $left -le 500) {
            $candidates += $btn
        }
    }
    return $candidates | Sort-Object { $_.Current.BoundingRectangle.Left }
}

function FindCancelButton($window) {
    $buttonCondition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Button)
    $buttons = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $buttonCondition)
    $windowRect = $window.Current.BoundingRectangle
    $candidates = @()
    foreach ($btn in $buttons) {
        $rect = $btn.Current.BoundingRectangle
        if (($windowRect.Bottom - $rect.Bottom) -lt 100) { $candidates += $btn }
    }
    if ($candidates.Count -eq 0) { return $null }
    $sorted = $candidates | Sort-Object { $_.Current.BoundingRectangle.Left }
    return $sorted[[Math]::Floor($sorted.Count / 2)]
}

function ClickElement($element) {
    $rect = $element.Current.BoundingRectangle
    $x = [Math]::Round(($rect.Left + $rect.Right) / 2)
    $y = [Math]::Round(($rect.Top + $rect.Bottom) / 2)
    Click $x $y
}

function InvokeButton($btn) {
    try {
        $pattern = $btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
        if ($pattern -ne $null) {
            $pattern.Invoke()
            return
        }
    } catch {}
    ClickElement $btn
}

# Kill existing
$existing = Get-Process -Name PortLens -ErrorAction SilentlyContinue
if ($existing) { Stop-Process -InputObject $existing -Force; Start-Sleep -Seconds 2 }

$p = Start-Process -FilePath 'outputs/PortLensMaterial/PortLens.exe' -PassThru
Start-Sleep -Seconds 6
if ($p.MainWindowHandle -eq 0) { throw "Main window not detected." }

[WinAPI]::ShowWindow($p.MainWindowHandle, [WinAPI]::SW_SHOW) | Out-Null
[WinAPI]::SetWindowPos($p.MainWindowHandle, [WinAPI]::HWND_TOPMOST, 0, 0, 0, 0, [WinAPI]::SWP_NOMOVE -bor [WinAPI]::SWP_NOSIZE -bor [WinAPI]::SWP_SHOWWINDOW) | Out-Null
[WinAPI]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
Start-Sleep -Seconds 2

CaptureWindow $p.MainWindowHandle 'verify-main-window.png'

$window = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
$settingsBtn = FindSettingsButton $window
if ($settingsBtn -eq $null) { throw "Settings button not found." }
InvokeButton $settingsBtn
Start-Sleep -Seconds 2

CaptureWindow $p.MainWindowHandle 'verify-first-open.png'

$window = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
$tabs1 = FindTabButtons $window
Write-Host "First open tab buttons: $($tabs1.Count)"
if ($tabs1.Count -lt 3) { throw "Expected 3 tab buttons on first open, found $($tabs1.Count)" }

$cancelBtn = FindCancelButton $window
if ($cancelBtn -ne $null) { InvokeButton $cancelBtn }
Start-Sleep -Seconds 1

# Second open
$window = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
$settingsBtn = FindSettingsButton $window
InvokeButton $settingsBtn
Start-Sleep -Seconds 2

CaptureWindow $p.MainWindowHandle 'verify-second-open.png'

$window = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
$tabs2 = FindTabButtons $window
Write-Host "Second open tab buttons: $($tabs2.Count)"
if ($tabs2.Count -lt 3) { throw "Expected 3 tab buttons on second open, found $($tabs2.Count)" }

# Try clicking the Rules tab on second open
InvokeButton $tabs2[1]
Start-Sleep -Seconds 1
CaptureWindow $p.MainWindowHandle 'verify-second-open-rules.png'
Write-Host "Rules tab clicked after second open: $($tabs2[1].Current.Name)"

# Try clicking Blacklist tab
$tabs3 = FindTabButtons $window
if ($tabs3.Count -ge 3) {
    InvokeButton $tabs3[2]
    Start-Sleep -Seconds 1
    CaptureWindow $p.MainWindowHandle 'verify-second-open-blacklist.png'
    Write-Host "Blacklist tab clicked after second open: $($tabs3[2].Current.Name)"
}

[WinAPI]::SetWindowPos($p.MainWindowHandle, [WinAPI]::HWND_NOTOPMOST, 0, 0, 0, 0, [WinAPI]::SWP_NOMOVE -bor [WinAPI]::SWP_NOSIZE) | Out-Null
Stop-Process -InputObject $p -Force
Write-Host "Verification complete."
