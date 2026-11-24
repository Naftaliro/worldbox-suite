@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\install_mods.ps1" %*
echo.
pause
