@echo off
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File "%~dp0mobile\Start-Mobile.ps1" %*
if errorlevel 1 (
    echo.
    echo VDF Mobile could not start. Read the error above.
    pause
)
