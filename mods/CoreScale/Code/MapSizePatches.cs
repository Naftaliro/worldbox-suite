using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using NeoModLoader.General;
using UnityEngine;

namespace CoreScale
{
    [HarmonyPatch(typeof(MapSizeLibrary))]
    public static class MapSizePatches
    {
        private static readonly (string id, int chunks, string displayName)[] CustomSizes =
        {
            ("colossal", 12, "Colossal (12x12)"),
            ("continental", 16, "Continental (16x16)"),
            ("planetary", 20, "Planetary (20x20)"),
            ("leviathan", 24, "Leviathan (24x24)"),
        };

        private static readonly FieldInfo MapSizesArrayField =
            AccessTools.Field(typeof(MapSizeLibrary), "_mapSizes");

        private static bool _loggedRegistration;

        [HarmonyPostfix]
        [HarmonyPatch(nameof(MapSizeLibrary.init))]
        private static void InitPostfix(MapSizeLibrary __instance)
        {
            try
            {
                if (Main.Settings == null || !Main.Settings.EnableCustomMapSizes) return;
                RegisterCustomSizes(__instance);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CoreScale] Custom map size registration skipped: {e.Message}");
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(MapSizeLibrary.linkAssets))]
        private static void LinkAssetsPostfix(MapSizeLibrary __instance)
        {
            try
            {
                if (Main.Settings == null || !Main.Settings.EnableCustomMapSizes) return;
                RegisterCustomSizes(__instance);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CoreScale] Custom map size link skipped: {e.Message}");
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(MapSizeLibrary.isSizeValid))]
        private static void IsSizeValidPostfix(int pMapSize, ref bool __result)
        {
            try
            {
                if (__result) return;
                if (pMapSize > 0 && pMapSize <= 24)
                {
                    __result = true;
                }
            }
            catch { }
        }

        public static void RegisterCustomSizes(MapSizeLibrary library)
        {
            if (library == null) return;

            var existingIds = new List<string>();
            if (MapSizesArrayField?.GetValue(null) is string[] currentArr && currentArr.Length > 0)
            {
                existingIds.AddRange(currentArr);
            }
            else if (library.list != null)
            {
                for (int i = 0; i < library.list.Count; i++)
                {
                    MapSizeAsset asset = library.list[i];
                    if (asset != null && !string.IsNullOrEmpty(asset.id) && !existingIds.Contains(asset.id))
                    {
                        existingIds.Add(asset.id);
                    }
                }
            }

            foreach (var (id, chunks, _) in CustomSizes)
            {
                if (!library.has(id))
                {
                    library.add(new MapSizeAsset
                    {
                        id = id,
                        size = chunks,
                        path_icon = "iconIceberg",
                        show_warning = true
                    });
                }
                else
                {
                    MapSizeAsset existing = library.get(id);
                    if (existing != null && string.IsNullOrEmpty(existing.path_icon))
                    {
                        existing.path_icon = "iconIceberg";
                    }
                }

                if (!existingIds.Contains(id))
                {
                    existingIds.Add(id);
                }
            }

            if (MapSizesArrayField != null && existingIds.Count > 0)
            {
                MapSizesArrayField.SetValue(null, existingIds.ToArray());
            }

            Config.maxMapSize = "leviathan";
            EnsureLocalesRegistered();

            if (!_loggedRegistration)
            {
                _loggedRegistration = true;
                Debug.Log($"[CoreScale] Registered {CustomSizes.Length} giant map presets (up to 24x24 chunks).");
            }
        }

        public static void EnsureLocalesRegistered()
        {
            try
            {
                if (LocalizedTextManager.instance == null) return;
                foreach (var (id, _, displayName) in CustomSizes)
                {
                    LM.AddToCurrentLocale($"map_size_{id}", displayName);
                    LM.AddToCurrentLocale(id, displayName);
                }
            }
            catch { }
        }
    }

    // Keep native Create New World size buttons and labels synced with custom giant presets
    [HarmonyPatch(typeof(WorldTemplatesWindow))]
    public static class WorldTemplatesSizePatches
    {
        [HarmonyPostfix]
        [HarmonyPatch("OnEnable")]
        private static void OnEnablePostfix()
        {
            try
            {
                if (Main.Settings == null || !Main.Settings.EnableCustomMapSizes) return;
                if (AssetManager.map_sizes != null)
                {
                    MapSizePatches.RegisterCustomSizes(AssetManager.map_sizes);
                }
                MapSizePatches.EnsureLocalesRegistered();
            }
            catch { }
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(WorldTemplatesWindow.increaseSize))]
        private static void IncreaseSizePrefix()
        {
            try
            {
                if (Main.Settings == null || !Main.Settings.EnableCustomMapSizes) return;
                if (AssetManager.map_sizes != null)
                {
                    MapSizePatches.RegisterCustomSizes(AssetManager.map_sizes);
                }
            }
            catch { }
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(WorldTemplatesWindow.decreaseSize))]
        private static void DecreaseSizePrefix()
        {
            try
            {
                if (Main.Settings == null || !Main.Settings.EnableCustomMapSizes) return;
                if (AssetManager.map_sizes != null)
                {
                    MapSizePatches.RegisterCustomSizes(AssetManager.map_sizes);
                }
            }
            catch { }
        }
    }

    // Re-register giant map size names once LocalizedTextManager initializes or switches language
    [HarmonyPatch(typeof(LocalizedTextManager))]
    public static class MapSizeLocalePatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(LocalizedTextManager.setLanguage))]
        private static void SetLanguagePostfix()
        {
            try
            {
                MapSizePatches.EnsureLocalesRegistered();
            }
            catch { }
        }
    }
}
