param([int]$W, [int]$H)
Get-Process Cargo -ErrorAction SilentlyContinue | Stop-Process -Force; Start-Sleep -Milliseconds 800
$env:APP_NO_HOTDESIGN = '1'; $env:CARGO_WINDOW = "${W}x${H}"
$p = Start-Process "C:\Users\Platform006\Cargo\Cargo\bin\Debug\net10.0-desktop\Cargo.exe" -PassThru
Start-Sleep -Seconds 12
"${W}x${H}: " + $(if ($p.HasExited) { 'CRASH {0:X}' -f $p.ExitCode } else { 'alive' })
