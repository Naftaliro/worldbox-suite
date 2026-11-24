using System;
using System.Reflection;
using HarmonyLib;
using NeoModLoader.api;
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
        private float _fpsSmoothed = 60f;
        private float _statsTimer = 0f;
        private Rect _windowRect = new Rect(18, 86, 270, 215);

        private GUIStyle _boxStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _btnStyle;
        private Texture2D _bgTex;
        private Texture2D _btnTex;

        public ModDeclare GetDeclaration() => _declare;
        public GameObject GetGameObject() => _gameObject;
        public string GetUrl() => "https://github.com/naftaliro/worldbox-suite";

        public void OnLoad(ModDeclare pModDecl, GameObject pGameObject)
        {
            Instance = this;
            _declare = pModDecl;
            _gameObject = pGameObject;

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

                SimulationLodPatches.ApplyParallelSettings();
                Debug.Log($"[CoreScale] Loaded v{_declare.Version} (threads={Environment.ProcessorCount})");
            }
            catch (Exception e)
            {
                Debug.LogError($"[CoreScale] Init failed: {e}");
            }
        }

        public void OnUnload()
        {
            _harmony?.UnpatchAll(_declare.UID);
            Debug.Log("[CoreScale] Unloaded and unpatched.");
        }

        private void Update()
        {
            if (Settings == null) return;

            if (Input.GetKeyDown(Settings.OverlayToggleKey))
            {
                _showOverlay = !_showOverlay;
            }

            float dt = Time.unscaledDeltaTime;
            if (dt > 0.0001f)
            {
                _fpsSmoothed = Mathf.Lerp(_fpsSmoothed, 1f / dt, 0.08f);
            }

            _statsTimer += dt;
            if (_statsTimer >= 0.35f)
            {
                _statsTimer = 0f;
                RefreshLodState();
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

        private void EnsureStyles()
        {
            if (_boxStyle != null) return;

            // Catppuccin Mocha palette (Mantle #181825, Surface0 #313244, Text #cdd6f4, Mauve #cba6f7)
            _bgTex = MakeTex(new Color(0.094f, 0.094f, 0.145f, 0.93f));
            _btnTex = MakeTex(new Color(0.192f, 0.196f, 0.267f, 0.95f));

            _boxStyle = new GUIStyle(GUI.skin.window)
            {
                padding = new RectOffset(12, 12, 24, 12)
            };
            _boxStyle.normal.background = _bgTex;
            _boxStyle.onNormal.background = _bgTex;
            _boxStyle.normal.textColor = new Color(0.796f, 0.651f, 0.969f);

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 13
            };
            _headerStyle.normal.textColor = new Color(0.706f, 0.745f, 0.996f);

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12
            };
            _labelStyle.normal.textColor = new Color(0.804f, 0.839f, 0.957f);

            _btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12
            };
            _btnStyle.normal.background = _btnTex;
            _btnStyle.normal.textColor = new Color(0.804f, 0.839f, 0.957f);
        }

        private static Texture2D MakeTex(Color col)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, col);
            tex.Apply();
            return tex;
        }

        private void OnGUI()
        {
            if (!_showOverlay || Settings == null) return;
            EnsureStyles();
            _windowRect = GUI.Window(941203, _windowRect, DrawOverlayWindow, "CoreScale Perf HUD (F8)", _boxStyle);
        }

        private void DrawOverlayWindow(int id)
        {
            string mapDims = MapBox.width > 0 ? $"{MapBox.width}x{MapBox.height} tiles" : "No map";

            GUILayout.Label($"FPS: {_fpsSmoothed:0.0}   |   Pop: {LivePopulation:N0}", _headerStyle);
            GUILayout.Label($"Map: {mapDims}", _labelStyle);
            GUILayout.Label($"LOD Tier: {CurrentLodTier}  (Parallel: {Config.parallel_jobs_updater})", _labelStyle);
            GUILayout.Space(6);

            if (GUILayout.Button($"Adaptive LOD: {(Settings.EnableAdaptiveLOD ? "ON" : "OFF")}", _btnStyle))
            {
                Settings.EnableAdaptiveLOD = !Settings.EnableAdaptiveLOD;
                Settings.Save(_declare.FolderPath);
                RefreshLodState();
            }

            if (GUILayout.Button($"Strategic Anim Cull: {(Settings.SkipAnimationsInStrategicZoom ? "ON" : "OFF")}", _btnStyle))
            {
                Settings.SkipAnimationsInStrategicZoom = !Settings.SkipAnimationsInStrategicZoom;
                Settings.Save(_declare.FolderPath);
            }

            if (GUILayout.Button($"Shadows: {(Settings.ForceDisableShadows ? "OFF" : "AUTO")}", _btnStyle))
            {
                Settings.ForceDisableShadows = !Settings.ForceDisableShadows;
                Settings.Save(_declare.FolderPath);
            }

            GUI.DragWindow();
        }
    }
}
