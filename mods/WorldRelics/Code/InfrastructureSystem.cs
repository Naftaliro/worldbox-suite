using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace WorldRelics
{
    public static class InfrastructureSystem
    {
        public static int HighwaysBuilt { get; private set; }
        public static int CitadelsBuilt { get; private set; }
        public static int GarrisonHeals { get; private set; }

        private struct CitadelRecord
        {
            public long CityId;
            public int CenterX;
            public int CenterY;
            public int Radius;
        }

        private static readonly HashSet<(long, long)> ConnectedPairs = new HashSet<(long, long)>();
        private static readonly Dictionary<long, CitadelRecord> CitadelCities = new Dictionary<long, CitadelRecord>();

        private static readonly MethodInfo RestoreStaminaMethod =
            AccessTools.Method(typeof(Actor), "addStamina", new[] { typeof(int) })
            ?? AccessTools.Method(typeof(Actor), "restoreStamina", new[] { typeof(int) })
            ?? AccessTools.Method(typeof(Actor), "changeStamina", new[] { typeof(int) });

        private static readonly MethodInfo AddNutritionMethod =
            AccessTools.Method(typeof(Actor), "addNutrition", new[] { typeof(int) });

        private static float _highwayTimer = 0f;
        private static float _citadelTimer = 0f;
        private static float _garrisonTimer = 0f;
        private static int _lastWorldHash = 0;

        public static void Reset()
        {
            ConnectedPairs.Clear();
            CitadelCities.Clear();
            _highwayTimer = 0f;
            _citadelTimer = 0f;
            _garrisonTimer = 0f;
            _lastWorldHash = 0;
        }

        public static void Tick(float dt)
        {
            var world = MapBox.instance;
            var cfg = Main.Settings;
            if (world == null || cfg == null || Config.paused) return;

            int worldHash = world.GetHashCode();
            if (worldHash != _lastWorldHash)
            {
                _lastWorldHash = worldHash;
                ConnectedPairs.Clear();
                CitadelCities.Clear();
            }

            if (cfg.EnableImperialHighways)
            {
                _highwayTimer += dt;
                if (_highwayTimer >= 25f)
                {
                    _highwayTimer = 0f;
                    BuildImperialHighways(force: false);
                }
            }

            if (cfg.EnableCitadelCompounds)
            {
                _citadelTimer += dt;
                if (_citadelTimer >= 30f)
                {
                    _citadelTimer = 0f;
                    BuildCitadelCompounds(force: false);
                }

                _garrisonTimer += dt;
                if (_garrisonTimer >= 15f)
                {
                    _garrisonTimer = 0f;
                    ApplyCitadelGarrisonBuff();
                }
            }
        }

        /// <summary>
        /// Connects pairs of major friendly/allied capitals or large cities (pop >= 45,
        /// distance 18..140 tiles) with 2-3 tile wide cobblestone highways and 2-tile
        /// stone bridges across narrow water gaps (<= 10 tiles).
        /// </summary>
        public static int BuildImperialHighways(bool force = false)
        {
            var world = MapBox.instance;
            var cfg = Main.Settings;
            if (world?.cities == null || cfg == null || (!force && !cfg.EnableImperialHighways)) return 0;

            var eligible = new List<(City city, Kingdom kingdom, WorldTile tile, int pop, bool isCapital)>();
            var citiesSnapshot = new List<City>(world.cities);
            for (int i = 0; i < citiesSnapshot.Count; i++)
            {
                City c = citiesSnapshot[i];
                if (c == null || !c.isAlive()) continue;
                WorldTile tile = c.getTile();
                if (tile == null) continue;
                Kingdom k = InfrastructureBuilder.GetCityKingdom(c);
                if (k == null || !k.isAlive() || !k.isCiv()) continue;

                int pop = c.getPopulationPeople();
                if (pop < 45) continue;

                bool isCapital = k.capital == c;
                eligible.Add((c, k, tile, pop, isCapital));
            }

            if (eligible.Count < 2) return 0;

            // Prioritize capitals and larger cities first
            eligible.Sort((a, b) =>
            {
                int capCmp = b.isCapital.CompareTo(a.isCapital);
                if (capCmp != 0) return capCmp;
                return b.pop.CompareTo(a.pop);
            });

            int maxSegments = force ? 6 : 2;
            int builtNow = 0;
            bool anyTerraformed = false;

            // Pass 0: same-kingdom highways first; Pass 1: friendly/allied cross-kingdom highways
            for (int pass = 0; pass < 2 && builtNow < maxSegments; pass++)
            {
                for (int i = 0; i < eligible.Count && builtNow < maxSegments; i++)
                {
                    var a = eligible[i];
                    for (int j = i + 1; j < eligible.Count && builtNow < maxSegments; j++)
                    {
                        var b = eligible[j];
                        bool sameKingdom = a.kingdom == b.kingdom;
                        if (pass == 0 && !sameKingdom) continue;
                        if (pass == 1 && sameKingdom) continue;
                        if (!sameKingdom && a.kingdom.isEnemy(b.kingdom)) continue;

                        long idLo = Math.Min(a.city.id, b.city.id);
                        long idHi = Math.Max(a.city.id, b.city.id);
                        var pairKey = (idLo, idHi);
                        if (ConnectedPairs.Contains(pairKey)) continue;

                        float dx = b.tile.x - a.tile.x;
                        float dy = b.tile.y - a.tile.y;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);
                        if (dist < 18f || dist > 140f) continue;

                        int roadWidth = (a.isCapital || b.isCapital || a.pop >= 90 || b.pop >= 90) ? 3 : 2;
                        if (TryBuildHighwaySegment(world, a.tile, b.tile, roadWidth, out bool changed))
                        {
                            ConnectedPairs.Add(pairKey);
                            builtNow++;
                            HighwaysBuilt++;
                            if (changed) anyTerraformed = true;
                        }
                    }
                }
            }

            if (anyTerraformed)
            {
                world.redrawTiles();
            }
            return builtNow;
        }

        private static bool TryBuildHighwaySegment(MapBox world, WorldTile start, WorldTile end, int roadWidth, out bool changedAny)
        {
            changedAny = false;
            float totalDx = end.x - start.x;
            float totalDy = end.y - start.y;
            int steps = Mathf.CeilToInt(Mathf.Sqrt(totalDx * totalDx + totalDy * totalDy));
            if (steps < 18) return false;

            // Validate route before modifying terrain: water gaps along centerline must be <= 10 tiles
            var path = new List<(int x, int y)>(steps + 1);
            int totalWater = 0;
            int currentWaterRun = 0;
            int maxWaterRun = 0;
            int lastX = int.MinValue;
            int lastY = int.MinValue;

            for (int s = 0; s <= steps; s++)
            {
                float t = (float)s / steps;
                int x = Mathf.RoundToInt(Mathf.Lerp(start.x, end.x, t));
                int y = Mathf.RoundToInt(Mathf.Lerp(start.y, end.y, t));
                if (x == lastX && y == lastY) continue;
                lastX = x;
                lastY = y;

                WorldTile tile = world.GetTile(x, y);
                if (tile == null || tile.Type == null) return false;
                if (tile.Type.lava) return false;

                if (tile.Type.liquid)
                {
                    totalWater++;
                    currentWaterRun++;
                    if (currentWaterRun > maxWaterRun) maxWaterRun = currentWaterRun;
                    if (maxWaterRun > 10 || totalWater > 10) return false;
                }
                else
                {
                    currentWaterRun = 0;
                }

                path.Add((x, y));
            }

            if (path.Count < 10) return false;

            // Determine perpendicular axis for road width (2 or 3 tiles wide)
            bool mostlyHorizontal = Math.Abs(totalDx) >= Math.Abs(totalDy);
            int px = mostlyHorizontal ? 0 : 1;
            int py = mostlyHorizontal ? 1 : 0;

            int minOff = roadWidth >= 3 ? -1 : 0;
            int maxOff = 1;
            int tilesLaid = 0;

            for (int i = 0; i < path.Count; i++)
            {
                int cx = path[i].x;
                int cy = path[i].y;
                WorldTile centerTile = world.GetTile(cx, cy);
                if (centerTile == null || centerTile.Type == null) continue;

                bool isWaterStep = centerTile.Type.liquid;
                int stepMin = isWaterStep ? 0 : minOff;
                int stepMax = isWaterStep ? 1 : maxOff; // 2-tile-wide stone bridge on water

                for (int off = stepMin; off <= stepMax; off++)
                {
                    WorldTile tile = world.GetTile(cx + px * off, cy + py * off);
                    if (tile == null || tile.Type == null) continue;
                    if (tile.Type.lava) continue;
                    if (tile.building != null) continue;
                    if (InfrastructureBuilder.IsWallTile(tile.top_type)) continue;

                    if (tile.Type.liquid)
                    {
                        MapAction.terraformMain(tile, TileLibrary.soil_low, false);
                        MapAction.terraformTop(tile, TopTileLibrary.road, false);
                        tilesLaid++;
                    }
                    else if (tile.Type.ground)
                    {
                        if (tile.top_type != TopTileLibrary.road)
                        {
                            MapAction.terraformTop(tile, TopTileLibrary.road, false);
                            tilesLaid++;
                        }
                    }
                }
            }

            changedAny = tilesLaid > 0;
            return tilesLaid > 0;
        }

        /// <summary>
        /// Constructs double-thick gated stone fortress compounds around major capitals
        /// (pop >= 70, >= 4 zones) strictly within the capital's owned zones.
        /// </summary>
        public static int BuildCitadelCompounds(bool force = false)
        {
            var world = MapBox.instance;
            var cfg = Main.Settings;
            if (world?.cities == null || cfg == null || (!force && !cfg.EnableCitadelCompounds)) return 0;

            int builtNow = 0;
            int maxPerPass = force ? 6 : 2;
            bool anyTerraformed = false;

            var citiesSnapshot = new List<City>(world.cities);
            for (int i = 0; i < citiesSnapshot.Count && builtNow < maxPerPass; i++)
            {
                City city = citiesSnapshot[i];
                if (city == null || !city.isAlive()) continue;
                if (CitadelCities.ContainsKey(city.id)) continue;

                Kingdom kingdom = InfrastructureBuilder.GetCityKingdom(city);
                if (kingdom == null || !kingdom.isAlive() || !kingdom.isCiv()) continue;
                if (kingdom.capital != city) continue;

                int pop = city.getPopulationPeople();
                if (pop < 70) continue;

                var zones = InfrastructureBuilder.GetCityZones(city);
                if (zones == null || zones.Count < 4) continue;

                WorldTile center = city.getTile();
                if (center == null) continue;

                var ownedZones = new HashSet<(int, int)>();
                var zoneSnapshot = new List<TileZone>(zones);
                for (int z = 0; z < zoneSnapshot.Count; z++)
                {
                    TileZone tz = zoneSnapshot[z];
                    if (tz != null) ownedZones.Add((tz.x, tz.y));
                }

                // Double-wall ring at radius 10..11 (pop 70..119) or 11..12 (pop >= 120), up to 13 tiles
                int innerRadius = pop >= 120 ? 11 : 10;
                int outerRadius = innerRadius + 1; // 11 or 12
                int outerCornerCut = innerRadius * 2 - 1;
                int innerCornerCut = innerRadius * 2 - 4;
                TopTileType wallType = pop >= 120 ? TopTileLibrary.wall_iron : TopTileLibrary.wall_order;

                int wallsPlaced = 0;
                int roadsPlaced = 0;

                for (int dy = -outerRadius; dy <= outerRadius; dy++)
                {
                    for (int dx = -outerRadius; dx <= outerRadius; dx++)
                    {
                        int adx = Math.Abs(dx);
                        int ady = Math.Abs(dy);
                        int cheb = Math.Max(adx, ady);
                        int manh = adx + ady;

                        // Octagonal citadel footprint
                        bool insideOuter = cheb <= outerRadius && manh <= outerCornerCut;
                        if (!insideOuter) continue;

                        int tx = center.x + dx;
                        int ty = center.y + dy;
                        if (tx < 0 || ty < 0) continue;
                        if (!ownedZones.Contains((tx / 8, ty / 8))) continue;

                        WorldTile tile = world.GetTile(tx, ty);
                        if (tile == null || tile.Type == null || tile.Type.liquid || !tile.Type.ground) continue;
                        if (tile.building != null) continue;

                        bool insideCore = cheb < innerRadius && manh <= innerCornerCut;
                        bool isCardinalGateAxis = (adx <= 1) || (ady <= 1);

                        if (!insideCore)
                        {
                            // Double-thick citadel wall ring with 4 wide 3-tile cardinal gates (N/S/E/W)
                            if (isCardinalGateAxis)
                            {
                                if (tile.top_type != TopTileLibrary.road)
                                {
                                    MapAction.terraformTop(tile, TopTileLibrary.road, false);
                                    roadsPlaced++;
                                }
                            }
                            else
                            {
                                if (tile.top_type != wallType && tile.top_type != TopTileLibrary.wall_iron)
                                {
                                    MapAction.terraformTop(tile, wallType, false);
                                    wallsPlaced++;
                                }
                            }
                        }
                        else if (isCardinalGateAxis)
                        {
                            // Paved inner courtyard crossroad connecting all 4 cardinal gates
                            if (tile.top_type != TopTileLibrary.road && !InfrastructureBuilder.IsWallTile(tile.top_type))
                            {
                                MapAction.terraformTop(tile, TopTileLibrary.road, false);
                                roadsPlaced++;
                            }
                        }
                    }
                }

                if (wallsPlaced >= 12)
                {
                    CitadelCities[city.id] = new CitadelRecord
                    {
                        CityId = city.id,
                        CenterX = center.x,
                        CenterY = center.y,
                        Radius = outerRadius
                    };
                    builtNow++;
                    CitadelsBuilt++;
                    anyTerraformed = true;
                }
                else if (roadsPlaced > 0 || wallsPlaced > 0)
                {
                    anyTerraformed = true;
                }
            }

            if (anyTerraformed)
            {
                world.redrawTiles();
            }
            return builtNow;
        }

        /// <summary>
        /// Restores +6 HP (and gentle stamina/nutrition) every ~15s to defenders standing
        /// inside their own citadeled capital compound.
        /// </summary>
        public static void ApplyCitadelGarrisonBuff()
        {
            var world = MapBox.instance;
            var cfg = Main.Settings;
            if (world?.cities == null || world.units?.units_only_alive == null || cfg == null || !cfg.EnableCitadelCompounds) return;
            if (CitadelCities.Count == 0) return;

            var activeCitadels = new Dictionary<long, List<CitadelRecord>>();
            var citiesSnapshot = new List<City>(world.cities);
            var aliveCityIds = new HashSet<long>();
            int aliveCitadelMatches = 0;

            for (int i = 0; i < citiesSnapshot.Count; i++)
            {
                City c = citiesSnapshot[i];
                if (c == null || !c.isAlive()) continue;
                aliveCityIds.Add(c.id);
                if (!CitadelCities.TryGetValue(c.id, out CitadelRecord rec)) continue;
                aliveCitadelMatches++;
                Kingdom k = InfrastructureBuilder.GetCityKingdom(c);
                if (k == null || !k.isAlive()) continue;
                if (!activeCitadels.TryGetValue(k.id, out var list))
                {
                    list = new List<CitadelRecord>(1);
                    activeCitadels[k.id] = list;
                }
                list.Add(rec);
            }

            // Prune fallen citadels cleanly
            if (aliveCitadelMatches < CitadelCities.Count)
            {
                var stale = new List<long>();
                foreach (long cid in CitadelCities.Keys)
                {
                    if (!aliveCityIds.Contains(cid)) stale.Add(cid);
                }
                for (int i = 0; i < stale.Count; i++) CitadelCities.Remove(stale[i]);
            }

            if (activeCitadels.Count == 0) return;

            var aliveUnits = new List<Actor>(world.units.units_only_alive);
            for (int i = 0; i < aliveUnits.Count; i++)
            {
                Actor u = aliveUnits[i];
                if (u == null || !u.isAlive() || u.current_tile == null || u.kingdom == null) continue;
                if (u.asset != null && u.asset.is_boat) continue;

                if (!activeCitadels.TryGetValue(u.kingdom.id, out var recs)) continue;

                bool insideAnyCitadel = false;
                for (int cIdx = 0; cIdx < recs.Count; cIdx++)
                {
                    CitadelRecord rec = recs[cIdx];
                    int dx = u.current_tile.x - rec.CenterX;
                    int dy = u.current_tile.y - rec.CenterY;
                    int maxDist = rec.Radius + 2;
                    if (dx * dx + dy * dy <= maxDist * maxDist)
                    {
                        insideAnyCitadel = true;
                        break;
                    }
                }
                if (!insideAnyCitadel) continue;

                u.restoreHealth(6);
                if (RestoreStaminaMethod != null)
                {
                    try { RestoreStaminaMethod.Invoke(u, new object[] { 6 }); } catch { }
                }
                else if (AddNutritionMethod != null)
                {
                    try { AddNutritionMethod.Invoke(u, new object[] { 4 }); } catch { }
                }
                GarrisonHeals++;
            }
        }
    }
}
