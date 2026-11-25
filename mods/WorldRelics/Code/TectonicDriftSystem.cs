using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace WorldRelics
{
    public enum TectonicMode
    {
        None = 0,
        PangaeaDrift = 1,
        RiftStrait = 2
    }

    /// <summary>
    /// Tick-driven continental tectonics engine supporting both physical Pangaea plate drift
    /// and empire-safe widening ocean rifts.
    /// </summary>
    [HarmonyPatch]
    public static class TectonicDriftSystem
    {
        public static TectonicMode Mode { get; private set; } = TectonicMode.None;
        public static bool Active => Mode != TectonicMode.None;
        public static bool VerticalSplit { get; private set; }
        public static int CurrentStep { get; private set; }
        public static int MaxSteps { get; private set; }

        private static int[] _faultCoords;
        private static float _stepTimer;
        private static float _naturalEventTimer;
        private static int _eventSeed;

        private static MapBox _boundWorld;
        private static int _boundWidth;
        private static int _boundHeight;

        private static TileType[] _snapMain;
        private static TopTileType[] _snapTop;

        private static readonly MethodInfo ActorSetCurrentTilePositionMethod =
            AccessTools.Method(typeof(Actor), "setCurrentTilePosition", new[] { typeof(WorldTile) });

        private static readonly MethodInfo ActorSetCurrentTileMethod =
            AccessTools.Method(typeof(Actor), "setCurrentTile", new[] { typeof(WorldTile) });

        private static readonly MethodInfo ActorCancelAllBehMethod =
            AccessTools.Method(typeof(Actor), "cancelAllBeh", Type.EmptyTypes);

        private static readonly AccessTools.FieldRef<Actor, WorldTile> ActorTileTargetRef =
            AccessTools.FieldRefAccess<Actor, WorldTile>("tile_target");

        private static readonly AccessTools.FieldRef<Actor, BaseSimObject> ActorBehTargetRef =
            AccessTools.FieldRefAccess<Actor, BaseSimObject>("beh_actor_target");

        private static readonly FieldInfo ActorDirtyCurrentTileField =
            AccessTools.Field(typeof(Actor), "dirty_current_tile");

        private static readonly MethodInfo BuildingStartDestroyMethod =
            AccessTools.Method(typeof(Building), "startDestroyBuilding", Type.EmptyTypes);

        // Guard against null tile_target or target.current_tile after tectonic unit relocation
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Actor), "checkCalibrateTargetPosition")]
        private static bool CheckCalibrateTargetPositionPrefix(Actor __instance)
        {
            if (__instance == null) return false;
            if (ActorTileTargetRef(__instance) == null) return false;
            BaseSimObject target = ActorBehTargetRef(__instance);
            if (target == null || target.current_tile == null) return false;
            if (target.isActor() && (target.a == null || target.a.current_tile == null)) return false;
            return true;
        }

        public static string StatusLabel()
        {
            if (!Active) return "Idle";
            string modeName = Mode == TectonicMode.PangaeaDrift ? "Pangaea Drift" : "Tectonic Rift";
            return $"{modeName} {CurrentStep}/{MaxSteps}";
        }

        public static string StopActiveEvent()
        {
            if (!Active) return "No tectonic event is currently active.";
            string prev = Mode == TectonicMode.PangaeaDrift ? "Pangaea Drift" : "Tectonic Rift";
            int step = CurrentStep;
            int total = MaxSteps;
            ResetState();
            return $"Stopped {prev} at step {step}/{total}.";
        }

        public static void StopSilent()
        {
            ResetState();
            _naturalEventTimer = 0f;
        }

        public static string StartPangaeaDrift()
        {
            EnsureWorldValid();
            if (Active)
            {
                return StopActiveEvent();
            }

            MapBox world = MapBox.instance;
            if (world == null || MapBox.width < 16 || MapBox.height < 16)
            {
                return "No active world available for Pangaea Drift.";
            }

            int w = MapBox.width;
            int h = MapBox.height;

            if (!ScanLandBounds(world, w, h, out int landCount, out int minX, out int maxX, out int minY, out int maxY, out int centerX, out int centerY)
                || landCount < 40)
            {
                return "Not enough landmass for Pangaea Drift (need at least 40 land tiles).";
            }

            int marginX = Math.Min(minX, w - 1 - maxX);
            int marginY = Math.Min(minY, h - 1 - maxY);
            bool verticalSplit = marginX >= marginY;
            int availMargin = verticalSplit ? marginX : marginY;

            // Keep at least 4 tiles of permanent ocean buffer around map borders
            if (availMargin < 6)
            {
                const string edgeMsg = "Not enough ocean room at map edges for Pangaea Drift (use Tectonic Rift instead).";
                ShowToast(edgeMsg);
                return edgeMsg;
            }

            int cfgSteps = Main.Settings != null ? Main.Settings.TectonicMaxDriftSteps : 14;
            int maxSteps = Math.Min(Mathf.Clamp(cfgSteps, 4, 28), availMargin - 4);
            if (maxSteps < 2)
            {
                const string edgeMsg = "Not enough ocean room at map edges for Pangaea Drift (use Tectonic Rift instead).";
                ShowToast(edgeMsg);
                return edgeMsg;
            }

            _eventSeed = UnityEngine.Random.Range(1000, 999999);
            int axisLen = verticalSplit ? h : w;
            int perpLen = verticalSplit ? w : h;
            int centerCoord = verticalSplit ? centerX : centerY;
            int landMin = verticalSplit ? minX : minY;
            int landMax = verticalSplit ? maxX : maxY;

            int safeMin = Math.Max(maxSteps + 3, Math.Min(landMin + 2, perpLen - 4 - maxSteps));
            int safeMax = Math.Min(perpLen - 4 - maxSteps, Math.Max(landMax - 2, safeMin));
            _faultCoords = BuildFaultCurve(axisLen, perpLen, centerCoord, safeMin, safeMax, _eventSeed);

            BindWorld(world, w, h);
            VerticalSplit = verticalSplit;
            MaxSteps = maxSteps;
            CurrentStep = 0;
            _stepTimer = 0f;
            Mode = TectonicMode.PangaeaDrift;

            BeginPangaea(world);

            string dirLabel = verticalSplit ? "West/East" : "South/North";
            string speedLabel = IsSlowGeological() ? "Geological" : "Cinematic";
            string msg = $"Pangaea Drift started ({dirLabel}, {maxSteps} steps, {speedLabel}).";
            ShowToast(msg);
            return msg;
        }

        public static string StartTectonicRift()
        {
            EnsureWorldValid();
            if (Active)
            {
                return StopActiveEvent();
            }

            MapBox world = MapBox.instance;
            if (world == null || MapBox.width < 16 || MapBox.height < 16)
            {
                return "No active world available for Tectonic Rift.";
            }

            int w = MapBox.width;
            int h = MapBox.height;

            if (!ScanLandBounds(world, w, h, out int landCount, out int minX, out int maxX, out int minY, out int maxY, out int centerX, out int centerY)
                || landCount < 40)
            {
                return "Not enough landmass for Tectonic Rift (need at least 40 land tiles).";
            }

            int spanX = maxX - minX;
            int spanY = maxY - minY;
            bool verticalSplit = spanX >= spanY;

            int cfgSteps = Main.Settings != null ? Main.Settings.TectonicMaxDriftSteps : 14;
            int maxSteps = Mathf.Clamp(cfgSteps, 5, 22);

            _eventSeed = UnityEngine.Random.Range(1000, 999999);
            int axisLen = verticalSplit ? h : w;
            int perpLen = verticalSplit ? w : h;
            int centerCoord = verticalSplit ? centerX : centerY;
            int safeMin = Math.Min(4, perpLen - 2);
            int safeMax = Math.Max(safeMin, perpLen - 5);

            _faultCoords = BuildFaultCurve(axisLen, perpLen, centerCoord, safeMin, safeMax, _eventSeed);

            BindWorld(world, w, h);
            VerticalSplit = verticalSplit;
            MaxSteps = maxSteps;
            CurrentStep = 0;
            _stepTimer = 0f;
            Mode = TectonicMode.RiftStrait;

            TriggerShake(world, 0.35f, 0.02f, 1.8f, true, true);

            string dirLabel = verticalSplit ? "North-South" : "West-East";
            string speedLabel = IsSlowGeological() ? "Geological" : "Cinematic";
            string msg = $"Empire-Safe Tectonic Rift opening ({dirLabel}, {maxSteps} steps, {speedLabel}).";
            ShowToast(msg);
            return msg;
        }

        public static void Tick(float dt)
        {
            if (dt <= 0f) return;
            EnsureWorldValid();

            if (Active)
            {
                float interval = IsSlowGeological() ? 6.0f : 0.9f;
                _stepTimer += dt;
                if (_stepTimer >= interval)
                {
                    _stepTimer = 0f;
                    try
                    {
                        if (Mode == TectonicMode.PangaeaDrift)
                        {
                            StepPangaeaDrift();
                        }
                        else if (Mode == TectonicMode.RiftStrait)
                        {
                            StepTectonicRift();
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[WorldRelics] Tectonic step failed; cancelling cleanly: {e}");
                        ResetState();
                    }
                }
                return;
            }

            if (Main.Settings != null && Main.Settings.EnableNaturalTectonicEvents)
            {
                _naturalEventTimer += dt;
                if (_naturalEventTimer >= 180f)
                {
                    _naturalEventTimer = 0f;
                    if (UnityEngine.Random.value < 0.08f)
                    {
                        StartTectonicRift();
                    }
                }
            }
            else
            {
                _naturalEventTimer = 0f;
            }
        }

        private static void BeginPangaea(MapBox world)
        {
            TriggerShake(world, 0.45f, 0.02f, 2.2f, true, true);
            CollapseAllBuildings(world);
        }

        private static void CollapseAllBuildings(MapBox world)
        {
            if (world?.buildings == null) return;
            var list = world.buildings.getSimpleList();
            if (list == null || list.Count == 0) return;

            var snapshot = new List<Building>(list);
            for (int i = 0; i < snapshot.Count; i++)
            {
                DestroyBuildingSafe(snapshot[i]);
            }
        }

        private static void StepPangaeaDrift()
        {
            MapBox world = MapBox.instance;
            if (!IsBoundWorldValid(world))
            {
                ResetState();
                return;
            }

            int w = MapBox.width;
            int h = MapBox.height;
            int gapHalf = CurrentStep + 1;

            // Keep TileZone building pointers clean if any building appeared mid-drift (e.g. Geological mode)
            CollapseAllBuildings(world);

            bool anyChanged = false;
            int snapLen = VerticalSplit ? w : h;
            EnsureSnapBuffers(snapLen);

            if (VerticalSplit)
            {
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        WorldTile t = world.GetTile(x, y);
                        _snapMain[x] = t != null ? t.main_type : null;
                        _snapTop[x] = t != null ? t.top_type : null;
                    }

                    int fx = _faultCoords[y];
                    int westMax = Math.Min(w - 3, fx - gapHalf);
                    for (int x = 2; x <= westMax; x++)
                    {
                        WorldTile dst = world.GetTile(x, y);
                        if (ApplyTileCopy(dst, _snapMain[x + 1], _snapTop[x + 1], flash: false))
                        {
                            anyChanged = true;
                        }
                    }

                    int eastMin = Math.Max(2, fx + gapHalf);
                    for (int x = w - 3; x >= eastMin; x--)
                    {
                        WorldTile dst = world.GetTile(x, y);
                        if (ApplyTileCopy(dst, _snapMain[x - 1], _snapTop[x - 1], flash: false))
                        {
                            anyChanged = true;
                        }
                    }

                    int seamL = Math.Max(2, fx - gapHalf + 1);
                    int seamR = Math.Min(w - 3, fx + gapHalf - 1);
                    for (int x = seamL; x <= seamR; x++)
                    {
                        WorldTile dst = world.GetTile(x, y);
                        int distFromEdge = Math.Min(x - seamL, seamR - x);
                        TileType seamMain = PickPangaeaSeamTile(x, y, fx, gapHalf, distFromEdge);
                        if (ApplyTileCopy(dst, seamMain, null, flash: distFromEdge == 0))
                        {
                            anyChanged = true;
                        }
                    }
                }
            }
            else
            {
                for (int x = 0; x < w; x++)
                {
                    for (int y = 0; y < h; y++)
                    {
                        WorldTile t = world.GetTile(x, y);
                        _snapMain[y] = t != null ? t.main_type : null;
                        _snapTop[y] = t != null ? t.top_type : null;
                    }

                    int fy = _faultCoords[x];
                    int southMax = Math.Min(h - 3, fy - gapHalf);
                    for (int y = 2; y <= southMax; y++)
                    {
                        WorldTile dst = world.GetTile(x, y);
                        if (ApplyTileCopy(dst, _snapMain[y + 1], _snapTop[y + 1], flash: false))
                        {
                            anyChanged = true;
                        }
                    }

                    int northMin = Math.Max(2, fy + gapHalf);
                    for (int y = h - 3; y >= northMin; y--)
                    {
                        WorldTile dst = world.GetTile(x, y);
                        if (ApplyTileCopy(dst, _snapMain[y - 1], _snapTop[y - 1], flash: false))
                        {
                            anyChanged = true;
                        }
                    }

                    int seamB = Math.Max(2, fy - gapHalf + 1);
                    int seamT = Math.Min(h - 3, fy + gapHalf - 1);
                    for (int y = seamB; y <= seamT; y++)
                    {
                        WorldTile dst = world.GetTile(x, y);
                        int distFromEdge = Math.Min(y - seamB, seamT - y);
                        TileType seamMain = PickPangaeaSeamTile(x, y, fy, gapHalf, distFromEdge);
                        if (ApplyTileCopy(dst, seamMain, null, flash: distFromEdge == 0))
                        {
                            anyChanged = true;
                        }
                    }
                }
            }

            ShiftUnitsWithPlates(world, w, h);

            if (anyChanged)
            {
                world.redrawTiles();
            }

            TriggerShake(world, 0.22f, 0.02f, 1.15f, VerticalSplit, !VerticalSplit);

            CurrentStep++;
            if (CurrentStep >= MaxSteps)
            {
                int oceanTiles = MaxSteps * 2;
                ResetState();
                ShowToast($"Pangaea Drift complete: continent split across a {oceanTiles}-tile ocean!");
            }
        }

        private static TileType PickPangaeaSeamTile(int x, int y, int faultCoord, int gapHalf, int distFromEdge)
        {
            int axisPos = VerticalSplit ? x : y;
            if (gapHalf >= 4 && distFromEdge >= 2 && Math.Abs(axisPos - faultCoord) <= 1)
            {
                float h = Hash01((x * 73856093) ^ (y * 19349663) ^ _eventSeed);
                if (h < 0.007f) return TileLibrary.hills;
                if (h < 0.020f) return TileLibrary.sand;
            }

            if (distFromEdge <= 0) return TileLibrary.shallow_waters;
            if (distFromEdge <= 2) return TileLibrary.close_ocean;
            return TileLibrary.deep_ocean;
        }

        private static void ShiftUnitsWithPlates(MapBox world, int w, int h)
        {
            var alive = world.units?.units_only_alive;
            if (alive == null || alive.Count == 0) return;

            var snapshot = new List<Actor>(alive);
            for (int i = 0; i < snapshot.Count; i++)
            {
                Actor unit = snapshot[i];
                if (!IsMovableLandUnit(unit)) continue;

                int ux = unit.current_tile.x;
                int uy = unit.current_tile.y;

                if (VerticalSplit)
                {
                    int fx = _faultCoords[Mathf.Clamp(uy, 0, h - 1)];
                    int dir = ux <= fx ? -1 : 1;
                    int nx = Mathf.Clamp(ux + dir, 2, w - 3);
                    WorldTile target = world.GetTile(nx, uy);
                    if (target != null && target.Type != null && target.Type.liquid
                        && (unit.asset == null || !unit.asset.force_ocean_creature))
                    {
                        WorldTile dry = FindNearbyDryTile(world, nx, uy, dir, 0, w, h, 4);
                        if (dry != null) target = dry;
                    }
                    if (target != null) MoveActorToTile(unit, target);
                }
                else
                {
                    int fy = _faultCoords[Mathf.Clamp(ux, 0, w - 1)];
                    int dir = uy <= fy ? -1 : 1;
                    int ny = Mathf.Clamp(uy + dir, 2, h - 3);
                    WorldTile target = world.GetTile(ux, ny);
                    if (target != null && target.Type != null && target.Type.liquid
                        && (unit.asset == null || !unit.asset.force_ocean_creature))
                    {
                        WorldTile dry = FindNearbyDryTile(world, ux, ny, 0, dir, w, h, 4);
                        if (dry != null) target = dry;
                    }
                    if (target != null) MoveActorToTile(unit, target);
                }
            }
        }

        private static void StepTectonicRift()
        {
            MapBox world = MapBox.instance;
            if (!IsBoundWorldValid(world))
            {
                ResetState();
                return;
            }

            int w = MapBox.width;
            int h = MapBox.height;
            int r = CurrentStep + 1;

            // Evacuate living units standing inside the newly opening rift band first
            EvacuateRiftUnits(world, w, h, r);

            bool anyChanged = false;
            int axisLen = VerticalSplit ? h : w;
            int perpLen = VerticalSplit ? w : h;

            for (int along = 0; along < axisLen; along++)
            {
                int fc = _faultCoords[along];
                int dMin = -(r + 1);
                int dMax = r + 1;

                for (int d = dMin; d <= dMax; d++)
                {
                    int p = fc + d;
                    if (p < 1 || p >= perpLen - 1) continue;

                    WorldTile tile = VerticalSplit ? world.GetTile(p, along) : world.GetTile(along, p);
                    if (tile == null || tile.Type == null) continue;

                    int absD = Math.Abs(d);

                    // Soften immediate shoreline to sand for plain soil/grass while preserving cliffs, walls & buildings
                    if (absD == r + 1)
                    {
                        if (tile.building != null) continue;
                        if (InfrastructureBuilder.IsWallTile(tile.top_type)) continue;
                        if (tile.main_type == TileLibrary.soil_low || tile.main_type == TileLibrary.soil_high)
                        {
                            if (ApplyTileCopy(tile, TileLibrary.sand, null, flash: false))
                            {
                                anyChanged = true;
                            }
                        }
                        continue;
                    }

                    // Collapse ONLY buildings standing directly on flooded rift tiles
                    if (tile.building != null)
                    {
                        DestroyBuildingSafe(tile.building);
                    }

                    int depthFromEdge = r - absD;
                    TileType targetWater;
                    if (depthFromEdge <= 0) targetWater = TileLibrary.shallow_waters;
                    else if (depthFromEdge <= 2) targetWater = TileLibrary.close_ocean;
                    else targetWater = TileLibrary.deep_ocean;

                    if (WaterDepthRank(targetWater) > WaterDepthRank(tile.main_type))
                    {
                        if (ApplyTileCopy(tile, targetWater, null, flash: depthFromEdge == 0))
                        {
                            anyChanged = true;
                        }
                    }
                }
            }

            if (anyChanged)
            {
                world.redrawTiles();
            }

            TriggerShake(world, 0.20f, 0.02f, 1.0f, true, true);

            CurrentStep++;
            if (CurrentStep >= MaxSteps)
            {
                int straitWidth = MaxSteps * 2 + 1;
                ResetState();
                ShowToast($"Tectonic Rift complete: a {straitWidth}-tile ocean strait now divides the realm!");
            }
        }

        private static void EvacuateRiftUnits(MapBox world, int w, int h, int radius)
        {
            var alive = world.units?.units_only_alive;
            if (alive == null || alive.Count == 0) return;

            var snapshot = new List<Actor>(alive);
            for (int i = 0; i < snapshot.Count; i++)
            {
                Actor unit = snapshot[i];
                if (!IsMovableLandUnit(unit)) continue;
                if (unit.asset != null && unit.asset.force_ocean_creature) continue;

                int ux = unit.current_tile.x;
                int uy = unit.current_tile.y;

                if (VerticalSplit)
                {
                    int fx = _faultCoords[Mathf.Clamp(uy, 0, h - 1)];
                    int signedD = ux - fx;
                    if (Math.Abs(signedD) > radius) continue;

                    int dir = signedD < 0 ? -1 : (signedD > 0 ? 1 : (((ux + uy) & 1) == 0 ? -1 : 1));
                    int startX = Mathf.Clamp(fx + dir * (radius + 2), 1, w - 2);
                    WorldTile safe = FindNearbyDryTile(world, startX, uy, dir, 0, w, h, 8);
                    if (safe != null) MoveActorToTile(unit, safe);
                }
                else
                {
                    int fy = _faultCoords[Mathf.Clamp(ux, 0, w - 1)];
                    int signedD = uy - fy;
                    if (Math.Abs(signedD) > radius) continue;

                    int dir = signedD < 0 ? -1 : (signedD > 0 ? 1 : (((ux + uy) & 1) == 0 ? -1 : 1));
                    int startY = Mathf.Clamp(fy + dir * (radius + 2), 1, h - 2);
                    WorldTile safe = FindNearbyDryTile(world, ux, startY, 0, dir, w, h, 8);
                    if (safe != null) MoveActorToTile(unit, safe);
                }
            }
        }

        private static WorldTile FindNearbyDryTile(MapBox world, int startX, int startY, int stepX, int stepY, int w, int h, int maxReach)
        {
            for (int dist = 0; dist <= maxReach; dist++)
            {
                int tx = Mathf.Clamp(startX + stepX * dist, 1, w - 2);
                int ty = Mathf.Clamp(startY + stepY * dist, 1, h - 2);

                WorldTile direct = world.GetTile(tx, ty);
                if (IsDryTile(direct)) return direct;

                // Small perpendicular wobble to sidestep small coastal inlets
                for (int side = -1; side <= 1; side += 2)
                {
                    int sx = Mathf.Clamp(tx + (stepY != 0 ? side : 0), 1, w - 2);
                    int sy = Mathf.Clamp(ty + (stepX != 0 ? side : 0), 1, h - 2);
                    WorldTile alt = world.GetTile(sx, sy);
                    if (IsDryTile(alt)) return alt;
                }
            }
            return null;
        }

        private static bool IsDryTile(WorldTile tile)
        {
            return tile != null && tile.Type != null && !tile.Type.liquid;
        }

        private static bool IsMovableLandUnit(Actor unit)
        {
            if (unit == null || !unit.isAlive() || unit.current_tile == null) return false;
            if (unit.asset != null && unit.asset.is_boat) return false;
            if (unit.isInsideSomething()) return false;
            return true;
        }

        private static void MoveActorToTile(Actor actor, WorldTile target)
        {
            if (actor == null || target == null) return;
            try
            {
                // Cancel active AI task/path cleanly before moving so calibrate_target_position never reads a null tile_target
                ActorCancelAllBehMethod?.Invoke(actor, null);

                if (ActorSetCurrentTilePositionMethod != null)
                {
                    ActorSetCurrentTilePositionMethod.Invoke(actor, new object[] { target });
                }
                else
                {
                    ActorSetCurrentTileMethod?.Invoke(actor, new object[] { target });
                    actor.current_position = new Vector2(target.x + 0.5f, target.y + 0.5f);
                }

                ActorDirtyCurrentTileField?.SetValue(actor, true);
            }
            catch { }
        }

        private static void DestroyBuildingSafe(Building building)
        {
            if (building == null) return;
            try
            {
                if (!building.isAlive()) return;
                BuildingStartDestroyMethod?.Invoke(building, null);
            }
            catch { }
        }

        private static bool ApplyTileCopy(WorldTile dst, TileType srcMain, TopTileType srcTop, bool flash)
        {
            if (dst == null || srcMain == null) return false;

            bool mainChanged = dst.main_type != srcMain;
            bool topChanged = dst.top_type != srcTop;
            if (!mainChanged && !topChanged) return false;

            TerraformOptions opts = flash
                ? (TerraformLibrary.flash ?? TerraformLibrary.nothing)
                : (TerraformLibrary.nothing ?? TerraformLibrary.flash);

            if (mainChanged || (srcTop == null && dst.top_type != null))
            {
                if (opts != null)
                {
                    MapAction.terraformMain(dst, srcMain, opts, false);
                }
                else
                {
                    MapAction.terraformMain(dst, srcMain, false);
                }
            }

            if (srcTop != null && dst.top_type != srcTop)
            {
                if (opts != null)
                {
                    MapAction.terraformTop(dst, srcTop, opts, false);
                }
                else
                {
                    MapAction.terraformTop(dst, srcTop, false);
                }
            }

            return true;
        }

        private static int WaterDepthRank(TileType tileType)
        {
            if (tileType == null) return 0;
            if (tileType == TileLibrary.deep_ocean) return 3;
            if (tileType == TileLibrary.close_ocean) return 2;
            if (tileType == TileLibrary.shallow_waters) return 1;
            return 0;
        }

        private static bool ScanLandBounds(
            MapBox world,
            int w,
            int h,
            out int landCount,
            out int minX,
            out int maxX,
            out int minY,
            out int maxY,
            out int centerX,
            out int centerY)
        {
            landCount = 0;
            minX = w;
            maxX = -1;
            minY = h;
            maxY = -1;
            long sumX = 0;
            long sumY = 0;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    WorldTile tile = world.GetTile(x, y);
                    if (tile == null || tile.Type == null || tile.Type.liquid) continue;

                    landCount++;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                    sumX += x;
                    sumY += y;
                }
            }

            if (landCount <= 0)
            {
                centerX = w / 2;
                centerY = h / 2;
                return false;
            }

            centerX = (int)(sumX / landCount);
            centerY = (int)(sumY / landCount);
            return true;
        }

        private static int[] BuildFaultCurve(int axisLen, int perpLen, int centerCoord, int minCoord, int maxCoord, int seed)
        {
            int[] curve = new int[axisLen];
            float amp = Mathf.Clamp(perpLen * 0.065f, 4f, 18f);
            float p1 = Hash01(seed * 17 + 3) * Mathf.PI * 2f;
            float p2 = Hash01(seed * 31 + 11) * Mathf.PI * 2f;
            float p3 = Hash01(seed * 53 + 29) * Mathf.PI * 2f;

            float invLen = axisLen > 1 ? 1f / (axisLen - 1) : 1f;
            for (int i = 0; i < axisLen; i++)
            {
                float t = i * invLen;
                float wave =
                    Mathf.Sin(t * 2.15f * Mathf.PI + p1) * amp * 0.55f +
                    Mathf.Sin(t * 5.65f * Mathf.PI + p2) * amp * 0.28f +
                    Mathf.Sin(t * 13.4f * Mathf.PI + p3) * amp * 0.12f;

                float jagA = Hash01((i / 3) * 19349663 ^ seed) - 0.5f;
                float jagB = Hash01((i / 3 + 1) * 19349663 ^ seed) - 0.5f;
                float frac = (i % 3) / 3f;
                float jag = Mathf.Lerp(jagA, jagB, frac) * 3.2f;

                int coord = Mathf.RoundToInt(centerCoord + wave + jag);
                curve[i] = Mathf.Clamp(coord, minCoord, maxCoord);
            }

            return curve;
        }

        private static float Hash01(int n)
        {
            unchecked
            {
                uint x = (uint)n;
                x ^= x >> 16;
                x *= 0x7FEB352Du;
                x ^= x >> 15;
                x *= 0x846CA68Bu;
                x ^= x >> 16;
                return (x & 0x00FFFFFFu) / 16777216f;
            }
        }

        private static bool IsSlowGeological()
        {
            return Main.Settings != null && Main.Settings.TectonicSlowGeological;
        }

        private static void BindWorld(MapBox world, int w, int h)
        {
            _boundWorld = world;
            _boundWidth = w;
            _boundHeight = h;
        }

        private static bool IsBoundWorldValid(MapBox world)
        {
            if (world == null || world != _boundWorld) return false;
            if (MapBox.width != _boundWidth || MapBox.height != _boundHeight) return false;
            if (_faultCoords == null) return false;
            int expectedAxis = VerticalSplit ? _boundHeight : _boundWidth;
            return _faultCoords.Length == expectedAxis;
        }

        private static void EnsureWorldValid()
        {
            if (!Active) return;
            if (!IsBoundWorldValid(MapBox.instance))
            {
                ResetState();
            }
        }

        private static void EnsureSnapBuffers(int size)
        {
            if (_snapMain == null || _snapMain.Length < size)
            {
                _snapMain = new TileType[size];
                _snapTop = new TopTileType[size];
            }
        }

        private static void ResetState()
        {
            Mode = TectonicMode.None;
            CurrentStep = 0;
            MaxSteps = 0;
            _stepTimer = 0f;
            _faultCoords = null;
            _boundWorld = null;
            _boundWidth = 0;
            _boundHeight = 0;
        }

        private static void TriggerShake(MapBox world, float duration, float interval, float intensity, bool shakeX, bool shakeY)
        {
            if (world == null) return;
            try
            {
                world.startShake(duration, interval, intensity, shakeX, shakeY);
            }
            catch { }
        }

        private static void ShowToast(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            try
            {
                WorldTip.showNowTop(message, false);
            }
            catch
            {
                Debug.Log($"[WorldRelics] {message}");
            }
        }
    }
}
