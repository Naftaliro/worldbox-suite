using HarmonyLib;
using UnityEngine;

namespace VisualPolish
{
    [HarmonyPatch(typeof(QualityChanger))]
    public static class ExtendedHdZoomPatches
    {
        private const float AlwaysHdThreshold = 25f;

        [HarmonyPostfix]
        [HarmonyPatch(nameof(QualityChanger.getZoomRateBoundLow))]
        private static void GetZoomRateBoundLowPostfix(ref float __result)
        {
            try
            {
                var settings = Main.Settings;
                if (settings == null || !settings.ExtendedHdZoom) return;

                float mult = Mathf.Max(1f, settings.HdZoomMultiplier);
                if (mult > 1.001f)
                {
                    __result *= mult;
                }
            }
            catch { }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(QualityChanger.isLowRes))]
        private static void IsLowResPostfix(ref bool __result)
        {
            try
            {
                var settings = Main.Settings;
                if (settings != null && settings.ExtendedHdZoom && settings.HdZoomMultiplier >= AlwaysHdThreshold)
                {
                    __result = false;
                }
            }
            catch { }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(QualityChanger.shouldRenderBuildings))]
        private static void ShouldRenderBuildingsPostfix(ref bool __result)
        {
            try
            {
                var settings = Main.Settings;
                if (settings != null && settings.ExtendedHdZoom && settings.HdZoomMultiplier >= AlwaysHdThreshold)
                {
                    __result = true;
                }
            }
            catch { }
        }
    }
}
