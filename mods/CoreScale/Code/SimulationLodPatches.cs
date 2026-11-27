using System;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using UnityEngine;

namespace CoreScale
{
    [HarmonyPatch(typeof(MapBox))]
    public static class MapBoxParallelPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch("Awake")]
        private static void AwakePostfix()
        {
            try { SimulationLodPatches.ApplyParallelSettings(); } catch { }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(MapBox.finishingUpLoading))]
        private static void FinishLoadingPostfix()
        {
            try { SimulationLodPatches.ApplyParallelSettings(); } catch { }
        }
    }

    [HarmonyPatch(typeof(BatchActors))]
    public static class SimulationLodPatches
    {
        private static readonly FieldInfo ParallelOptionsField =
            AccessTools.Field(typeof(MapBox), "parallel_options");

        private static readonly FieldInfo QualityChangerField =
            AccessTools.Field(typeof(MapBox), "quality_changer");

        private static QualityChanger _cachedQualityChanger;
        private static MapBox _cachedWorld;

        private static QualityChanger GetQualityChanger(MapBox world)
        {
            if (world == null || QualityChangerField == null) return null;
            return QualityChangerField.GetValue(world) as QualityChanger;
        }

        public static bool IsStrategicZoom()
        {
            var world = MapBox.instance;
            if (world == null) return false;
            if (!ReferenceEquals(world, _cachedWorld) || _cachedQualityChanger == null)
            {
                _cachedWorld = world;
                _cachedQualityChanger = GetQualityChanger(world);
            }
            return _cachedQualityChanger != null && _cachedQualityChanger.isLowRes();
        }

        public static void ApplyParallelSettings()
        {
            if (Main.Settings == null || !Main.Settings.UnlockParallelism) return;

            Config.parallel_jobs_updater = true;
            // Keep parallel_chunk_manager off: enabling it races MapRegion rebuilds against RegionPathFinder
            Config.parallel_chunk_manager = false;

            // Tune Unity incremental GC slice to 2ms so Mono avoids big stop-the-world hitches
            try
            {
                Type gcType = AccessTools.TypeByName("UnityEngine.Scripting.GarbageCollector");
                PropertyInfo sliceProp = gcType?.GetProperty("incrementalTimeSliceNanoseconds", BindingFlags.Public | BindingFlags.Static);
                if (sliceProp != null && sliceProp.CanWrite)
                {
                    sliceProp.SetValue(null, 2000000UL);
                }
            }
            catch { }

            var world = MapBox.instance;
            if (world != null)
            {
                _cachedWorld = world;
                _cachedQualityChanger = GetQualityChanger(world);
                if (ParallelOptionsField?.GetValue(world) is ParallelOptions opts)
                {
                    opts.MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount - 1);
                }
            }
        }

        // Skip micro-sprite frame updates when zoomed far out into strategic/low-res view (zero reflection)
        [HarmonyPrefix]
        [HarmonyPatch("u3_spriteAnimation")]
        private static bool SpriteAnimationPrefix(BatchActors __instance)
        {
            if (__instance == null || Main.Settings == null) return true;
            if (Main.Settings.SkipAnimationsInStrategicZoom && IsStrategicZoom())
            {
                return false;
            }
            if (Main.Settings.TurboMode || Main.EffectiveLodTier >= 2)
            {
                return ((Main.CurrentFrameCount + __instance.batch_id) & 1) == 0;
            }
            return true;
        }

        // Skip purely cosmetic hit wobble in strategic zoom or under load
        [HarmonyPrefix]
        [HarmonyPatch("updateShake")]
        private static bool ActorShakePrefix()
        {
            if (Main.Settings == null) return true;
            if (Main.Settings.TurboMode || Main.EffectiveLodTier >= 1) return false;
            if (Main.Settings.SkipAnimationsInStrategicZoom && IsStrategicZoom()) return false;
            return true;
        }

        // Throttle ambient cosmetic checks at LOD Tier 1+
        [HarmonyPrefix]
        [HarmonyPatch("updateHovering")]
        private static bool HoveringPrefix(BatchActors __instance)
        {
            if (__instance == null) return true;
            int tier = Main.EffectiveLodTier;
            if (tier < 1) return true;
            int stride = tier >= 2 ? 4 : 2;
            return ((Main.CurrentFrameCount + __instance.batch_id) % stride) == 0;
        }

        [HarmonyPrefix]
        [HarmonyPatch("updatePollinating")]
        private static bool PollinatingPrefix(BatchActors __instance)
        {
            if (__instance == null) return true;
            int tier = Main.EffectiveLodTier;
            if (tier < 1) return true;
            int stride = tier >= 2 ? 4 : 2;
            return ((Main.CurrentFrameCount + __instance.batch_id) % stride) == 0;
        }

        // Stagger expensive target-search scans in mega-wars
        [HarmonyPrefix]
        [HarmonyPatch("b3_findEnemyTarget")]
        private static bool FindEnemyTargetPrefix(BatchActors __instance)
        {
            if (__instance == null) return true;
            int tier = Main.EffectiveLodTier;
            if (tier < 2 && (Main.Settings == null || !Main.Settings.TurboMode)) return true;
            return ((Main.CurrentFrameCount + __instance.batch_id) & 1) == 0;
        }

        // Time-slice heavy AI decisions across batches at LOD Tier 2+ (smoothly staggered by batch_id)
        [HarmonyPrefix]
        [HarmonyPatch("b6_0_updateDecision")]
        private static bool UpdateDecisionPrefix(BatchActors __instance)
        {
            if (__instance == null) return true;
            int tier = Main.EffectiveLodTier;
            if (tier < 2) return true;
            int stride = tier >= 3 ? 4 : 2;
            return ((Main.CurrentFrameCount + __instance.batch_id) % stride) == 0;
        }

        [HarmonyPrefix]
        [HarmonyPatch("b6_updateAI")]
        private static bool UpdateAiPrefix(BatchActors __instance)
        {
            if (__instance == null || Main.EffectiveLodTier < 3) return true;
            return ((Main.CurrentFrameCount + __instance.batch_id) & 1) == 0;
        }
    }

    // Cull building cosmetic shakes & stagger massive flora spread loops on giant maps
    [HarmonyPatch(typeof(BatchBuildings))]
    public static class BuildingLodPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch("updateShake")]
        private static bool BuildingShakePrefix()
        {
            if (Main.Settings == null) return true;
            return !Main.Settings.TurboMode && Main.EffectiveLodTier < 1 && !(Main.Settings.SkipAnimationsInStrategicZoom && SimulationLodPatches.IsStrategicZoom());
        }

        [HarmonyPrefix]
        [HarmonyPatch("updateResourceShaker")]
        private static bool ResourceShakePrefix()
        {
            if (Main.Settings == null) return true;
            return !Main.Settings.TurboMode && Main.EffectiveLodTier < 1 && !(Main.Settings.SkipAnimationsInStrategicZoom && SimulationLodPatches.IsStrategicZoom());
        }

        [HarmonyPrefix]
        [HarmonyPatch("updateSpreadTrees")]
        private static bool SpreadTreesPrefix(BatchBuildings __instance)
        {
            if (__instance == null) return true;
            int tier = Main.EffectiveLodTier;
            if (tier < 1 && (Main.Settings == null || !Main.Settings.TurboMode)) return true;
            int stride = tier >= 2 || (Main.Settings != null && Main.Settings.TurboMode) ? 4 : 2;
            return ((Main.CurrentFrameCount + __instance.batch_id) % stride) == 0;
        }

        [HarmonyPrefix]
        [HarmonyPatch("updateSpreadPlants")]
        private static bool SpreadPlantsPrefix(BatchBuildings __instance)
        {
            if (__instance == null) return true;
            int tier = Main.EffectiveLodTier;
            if (tier < 1 && (Main.Settings == null || !Main.Settings.TurboMode)) return true;
            int stride = tier >= 2 || (Main.Settings != null && Main.Settings.TurboMode) ? 4 : 2;
            return ((Main.CurrentFrameCount + __instance.batch_id) % stride) == 0;
        }

        [HarmonyPrefix]
        [HarmonyPatch("updateSpreadFungi")]
        private static bool SpreadFungiPrefix(BatchBuildings __instance)
        {
            if (__instance == null) return true;
            int tier = Main.EffectiveLodTier;
            if (tier < 1 && (Main.Settings == null || !Main.Settings.TurboMode)) return true;
            int stride = tier >= 2 || (Main.Settings != null && Main.Settings.TurboMode) ? 4 : 2;
            return ((Main.CurrentFrameCount + __instance.batch_id) % stride) == 0;
        }
    }

    // Prevent CPU->GPU texture re-uploads from tile flash spam in strategic view / Turbo Mode
    [HarmonyPatch(typeof(PixelFlashEffects))]
    public static class PixelFlashThrottlePatch
    {
        [HarmonyPrefix]
        [HarmonyPatch("flashPixel")]
        private static bool FlashPixelPrefix()
        {
            if (Main.Settings == null || !Main.Settings.ThrottleParticlesAndFlashes) return true;
            if (Main.Settings.TurboMode || Main.EffectiveLodTier >= 2) return false;
            if (Main.Settings.SkipAnimationsInStrategicZoom && SimulationLodPatches.IsStrategicZoom()) return false;
            return true;
        }
    }

    // Cap cosmetic war-time particle bursts & status sparkles so big battles stay smooth
    [HarmonyPatch(typeof(EffectsLibrary))]
    public static class EffectsThrottlePatches
    {
        private static int _lastFrame = -1;
        private static int _frameCount = 0;

        [HarmonyPrefix]
        [HarmonyPatch("canShowSlashEffect")]
        private static bool CanShowSlashPrefix(ref bool __result)
        {
            if (Main.Settings == null || !Main.Settings.ThrottleParticlesAndFlashes) return true;
            if (Main.Settings.TurboMode || Main.EffectiveLodTier >= 1 || (Main.Settings.SkipAnimationsInStrategicZoom && SimulationLodPatches.IsStrategicZoom()))
            {
                __result = false;
                return false;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch("spawnStatusParticle")]
        private static bool StatusParticlePrefix(ref BaseEffect __result)
        {
            if (Main.Settings == null || !Main.Settings.ThrottleParticlesAndFlashes) return true;
            if (Main.Settings.TurboMode || Main.EffectiveLodTier >= 1 || (Main.Settings.SkipAnimationsInStrategicZoom && SimulationLodPatches.IsStrategicZoom()))
            {
                __result = null;
                return false;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch("spawnAt", new[] { typeof(string), typeof(Vector2), typeof(float) })]
        private static bool SpawnAt2DPrefix(ref BaseEffect __result)
        {
            if (Main.Settings == null || !Main.Settings.ThrottleParticlesAndFlashes) return true;
            int tier = Main.EffectiveLodTier;
            bool turbo = Main.Settings.TurboMode;
            if (tier < 1 && !turbo) return true;

            int frame = Time.frameCount;
            if (frame != _lastFrame)
            {
                _lastFrame = frame;
                _frameCount = 0;
            }
            int budget = turbo ? 6 : (tier >= 2 ? 12 : 24);
            if (_frameCount >= budget)
            {
                __result = null;
                return false;
            }
            _frameCount++;
            return true;
        }
    }
}

