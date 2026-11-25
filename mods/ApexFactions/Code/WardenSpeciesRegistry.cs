using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using NeoModLoader.General;
using UnityEngine;

namespace ApexFactions
{
    public static class WardenSpeciesRegistry
    {
        public const string WardenAssetId = "warden";

        private static readonly FieldInfo CachedSubspeciesTraitsField =
            AccessTools.Field(typeof(ActorAsset), "_cached_assets_subspecies_traits");

        private static readonly FieldInfo CachedOverviewStatsField =
            AccessTools.Field(typeof(ActorAsset), "_cached_overview_stats");

        private static readonly FieldInfo CachedSpriteField =
            AccessTools.Field(typeof(ActorAsset), "_cached_sprite");

        private static bool _registered;
        private static bool _localeRegistered;

        public static bool EnsureRegistered()
        {
            if (_registered && _localeRegistered) return true;

            var lib = AssetManager.actor_library;
            if (lib == null || !lib.has("human")) return false;

            RegisterLocaleSafe();

            if (lib.has(WardenAssetId))
            {
                _registered = true;
                return true;
            }

            try
            {
                ActorAsset human = lib.get("human");
                ActorAsset warden = lib.clone(WardenAssetId, "human");
                if (warden == null) return false;

                warden.name_locale = "Warden";
                warden.civ = true;
                warden.is_humanoid = true;
                warden.default_animal = false;

                warden.texture_id = "human";
                warden.architecture_id = "human";
                warden.kingdom_id_civilization = "human";
                warden.kingdom_id_wild = "nomads_human";

                warden.color_hex = "#D4AF37";
                warden.color = new Color32(212, 175, 55, 255);
                warden.shadow = true;

                warden.civ_base_cities = 2;
                warden.family_limit = 8;
                warden.item_making_skill = 4;

                // Ensure complete collection isolation from the cloned human ActorAsset
                if (warden.units == null || ReferenceEquals(warden.units, human.units))
                    warden.units = new HashSet<Actor>();
                else
                    warden.units.Clear();

                if (warden.genome_parts == null || ReferenceEquals(warden.genome_parts, human.genome_parts))
                    warden.genome_parts = new HashSet<GenomePart>();
                else
                    warden.genome_parts.Clear();

                if (warden.base_stats == null || ReferenceEquals(warden.base_stats, human.base_stats))
                    warden.base_stats = human.base_stats != null ? (BaseStats)human.base_stats.Clone() : new BaseStats();

                if (warden.traits == null || ReferenceEquals(warden.traits, human.traits))
                    warden.traits = new List<string>();
                else
                    warden.traits.Clear();

                if (warden.default_subspecies_traits == null || ReferenceEquals(warden.default_subspecies_traits, human.default_subspecies_traits))
                    warden.default_subspecies_traits = human.default_subspecies_traits != null
                        ? new List<string>(human.default_subspecies_traits)
                        : new List<string>();

                if (warden.default_culture_traits == null || ReferenceEquals(warden.default_culture_traits, human.default_culture_traits))
                    warden.default_culture_traits = new List<string>();
                else
                    warden.default_culture_traits.Clear();

                // Calibrated genome (1 Warden ~ 30 Humans / 20 Dwarves / 15 Elves / 15 Orcs)
                warden.addGenome(
                    ("health", 800f),
                    ("stamina", 300f),
                    ("mutation", 0.5f),
                    ("bonus_sex_random", 1f),
                    ("lifespan", 450f),
                    ("damage", 68f),
                    ("speed", 20f),
                    ("offspring", 1f),
                    ("diplomacy", 7f),
                    ("warfare", 10f),
                    ("stewardship", 8f),
                    ("intelligence", 10f)
                );

                // Calibrated combat & physical base stats
                warden.base_stats["armor"] = 45f;
                warden.base_stats["attack_speed"] = 1.45f;
                warden.base_stats["critical_chance"] = 0.20f;
                warden.base_stats["critical_damage_multiplier"] = 2.0f;
                warden.base_stats["targets"] = 3f;
                warden.base_stats["area_of_effect"] = 0.55f;
                warden.base_stats["knockback"] = 2.2f;
                warden.base_stats["scale"] = 0.135f;
                warden.base_stats["mass"] = 4f;
                warden.base_stats["mass_2"] = 160f;
                warden.base_stats["accuracy"] = 5f;

                // Innate actor traits
                AddUnique(warden.traits, "deflect_projectile");
                AddUnique(warden.traits, "block");
                AddUnique(warden.traits, "strong_minded");
                AddUnique(warden.traits, "immune");

                // Subspecies traits
                warden.default_subspecies_traits.Remove("bad_genes");
                warden.default_subspecies_traits.Remove("gestation_long");
                AddUnique(warden.default_subspecies_traits, "gestation_extremely_long");
                AddUnique(warden.default_subspecies_traits, "population_minimal");
                AddUnique(warden.default_subspecies_traits, "accelerated_healing");
                AddUnique(warden.default_subspecies_traits, "pure");
                CachedSubspeciesTraitsField?.SetValue(warden, null);
                CachedOverviewStatsField?.SetValue(warden, null);
                CachedSpriteField?.SetValue(warden, WardenSprites.IconSprite);

                // Culture traits
                AddUnique(warden.default_culture_traits, "weaponsmith_mastery");
                AddUnique(warden.default_culture_traits, "armorsmith_mastery");
                AddUnique(warden.default_culture_traits, "serenity_now");
                AddUnique(warden.default_culture_traits, "elder_reverence");
                AddUnique(warden.default_culture_traits, "legacy_keepers");
                AddUnique(warden.default_culture_traits, "roads");
                AddUnique(warden.default_culture_traits, "tower_lovers");
                AddUnique(warden.default_culture_traits, "statue_lovers");
                AddUnique(warden.default_culture_traits, "city_layout_the_grand_arrangement");

                _registered = true;
                Debug.Log("[ApexFactions] Registered dedicated 'warden' ActorAsset species.");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ApexFactions] Failed to register 'warden' species: {e}");
                return false;
            }
        }

        private static void AddUnique(List<string> list, string id)
        {
            if (!list.Contains(id)) list.Add(id);
        }

        private static void RegisterLocaleSafe()
        {
            if (_localeRegistered) return;
            if (LocalizedTextManager.instance == null) return;
            try
            {
                LM.AddToCurrentLocale("Warden", "Warden");
                LM.AddToCurrentLocale("warden", "Warden");
                if (LocalizedTextManager.stringExists("Warden"))
                {
                    _localeRegistered = true;
                }
            }
            catch
            {
                // Locale manager may not be initialized yet; retry on next call
            }
        }
    }
}
