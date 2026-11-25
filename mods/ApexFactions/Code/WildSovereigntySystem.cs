using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ApexFactions
{
    public class FeralPackDomain
    {
        public string SpeciesId;
        public string DomainTitle;
        public Actor Alpha;
        public float CenterX;
        public float CenterY;
        public float Radius;
        public int PackCount;
    }

    public static class WildSovereigntySystem
    {
        public static readonly List<FeralPackDomain> ActiveDomains = new List<FeralPackDomain>();

        private static readonly HashSet<string> ExcludedSpecies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "butterfly", "beetle", "fly", "grasshopper", "ant_blue", "ant_green", "ant_red", "ant_black", "bee", "piranha"
        };

        private static bool IsEligibleWildAnimal(ActorAsset asset)
        {
            if (asset == null || asset.is_boat || asset.civ) return false;
            if (ExcludedSpecies.Contains(asset.id)) return false;
            return asset.default_animal;
        }

        private static string PluralizeSpecies(string id)
        {
            if (string.IsNullOrEmpty(id)) return "Beasts";
            string clean = id.Replace('_', ' ');
            switch (id.ToLowerInvariant())
            {
                case "wolf": return "Wolves";
                case "fox": return "Foxes";
                case "sheep": return "Sheep";
                case "deer": return "Deer";
                case "fish": return "Fish";
                case "mouse": return "Mice";
                case "monkey": return "Monkeys";
                case "butterfly": return "Butterflies";
            }
            string titled = char.ToUpper(clean[0]) + clean.Substring(1);
            if (titled.EndsWith("s") || titled.EndsWith("x") || titled.EndsWith("ch") || titled.EndsWith("sh"))
                return titled + "es";
            return titled + "s";
        }

        private static readonly MethodInfo SpeciesIdGetter =
            AccessTools.PropertyGetter(typeof(Subspecies), "species_id");

        /// <summary>
        /// Spawns a pack of sapient Wolfkin (civ_wolf) ready to found a full Wolf Kingdom immediately.
        /// </summary>
        private static readonly MethodInfo SetCityMethod =
            AccessTools.Method(typeof(Actor), "setCity", new[] { typeof(City) });

        public static int SpawnWolfkinKingdomAtCamera(int count = 12)
        {
            return SpawnWolfkinAtTile(WardenSystem.FindSafeLandNearCamera(), count, out _);
        }

        public static int SpawnWolfkinAtTile(WorldTile center, int count, out string foundedKingdomName)
        {
            foundedKingdomName = null;
            var world = MapBox.instance;
            if (world?.units == null || center == null) return 0;

            string speciesId = (AssetManager.actor_library != null && AssetManager.actor_library.has("civ_wolf"))
                ? "civ_wolf"
                : "wolf";

            var settlers = new List<Actor>(count);
            for (int i = 0; i < count; i++)
            {
                WorldTile spawnTile = center;
                if (i > 0)
                {
                    int ox = (i % 3) - 1;
                    int oy = ((i / 3) % 3) - 1;
                    WorldTile cand = world.GetTile(center.x + ox, center.y + oy);
                    if (cand != null && cand.Type != null && !cand.Type.liquid && cand.Type.ground)
                        spawnTile = cand;
                }

                Actor u = world.units.spawnNewUnit(speciesId, spawnTile);
                if (u != null) settlers.Add(u);
            }

            if (settlers.Count == 0) return 0;

            Actor leader = settlers[0];
            try
            {
                if (speciesId == "wolf" && leader.subspecies != null && !leader.subspecies.isSapient())
                {
                    leader.subspecies.makeSapient();
                }

                if (leader.buildCityAndStartCivilization() && leader.city != null && leader.kingdom != null && leader.kingdom.isCiv())
                {
                    City newCity = leader.city;
                    for (int i = 1; i < settlers.Count; i++)
                    {
                        if (SetCityMethod != null)
                        {
                            try { SetCityMethod.Invoke(settlers[i], new object[] { newCity }); } catch { }
                        }
                    }
                    foundedKingdomName = leader.kingdom.name;
                    Debug.Log($"[ApexFactions] Founded Wolfkin Kingdom '{foundedKingdomName}' at ({center.x},{center.y}) with {settlers.Count} settlers.");
                    return settlers.Count;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ApexFactions] Wolfkin immediate city founding deferred: {e.Message}");
            }

            Debug.Log($"[ApexFactions] Spawned {settlers.Count} sapient Wolfkin at ({center.x},{center.y}).");
            return settlers.Count;
        }

        public static int SpawnWolfkinAtTile(WorldTile center, int count = 12)
        {
            return SpawnWolfkinAtTile(center, count, out _);
        }

        /// <summary>
        /// Instantly grants sapience (Subspecies.makeSapient) to wild animal subspecies on the map
        /// so they can build kingdoms without waiting on Monolith RNG.
        /// </summary>
        public static int AwakenWildSubspecies(string filterSpecies = null)
        {
            var world = MapBox.instance;
            if (world?.subspecies == null) return 0;

            int awakened = 0;
            foreach (Subspecies sub in world.subspecies)
            {
                if (sub == null || !sub.isAlive() || sub.isSapient()) continue;
                string sid = SpeciesIdGetter?.Invoke(sub, null) as string ?? "";
                if (!string.IsNullOrEmpty(filterSpecies) && !string.Equals(sid, filterSpecies, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (string.IsNullOrEmpty(filterSpecies))
                {
                    ActorAsset asset = AssetManager.actor_library?.get(sid);
                    if (!IsEligibleWildAnimal(asset)) continue;
                }

                try
                {
                    sub.makeSapient();
                    awakened++;
                    Debug.Log($"[ApexFactions] Awakened wild subspecies '{sub.name}' ({sid}) into sapience!");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ApexFactions] Could not awaken {sub.name}: {e.Message}");
                }
            }
            return awakened;
        }

        /// <summary>
        /// Scans wild beast packs and crowns Alphas + Feral Pack Domains ("Land of the Wolves").
        /// </summary>
        public static void ScanFeralDomains()
        {
            ActiveDomains.Clear();
            var world = MapBox.instance;
            var settings = Main.Settings;
            if (world?.units?.units_only_alive == null || settings == null || !settings.EnableFeralDomains) return;

            var wildBySpecies = new Dictionary<string, List<Actor>>(StringComparer.OrdinalIgnoreCase);
            var alive = world.units.units_only_alive;

            for (int i = 0; i < alive.Count; i++)
            {
                Actor u = alive[i];
                if (u == null || !u.isAlive() || u.current_tile == null || u.asset == null) continue;
                if (u.kingdom != null && u.kingdom.isCiv()) continue;
                if (!IsEligibleWildAnimal(u.asset)) continue;

                if (!wildBySpecies.TryGetValue(u.asset.id, out var list))
                {
                    list = new List<Actor>(16);
                    wildBySpecies[u.asset.id] = list;
                }
                list.Add(u);
            }

            float radius = Mathf.Max(12f, settings.FeralDomainRadiusTiles);
            float radSq = radius * radius;
            int minPack = Math.Max(3, settings.MinPackSizeForDomain);

            foreach (var kv in wildBySpecies)
            {
                string sp = kv.Key;
                List<Actor> beasts = kv.Value;
                if (beasts.Count < minPack) continue;

                var used = new HashSet<Actor>();
                for (int i = 0; i < beasts.Count; i++)
                {
                    Actor seed = beasts[i];
                    if (used.Contains(seed)) continue;

                    var cluster = new List<Actor> { seed };
                    float sumX = seed.current_tile.x;
                    float sumY = seed.current_tile.y;

                    for (int j = i + 1; j < beasts.Count; j++)
                    {
                        Actor other = beasts[j];
                        if (used.Contains(other)) continue;
                        float dx = seed.current_tile.x - other.current_tile.x;
                        float dy = seed.current_tile.y - other.current_tile.y;
                        if (dx * dx + dy * dy <= radSq)
                        {
                            cluster.Add(other);
                            sumX += other.current_tile.x;
                            sumY += other.current_tile.y;
                        }
                    }

                    if (cluster.Count < minPack) continue;

                    // Pick Alpha (highest health/existing giant)
                    Actor alpha = cluster[0];
                    foreach (Actor b in cluster)
                    {
                        used.Add(b);
                        if (b.getHealth() > alpha.getHealth()) alpha = b;
                    }

                    // Crown Alpha beast
                    alpha.addTrait("giant");
                    alpha.addTrait("strong");
                    alpha.addTrait("tough");
                    alpha.addTrait("veteran");
                    alpha.setStatsDirty();

                    string prettyPlural = PluralizeSpecies(sp);

                    ActiveDomains.Add(new FeralPackDomain
                    {
                        SpeciesId = sp,
                        DomainTitle = $"Land of the {prettyPlural}",
                        Alpha = alpha,
                        CenterX = sumX / cluster.Count,
                        CenterY = sumY / cluster.Count,
                        Radius = radius,
                        PackCount = cluster.Count
                    });
                }
            }

            DefendDomainBorders(alive);
        }

        private static void DefendDomainBorders(List<Actor> alive)
        {
            if (ActiveDomains.Count == 0) return;

            foreach (var domain in ActiveDomains)
            {
                if (domain.Alpha == null || !domain.Alpha.isAlive()) continue;
                float rSq = domain.Radius * domain.Radius;

                // Repel encroaching civilized trespassers inside the pack's hunting grounds
                int warned = 0;
                for (int i = 0; i < alive.Count && warned < 4; i++)
                {
                    Actor u = alive[i];
                    if (u == null || !u.isAlive() || u.current_tile == null) continue;
                    if (u.kingdom == null || !u.kingdom.isCiv()) continue;
                    if (u.asset != null && u.asset.is_boat) continue;

                    float dx = u.current_tile.x - domain.CenterX;
                    float dy = u.current_tile.y - domain.CenterY;
                    if (dx * dx + dy * dy <= rSq * 0.65f)
                    {
                        // Nudge pack aggression onto trespasser by applying territorial pressure
                        u.changeHealth(-4);
                        u.startShake(0.2f, 0.06f, true, true);
                        warned++;
                    }
                }
            }
        }
    }
}
