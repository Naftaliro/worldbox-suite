# Standalone WorldBox Suite installer for Windows PowerShell
# Usage: irm https://raw.githubusercontent.com/Naftaliro/worldbox-suite/main/scripts/install.ps1 | iex

$ErrorActionPreference = "Stop"

function Find-WorldBoxDir {
    $candidates = [System.Collections.Generic.List[string]]::new()

    # Check common Steam install locations and parse libraryfolders.vdf for extra drives
    $steamRoots = @(
        "C:\Program Files (x86)\Steam",
        "C:\Program Files\Steam"
    )
    try {
        $regPath = (Get-ItemProperty -Path "HKCU:\Software\Valve\Steam" -Name "SteamPath" -ErrorAction SilentlyContinue).SteamPath
        if ($regPath) { $steamRoots += ($regPath -replace "/", "\") }
    } catch {}

    foreach ($steam in ($steamRoots | Select-Object -Unique)) {
        $candidates.Add((Join-Path $steam "steamapps\common\worldbox"))
        $vdf = Join-Path $steam "steamapps\libraryfolders.vdf"
        if (Test-Path $vdf) {
            $lines = Get-Content $vdf -ErrorAction SilentlyContinue
            foreach ($line in $lines) {
                if ($line -match '"path"\s+"([^"]+)"') {
                    $lib = $Matches[1] -replace '\\\\', '\'
                    $candidates.Add((Join-Path $lib "steamapps\common\worldbox"))
                }
            }
        }
    }

    foreach ($drive in @("D", "E", "F", "G")) {
        $candidates.Add("${drive}:\SteamLibrary\steamapps\common\worldbox")
        $candidates.Add("${drive}:\Steam\steamapps\common\worldbox")
    }

    foreach ($c in ($candidates | Select-Object -Unique)) {
        if (Test-Path (Join-Path $c "worldbox_Data\StreamingAssets")) {
            return $c
        }
    }
    return $null
}

function Test-RealPython {
    param([string]$ExePath)
    if (-not $ExePath) { return $false }
    try {
        & $ExePath -c "import sys; raise SystemExit(0 if sys.version_info >= (3, 9) else 1)" 2>$null
        return ($LASTEXITCODE -eq 0)
    } catch {
        return $false
    }
}

function Find-SystemPython {
    foreach ($cmd in @("python", "python3")) {
        $found = Get-Command $cmd -ErrorAction SilentlyContinue
        if ($found -and $found.Source -and ($found.Source -notlike "*\WindowsApps\*")) {
            if (Test-RealPython $found.Source) { return $found.Source }
        }
    }
    $pyDirs = Get-ChildItem "$env:LOCALAPPDATA\Programs\Python\Python3*", "C:\Python3*", "C:\Program Files\Python3*" -Directory -ErrorAction SilentlyContinue | Sort-Object Name -Descending
    foreach ($d in $pyDirs) {
        $exe = Join-Path $d.FullName "python.exe"
        if ((Test-Path $exe) -and (Test-RealPython $exe)) { return $exe }
    }
    return $null
}

function Ensure-EmbeddedPython {
    param([string]$StudioRoot)
    $PyDir = Join-Path $StudioRoot "python"
    $PyExe = Join-Path $PyDir "python.exe"
    if ((Test-Path $PyExe) -and (Test-RealPython $PyExe)) {
        return $PyExe
    }

    Write-Host "Installing portable Python 3.12 runtime for WorldBox Studio (~11 MB, no admin needed)..." -ForegroundColor Cyan
    $PyZip = Join-Path $StudioRoot "python_embed.zip"
    $PyUrl = "https://www.python.org/ftp/python/3.12.8/python-3.12.8-embed-amd64.zip"
    Invoke-WebRequest -UseBasicParsing -Uri $PyUrl -OutFile $PyZip

    if (Test-Path $PyDir) { Remove-Item -Recurse -Force $PyDir }
    New-Item -ItemType Directory -Force -Path $PyDir | Out-Null
    Expand-Archive -Path $PyZip -DestinationPath $PyDir -Force
    Remove-Item -Force $PyZip -ErrorAction SilentlyContinue

    # Allow embedded Python to import tools.* from the parent WorldBoxSuite folder
    $pthFile = Get-ChildItem $PyDir -Filter "python*._pth" | Select-Object -First 1
    if ($pthFile) {
        $lines = Get-Content $pthFile.FullName
        if ($lines -notcontains "..") {
            Add-Content -Path $pthFile.FullName -Value ".."
        }
    }
    return $PyExe
}

function New-StudioIcon {
    param([string]$PngPath, [string]$IcoPath)
    if (-not (Test-Path $PngPath)) { return $false }
    try {
        Add-Type -AssemblyName System.Drawing
        $bmp = New-Object System.Drawing.Bitmap($PngPath)
        $resized = New-Object System.Drawing.Bitmap($bmp, 64, 64)
        $msPng = New-Object System.IO.MemoryStream
        $resized.Save($msPng, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngBytes = $msPng.ToArray()

        $fs = [System.IO.File]::Open($IcoPath, [System.IO.FileMode]::Create)
        $bw = New-Object System.IO.BinaryWriter($fs)
        # Standard ICO header with embedded 64x64 PNG
        $bw.Write([uint16]0)
        $bw.Write([uint16]1)
        $bw.Write([uint16]1)
        $bw.Write([byte]64)
        $bw.Write([byte]64)
        $bw.Write([byte]0)
        $bw.Write([byte]0)
        $bw.Write([uint16]1)
        $bw.Write([uint16]32)
        $bw.Write([uint32]$pngBytes.Length)
        $bw.Write([uint32]22)
        $bw.Write($pngBytes)
        $bw.Close()
        $fs.Close()
        $resized.Dispose()
        $bmp.Dispose()
        return $true
    } catch {
        return $false
    }
}

function New-ShortcutFile {
    param([string]$LnkPath, [string]$TargetPath, [string]$WorkingDir, [string]$IconPath)
    try {
        $ws = New-Object -ComObject WScript.Shell
        $sc = $ws.CreateShortcut($LnkPath)
        $sc.TargetPath = $TargetPath
        $sc.WorkingDirectory = $WorkingDir
        $sc.Description = "WorldBox Studio - Maps, Mod Profiles, Stitcher & Diagnostics"
        $sc.WindowStyle = 7 # Minimized terminal so Studio app opens cleanly
        if ($IconPath -and (Test-Path $IconPath)) {
            $sc.IconLocation = "$IconPath,0"
        }
        $sc.Save()
        return $true
    } catch {
        return $false
    }
}

Write-Host ""
Write-Host "=== WorldBox Suite Installer ===" -ForegroundColor Magenta

# Warn if WorldBox is currently running
$wbProc = Get-Process -Name "worldbox" -ErrorAction SilentlyContinue
if ($wbProc) {
    Write-Host "WorldBox is currently running. Mods need a game restart to compile and load." -ForegroundColor Yellow
    if ($env:WORLDBOX_CLOSE_RUNNING -eq "1") {
        $wbProc | Stop-Process -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 1
        Write-Host "Closed running WorldBox instance." -ForegroundColor DarkGray
    } else {
        Write-Host "Tip: Close WorldBox now or restart it right after this finishes." -ForegroundColor Yellow
    }
}

$WorldBoxDir = Find-WorldBoxDir
if (-not $WorldBoxDir) {
    Write-Host "Could not auto-detect WorldBox. Paste your WorldBox folder path (e.g. D:\SteamLibrary\steamapps\common\worldbox):" -ForegroundColor Yellow
    $WorldBoxDir = (Read-Host "WorldBox folder").Trim().Trim('"')
}

if (-not $WorldBoxDir -or -not (Test-Path (Join-Path $WorldBoxDir "worldbox_Data\StreamingAssets"))) {
    Write-Host "Error: Could not find worldbox_Data\StreamingAssets inside '$WorldBoxDir'." -ForegroundColor Red
    return
}

Write-Host "Found WorldBox: $WorldBoxDir" -ForegroundColor Green

$LoaderDir  = Join-Path $WorldBoxDir "worldbox_Data\StreamingAssets\mods"
$ModsDir    = Join-Path $WorldBoxDir "Mods"
$StudioRoot = Join-Path $env:LOCALAPPDATA "WorldBoxSuite"
New-Item -ItemType Directory -Force -Path $LoaderDir  | Out-Null
New-Item -ItemType Directory -Force -Path $ModsDir    | Out-Null
New-Item -ItemType Directory -Force -Path $StudioRoot | Out-Null

# 1. Install NeoModLoader.dll if missing
$NmlDll = Join-Path $LoaderDir "NeoModLoader.dll"
if (-not (Test-Path $NmlDll)) {
    Write-Host "Downloading NeoModLoader.dll..." -ForegroundColor Cyan
    Invoke-WebRequest -UseBasicParsing -Uri "https://github.com/WorldBoxOpenMods/ModLoader/releases/latest/download/NeoModLoader.dll" -OutFile $NmlDll
    Write-Host "Installed NeoModLoader.dll" -ForegroundColor Green
} else {
    Write-Host "NeoModLoader.dll already installed." -ForegroundColor DarkGray
}

# 2. Download latest worldbox-suite archive from GitHub
$TmpRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("worldbox-suite-" + [System.Guid]::NewGuid().ToString("N"))
$ZipPath = Join-Path $TmpRoot "suite.zip"
New-Item -ItemType Directory -Force -Path $TmpRoot | Out-Null

try {
    Write-Host "Downloading latest worldbox-suite from GitHub..." -ForegroundColor Cyan
    Invoke-WebRequest -UseBasicParsing -Uri "https://github.com/Naftaliro/worldbox-suite/archive/refs/heads/main.zip" -OutFile $ZipPath
    Expand-Archive -Path $ZipPath -DestinationPath $TmpRoot -Force

    $Extracted = Join-Path $TmpRoot "worldbox-suite-main"
    $SuiteMods = @("SuiteHub", "CoreScale", "VisualPolish", "GrandWarfare", "LinkedContinents", "EraChronicle", "ApexFactions", "WorldRelics", "CoopHost")

    # Clean up legacy standalone Arsenal folder if present (folded into ModernAge Tier 1)
    $LegacyArsenal = Join-Path $ModsDir "Arsenal"
    if (Test-Path $LegacyArsenal) { Remove-Item -Recurse -Force $LegacyArsenal }

    foreach ($mod in $SuiteMods) {
        $Src = Join-Path $Extracted "mods\$mod"
        $Dst = Join-Path $ModsDir $mod
        if (-not (Test-Path $Src)) { continue }
        if (Test-Path $Dst) { Remove-Item -Recurse -Force $Dst }
        New-Item -ItemType Directory -Force -Path $Dst | Out-Null

        foreach ($item in @("mod.json", "icon.png", "Code", "Locales", "GameResources", "WebClient")) {
            $SrcItem = Join-Path $Src $item
            if (Test-Path $SrcItem) {
                Copy-Item -Recurse -Force $SrcItem (Join-Path $Dst $item)
            }
        }
        Write-Host "  + Installed $mod" -ForegroundColor Green
    }

    # Opt-in ModernAge expansion (set $env:WORLDBOX_MODERN=1 before running to include)
    if ($env:WORLDBOX_MODERN -eq "1" -or $env:WORLDBOX_MODERN -eq "true") {
        $Src = Join-Path $Extracted "expansions\ModernAge"
        $Dst = Join-Path $ModsDir "ModernAge"
        if (Test-Path $Src) {
            if (Test-Path $Dst) { Remove-Item -Recurse -Force $Dst }
            New-Item -ItemType Directory -Force -Path $Dst | Out-Null
            foreach ($item in @("mod.json", "icon.png", "Code", "Locales", "GameResources")) {
                $SrcItem = Join-Path $Src $item
                if (Test-Path $SrcItem) {
                    Copy-Item -Recurse -Force $SrcItem (Join-Path $Dst $item)
                }
            }
            Write-Host "  + Installed ModernAge Expansion (Tier 1-3)" -ForegroundColor Cyan
        }
    }

    # 3. Install WorldBox Studio locally (preserving local/ saves, backups & config)
    Write-Host "Installing WorldBox Studio -> $StudioRoot ..." -ForegroundColor Cyan
    foreach ($dirName in @("tools", "mods", "expansions", "scripts")) {
        $srcDir = Join-Path $Extracted $dirName
        $dstDir = Join-Path $StudioRoot $dirName
        if (Test-Path $srcDir) {
            if (Test-Path $dstDir) { Remove-Item -Recurse -Force $dstDir }
            Copy-Item -Recurse -Force $srcDir $dstDir
        }
    }
    foreach ($fileName in @("WorldBox Studio.bat", "install.bat", "README.md", "LICENSE")) {
        $srcFile = Join-Path $Extracted $fileName
        if (Test-Path $srcFile) {
            Copy-Item -Force $srcFile (Join-Path $StudioRoot $fileName)
        }
    }
} finally {
    if (Test-Path $TmpRoot) {
        Remove-Item -Recurse -Force $TmpRoot -ErrorAction SilentlyContinue
    }
}

# 4. Ensure Python 3 runtime is ready (use existing system Python or bundle portable Python)
$sysPy = Find-SystemPython
if ($sysPy) {
    Write-Host "Detected Python 3: $sysPy" -ForegroundColor DarkGray
} else {
    $embPy = Ensure-EmbeddedPython -StudioRoot $StudioRoot
    Write-Host "Installed portable Python runtime: $embPy" -ForegroundColor Green
}

# 5. Create Desktop & Start Menu shortcuts for WorldBox Studio
$BatPath   = Join-Path $StudioRoot "WorldBox Studio.bat"
$PngIcon   = Join-Path $StudioRoot "mods\SuiteHub\icon.png"
$IcoPath   = Join-Path $StudioRoot "studio.ico"
$hasIcon   = New-StudioIcon -PngPath $PngIcon -IcoPath $IcoPath
$IconUse   = if ($hasIcon) { $IcoPath } else { "" }

$DesktopDir   = [Environment]::GetFolderPath("Desktop")
$StartMenuDir = Join-Path ([Environment]::GetFolderPath("Programs")) "WorldBox Suite"
New-Item -ItemType Directory -Force -Path $StartMenuDir | Out-Null

$DesktopLnk   = Join-Path $DesktopDir "WorldBox Studio.lnk"
$StartMenuLnk = Join-Path $StartMenuDir "WorldBox Studio.lnk"
if (New-ShortcutFile -LnkPath $DesktopLnk -TargetPath $BatPath -WorkingDir $StudioRoot -IconPath $IconUse) {
    Write-Host "  + Created Desktop shortcut: WorldBox Studio" -ForegroundColor Green
}
if (New-ShortcutFile -LnkPath $StartMenuLnk -TargetPath $BatPath -WorkingDir $StudioRoot -IconPath $IconUse) {
    Write-Host "  + Created Start Menu shortcut: WorldBox Studio" -ForegroundColor Green
}

Write-Host ""
if ($env:WORLDBOX_MODERN -eq "1" -or $env:WORLDBOX_MODERN -eq "true") {
    Write-Host "Core pack (9 mods) + ModernAge Expansion + WorldBox Studio installed!" -ForegroundColor Cyan
} else {
    Write-Host "Classic Core pack (9 mods) + WorldBox Studio installed!" -ForegroundColor Cyan
    Write-Host "Want Tier 1-3 firearms, tanks, jets, and modern geopolitics too? Toggle 'Modern Warfare' in WorldBox Studio, or run:" -ForegroundColor DarkGray
    Write-Host '  $env:WORLDBOX_MODERN=1; irm https://raw.githubusercontent.com/Naftaliro/worldbox-suite/main/scripts/install.ps1 | iex' -ForegroundColor Gray
}
Write-Host ""
Write-Host "Next steps:" -ForegroundColor White
Write-Host "  1. Open WorldBox -> Settings -> turn ON 'Experimental Mode' (first time only)" -ForegroundColor Gray
Write-Host "  2. Restart WorldBox so NeoModLoader compiles the mods" -ForegroundColor Gray
Write-Host "  3. In-game: click the 'WorldBox Suite' tab on the bottom bar (or press F2-F12 / Alt+R / Alt+C / Alt+M)" -ForegroundColor Gray
Write-Host "  4. Double-click 'WorldBox Studio' on your Desktop to manage mods, switch Classic/Modern profiles, stitch worlds & view diagnostics" -ForegroundColor Gray
Write-Host ""
