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

    [HarmonyPatch]
    public static class WildSovereigntySystem
    {
        public static readonly List<FeralPackDomain> ActiveDomains = new List<FeralPackDomain>();

        private static readonly HashSet<string> ExcludedFeralSpecies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "butterfly", "beetle", "fly", "grasshopper", "ant_blue", "ant_green", "ant_red", "ant_black", "bee", "piranha"
        };

        private static readonly MethodInfo SpeciesIdGetter =
            AccessTools.PropertyGetter(typeof(Subspecies), "species_id");

        public static bool EnsureValidArchitecture(ActorAsset asset)
        {
            if (asset == null) return false;
            if (asset.architecture_asset != null) return true;

            ArchitectureAsset arch = null;
            var archLib = AssetManager.architecture_library;
            if (archLib != null)
            {
                if (!string.IsNullOrEmpty(asset.architecture_id))
                    arch = archLib.get(asset.architecture_id);

                if (arch == null)
                    arch = archLib.get("civ_bandit") ?? archLib.get("human");
            }

            if (arch == null) return false;

            asset.architecture_asset = arch;
            if (string.IsNullOrEmpty(asset.architecture_id))
                asset.architecture_id = arch.id;
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(BuildOrder), nameof(BuildOrder.getBuildingAsset))]
        private static void BuildOrderEnsureArchPrefix(City pCity)
        {
            ActorAsset asset = pCity?.getActorAsset();
            if (asset != null && asset.architecture_asset == null)
            {
                EnsureValidArchitecture(asset);
            }
        }

        private static bool IsEligibleWildAnimal(ActorAsset asset)
        {
            if (asset == null || asset.is_boat || asset.civ) return false;
            if (ExcludedFeralSpecies.Contains(asset.id)) return false;
            return asset.default_animal;
        }

        public static string PluralizeSpecies(string id)
        {
            if (string.IsNullOrEmpty(id)) return "Beasts";
            string norm = SpeciesPersonalityRegistry.NormalizeSpecies(id);
            if (string.IsNullOrEmpty(norm)) return "Beasts";
            switch (norm)
            {
                case "wolf": return "Wolves";
                case "fox": return "Foxes";
                case "sheep": return "Sheep";
                case "deer": return "Deer";
                case "fish": return "Fish";
                case "mouse": return "Mice";
                case "monkey": return "Monkeys";
                case "butterfly": return "Butterflies";
                case "buffalo": return "Buffalo";
                case "ostrich": return "Ostriches";
                case "rhino": return "Rhinos";
                case "hyena": return "Hyenas";
                case "alpaca": return "Alpacas";
                case "capybara": return "Capybaras";
                case "piranha": return "Piranhas";
            }
            string clean = norm.Replace('_', ' ');
            string titled = char.ToUpper(clean[0]) + clean.Substring(1);
            if (titled.EndsWith("s") || titled.EndsWith("x") || titled.EndsWith("ch") || titled.EndsWith("sh"))
                return titled + "es";
            return titled + "s";
        }

        private static bool IsUnclaimedBuildableLand(WorldTile tile)
        {
            if (tile == null || tile.Type == null) return false;
            if (tile.Type.liquid || !tile.Type.ground) return false;
            if (tile.zone == null || tile.zone.hasCity() || tile.zone.city != null) return false;
            if (!tile.zone.canStartCityHere() || !tile.zone.isGoodForNewCity()) return false;
            return true;
        }

        /// <summary>
        /// Scans unaligned wild animals and auto-founds literal animal kingdoms when enough
        /// same-species animals gather on unclaimed buildable land.
        /// </summary>
        public static int ScanAndAutoFoundAnimalKingdoms()
        {
            var world = MapBox.instance;
            var settings = Main.Settings;
            if (world?.units?.units_only_alive == null || settings == null || !settings.AutoFoundAnimalKingdoms)
                return 0;

            int minCluster = Mathf.Clamp(settings.ClusterSizeToFound, 4, 25);
            float radius = Mathf.Clamp(settings.ClusterRadiusTiles, 8f, 40f);
            float radSq = radius * radius;

            var wildBySpecies = new Dictionary<string, List<Actor>>(StringComparer.OrdinalIgnoreCase);
            var alive = new List<Actor>(world.units.units_only_alive);

            for (int i = 0; i < alive.Count; i++)
            {
                Actor u = alive[i];
                if (u == null || !u.isAlive() || u.current_tile == null || u.asset == null) continue;
                if (u.asset.is_boat) continue;
                if (u.city != null || (u.kingdom != null && u.kingdom.isCiv())) continue;

                string sp = SpeciesPersonalityRegistry.NormalizeSpecies(u.asset.id);
                if (!settings.IsSpeciesAllowedToFound(sp)) continue;

                if (!wildBySpecies.TryGetValue(sp, out var list))
                {
                    list = new List<Actor>(16);
                    wildBySpecies[sp] = list;
                }
                list.Add(u);
            }

            int foundedCount = 0;
            const int maxFoundedPerScan = 2;

            foreach (var kv in wildBySpecies)
            {
                if (foundedCount >= maxFoundedPerScan) break;

                string sp = kv.Key;
                List<Actor> beasts = kv.Value;
                if (beasts.Count < minCluster) continue;

                var used = new HashSet<Actor>();
                for (int i = 0; i < beasts.Count && foundedCount < maxFoundedPerScan; i++)
                {
                    Actor seed = beasts[i];
                    if (used.Contains(seed)) continue;
                    if (!IsUnclaimedBuildableLand(seed.current_tile)) continue;

                    var cluster = new List<Actor>(minCluster + 4) { seed };
                    int sx = seed.current_tile.x;
                    int sy = seed.current_tile.y;

                    for (int j = 0; j < beasts.Count; j++)
                    {
                        if (j == i) continue;
                        Actor other = beasts[j];
                        if (used.Contains(other)) continue;

                        int dx = sx - other.current_tile.x;
                        int dy = sy - other.current_tile.y;
                        if (dx * dx + dy * dy <= radSq)
                        {
                            cluster.Add(other);
                        }
                    }

                    if (cluster.Count < minCluster) continue;

                    // Choose a leader on valid unclaimed buildable land with a valid subspecies
                    Actor leader = null;
                    for (int c = 0; c < cluster.Count; c++)
                    {
                        Actor cand = cluster[c];
                        if (cand.subspecies != null && IsUnclaimedBuildableLand(cand.current_tile))
                        {
                            leader = cand;
                            break;
                        }
                    }
                    if (leader == null) continue;

                    for (int c = 0; c < cluster.Count; c++)
                        used.Add(cluster[c]);

                    if (TryFoundAnimalKingdomFromCluster(leader, cluster, sp))
                    {
                        foundedCount++;
                    }
                }
            }

            return foundedCount;
        }

        private static bool TryFoundAnimalKingdomFromCluster(Actor leader, List<Actor> cluster, string speciesId)
        {
            try
            {
                Subspecies sub = leader.subspecies;
                if (sub == null) return false;
                if (!EnsureValidArchitecture(leader.asset)) return false;

                // Keep literal animal sprite — only awaken sapience on the animal's own subspecies
                if (!sub.isSapient())
                {
                    sub.makeSapient();
                }

                SpeciesPersonalityRegistry.ApplyProfileToSubspecies(sub, speciesId);
                for (int i = 0; i < cluster.Count; i++)
                {
                    Actor member = cluster[i];
                    if (member != null && member.isAlive())
                    {
                        SpeciesPersonalityRegistry.ApplyProfileToActor(member, speciesId);
                    }
                }

                if (!leader.buildCityAndStartCivilization() || leader.city == null || leader.kingdom == null || !leader.kingdom.isCiv())
                {
                    return false;
                }

                City newCity = leader.city;
                Kingdom newKingdom = leader.kingdom;

                for (int i = 0; i < cluster.Count; i++)
                {
                    Actor member = cluster[i];
                    if (member == null || member == leader || !member.isAlive()) continue;
                    if (member.subspecies != sub)
                    {
                        try { member.setSubspecies(sub); } catch { }
                    }
                    try
                    {
                        member.joinCity(newCity);
                        member.setMetasFromCity(newCity);
                    }
                    catch { }
                }

                if (newKingdom.culture != null)
                {
                    SpeciesPersonalityRegistry.ApplyProfileToCulture(newKingdom.culture, speciesId);
                }

                string plural = PluralizeSpecies(speciesId);
                string toast = $"{cluster.Count} {plural} banded together to found {newKingdom.name}!";
                Debug.Log($"[ApexFactions] {toast}");
                try { WorldTip.showNowTop(toast, false); } catch { }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ApexFactions] Auto-founding {speciesId} kingdom failed: {e.Message}");
                return false;
            }
        }

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

            ActorAsset wolfAsset = AssetManager.actor_library?.get(speciesId);
            EnsureValidArchitecture(wolfAsset);

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
                if (u != null)
                {
                    SpeciesPersonalityRegistry.ApplyProfileToActor(u, "wolf");
                    settlers.Add(u);
                }
            }

            if (settlers.Count == 0) return 0;

            Actor leader = settlers[0];
            try
            {
                EnsureValidArchitecture(leader.asset);
                if (leader.subspecies != null)
                {
                    if (!leader.subspecies.isSapient())
                    {
                        leader.subspecies.makeSapient();
                    }
                    SpeciesPersonalityRegistry.ApplyProfileToSubspecies(leader.subspecies, "wolf");
                }

                if (leader.buildCityAndStartCivilization() && leader.city != null && leader.kingdom != null && leader.kingdom.isCiv())
                {
                    City newCity = leader.city;
                    for (int i = 1; i < settlers.Count; i++)
                    {
                        try
                        {
                            settlers[i].joinCity(newCity);
                            settlers[i].setMetasFromCity(newCity);
                        }
                        catch { }
                    }
                    if (leader.kingdom.culture != null)
                    {
                        SpeciesPersonalityRegistry.ApplyProfileToCulture(leader.kingdom.culture, "wolf");
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
        /// and applies their species personality profile.
        /// </summary>
        public static int AwakenWildSubspecies(string filterSpecies = null)
        {
            var world = MapBox.instance;
            if (world?.subspecies == null) return 0;

            string normFilter = string.IsNullOrEmpty(filterSpecies)
                ? null
                : SpeciesPersonalityRegistry.NormalizeSpecies(filterSpecies);

            int awakened = 0;
            var subs = new List<Subspecies>(world.subspecies);
            for (int i = 0; i < subs.Count; i++)
            {
                Subspecies sub = subs[i];
                if (sub == null || !sub.isAlive() || sub.isSapient()) continue;
                string rawSid = SpeciesIdGetter?.Invoke(sub, null) as string ?? "";
                string normSid = SpeciesPersonalityRegistry.NormalizeSpecies(rawSid);

                // Only awaken our 30 verified normal animal species
                if (string.IsNullOrEmpty(normSid) || !SpeciesPersonalityRegistry.Profiles.ContainsKey(normSid))
                    continue;
                if (!string.IsNullOrEmpty(normFilter) && !string.Equals(normSid, normFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                ActorAsset asset = AssetManager.actor_library?.get(rawSid);
                if (!EnsureValidArchitecture(asset))
                    continue;

                try
                {
                    sub.makeSapient();
                    SpeciesPersonalityRegistry.ApplyProfileToSubspecies(sub, normSid);
                    awakened++;
                    Debug.Log($"[ApexFactions] Awakened wild subspecies '{sub.name}' ({rawSid}) into sapience!");
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
            var alive = new List<Actor>(world.units.units_only_alive);

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

                    Actor alpha = cluster[0];
                    foreach (Actor b in cluster)
                    {
                        used.Add(b);
                        if (b.getHealth() > alpha.getHealth()) alpha = b;
                    }

                    alpha.addTrait("giant", false);
                    alpha.addTrait("strong", false);
                    alpha.addTrait("tough", false);
                    alpha.addTrait("veteran", false);
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
            if (ActiveDomains.Count == 0 || alive == null) return;

            for (int d = 0; d < ActiveDomains.Count; d++)
            {
                var domain = ActiveDomains[d];
                if (domain.Alpha == null || !domain.Alpha.isAlive()) continue;
                float rSq = domain.Radius * domain.Radius;

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
                        u.changeHealth(-4);
                        u.startShake(0.2f, 0.06f, true, true);
                        warned++;
                    }
                }
            }
        }
    }
}
