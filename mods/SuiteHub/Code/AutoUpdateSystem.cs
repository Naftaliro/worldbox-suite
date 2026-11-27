using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace SuiteHub
{
    [Serializable]
    public class SuiteHubConfig
    {
        public bool AutoUpdateMods = true;

        public static SuiteHubConfig LoadOrCreate(string modFolder)
        {
            string path = Path.Combine(modFolder, "suitehub_config.json");
            try
            {
                if (File.Exists(path))
                {
                    var loaded = JsonConvert.DeserializeObject<SuiteHubConfig>(File.ReadAllText(path));
                    if (loaded != null) return loaded;
                }
            }
            catch { }

            var cfg = new SuiteHubConfig();
            cfg.Save(modFolder);
            return cfg;
        }

        public void Save(string modFolder)
        {
            try
            {
                File.WriteAllText(
                    Path.Combine(modFolder, "suitehub_config.json"),
                    JsonConvert.SerializeObject(this, Formatting.Indented)
                );
            }
            catch { }
        }
    }

    public static class AutoUpdateSystem
    {
        private const string CommitsApiUrl = "https://api.github.com/repos/Naftaliro/worldbox-suite/commits/main";
        private const string ArchiveZipUrl = "https://github.com/Naftaliro/worldbox-suite/archive/refs/heads/main.zip";

        public static volatile string StatusLabel = "Idle";
        public static volatile bool RestartRecommended = false;

        private static int _busy = 0;
        private static volatile string _pendingToast = null;

        public static string PopPendingToast()
        {
            string msg = _pendingToast;
            if (msg != null)
            {
                _pendingToast = null;
            }
            return msg;
        }

        public static void CheckAndUpdateAsync(string suiteHubFolder, bool forceCheckOnly = false, bool notifyWhenUpToDate = false)
        {
            if (string.IsNullOrEmpty(suiteHubFolder)) return;
            if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0) return;

            Task.Run(() =>
            {
                try
                {
                    StatusLabel = "Checking GitHub for updates...";
                    string remoteSha = FetchLatestMainSha();
                    if (string.IsNullOrEmpty(remoteSha))
                    {
                        StatusLabel = "Offline / GitHub unreachable";
                        return;
                    }

                    string shortRemote = remoteSha.Length >= 7 ? remoteSha.Substring(0, 7) : remoteSha;
                    string shaFile = Path.Combine(suiteHubFolder, ".suite_sha");
                    string localSha = File.Exists(shaFile) ? File.ReadAllText(shaFile).Trim() : "";

                    if (!string.IsNullOrEmpty(localSha) &&
                        string.Equals(localSha, remoteSha, StringComparison.OrdinalIgnoreCase))
                    {
                        StatusLabel = $"Up to date ({shortRemote})";
                        if (notifyWhenUpToDate)
                        {
                            _pendingToast = $"WorldBox Suite is already up to date ({shortRemote}).";
                        }
                        return;
                    }

                    // If local .suite_sha was never stamped yet, check if we should update now
                    if (forceCheckOnly)
                    {
                        StatusLabel = $"Update available ({shortRemote})";
                        _pendingToast = $"WorldBox Suite update ({shortRemote}) available in F5!";
                        return;
                    }

                    StatusLabel = $"Downloading update ({shortRemote})...";
                    bool ok = ApplyUpdateInBackground(suiteHubFolder, remoteSha);
                    if (ok)
                    {
                        RestartRecommended = true;
                        StatusLabel = $"Updated to {shortRemote} (Restart WorldBox)";
                        _pendingToast = $"WorldBox Suite auto-updated ({shortRemote})! Restart WorldBox anytime to apply.";
                        UnityEngine.Debug.Log($"[SuiteHub] Auto-updated WorldBox Suite to {shortRemote}.");
                    }
                    else
                    {
                        StatusLabel = "Auto-update skipped (use Studio or F5)";
                    }
                }
                catch (Exception e)
                {
                    StatusLabel = "Update check skipped";
                    UnityEngine.Debug.LogWarning($"[SuiteHub] Auto-update check skipped: {e.Message}");
                }
                finally
                {
                    Interlocked.Exchange(ref _busy, 0);
                }
            });
        }

        private static string FetchLatestMainSha()
        {
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                var req = (HttpWebRequest)WebRequest.Create(CommitsApiUrl);
                req.Method = "GET";
                req.UserAgent = "WorldBoxSuite-AutoUpdater/1.0";
                req.Accept = "application/vnd.github.v3+json";
                req.Timeout = 10000;
                req.ReadWriteTimeout = 10000;

                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var stream = resp.GetResponseStream())
                {
                    if (stream == null) return null;
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        string json = reader.ReadToEnd();
                        var obj = JObject.Parse(json);
                        return obj["sha"]?.ToString()?.Trim();
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        private static bool ApplyUpdateInBackground(string suiteHubFolder, string remoteSha)
        {
            DirectoryInfo modsDirInfo = Directory.GetParent(suiteHubFolder);
            DirectoryInfo wbRootInfo = modsDirInfo?.Parent;
            if (modsDirInfo == null || wbRootInfo == null) return false;

            string wbRoot = wbRootInfo.FullName;
            bool isWindows = Application.platform == RuntimePlatform.WindowsPlayer ||
                             Application.platform == RuntimePlatform.WindowsEditor ||
                             Path.DirectorySeparatorChar == '\\';

            if (isWindows)
            {
                return RunWindowsSilentUpdater(wbRoot, remoteSha);
            }
            return RunUnixSilentUpdater(wbRoot, remoteSha);
        }

        private static bool RunWindowsSilentUpdater(string wbRoot, string remoteSha)
        {
            string tempScript = Path.Combine(Path.GetTempPath(), "wb_suite_autoupdate_" + Guid.NewGuid().ToString("N") + ".ps1");
            try
            {
                // Self-contained non-destructive PowerShell updater that preserves user *_config.json & chronicles/
                string script = @"
param([string]$WbRoot, [string]$Sha)
$ErrorActionPreference = 'Stop'
$ModsDir = Join-Path $WbRoot 'Mods'
$DisabledDir = Join-Path $WbRoot 'Mods_Disabled'
$StudioRoot = Join-Path $env:LOCALAPPDATA 'WorldBoxSuite'
$Tmp = Join-Path ([System.IO.Path]::GetTempPath()) ('wbs-upd-' + [System.Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $Tmp | Out-Null
try {
    $Zip = Join-Path $Tmp 'main.zip'
    Invoke-WebRequest -UseBasicParsing -Uri '" + ArchiveZipUrl + @"' -OutFile $Zip -TimeoutSec 45
    Expand-Archive -Path $Zip -DestinationPath $Tmp -Force
    $Ext = Join-Path $Tmp 'worldbox-suite-main'
    $Core = @('SuiteHub','CoreScale','VisualPolish','GrandWarfare','LinkedContinents','EraChronicle','ApexFactions','WorldRelics','CoopHost')
    foreach ($m in $Core) {
        $src = Join-Path $Ext ('mods\' + $m)
        if (-not (Test-Path $src)) { continue }
        $dst = if ((Test-Path (Join-Path $DisabledDir $m)) -and -not (Test-Path (Join-Path $ModsDir $m))) { Join-Path $DisabledDir $m } else { Join-Path $ModsDir $m }
        New-Item -ItemType Directory -Force -Path $dst | Out-Null
        foreach ($item in @('mod.json','icon.png','Code','Locales','GameResources','WebClient')) {
            $si = Join-Path $src $item
            $di = Join-Path $dst $item
            if (Test-Path $si) {
                if ((Test-Path $di) -and (Get-Item $si).PSIsContainer) { Remove-Item -Recurse -Force $di }
                Copy-Item -Recurse -Force $si $di
            }
        }
    }
    foreach ($modTarget in @((Join-Path $ModsDir 'ModernAge'), (Join-Path $DisabledDir 'ModernAge'))) {
        if (Test-Path $modTarget) {
            $srcM = Join-Path $Ext 'expansions\ModernAge'
            foreach ($item in @('mod.json','icon.png','Code','Locales','GameResources')) {
                $si = Join-Path $srcM $item
                $di = Join-Path $modTarget $item
                if (Test-Path $si) {
                    if ((Test-Path $di) -and (Get-Item $si).PSIsContainer) { Remove-Item -Recurse -Force $di }
                    Copy-Item -Recurse -Force $si $di
                }
            }
        }
    }
    if (Test-Path $StudioRoot) {
        foreach ($d in @('tools','mods','expansions','scripts')) {
            $sd = Join-Path $Ext $d
            $dd = Join-Path $StudioRoot $d
            if (Test-Path $sd) {
                if (Test-Path $dd) { Remove-Item -Recurse -Force $dd }
                Copy-Item -Recurse -Force $sd $dd
            }
        }
        Set-Content -Path (Join-Path $StudioRoot '.suite_sha') -Value $Sha -NoNewline
    }
    $hubDir = if (Test-Path (Join-Path $ModsDir 'SuiteHub')) { Join-Path $ModsDir 'SuiteHub' } else { Join-Path $DisabledDir 'SuiteHub' }
    if (Test-Path $hubDir) {
        Set-Content -Path (Join-Path $hubDir '.suite_sha') -Value $Sha -NoNewline
    }
} finally {
    if (Test-Path $Tmp) { Remove-Item -Recurse -Force $Tmp -ErrorAction SilentlyContinue }
}
";
                File.WriteAllText(tempScript, script, Encoding.UTF8);

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File \"{tempScript}\" -WbRoot \"{wbRoot}\" -Sha \"{remoteSha}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using (var proc = Process.Start(psi))
                {
                    if (proc == null) return false;
                    if (!proc.WaitForExit(90000))
                    {
                        try { proc.Kill(); } catch { }
                        return false;
                    }
                    return proc.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
            finally
            {
                try { if (File.Exists(tempScript)) File.Delete(tempScript); } catch { }
            }
        }

        private static bool RunUnixSilentUpdater(string wbRoot, string remoteSha)
        {
            string pyScript = Path.Combine(Path.GetTempPath(), "wb_suite_upd_" + Guid.NewGuid().ToString("N") + ".py");
            try
            {
                string code = @"
import os, shutil, sys, tempfile, urllib.request, zipfile
from pathlib import Path
wb_root = Path(sys.argv[1])
sha = sys.argv[2]
mods_dir = wb_root / 'Mods'
dis_dir = wb_root / 'Mods_Disabled'
with tempfile.TemporaryDirectory() as tmp:
    zp = Path(tmp) / 'main.zip'
    urllib.request.urlretrieve('" + ArchiveZipUrl + @"', zp)
    with zipfile.ZipFile(zp) as zf:
        zf.extractall(tmp)
    ext = Path(tmp) / 'worldbox-suite-main'
    for m in ['SuiteHub','CoreScale','VisualPolish','GrandWarfare','LinkedContinents','EraChronicle','ApexFactions','WorldRelics','CoopHost']:
        src = ext / 'mods' / m
        if not src.exists(): continue
        dst = (dis_dir / m) if ((dis_dir / m).exists() and not (mods_dir / m).exists()) else (mods_dir / m)
        dst.mkdir(parents=True, exist_ok=True)
        for item in ('mod.json','icon.png','Code','Locales','GameResources','WebClient'):
            si, di = src / item, dst / item
            if si.is_dir():
                if di.exists(): shutil.rmtree(di)
                shutil.copytree(si, di)
            elif si.is_file():
                shutil.copy2(si, di)
    for mt in (mods_dir / 'ModernAge', dis_dir / 'ModernAge'):
        if mt.exists():
            srcm = ext / 'expansions' / 'ModernAge'
            for item in ('mod.json','icon.png','Code','Locales','GameResources'):
                si, di = srcm / item, mt / item
                if si.is_dir():
                    if di.exists(): shutil.rmtree(di)
                    shutil.copytree(si, di)
                elif si.is_file():
                    shutil.copy2(si, di)
    hub = (mods_dir / 'SuiteHub') if (mods_dir / 'SuiteHub').exists() else (dis_dir / 'SuiteHub')
    if hub.exists():
        (hub / '.suite_sha').write_text(sha, encoding='utf-8')
";
                File.WriteAllText(pyScript, code, Encoding.UTF8);
                var psi = new ProcessStartInfo
                {
                    FileName = "python3",
                    Arguments = $"\"{pyScript}\" \"{wbRoot}\" \"{remoteSha}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using (var proc = Process.Start(psi))
                {
                    if (proc == null) return false;
                    if (!proc.WaitForExit(90000))
                    {
                        try { proc.Kill(); } catch { }
                        return false;
                    }
                    return proc.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
            finally
            {
                try { if (File.Exists(pyScript)) File.Delete(pyScript); } catch { }
            }
        }
    }
}
