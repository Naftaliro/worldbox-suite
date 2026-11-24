using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace CoreScale
{
    [HarmonyPatch(typeof(MoveCamera))]
    public static class CameraPatches
    {
        private static readonly FieldInfo OrthoSizeMaxField =
            AccessTools.Field(typeof(MoveCamera), "orthographic_size_max");

        [HarmonyPostfix]
        [HarmonyPatch("zoomToBounds")]
        private static void ZoomToBoundsPostfix(MoveCamera __instance)
        {
            ApplyZoomLimits(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch("resetZoom")]
        private static void ResetZoomPostfix(MoveCamera __instance)
        {
            ApplyZoomLimits(__instance);
        }

        private static void ApplyZoomLimits(MoveCamera cam)
        {
            if (cam == null || Main.Settings == null || OrthoSizeMaxField == null) return;

            if (MapBox.width > 0 && MapBox.height > 0)
            {
                float maxDim = Mathf.Max(MapBox.width, MapBox.height);
                float currentMax = OrthoSizeMaxField.GetValue(cam) is float f ? f : 150f;
                float desiredMax = Mathf.Max(currentMax, maxDim * 0.5f * Main.Settings.ZoomOutMultiplier);
                OrthoSizeMaxField.SetValue(cam, desiredMax);
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
