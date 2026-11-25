using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using NeoModLoader.api;
using NeoModLoader.General;
using Newtonsoft.Json;
using UnityEngine;

namespace CoreScale
{
    public class Main : MonoBehaviour, IMod
    {
        public static Main Instance { get; private set; }
        public static CoreScaleConfig Settings { get; private set; }
        public static int CurrentLodTier { get; private set; }
        public static int LivePopulation { get; private set; }

        private ModDeclare _declare;
        private GameObject _gameObject;
        private Harmony _harmony;

        private bool _showOverlay = false;
        private GuiOverlayRunner _panelRunner;
        private float _fpsSmoothed = 60f;
        private float _statsTimer = 0f;
        private float _battalionTimer = 0f;
        private Rect _windowRect = new Rect(18, 86, 305, 315);

        public ModDeclare GetDeclaration() => _declare;
        public GameObject GetGameObject() => _gameObject;
        public string GetUrl() => "https://github.com/naftaliro/worldbox-suite";

        public void OnLoad(ModDeclare pModDecl, GameObject pGameObject)
        {
            Instance = this;
            _declare = pModDecl;
            _gameObject = pGameObject;
            _panelRunner = pGameObject.AddComponent<GuiOverlayRunner>();
            _panelRunner.Configure(true, DrawGui);

            Settings = CoreScaleConfig.LoadOrCreate(_declare.FolderPath);

            try
            {
                _harmony = new Harmony(_declare.UID);
                _harmony.PatchAll(Assembly.GetExecutingAssembly());

                // If AssetManager already initialized before mod load, register sizes immediately
                if (Settings.EnableCustomMapSizes && AssetManager.map_sizes != null)
                {
                    MapSizePatches.RegisterCustomSizes(AssetManager.map_sizes);
                }

                LoadLocales(_declare.FolderPath);
                SimulationLodPatches.ApplyParallelSettings();
                Debug.Log($"[CoreScale] Loaded v{_declare.Version} (threads={Environment.ProcessorCount})");
            }
            catch (Exception e)
            {
                Debug.LogError($"[CoreScale] Init failed: {e}");
            }
        }

        private static void LoadLocales(string modFolder)
        {
            try
            {
                string enPath = Path.Combine(modFolder, "Locales", "en.json");
                if (!File.Exists(enPath)) return;
                var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(enPath));
                if (dict == null || dict.Count == 0) return;
                foreach (var kv in dict)
                {
                    if (!string.IsNullOrEmpty(kv.Key)) LM.AddToCurrentLocale(kv.Key, kv.Value);
                }
                LM.ApplyLocale(false);
            }
            catch { }
        }

        public void OnUnload()
        {
            _harmony?.UnpatchSelf();
            Debug.Log("[CoreScale] Unloaded and unpatched.");
        }

        public void ToggleHud()
        {
            _showOverlay = !_showOverlay;
            if (_panelRunner != null) _panelRunner.enabled = _showOverlay;
        }

        public bool GetState(string key)
        {
            if (Settings == null) return false;
            switch (key)
            {
                case "lod": return Settings.EnableAdaptiveLOD;
                case "anim_cull": return Settings.SkipAnimationsInStrategicZoom;
                case "no_shadows": return Settings.ForceDisableShadows;
                case "battalions": return Settings.EnableBattalionCompression;
                default: return false;
            }
        }

        public string ToggleAdaptiveLod()
        {
            if (Settings == null) return "";
            Settings.EnableAdaptiveLOD = !Settings.EnableAdaptiveLOD;
            Settings.Save(_declare.FolderPath);
            RefreshLodState();
            return $"CoreScale Adaptive LOD: {(Settings.EnableAdaptiveLOD ? "ON" : "OFF")}";
        }

        public string ToggleAnimCull()
        {
            if (Settings == null) return "";
            Settings.SkipAnimationsInStrategicZoom = !Settings.SkipAnimationsInStrategicZoom;
            Settings.Save(_declare.FolderPath);
            return $"Strategic Anim Cull: {(Settings.SkipAnimationsInStrategicZoom ? "ON" : "OFF")}";
        }

        public string ToggleShadows()
        {
            if (Settings == null) return "";
            Settings.ForceDisableShadows = !Settings.ForceDisableShadows;
            Settings.Save(_declare.FolderPath);
            return $"Shadows: {(Settings.ForceDisableShadows ? "OFF (Max FPS)" : "AUTO")}";
        }

        public string ToggleAutoBattalions()
        {
            if (Settings == null) return "";
            Settings.EnableBattalionCompression = !Settings.EnableBattalionCompression;
            Settings.Save(_declare.FolderPath);
            return $"Auto Army Battalions: {(Settings.EnableBattalionCompression ? "ON" : "OFF")}";
        }

        public string CompressArmiesNow()
        {
            int before = BattalionCompressionSystem.TotalSoldiersConsolidated;
            BattalionCompressionSystem.CompressMegaArmies(force: true);
            int delta = BattalionCompressionSystem.TotalSoldiersConsolidated - before;
            return delta > 0 ? $"Compressed {delta} soldiers into veteran champions!" : "No oversized armies needed compression.";
        }

        private void Update()
        {
            if (Settings == null) return;

            if (Input.GetKeyDown(Settings.OverlayToggleKey))
            {
                ToggleHud();
            }

            if (_showOverlay)
            {
                float dt = Time.unscaledDeltaTime;
                if (dt > 0.0001f) _fpsSmoothed = Mathf.Lerp(_fpsSmoothed, 1f / dt, 0.08f);
            }

            _statsTimer += Time.unscaledDeltaTime;
            if (_statsTimer >= 0.75f)
            {
                _statsTimer = 0f;
                RefreshLodState();
            }

            if (Settings.EnableBattalionCompression && !Config.paused)
            {
                _battalionTimer += Time.deltaTime;
                if (_battalionTimer >= 8.0f)
                {
                    _battalionTimer = 0f;
                    BattalionCompressionSystem.CompressMegaArmies(force: false);
                }
            }
        }

        private void RefreshLodState()
        {
            var world = MapBox.instance;
            if (world == null || world.units == null)
            {
                LivePopulation = 0;
                CurrentLodTier = 0;
                return;
            }

            LivePopulation = world.units.units_only_alive != null
                ? world.units.units_only_alive.Count
                : 0;

            if (!Settings.EnableAdaptiveLOD)
            {
                CurrentLodTier = 0;
                return;
            }

            if (LivePopulation >= Settings.LodTier3Population) CurrentLodTier = 3;
            else if (LivePopulation >= Settings.LodTier2Population) CurrentLodTier = 2;
            else if (LivePopulation >= Settings.LodTier1Population) CurrentLodTier = 1;
            else CurrentLodTier = 0;
        }

        private void DrawGui()
        {
            if (!_showOverlay || Settings == null) return;
            GameUiStyle.BeginFrame();
            _windowRect = GUI.Window(941203, _windowRect, DrawOverlayWindow, "CoreScale Perf HUD (F8)", GameUiStyle.WindowStyle);
            GameUiStyle.DrawTooltip(949002);
        }

        private void DrawOverlayWindow(int id)
        {
            GameUiStyle.DrawCloseButton(_windowRect, ToggleHud);
            string mapDims = MapBox.width > 0 ? $"{MapBox.width}x{MapBox.height} tiles" : "No map";

            GameUiStyle.Label($"FPS: {_fpsSmoothed:0.0}   |   Pop: {LivePopulation:N0}", "Live Telemetry|Smoothed frame rate and total living units currently simulated in this world.", isHeader: true);
            GameUiStyle.Label($"Map: {mapDims}", "Map Dimensions|Current world size in tiles. CoreScale unlocks giant presets up to 24x24 chunks (1536x1536 tiles).");
            GameUiStyle.Label($"LOD Tier: {CurrentLodTier}  (Parallel: {Config.parallel_jobs_updater})", "Simulation LOD Tier|Tier 0 (<3k pop): full speed. Tier 1 (3k+): lighter cosmetics. Tier 2 (8k+): time-sliced AI. Tier 3 (15k+): max throttling.");
            GUILayout.Space(6);

            if (GameUiStyle.ToggleButton(
                $"Adaptive LOD: {(Settings.EnableAdaptiveLOD ? "ON" : "OFF")}",
                Settings.EnableAdaptiveLOD,
                "Adaptive Simulation LOD|Progressively throttles heavy background AI batches at 3k, 8k, and 15k population so giant worlds stay playable.|Saved automatically"))
            {
                Settings.EnableAdaptiveLOD = !Settings.EnableAdaptiveLOD;
                Settings.Save(_declare.FolderPath);
                RefreshLodState();
            }

            if (GameUiStyle.ToggleButton(
                $"Strategic Anim Cull: {(Settings.SkipAnimationsInStrategicZoom ? "ON" : "OFF")}",
                Settings.SkipAnimationsInStrategicZoom,
                "Strategic Animation Culling|Skips micro-sprite frame updates when zoomed far out into strategic map view to save CPU cycles.|Saved automatically"))
            {
                Settings.SkipAnimationsInStrategicZoom = !Settings.SkipAnimationsInStrategicZoom;
                Settings.Save(_declare.FolderPath);
            }

            if (GameUiStyle.ToggleButton(
                $"Shadows: {(Settings.ForceDisableShadows ? "OFF" : "AUTO")}",
                !Settings.ForceDisableShadows,
                "Unit & Building Shadows|AUTO keeps shadows at low pop and trims them at high LOD tiers. OFF forces shadows off for maximum FPS.|Saved automatically"))
            {
                Settings.ForceDisableShadows = !Settings.ForceDisableShadows;
                Settings.Save(_declare.FolderPath);
            }

            if (GameUiStyle.ToggleButton(
                $"Auto Battalions: {(Settings.EnableBattalionCompression ? "ON" : "OFF")} ({BattalionCompressionSystem.TotalSoldiersConsolidated})",
                Settings.EnableBattalionCompression,
                "Army Battalion Compression|Automatically merges massive squads into stronger veteran champions during mega-wars to cut unit count without weakening empires.|Saved automatically"))
            {
                Settings.EnableBattalionCompression = !Settings.EnableBattalionCompression;
                Settings.Save(_declare.FolderPath);
            }

            if (GameUiStyle.Button(
                "Compress Mega-Armies Now",
                "Compress Armies Immediately|Runs a one-shot battalion compression pass on all oversized armies right now to reclaim FPS."))
            {
                BattalionCompressionSystem.CompressMegaArmies(force: true);
            }

            GameUiStyle.CaptureTooltip();
            GUI.DragWindow();
        }
    }
}
