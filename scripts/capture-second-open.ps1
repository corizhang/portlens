Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Runtime.InteropServices;
public class WinAPI {
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll", SetLastError=true)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll", SetLastError=true)]
    public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_SHOWWINDOW = 0x0040;
    public const int SW_SHOW = 5;
    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP = 0x0004;
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
}
"@

function CaptureScreen($path) {
    $screen = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bitmap = New-Object System.Drawing.Bitmap($screen.Width, $screen.Height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.CopyFromScreen($screen.Location, [System.Drawing.Point]::Empty, $screen.Size)
    $bitmap.Save($path)
    $bitmap.Dispose()
}

function Click($x, $y) {
    [System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point($x, $y)
    [WinAPI]::mouse_event([WinAPI]::MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0)
    [WinAPI]::mouse_event([WinAPI]::MOUSEEVENTF_LEFTUP, 0, 0, 0, 0)
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

$rect = New-Object WinAPI+RECT
[WinAPI]::GetWindowRect($p.MainWindowHandle, [ref]$rect) | Out-Null

# Click settings button top-right
$settingsX = $rect.Right - 90
$settingsY = $rect.Top + 34
Click $settingsX $settingsY
Start-Sleep -Seconds 2
CaptureScreen 'first-open.png'

# Click cancel (bottom-right middle button)
$cancelX = $rect.Left + ($rect.Right - $rect.Left) / 2 + 70
$cancelY = $rect.Top + ($rect.Bottom - $rect.Top) - 55
Click $cancelX $cancelY
Start-Sleep -Seconds 1

# Click settings again
Click $settingsX $settingsY
Start-Sleep -Seconds 2
CaptureScreen 'second-open.png'

[WinAPI]::SetWindowPos($p.MainWindowHandle, [WinAPI]::HWND_NOTOPMOST, 0, 0, 0, 0, [WinAPI]::SWP_NOMOVE -bor [WinAPI]::SWP_NOSIZE) | Out-Null
Stop-Process -InputObject $p -Force
Write-Host "Screenshots saved."
