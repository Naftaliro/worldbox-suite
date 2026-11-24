using System;
using HarmonyLib;
using UnityEngine;

namespace CoreScale
{
    [HarmonyPatch(typeof(MapBox))]
    public static class MapBoxParallelPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(MapBox.Awake))]
        private static void AwakePostfix()
        {
            SimulationLodPatches.ApplyParallelSettings();
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(MapBox.finishingUpLoading))]
        private static void FinishLoadingPostfix()
        {
            SimulationLodPatches.ApplyParallelSettings();
        }
    }

    [HarmonyPatch(typeof(BatchActors))]
    public static class SimulationLodPatches
    {
        public static void ApplyParallelSettings()
        {
            if (Main.Settings == null || !Main.Settings.UnlockParallelism) return;

            Config.parallel_jobs_updater = true;
            Config.parallel_chunk_manager = true;

            var world = MapBox.instance;
            if (world != null && world.parallel_options != null)
            {
                world.parallel_options.MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount - 1);
            }
        }

        // Skip micro-sprite frame updates when zoomed far out into strategic/low-res view
        [HarmonyPrefix]
        [HarmonyPatch(nameof(BatchActors.u3_spriteAnimation))]
        private static bool SpriteAnimationPrefix()
        {
            if (Main.Settings == null || !Main.Settings.SkipAnimationsInStrategicZoom) return true;
            var qc = MapBox.instance?.quality_changer;
            if (qc != null && qc.isLowRes())
            {
                return false;
            }
            return true;
        }

        // Throttle ambient cosmetic checks at LOD Tier 1+
        [HarmonyPrefix]
        [HarmonyPatch(nameof(BatchActors.updateHovering))]
        private static bool HoveringPrefix(BatchActors __instance)
        {
            if (Main.CurrentLodTier < 1) return true;
            int stride = Main.CurrentLodTier >= 2 ? 4 : 2;
            return ((Time.frameCount + __instance.batch_id) % stride) == 0;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(BatchActors.updatePollinating))]
        private static bool PollinatingPrefix(BatchActors __instance)
        {
            if (Main.CurrentLodTier < 1) return true;
            int stride = Main.CurrentLodTier >= 2 ? 4 : 2;
            return ((Time.frameCount + __instance.batch_id) % stride) == 0;
        }

        // Time-slice heavy AI decisions across batches at LOD Tier 2+ (smoothly staggered by batch_id)
        [HarmonyPrefix]
        [HarmonyPatch(nameof(BatchActors.b6_0_updateDecision))]
        private static bool UpdateDecisionPrefix(BatchActors __instance)
        {
            if (Main.CurrentLodTier < 2) return true;
            int stride = Main.CurrentLodTier >= 3 ? 4 : 2;
            return ((Time.frameCount + __instance.batch_id) % stride) == 0;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(BatchActors.b6_updateAI))]
        private static bool UpdateAiPrefix(BatchActors __instance)
        {
            if (Main.CurrentLodTier < 3) return true;
            return ((Time.frameCount + __instance.batch_id) % 2) == 0;
        }
    }
}
