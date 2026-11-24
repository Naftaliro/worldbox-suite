using System;
using System.Reflection;
using HarmonyLib;
using NeoModLoader.api;
using UnityEngine;

namespace ApexFactions
{
    public class Main : MonoBehaviour, IMod
    {
        public static Main Instance { get; private set; }
        public static ApexFactionsConfig Settings { get; private set; }

        private ModDeclare _declare;
        private GameObject _gameObject;
        private Harmony _harmony;

        private bool _showHud = false;
        private float _wardenTimer = 0f;
        private float _wildTimer = 0f;
        private Rect _windowRect = new Rect(695, 320, 355, 420);
        private Vector2 _scroll;
        private string _status = "";

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
            Settings = ApexFactionsConfig.LoadOrCreate(_declare.FolderPath);

            try
            {
                _harmony = new Harmony(_declare.UID);
                _harmony.PatchAll(Assembly.GetExecutingAssembly());
                Debug.Log($"[ApexFactions] Loaded v{_declare.Version} (mercy={Settings.MercyThresholdPercent}%)");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ApexFactions] Init failed: {e}");
            }
        }

        public void OnUnload()
        {
            _harmony?.UnpatchAll(_declare.UID);
            Debug.Log("[ApexFactions] Unloaded.");
        }

        public void ToggleHud() => _showHud = !_showHud;

        private void Update()
        {
            if (Settings == null) return;

            if (Input.GetKeyDown(Settings.HudToggleKey))
            {
                ToggleHud();
            }

            if (Config.paused || MapBox.instance == null) return;

            float dt = Time.deltaTime;
            _wardenTimer += dt;
            if (_wardenTimer >= 1.5f)
            {
                WardenSystem.Tick(_wardenTimer);
                _wardenTimer = 0f;
            }

            _wildTimer += dt;
            if (_wildTimer >= 4.0f)
            {
                _wildTimer = 0f;
                WildSovereigntySystem.ScanFeralDomains();
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
            _headerStyle.normal.textColor = new Color(0.796f, 0.651f, 0.969f); // Catppuccin Mauve

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
            WildSovereigntySystem.DrawDomainOverlays();

            if (!_showHud || Settings == null) return;
            EnsureStyles();
            _windowRect = GUI.Window(941212, _windowRect, DrawHud, "ApexFactions (F12)", _boxStyle);
        }

        private void DrawHud(int id)
        {
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(365));

            GUILayout.Label($"Warden Doctrine ({WardenSystem.WardenKingdomIds.Count} kingdoms)", _headerStyle);
            GUILayout.Label(
                Settings.MercyThresholdPercent > 0f
                    ? $"Mercy Ceasefire: stop retaliation at {Settings.MercyThresholdPercent:0}% pop"
                    : "Mercy Ceasefire: OFF (total elimination)",
                _labelStyle
            );
            float newPct = GUILayout.HorizontalSlider(Settings.MercyThresholdPercent, 0f, 50f);
            if (Mathf.Abs(newPct - Settings.MercyThresholdPercent) >= 1f)
            {
                Settings.MercyThresholdPercent = Mathf.Round(newPct);
                Settings.Save(_declare.FolderPath);
            }

            if (GUILayout.Button("Spawn Warden Colony (14 Enhanced Humans)", _btnStyle))
            {
                WardenSystem.SpawnWardenColonyAtCamera();
                _status = "Spawned Warden colony at camera!";
            }

            if (GUILayout.Button("Anoint Largest Kingdom as Wardens", _btnStyle))
            {
                Kingdom top = FindLargestNonWardenKingdom();
                if (top != null)
                {
                    WardenSystem.AnointKingdomAsWardens(top);
                    _status = $"Anointed {top.name} as Wardens.";
                }
            }

            if (WardenSystem.ActiveRetaliations.Count > 0)
            {
                GUILayout.Space(4);
                GUILayout.Label("Active Sleeping Giant Retaliations:", _headerStyle);
                foreach (var tr in WardenSystem.ActiveRetaliations.Values)
                {
                    GUILayout.Label(
                        $"• {tr.WardenName} -> {tr.AggressorName}: {tr.CurrentAggressorPop}/{tr.BaselinePop} (ceasefire @ {tr.MercyTargetPop})",
                        _labelStyle
                    );
                }
            }

            GUILayout.Space(8);
            GUILayout.Label($"Wild Sovereignty ({WildSovereigntySystem.ActiveDomains.Count} Feral Domains)", _headerStyle);

            if (GUILayout.Button("Spawn Wolfkin Kingdom (12 civ_wolf)", _btnStyle))
            {
                int n = WildSovereigntySystem.SpawnWolfkinKingdomAtCamera(12);
                _status = $"Spawned {n} Wolfkin settlers!";
            }

            if (GUILayout.Button("Awaken All Wild Wolves (Instant Sapience)", _btnStyle))
            {
                int n = WildSovereigntySystem.AwakenWildSubspecies("wolf");
                _status = n > 0 ? $"Awakened {n} wolf subspecies!" : "No wild wolf subspecies found.";
            }

            if (GUILayout.Button("Awaken All Wild Animals (Full Sapience)", _btnStyle))
            {
                int n = WildSovereigntySystem.AwakenWildSubspecies(null);
                _status = $"Awakened {n} wild animal subspecies!";
            }

            if (GUILayout.Button($"Feral Domain Borders: {(Settings.ShowFeralDomainRings ? "ON" : "OFF")}", _btnStyle))
            {
                Settings.ShowFeralDomainRings = !Settings.ShowFeralDomainRings;
                Settings.Save(_declare.FolderPath);
            }

            foreach (var d in WildSovereigntySystem.ActiveDomains)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"• {d.DomainTitle} ({d.PackCount})", _labelStyle);
                if (GUILayout.Button("Go", _btnStyle, GUILayout.Width(40)))
                {
                    Camera cam = Camera.main;
                    if (cam != null) cam.transform.position = new Vector3(d.CenterX, d.CenterY, cam.transform.position.z);
                }
                GUILayout.EndHorizontal();
            }

            if (!string.IsNullOrEmpty(_status))
            {
                GUILayout.Space(6);
                GUILayout.Label(_status, _headerStyle);
            }

            GUILayout.EndScrollView();
            GUI.DragWindow();
        }

        private static Kingdom FindLargestNonWardenKingdom()
        {
            var world = MapBox.instance;
            if (world?.kingdoms == null) return null;
            Kingdom best = null;
            foreach (var k in world.kingdoms)
            {
                if (k == null || !k.isCiv() || !k.isAlive() || WardenSystem.WardenKingdomIds.Contains(k.id)) continue;
                if (best == null || k.getPopulationPeople() > best.getPopulationPeople()) best = k;
            }
            return best;
        }
    }
}
