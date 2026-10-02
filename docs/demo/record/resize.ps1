param([int]$W, [int]$H, [string]$Snap = "")
. "$PSScriptRoot\drive.ps1"
$win = Get-CargoWindow
# One call, no z-order change, no activation. (A zero-height request crashes the GL renderer; the "resize
# crash" seen on 2026-10-01 was this script asking for height 0 after $H collided with drive.ps1's handle.)
[U]::SetWindowPos($win, [IntPtr]::Zero, 0, 0, $W, $H, 0x0004 -bor 0x0010) | Out-Null
Start-Sleep -Milliseconds 1500
Use-Window $win; $r = Get-ClientRect; "client $($r.W)x$($r.H)"
if ($Snap) { Snap $Snap }
