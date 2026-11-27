using System;
using System.Reflection;
using HarmonyLib;
using NeoModLoader.General;
using UnityEngine;

namespace CoreScale
{
    [HarmonyPatch]
    public static class HyperSpeedSystem
    {
        private static readonly string[] CycleOrder = { "x1", "x5", "x10", "x20", "x40", "x60" };

        private static readonly FieldInfo TimeScaleAssetField =
            AccessTools.Field(typeof(Config), "time_scale_asset");

        private static bool _assetRegistered;
        private static bool _localesRegistered;

        public static void EnsureRegistered()
        {
            if (!_localesRegistered && LocalizedTextManager.instance != null)
            {
                try
                {
                    LM.AddToCurrentLocale("speed_x10", "10x");
                    LM.AddToCurrentLocale("speed_x15", "15x");
                    LM.AddToCurrentLocale("speed_x20", "20x");
                    LM.AddToCurrentLocale("speed_x40", "40x (Sonic)");
                    LM.AddToCurrentLocale("speed_x60", "60x (Hyper)");
                    if (LocalizedTextManager.stringExists("speed_x60"))
                    {
                        _localesRegistered = true;
                    }
                }
                catch { }
            }

            if (!_assetRegistered && AssetManager.time_scales != null && AssetManager.time_scales.has("x40"))
            {
                try
                {
                    if (!AssetManager.time_scales.has("x60"))
                    {
                        AssetManager.time_scales.add(new WorldTimeScaleAsset
                        {
                            id = "x60",
                            locale_key = "speed_x60",
                            multiplier = 60f,
                            ticks = 3,
                            conway_ticks = 3,
                            sonic = true,
                            path_icon = "ui/Icons/iconClockXSonic"
                        });
                    }
                    _assetRegistered = true;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[CoreScale] Failed to register x60 speed asset: {e.Message}");
                }
            }
        }

        public static string GetCurrentSpeedId()
        {
            try
            {
                if (TimeScaleAssetField?.GetValue(null) is WorldTimeScaleAsset asset && !string.IsNullOrEmpty(asset.id))
                {
                    return asset.id;
                }
            }
            catch { }
            return "x1";
        }

        public static string SetSpeed(string speedId)
        {
            EnsureRegistered();
            if (string.IsNullOrEmpty(speedId)) speedId = "x1";

            try
            {
                if (AssetManager.time_scales != null && !AssetManager.time_scales.has(speedId))
                {
                    speedId = "x40";
                }
                Config.paused = false;
                Config.setWorldSpeed(speedId, true);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CoreScale] SetSpeed({speedId}) failed: {e.Message}");
            }

            return $"World Speed: {FormatSpeedLabel(speedId)}";
        }

        public static string CycleHyperSpeed()
        {
            EnsureRegistered();
            string cur = GetCurrentSpeedId();

            int idx = -1;
            for (int i = 0; i < CycleOrder.Length; i++)
            {
                if (string.Equals(CycleOrder[i], cur, StringComparison.OrdinalIgnoreCase))
                {
                    idx = i;
                    break;
                }
            }

            string next = idx >= 0 ? CycleOrder[(idx + 1) % CycleOrder.Length] : "x10";
            return SetSpeed(next);
        }

        public static string FormatSpeedLabel(string speedId)
        {
            switch (speedId)
            {
                case "x1": return "1x";
                case "x2": return "2x";
                case "x3": return "3x";
                case "x4": return "4x";
                case "x5": return "5x";
                case "x10": return "10x";
                case "x15": return "15x";
                case "x20": return "20x";
                case "x40": return "40x (Sonic)";
                case "x60": return "60x (Hyper)";
                default: return speedId;
            }
        }

        private static string MapExtendedNextId(string currentId)
        {
            switch (currentId)
            {
                case "x5": return "x10";
                case "x10": return "x20";
                case "x15": return "x20";
                case "x20": return "x40";
                case "x40": return AssetManager.time_scales != null && AssetManager.time_scales.has("x60") ? "x60" : "x1";
                case "x60": return "x1";
                default: return null;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(WorldTimeScaleAsset), nameof(WorldTimeScaleAsset.getNext))]
        private static void GetNextPostfix(WorldTimeScaleAsset __instance, ref object __result)
        {
            try
            {
                if (__instance == null || Main.Settings == null || !Main.Settings.UnlockHyperSpeed) return;
                EnsureRegistered();

                string nextId = MapExtendedNextId(__instance.id);
                if (string.IsNullOrEmpty(nextId)) return;

                if (__result is string)
                {
                    __result = nextId;
                }
                else if (__result is WorldTimeScaleAsset && AssetManager.time_scales != null && AssetManager.time_scales.has(nextId))
                {
                    __result = AssetManager.time_scales.get(nextId);
                }
            }
            catch { }
        }
    }
}
