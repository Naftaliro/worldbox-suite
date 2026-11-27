# Silent background auto-updater for WorldBox Suite (Windows PowerShell)
# Updates installed WorldBox mods + WorldBox Studio while preserving user *_config.json & chronicles/

param(
    [string]$WorldBoxDir = "",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

function Find-WorldBoxDir {
    $candidates = [System.Collections.Generic.List[string]]::new()
    $steamRoots = @("C:\Program Files (x86)\Steam", "C:\Program Files\Steam")
    try {
        $regPath = (Get-ItemProperty -Path "HKCU:\Software\Valve\Steam" -Name "SteamPath" -ErrorAction SilentlyContinue).SteamPath
        if ($regPath) { $steamRoots += ($regPath -replace "/", "\") }
    } catch {}

    foreach ($steam in ($steamRoots | Select-Object -Unique)) {
        $candidates.Add((Join-Path $steam "steamapps\common\worldbox"))
        $vdf = Join-Path $steam "steamapps\libraryfolders.vdf"
        if (Test-Path $vdf) {
            foreach ($line in (Get-Content $vdf -ErrorAction SilentlyContinue)) {
                if ($line -match '"path"\s+"([^"]+)"') {
                    $lib = $Matches[1] -replace '\\\\', '\'
                    $candidates.Add((Join-Path $lib "steamapps\common\worldbox"))
                }
            }
        }
    }
    foreach ($d in @("D", "E", "F", "G")) {
        $candidates.Add("${d}:\SteamLibrary\steamapps\common\worldbox")
        $candidates.Add("${d}:\Steam\steamapps\common\worldbox")
    }
    foreach ($c in ($candidates | Select-Object -Unique)) {
        if (Test-Path (Join-Path $c "worldbox_Data\StreamingAssets")) { return $c }
    }
    return $null
}

$StudioRoot = Join-Path $env:LOCALAPPDATA "WorldBoxSuite"
$InfoPath   = Join-Path $StudioRoot "install_info.json"

if (-not $WorldBoxDir -and (Test-Path $InfoPath)) {
    try {
        $info = Get-Content $InfoPath -Raw | ConvertFrom-Json
        if ($info.worldbox_dir -and (Test-Path (Join-Path $info.worldbox_dir "worldbox_Data\StreamingAssets"))) {
            $WorldBoxDir = $info.worldbox_dir
        }
    } catch {}
}

if (-not $WorldBoxDir) {
    $WorldBoxDir = Find-WorldBoxDir
}
if (-not $WorldBoxDir) { exit 0 }

$ModsDir     = Join-Path $WorldBoxDir "Mods"
$DisabledDir = Join-Path $WorldBoxDir "Mods_Disabled"
$HubDir      = if (Test-Path (Join-Path $ModsDir "SuiteHub")) { Join-Path $ModsDir "SuiteHub" } elseif (Test-Path (Join-Path $DisabledDir "SuiteHub")) { Join-Path $DisabledDir "SuiteHub" } else { Join-Path $ModsDir "SuiteHub" }
$ShaFile     = Join-Path $HubDir ".suite_sha"
$LocalSha    = if (Test-Path $ShaFile) { (Get-Content $ShaFile -Raw).Trim() } else { "" }

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$RemoteSha = ""
try {
    $resp = Invoke-RestMethod -UseBasicParsing -Uri "https://api.github.com/repos/Naftaliro/worldbox-suite/commits/main" -Headers @{ "User-Agent" = "WorldBoxSuite-AutoUpdater" } -TimeoutSec 10
    $RemoteSha = [string]$resp.sha
} catch {
    exit 0
}

if (-not $RemoteSha) { exit 0 }
if (-not $Force -and ($LocalSha -eq $RemoteSha) -and (Test-Path (Join-Path $HubDir "mod.json"))) {
    exit 0
}

$TmpRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("worldbox-suite-upd-" + [System.Guid]::NewGuid().ToString("N"))
$ZipPath = Join-Path $TmpRoot "suite.zip"
New-Item -ItemType Directory -Force -Path $TmpRoot | Out-Null

try {
    Invoke-WebRequest -UseBasicParsing -Uri "https://github.com/Naftaliro/worldbox-suite/archive/refs/heads/main.zip" -OutFile $ZipPath -TimeoutSec 45
    Expand-Archive -Path $ZipPath -DestinationPath $TmpRoot -Force
    $Extracted = Join-Path $TmpRoot "worldbox-suite-main"

    $SuiteMods = @("SuiteHub", "CoreScale", "VisualPolish", "GrandWarfare", "LinkedContinents", "EraChronicle", "ApexFactions", "WorldRelics", "CoopHost")
    foreach ($mod in $SuiteMods) {
        $Src = Join-Path $Extracted "mods\$mod"
        if (-not (Test-Path $Src)) { continue }
        $Dst = if ((Test-Path (Join-Path $DisabledDir $mod)) -and -not (Test-Path (Join-Path $ModsDir $mod))) { Join-Path $DisabledDir $mod } else { Join-Path $ModsDir $mod }
        New-Item -ItemType Directory -Force -Path $Dst | Out-Null

        foreach ($item in @("mod.json", "icon.png", "Code", "Locales", "GameResources", "WebClient")) {
            $SrcItem = Join-Path $Src $item
            $DstItem = Join-Path $Dst $item
            if (Test-Path $SrcItem) {
                if ((Test-Path $DstItem) -and (Get-Item $SrcItem).PSIsContainer) { Remove-Item -Recurse -Force $DstItem }
                Copy-Item -Recurse -Force $SrcItem $DstItem
            }
        }
    }

    foreach ($modernDst in @((Join-Path $ModsDir "ModernAge"), (Join-Path $DisabledDir "ModernAge"))) {
        if (Test-Path $modernDst) {
            $SrcM = Join-Path $Extracted "expansions\ModernAge"
            foreach ($item in @("mod.json", "icon.png", "Code", "Locales", "GameResources")) {
                $SrcItem = Join-Path $SrcM $item
                $DstItem = Join-Path $modernDst $item
                if (Test-Path $SrcItem) {
                    if ((Test-Path $DstItem) -and (Get-Item $SrcItem).PSIsContainer) { Remove-Item -Recurse -Force $DstItem }
                    Copy-Item -Recurse -Force $SrcItem $DstItem
                }
            }
        }
    }

    if (Test-Path $StudioRoot) {
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
            if (Test-Path $srcFile) { Copy-Item -Force $srcFile (Join-Path $StudioRoot $fileName) }
        }
        Set-Content -Path (Join-Path $StudioRoot ".suite_sha") -Value $RemoteSha -NoNewline
    }

    $FinalHub = if (Test-Path (Join-Path $ModsDir "SuiteHub")) { Join-Path $ModsDir "SuiteHub" } else { Join-Path $DisabledDir "SuiteHub" }
    if (Test-Path $FinalHub) {
        Set-Content -Path (Join-Path $FinalHub ".suite_sha") -Value $RemoteSha -NoNewline
    }
} finally {
    if (Test-Path $TmpRoot) {
        Remove-Item -Recurse -Force $TmpRoot -ErrorAction SilentlyContinue
    }
}
