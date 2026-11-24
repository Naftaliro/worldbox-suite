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
        private float _infraTimer = 0f;
        private float _decayTimer = 0f;
        private string _status = "";
        private Vector2 _scroll;
        private Rect _windowRect = new Rect(340, 290, 355, 385);

        private Texture2D _bgTex;
        private Texture2D _btnTex;
        private GUIStyle _boxStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _btnStyle;

        public ModDeclare GetDeclaration() => _declare;
        public GameObject GetGameObject() => _gameObject;
        public string GetUrl() => "https://github.com/naftaliro/worldbox-suite";

        public void OnLoad(ModDeclare pModDecl, GameObject pGameObject)
        {
            Instance = this;
            _declare = pModDecl;
            _gameObject = pGameObject;
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

        public void ToggleHud() => _showHud = !_showHud;

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

        private void EnsureStyles()
        {
            if (_boxStyle != null) return;

            _bgTex = MakeTex(new Color(0.094f, 0.094f, 0.145f, 0.94f));
            _btnTex = MakeTex(new Color(0.192f, 0.196f, 0.267f, 0.96f));

            _boxStyle = new GUIStyle(GUI.skin.window) { padding = new RectOffset(12, 12, 24, 12) };
            _boxStyle.normal.background = _bgTex;
            _boxStyle.onNormal.background = _bgTex;
            _boxStyle.normal.textColor = new Color(0.651f, 0.890f, 0.631f); // Catppuccin Green

            _headerStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 13 };
            _headerStyle.normal.textColor = new Color(0.580f, 0.886f, 0.835f); // Catppuccin Teal

            _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            _labelStyle.normal.textColor = new Color(0.804f, 0.839f, 0.957f);

            _btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 12 };
            _btnStyle.normal.background = _btnTex;
            _btnStyle.normal.textColor = new Color(0.804f, 0.839f, 0.957f);
        }

        private static Texture2D MakeTex(Color col)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, col);
            t.Apply();
            return t;
        }

        private void OnGUI()
        {
            if (!_showHud || Settings == null) return;
            EnsureStyles();
            _windowRect = GUI.Window(941214, _windowRect, DrawHud, "WorldRelics: Walls, Bridges & Ruins (Alt+R)", _boxStyle);
        }

        private void DrawHud(int id)
        {
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(335));

            GUILayout.Label($"Walls Built: {InfrastructureBuilder.WallsBuilt}  |  Bridges: {InfrastructureBuilder.BridgesBuilt}", _headerStyle);
            GUILayout.Label($"Tracked Ruin Sites: {RuinsDecaySystem.RuinSites.Count}", _labelStyle);
            GUILayout.Space(6);

            if (GUILayout.Button($"Auto City Walls (85+ pop): {(Settings.EnableCityWalls ? "ON" : "OFF")}", _btnStyle))
            {
                Settings.EnableCityWalls = !Settings.EnableCityWalls;
                Settings.Save(_declare.FolderPath);
            }

            if (GUILayout.Button($"Auto Sea Strait Bridges: {(Settings.EnableSeaBridges ? "ON" : "OFF")}", _btnStyle))
            {
                Settings.EnableSeaBridges = !Settings.EnableSeaBridges;
                Settings.Save(_declare.FolderPath);
            }

            if (GUILayout.Button($"Centuries Ruin Decay: {(Settings.EnableRuinDecay ? "ON" : "OFF")}", _btnStyle))
            {
                Settings.EnableRuinDecay = !Settings.EnableRuinDecay;
                Settings.Save(_declare.FolderPath);
            }

            GUILayout.Space(4);
            if (GUILayout.Button("Fortify Large Cities Now", _btnStyle))
            {
                int n = InfrastructureBuilder.FortifyLargeCities(force: true);
                _status = $"Placed {n} gated perimeter wall tiles.";
            }

            if (GUILayout.Button("Build Narrow Sea Bridges Now", _btnStyle))
            {
                int n = InfrastructureBuilder.BuildNarrowSeaBridges(force: true);
                _status = n > 0 ? $"Built {n} new sea bridge(s)!" : "No narrow friendly straits found.";
            }

            if (GUILayout.Button("Age All Ruins +60 Years (Weather & Moss)", _btnStyle))
            {
                int n = RuinsDecaySystem.WeatherAllRuins(60);
                _status = $"Weathered {n} ruin tiles.";
            }

            if (RuinsDecaySystem.RuinSites.Count > 0)
            {
                GUILayout.Space(8);
                GUILayout.Label("Fallen Empire Ruins:", _headerStyle);
                foreach (var site in RuinsDecaySystem.RuinSites)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"• {site.CityName} ({RuinsDecaySystem.StageLabel(site)})", _labelStyle);
                    if (GUILayout.Button("Go", _btnStyle, GUILayout.Width(40)))
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
                GUILayout.Label(_status, _headerStyle);
            }

            GUILayout.EndScrollView();
            GUI.DragWindow();
        }
    }
}
