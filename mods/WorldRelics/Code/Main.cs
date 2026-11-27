using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using NeoModLoader.api;
using UnityEngine;

namespace WorldRelics
{
    public class Main : MonoBehaviour, IMod
    {
        public static Main Instance { get; private set; }
        public static WorldRelicsConfig Settings { get; private set; }

        private ModDeclare _declare;
        private GameObject _gameObject;
        private Harmony _harmony;

        private bool _showHud = false;
        private GuiOverlayRunner _panelRunner;
        private float _infraTimer = 0f;
        private float _decayTimer = 0f;
        private float _lastUpdateWarnTime = -99f;
        private string _status = "";
        private Vector2 _scroll;
        private Rect _windowRect = new Rect(340, 220, 365, 490);

        public ModDeclare GetDeclaration() => _declare;
        public GameObject GetGameObject() => _gameObject;
        public string GetUrl() => "https://github.com/naftaliro/worldbox-suite";

        public void OnLoad(ModDeclare pModDecl, GameObject pGameObject)
        {
            Instance = this;
            _declare = pModDecl;
            _gameObject = pGameObject;
            _panelRunner = pGameObject.AddComponent<GuiOverlayRunner>();
            _panelRunner.Configure(true, DrawPanelGui);
            Settings = WorldRelicsConfig.LoadOrCreate(_declare.FolderPath);

            try
            {
                _harmony = new Harmony(_declare.UID);
                _harmony.PatchAll(Assembly.GetExecutingAssembly());
                if (Settings.EnableDenseCities)
                {
                    DenseCitiesSystem.Apply(true);
                }
                Debug.Log($"[WorldRelics] Loaded v{_declare.Version}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[WorldRelics] Init failed: {e}");
            }
        }

        public void OnUnload()
        {
            TectonicDriftSystem.StopSilent();
            InfrastructureSystem.Reset();
            DenseCitiesSystem.RestoreVanilla();
            _harmony?.UnpatchSelf();
            Debug.Log("[WorldRelics] Unloaded.");
        }

        public void ToggleHud()
        {
            _showHud = !_showHud;
            if (_panelRunner != null) _panelRunner.enabled = _showHud;
        }

        public bool GetState(string key)
        {
            if (Settings == null) return false;
            switch (key)
            {
                case "walls": return Settings.EnableCityWalls;
                case "bridges": return Settings.EnableSeaBridges;
                case "ruins": return Settings.EnableRuinDecay;
                case "dense_cities": return Settings.EnableDenseCities;
                case "highways": return Settings.EnableImperialHighways;
                case "citadels": return Settings.EnableCitadelCompounds;
                case "infra": return Settings.EnableImperialHighways || Settings.EnableCitadelCompounds;
                case "relics_cities":
                    return Settings.EnableDenseCities
                        || Settings.EnableImperialHighways
                        || Settings.EnableCitadelCompounds
                        || Settings.EnableCityWalls;
                case "tectonic_slow": return Settings.TectonicSlowGeological;
                case "tectonic_natural": return Settings.EnableNaturalTectonicEvents;
                default: return false;
            }
        }

        public string BuildInfrastructureNow()
        {
            int highways = InfrastructureSystem.BuildImperialHighways(force: true);
            int citadels = InfrastructureSystem.BuildCitadelCompounds(force: true);
            string msg;
            if (highways > 0 || citadels > 0)
            {
                msg = $"Built {highways} imperial highway(s) and {citadels} fortified capital citadel(s)!";
            }
            else
            {
                msg = "No eligible capitals or city pairs needed new highways or citadels.";
            }
            _status = msg;
            return msg;
        }

        public string ToggleInfrastructure()
        {
            if (Settings == null) return "";
            bool next = !(Settings.EnableImperialHighways || Settings.EnableCitadelCompounds);
            Settings.EnableImperialHighways = next;
            Settings.EnableCitadelCompounds = next;
            Settings.Save(_declare.FolderPath);
            string msg = $"Imperial Highways & Citadels: {(next ? "ON" : "OFF")}";
            _status = msg;
            return msg;
        }

        public string ToggleRelicsAndCitiesBundle()
        {
            if (Settings == null) return "";
            bool next = !GetState("relics_cities");
            Settings.EnableDenseCities = next;
            Settings.EnableCityWalls = next;
            Settings.EnableSeaBridges = next;
            Settings.EnableImperialHighways = next;
            Settings.EnableCitadelCompounds = next;
            DenseCitiesSystem.Apply(next);
            Settings.Save(_declare.FolderPath);
            string msg = next
                ? "Dense Cities & Imperial Infrastructure: ON (highways, citadels, walls, bridges, dense housing)"
                : "Dense Cities & Imperial Infrastructure: OFF (vanilla footprints restored)";
            _status = msg;
            return msg;
        }

        public string ToggleDenseCities()
        {
            if (Settings == null) return "";
            Settings.EnableDenseCities = !Settings.EnableDenseCities;
            DenseCitiesSystem.Apply(Settings.EnableDenseCities);
            Settings.Save(_declare.FolderPath);
            return Settings.EnableDenseCities
                ? "Dense Cities: ON (compact footprints + 2.5x manor housing)"
                : "Dense Cities: OFF (vanilla footprints & housing restored)";
        }

        public string ToggleCityWalls()
        {
            if (Settings == null) return "";
            Settings.EnableCityWalls = !Settings.EnableCityWalls;
            Settings.Save(_declare.FolderPath);
            return $"Auto City Walls: {(Settings.EnableCityWalls ? "ON" : "OFF")}";
        }

        public string ToggleSeaBridges()
        {
            if (Settings == null) return "";
            Settings.EnableSeaBridges = !Settings.EnableSeaBridges;
            Settings.Save(_declare.FolderPath);
            return $"Auto Sea Strait Bridges: {(Settings.EnableSeaBridges ? "ON" : "OFF")}";
        }

        public string ToggleRuinDecay()
        {
            if (Settings == null) return "";
            Settings.EnableRuinDecay = !Settings.EnableRuinDecay;
            Settings.Save(_declare.FolderPath);
            return $"Centuries Ruin Decay: {(Settings.EnableRuinDecay ? "ON" : "OFF")}";
        }

        public string FortifyNow()
        {
            int n = InfrastructureBuilder.FortifyLargeCities(force: true);
            return n > 0 ? $"Fortified cities with {n} gated wall tiles!" : "No eligible cities needed new walls.";
        }

        public string BuildBridgesNow()
        {
            int n = InfrastructureBuilder.BuildNarrowSeaBridges(force: true);
            return n > 0 ? $"Built {n} stone sea bridge(s)!" : "No narrow friendly straits found.";
        }

        public string AgeRuinsNow()
        {
            int n = RuinsDecaySystem.WeatherAllRuins(60);
            return n > 0 ? $"Weathered {n} ancient ruin tiles (+60 years)!" : "No fallen city ruins tracked yet.";
        }

        public string TriggerPangaeaDrift()
        {
            string msg = TectonicDriftSystem.StartPangaeaDrift();
            _status = msg;
            return msg;
        }

        public string TriggerTectonicRift()
        {
            string msg = TectonicDriftSystem.StartTectonicRift();
            _status = msg;
            return msg;
        }

        public string ToggleTectonicSpeed()
        {
            Settings.TectonicSlowGeological = !Settings.TectonicSlowGeological;
            Settings.Save(_declare.FolderPath);
            string msg = Settings.TectonicSlowGeological
                ? "Tectonic Speed: Geological (slow drift, 6.0s/step)"
                : "Tectonic Speed: Cinematic (fast drift, 0.9s/step)";
            _status = msg;
            return msg;
        }

        public string ToggleNaturalTectonics()
        {
            Settings.EnableNaturalTectonicEvents = !Settings.EnableNaturalTectonicEvents;
            Settings.Save(_declare.FolderPath);
            string msg = $"Rare Natural Tectonic Rifts: {(Settings.EnableNaturalTectonicEvents ? "ON" : "OFF")}";
            _status = msg;
            return msg;
        }

        private void Update()
        {
            if (Settings == null) return;

            if ((Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) && Input.GetKeyDown(KeyCode.R))
            {
                ToggleHud();
            }

            try
            {
                if (DenseCitiesSystem.NeedsSync(Settings.EnableDenseCities)
                    && AssetManager.buildings?.list != null
                    && AssetManager.buildings.list.Count > 0)
                {
                    DenseCitiesSystem.Apply(Settings.EnableDenseCities);
                }

                if (Config.paused || MapBox.instance == null) return;

                float dt = Time.deltaTime;
                TectonicDriftSystem.Tick(dt);
                InfrastructureSystem.Tick(dt);

                _infraTimer += dt;
                if (_infraTimer >= 18f)
                {
                    _infraTimer = 0f;
                    InfrastructureBuilder.FortifyLargeCities(force: false);
                    InfrastructureBuilder.BuildNarrowSeaBridges(force: false);
                }

                _decayTimer += dt;
                if (_decayTimer >= 12f)
                {
                    _decayTimer = 0f;
                    RuinsDecaySystem.WeatherAllRuins(0);
                }
            }
            catch (Exception e)
            {
                float now = Time.unscaledTime;
                if (now - _lastUpdateWarnTime >= 30f)
                {
                    _lastUpdateWarnTime = now;
                    Debug.LogWarning($"[WorldRelics] Update tick exception: {e.Message}");
                }
            }
        }

        private void DrawPanelGui()
        {
            if (!_showHud || Settings == null) return;
            GameUiStyle.BeginFrame();
            _windowRect = GUI.Window(941214, _windowRect, DrawHud, "WorldRelics: Walls & Ruins (Alt+R)", GameUiStyle.WindowStyle);
            GameUiStyle.DrawTooltip(949008);
        }

        private void DrawHud(int id)
        {
            GameUiStyle.DrawCloseButton(_windowRect, ToggleHud);
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(440));

            GameUiStyle.Label($"Walls: {InfrastructureBuilder.WallsBuilt} | Bridges: {InfrastructureBuilder.BridgesBuilt} | Highways: {InfrastructureSystem.HighwaysBuilt} | Citadels: {InfrastructureSystem.CitadelsBuilt}", "Infrastructure Built|Total city perimeter wall tiles, sea bridges, wide imperial highways, and fortified capital citadels built in this session.", isHeader: true);
            GameUiStyle.Label($"Tracked Ruin Sites: {RuinsDecaySystem.RuinSites.Count}", "Fallen Empires|Ruined cities whose abandoned walls and roads are slowly weathering over centuries.");
            GUILayout.Space(6);

            if (GameUiStyle.ToggleButton(
                $"Dense Cities (Condensed): {(Settings.EnableDenseCities ? "ON" : "OFF")}",
                Settings.EnableDenseCities,
                "Dense Cities Mode|Tightens city building footprints, boosts residential and hall housing capacity (up to 2.5x on manors/halls), and speeds up building upgrades by 30%. Fully reversible to vanilla.|Saved automatically"))
            {
                _status = ToggleDenseCities();
            }

            if (GameUiStyle.ToggleButton(
                $"Imperial Highways (45+ pop): {(Settings.EnableImperialHighways ? "ON" : "OFF")}",
                Settings.EnableImperialHighways,
                "Wide Imperial Highways|Connects major friendly or allied cities (45+ pop) with 2-3 tile wide cobblestone highways and 2-tile stone bridges over narrow water gaps.|Saved automatically"))
            {
                Settings.EnableImperialHighways = !Settings.EnableImperialHighways;
                Settings.Save(_declare.FolderPath);
                _status = $"Imperial Highways: {(Settings.EnableImperialHighways ? "ON" : "OFF")}";
            }

            if (GameUiStyle.ToggleButton(
                $"Fortified Capital Citadels (70+ pop): {(Settings.EnableCitadelCompounds ? "ON" : "OFF")}",
                Settings.EnableCitadelCompounds,
                "Fortified Citadel Compounds|Surrounds major capitals (70+ pop, 4+ zones) with a double-thick gated stone or iron fortress ring, paved crossroad, and gentle defender HP recovery.|Saved automatically"))
            {
                Settings.EnableCitadelCompounds = !Settings.EnableCitadelCompounds;
                Settings.Save(_declare.FolderPath);
                _status = $"Fortified Capital Citadels: {(Settings.EnableCitadelCompounds ? "ON" : "OFF")}";
            }

            if (GameUiStyle.ToggleButton(
                $"Auto City Walls (85+ pop): {(Settings.EnableCityWalls ? "ON" : "OFF")}",
                Settings.EnableCityWalls,
                "Automatic City Fortifications|Prosperous cities (85+ pop) automatically construct gated stone/iron perimeter walls along their borders.|Saved automatically"))
            {
                Settings.EnableCityWalls = !Settings.EnableCityWalls;
                Settings.Save(_declare.FolderPath);
            }

            if (GameUiStyle.ToggleButton(
                $"Auto Sea Strait Bridges: {(Settings.EnableSeaBridges ? "ON" : "OFF")}",
                Settings.EnableSeaBridges,
                "Automatic Sea Bridges|Friendly coastal cities separated by narrow ocean straits automatically build stone causeways so armies and villagers can cross.|Saved automatically"))
            {
                Settings.EnableSeaBridges = !Settings.EnableSeaBridges;
                Settings.Save(_declare.FolderPath);
            }

            if (GameUiStyle.ToggleButton(
                $"Centuries Ruin Decay: {(Settings.EnableRuinDecay ? "ON" : "OFF")}",
                Settings.EnableRuinDecay,
                "Centuries-Long Ruin Decay|When a fortified city falls, its walls weather over 100-300+ years into ancient stone, mossy walls, and wild overgrown ruins.|Saved automatically"))
            {
                Settings.EnableRuinDecay = !Settings.EnableRuinDecay;
                Settings.Save(_declare.FolderPath);
            }

            GUILayout.Space(4);
            if (GameUiStyle.Button(
                "Build Imperial Highways & Citadels Now",
                "Construct Highways & Capital Citadels Immediately|Links eligible friendly cities with wide paved highways and fortifies major capitals with double-wall gated citadels right now."))
            {
                BuildInfrastructureNow();
            }

            if (GameUiStyle.Button(
                "Fortify Large Cities Now",
                "Build City Walls Immediately|Scans all eligible large cities and places gated defensive perimeter walls right now."))
            {
                int n = InfrastructureBuilder.FortifyLargeCities(force: true);
                _status = $"Placed {n} gated perimeter wall tiles.";
            }

            if (GameUiStyle.Button(
                "Build Narrow Sea Bridges Now",
                "Construct Strait Bridges Immediately|Connects nearby friendly shorelines across narrow water channels with stone bridges right now."))
            {
                int n = InfrastructureBuilder.BuildNarrowSeaBridges(force: true);
                _status = n > 0 ? $"Built {n} new sea bridge(s)!" : "No narrow friendly straits found.";
            }

            if (GameUiStyle.Button(
                "Age All Ruins +60 Years (Weather & Moss)",
                "Fast-Forward Ruin Weathering|Advances every tracked ruin site by 60 years so you can watch ancient walls turn mossy and wild immediately."))
            {
                int n = RuinsDecaySystem.WeatherAllRuins(60);
                _status = $"Weathered {n} ruin tiles.";
            }

            GUILayout.Space(8);
            GameUiStyle.Label(
                $"Tectonic Events: {TectonicDriftSystem.StatusLabel()}",
                "Continental Tectonics|Split continents apart physically with Pangaea Drift, or carve an empire-safe widening ocean strait across populated worlds.",
                isHeader: true);

            GUILayout.BeginHorizontal();
            if (GameUiStyle.Button(
                "Pangaea Drift",
                "Physical Plate Drift|Physically slides the two continental plates apart across open ocean, opening a jagged sea between them while carrying living units on their plates. Collapses buildings before shifting so zones stay safe.|Requires ocean room at map edges"))
            {
                TriggerPangaeaDrift();
            }
            if (GameUiStyle.Button(
                "Empire-Safe Rift",
                "Widening Ocean Strait|Carves an organic tectonic sea channel across any map size. Evacuates units to dry land and leaves inland cities, buildings, and walls untouched.|Safe for populated worlds"))
            {
                TriggerTectonicRift();
            }
            if (GameUiStyle.Button(
                "Stop",
                "Stop Tectonic Event|Halt any active Pangaea Drift or Tectonic Rift immediately.",
                GUILayout.Width(54)))
            {
                _status = TectonicDriftSystem.StopActiveEvent();
            }
            GUILayout.EndHorizontal();

            if (GameUiStyle.ToggleButton(
                Settings.TectonicSlowGeological
                    ? "Drift Speed: Geological (6.0s/step)"
                    : "Drift Speed: Cinematic (0.9s/step)",
                Settings.TectonicSlowGeological,
                "Tectonic Drift Speed|Cinematic unfolds in ~12-20 seconds; Geological unfolds slowly over minutes so continents drift over decades.|Saved automatically"))
            {
                ToggleTectonicSpeed();
            }

            float nextStepsFloat = GameUiStyle.SliderRow(
                $"Max Drift Steps: {Mathf.Clamp(Settings.TectonicMaxDriftSteps, 6, 24)} (~{Mathf.Clamp(Settings.TectonicMaxDriftSteps, 6, 24) * 2} tile sea)",
                Mathf.Clamp(Settings.TectonicMaxDriftSteps, 6, 24),
                6f,
                24f,
                "Max Drift Steps|Controls how wide the ocean gap becomes (roughly 2x steps in tiles). Pangaea Drift also auto-caps to keep a 4-tile ocean buffer at map borders.");
            int nextSteps = Mathf.RoundToInt(nextStepsFloat);
            if (nextSteps != Settings.TectonicMaxDriftSteps)
            {
                Settings.TectonicMaxDriftSteps = nextSteps;
                Settings.Save(_declare.FolderPath);
            }

            if (GameUiStyle.ToggleButton(
                $"Rare Natural Rifts: {(Settings.EnableNaturalTectonicEvents ? "ON" : "OFF")}",
                Settings.EnableNaturalTectonicEvents,
                "Rare Natural Tectonic Events|When enabled, the world occasionally triggers an empire-safe tectonic rift on its own (~8% chance every 3 minutes when idle).|Saved automatically"))
            {
                ToggleNaturalTectonics();
            }

            if (RuinsDecaySystem.RuinSites.Count > 0)
            {
                GUILayout.Space(8);
                GameUiStyle.Label("Fallen Empire Ruins:", "Historical Ruin Sites|Click Go to pan the camera directly to any fallen city's overgrown remains.", isHeader: true);
                var sitesSnapshot = new List<FallenRuinSite>(RuinsDecaySystem.RuinSites);
                foreach (var site in sitesSnapshot)
                {
                    GUILayout.BeginHorizontal();
                    GameUiStyle.Label($"• {site.CityName} ({RuinsDecaySystem.StageLabel(site)})", $"Ruin Site|{site.CityName} is currently in the {RuinsDecaySystem.StageLabel(site)} decay stage.");
                    if (GameUiStyle.Button("Go", $"Visit {site.CityName}|Center the camera on the ruins of {site.CityName}.", GUILayout.Width(42)))
                    {
                        Camera cam = Camera.main;
                        if (cam != null) cam.transform.position = new Vector3(site.CenterX, site.CenterY, cam.transform.position.z);
                    }
                    GUILayout.EndHorizontal();
                }
            }

            if (!string.IsNullOrEmpty(_status))
            {
                GUILayout.Space(6);
                GameUiStyle.Label(_status, null, isHeader: true);
            }

            GUILayout.EndScrollView();
            GameUiStyle.CaptureTooltip();
            GUI.DragWindow();
        }
    }
}
