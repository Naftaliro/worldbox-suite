using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace GrandWarfare
{
    public static class DivineInterventionSystem
    {
        private const int MaxArmoryTroops = 12;
        private const int MaxAnointedPerCast = 2;
        private const int MaxNearbyHealed = 10;
        private const int MercenarySquadSize = 8;

        private static readonly string[] HeroEpithets =
        {
            "the Valiant",
            "the Vanguard",
            "the Stalwart",
            "the Bold",
            "the Unbroken",
            "the Ironclad"
        };

        private static readonly string[] ModernTier12Weapons =
        {
            "modern_musket",
            "modern_flintlock",
            "modern_shock_halberd",
            "modern_warbow",
            "modern_at_rifle",
            "modern_flak_gun"
        };

        private static readonly HashSet<string> ModernVehicleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "modern_light_tank", "modern_mbt",
            "modern_prop_fighter", "modern_early_heli", "modern_attack_heli", "modern_jet"
        };

        private static readonly HashSet<long> AnointedHeroIds = new HashSet<long>();

        private static readonly FieldInfo CityKingdomField =
            AccessTools.Field(typeof(City), "kingdom");

        private static readonly MethodInfo LevelUpMethod =
            AccessTools.Method(typeof(Actor), "levelUp", Type.EmptyTypes);

        private static List<EquipmentAsset> _cachedSteelMithrilWeapons;
        private static List<EquipmentAsset> _cachedSteelMithrilArmors;
        private static List<EquipmentAsset> _cachedSteelMithrilHelmets;

        public static string ArmoryBlessingAtTile(WorldTile tile)
        {
            var world = MapBox.instance;
            if (world == null || world.cities == null || tile == null)
            {
                return "Click inside or near a kingdom city to bestow an Armory Blessing.";
            }

            try
            {
                City city = FindCityNearTile(world, tile, 35f);
                if (city == null || !city.isAlive())
                {
                    return "No kingdom city within 35 tiles for Armory Blessing.";
                }

                Kingdom kingdom = GetCityKingdom(city);
                if (kingdom == null || !kingdom.isAlive() || !kingdom.isCiv())
                {
                    return "No active civilization found at that location.";
                }

                // 1. Refill moderate war supplies in the city's stockpiles
                city.addResourcesToRandomStockpile("gold", 35);
                city.addResourcesToRandomStockpile("common_metals", 25);
                city.addResourcesToRandomStockpile("wood", 25);
                city.addResourcesToRandomStockpile("stone", 25);
                city.addResourcesToRandomStockpile("bread", 30);

                // 2. Collect up to 12 alive troops/adults in this city (soldiers first)
                var warriors = new List<Actor>();
                var adults = new List<Actor>();
                if (world.units?.units_only_alive != null)
                {
                    var alive = new List<Actor>(world.units.units_only_alive);
                    for (int i = 0; i < alive.Count; i++)
                    {
                        Actor u = alive[i];
                        if (u == null || !u.isAlive() || u.city != city || u.kingdom != kingdom) continue;
                        if (u.asset == null || u.asset.is_boat || IsModernVehicle(u)) continue;
                        if (!u.isAdult()) continue;

                        if (u.army != null || u.isWarrior())
                            warriors.Add(u);
                        else
                            adults.Add(u);
                    }
                }

                var targets = new List<Actor>(MaxArmoryTroops);
                for (int i = 0; i < warriors.Count && targets.Count < MaxArmoryTroops; i++)
                    targets.Add(warriors[i]);
                for (int i = 0; i < adults.Count && targets.Count < MaxArmoryTroops; i++)
                    targets.Add(adults[i]);

                bool modernActive = AssetManager.items != null && AssetManager.items.has("modern_musket");
                if (!modernActive) EnsureVanillaForgedPools();

                int armed = 0;
                for (int i = 0; i < targets.Count; i++)
                {
                    Actor u = targets[i];
                    if (u == null || !u.isAlive()) continue;

                    if (modernActive)
                    {
                        EquipModernArmoryGear(world, u, i);
                    }
                    else
                    {
                        EquipVanillaForgedGear(world, u, i);
                    }

                    AddTraitSafe(u, "veteran");
                    AddTraitSafe(u, "tough");
                    u.setMaxHealth();
                    u.setMaxStamina();
                    u.setStatsDirty();
                    armed++;
                }

                return $"Armory Blessing: Resupplied {city.name} ({kingdom.name}) & armed {armed} troops!";
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GrandWarfare] ArmoryBlessingAtTile failed: {e.Message}");
                return $"Armory Blessing failed: {e.Message}";
            }
        }

        public static string AnointWarHeroAtTile(WorldTile tile)
        {
            var world = MapBox.instance;
            if (world == null || world.units == null || tile == null)
            {
                return "Click inside or near a kingdom to anoint War Heroes.";
            }

            try
            {
                PruneDeadHeroes(world);

                Kingdom kingdom = FindKingdomNearTile(world, tile, 35f);
                if (kingdom == null || !kingdom.isAlive() || !kingdom.isCiv())
                {
                    return "No kingdom found within 35 tiles to anoint War Heroes.";
                }

                var alive = new List<Actor>(world.units.units_only_alive);
                var warriorCands = new List<(Actor actor, float distSq)>();
                var citizenCands = new List<(Actor actor, float distSq)>();

                for (int i = 0; i < alive.Count; i++)
                {
                    Actor u = alive[i];
                    if (u == null || !u.isAlive() || u.kingdom != kingdom || u.current_tile == null) continue;
                    if (u.asset == null || u.asset.is_boat || IsModernVehicle(u)) continue;
                    if (!u.isAdult()) continue;
                    if (AnointedHeroIds.Contains(u.id)) continue;

                    float dx = u.current_tile.x - tile.x;
                    float dy = u.current_tile.y - tile.y;
                    float dSq = dx * dx + dy * dy;

                    if (u.army != null || u.isWarrior())
                        warriorCands.Add((u, dSq));
                    else
                        citizenCands.Add((u, dSq));
                }

                warriorCands.Sort((a, b) => a.distSq.CompareTo(b.distSq));
                citizenCands.Sort((a, b) => a.distSq.CompareTo(b.distSq));

                var chosen = new List<Actor>(MaxAnointedPerCast);
                for (int i = 0; i < warriorCands.Count && chosen.Count < MaxAnointedPerCast; i++)
                    chosen.Add(warriorCands[i].actor);
                for (int i = 0; i < citizenCands.Count && chosen.Count < MaxAnointedPerCast; i++)
                    chosen.Add(citizenCands[i].actor);

                if (chosen.Count == 0)
                {
                    return $"No eligible un-anointed adult warriors or citizens found in {kingdom.name}.";
                }

                var heroNames = new List<string>(chosen.Count);
                for (int i = 0; i < chosen.Count; i++)
                {
                    Actor hero = chosen[i];
                    AnointedHeroIds.Add(hero.id);

                    // Balanced champion traits (explicitly no immortal / invincibility)
                    AddTraitSafe(hero, "veteran");
                    AddTraitSafe(hero, "strong");
                    AddTraitSafe(hero, "agile");
                    AddTraitSafe(hero, "tough");

                    if (LevelUpMethod != null)
                    {
                        for (int lvl = 0; lvl < 2 && hero.level < 12; lvl++)
                        {
                            try { LevelUpMethod.Invoke(hero, null); } catch { break; }
                        }
                    }

                    string displayName = ApplyHeroEpithet(hero);
                    hero.setMaxHealth();
                    hero.setMaxStamina();
                    hero.setStatsDirty();
                    heroNames.Add(displayName);
                }

                // Rally & heal up to 10 nearby friendly soldiers around the anointed hero(es)
                WorldTile anchor = chosen[0].current_tile ?? tile;
                const float healRangeSq = 22f * 22f;
                int healed = 0;
                for (int i = 0; i < alive.Count && healed < MaxNearbyHealed; i++)
                {
                    Actor ally = alive[i];
                    if (ally == null || !ally.isAlive() || ally.kingdom != kingdom || ally.current_tile == null) continue;
                    if (ally.asset == null || ally.asset.is_boat) continue;
                    if (chosen.Contains(ally)) continue;
                    if (ally.army == null && !ally.isWarrior()) continue;

                    float dx = ally.current_tile.x - anchor.x;
                    float dy = ally.current_tile.y - anchor.y;
                    if (dx * dx + dy * dy <= healRangeSq)
                    {
                        ally.setMaxHealth();
                        ally.setMaxStamina();
                        healed++;
                    }
                }

                string joinedNames = string.Join(" & ", heroNames);
                return $"Anointed War Hero(es) {joinedNames} for {kingdom.name}!";
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GrandWarfare] AnointWarHeroAtTile failed: {e.Message}");
                return $"Anoint War Hero failed: {e.Message}";
            }
        }

        public static string DeployMercenariesAtTile(WorldTile tile)
        {
            var world = MapBox.instance;
            if (world == null || world.units == null || tile == null)
            {
                return "Click on solid land to deploy mercenaries.";
            }

            if (tile.Type == null || tile.Type.liquid)
            {
                return "Mercenaries require solid ground. Click on land near a kingdom.";
            }

            try
            {
                City city = FindCityNearTile(world, tile, 45f);
                if (city == null || !city.isAlive())
                {
                    return "Click inside or near a kingdom to deploy mercenaries for them.";
                }

                Kingdom kingdom = GetCityKingdom(city);
                if (kingdom == null || !kingdom.isAlive() || !kingdom.isCiv())
                {
                    return "Click inside or near a kingdom to deploy mercenaries for them.";
                }

                string speciesId = ResolveMercenarySpeciesAsset(kingdom, city);
                if (AssetManager.actor_library == null || !AssetManager.actor_library.has(speciesId))
                {
                    speciesId = "human";
                }
                if (AssetManager.actor_library == null || !AssetManager.actor_library.has(speciesId))
                {
                    return "Unable to resolve a valid mercenary unit asset.";
                }

                EnsureVanillaForgedPools();
                var spawnTiles = CollectSolidTilesAround(world, tile, 3);
                int spawned = 0;

                for (int i = 0; i < MercenarySquadSize; i++)
                {
                    WorldTile spawnTile = spawnTiles.Count > 0 ? spawnTiles[i % spawnTiles.Count] : tile;
                    Actor merc = world.units.spawnNewUnit(speciesId, spawnTile, false, false, 0f, null, false, true);
                    if (merc == null || !merc.isAlive()) continue;

                    AddTraitSafe(merc, "veteran");
                    AddTraitSafe(merc, "agile");

                    try
                    {
                        merc.joinCity(city);
                        merc.setMetasFromCity(city);
                        city.makeWarrior(merc);
                    }
                    catch { }

                    // Give mercenaries a reliable steel weapon if they spawned empty-handed
                    if (merc.equipment != null)
                    {
                        ActorEquipmentSlot wSlot = merc.equipment.getSlot(EquipmentType.Weapon);
                        if (wSlot == null || wSlot.isEmpty())
                        {
                            EquipVanillaForgedWeaponOnly(world, merc, i);
                        }
                    }

                    merc.setMaxHealth();
                    merc.setMaxStamina();
                    merc.setStatsDirty();
                    spawned++;
                }

                if (spawned == 0)
                {
                    return $"Could not spawn mercenaries near {city.name}.";
                }

                return $"Deployed {spawned} mercenaries for {kingdom.name} at {city.name}!";
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GrandWarfare] DeployMercenariesAtTile failed: {e.Message}");
                return $"Mercenary deployment failed: {e.Message}";
            }
        }

        private static string ResolveMercenarySpeciesAsset(Kingdom kingdom, City city)
        {
            string raw = kingdom?.data?.original_actor_asset;
            if (string.IsNullOrEmpty(raw))
            {
                try { raw = city?.getActorAsset()?.id; } catch { }
            }
            if (string.IsNullOrEmpty(raw))
            {
                try { raw = kingdom?.getActorAsset()?.id; } catch { }
            }
            if (string.IsNullOrEmpty(raw)) return "human";

            string norm = raw.Trim().ToLowerInvariant();
            if (norm == "warden" || norm == "civ_warden" || norm.Contains("warden"))
            {
                // Wardens never serve as mercenaries for any kingdom
                return "human";
            }

            if (AssetManager.actor_library != null && AssetManager.actor_library.has(raw))
            {
                ActorAsset asset = AssetManager.actor_library.get(raw);
                if (asset != null && !asset.is_boat && !IsModernVehicleId(asset.id))
                {
                    return raw;
                }
            }

            return "human";
        }

        private static void EquipModernArmoryGear(MapBox world, Actor u, int index)
        {
            if (world?.items == null || u?.equipment == null || AssetManager.items == null) return;

            string weaponId = ModernTier12Weapons[index % ModernTier12Weapons.Length];
            TryGiveItemById(world, u, weaponId);

            if (index % 2 == 0)
                TryGiveItemById(world, u, "modern_bastion_plate");
            else
                TryGiveItemById(world, u, "modern_aegis_amulet");
        }

        private static void EquipVanillaForgedGear(MapBox world, Actor u, int index)
        {
            if (world?.items == null || u?.equipment == null) return;

            EquipVanillaForgedWeaponOnly(world, u, index);

            if (_cachedSteelMithrilArmors != null && _cachedSteelMithrilArmors.Count > 0)
            {
                TryGiveEquipmentAsset(world, u, _cachedSteelMithrilArmors[index % _cachedSteelMithrilArmors.Count]);
            }
            if (_cachedSteelMithrilHelmets != null && _cachedSteelMithrilHelmets.Count > 0 && (index % 2 == 0))
            {
                TryGiveEquipmentAsset(world, u, _cachedSteelMithrilHelmets[index % _cachedSteelMithrilHelmets.Count]);
            }
        }

        private static void EquipVanillaForgedWeaponOnly(MapBox world, Actor u, int index)
        {
            if (world?.items == null || u?.equipment == null) return;
            if (_cachedSteelMithrilWeapons != null && _cachedSteelMithrilWeapons.Count > 0)
            {
                TryGiveEquipmentAsset(world, u, _cachedSteelMithrilWeapons[index % _cachedSteelMithrilWeapons.Count]);
            }
        }

        private static void EnsureVanillaForgedPools()
        {
            if (_cachedSteelMithrilWeapons != null && _cachedSteelMithrilWeapons.Count > 0) return;
            if (AssetManager.items?.list == null) return;

            var weapons = new List<EquipmentAsset>();
            var armors = new List<EquipmentAsset>();
            var helmets = new List<EquipmentAsset>();

            var items = AssetManager.items.list;
            for (int i = 0; i < items.Count; i++)
            {
                EquipmentAsset eq = items[i];
                if (eq == null || string.IsNullOrEmpty(eq.id)) continue;
                if (eq.id.StartsWith("modern_")) continue;

                string mat = (eq.material ?? "").ToLowerInvariant();
                bool goodMaterial = mat == "steel" || mat == "mithril" ||
                                    eq.id.Contains("steel") || eq.id.Contains("mithril");
                if (!goodMaterial) continue;

                if (eq.equipment_type == EquipmentType.Weapon)
                {
                    weapons.Add(eq);
                }
                else if (eq.equipment_type == EquipmentType.Armor)
                {
                    armors.Add(eq);
                }
                else if (eq.equipment_type == EquipmentType.Helmet)
                {
                    helmets.Add(eq);
                }
            }

            // Graceful fallback to iron/silver if steel/mithril pool is empty
            if (weapons.Count == 0)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    EquipmentAsset eq = items[i];
                    if (eq == null || string.IsNullOrEmpty(eq.id) || eq.id.StartsWith("modern_")) continue;
                    string mat = (eq.material ?? "").ToLowerInvariant();
                    if (eq.equipment_type == EquipmentType.Weapon && (mat == "iron" || mat == "silver" || eq.id == "sword" || eq.id == "bow"))
                    {
                        weapons.Add(eq);
                    }
                }
            }

            _cachedSteelMithrilWeapons = weapons;
            _cachedSteelMithrilArmors = armors;
            _cachedSteelMithrilHelmets = helmets;
        }

        private static void TryGiveItemById(MapBox world, Actor u, string assetId)
        {
            if (string.IsNullOrEmpty(assetId) || AssetManager.items == null || !AssetManager.items.has(assetId)) return;
            TryGiveEquipmentAsset(world, u, AssetManager.items.get(assetId));
        }

        private static void TryGiveEquipmentAsset(MapBox world, Actor u, EquipmentAsset eq)
        {
            if (world?.items == null || u?.equipment == null || eq == null) return;
            try
            {
                Item item = world.items.newItem(eq);
                if (item != null)
                {
                    u.equipment.setItem(item, u);
                }
            }
            catch { }
        }

        private static string ApplyHeroEpithet(Actor hero)
        {
            string baseName = "Champion";
            try
            {
                string n = hero.getName();
                if (!string.IsNullOrEmpty(n)) baseName = n;
            }
            catch { }

            if (!baseName.Contains(" the "))
            {
                string epithet = HeroEpithets[Math.Abs((int)(hero.id % HeroEpithets.Length))];
                string titled = $"{baseName} {epithet}";
                try
                {
                    hero.setName(titled, true);
                    return titled;
                }
                catch { }
            }
            return baseName;
        }

        private static City FindCityNearTile(MapBox world, WorldTile tile, float maxDistTiles)
        {
            if (tile?.zone?.city != null && tile.zone.city.isAlive())
            {
                Kingdom directK = GetCityKingdom(tile.zone.city);
                if (directK != null && directK.isAlive() && directK.isCiv())
                {
                    return tile.zone.city;
                }
            }

            if (world?.cities == null) return null;

            float bestDistSq = maxDistTiles * maxDistTiles;
            City bestCity = null;
            var citiesSnapshot = new List<City>(world.cities);
            for (int i = 0; i < citiesSnapshot.Count; i++)
            {
                City c = citiesSnapshot[i];
                if (c == null || !c.isAlive()) continue;
                Kingdom k = GetCityKingdom(c);
                if (k == null || !k.isAlive() || !k.isCiv()) continue;

                WorldTile cTile = c.getTile();
                if (cTile == null) continue;
                float dx = cTile.x - tile.x;
                float dy = cTile.y - tile.y;
                float dSq = dx * dx + dy * dy;
                if (dSq <= bestDistSq)
                {
                    bestDistSq = dSq;
                    bestCity = c;
                }
            }
            return bestCity;
        }

        private static Kingdom FindKingdomNearTile(MapBox world, WorldTile tile, float maxDistTiles)
        {
            City city = FindCityNearTile(world, tile, maxDistTiles);
            if (city != null)
            {
                Kingdom k = GetCityKingdom(city);
                if (k != null && k.isAlive() && k.isCiv()) return k;
            }

            if (world?.units?.units_only_alive == null) return null;

            float bestDistSq = maxDistTiles * maxDistTiles;
            Kingdom bestKingdom = null;
            var alive = new List<Actor>(world.units.units_only_alive);
            for (int i = 0; i < alive.Count; i++)
            {
                Actor u = alive[i];
                if (u == null || !u.isAlive() || u.current_tile == null) continue;
                if (u.kingdom == null || !u.kingdom.isAlive() || !u.kingdom.isCiv()) continue;

                float dx = u.current_tile.x - tile.x;
                float dy = u.current_tile.y - tile.y;
                float dSq = dx * dx + dy * dy;
                if (dSq <= bestDistSq)
                {
                    bestDistSq = dSq;
                    bestKingdom = u.kingdom;
                }
            }
            return bestKingdom;
        }

        private static List<WorldTile> CollectSolidTilesAround(MapBox world, WorldTile center, int radius)
        {
            var result = new List<WorldTile>();
            if (world == null || center == null) return result;

            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    WorldTile t = world.GetTile(center.x + dx, center.y + dy);
                    if (t != null && t.Type != null && !t.Type.liquid && t.Type.ground)
                    {
                        result.Add(t);
                    }
                }
            }

            if (result.Count == 0 && center.Type != null && !center.Type.liquid)
            {
                result.Add(center);
            }
            return result;
        }

        private static Kingdom GetCityKingdom(City c)
        {
            if (c == null || CityKingdomField == null) return null;
            try { return CityKingdomField.GetValue(c) as Kingdom; }
            catch { return null; }
        }

        private static void AddTraitSafe(Actor a, string traitId)
        {
            if (a == null || string.IsNullOrEmpty(traitId) || AssetManager.traits == null) return;
            try
            {
                if (AssetManager.traits.has(traitId) && !a.hasTrait(traitId))
                {
                    a.addTrait(traitId, false);
                }
            }
            catch { }
        }

        private static bool IsModernVehicle(Actor a)
        {
            return a?.asset != null && IsModernVehicleId(a.asset.id);
        }

        private static bool IsModernVehicleId(string id)
        {
            return !string.IsNullOrEmpty(id) && ModernVehicleIds.Contains(id);
        }

        private static void PruneDeadHeroes(MapBox world)
        {
            if (world?.units == null || AnointedHeroIds.Count == 0) return;
            if (AnointedHeroIds.Count < 32) return;

            var snapshot = new List<long>(AnointedHeroIds);
            for (int i = 0; i < snapshot.Count; i++)
            {
                Actor a = world.units.get(snapshot[i]);
                if (a == null || !a.isAlive())
                {
                    AnointedHeroIds.Remove(snapshot[i]);
                }
            }
        }
    }
}
