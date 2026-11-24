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

        private Rect _hubRect = new Rect(20, 76, 325, 465);

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
            _harmony?.UnpatchSelf();
            Debug.Log("[SuiteHub] Unloaded.");
        }

        public void ToggleHub() => _showHub = !_showHub;
        public void ToggleWarMatrix() => _showWarMatrix = !_showWarMatrix;
        public void ToggleWorldBuilder() => _showWorldBuilder = !_showWorldBuilder;
        public void ToggleBattleRadar() => BattleRadarSystem.Enabled = !BattleRadarSystem.Enabled;

        public static bool IsCompanionLoaded(string mainTypeName)
        {
            return AccessTools.TypeByName(mainTypeName) != null;
        }

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

        private void OnGUI()
        {
            // Render live conflict rings behind windows
            BattleRadarSystem.DrawBattleMarkers();

            if (!_showHub && !_showWarMatrix && !_showWorldBuilder) return;
            GameUiStyle.BeginFrame();

            if (_showHub)
            {
                _hubRect = GUI.Window(941209, _hubRect, DrawHubWindow, "WorldBox Suite Hub (F5)", GameUiStyle.WindowStyle);
            }
            if (_showWarMatrix)
            {
                DiplomacyWarOverlay.Draw(() => _showWarMatrix = false);
            }
            if (_showWorldBuilder)
            {
                MultiContinentGenerator.Draw(() => _showWorldBuilder = false);
            }

            GameUiStyle.DrawTooltip(949001);
        }

        private void DrawHubWindow(int id)
        {
            GameUiStyle.DrawCloseButton(_hubRect, () => _showHub = false);

            GameUiStyle.Label("Suite Controls & Tools", "Master Controls|Quick toggles for SuiteHub's built-in world builder, war monitor, and battle radar.", isHeader: true);
            GameUiStyle.Subtle("Hover any button for details. Drag the title bar to move.");
            GUILayout.Space(6);

            if (GameUiStyle.ToggleButton(
                $"War & Diplomacy Monitor (F4): {(_showWarMatrix ? "OPEN" : "CLOSED")}",
                _showWarMatrix,
                "War & Diplomacy Monitor|View all active wars, casualties, live skirmishes, and kingdom opinion scores with 1-click camera jumps.|Hotkey: F4"))
                ToggleWarMatrix();

            if (GameUiStyle.ToggleButton(
                $"Battlefield Conflict Radar (F3): {(BattleRadarSystem.Enabled ? "ON" : "OFF")}",
                BattleRadarSystem.Enabled,
                "Battlefield Conflict Radar|Draws pulsing clash rings directly over live combat hotspots on the map so big fights stand out immediately.|Hotkey: F3"))
                ToggleBattleRadar();

            if (GameUiStyle.ToggleButton(
                $"Multi-Continent World Builder (F2): {(_showWorldBuilder ? "OPEN" : "CLOSED")}",
                _showWorldBuilder,
                "Multi-Continent World Builder|Sculpt a brand-new multi-continent world right in-game with custom sizes, biomes, ocean channels, and stepping-stone islands.|Hotkey: F2"))
                ToggleWorldBuilder();

            GUILayout.Space(8);
            GameUiStyle.Label("Suite Module Panels:", "Module Panels|Open or close each companion mod's dedicated control panel.", isHeader: true);

            if (GameUiStyle.Button("CoreScale Perf & Battalions (F8)", "CoreScale Performance|Giant map presets up to 24x24 chunks, adaptive simulation LOD, shadow culling, and mega-army battalion compression.|Hotkey: F8"))
                ToggleCompanionHud("CoreScale.Main");

            if (GameUiStyle.Button("VisualPolish Lighting & Zoom (F9)", "VisualPolish Lighting|Switch atmospheric lighting presets, dynamic day/night cycles, vignette strength, and F6 photo mode.|Hotkey: F9"))
                ToggleCompanionHud("VisualPolish.Main");

            if (GameUiStyle.Button("GrandWarfare War Room (F10)", "GrandWarfare War Room|Toggle naval ship-to-ship broadsides, coastal bombardment, port blockades, supply-line attrition, and defense pacts.|Hotkey: F10"))
                ToggleCompanionHud("GrandWarfare.Main");

            if (GameUiStyle.Button("LinkedContinents Portal (F11)", "LinkedContinents Portal|Connect separate continent saves across ocean edges so fleets and armies sail from one save file into another.|Hotkey: F11"))
                ToggleCompanionHud("LinkedContinents.Main");

            if (GameUiStyle.Button("EraChronicle History (F7)", "EraChronicle History|Track 5-tier kingdom eras with veteran milestones and export JSON history timelines for WorldBox Studio.|Hotkey: F7"))
                ToggleCompanionHud("EraChronicle.Main");

            if (GameUiStyle.Button("ApexFactions Wardens & Wild Realms (F12)", "ApexFactions & Wild Realms|Warden Sleeping Giant doctrine with 3-strikes mercy ceasefires, instant species awakener, and feral animal pack domains.|Hotkey: F12"))
                ToggleCompanionHud("ApexFactions.Main");

            if (GameUiStyle.Button("WorldRelics Walls, Bridges & Ruins (Alt+R)", "WorldRelics Infrastructure|Large cities build gated perimeter walls, connect narrow straits with stone bridges, and weather into mossy ruins after falling.|Hotkey: Alt+R"))
                ToggleCompanionHud("WorldRelics.Main");

            if (GameUiStyle.Button("CoopHost Shared God Mode (Alt+C)", "Co-Op God Mode Host|Start a browser Co-Op God Console over LAN, Tailscale, or playit.gg so a second player can paint terrain and cast powers with you.|Hotkey: Alt+C"))
                ToggleCompanionHud("CoopHost.Main");

            if (IsCompanionLoaded("ModernAge.Main"))
            {
                if (GameUiStyle.Button("ModernAge Expansion (Alt+M)", "ModernAge Expansion|Tier 1-3 firearms, tanks, helicopters, supersonic jets, capital radar SAM domes, 6 governments, and international trade.|Hotkey: Alt+M"))
                    ToggleCompanionHud("ModernAge.Main");
            }

            GameUiStyle.CaptureTooltip();
            GUI.DragWindow();
        }
    }
}
