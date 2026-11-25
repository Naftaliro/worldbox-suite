using System;
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
        private string _status = "";
        private Vector2 _scroll;
        private Rect _windowRect = new Rect(340, 290, 365, 410);

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
                Debug.Log($"[WorldRelics] Loaded v{_declare.Version}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[WorldRelics] Init failed: {e}");
            }
        }

        public void OnUnload()
        {
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
                default: return false;
            }
        }

        public string ToggleCityWalls()
        {
            Settings.EnableCityWalls = !Settings.EnableCityWalls;
            Settings.Save(_declare.FolderPath);
            return $"Auto City Walls: {(Settings.EnableCityWalls ? "ON" : "OFF")}";
        }

        public string ToggleSeaBridges()
        {
            Settings.EnableSeaBridges = !Settings.EnableSeaBridges;
            Settings.Save(_declare.FolderPath);
            return $"Auto Sea Strait Bridges: {(Settings.EnableSeaBridges ? "ON" : "OFF")}";
        }

        public string ToggleRuinDecay()
        {
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

        private void Update()
        {
            if (Settings == null) return;

            if ((Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) && Input.GetKeyDown(KeyCode.R))
            {
                ToggleHud();
            }

            if (Config.paused || MapBox.instance == null) return;

            float dt = Time.deltaTime;
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
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(360));

            GameUiStyle.Label($"Walls Built: {InfrastructureBuilder.WallsBuilt}  |  Bridges: {InfrastructureBuilder.BridgesBuilt}", "Infrastructure Built|Total city perimeter wall tiles and stone sea bridges constructed in this session.", isHeader: true);
            GameUiStyle.Label($"Tracked Ruin Sites: {RuinsDecaySystem.RuinSites.Count}", "Fallen Empires|Ruined cities whose abandoned walls and roads are slowly weathering over centuries.");
            GUILayout.Space(6);

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

            if (RuinsDecaySystem.RuinSites.Count > 0)
            {
                GUILayout.Space(8);
                GameUiStyle.Label("Fallen Empire Ruins:", "Historical Ruin Sites|Click Go to pan the camera directly to any fallen city's overgrown remains.", isHeader: true);
                foreach (var site in RuinsDecaySystem.RuinSites)
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
