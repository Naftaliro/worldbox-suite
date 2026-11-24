@echo off
setlocal enabledelayedexpansion
title WorldBox Studio
cd /d "%~dp0"

set "PORT=8765"
set "URL=http://127.0.0.1:%PORT%"

:: 1. Find a real Python 3 executable (avoiding the Windows Store python.exe stub)
set "PY_CMD="

if exist "%~dp0python\python.exe" (
    "%~dp0python\python.exe" -c "import sys" >nul 2>nul
    if !errorlevel! equ 0 set "PY_CMD=%~dp0python\python.exe"
)

if not defined PY_CMD (
    python -c "import sys; raise SystemExit(0 if sys.version_info >= (3, 9) else 1)" >nul 2>nul
    if !errorlevel! equ 0 set "PY_CMD=python"
)

if not defined PY_CMD (
    py -3 -c "import sys" >nul 2>nul
    if !errorlevel! equ 0 set "PY_CMD=py -3"
)

if not defined PY_CMD (
    for /d %%D in ("%LOCALAPPDATA%\Programs\Python\Python3*") do (
        if exist "%%D\python.exe" (
            "%%D\python.exe" -c "import sys" >nul 2>nul
            if !errorlevel! equ 0 set "PY_CMD=%%D\python.exe"
        )
    )
)

if not defined PY_CMD (
    echo Python 3 was not found on this PC.
    echo Re-run the WorldBox Suite PowerShell installer, or install Python 3 from https://www.python.org/downloads/
    pause
    exit /b 1
)

:: 2. Check if WorldBox Studio is already running on this port
set "ALREADY_RUNNING=0"
if "%PY_CMD%"=="py -3" (
    py -3 -c "import urllib.request; urllib.request.urlopen('%URL%/api/config', timeout=1)" >nul 2>nul
) else (
    "%PY_CMD%" -c "import urllib.request; urllib.request.urlopen('%URL%/api/config', timeout=1)" >nul 2>nul
)
if !errorlevel! equ 0 set "ALREADY_RUNNING=1"

if "!ALREADY_RUNNING!"=="0" (
    echo Starting WorldBox Studio at %URL% ...
    if "%PY_CMD%"=="py -3" (
        start "" /b py -3 -m tools.studio.server --port %PORT%
    ) else (
        start "" /b "%PY_CMD%" -m tools.studio.server --port %PORT%
    )
    timeout /t 2 /nobreak >nul
) else (
    echo WorldBox Studio is already running at %URL% - opening window...
)

:: 3. Open as a clean chromeless desktop-style window in Edge or Chrome if available
if exist "%ProgramFiles(x86)%\Microsoft\Edge\Application\msedge.exe" (
    start "" "%ProgramFiles(x86)%\Microsoft\Edge\Application\msedge.exe" --app=%URL%
) else if exist "%ProgramFiles%\Microsoft\Edge\Application\msedge.exe" (
    start "" "%ProgramFiles%\Microsoft\Edge\Application\msedge.exe" --app=%URL%
) else if exist "%ProgramFiles%\Google\Chrome\Application\chrome.exe" (
    start "" "%ProgramFiles%\Google\Chrome\Application\chrome.exe" --app=%URL%
) else if exist "%LOCALAPPDATA%\Google\Chrome\Application\chrome.exe" (
    start "" "%LOCALAPPDATA%\Google\Chrome\Application\chrome.exe" --app=%URL%
) else (
    start "" %URL%
)

if "!ALREADY_RUNNING!"=="1" exit /b 0

echo WorldBox Studio is running. Keep this window open while using Studio, or close it to stop the server.
pause >nul
