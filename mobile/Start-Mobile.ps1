$ErrorActionPreference = 'Stop'
$mobileFolder = $PSScriptRoot
$forkRoot = Split-Path $mobileFolder -Parent
$env:VDF_SETTINGS_SYNC_PATH = Join-Path (Split-Path $mobileFolder -Parent) '.shared-scan-settings.json'
$env:VDF_DESKTOP_MOBILE = '1'
$env:VDF_MOBILE_CONTENT_ROOT = $mobileFolder
$desktopExe = Join-Path $forkRoot 'VDF.GUI\bin\Debug\net10.0\VDF.GUI.exe'
$existing = Get-CimInstance Win32_Process -Filter "Name='VDF.GUI.exe'" |
    Where-Object ExecutablePath -eq $desktopExe
if (-not $existing) {
    $oldWeb = Get-CimInstance Win32_Process -Filter "Name='VDF.Web.exe'" |
        Where-Object ExecutablePath -eq (Join-Path $mobileFolder 'VDF.Web.exe')
    if ($oldWeb) { throw 'The older separate web server is still running. Close it before opening the shared desktop.' }
    Start-Process -FilePath $desktopExe -WorkingDirectory (Split-Path $desktopExe -Parent)
}
$ready = $false
for ($attempt = 0; $attempt -lt 45; $attempt++) {
    try {
        $response = Invoke-WebRequest 'http://127.0.0.1:5001/login' -UseBasicParsing -TimeoutSec 2
        if ($response.StatusCode -eq 200) { $ready = $true; break }
    } catch { Start-Sleep -Seconds 1 }
}
if (-not $ready) { throw 'The desktop mobile server did not start. Check the desktop window and its log.txt.' }
$serveConfig = & 'C:\Program Files\Tailscale\tailscale.exe' serve status --json | ConvertFrom-Json
if ($serveConfig.TCP.'80'.HTTP) {
    & 'C:\Program Files\Tailscale\tailscale.exe' serve --http=80 off
    if ($LASTEXITCODE -ne 0) { throw 'Tailscale could not remove the old hostname-only route.' }
}
# Forward TCP so the phone can use the Tailscale IP as well as the DNS name.
& 'C:\Program Files\Tailscale\tailscale.exe' serve --bg --tcp=80 --yes tcp://127.0.0.1:5001
if ($LASTEXITCODE -ne 0) { throw 'Tailscale could not update the phone link.' }
$phoneAddress = & 'C:\Program Files\Tailscale\tailscale.exe' ip -4
Write-Host "VDF fork mobile is ready. Turn on Tailscale on your phone and open http://$phoneAddress/" -ForegroundColor Green
Write-Host 'The native VDF desktop and phone share one engine and database. Keep VDF open on the PC.'
