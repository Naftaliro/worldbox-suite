using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using NeoModLoader.api;
using UnityEngine;

namespace GrandWarfare
{
    public class Main : MonoBehaviour, IMod
    {
        public static Main Instance { get; private set; }
        public static GrandWarfareConfig Settings { get; private set; }

        public static int NavalVolleysFired { get; internal set; }
        public static int ShipsSunk { get; internal set; }
        public static int AttritionTicksApplied { get; internal set; }

        // Defense pacts: kingdomId -> set of allied defender kingdomIds
        public static readonly Dictionary<long, HashSet<long>> DefensePacts = new Dictionary<long, HashSet<long>>();

        private ModDeclare _declare;
        private GameObject _gameObject;
        private Harmony _harmony;

        private bool _showWarRoom = false;
        private float _supplyTimer = 0f;
        private float _navalTimer = 0f;
        private Rect _windowRect = new Rect(610, 86, 310, 290);

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
            Settings = GrandWarfareConfig.LoadOrCreate(_declare.FolderPath);

            try
            {
                _harmony = new Harmony(_declare.UID);
                _harmony.PatchAll(Assembly.GetExecutingAssembly());
                Debug.Log($"[GrandWarfare] Loaded v{_declare.Version}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[GrandWarfare] Init failed: {e}");
            }
        }

        public void OnUnload()
        {
            _harmony?.UnpatchAll(_declare.UID);
            Debug.Log("[GrandWarfare] Unloaded.");
        }

        public void ToggleHud() => _showWarRoom = !_showWarRoom;

        private void Update()
        {
            if (Settings == null) return;

            if (Input.GetKeyDown(Settings.WarRoomKey))
            {
                _showWarRoom = !_showWarRoom;
            }

            if (Config.paused || MapBox.instance == null) return;

            float dt = Time.deltaTime;
            if (Settings.EnableNavalCombat)
            {
                _navalTimer += dt;
                if (_navalTimer >= Settings.NavalVolleyCooldown)
                {
                    _navalTimer = 0f;
                    NavalCombatSystem.TickNavalCombat();
                }
            }

            if (Settings.EnableSupplyAttrition)
            {
                _supplyTimer += dt;
                if (_supplyTimer >= Settings.SupplyCheckInterval)
                {
                    _supplyTimer = 0f;
                    SupplyLineSystem.TickSupplyAttrition();
                }
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
            _boxStyle.normal.textColor = new Color(0.953f, 0.545f, 0.659f); // Catppuccin Red

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
            if (!_showWarRoom || Settings == null) return;
            EnsureStyles();
            _windowRect = GUI.Window(941205, _windowRect, DrawWarRoom, "GrandWarfare War Room (F10)", _boxStyle);
        }

        private void DrawWarRoom(int id)
        {
            var world = MapBox.instance;
            long activeWars = world?.wars != null ? world.wars.countActiveWars() : 0;

            GUILayout.Label($"Active Wars: {activeWars}  |  Pacts: {DefensePacts.Count}", _headerStyle);
            GUILayout.Label($"Naval Volleys: {NavalVolleysFired}  |  Ships Sunk: {ShipsSunk}", _labelStyle);
            GUILayout.Label($"Supply Attrition Ticks: {AttritionTicksApplied}", _labelStyle);
            GUILayout.Space(6);

            if (GUILayout.Button($"Naval Combat: {(Settings.EnableNavalCombat ? "ON" : "OFF")}", _btnStyle))
            {
                Settings.EnableNavalCombat = !Settings.EnableNavalCombat;
                Settings.Save(_declare.FolderPath);
            }

            if (GUILayout.Button($"Coastal Bombardment: {(Settings.EnableCoastalBombardment ? "ON" : "OFF")}", _btnStyle))
            {
                Settings.EnableCoastalBombardment = !Settings.EnableCoastalBombardment;
                Settings.Save(_declare.FolderPath);
            }

            if (GUILayout.Button($"Supply Line Attrition: {(Settings.EnableSupplyAttrition ? "ON" : "OFF")}", _btnStyle))
            {
                Settings.EnableSupplyAttrition = !Settings.EnableSupplyAttrition;
                Settings.Save(_declare.FolderPath);
            }

            if (GUILayout.Button($"Defense Pacts: {(Settings.EnableDefensePacts ? "ON" : "OFF")}", _btnStyle))
            {
                Settings.EnableDefensePacts = !Settings.EnableDefensePacts;
                Settings.Save(_declare.FolderPath);
            }

            if (GUILayout.Button("Form Pact: Top 2 Peaceful Kingdoms", _btnStyle))
            {
                DiplomacyPactSystem.TryLinkTopPeacefulKingdoms();
            }

            GUI.DragWindow();
        }
    }

    public static class NavalCombatSystem
    {
        public static void TickNavalCombat()
        {
            var world = MapBox.instance;
            if (world == null || world.units == null || world.units.units_only_alive == null) return;

            var settings = Main.Settings;
            float rangeSq = settings.NavalAttackRangeTiles * settings.NavalAttackRangeTiles;

            // Gather active naval vessels belonging to civilized kingdoms
            var ships = new List<Actor>(64);
            var alive = world.units.units_only_alive;
            for (int i = 0; i < alive.Count; i++)
            {
                Actor u = alive[i];
                if (u == null || !u.isAlive() || u.current_tile == null || u.kingdom == null || !u.kingdom.isCiv())
                    continue;
                if (u.asset != null && u.asset.is_boat)
                {
                    ships.Add(u);
                }
            }

            if (ships.Count == 0) return;

            for (int i = 0; i < ships.Count; i++)
            {
                Actor shooter = ships[i];
                if (!shooter.isAlive() || shooter.current_tile == null) continue;

                // 1. Ship-to-ship engagement
                Actor bestShip = null;
                float bestDistSq = rangeSq;

                for (int j = 0; j < ships.Count; j++)
                {
                    if (i == j) continue;
                    Actor target = ships[j];
                    if (!target.isAlive() || target.current_tile == null) continue;
                    if (!shooter.kingdom.isEnemy(target.kingdom)) continue;

                    float dx = shooter.current_tile.x - target.current_tile.x;
                    float dy = shooter.current_tile.y - target.current_tile.y;
                    float dSq = dx * dx + dy * dy;
                    if (dSq < bestDistSq)
                    {
                        bestDistSq = dSq;
                        bestShip = target;
                    }
                }

                if (bestShip != null)
                {
                    FireBroadside(shooter, bestShip, settings.NavalDamage);
                    continue;
                }

                // 2. Coastal bombardment of enemy soldiers near shorelines
                if (settings.EnableCoastalBombardment)
                {
                    Actor shoreTarget = FindEnemyNearCoast(shooter, alive, rangeSq * 0.7f);
                    if (shoreTarget != null)
                    {
                        FireBroadside(shooter, shoreTarget, Math.Max(6, settings.NavalDamage / 2));
                    }
                }
            }
        }

        private static readonly FieldInfo InsideBoatField =
            AccessTools.Field(typeof(Actor), "is_inside_boat");

        public static bool IsInsideBoat(Actor a)
        {
            return a != null && InsideBoatField != null && InsideBoatField.GetValue(a) is bool b && b;
        }

        private static Actor FindEnemyNearCoast(Actor ship, List<Actor> alive, float maxDistSq)
        {
            // Sample stride keeps coastal search fast even with 10,000+ units
            int stride = alive.Count > 2500 ? 4 : 1;
            int offset = Time.frameCount % stride;
            for (int i = offset; i < alive.Count; i += stride)
            {
                Actor cand = alive[i];
                if (cand == null || !cand.isAlive() || cand.current_tile == null) continue;
                if (cand.kingdom == null || !ship.kingdom.isEnemy(cand.kingdom)) continue;
                if (IsInsideBoat(cand)) continue;

                float dx = ship.current_tile.x - cand.current_tile.x;
                float dy = ship.current_tile.y - cand.current_tile.y;
                if (dx * dx + dy * dy <= maxDistSq)
                {
                    return cand;
                }
            }
            return null;
        }

        private static void FireBroadside(Actor shooter, Actor target, int damage)
        {
            var world = MapBox.instance;
            if (world?.projectiles != null && shooter.current_tile != null && target.current_tile != null)
            {
                try
                {
                    world.projectiles.spawn(
                        shooter,
                        target,
                        "arrow",
                        shooter.current_tile.posV3,
                        target.current_tile.posV3,
                        1.5f,
                        0.4f
                    );
                }
                catch
                {
                    // Fallback to direct broadside impact if projectile asset differs
                }
            }

            bool wasAlive = target.isAlive();
            target.changeHealth(-damage);
            target.startShake(0.25f, 0.08f, true, true);
            Main.NavalVolleysFired++;

            if (wasAlive && target.getHealth() <= 0 && target.asset != null && target.asset.is_boat)
            {
                Main.ShipsSunk++;
            }
        }
    }

    public static class SupplyLineSystem
    {
        public static void TickSupplyAttrition()
        {
            var world = MapBox.instance;
            if (world == null || world.units == null || world.units.units_only_alive == null) return;

            var settings = Main.Settings;
            float maxDistSq = settings.MaxSupplyDistanceTiles * settings.MaxSupplyDistanceTiles;
            var alive = world.units.units_only_alive;

            for (int i = 0; i < alive.Count; i++)
            {
                Actor unit = alive[i];
                if (unit == null || !unit.isAlive() || unit.current_tile == null) continue;
                if (unit.kingdom == null || !unit.kingdom.isCiv() || NavalCombatSystem.IsInsideBoat(unit)) continue;
                if (unit.asset != null && unit.asset.is_boat) continue;
                if (unit.army == null) continue; // Only campaigning army soldiers suffer deep supply attrition

                if (!IsWithinFriendlySupply(unit, maxDistSq))
                {
                    unit.decreaseNutrition(10);
                    if (unit.getHealth() > settings.AttritionHealthLoss + 5)
                    {
                        unit.changeHealth(-settings.AttritionHealthLoss);
                    }
                    Main.AttritionTicksApplied++;
                }
            }
        }

        private static bool IsWithinFriendlySupply(Actor unit, float maxDistSq)
        {
            var cities = unit.kingdom.cities;
            if (cities == null || cities.Count == 0) return true;

            int ux = unit.current_tile.x;
            int uy = unit.current_tile.y;

            for (int i = 0; i < cities.Count; i++)
            {
                City city = cities[i];
                WorldTile cTile = city?.getTile();
                if (cTile == null) continue;
                float dx = ux - cTile.x;
                float dy = uy - cTile.y;
                if (dx * dx + dy * dy <= maxDistSq)
                {
                    return true;
                }
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(DiplomacyManager))]
    public static class DiplomacyPactSystem
    {
        public static void AddDefensePact(Kingdom a, Kingdom b)
        {
            if (a == null || b == null || a.id == b.id) return;
            if (!Main.DefensePacts.TryGetValue(a.id, out var setA))
            {
                setA = new HashSet<long>();
                Main.DefensePacts[a.id] = setA;
            }
            if (!Main.DefensePacts.TryGetValue(b.id, out var setB))
            {
                setB = new HashSet<long>();
                Main.DefensePacts[b.id] = setB;
            }
            setA.Add(b.id);
            setB.Add(a.id);
            Debug.Log($"[GrandWarfare] Defense pact formed between {a.name} and {b.name}");
        }

        public static void TryLinkTopPeacefulKingdoms()
        {
            var world = MapBox.instance;
            if (world?.kingdoms == null) return;

            var civs = new List<Kingdom>();
            foreach (var k in world.kingdoms)
            {
                if (k != null && k.isCiv() && k.isAlive())
                {
                    civs.Add(k);
                }
            }
            civs.Sort((a, b) => b.getPopulationPeople().CompareTo(a.getPopulationPeople()));

            for (int i = 0; i < civs.Count; i++)
            {
                for (int j = i + 1; j < civs.Count; j++)
                {
                    if (!civs[i].isEnemy(civs[j]))
                    {
                        AddDefensePact(civs[i], civs[j]);
                        return;
                    }
                }
            }
        }

        // When a war starts, pull any defense-pact allies of the defender onto the defending side
        [HarmonyPostfix]
        [HarmonyPatch("startWar")]
        private static void StartWarPostfix(Kingdom pAttacker, Kingdom pDefender, War __result)
        {
            if (Main.Settings == null || !Main.Settings.EnableDefensePacts) return;
            if (__result == null || pAttacker == null || pDefender == null) return;
            if (!Main.DefensePacts.TryGetValue(pDefender.id, out var allies)) return;

            var world = MapBox.instance;
            if (world?.kingdoms == null) return;

            foreach (long allyId in allies)
            {
                if (allyId == pAttacker.id) continue;
                Kingdom ally = world.kingdoms.get(allyId);
                if (ally != null && ally.isCiv() && ally.isAlive() && !__result.hasKingdom(ally))
                {
                    __result.joinDefenders(ally);
                    Debug.Log($"[GrandWarfare] {ally.name} honored its defense pact and joined {pDefender.name}!");
                }
            }
        }
    }
}
