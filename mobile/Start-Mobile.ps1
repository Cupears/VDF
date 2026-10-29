$ErrorActionPreference = 'Stop'
$mobileFolder = $PSScriptRoot
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
& 'C:\Program Files\Tailscale\tailscale.exe' serve --bg --http=80 --yes http://127.0.0.1:5001
if ($LASTEXITCODE -ne 0) { throw 'Tailscale could not update the phone link.' }
$phoneAddress = & 'C:\Program Files\Tailscale\tailscale.exe' ip -4
Write-Host "VDF fork mobile is ready. Turn on Tailscale on your phone and open http://$phoneAddress/" -ForegroundColor Green
