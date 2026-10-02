param([int]$W, [int]$H)
. "$PSScriptRoot\drive.ps1"
Add-Type -TypeDefinition 'using System; using System.Runtime.InteropServices; public static class U5 { public struct R { public int L, T, Rt, B; } [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out R r); }'
Get-Process Cargo -ErrorAction SilentlyContinue | Stop-Process -Force; Start-Sleep -Milliseconds 800
$env:APP_NO_HOTDESIGN = '1'; $env:CARGO_WINDOW = "${W}x${H}"
$p = Start-Process "C:\Users\Platform006\Cargo\Cargo\bin\Debug\net10.0-desktop\Cargo.exe" -PassThru
$t = [Diagnostics.Stopwatch]::StartNew(); $last = ''
while ($t.Elapsed.TotalSeconds -lt 9) {
    $q = Get-Process -Id $p.Id -ErrorAction SilentlyContinue
    if ($q -and $q.MainWindowHandle -ne 0) {
        $r = New-Object U5+R; [U5]::GetWindowRect($q.MainWindowHandle, [ref]$r) | Out-Null
        $s = "$($r.Rt-$r.L)x$($r.B-$r.T)"
        if ($s -ne $last) { "{0:N1}s {1}" -f $t.Elapsed.TotalSeconds, $s; $last = $s }
    }
    Start-Sleep -Milliseconds 100
}
"exited: $($p.HasExited)"
