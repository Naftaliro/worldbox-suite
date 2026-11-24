using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ModernAge
{
    public class KingdomModernState
    {
        public long KingdomId;
        public int TechTier; // 0 = Medieval, 1 = Blackpowder, 2 = Industrial/Cold War, 3 = Modern
        public GovernmentType Government;
        public TradeSpecialty Specialty;
        public int LendLeaseDeliveries;
    }

    [HarmonyPatch(typeof(Actor))]
    public static class ModernCombatPatches
    {
        public static int BulletsDeflected { get; private set; }
        public static int ArmorKillsCountered { get; private set; }
        public static int AirIntercepts { get; private set; }

        private static readonly MethodInfo AddStatusMethod =
            AccessTools.Method(typeof(BaseSimObject), "addStatusEffect", new[] { typeof(string), typeof(float), typeof(bool) });

        private static readonly Dictionary<long, float> AegisCooldowns = new Dictionary<long, float>();

        public static EquipmentAsset GetWeapon(Actor a)
        {
            if (a?.equipment == null) return null;
            ActorEquipmentSlot slot = a.equipment.getSlot(EquipmentType.Weapon);
            if (slot == null || slot.isEmpty()) return null;
            return slot.getItem()?.getAsset();
        }

        private static bool HasSlotId(Actor a, EquipmentType slotType, string id)
        {
            if (a?.equipment == null) return false;
            ActorEquipmentSlot slot = a.equipment.getSlot(slotType);
            if (slot == null || slot.isEmpty()) return false;
            return slot.getItem()?.getAsset()?.id == id;
        }

        [HarmonyPrefix]
        [HarmonyPatch("getHit")]
        private static void ActorGetHitPrefix(Actor __instance, ref float pDamage, BaseSimObject pAttacker)
        {
            if (__instance == null || pDamage <= 0f || Main.Settings == null) return;
            Actor attacker = pAttacker as Actor;
            if (attacker == null) return;

            EquipmentAsset atkW = GetWeapon(attacker);
            EquipmentAsset defW = GetWeapon(__instance);
            string atkWeaponId = atkW?.id ?? "";
            string defAssetId = __instance.asset?.id ?? "";

            bool atkIsGun = attacker.isWeaponFirearm() || ModernIds.FirearmIds.Contains(atkWeaponId);
            bool defIsTank = ModernIds.ArmorVehicleIds.Contains(defAssetId);
            bool defIsAir = ModernIds.AircraftIds.Contains(defAssetId);

            // Tier 1 counter-play: Bastion Plate & Aegis Amulet vs firearms; Shock Halberd vs gunners
            if (atkIsGun)
            {
                if (HasSlotId(__instance, EquipmentType.Armor, ModernIds.BastionPlate))
                {
                    pDamage *= (1f - Mathf.Clamp(Main.Settings.BastionBulletReduction, 0f, 0.85f));
                    BulletsDeflected++;
                }
                if (HasSlotId(__instance, EquipmentType.Amulet, ModernIds.AegisAmulet))
                {
                    float now = Time.unscaledTime;
                    if (!AegisCooldowns.TryGetValue(__instance.id, out float readyAt) || now >= readyAt)
                    {
                        AegisCooldowns[__instance.id] = now + 12f;
                        AddStatusMethod?.Invoke(__instance, new object[] { "shield", 4.0f, true });
                        pDamage *= 0.5f;
                    }
                }
            }
            else if (atkWeaponId == ModernIds.ShockHalberd && (defW != null && ModernIds.FirearmIds.Contains(defW.id)))
            {
                pDamage *= (1f + Main.Settings.HalberdVsGunBonus);
            }

            // Tier 2 & 3 Rock-Paper-Scissors:
            // Small arms bounce off Tanks; Anti-Tank weapons shred Tanks; Flak/SAM shreds Aircraft!
            if (defIsTank)
            {
                if (atkWeaponId == ModernIds.AtRifle || atkWeaponId == ModernIds.GuidedAt)
                {
                    pDamage *= (1f + Main.Settings.AntiTankBonus);
                    ArmorKillsCountered++;
                }
                else if (atkIsGun || atkWeaponId == "arrow" || atkWeaponId == ModernIds.Warbow)
                {
                    pDamage *= 0.35f; // Heavy tank armor shrugs off small arms
                }
            }

            if (defIsAir)
            {
                if (atkWeaponId == ModernIds.FlakGun || atkWeaponId == ModernIds.SamLauncher)
                {
                    pDamage *= (1f + Main.Settings.AntiAirBonus);
                    AirIntercepts++;
                }
            }
        }
    }

    public static class GeopoliticsAndTradeSystem
    {
        public static readonly Dictionary<long, KingdomModernState> States = new Dictionary<long, KingdomModernState>();
        public static int VehiclesDeployed { get; private set; }
        public static int RadarSamShots { get; private set; }
        public static int HighwaysBuilt { get; private set; }

        private static readonly MethodInfo SetKingdomMethod =
            AccessTools.Method(typeof(Actor), "setKingdom", new[] { typeof(Kingdom) });

        private static Texture2D _radarTex;
        private static GUIStyle _radarLabelStyle;

        public static KingdomModernState GetOrCreateState(Kingdom k)
        {
            if (k == null) return null;
            if (!States.TryGetValue(k.id, out var st))
            {
                int govCount = Enum.GetValues(typeof(GovernmentType)).Length;
                int specCount = Enum.GetValues(typeof(TradeSpecialty)).Length;
                st = new KingdomModernState
                {
                    KingdomId = k.id,
                    TechTier = 0,
                    Government = (GovernmentType)((int)(k.id % govCount)),
                    Specialty = (TradeSpecialty)(1 + (int)((k.id * 3) % (specCount - 1)))
                };
                States[k.id] = st;
            }
            return st;
        }

        public static void TickAll()
        {
            var world = MapBox.instance;
            var cfg = Main.Settings;
            if (world?.kingdoms == null || cfg == null) return;

            var civs = new List<Kingdom>();
            foreach (Kingdom k in world.kingdoms)
            {
                if (k != null && k.isCiv() && k.isAlive()) civs.Add(k);
            }

            foreach (Kingdom k in civs)
            {
                var st = GetOrCreateState(k);

                // 1. Evaluate Tier Progression (Technocracy unlocks 25% earlier)
                if (cfg.EnableAutoProgression)
                {
                    float techMult = st.Government == GovernmentType.Technocracy ? 0.75f : 1.0f;
                    int pop = k.getPopulationPeople();
                    int cities = k.cities != null ? k.cities.Count : 0;

                    int targetTier = st.TechTier;
                    if (pop >= cfg.Tier3Pop * techMult && cities >= 5) targetTier = Math.Max(targetTier, 3);
                    else if (pop >= cfg.Tier2Pop * techMult && cities >= 3) targetTier = Math.Max(targetTier, 2);
                    else if (pop >= cfg.Tier1Pop * techMult && cities >= 2) targetTier = Math.Max(targetTier, 1);

                    if (targetTier > st.TechTier)
                    {
                        st.TechTier = targetTier;
                        Debug.Log($"[ModernAge] {k.name} ({st.Government}) reached Modern Tier {st.TechTier}!");
                    }
                }

                // 2. Equip infantry & deploy tier-appropriate vehicles (capped per kingdom so battles stay balanced)
                if (st.TechTier >= 1)
                {
                    EquipTierInfantry(world, k, st.TechTier, 5);
                }
                if (st.TechTier >= 2)
                {
                    EnsureKingdomVehicles(world, k, st);
                    if (cfg.EnableHighways) BuildInterCityHighway(world, k);
                }

                // 3. Government & International Trade / Lend-Lease effects
                if (cfg.EnableTradeAndLendLease)
                {
                    ApplyGovernmentAndTrade(world, k, st, civs);
                }
            }

            // 4. Tier 3 Capital Radar & SAM Umbrella Defense
            if (cfg.EnableRadarUmbrella)
            {
                TickRadarSamDefense(world, civs, cfg);
            }
        }

        private static void ApplyGovernmentAndTrade(MapBox world, Kingdom k, KingdomModernState st, List<Kingdom> allCivs)
        {
            // Collectivist: empire-wide resource pooling keeps frontline soldiers fed & healthy
            if (st.Government == GovernmentType.Collectivist && world.units?.units_only_alive != null)
            {
                var alive = world.units.units_only_alive;
                int healed = 0;
                for (int i = 0; i < alive.Count && healed < 15; i++)
                {
                    Actor u = alive[i];
                    if (u != null && u.isAlive() && u.kingdom == k)
                    {
                        u.restoreHealthPercent(0.15f);
                        healed++;
                    }
                }
            }

            // International Cooperation & Lend-Lease with peaceful partners
            foreach (Kingdom partner in allCivs)
            {
                if (partner == k || k.isEnemy(partner)) continue;
                var pState = GetOrCreateState(partner);

                // Breadbasket exports food/vitality to peaceful partners
                if (st.Specialty == TradeSpecialty.Breadbasket && partner.king != null && partner.king.isAlive())
                {
                    partner.king.restoreHealthPercent(0.25f);
                }

                // Armory State sends Lend-Lease gear/vehicles to allied nations currently fighting a war!
                if (st.Specialty == TradeSpecialty.ArmoryState && partner.hasEnemies() && st.TechTier >= 1)
                {
                    EquipTierInfantry(world, partner, st.TechTier, 3);
                    st.LendLeaseDeliveries++;
                }

                // Research Hub helps peaceful allies advance faster
                if (st.Specialty == TradeSpecialty.ResearchHub && st.TechTier > pState.TechTier)
                {
                    pState.TechTier = st.TechTier;
                }
            }
        }

        public static int EquipTierInfantry(MapBox world, Kingdom k, int tier, int maxSoldiers)
        {
            if (world?.units?.units_only_alive == null || world.items == null) return 0;
            var alive = world.units.units_only_alive;
            int issued = 0;

            for (int i = 0; i < alive.Count && issued < maxSoldiers; i++)
            {
                Actor u = alive[i];
                if (u == null || !u.isAlive() || u.kingdom != k || u.army == null || u.equipment == null) continue;
                if (u.asset == null || u.asset.is_boat) continue;
                if (ModernIds.ArmorVehicleIds.Contains(u.asset.id) || ModernIds.AircraftIds.Contains(u.asset.id)) continue;

                EquipmentAsset cur = ModernCombatPatches.GetWeapon(u);
                if (cur != null && cur.id.StartsWith("modern_")) continue;

                int roll = (int)((u.id + issued) % 10);
                if (tier == 1)
                {
                    if (roll < 3) Give(world, u, ModernIds.Musket);
                    else if (roll < 5) Give(world, u, ModernIds.Flintlock);
                    else if (roll == 5) Give(world, u, ModernIds.Blunderbuss);
                    else if (roll == 6) Give(world, u, ModernIds.Culverin);
                    else if (roll < 9) { Give(world, u, ModernIds.ShockHalberd); Give(world, u, ModernIds.BastionPlate); }
                    else { Give(world, u, ModernIds.Warbow); Give(world, u, ModernIds.AegisAmulet); }
                }
                else if (tier == 2)
                {
                    if (roll < 3) Give(world, u, ModernIds.Musket);
                    else if (roll < 6) { Give(world, u, ModernIds.AtRifle); Give(world, u, ModernIds.BastionPlate); }
                    else if (roll < 9) Give(world, u, ModernIds.FlakGun);
                    else { Give(world, u, ModernIds.ShockHalberd); Give(world, u, ModernIds.AegisAmulet); }
                }
                else
                {
                    if (roll < 4) { Give(world, u, ModernIds.GuidedAt); Give(world, u, ModernIds.BastionPlate); }
                    else if (roll < 7) Give(world, u, ModernIds.SamLauncher);
                    else { Give(world, u, ModernIds.Culverin); Give(world, u, ModernIds.AegisAmulet); }
                }
                issued++;
            }
            return issued;
        }

        private static void Give(MapBox world, Actor u, string assetId)
        {
            if (AssetManager.items == null || !AssetManager.items.has(assetId)) return;
            EquipmentAsset eq = AssetManager.items.get(assetId);
            if (eq == null) return;
            Item item = world.items.newItem(eq);
            if (item == null) return;
            u.equipment.setItem(item, u);
            u.setStatsDirty();
        }

        private static void EnsureKingdomVehicles(MapBox world, Kingdom k, KingdomModernState st)
        {
            if (world?.units?.units_only_alive == null || k.capital?.getTile() == null) return;

            int capLimit = st.Government == GovernmentType.MilitaryJunta ? 6 : 4;
            int currentVehicles = 0;
            var alive = world.units.units_only_alive;
            for (int i = 0; i < alive.Count; i++)
            {
                Actor u = alive[i];
                if (u != null && u.isAlive() && u.kingdom == k && u.asset != null)
                {
                    if (ModernIds.ArmorVehicleIds.Contains(u.asset.id) || ModernIds.AircraftIds.Contains(u.asset.id))
                        currentVehicles++;
                }
            }

            if (currentVehicles >= capLimit) return;

            string spawnId;
            if (st.TechTier >= 3)
            {
                spawnId = (currentVehicles % 3 == 0) ? ModernIds.MainBattleTank
                    : (currentVehicles % 3 == 1 ? ModernIds.AttackHeli : ModernIds.SupersonicJet);
            }
            else
            {
                spawnId = (currentVehicles % 3 == 0) ? ModernIds.LightTank
                    : (currentVehicles % 3 == 1 ? ModernIds.PropFighter : ModernIds.EarlyHeli);
            }

            SpawnVehicleForKingdom(world, k, spawnId, k.capital.getTile());
        }

        public static Actor SpawnVehicleForKingdom(MapBox world, Kingdom k, string unitAssetId, WorldTile tile)
        {
            if (world?.units == null || tile == null || AssetManager.actor_library == null || !AssetManager.actor_library.has(unitAssetId))
                return null;

            Actor unit = world.units.spawnNewUnit(unitAssetId, tile);
            if (unit != null && k != null && SetKingdomMethod != null)
            {
                SetKingdomMethod.Invoke(unit, new object[] { k });
                VehiclesDeployed++;
            }
            return unit;
        }

        private static void BuildInterCityHighway(MapBox world, Kingdom k)
        {
            if (k.cities == null || k.cities.Count < 2) return;
            City a = k.cities[0];
            City b = k.cities[k.cities.Count - 1];
            WorldTile ta = a?.getTile();
            WorldTile tb = b?.getTile();
            if (ta == null || tb == null) return;

            int steps = Mathf.CeilToInt(Vector2.Distance(new Vector2(ta.x, ta.y), new Vector2(tb.x, tb.y)));
            if (steps < 8 || steps > 180) return;

            int laid = 0;
            for (int s = 1; s < steps; s++)
            {
                float t = (float)s / steps;
                int x = Mathf.RoundToInt(Mathf.Lerp(ta.x, tb.x, t));
                int y = Mathf.RoundToInt(Mathf.Lerp(ta.y, tb.y, t));
                WorldTile tile = world.GetTile(x, y);
                if (tile != null && tile.Type != null && !tile.Type.liquid && tile.Type.ground && tile.top_type != TopTileLibrary.road)
                {
                    tile.setTopTileType(TopTileLibrary.road);
                    laid++;
                }
            }
            if (laid > 0)
            {
                HighwaysBuilt += laid;
                world.redrawTiles();
            }
        }

        private static void TickRadarSamDefense(MapBox world, List<Kingdom> civs, ModernAgeConfig cfg)
        {
            if (world?.units?.units_only_alive == null) return;
            float rSq = cfg.RadarRangeTiles * cfg.RadarRangeTiles;
            var alive = world.units.units_only_alive;

            foreach (Kingdom k in civs)
            {
                var st = GetOrCreateState(k);
                if (st.TechTier < 3) continue;
                WorldTile cap = k.capital?.getTile();
                if (cap == null) continue;

                for (int i = 0; i < alive.Count; i++)
                {
                    Actor u = alive[i];
                    if (u == null || !u.isAlive() || u.current_tile == null || u.asset == null) continue;
                    if (!ModernIds.AircraftIds.Contains(u.asset.id)) continue;
                    if (u.kingdom == null || !k.isEnemy(u.kingdom)) continue;

                    float dx = u.current_tile.x - cap.x;
                    float dy = u.current_tile.y - cap.y;
                    if (dx * dx + dy * dy <= rSq)
                    {
                        u.changeHealth(-65);
                        u.startShake(0.3f, 0.1f, true, true);
                        RadarSamShots++;
                    }
                }
            }
        }

        public static void DrawRadarUmbrellas()
        {
            if (Main.Settings == null || !Main.Settings.ShowRadarRings || Event.current.type != EventType.Repaint) return;
            var world = MapBox.instance;
            Camera cam = Camera.main;
            if (world?.kingdoms == null || cam == null) return;

            if (_radarTex == null)
            {
                const int sz = 64;
                _radarTex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
                float half = (sz - 1) * 0.5f;
                for (int y = 0; y < sz; y++)
                {
                    for (int x = 0; x < sz; x++)
                    {
                        float dist = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                        float alpha = (dist >= 0.88f && dist <= 0.98f) ? 0.65f : (dist < 0.88f ? 0.05f : 0f);
                        // Catppuccin Sapphire (#74c7ec)
                        _radarTex.SetPixel(x, y, new Color(0.455f, 0.780f, 0.925f, alpha));
                    }
                }
                _radarTex.Apply();
                _radarLabelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 10, fontStyle = FontStyle.Bold };
                _radarLabelStyle.normal.textColor = new Color(0.455f, 0.780f, 0.925f);
            }

            foreach (Kingdom k in world.kingdoms)
            {
                if (k == null || !k.isCiv() || !k.isAlive()) continue;
                var st = GetOrCreateState(k);
                if (st.TechTier < 3) continue;
                WorldTile cap = k.capital?.getTile();
                if (cap == null) continue;

                Vector3 cScr = cam.WorldToScreenPoint(new Vector3(cap.x, cap.y, 0f));
                if (cScr.z < 0f) continue;
                Vector3 eScr = cam.WorldToScreenPoint(new Vector3(cap.x + Main.Settings.RadarRangeTiles, cap.y, 0f));
                float rPx = Mathf.Clamp(Mathf.Abs(eScr.x - cScr.x), 24f, 450f);
                float sx = cScr.x;
                float sy = Screen.height - cScr.y;

                GUI.DrawTexture(new Rect(sx - rPx, sy - rPx, rPx * 2f, rPx * 2f), _radarTex);
                GUI.Label(new Rect(sx - 80f, sy - rPx - 14f, 160f, 16f), $"RADAR SAM ({k.name})", _radarLabelStyle);
            }
        }
    }
}
