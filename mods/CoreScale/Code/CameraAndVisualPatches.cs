using HarmonyLib;
using UnityEngine;

namespace CoreScale
{
    [HarmonyPatch(typeof(MoveCamera))]
    public static class CameraPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(MoveCamera.zoomToBounds))]
        private static void ZoomToBoundsPostfix(MoveCamera __instance)
        {
            ApplyZoomLimits(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(MoveCamera.resetZoom))]
        private static void ResetZoomPostfix(MoveCamera __instance)
        {
            ApplyZoomLimits(__instance);
        }

        private static void ApplyZoomLimits(MoveCamera cam)
        {
            if (cam == null || Main.Settings == null) return;

            var world = MapBox.instance;
            if (world != null && world.width > 0 && world.height > 0)
            {
                float maxDim = Mathf.Max(world.width, world.height);
                float desiredMax = Mathf.Max(cam.orthographic_size_max, maxDim * 0.5f * Main.Settings.ZoomOutMultiplier);
                cam.orthographic_size_max = desiredMax;
            }
        }
    }

    [HarmonyPatch(typeof(QualityChanger))]
    public static class QualityShadowPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(QualityChanger.shouldRenderUnitShadows))]
        private static void UnitShadowsPostfix(ref bool __result)
        {
            if (!__result || Main.Settings == null) return;
            if (Main.Settings.ForceDisableShadows || Main.CurrentLodTier >= 1)
            {
                __result = false;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(QualityChanger.shouldRenderBuildingShadows))]
        private static void BuildingShadowsPostfix(ref bool __result)
        {
            if (!__result || Main.Settings == null) return;
            if (Main.Settings.ForceDisableShadows || Main.CurrentLodTier >= 2)
            {
                __result = false;
            }
        }
    }
}
