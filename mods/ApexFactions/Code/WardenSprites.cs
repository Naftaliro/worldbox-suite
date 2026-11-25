using System;
using System.IO;
using HarmonyLib;
using NeoModLoader.utils;
using UnityEngine;

namespace ApexFactions
{
    [HarmonyPatch]
    public static class WardenSprites
    {
        private static Sprite _citizenSprite;
        private static Sprite _warriorSprite;
        private static Sprite _leaderSprite;
        private static Sprite _iconSprite;
        private static bool _loaded;

        public static Sprite IconSprite => _iconSprite;
        public static bool HasCustomSprites => _citizenSprite != null || _warriorSprite != null || _leaderSprite != null;

        public static void Load(string modFolder)
        {
            if (_loaded || string.IsNullOrEmpty(modFolder)) return;
            _loaded = true;

            string dir = Path.Combine(modFolder, "GameResources", "wardens");
            _citizenSprite = LoadSafe(Path.Combine(dir, "warden_citizen.png"));
            _warriorSprite = LoadSafe(Path.Combine(dir, "warden_warrior.png"));
            _leaderSprite  = LoadSafe(Path.Combine(dir, "warden_leader.png"));
            _iconSprite    = LoadSafe(Path.Combine(dir, "warden_icon.png"));

            Debug.Log($"[ApexFactions] Warden sprites loaded (citizen={_citizenSprite != null}, warrior={_warriorSprite != null}, leader={_leaderSprite != null}, icon={_iconSprite != null}).");
        }

        private static Sprite LoadSafe(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                return SpriteLoadUtils.LoadSingleSprite(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ApexFactions] Failed to load sprite '{path}': {e.Message}");
                return null;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Actor), "calculateMainSprite")]
        private static void CalculateMainSpritePostfix(Actor __instance, ref Sprite __result)
        {
            if (__instance == null || __instance.asset == null) return;
            if (__instance.asset.id != WardenSpeciesRegistry.WardenAssetId) return;

            if ((__instance.isKing() || __instance.isCityLeader()) && _leaderSprite != null)
            {
                __result = _leaderSprite;
                return;
            }

            bool inRetaliation = __instance.kingdom != null && WardenSystem.HasActiveRetaliation(__instance.kingdom.id);
            if ((__instance.isWarrior() || inRetaliation) && _warriorSprite != null)
            {
                __result = _warriorSprite;
                return;
            }

            if (_citizenSprite != null)
            {
                __result = _citizenSprite;
            }
        }
    }
}
