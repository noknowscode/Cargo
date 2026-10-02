# Opens the app fresh at a window size and saves a still of the client area: shot.ps1 -W 390 -H 844 -Name phone
param([int]$W, [int]$H, [string]$Name, [int]$Settle = 9)
. "$PSScriptRoot\drive.ps1"
Get-Process Cargo -ErrorAction SilentlyContinue | Stop-Process -Force; Start-Sleep -Milliseconds 800
$env:APP_NO_HOTDESIGN = '1'; $env:CARGO_WINDOW = "${W}x${H}"
Start-Process "C:\Users\Platform006\Cargo\Cargo\bin\Debug\net10.0-desktop\Cargo.exe"
$h = [IntPtr]::Zero
while ($h -eq [IntPtr]::Zero) { Start-Sleep -Milliseconds 50; $p = Get-Process Cargo -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1; if ($p) { $h = $p.MainWindowHandle } }
[U]::SetWindowPos($h, [IntPtr]::Zero, 0, 0, 0, 0, 0x0001 -bor 0x0004 -bor 0x0010) | Out-Null
[U]::SetForegroundWindow($h) | Out-Null
Start-Sleep -Seconds $Settle
Use-Window $h; $r = Get-ClientRect; "client $($r.W)x$($r.H)"
Snap $Name
