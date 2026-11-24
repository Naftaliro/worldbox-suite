@echo off
title WorldBox Studio
cd /d "%~dp0"

where python >nul 2>nul
if %errorlevel% neq 0 (
    echo Python 3 was not found in PATH. Install Python 3 from https://www.python.org/downloads/ and check "Add Python to PATH".
    pause
    exit /b 1
)

echo Starting WorldBox Studio at http://127.0.0.1:8765 ...
start "" /b python -m tools.studio.server --port 8765
timeout /t 2 /nobreak >nul

:: Open as a clean chromeless desktop-style window in Edge or Chrome if available
set "URL=http://127.0.0.1:8765"
if exist "%ProgramFiles(x86)%\Microsoft\Edge\Application\msedge.exe" (
    start "" "%ProgramFiles(x86)%\Microsoft\Edge\Application\msedge.exe" --app=%URL%
) else if exist "%ProgramFiles%\Google\Chrome\Application\chrome.exe" (
    start "" "%ProgramFiles%\Google\Chrome\Application\chrome.exe" --app=%URL%
) else (
    start "" %URL%
)

echo WorldBox Studio is running. Keep this window open while using the Studio, or press Ctrl+C to stop.
pause >nul
