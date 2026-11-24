using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using NeoModLoader.api;
using UnityEngine;

namespace Arsenal
{
    public class Main : MonoBehaviour, IMod
    {
        public static Main Instance { get; private set; }
        public static ArsenalConfig Settings { get; private set; }

        private ModDeclare _declare;
        private GameObject _gameObject;
        private Harmony _harmony;

        private bool _showHud = false;
        private float _supplyTimer = 0f;
        private string _status = "";
        private Rect _windowRect = new Rect(350, 120, 350, 360);

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
            Settings = ArsenalConfig.LoadOrCreate(_declare.FolderPath);

            try
            {
                _harmony = new Harmony(_declare.UID);
                _harmony.PatchAll(Assembly.GetExecutingAssembly());
                ArsenalLibrary.RegisterAllAssets();
                Debug.Log($"[Arsenal] Loaded v{_declare.Version}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Arsenal] Failed to initialize: {e}");
            }
        }

        public void OnUnload()
        {
            _harmony?.UnpatchAll(_declare.UID);
            Debug.Log("[Arsenal] Unloaded.");
        }

        public void ToggleHud() => _showHud = !_showHud;

        private void Update()
        {
            if (Settings == null) return;

            if ((Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) && Input.GetKeyDown(KeyCode.A))
            {
                ToggleHud();
            }

            if (Config.paused || MapBox.instance == null) return;

            _supplyTimer += Time.deltaTime;
            if (_supplyTimer >= 10f)
            {
                _supplyTimer = 0f;
                ArsenalLibrary.RegisterAllAssets();
                ArsenalSupplySystem.TickAutoSupply();
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
            _boxStyle.normal.textColor = new Color(0.980f, 0.702f, 0.529f); // Catppuccin Peach

            _headerStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 13 };
            _headerStyle.normal.textColor = new Color(0.537f, 0.706f, 0.980f); // Catppuccin Blue

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
            _windowRect = GUI.Window(941213, _windowRect, DrawHud, "Arsenal: Blackpowder & Counters (Alt+A)", _boxStyle);
        }

        private void DrawHud(int id)
        {
            GUILayout.Label($"Issued: {ArsenalSupplySystem.TotalItemsIssued}  |  Deflected: {ArsenalCombatPatches.BulletsDeflected}", _headerStyle);
            GUILayout.Label($"Aegis Wards: {ArsenalCombatPatches.AegisWardsTriggered}  |  Halberd Flanks: {ArsenalCombatPatches.AntiGunFlankStrikes}", _labelStyle);
            GUILayout.Space(6);

            if (GUILayout.Button($"Auto Supply Armies: {(Settings.EnableAutoSupply ? "ON" : "OFF")}", _btnStyle))
            {
                Settings.EnableAutoSupply = !Settings.EnableAutoSupply;
                Settings.Save(_declare.FolderPath);
            }

            if (GUILayout.Button($"Era Gate (250+ pop, 3+ cities): {(Settings.EraGateEnabled ? "ON" : "YEAR 1 UNLOCK")}", _btnStyle))
            {
                Settings.EraGateEnabled = !Settings.EraGateEnabled;
                Settings.Save(_declare.FolderPath);
            }

            GUILayout.Space(4);
            GUILayout.Label($"Bastion Plate Bullet Deflection: {(int)(Settings.BastionBulletReduction * 100)}%", _labelStyle);
            float newRed = GUILayout.HorizontalSlider(Settings.BastionBulletReduction, 0.1f, 0.85f);
            if (Mathf.Abs(newRed - Settings.BastionBulletReduction) > 0.02f)
            {
                Settings.BastionBulletReduction = newRed;
                Settings.Save(_declare.FolderPath);
            }

            GUILayout.Space(6);
            if (GUILayout.Button("Issue Gunpowder Division -> #1 Kingdom", _btnStyle))
            {
                var top = GetTopKingdoms();
                if (top.Count >= 1)
                {
                    int n = ArsenalSupplySystem.SupplyKingdom(top[0], "gunpowder", 30);
                    _status = $"Armed {n} soldiers in {top[0].name} with firearms!";
                }
            }

            if (GUILayout.Button("Issue Anti-Gun Vanguard -> #2 Kingdom", _btnStyle))
            {
                var top = GetTopKingdoms();
                Kingdom target = top.Count >= 2 ? top[1] : (top.Count == 1 ? top[0] : null);
                if (target != null)
                {
                    int n = ArsenalSupplySystem.SupplyKingdom(target, "vanguard", 30);
                    _status = $"Armed {n} soldiers in {target.name} with Bastion Plate & Halberds!";
                }
            }

            if (!string.IsNullOrEmpty(_status))
            {
                GUILayout.Space(6);
                GUILayout.Label(_status, _headerStyle);
            }

            GUI.DragWindow();
        }

        private static List<Kingdom> GetTopKingdoms()
        {
            var list = new List<Kingdom>();
            if (MapBox.instance?.kingdoms == null) return list;
            foreach (var k in MapBox.instance.kingdoms)
            {
                if (k != null && k.isCiv() && k.isAlive()) list.Add(k);
            }
            list.Sort((a, b) => b.getPopulationPeople().CompareTo(a.getPopulationPeople()));
            return list;
        }
    }
}
