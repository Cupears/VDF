param([switch]$NoOpen)
$ErrorActionPreference = 'Stop'
$forkRoot = Split-Path $PSScriptRoot -Parent
# The native desktop hosts the mobile server using its own engine and database.
& (Join-Path $forkRoot 'mobile\Start-Mobile.ps1')
Write-Host 'VDF desktop is ready. The phone shows this same scan and database.'
