using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ModernAge
{
    public static class ModernIds
    {
        // Tier 1: Blackpowder & Counters
        public const string Flintlock = "modern_flintlock";
        public const string Musket = "modern_musket";
        public const string Blunderbuss = "modern_blunderbuss";
        public const string Culverin = "modern_culverin";
        public const string BastionPlate = "modern_bastion_plate";
        public const string AegisAmulet = "modern_aegis_amulet";
        public const string ShockHalberd = "modern_shock_halberd";
        public const string Warbow = "modern_warbow";

        // Tier 2 & 3 Anti-Armor / Anti-Air infantry counters
        public const string AtRifle = "modern_at_rifle";
        public const string FlakGun = "modern_flak_gun";
        public const string GuidedAt = "modern_guided_at";
        public const string SamLauncher = "modern_sam_launcher";

        // Tier 2 & 3 Vehicles & Aircraft (ActorAsset IDs)
        public const string LightTank = "modern_light_tank";
        public const string PropFighter = "modern_prop_fighter";
        public const string EarlyHeli = "modern_early_heli";
        public const string MainBattleTank = "modern_mbt";
        public const string AttackHeli = "modern_attack_heli";
        public const string SupersonicJet = "modern_jet";

        public static readonly HashSet<string> FirearmIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Flintlock, Musket, Blunderbuss, Culverin, "shotgun", "alien_blaster"
        };

        public static readonly HashSet<string> ArmorVehicleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            LightTank, MainBattleTank
        };

        public static readonly HashSet<string> AircraftIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            PropFighter, EarlyHeli, AttackHeli, SupersonicJet
        };
    }

    [HarmonyPatch(typeof(ItemLibrary))]
    public static class ModernAssetRegistry
    {
        private static bool _itemsDone = false;
        private static bool _unitsDone = false;

        [HarmonyPostfix]
        [HarmonyPatch(nameof(ItemLibrary.init))]
        private static void ItemInitPostfix()
        {
            RegisterAll();
        }

        public static void RegisterAll()
        {
            if (!_itemsDone && AssetManager.items != null && AssetManager.projectiles != null)
            {
                RegisterProjectiles();
                RegisterEquipment();
                _itemsDone = true;
            }
            if (!_unitsDone && AssetManager.actor_library != null)
            {
                RegisterVehiclesAndAircraft();
                _unitsDone = true;
            }
        }

        private static void RegisterProjectiles()
        {
            var projs = AssetManager.projectiles;
            CloneProj(projs, "modern_bullet", "arrow", p => { p.speed = 34f; p.trail_effect_enabled = true; p.can_be_blocked = true; });
            CloneProj(projs, "modern_shell", "rock", p => { p.speed = 24f; p.trail_effect_enabled = true; p.hit_shake = true; p.shake_intensity = 0.14f; });
            CloneProj(projs, "modern_rocket", "fireball", p => { p.speed = 36f; p.trail_effect_enabled = true; p.terraform_range = 0; p.hit_shake = true; p.shake_intensity = 0.10f; });
            CloneProj(projs, "modern_sam", "arrow", p => { p.speed = 48f; p.trail_effect_enabled = true; p.can_be_blocked = false; });
        }

        private static void CloneProj(ProjectileLibrary lib, string id, string tpl, Action<ProjectileAsset> cfg)
        {
            if (lib.has(id)) return;
            ProjectileAsset p = lib.has(tpl) ? lib.clone(id, tpl) : lib.add(new ProjectileAsset { id = id });
            if (p != null) cfg?.Invoke(p);
        }

        private static void RegisterEquipment()
        {
            var lib = AssetManager.items;
            string gunBase = lib.has("shotgun") ? "shotgun" : "bow";
            string bowBase = lib.has("bow") ? "bow" : gunBase;
            string poleBase = lib.has("spear") ? "spear" : "sword";
            string armorBase = lib.has("armor") ? "armor" : "leather_armor";
            string amuletBase = lib.has("amulet") ? "amulet" : "ring";

            // --- TIER 1: Blackpowder & Counters ---
            CloneEq(lib, ModernIds.Flintlock, gunBase, e => { e.projectile = "modern_bullet"; e.base_stats["damage"] = 24f; e.base_stats["range"] = 10f; e.base_stats["attack_speed"] = 12f; });
            CloneEq(lib, ModernIds.Musket, gunBase, e => { e.projectile = "modern_bullet"; e.base_stats["damage"] = 42f; e.base_stats["range"] = 19f; e.base_stats["attack_speed"] = -35f; e.base_stats["critical_chance"] = 0.18f; });
            CloneEq(lib, ModernIds.Blunderbuss, gunBase, e => { e.projectile = "modern_bullet"; e.base_stats["damage"] = 28f; e.base_stats["range"] = 8f; e.base_stats["targets"] = 3f; e.base_stats["attack_speed"] = -25f; e.base_stats["knockback"] = 1.6f; });
            CloneEq(lib, ModernIds.Culverin, gunBase, e => { e.projectile = "modern_shell"; e.base_stats["damage"] = 58f; e.base_stats["range"] = 22f; e.base_stats["targets"] = 2f; e.base_stats["attack_speed"] = -55f; e.base_stats["speed"] = -4f; });

            CloneEq(lib, ModernIds.BastionPlate, armorBase, e => { e.base_stats["armor"] = 38f; e.base_stats["health"] = 160f; e.base_stats["speed"] = -2f; });
            CloneEq(lib, ModernIds.AegisAmulet, amuletBase, e => { e.base_stats["armor"] = 14f; e.base_stats["health"] = 80f; });
            CloneEq(lib, ModernIds.ShockHalberd, poleBase, e => { e.base_stats["damage"] = 34f; e.base_stats["speed"] = 8f; e.base_stats["attack_speed"] = 14f; e.base_stats["critical_chance"] = 0.20f; });
            CloneEq(lib, ModernIds.Warbow, bowBase, e => { e.projectile = "arrow"; e.base_stats["damage"] = 26f; e.base_stats["range"] = 21f; e.base_stats["attack_speed"] = 25f; e.base_stats["speed"] = 3f; });

            // --- TIER 2 & 3: Anti-Tank & Anti-Air Counters ---
            CloneEq(lib, ModernIds.AtRifle, gunBase, e => { e.projectile = "modern_bullet"; e.base_stats["damage"] = 48f; e.base_stats["range"] = 22f; e.base_stats["attack_speed"] = -30f; });
            CloneEq(lib, ModernIds.FlakGun, gunBase, e => { e.projectile = "modern_sam"; e.base_stats["damage"] = 38f; e.base_stats["range"] = 26f; e.base_stats["attack_speed"] = 18f; });
            CloneEq(lib, ModernIds.GuidedAt, gunBase, e => { e.projectile = "modern_rocket"; e.base_stats["damage"] = 72f; e.base_stats["range"] = 25f; e.base_stats["attack_speed"] = -40f; });
            CloneEq(lib, ModernIds.SamLauncher, gunBase, e => { e.projectile = "modern_sam"; e.base_stats["damage"] = 64f; e.base_stats["range"] = 32f; e.base_stats["attack_speed"] = 8f; });
        }

        private static void CloneEq(ItemLibrary lib, string id, string tpl, Action<EquipmentAsset> cfg)
        {
            if (lib.has(id)) return;
            EquipmentAsset eq = lib.has(tpl) ? lib.clone(id, tpl) : null;
            if (eq == null) return;
            eq.translation_key = "item_" + id;
            eq.material = "steel";
            cfg?.Invoke(eq);
        }

        private static void RegisterVehiclesAndAircraft()
        {
            var lib = AssetManager.actor_library;
            string groundTpl = lib.has("assimilator") ? "assimilator" : (lib.has("living_house") ? "living_house" : "human");
            string airTpl = lib.has("ufo") ? "ufo" : (lib.has("fairy") ? "fairy" : "human");

            // Tier 2: Light Tank, Prop Fighter, Early Helicopter
            CloneUnit(lib, ModernIds.LightTank, groundTpl, u =>
            {
                u.flying = false;
                u.hovering = false;
                u.can_attack_buildings = true;
                u.default_attack = ModernIds.Culverin;
                u.base_stats["health"] = 680f;
                u.base_stats["armor"] = 48f;
                u.base_stats["damage"] = 52f;
                u.base_stats["speed"] = 44f;
                u.base_stats["range"] = 18f;
            });

            CloneUnit(lib, ModernIds.PropFighter, airTpl, u =>
            {
                u.flying = true;
                u.hovering = true;
                u.can_attack_buildings = true;
                u.default_attack = ModernIds.Flintlock;
                u.base_stats["health"] = 340f;
                u.base_stats["armor"] = 18f;
                u.base_stats["damage"] = 44f;
                u.base_stats["speed"] = 78f;
                u.base_stats["range"] = 20f;
            });

            CloneUnit(lib, ModernIds.EarlyHeli, airTpl, u =>
            {
                u.flying = true;
                u.hovering = true;
                u.can_attack_buildings = true;
                u.default_attack = ModernIds.Musket;
                u.base_stats["health"] = 460f;
                u.base_stats["armor"] = 26f;
                u.base_stats["damage"] = 48f;
                u.base_stats["speed"] = 62f;
                u.base_stats["range"] = 18f;
            });

            // Tier 3: Main Battle Tank, Attack Helicopter, Supersonic Jet
            CloneUnit(lib, ModernIds.MainBattleTank, groundTpl, u =>
            {
                u.flying = false;
                u.hovering = false;
                u.can_attack_buildings = true;
                u.default_attack = ModernIds.GuidedAt;
                u.base_stats["health"] = 1350f;
                u.base_stats["armor"] = 68f;
                u.base_stats["damage"] = 86f;
                u.base_stats["speed"] = 48f;
                u.base_stats["range"] = 22f;
            });

            CloneUnit(lib, ModernIds.AttackHeli, airTpl, u =>
            {
                u.flying = true;
                u.hovering = true;
                u.can_attack_buildings = true;
                u.default_attack = ModernIds.GuidedAt;
                u.base_stats["health"] = 760f;
                u.base_stats["armor"] = 36f;
                u.base_stats["damage"] = 76f;
                u.base_stats["speed"] = 72f;
                u.base_stats["range"] = 22f;
            });

            CloneUnit(lib, ModernIds.SupersonicJet, airTpl, u =>
            {
                u.flying = true;
                u.hovering = true;
                u.very_high_flyer = true;
                u.can_attack_buildings = true;
                u.default_attack = ModernIds.SamLauncher;
                u.base_stats["health"] = 620f;
                u.base_stats["armor"] = 28f;
                u.base_stats["damage"] = 88f;
                u.base_stats["speed"] = 105f;
                u.base_stats["range"] = 26f;
            });

            Debug.Log("[ModernAge] Registered Tier 1-3 firearms, counters, tanks, helicopters, and jets.");
        }

        private static void CloneUnit(ActorAssetLibrary lib, string id, string tpl, Action<ActorAsset> cfg)
        {
            if (lib.has(id)) return;
            ActorAsset a = lib.has(tpl) ? lib.clone(id, tpl) : null;
            if (a == null) return;
            a.ignore_tile_speed_multiplier = true;
            cfg?.Invoke(a);
        }
    }
}
