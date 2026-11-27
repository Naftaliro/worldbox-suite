using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using NeoModLoader.utils;
using UnityEngine;

namespace ModernAge
{
    [HarmonyPatch(typeof(Actor))]
    public static class VehicleSpritePatches
    {
        private static readonly Dictionary<string, Sprite> LoadedSprites = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        private static readonly string[] VehicleIds =
        {
            ModernIds.LightTank,
            ModernIds.MainBattleTank,
            ModernIds.PropFighter,
            ModernIds.EarlyHeli,
            ModernIds.AttackHeli,
            ModernIds.SupersonicJet
        };

        public static void LoadSprites(string modFolder)
        {
            LoadedSprites.Clear();
            string dir = Path.Combine(modFolder, "GameResources", "vehicles");
            if (!Directory.Exists(dir)) return;

            foreach (string id in VehicleIds)
            {
                string path = Path.Combine(dir, id + ".png");
                if (!File.Exists(path)) continue;
                try
                {
                    Sprite sp = SpriteLoadUtils.LoadSingleSprite(path);
                    if (sp != null)
                    {
                        LoadedSprites[id] = sp;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ModernAge] Could not load sprite {id}.png: {e.Message}");
                }
            }
            Debug.Log($"[ModernAge] Loaded {LoadedSprites.Count} custom vehicle/aircraft pixel sprites.");
        }

        [HarmonyPostfix]
        [HarmonyPatch("calculateMainSprite")]
        private static void CalculateMainSpritePostfix(Actor __instance, ref Sprite __result)
        {
            if (__instance?.asset == null || string.IsNullOrEmpty(__instance.asset.id) || LoadedSprites.Count == 0) return;
            if (LoadedSprites.TryGetValue(__instance.asset.id, out Sprite custom) && custom != null)
            {
                __result = custom;
            }
        }
    }
}
