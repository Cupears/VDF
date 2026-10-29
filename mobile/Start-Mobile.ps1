$ErrorActionPreference = 'Stop'
$mobileFolder = $PSScriptRoot
$env:VDF_SETTINGS_SYNC_PATH = Join-Path (Split-Path $mobileFolder -Parent) '.shared-scan-settings.json'
$existing = Get-CimInstance Win32_Process -Filter "Name='VDF.Web.exe'" |
    Where-Object ExecutablePath -eq (Join-Path $mobileFolder 'VDF.Web.exe')
if (-not $existing) {
    Start-Process -FilePath (Join-Path $mobileFolder 'VDF.Web.exe') -ArgumentList '--urls','http://127.0.0.1:5001' -WorkingDirectory $mobileFolder -WindowStyle Hidden -RedirectStandardOutput (Join-Path $mobileFolder 'server.log') -RedirectStandardError (Join-Path $mobileFolder 'server-error.log')
}
$ready = $false
for ($attempt = 0; $attempt -lt 20; $attempt++) {
    try {
        $response = Invoke-WebRequest 'http://127.0.0.1:5001/login' -UseBasicParsing -TimeoutSec 2
        if ($response.StatusCode -eq 200) { $ready = $true; break }
    } catch { Start-Sleep -Seconds 1 }
}
if (-not $ready) { throw 'VDF mobile server did not start. Check server-error.log in this folder.' }
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
