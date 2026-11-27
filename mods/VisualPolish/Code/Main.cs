using System;
using System.Reflection;
using HarmonyLib;
using NeoModLoader.api;
using UnityEngine;

namespace VisualPolish
{
    public class Main : MonoBehaviour, IMod
    {
        public static Main Instance { get; private set; }
        public static VisualPolishConfig Settings { get; private set; }

        private ModDeclare _declare;
        private GameObject _gameObject;
        private Harmony _harmony;

        private bool _showPanel = false;
        private GuiOverlayRunner _fxRunner;
        private GuiOverlayRunner _panelRunner;
        private float _cycleTime = 0f;
        private float _ageCheckTimer = 0f;
        private float _lastUpdateWarnTime = -99f;
        private Rect _windowRect = new Rect(305, 86, 315, 470);

        private Texture2D _vignetteTex;
        private Texture2D _pixelTex;

        public ModDeclare GetDeclaration() => _declare;
        public GameObject GetGameObject() => _gameObject;
        public string GetUrl() => "https://github.com/naftaliro/worldbox-suite";

        public void OnLoad(ModDeclare pModDecl, GameObject pGameObject)
        {
            Instance = this;
            _declare = pModDecl;
            _gameObject = pGameObject;
            _fxRunner = pGameObject.AddComponent<GuiOverlayRunner>();
            _fxRunner.Configure(false, DrawAtmosphereFx);
            _panelRunner = pGameObject.AddComponent<GuiOverlayRunner>();
            _panelRunner.Configure(true, DrawPanelGui);
            Settings = VisualPolishConfig.LoadOrCreate(_declare.FolderPath);

            try
            {
                _harmony = new Harmony(_declare.UID);
                _harmony.PatchAll(Assembly.GetExecutingAssembly());
                BuildTextures();
                SyncRunners();
                Debug.Log($"[VisualPolish] Loaded v{_declare.Version} (preset={Settings.Preset})");
            }
            catch (Exception e)
            {
                Debug.LogError($"[VisualPolish] Failed to load: {e}");
            }
        }

        public void OnUnload()
        {
            _harmony?.UnpatchSelf();
            Debug.Log("[VisualPolish] Unloaded.");
        }

        private void SyncRunners()
        {
            if (Settings == null) return;
            bool needFx = Settings.DynamicDayNightCycle || Settings.Preset != VisualPreset.Vanilla || Settings.VignetteStrength > 0.01f;
            if (_fxRunner != null) _fxRunner.enabled = needFx;
            if (_panelRunner != null) _panelRunner.enabled = _showPanel;
        }

        public void ToggleHud()
        {
            _showPanel = !_showPanel;
            SyncRunners();
        }

        public bool GetState(string key)
        {
            if (Settings == null) return false;
            switch (key)
            {
                case "preset": return Settings.Preset != VisualPreset.Vanilla;
                case "daynight": return Settings.DynamicDayNightCycle;
                case "vignette": return Settings.VignetteStrength > 0.01f;
                case "photo": return Config.ui_main_hidden;
                case "always_day": return Settings.AlwaysDayClientSide;
                case "age_hope": return Settings.LockAgeOfHope;
                case "colors_borders": return Settings.AutoDistinctKingdomColors || Settings.EnhancedBorders;
                case "distinct_colors": return Settings.AutoDistinctKingdomColors;
                case "enhanced_borders": return Settings.EnhancedBorders;
                default: return false;
            }
        }

        public string CyclePreset()
        {
            if (Settings == null) return "";
            int count = Enum.GetValues(typeof(VisualPreset)).Length;
            Settings.Preset = (VisualPreset)(((int)Settings.Preset + 1) % count);
            Settings.DynamicDayNightCycle = false;
            Settings.Save(_declare.FolderPath);
            SyncRunners();
            return $"Lighting Preset: {Settings.Preset}";
        }

        public string ToggleDayNight()
        {
            if (Settings == null) return "";
            Settings.DynamicDayNightCycle = !Settings.DynamicDayNightCycle;
            Settings.Save(_declare.FolderPath);
            SyncRunners();
            return $"Dynamic Day/Night: {(Settings.DynamicDayNightCycle ? "ON" : "OFF")}";
        }

        public string ToggleVignette()
        {
            if (Settings == null) return "";
            Settings.VignetteStrength = Settings.VignetteStrength > 0.01f ? 0f : 0.24f;
            Settings.Save(_declare.FolderPath);
            SyncRunners();
            return $"Screen Vignette: {(Settings.VignetteStrength > 0.01f ? "ON" : "OFF")}";
        }

        public string TogglePhotoMode()
        {
            Config.ui_main_hidden = !Config.ui_main_hidden;
            return $"Photo Mode (F6): {(Config.ui_main_hidden ? "HUD HIDDEN" : "NORMAL")}";
        }

        public string ToggleAlwaysDay()
        {
            if (Settings == null) return "";
            Settings.AlwaysDayClientSide = !Settings.AlwaysDayClientSide;
            Settings.Save(_declare.FolderPath);
            return $"Always Day (Visual): {(Settings.AlwaysDayClientSide ? "ON" : "OFF")}";
        }

        public string ToggleLockAgeOfHope()
        {
            if (Settings == null) return "";
            Settings.LockAgeOfHope = !Settings.LockAgeOfHope;
            Settings.Save(_declare.FolderPath);
            DaylightAndAgePatches.ApplyLockAgeOfHope(Settings.LockAgeOfHope);
            return $"Lock Age of Hope: {(Settings.LockAgeOfHope ? "ON" : "OFF")}";
        }

        public string ToggleColorsAndBorders()
        {
            if (Settings == null) return "";
            bool next = !(Settings.AutoDistinctKingdomColors || Settings.EnhancedBorders);
            Settings.AutoDistinctKingdomColors = next;
            Settings.EnhancedBorders = next;
            Settings.Save(_declare.FolderPath);
            KingdomColorAndBorderSystem.ApplyNow(runColorPass: next);
            return $"Distinct Colors & Borders: {(next ? "ON" : "OFF")}";
        }

        public string ToggleAutoDistinctColors()
        {
            if (Settings == null) return "";
            Settings.AutoDistinctKingdomColors = !Settings.AutoDistinctKingdomColors;
            Settings.Save(_declare.FolderPath);
            if (Settings.AutoDistinctKingdomColors)
            {
                KingdomColorAndBorderSystem.ApplyNow(runColorPass: true);
            }
            return $"Auto Distinct Kingdom Colors: {(Settings.AutoDistinctKingdomColors ? "ON" : "OFF")}";
        }

        public string ToggleEnhancedBorders()
        {
            if (Settings == null) return "";
            Settings.EnhancedBorders = !Settings.EnhancedBorders;
            Settings.Save(_declare.FolderPath);
            KingdomColorAndBorderSystem.ApplyNow(runColorPass: false);
            return $"Enhanced Borders: {(Settings.EnhancedBorders ? "ON" : "OFF")}";
        }

        private void BuildTextures()
        {
            _pixelTex = new Texture2D(1, 1);
            _pixelTex.SetPixel(0, 0, Color.white);
            _pixelTex.Apply();

            // Soft radial vignette texture (128x128)
            const int size = 128;
            _vignetteTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            _vignetteTex.wrapMode = TextureWrapMode.Clamp;
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x - half) / half;
                    float ny = (y - half) / half;
                    float dist = Mathf.Sqrt(nx * nx + ny * ny);
                    float alpha = Mathf.Clamp01((dist - 0.58f) / 0.85f);
                    alpha = alpha * alpha;
                    _vignetteTex.SetPixel(x, y, new Color(0.05f, 0.05f, 0.09f, alpha));
                }
            }
            _vignetteTex.Apply();
        }

        private void Update()
        {
            if (Settings == null) return;

            if (Input.GetKeyDown(Settings.PanelToggleKey))
            {
                ToggleHud();
            }

            if (Input.GetKeyDown(Settings.PhotoModeKey))
            {
                Config.ui_main_hidden = !Config.ui_main_hidden;
            }

            try
            {
                if (Settings.DynamicDayNightCycle && Settings.CycleDurationSeconds > 5f)
                {
                    _cycleTime = (_cycleTime + Time.unscaledDeltaTime) % Settings.CycleDurationSeconds;
                }

                if (Settings.LockAgeOfHope && MapBox.instance != null)
                {
                    _ageCheckTimer += Time.unscaledDeltaTime;
                    if (_ageCheckTimer >= 2.0f)
                    {
                        _ageCheckTimer = 0f;
                        DaylightAndAgePatches.EnforceAgeOfHopeIfLocked();
                    }
                }

                KingdomColorAndBorderSystem.Tick(Time.unscaledDeltaTime);
            }
            catch (Exception e)
            {
                float now = Time.unscaledTime;
                if (now - _lastUpdateWarnTime >= 30f)
                {
                    _lastUpdateWarnTime = now;
                    Debug.LogWarning($"[VisualPolish] Update tick exception: {e.Message}");
                }
            }
        }

        private Color ComputeAtmosphereTint()
        {
            if (Settings.DynamicDayNightCycle)
            {
                float phase = (_cycleTime / Mathf.Max(10f, Settings.CycleDurationSeconds)) * Mathf.PI * 2f;
                float sun = Mathf.Sin(phase); // -1 night, 0 dawn/dusk, +1 noon
                if (sun >= 0f)
                {
                    // Warm golden dawn/dusk fading to crisp daylight
                    float dusk = 1f - sun;
                    return new Color(1.0f, 0.72f, 0.44f, dusk * Settings.TintStrength * 1.15f);
                }
                else
                {
                    // Cool twilight blue-violet night
                    float night = -sun;
                    return new Color(0.11f, 0.14f, 0.30f, night * Mathf.Clamp01(Settings.TintStrength * 2.1f));
                }
            }

            switch (Settings.Preset)
            {
                case VisualPreset.CrispVibrant:
                    return new Color(0.55f, 0.82f, 1.0f, Settings.TintStrength * 0.35f);
                case VisualPreset.GoldenHour:
                    return new Color(1.0f, 0.66f, 0.32f, Settings.TintStrength * 1.2f);
                case VisualPreset.MoonlitNight:
                    return new Color(0.10f, 0.15f, 0.32f, Settings.TintStrength * 2.0f);
                case VisualPreset.ParchmentAtlas:
                    return new Color(0.84f, 0.73f, 0.52f, Settings.TintStrength * 1.25f);
                case VisualPreset.MochaTwilight:
                    return new Color(0.45f, 0.34f, 0.65f, Settings.TintStrength * 1.0f);
                default:
                    return Color.clear;
            }
        }

        private void DrawAtmosphereFx()
        {
            if (Settings == null || Event.current.type != EventType.Repaint) return;
            Rect screen = new Rect(0, 0, Screen.width, Screen.height);
            Color tint = ComputeAtmosphereTint();
            if (tint.a > 0.002f && _pixelTex != null)
            {
                Color prev = GUI.color;
                GUI.color = tint;
                GUI.DrawTexture(screen, _pixelTex);
                GUI.color = prev;
            }

            if (Settings.VignetteStrength > 0.01f && _vignetteTex != null)
            {
                Color prev = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(Settings.VignetteStrength));
                GUI.DrawTexture(screen, _vignetteTex);
                GUI.color = prev;
            }
        }

        private void DrawPanelGui()
        {
            if (!_showPanel || Settings == null) return;
            GameUiStyle.BeginFrame();
            _windowRect = GUI.Window(941204, _windowRect, DrawWindow, "VisualPolish (F9)", GameUiStyle.WindowStyle);
            GameUiStyle.DrawTooltip(949003);
        }

        private void DrawWindow(int id)
        {
            GameUiStyle.DrawCloseButton(_windowRect, ToggleHud);
            GameUiStyle.Label($"Preset: {Settings.Preset}", "Active Lighting Preset|Current atmospheric color grade applied across the map.", isHeader: true);

            if (GameUiStyle.Button(
                "Cycle Visual Preset",
                "Cycle Lighting Preset|Switch between Vanilla, Crisp Vibrant, Golden Hour, Moonlit Night, Parchment Atlas, and Mocha Twilight.|Turns off Dynamic Day/Night"))
            {
                CyclePreset();
            }

            if (GameUiStyle.ToggleButton(
                $"Dynamic Day/Night: {(Settings.DynamicDayNightCycle ? "ON" : "OFF")}",
                Settings.DynamicDayNightCycle,
                "Dynamic Day/Night Cycle|Smoothly shifts lighting from golden dawn to bright noon, warm dusk, and cool moonlit night."))
            {
                ToggleDayNight();
            }

            if (GameUiStyle.ToggleButton(
                $"Always Day (Visual): {(Settings.AlwaysDayClientSide ? "ON" : "OFF")}",
                Settings.AlwaysDayClientSide,
                "Always Day (Client-Side)|Removes night, moon, ash, and storm darkness overlays while preserving all World Age gameplay mechanics and disasters.|Purely visual override"))
            {
                ToggleAlwaysDay();
            }

            if (GameUiStyle.ToggleButton(
                $"Lock Age of Hope: {(Settings.LockAgeOfHope ? "ON" : "OFF")}",
                Settings.LockAgeOfHope,
                "Lock Age of Hope|Sets the active world age to Age of Hope and pauses age progression so the world stays sunny and peaceful.|Optional world rule"))
            {
                ToggleLockAgeOfHope();
            }

            if (GameUiStyle.ToggleButton(
                $"Distinct Kingdom Colors: {(Settings.AutoDistinctKingdomColors ? "ON" : "OFF")}",
                Settings.AutoDistinctKingdomColors,
                "Auto Distinct Kingdom Colors|Recolors newly colliding kingdoms when two active civs share nearly identical RGB territory colors.|Preserves already-distinct colors"))
            {
                ToggleAutoDistinctColors();
            }

            if (GameUiStyle.ToggleButton(
                $"Enhanced Borders: {(Settings.EnhancedBorders ? "ON" : "OFF")}",
                Settings.EnhancedBorders,
                "Enhanced Territory Borders|Boosts border brightness and minimap opacity so kingdom borders read cleanly at any zoom."))
            {
                ToggleEnhancedBorders();
            }

            GUILayout.Space(6);
            float newVig = GameUiStyle.SliderRow(
                $"Vignette: {Settings.VignetteStrength:0.00}",
                Settings.VignetteStrength,
                0f,
                0.65f,
                "Screen Vignette|Adjusts the soft darkened edge framing around the screen. Set to 0.00 to disable.");
            if (Mathf.Abs(newVig - Settings.VignetteStrength) > 0.005f)
            {
                Settings.VignetteStrength = newVig;
                SyncRunners();
            }

            float newTint = GameUiStyle.SliderRow(
                $"Atmosphere Intensity: {Settings.TintStrength:0.00}",
                Settings.TintStrength,
                0f,
                0.35f,
                "Atmosphere Strength|Controls how strong the lighting preset or day/night color tint appears over the world.");
            if (Mathf.Abs(newTint - Settings.TintStrength) > 0.005f)
            {
                Settings.TintStrength = newTint;
            }

            GUILayout.Space(6);
            if (GameUiStyle.ToggleButton(
                $"Photo Mode (F6): {(Config.ui_main_hidden ? "HUD HIDDEN" : "NORMAL")}",
                Config.ui_main_hidden,
                "Photo Mode|Hides WorldBox's main interface for clean map screenshots.|Press F6 anytime to toggle back"))
            {
                TogglePhotoMode();
            }

            if (GameUiStyle.Button(
                "Save Settings",
                "Save Visual Config|Writes your current slider values and lighting preset to visualpolish_config.json."))
            {
                Settings.Save(_declare.FolderPath);
            }

            GameUiStyle.CaptureTooltip();
            GUI.DragWindow();
        }
    }

    [HarmonyPatch(typeof(MoveCamera))]
    public static class CloseZoomPatch
    {
        private static readonly FieldInfo OrthoMinField =
            AccessTools.Field(typeof(MoveCamera), "ORTHOGRAPHIC_SIZE_MIN");

        [HarmonyPostfix]
        [HarmonyPatch("Awake")]
        private static void AwakePostfix(MoveCamera __instance)
        {
            if (__instance == null || Main.Settings == null || !Main.Settings.UnlockCloseZoom) return;
            try
            {
                if (OrthoMinField != null && !OrthoMinField.IsLiteral && !OrthoMinField.IsInitOnly)
                {
                    OrthoMinField.SetValue(__instance, Main.Settings.CloseZoomMin);
                }
            }
            catch { }
        }
    }
}
