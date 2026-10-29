@echo off
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Start-Desktop.ps1" %*
if errorlevel 1 (
    echo.
    echo VDF could not start. Read the error above.
    pause
)
