$ErrorActionPreference = 'Stop'
$forkRoot = Split-Path $PSScriptRoot -Parent
$env:VDF_SETTINGS_SYNC_PATH = Join-Path $forkRoot '.shared-scan-settings.json'
$desktopFolder = Join-Path $forkRoot 'VDF.GUI\bin\Debug\net10.0'
$executable = Join-Path $desktopFolder 'VDF.GUI.exe'
if (-not (Test-Path -LiteralPath $executable)) { throw 'Build VDF.GUI before starting the desktop app.' }
$existing = Get-CimInstance Win32_Process -Filter "Name='VDF.GUI.exe'" | Where-Object ExecutablePath -eq $executable
if (-not $existing) {
    Start-Process -FilePath $executable -WorkingDirectory $desktopFolder -WindowStyle Hidden
}
