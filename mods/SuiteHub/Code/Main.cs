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
        private GuiOverlayRunner _radarRunner;
        private GuiOverlayRunner _panelRunner;

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

            _radarRunner = pGameObject.AddComponent<GuiOverlayRunner>();
            _radarRunner.Configure(false, BattleRadarSystem.DrawBattleMarkers);
            _panelRunner = pGameObject.AddComponent<GuiOverlayRunner>();
            _panelRunner.Configure(true, DrawPanelsGui);

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

        private void SyncRunners()
        {
            if (_radarRunner != null)
            {
                _radarRunner.enabled = BattleRadarSystem.Enabled && BattleRadarSystem.Hotspots.Count > 0;
            }
            if (_panelRunner != null)
            {
                _panelRunner.enabled = _showHub || _showWarMatrix || _showWorldBuilder;
            }
        }

        public void ToggleHub() { _showHub = !_showHub; SyncRunners(); }
        public void ToggleWarMatrix() { _showWarMatrix = !_showWarMatrix; SyncRunners(); }
        public void ToggleWorldBuilder() { _showWorldBuilder = !_showWorldBuilder; SyncRunners(); }

        public string ToggleBattleRadar()
        {
            BattleRadarSystem.Enabled = !BattleRadarSystem.Enabled;
            SyncRunners();
            return $"Battlefield Conflict Radar: {(BattleRadarSystem.Enabled ? "ON" : "OFF")}";
        }

        public static void ShowToast(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            try
            {
                WorldTip.showNowTop(message, false);
            }
            catch
            {
                Debug.Log($"[SuiteHub] {message}");
            }
        }

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

        public static string InvokeCompanionAction(string mainTypeName, string methodName, params object[] args)
        {
            try
            {
                Type t = AccessTools.TypeByName(mainTypeName);
                object inst = t?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                if (t == null || inst == null) return $"{mainTypeName.Split('.')[0]} is not loaded.";
                MethodInfo m = t.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
                if (m == null) return "";
                object res = m.Invoke(inst, args);
                return res as string ?? "";
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SuiteHub] Action {mainTypeName}.{methodName} failed: {e.Message}");
                return "";
            }
        }

        public static bool GetCompanionState(string mainTypeName, string key)
        {
            try
            {
                Type t = AccessTools.TypeByName(mainTypeName);
                object inst = t?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                if (t == null || inst == null) return false;
                MethodInfo m = t.GetMethod("GetState", BindingFlags.Public | BindingFlags.Instance);
                if (m == null) return false;
                return m.Invoke(inst, new object[] { key }) is bool b && b;
            }
            catch
            {
                return false;
            }
        }

        public static string JumpToActiveBattle()
        {
            for (int i = BattleRadarSystem.Hotspots.Count - 1; i >= 0; i--)
            {
                var h = BattleRadarSystem.Hotspots[i];
                if (h.Hits >= 2)
                {
                    DiplomacyWarOverlay.JumpCameraToCoords(h.X, h.Y);
                    return $"Jumped to clash: {h.SideA} vs {h.SideB} ({h.Hits} hits)";
                }
            }

            var world = MapBox.instance;
            if (world?.wars != null)
            {
                foreach (var w in world.wars.getActiveWars())
                {
                    WorldTile cap = w?.getMainDefender()?.capital?.getTile();
                    if (cap != null)
                    {
                        DiplomacyWarOverlay.JumpCameraToTile(cap);
                        return $"Jumped to active war: {w.name}";
                    }
                }
            }
            return "No active battles or wars right now.";
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
                    BottomBarIntegration.RegisterBottomTabs(_declare.FolderPath);
                }
            }

            if (Input.GetKeyDown(KeyCode.F5)) ToggleHub();
            if (Input.GetKeyDown(KeyCode.F4)) ToggleWarMatrix();
            if (Input.GetKeyDown(KeyCode.F3)) ShowToast(ToggleBattleRadar());
            if (Input.GetKeyDown(KeyCode.F2)) ToggleWorldBuilder();

            _pruneTimer += Time.unscaledDeltaTime;
            if (_pruneTimer >= 1.0f)
            {
                _pruneTimer = 0f;
                BattleRadarSystem.PruneExpired();
                SyncRunners();
            }
        }

        private void DrawPanelsGui()
        {
            if (!_showHub && !_showWarMatrix && !_showWorldBuilder) return;
            GameUiStyle.BeginFrame();

            if (_showHub)
            {
                _hubRect = GUI.Window(941209, _hubRect, DrawHubWindow, "WorldBox Suite Hub (F5)", GameUiStyle.WindowStyle);
            }
            if (_showWarMatrix)
            {
                DiplomacyWarOverlay.Draw(() => { _showWarMatrix = false; SyncRunners(); });
            }
            if (_showWorldBuilder)
            {
                MultiContinentGenerator.Draw(() => { _showWorldBuilder = false; SyncRunners(); });
            }

            GameUiStyle.DrawTooltip(949001);
        }

        private void DrawHubWindow(int id)
        {
            GameUiStyle.DrawCloseButton(_hubRect, ToggleHub);

            GameUiStyle.Label("Suite Controls & Tools", "Master Controls|All actions and toggles are also available directly on the Suite: Powers and Suite: Rules bottom tabs.", isHeader: true);
            GameUiStyle.Subtle("Optional quick-access panel. Close for 0ms IMGUI overhead.");
            GUILayout.Space(6);

            if (GameUiStyle.ToggleButton(
                $"War & Diplomacy Monitor (F4): {(_showWarMatrix ? "OPEN" : "CLOSED")}",
                _showWarMatrix,
                "War & Diplomacy Monitor|View all active wars, casualties, live skirmishes, and kingdom opinion scores with 1-click camera jumps.|Hotkey: F4"))
                ToggleWarMatrix();

            if (GameUiStyle.ToggleButton(
                $"Battlefield Conflict Radar (F3): {(BattleRadarSystem.Enabled ? "ON" : "OFF")}",
                BattleRadarSystem.Enabled,
                "Battlefield Conflict Radar|Draws pulsing clash rings directly over live combat hotspots on the map.|Hotkey: F3"))
                ShowToast(ToggleBattleRadar());

            if (GameUiStyle.ToggleButton(
                $"Multi-Continent World Builder (F2): {(_showWorldBuilder ? "OPEN" : "CLOSED")}",
                _showWorldBuilder,
                "Multi-Continent World Builder|Sculpt a brand-new multi-continent world right in-game with custom sizes, biomes, and ocean divides.|Hotkey: F2"))
                ToggleWorldBuilder();

            GUILayout.Space(8);
            GameUiStyle.Label("Detailed Module Panels:", "Optional Detail Panels|Open a mod's full settings panel if you want fine-grained sliders or live logs.", isHeader: true);

            if (GameUiStyle.Button("CoreScale Perf & Battalions (F8)", "CoreScale Details|Open FPS telemetry and LOD status.|Hotkey: F8")) ToggleCompanionHud("CoreScale.Main");
            if (GameUiStyle.Button("VisualPolish Lighting & Zoom (F9)", "VisualPolish Details|Open fine-grained vignette & atmosphere sliders.|Hotkey: F9")) ToggleCompanionHud("VisualPolish.Main");
            if (GameUiStyle.Button("GrandWarfare War Room (F10)", "GrandWarfare Details|Open naval volley & blockade telemetry.|Hotkey: F10")) ToggleCompanionHud("GrandWarfare.Main");
            if (GameUiStyle.Button("LinkedContinents Portal (F11)", "LinkedContinents Details|Set target continent name and outbound edge.|Hotkey: F11")) ToggleCompanionHud("LinkedContinents.Main");
            if (GameUiStyle.Button("EraChronicle History (F7)", "EraChronicle Details|Inspect recent era milestones.|Hotkey: F7")) ToggleCompanionHud("EraChronicle.Main");
            if (GameUiStyle.Button("ApexFactions Wardens & Wild Realms (F12)", "ApexFactions Details|Adjust mercy ceasefire % and inspect feral domains.|Hotkey: F12")) ToggleCompanionHud("ApexFactions.Main");
            if (GameUiStyle.Button("WorldRelics Walls, Bridges & Ruins (Alt+R)", "WorldRelics Details|Browse fallen city ruin sites with Go buttons.|Hotkey: Alt+R")) ToggleCompanionHud("WorldRelics.Main");
            if (GameUiStyle.Button("CoopHost Shared God Mode (Alt+C)", "CoopHost Details|Inspect LAN URL, PIN, and live co-op activity feed.|Hotkey: Alt+C")) ToggleCompanionHud("CoopHost.Main");
            if (IsCompanionLoaded("ModernAge.Main"))
            {
                if (GameUiStyle.Button("ModernAge Expansion (Alt+M)", "ModernAge Details|Inspect governments, trade specialties, and counter stats.|Hotkey: Alt+M")) ToggleCompanionHud("ModernAge.Main");
            }

            GameUiStyle.CaptureTooltip();
            GUI.DragWindow();
        }
    }
}
