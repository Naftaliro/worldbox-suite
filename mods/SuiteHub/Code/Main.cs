using System;
using System.Collections.Generic;
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
        private GuiOverlayRunner _panelRunner;
        private GuiOverlayRunner _tickerRunner;

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

            _panelRunner = pGameObject.AddComponent<GuiOverlayRunner>();
            _panelRunner.Configure(true, DrawPanelsGui);
            _tickerRunner = pGameObject.AddComponent<GuiOverlayRunner>();
            _tickerRunner.Configure(true, DrawTickerGui);

            try
            {
                _harmony = new Harmony(_declare.UID);
                _harmony.PatchAll(Assembly.GetExecutingAssembly());
                SyncRunners();
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
            if (_panelRunner != null)
            {
                _panelRunner.enabled = _showHub || _showWarMatrix || _showWorldBuilder;
            }
            if (_tickerRunner != null)
            {
                _tickerRunner.enabled = WarTickerOverlay.Visible
                    && !Config.ui_main_hidden
                    && WarTickerOverlay.CountActiveWars() > 0;
            }
        }

        public void ToggleHub() { _showHub = !_showHub; SyncRunners(); }
        public void ToggleWarMatrix() { _showWarMatrix = !_showWarMatrix; SyncRunners(); }
        public void ToggleWorldBuilder() { _showWorldBuilder = !_showWorldBuilder; SyncRunners(); }

        public string ToggleWarTicker()
        {
            WarTickerOverlay.Visible = !WarTickerOverlay.Visible;
            if (WarTickerOverlay.Visible) WarTickerOverlay.Collapsed = false;
            SyncRunners();
            return $"Live War Ticker (F3): {(WarTickerOverlay.Visible ? "ON" : "OFF")}";
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

        private static readonly Dictionary<string, Type> CompanionTypeCache = new Dictionary<string, Type>(StringComparer.Ordinal);
        private static readonly Dictionary<string, float> CompanionMissRetryAt = new Dictionary<string, float>(StringComparer.Ordinal);
        private static readonly Dictionary<Type, PropertyInfo> CompanionInstancePropCache = new Dictionary<Type, PropertyInfo>();
        private static readonly Dictionary<(Type, string), MethodInfo> CompanionMethodCache = new Dictionary<(Type, string), MethodInfo>();

        private static Type ResolveCompanionType(string mainTypeName)
        {
            if (string.IsNullOrEmpty(mainTypeName)) return null;
            if (CompanionTypeCache.TryGetValue(mainTypeName, out Type cached)) return cached;

            float now = Time.unscaledTime;
            if (CompanionMissRetryAt.TryGetValue(mainTypeName, out float retryAt) && now < retryAt)
            {
                return null;
            }

            Type resolved = AccessTools.TypeByName(mainTypeName);
            if (resolved != null)
            {
                CompanionTypeCache[mainTypeName] = resolved;
                CompanionMissRetryAt.Remove(mainTypeName);
            }
            else
            {
                CompanionMissRetryAt[mainTypeName] = now + 5f;
            }
            return resolved;
        }

        private static object ResolveCompanionInstance(Type t)
        {
            if (t == null) return null;
            if (!CompanionInstancePropCache.TryGetValue(t, out PropertyInfo prop))
            {
                prop = t.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                CompanionInstancePropCache[t] = prop;
            }
            return prop?.GetValue(null);
        }

        private static MethodInfo ResolveCompanionMethod(Type t, string methodName)
        {
            if (t == null || string.IsNullOrEmpty(methodName)) return null;
            var key = (t, methodName);
            if (!CompanionMethodCache.TryGetValue(key, out MethodInfo method))
            {
                method = t.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
                CompanionMethodCache[key] = method;
            }
            return method;
        }

        public static bool IsCompanionLoaded(string mainTypeName)
        {
            return ResolveCompanionType(mainTypeName) != null;
        }

        public void ToggleCompanionHud(string mainTypeName)
        {
            try
            {
                Type t = ResolveCompanionType(mainTypeName);
                object inst = ResolveCompanionInstance(t);
                if (t == null || inst == null) return;
                ResolveCompanionMethod(t, "ToggleHud")?.Invoke(inst, null);
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
                Type t = ResolveCompanionType(mainTypeName);
                object inst = ResolveCompanionInstance(t);
                if (t == null || inst == null) return $"{mainTypeName.Split('.')[0]} is not loaded.";
                MethodInfo m = ResolveCompanionMethod(t, methodName);
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

        public static string InvokeFirstCompanionAction(string mainTypeName, string[] methodNames, params object[] args)
        {
            try
            {
                Type t = ResolveCompanionType(mainTypeName);
                object inst = ResolveCompanionInstance(t);
                if (t == null || inst == null) return $"{mainTypeName.Split('.')[0]} is not loaded.";
                if (methodNames != null)
                {
                    for (int i = 0; i < methodNames.Length; i++)
                    {
                        MethodInfo m = ResolveCompanionMethod(t, methodNames[i]);
                        if (m != null)
                        {
                            object res = m.Invoke(inst, args);
                            return res as string ?? "";
                        }
                    }
                }
                return "";
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SuiteHub] Companion action on {mainTypeName} failed: {e.Message}");
                return "";
            }
        }

        public static bool GetCompanionState(string mainTypeName, string key)
        {
            try
            {
                Type t = ResolveCompanionType(mainTypeName);
                object inst = ResolveCompanionInstance(t);
                if (t == null || inst == null) return false;
                MethodInfo m = ResolveCompanionMethod(t, "GetState");
                if (m == null) return false;
                return m.Invoke(inst, new object[] { key }) is bool b && b;
            }
            catch
            {
                return false;
            }
        }

        public static bool GetAnyCompanionState(string mainTypeName, params string[] keys)
        {
            if (keys == null || keys.Length == 0) return false;
            for (int i = 0; i < keys.Length; i++)
            {
                if (GetCompanionState(mainTypeName, keys[i])) return true;
            }
            return false;
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
                    if (w != null && DiplomacyWarOverlay.JumpToWar(w))
                    {
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
            if (Input.GetKeyDown(KeyCode.F3)) ShowToast(ToggleWarTicker());
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

            if (_tickerRunner == null || !_tickerRunner.enabled || Config.ui_main_hidden)
            {
                GameUiStyle.DrawTooltip(949001);
            }
        }

        private void DrawTickerGui()
        {
            if (!WarTickerOverlay.Visible || Config.ui_main_hidden) return;
            if (_panelRunner == null || !_panelRunner.enabled)
            {
                GameUiStyle.BeginFrame();
            }

            WarTickerOverlay.Draw(
                onOpenMonitor: () => { _showWarMatrix = true; SyncRunners(); },
                onHide: () => { WarTickerOverlay.Visible = false; SyncRunners(); }
            );

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
                "War & Diplomacy Monitor|View all active wars, war reasons, opinion drivers, casualties, and kingdom relations with Jump and Force Peace controls.|Hotkey: F4"))
                ToggleWarMatrix();

            if (GameUiStyle.ToggleButton(
                $"Live War Ticker (F3): {(WarTickerOverlay.Visible ? "ON" : "OFF")}",
                WarTickerOverlay.Visible,
                "Live War Ticker|Compact collapsible top-right feed showing active wars, causes, casualties, and instant Go jump buttons. Zero overhead when at peace or hidden.|Hotkey: F3"))
                ShowToast(ToggleWarTicker());

            if (GameUiStyle.ToggleButton(
                $"Multi-Continent World Builder (F2): {(_showWorldBuilder ? "OPEN" : "CLOSED")}",
                _showWorldBuilder,
                "Multi-Continent World Builder|Sculpt a brand-new multi-continent world right in-game with custom sizes, biomes, and ocean divides.|Hotkey: F2"))
                ToggleWorldBuilder();

            GUILayout.Space(8);
            GameUiStyle.Label("Detailed Module Panels:", "Optional Detail Panels|Open a mod's full settings panel if you want fine-grained sliders or live logs.", isHeader: true);

            if (GameUiStyle.Button("CoreScale Perf & Battalions (F8)", "CoreScale Details|Open FPS telemetry and LOD status.|Hotkey: F8")) ToggleCompanionHud("CoreScale.Main");
            if (GameUiStyle.Button("VisualPolish Lighting & Daylight (F9)", "VisualPolish Details|Open Always Day, Lock Age of Hope, vignette, and atmosphere controls.|Hotkey: F9")) ToggleCompanionHud("VisualPolish.Main");
            if (GameUiStyle.Button("GrandWarfare War Room (F10)", "GrandWarfare Details|Open naval volley & blockade telemetry.|Hotkey: F10")) ToggleCompanionHud("GrandWarfare.Main");
            if (GameUiStyle.Button("LinkedContinents Portal (F11)", "LinkedContinents Details|Set target continent name and outbound edge.|Hotkey: F11")) ToggleCompanionHud("LinkedContinents.Main");
            if (GameUiStyle.Button("EraChronicle History (F7)", "EraChronicle Details|Inspect recent era milestones.|Hotkey: F7")) ToggleCompanionHud("EraChronicle.Main");
            if (GameUiStyle.Button("ApexFactions Species & Wardens (F12)", "ApexFactions Details|Open the Species Kingdom Manager, Sentinel Wardens, and Snake Mode controls.|Hotkey: F12")) ToggleCompanionHud("ApexFactions.Main");
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
