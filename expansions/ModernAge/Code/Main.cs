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
        private Rect _windowRect = new Rect(340, 86, 380, 460);

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
            _harmony?.UnpatchSelf();
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

        private void OnGUI()
        {
            GeopoliticsAndTradeSystem.DrawRadarUmbrellas();

            if (!_showHud || Settings == null) return;
            GameUiStyle.BeginFrame();
            _windowRect = GUI.Window(941215, _windowRect, DrawHud, "ModernAge Expansion (Alt+M)", GameUiStyle.WindowStyle);
            GameUiStyle.DrawTooltip(949010);
        }

        private void DrawHud(int id)
        {
            GameUiStyle.DrawCloseButton(_windowRect, () => _showHud = false);
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(410));

            GameUiStyle.Label(
                $"Vehicles: {GeopoliticsAndTradeSystem.VehiclesDeployed}  |  SAM Shots: {GeopoliticsAndTradeSystem.RadarSamShots}  |  Highways: {GeopoliticsAndTradeSystem.HighwaysBuilt}",
                "Modern Warfare Telemetry|Total armored vehicles and aircraft deployed, radar SAM interceptions fired, and inter-city highways paved.",
                isHeader: true
            );
            GameUiStyle.Label(
                $"Counter-Play — Deflected: {ModernCombatPatches.BulletsDeflected} | AT Hits: {ModernCombatPatches.ArmorKillsCountered} | AA Hits: {ModernCombatPatches.AirIntercepts}",
                "Rock-Paper-Scissors Counters|Bullets deflected by Bastion plate/Aegis wards, armor countered by AT rifles/launchers, and aircraft hit by Flak/SAMs."
            );
            GUILayout.Space(6);

            if (GameUiStyle.ToggleButton(
                $"Auto Tier 1-3 Progression: {(Settings.EnableAutoProgression ? "ON" : "OFF")}",
                Settings.EnableAutoProgression,
                "Automatic Tech Eras|Kingdoms naturally advance from Tier 1 blackpowder to Tier 2 industrial armor/props and Tier 3 modern MBTs, jets, and radar SAMs.|Saved automatically"))
            {
                Settings.EnableAutoProgression = !Settings.EnableAutoProgression;
                Settings.Save(_declare.FolderPath);
            }

            if (GameUiStyle.ToggleButton(
                $"Radar SAM Umbrellas: {(Settings.ShowRadarRings ? "VISIBLE" : "HIDDEN")}",
                Settings.ShowRadarRings,
                "Capital Air-Defense Rings|Shows glowing radar coverage domes around Tier 3 capitals that intercept incoming enemy jets and helicopters.|Saved automatically"))
            {
                Settings.ShowRadarRings = !Settings.ShowRadarRings;
                Settings.Save(_declare.FolderPath);
            }

            GUILayout.Space(6);
            GameUiStyle.Label("Quick Sandbox Deployment (#1 Kingdom):", "Sandbox Controls|Test modern armies immediately by advancing or equipping the largest kingdom in your world.", isHeader: true);

            if (GameUiStyle.Button(
                "Advance #1 Kingdom +1 Tech Tier (T1->T2->T3)",
                "Upgrade Tech Tier|Promotes the largest kingdom by one technology tier and issues tier-appropriate weapons and counters to 15 soldiers."))
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

            if (GameUiStyle.Button(
                "Deploy Armored Column (2 MBTs + 2 Light Tanks)",
                "Spawn Armored Division|Deploys 2 Main Battle Tanks and 2 Light Tanks at the largest kingdom's capital."))
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

            if (GameUiStyle.Button(
                "Scramble Air Wing (2 Jets + 2 Attack Helis)",
                "Spawn Air Squadron|Scrambles 2 Supersonic Jets and 2 Attack Helicopters over the largest kingdom's capital."))
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
            GameUiStyle.Label("World Governments & Trade Blocs:", "Geopolitics Overview|Shows each empire's tech tier, government system, and trade specialty. Click Gov to cycle a kingdom's regime.", isHeader: true);
            if (MapBox.instance?.kingdoms != null)
            {
                int shown = 0;
                foreach (Kingdom k in MapBox.instance.kingdoms)
                {
                    if (k == null || !k.isCiv() || !k.isAlive() || shown >= 8) continue;
                    var st = GeopoliticsAndTradeSystem.GetOrCreateState(k);
                    GUILayout.BeginHorizontal();
                    GameUiStyle.Label(
                        $"• {k.name} [T{st.TechTier}] {st.Government} · {st.Specialty}",
                        $"{k.name}|Tech Tier {st.TechTier} · Government: {st.Government} · Trade Specialty: {st.Specialty}.");
                    if (GameUiStyle.Button("Gov", $"Switch {k.name} Government|Cycle between Monarchy, Republic, Collectivist, Merchant League, Military Junta, and Technocracy.", GUILayout.Width(42)))
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
                GameUiStyle.Label(_status, null, isHeader: true);
            }

            GUILayout.EndScrollView();
            GameUiStyle.CaptureTooltip();
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
