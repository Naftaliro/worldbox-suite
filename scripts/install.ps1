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

Write-Host ""
Write-Host "=== WorldBox Suite Installer ===" -ForegroundColor Magenta

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

$LoaderDir = Join-Path $WorldBoxDir "worldbox_Data\StreamingAssets\mods"
$ModsDir   = Join-Path $WorldBoxDir "Mods"
New-Item -ItemType Directory -Force -Path $LoaderDir | Out-Null
New-Item -ItemType Directory -Force -Path $ModsDir   | Out-Null

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
    $SuiteMods = @("SuiteHub", "CoreScale", "VisualPolish", "GrandWarfare", "LinkedContinents", "EraChronicle", "ApexFactions", "WorldRelics")

    # Clean up legacy standalone Arsenal folder if present (folded into ModernAge Tier 1)
    $LegacyArsenal = Join-Path $ModsDir "Arsenal"
    if (Test-Path $LegacyArsenal) { Remove-Item -Recurse -Force $LegacyArsenal }

    foreach ($mod in $SuiteMods) {
        $Src = Join-Path $Extracted "mods\$mod"
        $Dst = Join-Path $ModsDir $mod
        if (-not (Test-Path $Src)) { continue }
        if (Test-Path $Dst) { Remove-Item -Recurse -Force $Dst }
        New-Item -ItemType Directory -Force -Path $Dst | Out-Null

        foreach ($item in @("mod.json", "icon.png", "Code", "Locales", "GameResources")) {
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
} finally {
    if (Test-Path $TmpRoot) {
        Remove-Item -Recurse -Force $TmpRoot -ErrorAction SilentlyContinue
    }
}

Write-Host ""
if ($env:WORLDBOX_MODERN -eq "1" -or $env:WORLDBOX_MODERN -eq "true") {
    Write-Host "Core pack (8 mods) + ModernAge Expansion installed!" -ForegroundColor Cyan
} else {
    Write-Host "Classic Core pack (8 mods) installed!" -ForegroundColor Cyan
    Write-Host "Want Tier 1-3 firearms, tanks, jets, and modern geopolitics too? Run:" -ForegroundColor DarkGray
    Write-Host '  $env:WORLDBOX_MODERN=1; irm https://raw.githubusercontent.com/Naftaliro/worldbox-suite/main/scripts/install.ps1 | iex' -ForegroundColor Gray
}
Write-Host "Next steps:" -ForegroundColor White
Write-Host "  1. Open WorldBox -> Settings -> turn ON 'Experimental Mode'" -ForegroundColor Gray
Write-Host "  2. Restart WorldBox" -ForegroundColor Gray
Write-Host "  3. Click the 'WorldBox Suite' tab on the bottom bar (or press F2-F12 / Alt+R / Alt+M)" -ForegroundColor Gray
Write-Host ""
