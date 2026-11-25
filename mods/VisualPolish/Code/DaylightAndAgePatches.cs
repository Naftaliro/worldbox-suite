using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace VisualPolish
{
    public static class DaylightAndAgePatches
    {
        private static readonly FieldInfo EraManagerField =
            AccessTools.Field(typeof(MapBox), "era_manager");

        private static readonly MethodInfo SetCurrentAgeMethod =
            AccessTools.Method(typeof(WorldAgeManager), "setCurrentAge", new[] { typeof(WorldAgeAsset), typeof(bool) });

        private static WorldAgeManager GetEraManager()
        {
            if (MapBox.instance == null || EraManagerField == null) return null;
            return EraManagerField.GetValue(MapBox.instance) as WorldAgeManager;
        }

        public static void ApplyLockAgeOfHope(bool locked)
        {
            try
            {
                WorldAgeManager eraManager = GetEraManager();
                if (eraManager == null) return;

                if (locked)
                {
                    WorldAgeAsset hope = AssetManager.era_library?.get("age_hope");
                    if (hope == null) return;

                    WorldAgeAsset cur = eraManager.getCurrentAge();
                    if (cur == null || cur.id != "age_hope")
                    {
                        eraManager.setAgeToSlot(hope, eraManager.getCurrentSlotIndex());
                        SetCurrentAgeMethod?.Invoke(eraManager, new object[] { hope, true });
                    }
                    eraManager.togglePlay(false);
                }
                else
                {
                    eraManager.togglePlay(true);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VisualPolish] Age of Hope lock update skipped: {e.Message}");
            }
        }

        public static void EnforceAgeOfHopeIfLocked()
        {
            if (Main.Settings == null || !Main.Settings.LockAgeOfHope) return;
            try
            {
                WorldAgeManager eraManager = GetEraManager();
                if (eraManager == null) return;

                WorldAgeAsset hope = AssetManager.era_library?.get("age_hope");
                if (hope == null) return;

                WorldAgeAsset cur = eraManager.getCurrentAge();
                if (cur == null || cur.id != "age_hope")
                {
                    eraManager.setAgeToSlot(hope, eraManager.getCurrentSlotIndex());
                    SetCurrentAgeMethod?.Invoke(eraManager, new object[] { hope, true });
                }

                if (!eraManager.isPaused())
                {
                    eraManager.togglePlay(false);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VisualPolish] Age of Hope enforcement failed: {e.Message}");
            }
        }

        internal static bool IsDarkOverlayLayer(string pID)
        {
            return pID == "night" || pID == "rain_darkness" || pID == "moon" || pID == "ash";
        }
    }

    [HarmonyPatch(typeof(WorldAgeManager))]
    public static class WorldAgeManagerPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch("getNightMod")]
        private static bool GetNightModPrefix(ref float __result)
        {
            if (Main.Settings != null && Main.Settings.AlwaysDayClientSide)
            {
                __result = 0f;
                return false;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch("shouldShowLights")]
        private static bool ShouldShowLightsPrefix(ref bool __result)
        {
            if (Main.Settings != null && Main.Settings.AlwaysDayClientSide)
            {
                __result = false;
                return false;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch("startNextAge")]
        private static bool StartNextAgePrefix()
        {
            if (Main.Settings != null && Main.Settings.LockAgeOfHope)
            {
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(WorldAgeEffects))]
    public static class WorldAgeEffectsPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch("updateLayer")]
        private static void UpdateLayerPrefix(ref bool pEnabled, string pID, float pElapsed)
        {
            if (Main.Settings != null && Main.Settings.AlwaysDayClientSide && DaylightAndAgePatches.IsDarkOverlayLayer(pID))
            {
                pEnabled = false;
            }
        }
    }
}
