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
        public static readonly List<string> ActiveBlockades = new List<string>();

        // Defense pacts: kingdomId -> set of allied defender kingdomIds
        public static readonly Dictionary<long, HashSet<long>> DefensePacts = new Dictionary<long, HashSet<long>>();

        private ModDeclare _declare;
        private GameObject _gameObject;
        private Harmony _harmony;

        private bool _showWarRoom = false;
        private GuiOverlayRunner _panelRunner;
        private int _warRoomTab = 0; // 0 = Warfare & Logistics, 1 = Intelligence & Spies
        private Vector2 _intelScroll;
        private float _supplyTimer = 0f;
        private float _navalTimer = 0f;
        private Rect _windowRect = new Rect(610, 86, 365, 420);

        public ModDeclare GetDeclaration() => _declare;
        public GameObject GetGameObject() => _gameObject;
        public string GetUrl() => "https://github.com/naftaliro/worldbox-suite";

        public void OnLoad(ModDeclare pModDecl, GameObject pGameObject)
        {
            Instance = this;
            _declare = pModDecl;
            _gameObject = pGameObject;
            _panelRunner = pGameObject.AddComponent<GuiOverlayRunner>();
            _panelRunner.Configure(true, DrawGui);
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
            _harmony?.UnpatchSelf();
            Debug.Log("[GrandWarfare] Unloaded.");
        }

        public void ToggleHud()
        {
            _showWarRoom = !_showWarRoom;
            if (_panelRunner != null) _panelRunner.enabled = _showWarRoom;
        }

        public bool GetState(string key)
        {
            if (Settings == null) return false;
            switch (key)
            {
                case "naval": return Settings.EnableNavalCombat;
                case "coastal": return Settings.EnableCoastalBombardment;
                case "blockades": return Settings.EnableNavalBlockades;
                case "supply": return Settings.EnableSupplyAttrition;
                case "pacts": return Settings.EnableDefensePacts;
                case "espionage": return Settings.EnableEspionage;
                case "espionage_chaotic": return Settings.ChaoticEspionageMode;
                default: return false;
            }
        }

        public string ToggleNavalCombat()
        {
            Settings.EnableNavalCombat = !Settings.EnableNavalCombat;
            Settings.Save(_declare.FolderPath);
            return $"Naval Broadsides: {(Settings.EnableNavalCombat ? "ON" : "OFF")}";
        }

        public string ToggleCoastalBombardment()
        {
            Settings.EnableCoastalBombardment = !Settings.EnableCoastalBombardment;
            Settings.Save(_declare.FolderPath);
            return $"Coastal Bombardment: {(Settings.EnableCoastalBombardment ? "ON" : "OFF")}";
        }

        public string ToggleNavalBlockades()
        {
            Settings.EnableNavalBlockades = !Settings.EnableNavalBlockades;
            Settings.Save(_declare.FolderPath);
            return $"Naval Port Blockades: {(Settings.EnableNavalBlockades ? "ON" : "OFF")}";
        }

        public string ToggleSupplyAttrition()
        {
            Settings.EnableSupplyAttrition = !Settings.EnableSupplyAttrition;
            Settings.Save(_declare.FolderPath);
            return $"Supply Line Attrition: {(Settings.EnableSupplyAttrition ? "ON" : "OFF")}";
        }

        public string ToggleDefensePacts()
        {
            Settings.EnableDefensePacts = !Settings.EnableDefensePacts;
            Settings.Save(_declare.FolderPath);
            return $"Mutual Defense Pacts: {(Settings.EnableDefensePacts ? "ON" : "OFF")}";
        }

        public string ToggleEspionage()
        {
            Settings.EnableEspionage = !Settings.EnableEspionage;
            Settings.Save(_declare.FolderPath);
            return $"Kingdom Espionage & Counter-Intel: {(Settings.EnableEspionage ? "ON" : "OFF")}";
        }

        public string ToggleEspionageIntensity()
        {
            Settings.ChaoticEspionageMode = !Settings.ChaoticEspionageMode;
            Settings.Save(_declare.FolderPath);
            return $"Espionage Intensity: {(Settings.ChaoticEspionageMode ? "Active / Chaotic" : "Rare / Balanced")}";
        }

        public string DispatchSpyNow()
        {
            return EspionageSystem.DispatchSpyNow();
        }

        public string GetWarCause(long warId)
        {
            return EspionageSystem.GetWarCause(warId);
        }

        public string FormTopDefensePact()
        {
            int before = DefensePacts.Count;
            DiplomacyPactSystem.TryLinkTopPeacefulKingdoms();
            return DefensePacts.Count > before ? "Formed defense pact between top 2 peaceful kingdoms!" : "Defense pact refreshed for top peaceful kingdoms.";
        }

        private void Update()
        {
            if (Settings == null) return;

            EspionageSystem.EnsureAssetsRegistered();

            if (Input.GetKeyDown(Settings.WarRoomKey))
            {
                ToggleHud();
            }

            if (Config.paused || MapBox.instance == null) return;

            float dt = Time.deltaTime;
            EspionageSystem.Tick(dt);

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
                    if (Settings.EnableNavalBlockades)
                    {
                        NavalCombatSystem.TickNavalBlockades();
                    }
                }
            }
        }

        private void DrawGui()
        {
            if (!_showWarRoom || Settings == null) return;
            GameUiStyle.BeginFrame();
            _windowRect = GUI.Window(941205, _windowRect, DrawWarRoom, "GrandWarfare War Room (F10)", GameUiStyle.WindowStyle);
            GameUiStyle.DrawTooltip(949004);
        }

        private void DrawWarRoom(int id)
        {
            GameUiStyle.DrawCloseButton(_windowRect, ToggleHud);

            GUILayout.BeginHorizontal();
            if (GameUiStyle.ToggleButton(
                "Warfare & Logistics",
                _warRoomTab == 0,
                "Warfare & Logistics|Naval broadsides, coastal shelling, port blockades, supply attrition, and mutual defense pacts."))
            {
                _warRoomTab = 0;
            }
            if (GameUiStyle.ToggleButton(
                "Intelligence & Spies",
                _warRoomTab == 1,
                "Intelligence & Counter-Intelligence|Manage covert spy infiltration, palace coups, counter-intelligence, and peaceful throne takeovers."))
            {
                _warRoomTab = 1;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            if (_warRoomTab == 0)
            {
                DrawWarfareTab();
            }
            else
            {
                DrawIntelligenceTab();
            }

            GameUiStyle.CaptureTooltip();
            GUI.DragWindow();
        }

        private void DrawWarfareTab()
        {
            var world = MapBox.instance;
            long activeWars = world?.wars != null ? world.wars.countActiveWars() : 0;

            GameUiStyle.Label($"Active Wars: {activeWars}  |  Pacts: {DefensePacts.Count}", "War Room Overview|Ongoing world wars and mutual defense treaties currently enforced by GrandWarfare.", isHeader: true);
            GameUiStyle.Label($"Naval Volleys: {NavalVolleysFired}  |  Ships Sunk: {ShipsSunk}", "Naval Combat Stats|Broadside volleys exchanged and enemy vessels sunk during this session.");
            GameUiStyle.Label($"Supply Attrition: {AttritionTicksApplied}  |  Blockades: {ActiveBlockades.Count}", "Logistics & Blockades|Attrition ticks dealt to overextended armies and enemy ports currently under naval blockade.");
            GUILayout.Space(6);

            if (GameUiStyle.ToggleButton(
                $"Naval Combat: {(Settings.EnableNavalCombat ? "ON" : "OFF")}",
                Settings.EnableNavalCombat,
                "Ship-to-Ship Broadsides|Allows warships of warring kingdoms to fire volleys and sink enemy boats at sea.|Saved automatically"))
            {
                Settings.EnableNavalCombat = !Settings.EnableNavalCombat;
                Settings.Save(_declare.FolderPath);
            }

            if (GameUiStyle.ToggleButton(
                $"Coastal Bombardment: {(Settings.EnableCoastalBombardment ? "ON" : "OFF")}",
                Settings.EnableCoastalBombardment,
                "Coastal Bombardment|Warships fire upon hostile land soldiers marching near coastlines when no enemy vessels are in range.|Saved automatically"))
            {
                Settings.EnableCoastalBombardment = !Settings.EnableCoastalBombardment;
                Settings.Save(_declare.FolderPath);
            }

            if (GameUiStyle.ToggleButton(
                $"Naval Blockades: {(Settings.EnableNavalBlockades ? "ON" : "OFF")}",
                Settings.EnableNavalBlockades,
                "Port Blockades|Enemy ships holding within 24 tiles of a hostile coastal city drain nutrition from its defending garrison.|Saved automatically"))
            {
                Settings.EnableNavalBlockades = !Settings.EnableNavalBlockades;
                Settings.Save(_declare.FolderPath);
            }

            if (GameUiStyle.ToggleButton(
                $"Supply Line Attrition: {(Settings.EnableSupplyAttrition ? "ON" : "OFF")}",
                Settings.EnableSupplyAttrition,
                "Supply-Line Attrition|Campaigning armies marching far beyond friendly city supply range gradually lose nutrition and health.|Saved automatically"))
            {
                Settings.EnableSupplyAttrition = !Settings.EnableSupplyAttrition;
                Settings.Save(_declare.FolderPath);
            }

            if (GameUiStyle.ToggleButton(
                $"Defense Pacts: {(Settings.EnableDefensePacts ? "ON" : "OFF")}",
                Settings.EnableDefensePacts,
                "Mutual Defense Pacts|Automatically pulls allied pact partners into defensive wars whenever a member kingdom is attacked.|Saved automatically"))
            {
                Settings.EnableDefensePacts = !Settings.EnableDefensePacts;
                Settings.Save(_declare.FolderPath);
            }

            if (GameUiStyle.Button(
                "Form Pact: Top 2 Peaceful Kingdoms",
                "Forge Defense Pact|Links the two largest non-hostile kingdoms into a mutual defense treaty so they defend each other if invaded."))
            {
                DiplomacyPactSystem.TryLinkTopPeacefulKingdoms();
            }
        }

        private void DrawIntelligenceTab()
        {
            GameUiStyle.Label(
                $"Active Spies: {EspionageSystem.ActiveMissions.Count}  |  Caught: {EspionageSystem.SpiesCaught}  |  Takeovers: {EspionageSystem.PeacefulTakeovers}",
                "Intelligence Overview|Live covert agents currently operating abroad, spies caught by counter-intelligence, and completed peaceful kingdom takeovers.",
                isHeader: true);

            if (GameUiStyle.ToggleButton(
                $"Espionage & Counter-Intel: {(Settings.EnableEspionage ? "ON" : "OFF")}",
                Settings.EnableEspionage,
                "Kingdom Espionage|Allows rival kingdoms to infiltrate agents into target capitals, rise to city leader, stage palace coups, and unite kingdoms peacefully — or get caught by counter-intelligence.|Saved automatically"))
            {
                ToggleEspionage();
            }

            string intensityLabel = Settings.ChaoticEspionageMode ? "Active / Chaotic" : "Rare / Balanced";
            if (GameUiStyle.ToggleButton(
                $"Intensity: {intensityLabel}",
                Settings.ChaoticEspionageMode,
                "Espionage Intensity|Rare/Balanced: up to 4 global spies, 180s cooldown, slower consolidation.\nActive/Chaotic: up to 8 global spies, 60s cooldown, faster coups and unions.|Saved automatically"))
            {
                ToggleEspionageIntensity();
            }

            if (GameUiStyle.Button(
                "Dispatch Spy Now",
                "Manual Covert Operation|Immediately infiltrate a covert agent from a rival kingdom into a stronger target capital."))
            {
                string msg = DispatchSpyNow();
                try { WorldTip.showNowTop(msg, false); } catch { }
            }

            GUILayout.Space(5);
            _intelScroll = GUILayout.BeginScrollView(_intelScroll, GUILayout.Height(210));

            GameUiStyle.Label($"Active Operations ({EspionageSystem.ActiveMissions.Count})", "Active Spy Cells|Live missions with current infiltration stage, influence progress, and cover integrity.", isHeader: true);
            if (EspionageSystem.ActiveMissions.Count == 0)
            {
                GameUiStyle.Subtle("No active covert agents in the field.");
            }
            else
            {
                var missionsSnapshot = new List<SpyMission>(EspionageSystem.ActiveMissions);
                for (int i = 0; i < missionsSnapshot.Count; i++)
                {
                    SpyMission m = missionsSnapshot[i];
                    string stage = EspionageSystem.FormatStage(m.Stage);
                    string line = $"{m.SpyName} ({m.OriginName} → {m.TargetName})";
                    string detail = $"   {stage}  |  Inf: {m.Influence:0}%  |  Cover: {m.Cover:0}%";
                    string tip = $"{m.SpyName}|Origin: {m.OriginName}\nTarget: {m.TargetName}\nStage: {stage}\nInfluence: {m.Influence:0}%  |  Cover: {m.Cover:0}%";
                    GameUiStyle.Label(line, tip);
                    GameUiStyle.Subtle(detail, tip);
                }
            }

            GUILayout.Space(6);
            GameUiStyle.Label("Recent Intelligence Log", "Intelligence Feed|Latest dispatches, promotions, palace coups, executions, and peaceful unions.", isHeader: true);
            if (EspionageSystem.RecentEvents.Count == 0)
            {
                GameUiStyle.Subtle("No intelligence reports yet.");
            }
            else
            {
                var eventsSnapshot = new List<string>(EspionageSystem.RecentEvents);
                for (int i = eventsSnapshot.Count - 1; i >= 0; i--)
                {
                    GameUiStyle.Subtle("• " + eventsSnapshot[i]);
                }
            }

            GUILayout.EndScrollView();
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

        private static readonly FieldInfo ActorInsideBoatField = AccessTools.Field(typeof(Actor), "is_inside_boat");
        private static readonly AccessTools.FieldRef<Actor, bool> ActorInsideBoatRef = InitInsideBoatRef();

        private static AccessTools.FieldRef<Actor, bool> InitInsideBoatRef()
        {
            try { return AccessTools.FieldRefAccess<Actor, bool>("is_inside_boat"); }
            catch { return null; }
        }

        public static bool IsInsideBoat(Actor a)
        {
            if (a == null) return false;
            if (ActorInsideBoatRef != null) return ActorInsideBoatRef(a);
            if (ActorInsideBoatField == null) return false;
            try
            {
                return ActorInsideBoatField.GetValue(a) is bool b && b;
            }
            catch
            {
                return false;
            }
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

        private static readonly FieldInfo CityKingdomField = AccessTools.Field(typeof(City), "kingdom");

        /// <summary>
        /// Enemy warships hovering within blockade range of a city starve its defending garrison.
        /// </summary>
        public static void TickNavalBlockades()
        {
            Main.ActiveBlockades.Clear();
            var world = MapBox.instance;
            var settings = Main.Settings;
            if (world?.cities == null || world.units?.units_only_alive == null || settings == null) return;

            var alive = world.units.units_only_alive;
            var ships = new List<Actor>(32);
            for (int i = 0; i < alive.Count; i++)
            {
                Actor u = alive[i];
                if (u != null && u.isAlive() && u.current_tile != null && u.kingdom != null && u.kingdom.isCiv() && u.asset != null && u.asset.is_boat)
                {
                    ships.Add(u);
                }
            }
            if (ships.Count == 0) return;

            float rSq = settings.BlockadeRangeTiles * settings.BlockadeRangeTiles;
            foreach (City city in world.cities)
            {
                if (city == null || !city.isAlive()) continue;
                WorldTile cTile = city.getTile();
                Kingdom cKing = CityKingdomField?.GetValue(city) as Kingdom;
                if (cTile == null || cKing == null || !cKing.isCiv()) continue;

                bool blockaded = false;
                for (int i = 0; i < ships.Count; i++)
                {
                    Actor ship = ships[i];
                    if (!cKing.isEnemy(ship.kingdom)) continue;
                    float dx = ship.current_tile.x - cTile.x;
                    float dy = ship.current_tile.y - cTile.y;
                    if (dx * dx + dy * dy <= rSq)
                    {
                        blockaded = true;
                        break;
                    }
                }

                if (!blockaded) continue;
                Main.ActiveBlockades.Add(city.name ?? "Port");

                // Drain garrison nutrition inside the blockaded port city
                int starved = 0;
                for (int i = 0; i < alive.Count && starved < 18; i++)
                {
                    Actor u = alive[i];
                    if (u != null && u.isAlive() && u.kingdom == cKing && u.current_tile != null)
                    {
                        float dx = u.current_tile.x - cTile.x;
                        float dy = u.current_tile.y - cTile.y;
                        if (dx * dx + dy * dy <= rSq)
                        {
                            u.decreaseNutrition(12);
                            starved++;
                        }
                    }
                }
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
