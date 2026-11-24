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
        private float _cycleTime = 0f;
        private Rect _windowRect = new Rect(305, 86, 290, 290);

        private Texture2D _vignetteTex;
        private Texture2D _pixelTex;
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
            Settings = VisualPolishConfig.LoadOrCreate(_declare.FolderPath);

            try
            {
                _harmony = new Harmony(_declare.UID);
                _harmony.PatchAll(Assembly.GetExecutingAssembly());
                BuildTextures();
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

        public void ToggleHud() => _showPanel = !_showPanel;

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
                _showPanel = !_showPanel;
            }

            if (Input.GetKeyDown(Settings.PhotoModeKey))
            {
                Config.ui_main_hidden = !Config.ui_main_hidden;
            }

            if (Settings.DynamicDayNightCycle && Settings.CycleDurationSeconds > 5f)
            {
                _cycleTime = (_cycleTime + Time.unscaledDeltaTime) % Settings.CycleDurationSeconds;
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

        private void EnsureStyles()
        {
            if (_boxStyle != null) return;

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
            if (Settings == null) return;

            // Render atmospheric tint & vignette behind HUD windows
            if (Event.current.type == EventType.Repaint)
            {
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

            if (_showPanel)
            {
                EnsureStyles();
                _windowRect = GUI.Window(941204, _windowRect, DrawWindow, "VisualPolish (F9)", _boxStyle);
            }
        }

        private void DrawWindow(int id)
        {
            GUILayout.Label($"Preset: {Settings.Preset}", _headerStyle);

            if (GUILayout.Button("Cycle Visual Preset", _btnStyle))
            {
                int count = Enum.GetValues(typeof(VisualPreset)).Length;
                Settings.Preset = (VisualPreset)(((int)Settings.Preset + 1) % count);
                Settings.DynamicDayNightCycle = false;
                Settings.Save(_declare.FolderPath);
            }

            if (GUILayout.Button($"Dynamic Day/Night: {(Settings.DynamicDayNightCycle ? "ON" : "OFF")}", _btnStyle))
            {
                Settings.DynamicDayNightCycle = !Settings.DynamicDayNightCycle;
                Settings.Save(_declare.FolderPath);
            }

            GUILayout.Space(6);
            GUILayout.Label($"Vignette: {Settings.VignetteStrength:0.00}", _labelStyle);
            float newVig = GUILayout.HorizontalSlider(Settings.VignetteStrength, 0f, 0.65f);
            if (Mathf.Abs(newVig - Settings.VignetteStrength) > 0.005f)
            {
                Settings.VignetteStrength = newVig;
            }

            GUILayout.Label($"Atmosphere Intensity: {Settings.TintStrength:0.00}", _labelStyle);
            float newTint = GUILayout.HorizontalSlider(Settings.TintStrength, 0f, 0.35f);
            if (Mathf.Abs(newTint - Settings.TintStrength) > 0.005f)
            {
                Settings.TintStrength = newTint;
            }

            GUILayout.Space(6);
            if (GUILayout.Button($"Photo Mode (F6): {(Config.ui_main_hidden ? "HUD HIDDEN" : "NORMAL")}", _btnStyle))
            {
                Config.ui_main_hidden = !Config.ui_main_hidden;
            }

            if (GUILayout.Button("Save Settings", _btnStyle))
            {
                Settings.Save(_declare.FolderPath);
            }

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
            if (OrthoMinField != null && !OrthoMinField.IsLiteral && !OrthoMinField.IsInitOnly)
            {
                OrthoMinField.SetValue(__instance, Main.Settings.CloseZoomMin);
            }
        }
    }

    [HarmonyPatch(typeof(ZoneCalculator))]
    public static class StrategicBorderPatch
    {
        private static readonly FieldInfo BorderBrightnessField =
            AccessTools.Field(typeof(ZoneCalculator), "border_brightness");

        [HarmonyPostfix]
        [HarmonyPatch("create")]
        private static void CreatePostfix(ZoneCalculator __instance)
        {
            if (__instance == null || Main.Settings == null || BorderBrightnessField == null) return;
            if (BorderBrightnessField.GetValue(__instance) is float cur)
            {
                BorderBrightnessField.SetValue(__instance, Mathf.Clamp(cur * Main.Settings.StrategicBorderBoost, 0.5f, 2.0f));
            }
        }
    }
}
