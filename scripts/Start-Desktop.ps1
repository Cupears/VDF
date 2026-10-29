param([switch]$NoOpen)
$ErrorActionPreference = 'Stop'
$forkRoot = Split-Path $PSScriptRoot -Parent
# Both entry points attach to the same existing web process; they never launch a second engine.
& (Join-Path $forkRoot 'mobile\Start-Mobile.ps1')
$desktopUrl = 'http://127.0.0.1:5001/'
Write-Host "Shared VDF desktop: $desktopUrl"
Write-Host 'Desktop and phone use the same scan, database, results, and checked files.'
if (-not $NoOpen) {
    $browser = [System.Diagnostics.ProcessStartInfo]::new($desktopUrl)
    $browser.UseShellExecute = $true
    [System.Diagnostics.Process]::Start($browser) | Out-Null
}
