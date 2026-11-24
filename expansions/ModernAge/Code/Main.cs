using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using NeoModLoader.api;
using UnityEngine;

namespace ModernAge
{
    public class Main : MonoBehaviour, IMod
    {
        public static Main Instance { get; private set; }
        public static ModernAgeConfig Settings { get; private set; }

        private ModDeclare _declare;
        private GameObject _gameObject;
        private Harmony _harmony;

        private bool _showHud = false;
        private float _tickTimer = 0f;
        private string _status = "";
        private Vector2 _scroll;
        private Rect _windowRect = new Rect(340, 86, 370, 440);

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
            Settings = ModernAgeConfig.LoadOrCreate(_declare.FolderPath);

            try
            {
                _harmony = new Harmony(_declare.UID);
                _harmony.PatchAll(Assembly.GetExecutingAssembly());
                ModernAssetRegistry.RegisterAll();
                VehicleSpritePatches.LoadSprites(_declare.FolderPath);
                Debug.Log($"[ModernAge] Expansion loaded v{_declare.Version}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ModernAge] Failed to initialize: {e}");
            }
        }

        public void OnUnload()
        {
            _harmony?.UnpatchAll(_declare.UID);
            Debug.Log("[ModernAge] Unloaded.");
        }

        public void ToggleHud() => _showHud = !_showHud;

        private void Update()
        {
            if (Settings == null) return;

            if ((Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) && Input.GetKeyDown(KeyCode.M))
            {
                ToggleHud();
            }

            if (Config.paused || MapBox.instance == null) return;

            _tickTimer += Time.deltaTime;
            if (_tickTimer >= 9f)
            {
                _tickTimer = 0f;
                ModernAssetRegistry.RegisterAll();
                GeopoliticsAndTradeSystem.TickAll();
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
            _boxStyle.normal.textColor = new Color(0.455f, 0.780f, 0.925f); // Catppuccin Sapphire

            _headerStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 13 };
            _headerStyle.normal.textColor = new Color(0.980f, 0.702f, 0.529f); // Catppuccin Peach

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
            GeopoliticsAndTradeSystem.DrawRadarUmbrellas();

            if (!_showHud || Settings == null) return;
            EnsureStyles();
            _windowRect = GUI.Window(941215, _windowRect, DrawHud, "ModernAge Expansion (Alt+M)", _boxStyle);
        }

        private void DrawHud(int id)
        {
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(390));

            GUILayout.Label(
                $"Vehicles: {GeopoliticsAndTradeSystem.VehiclesDeployed}  |  SAM Shots: {GeopoliticsAndTradeSystem.RadarSamShots}  |  Highways: {GeopoliticsAndTradeSystem.HighwaysBuilt}",
                _headerStyle
            );
            GUILayout.Label(
                $"Counter-Play — Deflected: {ModernCombatPatches.BulletsDeflected} | AT Hits: {ModernCombatPatches.ArmorKillsCountered} | AA Hits: {ModernCombatPatches.AirIntercepts}",
                _labelStyle
            );
            GUILayout.Space(6);

            if (GUILayout.Button($"Auto Tier 1-3 Progression: {(Settings.EnableAutoProgression ? "ON" : "OFF")}", _btnStyle))
            {
                Settings.EnableAutoProgression = !Settings.EnableAutoProgression;
                Settings.Save(_declare.FolderPath);
            }

            if (GUILayout.Button($"Radar SAM Umbrellas: {(Settings.ShowRadarRings ? "VISIBLE" : "HIDDEN")}", _btnStyle))
            {
                Settings.ShowRadarRings = !Settings.ShowRadarRings;
                Settings.Save(_declare.FolderPath);
            }

            GUILayout.Space(6);
            GUILayout.Label("Quick Sandbox Deployment (#1 Kingdom):", _headerStyle);

            if (GUILayout.Button("Advance #1 Kingdom +1 Tech Tier (T1->T2->T3)", _btnStyle))
            {
                Kingdom top = GetTopKingdom();
                if (top != null)
                {
                    var st = GeopoliticsAndTradeSystem.GetOrCreateState(top);
                    st.TechTier = Math.Min(3, st.TechTier + 1);
                    GeopoliticsAndTradeSystem.EquipTierInfantry(MapBox.instance, top, st.TechTier, 15);
                    _status = $"{top.name} advanced to Tier {st.TechTier}!";
                }
            }

            if (GUILayout.Button("Deploy Armored Column (2 MBTs + 2 Light Tanks)", _btnStyle))
            {
                Kingdom top = GetTopKingdom();
                WorldTile cap = top?.capital?.getTile();
                if (top != null && cap != null)
                {
                    ModernAssetRegistry.RegisterAll();
                    GeopoliticsAndTradeSystem.SpawnVehicleForKingdom(MapBox.instance, top, ModernIds.MainBattleTank, cap);
                    GeopoliticsAndTradeSystem.SpawnVehicleForKingdom(MapBox.instance, top, ModernIds.MainBattleTank, cap);
                    GeopoliticsAndTradeSystem.SpawnVehicleForKingdom(MapBox.instance, top, ModernIds.LightTank, cap);
                    GeopoliticsAndTradeSystem.SpawnVehicleForKingdom(MapBox.instance, top, ModernIds.LightTank, cap);
                    _status = $"Deployed Armored Column for {top.name}!";
                }
            }

            if (GUILayout.Button("Scramble Air Wing (2 Jets + 2 Attack Helis)", _btnStyle))
            {
                Kingdom top = GetTopKingdom();
                WorldTile cap = top?.capital?.getTile();
                if (top != null && cap != null)
                {
                    ModernAssetRegistry.RegisterAll();
                    GeopoliticsAndTradeSystem.SpawnVehicleForKingdom(MapBox.instance, top, ModernIds.SupersonicJet, cap);
                    GeopoliticsAndTradeSystem.SpawnVehicleForKingdom(MapBox.instance, top, ModernIds.SupersonicJet, cap);
                    GeopoliticsAndTradeSystem.SpawnVehicleForKingdom(MapBox.instance, top, ModernIds.AttackHeli, cap);
                    GeopoliticsAndTradeSystem.SpawnVehicleForKingdom(MapBox.instance, top, ModernIds.AttackHeli, cap);
                    _status = $"Scrambled Air Wing for {top.name}!";
                }
            }

            GUILayout.Space(8);
            GUILayout.Label("World Governments & Trade Blocs:", _headerStyle);
            if (MapBox.instance?.kingdoms != null)
            {
                int shown = 0;
                foreach (Kingdom k in MapBox.instance.kingdoms)
                {
                    if (k == null || !k.isCiv() || !k.isAlive() || shown >= 8) continue;
                    var st = GeopoliticsAndTradeSystem.GetOrCreateState(k);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"• {k.name} [T{st.TechTier}] {st.Government} · {st.Specialty}", _labelStyle);
                    if (GUILayout.Button("Gov", _btnStyle, GUILayout.Width(38)))
                    {
                        int n = Enum.GetValues(typeof(GovernmentType)).Length;
                        st.Government = (GovernmentType)(((int)st.Government + 1) % n);
                    }
                    GUILayout.EndHorizontal();
                    shown++;
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

        private static Kingdom GetTopKingdom()
        {
            var world = MapBox.instance;
            if (world?.kingdoms == null) return null;
            Kingdom best = null;
            foreach (var k in world.kingdoms)
            {
                if (k != null && k.isCiv() && k.isAlive())
                {
                    if (best == null || k.getPopulationPeople() > best.getPopulationPeople()) best = k;
                }
            }
            return best;
        }
    }
}
