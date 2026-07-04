$existing = Get-Process -Name PortLens -ErrorAction SilentlyContinue
if ($existing) {
    Stop-Process -InputObject $existing -Force
    Start-Sleep -Seconds 1
}

$p = Start-Process -FilePath 'outputs/PortLensMaterial/PortLens.exe' -PassThru
Start-Sleep -Seconds 4
Write-Host "PID=$($p.Id) Handle=$($p.MainWindowHandle) Title=$($p.MainWindowTitle)"

$children = Get-CimInstance Win32_Process -Filter "ParentProcessId=$($p.Id)" | Select-Object Name, CommandLine
Write-Host "Children: $($children.Count)"
$children | ForEach-Object { Write-Host "$($_.Name): $($_.CommandLine)" }

if ($p.MainWindowHandle -eq 0) {
    throw "PortLens main window not detected."
}

Write-Host "Smoke test passed."

Stop-Process -InputObject $p -Force
