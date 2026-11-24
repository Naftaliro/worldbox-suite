using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace CoreScale
{
    [HarmonyPatch(typeof(MapSizeLibrary))]
    public static class MapSizePatches
    {
        private static readonly (string id, int chunks)[] CustomSizes =
        {
            ("colossal", 12),
            ("continental", 16),
            ("planetary", 20),
            ("leviathan", 24),
        };

        private static readonly FieldInfo MapSizesArrayField =
            AccessTools.Field(typeof(MapSizeLibrary), "_mapSizes");

        [HarmonyPostfix]
        [HarmonyPatch(nameof(MapSizeLibrary.init))]
        private static void InitPostfix(MapSizeLibrary __instance)
        {
            if (Main.Settings == null || !Main.Settings.EnableCustomMapSizes) return;
            RegisterCustomSizes(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(MapSizeLibrary.isSizeValid))]
        private static void IsSizeValidPostfix(string pSize, ref bool __result)
        {
            if (__result) return;
            foreach (var (id, _) in CustomSizes)
            {
                if (string.Equals(pSize, id, StringComparison.OrdinalIgnoreCase))
                {
                    __result = true;
                    return;
                }
            }
        }

        public static void RegisterCustomSizes(MapSizeLibrary library)
        {
            if (library == null) return;

            var existingIds = new List<string>();
            if (MapSizesArrayField?.GetValue(library) is string[] currentArr)
            {
                existingIds.AddRange(currentArr);
            }

            foreach (var (id, chunks) in CustomSizes)
            {
                if (!library.has(id))
                {
                    library.add(new MapSizeAsset
                    {
                        id = id,
                        size = chunks,
                        path_icon = "iconWorldSizeIceberg",
                        show_warning = true
                    });
                }

                if (!existingIds.Contains(id))
                {
                    existingIds.Add(id);
                }
            }

            if (MapSizesArrayField != null && existingIds.Count > 0)
            {
                MapSizesArrayField.SetValue(library, existingIds.ToArray());
            }

            Config.maxMapSize = "leviathan";
            Debug.Log($"[CoreScale] Registered {CustomSizes.Length} giant map presets (up to 24x24 chunks).");
        }
    }
}
