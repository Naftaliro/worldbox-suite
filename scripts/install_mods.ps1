param(
    [string]$WorldBoxDir = ""
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot

if (-not $WorldBoxDir) {
    $Candidates = @(
        "C:\Program Files (x86)\Steam\steamapps\common\worldbox",
        "C:\Program Files\Steam\steamapps\common\worldbox",
        "D:\SteamLibrary\steamapps\common\worldbox",
        "E:\SteamLibrary\steamapps\common\worldbox"
    )
    foreach ($c in $Candidates) {
        if (Test-Path (Join-Path $c "worldbox_Data\StreamingAssets")) {
            $WorldBoxDir = $c
            break
        }
    }
}

if (-not $WorldBoxDir -or -not (Test-Path (Join-Path $WorldBoxDir "worldbox_Data\StreamingAssets"))) {
    Write-Host "Could not auto-detect WorldBox. Pass your Steam install path:" -ForegroundColor Yellow
    Write-Host '  .\scripts\install_mods.ps1 -WorldBoxDir "D:\SteamLibrary\steamapps\common\worldbox"'
    exit 1
}

$LoaderDir = Join-Path $WorldBoxDir "worldbox_Data\StreamingAssets\mods"
$ModsDir   = Join-Path $WorldBoxDir "Mods"
New-Item -ItemType Directory -Force -Path $LoaderDir | Out-Null
New-Item -ItemType Directory -Force -Path $ModsDir   | Out-Null

# Download NeoModLoader.dll if not already installed
$NmlDll = Join-Path $LoaderDir "NeoModLoader.dll"
if (-not (Test-Path $NmlDll)) {
    Write-Host "Downloading latest NeoModLoader.dll..." -ForegroundColor Cyan
    Invoke-WebRequest -Uri "https://github.com/WorldBoxOpenMods/ModLoader/releases/latest/download/NeoModLoader.dll" -OutFile $NmlDll
} else {
    Write-Host "NeoModLoader.dll already present." -ForegroundColor Green
}

$SuiteMods = @("SuiteHub", "CoreScale", "VisualPolish", "GrandWarfare", "LinkedContinents", "EraChronicle", "ApexFactions")
foreach ($mod in $SuiteMods) {
    $Src = Join-Path $RepoRoot "mods\$mod"
    $Dst = Join-Path $ModsDir $mod
    if (Test-Path $Dst) { Remove-Item -Recurse -Force $Dst }
    New-Item -ItemType Directory -Force -Path $Dst | Out-Null

    foreach ($item in @("mod.json", "icon.png", "Code", "Locales", "GameResources")) {
        $SrcItem = Join-Path $Src $item
        if (Test-Path $SrcItem) {
            Copy-Item -Recurse -Force $SrcItem (Join-Path $Dst $item)
        }
    }
    Write-Host "Installed $mod -> $Dst" -ForegroundColor Green
}

Write-Host ""
Write-Host "Done! Open WorldBox -> Settings -> enable 'Experimental Mode', restart the game," -ForegroundColor Cyan
Write-Host "and click the 'WorldBox Suite' tab on the bottom power bar (or press F2-F12)." -ForegroundColor Cyan
