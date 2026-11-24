using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Arsenal
{
    public static class ArsenalIds
    {
        public const string Flintlock = "arsenal_flintlock";
        public const string Musket = "arsenal_musket";
        public const string Blunderbuss = "arsenal_blunderbuss";
        public const string Culverin = "arsenal_culverin";

        public const string BastionPlate = "arsenal_bastion_plate";
        public const string AegisAmulet = "arsenal_aegis_amulet";
        public const string ShockHalberd = "arsenal_shock_halberd";
        public const string Warbow = "arsenal_warbow";

        public static readonly HashSet<string> FirearmWeaponIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Flintlock, Musket, Blunderbuss, Culverin, "shotgun", "alien_blaster"
        };
    }

    [HarmonyPatch(typeof(ItemLibrary))]
    public static class ArsenalLibrary
    {
        private static bool _registered = false;

        [HarmonyPostfix]
        [HarmonyPatch(nameof(ItemLibrary.init))]
        private static void ItemLibraryInitPostfix()
        {
            RegisterAllAssets();
        }

        public static void RegisterAllAssets()
        {
            if (_registered) return;
            if (AssetManager.items == null || AssetManager.projectiles == null) return;

            RegisterProjectiles();
            RegisterEquipment();
            _registered = true;
            Debug.Log("[Arsenal] Registered 4 blackpowder firearms and 4 counter-firearm gear assets.");
        }

        private static void RegisterProjectiles()
        {
            var projs = AssetManager.projectiles;

            CloneProjectile(projs, "arsenal_musket_ball", "arrow", p =>
            {
                p.speed = 34f;
                p.trail_effect_enabled = true;
                p.can_be_blocked = true;
                p.hit_shake = true;
                p.shake_intensity = 0.04f;
            });

            CloneProjectile(projs, "arsenal_buckshot", "arrow", p =>
            {
                p.speed = 28f;
                p.trail_effect_enabled = true;
                p.can_be_blocked = true;
            });

            CloneProjectile(projs, "arsenal_culverin_shot", "rock", p =>
            {
                p.speed = 22f;
                p.trail_effect_enabled = true;
                p.can_be_blocked = true;
                p.hit_shake = true;
                p.shake_intensity = 0.12f;
            });

            CloneProjectile(projs, "arsenal_warbow_arrow", "arrow", p =>
            {
                p.speed = 26f;
                p.can_be_blocked = true;
            });
        }

        private static void CloneProjectile(ProjectileLibrary lib, string id, string templateId, Action<ProjectileAsset> configure)
        {
            if (lib.has(id)) return;
            ProjectileAsset cloned = lib.has(templateId) ? lib.clone(id, templateId) : lib.add(new ProjectileAsset { id = id });
            if (cloned != null)
            {
                configure?.Invoke(cloned);
            }
        }

        private static void RegisterEquipment()
        {
            var lib = AssetManager.items;
            string gunBase = lib.has("shotgun") ? "shotgun" : "bow";
            string bowBase = lib.has("bow") ? "bow" : gunBase;
            string poleBase = lib.has("spear") ? "spear" : "sword";
            string armorBase = lib.has("armor") ? "armor" : "leather_armor";
            string amuletBase = lib.has("amulet") ? "amulet" : "ring";

            // --- FIREARMS ---
            CloneEquip(lib, ArsenalIds.Flintlock, gunBase, e =>
            {
                e.translation_key = "item_" + ArsenalIds.Flintlock;
                e.projectile = "arsenal_musket_ball";
                e.equipment_value = 95;
                e.base_stats["damage"] = 24f;
                e.base_stats["range"] = 10f;
                e.base_stats["attack_speed"] = 12f;
                e.base_stats["critical_chance"] = 0.12f;
            });

            CloneEquip(lib, ArsenalIds.Musket, gunBase, e =>
            {
                e.translation_key = "item_" + ArsenalIds.Musket;
                e.projectile = "arsenal_musket_ball";
                e.equipment_value = 145;
                e.base_stats["damage"] = 42f;
                e.base_stats["range"] = 19f;
                e.base_stats["attack_speed"] = -35f; // slow blackpowder reload
                e.base_stats["critical_chance"] = 0.18f;
            });

            CloneEquip(lib, ArsenalIds.Blunderbuss, gunBase, e =>
            {
                e.translation_key = "item_" + ArsenalIds.Blunderbuss;
                e.projectile = "arsenal_buckshot";
                e.equipment_value = 130;
                e.base_stats["damage"] = 28f;
                e.base_stats["range"] = 8f;
                e.base_stats["targets"] = 3f;
                e.base_stats["attack_speed"] = -25f;
                e.base_stats["knockback"] = 1.6f;
            });

            CloneEquip(lib, ArsenalIds.Culverin, gunBase, e =>
            {
                e.translation_key = "item_" + ArsenalIds.Culverin;
                e.projectile = "arsenal_culverin_shot";
                e.equipment_value = 210;
                e.base_stats["damage"] = 58f;
                e.base_stats["range"] = 22f;
                e.base_stats["targets"] = 2f;
                e.base_stats["attack_speed"] = -55f;
                e.base_stats["speed"] = -4f;
                e.base_stats["knockback"] = 2.2f;
            });

            // --- ANTI-FIREARM COUNTERS ---
            CloneEquip(lib, ArsenalIds.BastionPlate, armorBase, e =>
            {
                e.translation_key = "item_" + ArsenalIds.BastionPlate;
                e.equipment_value = 180;
                e.base_stats["armor"] = 38f;
                e.base_stats["health"] = 160f;
                e.base_stats["speed"] = -2f;
            });

            CloneEquip(lib, ArsenalIds.AegisAmulet, amuletBase, e =>
            {
                e.translation_key = "item_" + ArsenalIds.AegisAmulet;
                e.equipment_value = 165;
                e.base_stats["armor"] = 14f;
                e.base_stats["health"] = 80f;
            });

            CloneEquip(lib, ArsenalIds.ShockHalberd, poleBase, e =>
            {
                e.translation_key = "item_" + ArsenalIds.ShockHalberd;
                e.equipment_value = 140;
                e.base_stats["damage"] = 34f;
                e.base_stats["speed"] = 8f;          // fast gap closer against musket lines
                e.base_stats["attack_speed"] = 14f;
                e.base_stats["critical_chance"] = 0.20f;
            });

            CloneEquip(lib, ArsenalIds.Warbow, bowBase, e =>
            {
                e.translation_key = "item_" + ArsenalIds.Warbow;
                e.projectile = "arsenal_warbow_arrow";
                e.equipment_value = 140;
                e.base_stats["damage"] = 26f;
                e.base_stats["range"] = 21f;         // outranges & out-fires slow muskets
                e.base_stats["attack_speed"] = 25f;
                e.base_stats["speed"] = 3f;
            });
        }

        private static void CloneEquip(ItemLibrary lib, string id, string templateId, Action<EquipmentAsset> configure)
        {
            if (lib.has(id)) return;
            EquipmentAsset asset = lib.has(templateId) ? lib.clone(id, templateId) : null;
            if (asset == null) return;
            asset.material = "steel";
            configure?.Invoke(asset);
        }
    }
}
