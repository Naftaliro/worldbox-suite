using System;
using System.Reflection;
using HarmonyLib;
using NeoModLoader.api;
using UnityEngine;

namespace SuiteHub
{
    public class Main : MonoBehaviour, IMod
    {
        public static Main Instance { get; private set; }

        private ModDeclare _declare;
        private GameObject _gameObject;
        private Harmony _harmony;

        private bool _showHub = false;
        private bool _showWarMatrix = false;
        private bool _showWorldBuilder = false;
        private bool _bottomBarTried = false;
        private float _initDelay = 0f;
        private float _pruneTimer = 0f;

        private Rect _hubRect = new Rect(20, 76, 310, 425);

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

            try
            {
                _harmony = new Harmony(_declare.UID);
                _harmony.PatchAll(Assembly.GetExecutingAssembly());
                Debug.Log($"[SuiteHub] Loaded v{_declare.Version}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SuiteHub] Failed to initialize Harmony patches: {e}");
            }
        }

        public void OnUnload()
        {
            _harmony?.UnpatchAll(_declare.UID);
            Debug.Log("[SuiteHub] Unloaded.");
        }

        public void ToggleHub() => _showHub = !_showHub;
        public void ToggleWarMatrix() => _showWarMatrix = !_showWarMatrix;
        public void ToggleWorldBuilder() => _showWorldBuilder = !_showWorldBuilder;
        public void ToggleBattleRadar() => BattleRadarSystem.Enabled = !BattleRadarSystem.Enabled;

        public void ToggleCompanionHud(string mainTypeName)
        {
            try
            {
                Type t = AccessTools.TypeByName(mainTypeName);
                object inst = t?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                t?.GetMethod("ToggleHud", BindingFlags.Public | BindingFlags.Instance)?.Invoke(inst, null);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SuiteHub] Companion module {mainTypeName} not loaded: {e.Message}");
            }
        }

        private void Update()
        {
            // Defer bottom-bar tab registration slightly so WorldBox's Canvas & PowerTabs are fully ready
            if (!_bottomBarTried)
            {
                _initDelay += Time.unscaledDeltaTime;
                if (_initDelay >= 1.5f && MapBox.instance != null)
                {
                    _bottomBarTried = true;
                    BottomBarIntegration.RegisterBottomTab(_declare.FolderPath);
                }
            }

            if (Input.GetKeyDown(KeyCode.F5)) ToggleHub();
            if (Input.GetKeyDown(KeyCode.F4)) ToggleWarMatrix();
            if (Input.GetKeyDown(KeyCode.F3)) ToggleBattleRadar();
            if (Input.GetKeyDown(KeyCode.F2)) ToggleWorldBuilder();

            _pruneTimer += Time.unscaledDeltaTime;
            if (_pruneTimer >= 1.0f)
            {
                _pruneTimer = 0f;
                BattleRadarSystem.PruneExpired();
            }
        }

        private void EnsureStyles()
        {
            if (_boxStyle != null) return;

            // Catppuccin Mocha: Mantle (#181825), Surface0 (#313244), Mauve (#cba6f7), Lavender (#b4befe), Text (#cdd6f4)
            _bgTex = MakeTex(new Color(0.094f, 0.094f, 0.145f, 0.94f));
            _btnTex = MakeTex(new Color(0.192f, 0.196f, 0.267f, 0.96f));

            _boxStyle = new GUIStyle(GUI.skin.window) { padding = new RectOffset(12, 12, 24, 12) };
            _boxStyle.normal.background = _bgTex;
            _boxStyle.onNormal.background = _bgTex;
            _boxStyle.normal.textColor = new Color(0.796f, 0.651f, 0.969f);

            _headerStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 13 };
            _headerStyle.normal.textColor = new Color(0.706f, 0.745f, 0.996f);

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
            // Render live conflict rings behind windows
            BattleRadarSystem.DrawBattleMarkers();

            if (!_showHub && !_showWarMatrix && !_showWorldBuilder) return;
            EnsureStyles();

            if (_showHub)
            {
                _hubRect = GUI.Window(941209, _hubRect, DrawHubWindow, "WorldBox Suite Hub (F5)", _boxStyle);
            }
            if (_showWarMatrix)
            {
                DiplomacyWarOverlay.Draw(_boxStyle, _headerStyle, _labelStyle, _btnStyle);
            }
            if (_showWorldBuilder)
            {
                MultiContinentGenerator.Draw(_boxStyle, _headerStyle, _labelStyle, _btnStyle);
            }
        }

        private void DrawHubWindow(int id)
        {
            GUILayout.Label("Suite Controls & Tools", _headerStyle);
            GUILayout.Label("Accessible from the bottom bar tab or hotkeys.", _labelStyle);
            GUILayout.Space(6);

            if (GUILayout.Button($"War & Diplomacy Monitor (F4): {(_showWarMatrix ? "OPEN" : "CLOSED")}", _btnStyle))
                ToggleWarMatrix();

            if (GUILayout.Button($"Battlefield Conflict Radar (F3): {(BattleRadarSystem.Enabled ? "ON" : "OFF")}", _btnStyle))
                ToggleBattleRadar();

            if (GUILayout.Button($"Multi-Continent World Builder (F2): {(_showWorldBuilder ? "OPEN" : "CLOSED")}", _btnStyle))
                ToggleWorldBuilder();

            GUILayout.Space(8);
            GUILayout.Label("Suite Module Panels:", _headerStyle);
            if (GUILayout.Button("CoreScale Perf & Battalions (F8)", _btnStyle)) ToggleCompanionHud("CoreScale.Main");
            if (GUILayout.Button("VisualPolish Lighting & Zoom (F9)", _btnStyle)) ToggleCompanionHud("VisualPolish.Main");
            if (GUILayout.Button("GrandWarfare War Room (F10)", _btnStyle)) ToggleCompanionHud("GrandWarfare.Main");
            if (GUILayout.Button("LinkedContinents Portal (F11)", _btnStyle)) ToggleCompanionHud("LinkedContinents.Main");
            if (GUILayout.Button("EraChronicle History (F7)", _btnStyle)) ToggleCompanionHud("EraChronicle.Main");
            if (GUILayout.Button("ApexFactions Wardens & Wild Realms (F12)", _btnStyle)) ToggleCompanionHud("ApexFactions.Main");
            if (GUILayout.Button("Arsenal Blackpowder & Counters (Alt+A)", _btnStyle)) ToggleCompanionHud("Arsenal.Main");

            GUI.DragWindow();
        }
    }
}
