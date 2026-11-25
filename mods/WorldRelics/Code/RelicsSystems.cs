using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace WorldRelics
{
    public class FallenRuinSite
    {
        public string CityName;
        public string KingdomName;
        public int FallYear;
        public int CenterX;
        public int CenterY;
        public List<(int x, int y)> WallTiles = new List<(int x, int y)>();
        public List<(int x, int y)> RoadTiles = new List<(int x, int y)>();
        public int LastWeatheredStage = 0;
    }

    public static class InfrastructureBuilder
    {
        public static int WallsBuilt { get; private set; }
        public static int BridgesBuilt { get; private set; }

        private static readonly FieldInfo CityZonesField = AccessTools.Field(typeof(City), "zones");
        private static readonly FieldInfo CityKingdomField = AccessTools.Field(typeof(City), "kingdom");

        public static List<TileZone> GetCityZones(City city)
        {
            return city != null ? CityZonesField?.GetValue(city) as List<TileZone> : null;
        }

        public static Kingdom GetCityKingdom(City city)
        {
            return city != null ? CityKingdomField?.GetValue(city) as Kingdom : null;
        }

        /// <summary>
        /// Builds gated perimeter walls around large cities along their outer zone boundaries.
        /// Leaves regular 3-tile road gate openings so armies and settlers move freely.
        /// </summary>
        public static int FortifyLargeCities(bool force = false)
        {
            var world = MapBox.instance;
            var cfg = Main.Settings;
            if (world?.cities == null || cfg == null || (!force && !cfg.EnableCityWalls)) return 0;

            int builtNow = 0;
            foreach (City city in world.cities)
            {
                var zones = GetCityZones(city);
                if (city == null || !city.isAlive() || zones == null || zones.Count < 4) continue;
                int pop = city.getPopulationPeople();
                if (!force && pop < cfg.MinPopForWalls) continue;

                TopTileType wallType = pop >= cfg.MinPopForIronWalls
                    ? TopTileLibrary.wall_iron
                    : TopTileLibrary.wall_order;

                var ownedZones = new HashSet<(int, int)>();
                foreach (TileZone z in zones)
                {
                    if (z != null) ownedZones.Add((z.x, z.y));
                }

                int cityWallsThisPass = 0;
                foreach (TileZone z in zones)
                {
                    if (z == null || cityWallsThisPass >= 24) break;
                    bool edgeN = !ownedZones.Contains((z.x, z.y + 1));
                    bool edgeS = !ownedZones.Contains((z.x, z.y - 1));
                    bool edgeE = !ownedZones.Contains((z.x + 1, z.y));
                    bool edgeW = !ownedZones.Contains((z.x - 1, z.y));
                    if (!edgeN && !edgeS && !edgeE && !edgeW) continue;

                    int x0 = z.x * 8;
                    int y0 = z.y * 8;

                    if (edgeN) cityWallsThisPass += LayWallSegment(world, x0, y0 + 7, 1, 0, 8, wallType);
                    if (edgeS) cityWallsThisPass += LayWallSegment(world, x0, y0, 1, 0, 8, wallType);
                    if (edgeE) cityWallsThisPass += LayWallSegment(world, x0 + 7, y0, 0, 1, 8, wallType);
                    if (edgeW) cityWallsThisPass += LayWallSegment(world, x0, y0, 0, 1, 8, wallType);
                }

                builtNow += cityWallsThisPass;
            }

            if (builtNow > 0)
            {
                WallsBuilt += builtNow;
                world.redrawTiles();
            }
            return builtNow;
        }

        private static int LayWallSegment(MapBox world, int sx, int sy, int dx, int dy, int len, TopTileType wallType)
        {
            int placed = 0;
            for (int i = 0; i < len; i++)
            {
                int tx = sx + dx * i;
                int ty = sy + dy * i;

                // Keep natural 3-tile wide road gates open every 16 tiles
                if ((dx != 0 && (tx % 16 == 7 || tx % 16 == 8 || tx % 16 == 9)) ||
                    (dy != 0 && (ty % 16 == 7 || ty % 16 == 8 || ty % 16 == 9)))
                {
                    continue;
                }

                WorldTile tile = world.GetTile(tx, ty);
                if (tile == null || tile.Type == null || tile.Type.liquid || !tile.Type.ground) continue;
                if (tile.building != null) continue;
                if (tile.top_type == TopTileLibrary.road || IsWallTile(tile.top_type)) continue;

                MapAction.terraformTop(tile, wallType, false);
                placed++;
            }
            return placed;
        }

        /// <summary>
        /// Connects friendly cities across narrow ocean straits with stone road causeways.
        /// </summary>
        public static int BuildNarrowSeaBridges(bool force = false)
        {
            var world = MapBox.instance;
            var cfg = Main.Settings;
            if (world?.cities == null || cfg == null || (!force && !cfg.EnableSeaBridges)) return 0;

            var cities = new List<City>();
            foreach (City c in world.cities)
            {
                if (c != null && c.isAlive() && c.getTile() != null) cities.Add(c);
            }

            int maxWater = Math.Max(8, cfg.MaxBridgeWaterTiles);
            int bridgesNow = 0;

            for (int i = 0; i < cities.Count && bridgesNow < 2; i++)
            {
                for (int j = i + 1; j < cities.Count && bridgesNow < 2; j++)
                {
                    City a = cities[i];
                    City b = cities[j];
                    Kingdom ka = GetCityKingdom(a);
                    Kingdom kb = GetCityKingdom(b);
                    if (ka == null || kb == null) continue;
                    if (ka.isEnemy(kb)) continue;

                    WorldTile ta = a.getTile();
                    WorldTile tb = b.getTile();
                    float dist = Vector2.Distance(new Vector2(ta.x, ta.y), new Vector2(tb.x, tb.y));
                    if (dist < 14f || dist > 95f) continue;

                    if (TryBuildCauseway(world, ta, tb, maxWater))
                    {
                        bridgesNow++;
                        BridgesBuilt++;
                    }
                }
            }

            if (bridgesNow > 0)
            {
                world.redrawTiles();
            }
            return bridgesNow;
        }

        private static bool TryBuildCauseway(MapBox world, WorldTile a, WorldTile b, int maxWater)
        {
            int steps = Mathf.CeilToInt(Vector2.Distance(new Vector2(a.x, a.y), new Vector2(b.x, b.y)));
            if (steps <= 0) return false;

            var waterSpan = new List<WorldTile>();
            for (int s = 1; s < steps; s++)
            {
                float t = (float)s / steps;
                int x = Mathf.RoundToInt(Mathf.Lerp(a.x, b.x, t));
                int y = Mathf.RoundToInt(Mathf.Lerp(a.y, b.y, t));
                WorldTile tile = world.GetTile(x, y);
                if (tile == null) return false;
                if (tile.Type != null && tile.Type.liquid)
                {
                    waterSpan.Add(tile);
                    if (waterSpan.Count > maxWater) return false;
                }
            }

            // Require an actual water strait of at least 4 tiles
            if (waterSpan.Count < 4) return false;

            foreach (WorldTile wt in waterSpan)
            {
                MapAction.terraformMain(wt, TileLibrary.soil_low, false);
                MapAction.terraformTop(wt, TopTileLibrary.road, false);
                WorldTile adj = world.GetTile(wt.x + 1, wt.y);
                if (adj != null && adj.Type != null && adj.Type.liquid)
                {
                    MapAction.terraformMain(adj, TileLibrary.soil_low, false);
                    MapAction.terraformTop(adj, TopTileLibrary.road, false);
                }
            }
            return true;
        }

        public static bool IsWallTile(TopTileType top)
        {
            if (top == null) return false;
            return top == TopTileLibrary.wall_order
                || top == TopTileLibrary.wall_iron
                || top == TopTileLibrary.wall_ancient
                || top == TopTileLibrary.wall_green
                || top == TopTileLibrary.wall_wild
                || top == TopTileLibrary.wall_evil
                || top == TopTileLibrary.wall_light;
        }
    }

    [HarmonyPatch(typeof(City))]
    public static class RuinsDecaySystem
    {
        public static readonly List<FallenRuinSite> RuinSites = new List<FallenRuinSite>();
        private static readonly FieldInfo MapStatsField = AccessTools.Field(typeof(MapBox), "map_stats");

        public static int CurrentYear()
        {
            var world = MapBox.instance;
            if (world != null && MapStatsField?.GetValue(world) is MapStats stats)
            {
                return stats.history_current_year;
            }
            return 0;
        }

        // Capture a city's walls and road silhouettes right before it is destroyed
        [HarmonyPrefix]
        [HarmonyPatch(nameof(City.destroyCity))]
        private static void DestroyCityPrefix(City __instance)
        {
            var zones = InfrastructureBuilder.GetCityZones(__instance);
            if (__instance == null || zones == null || zones.Count == 0) return;
            var world = MapBox.instance;
            if (world == null) return;

            var walls = new List<(int x, int y)>();
            var roads = new List<(int x, int y)>();

            foreach (TileZone z in zones)
            {
                if (z == null) continue;
                int x0 = z.x * 8;
                int y0 = z.y * 8;
                for (int dy = 0; dy < 8; dy++)
                {
                    for (int dx = 0; dx < 8; dx++)
                    {
                        WorldTile t = world.GetTile(x0 + dx, y0 + dy);
                        if (t == null) continue;
                        if (InfrastructureBuilder.IsWallTile(t.top_type))
                        {
                            walls.Add((t.x, t.y));
                        }
                        else if (t.top_type == TopTileLibrary.road)
                        {
                            roads.Add((t.x, t.y));
                        }
                    }
                }
            }

            // Only register meaningful territories that left visible walls or extensive roadworks
            if (walls.Count < 8 && roads.Count < 20) return;

            WorldTile center = __instance.getTile();
            var site = new FallenRuinSite
            {
                CityName = __instance.name ?? "Ancient City",
                KingdomName = InfrastructureBuilder.GetCityKingdom(__instance)?.name ?? "Fallen Realm",
                FallYear = CurrentYear(),
                CenterX = center != null ? center.x : (walls.Count > 0 ? walls[0].x : roads[0].x),
                CenterY = center != null ? center.y : (walls.Count > 0 ? walls[0].y : roads[0].y),
                WallTiles = walls,
                RoadTiles = roads,
                LastWeatheredStage = 0
            };

            if (RuinSites.Count >= 32) RuinSites.RemoveAt(0);
            RuinSites.Add(site);
            Debug.Log($"[WorldRelics] Preserved ruins of {site.CityName} ({walls.Count} wall tiles, {roads.Count} roads).");
        }

        /// <summary>
        /// Weathers fallen ruins over decades and centuries:
        /// Stage 1 (Ancient): wall_order/iron -> wall_ancient + small structural gaps
        /// Stage 2 (Mossy): wall_ancient -> wall_green (moss-overgrown stone walls)
        /// Stage 3 (Wild Overgrowth): wall_green -> wall_wild (vine-wrapped ancient relics)
        /// </summary>
        public static int WeatherAllRuins(int bonusAgeYears = 0)
        {
            var world = MapBox.instance;
            var cfg = Main.Settings;
            if (world == null || cfg == null || !cfg.EnableRuinDecay) return 0;

            int year = CurrentYear();
            int tilesChanged = 0;

            foreach (var site in RuinSites)
            {
                if (bonusAgeYears > 0) site.FallYear -= bonusAgeYears;
                int age = Math.Max(0, year - site.FallYear);

                int targetStage = 0;
                if (age >= cfg.WildOvergrowthYears) targetStage = 3;
                else if (age >= cfg.MossyStageYears) targetStage = 2;
                else if (age >= cfg.AncientStageYears) targetStage = 1;

                if (targetStage <= site.LastWeatheredStage) continue;

                for (int i = 0; i < site.WallTiles.Count; i++)
                {
                    var (tx, ty) = site.WallTiles[i];
                    WorldTile tile = world.GetTile(tx, ty);
                    if (tile == null || !InfrastructureBuilder.IsWallTile(tile.top_type)) continue;

                    // Deterministic organic hash per tile so breaches look natural
                    int hash = ((tx * 73856093) ^ (ty * 19349663)) & 0xFF;

                    if (targetStage == 1)
                    {
                        if (hash < 28) MapAction.terraformTop(tile, TopTileLibrary.grass_low, false); // cracked breach
                        else MapAction.terraformTop(tile, TopTileLibrary.wall_ancient, false);
                        tilesChanged++;
                    }
                    else if (targetStage == 2)
                    {
                        if (hash < 48) MapAction.terraformTop(tile, TopTileLibrary.grass_high, false);
                        else MapAction.terraformTop(tile, TopTileLibrary.wall_green, false); // mossy ancient stone!
                        tilesChanged++;
                    }
                    else if (targetStage == 3)
                    {
                        if (hash < 76) MapAction.terraformTop(tile, TopTileLibrary.grass_high, false);
                        else MapAction.terraformTop(tile, hash % 2 == 0 ? TopTileLibrary.wall_wild : TopTileLibrary.wall_green, false);
                        tilesChanged++;
                    }
                }

                // Gradually let grass reclaim half the old paved roads
                for (int i = 0; i < site.RoadTiles.Count; i++)
                {
                    var (tx, ty) = site.RoadTiles[i];
                    WorldTile tile = world.GetTile(tx, ty);
                    if (tile == null || tile.top_type != TopTileLibrary.road) continue;

                    int hash = ((tx * 83492791) ^ (ty * 297121507)) & 0xFF;
                    if ((targetStage == 1 && hash < 60) || (targetStage == 2 && hash < 130) || (targetStage == 3 && hash < 185))
                    {
                        MapAction.terraformTop(tile, TopTileLibrary.grass_low, false);
                        tilesChanged++;
                    }
                }

                site.LastWeatheredStage = targetStage;
            }

            if (tilesChanged > 0)
            {
                world.redrawTiles();
            }
            return tilesChanged;
        }

        public static string StageLabel(FallenRuinSite site)
        {
            int age = Math.Max(0, CurrentYear() - site.FallYear);
            if (site.LastWeatheredStage >= 3) return $"{age}y — Wild Overgrowth";
            if (site.LastWeatheredStage == 2) return $"{age}y — Mossy Ruins";
            if (site.LastWeatheredStage == 1) return $"{age}y — Cracked Ancient";
            return $"{age}y — Fresh Ruins";
        }
    }
}
