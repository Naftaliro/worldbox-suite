using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using NeoModLoader.General;
using UnityEngine;

namespace ApexFactions
{
    public class SpeciesProfile
    {
        public string Id;
        public string DisplayName;
        public string ArchetypeTitle;
        public string Description;
        public bool IsApexOrSmart;
        public int OpinionModifier;
        public bool NeverStartsWars;
        public bool ProtectedFromCasualWars;
        public bool IsThiefRaider;
        public bool IsBetrayer;
        public string[] BirthTraits;
        public string[] SubspeciesTraits;
        public string[] CultureTraits;
    }

    [HarmonyPatch]
    public static class SpeciesPersonalityRegistry
    {
        public const string OpinionPenguinPactId = "suite_penguin_tux_pact";
        public const string OpinionSpeciesTempId = "suite_species_temperament";

        public static readonly Dictionary<long, string> SpeciesWarReasons = new Dictionary<long, string>();

        private static readonly MethodInfo StartWarMethod =
            AccessTools.Method(typeof(DiplomacyManager), "startWar",
                new[] { typeof(Kingdom), typeof(Kingdom), typeof(WarTypeAsset), typeof(bool) });

        private static readonly FieldInfo CityKingdomField =
            AccessTools.Field(typeof(City), "kingdom");

        private static readonly MethodInfo SpeciesIdGetter =
            AccessTools.PropertyGetter(typeof(Subspecies), "species_id");

        private static readonly string[] RaidLootResources =
        {
            "gold", "gems", "adamantine", "mythril", "silver", "iron", "bread", "meat", "fish", "berries", "wheat"
        };

        private static bool _opinionsRegistered;
        private static bool _opinionLocalesRegistered;

        public static readonly List<string> AllSpeciesIds = new List<string>(30);
        public static readonly Dictionary<string, SpeciesProfile> Profiles =
            new Dictionary<string, SpeciesProfile>(StringComparer.OrdinalIgnoreCase);

        static SpeciesPersonalityRegistry()
        {
            Register(new SpeciesProfile
            {
                Id = "penguin",
                DisplayName = "Penguin",
                ArchetypeTitle = "Tux / Linux Pact",
                Description = "Universally beloved diplomats (+60 opinion). Never start offensive wars; AI refuses casual wars against them unless opinion < -120.",
                IsApexOrSmart = true,
                OpinionModifier = 60,
                NeverStartsWars = true,
                ProtectedFromCasualWars = true,
                BirthTraits = new[] { "wise", "honest", "peaceful", "content", "freeze_proof" },
                SubspeciesTraits = new[] { "adaptation_permafrost", "cold_resistance" },
                CultureTraits = new[] { "xenophiles", "serenity_now", "diplomatic_ascension" }
            });

            Register(new SpeciesProfile
            {
                Id = "snake",
                DisplayName = "Snake",
                ArchetypeTitle = "Serpent's Guile",
                Description = "Toggleable doctrine: deceitful alliance-backstabbers (-25 opinion) or peaceful venomous isolationists.",
                IsApexOrSmart = true,
                OpinionModifier = -25,
                IsBetrayer = true,
                BirthTraits = new[] { "deceitful", "venomous", "poisonous", "poison_immune", "ambitious", "agile" },
                SubspeciesTraits = new[] { "adaptation_swamp" },
                CultureTraits = new[] { "xenophobic", "ethnocentric_guard" }
            });

            Register(new SpeciesProfile
            {
                Id = "raccoon",
                DisplayName = "Raccoon",
                ArchetypeTitle = "Trash-Panda Heist Guild",
                Description = "Clever nocturnal thieves (-20 opinion) who raid rival stockpiles for gold, metals, and food and pickpocket nearby units.",
                IsApexOrSmart = true,
                OpinionModifier = -20,
                IsThiefRaider = true,
                BirthTraits = new[] { "greedy", "agile", "fast", "deceitful", "lucky" },
                SubspeciesTraits = new[] { "accelerated_healing" },
                CultureTraits = new[] { "fast_learners", "gossip_lovers" }
            });

            Register(new SpeciesProfile
            {
                Id = "fox",
                DisplayName = "Fox",
                ArchetypeTitle = "Vulpine Trickster Raiders",
                Description = "Cunning skirmishers (-15 opinion) who siphon gold, gems, and supplies from nearby rival cities and pickpocket foes.",
                IsApexOrSmart = true,
                OpinionModifier = -15,
                IsThiefRaider = true,
                BirthTraits = new[] { "agile", "fast", "deceitful", "lucky", "eagle_eyed" },
                SubspeciesTraits = new[] { "walk_adaptation_snow" },
                CultureTraits = new[] { "bow_lovers", "fast_learners" }
            });

            Register(new SpeciesProfile
            {
                Id = "wolf",
                DisplayName = "Wolf",
                ArchetypeTitle = "Iron Pack Warlords",
                Description = "Fierce coordinated pack hunters (-10 opinion) built for fast raids and relentless frontier warfare.",
                IsApexOrSmart = true,
                OpinionModifier = -10,
                BirthTraits = new[] { "strong", "fast", "savage", "agile", "eagle_eyed" },
                SubspeciesTraits = new[] { "cold_resistance" },
                CultureTraits = new[] { "join_or_die", "axe_lovers" }
            });

            Register(new SpeciesProfile
            {
                Id = "bear",
                DisplayName = "Bear",
                ArchetypeTitle = "Ursine Ironclads",
                Description = "Towering frontline juggernauts (-5 opinion) with immense health, resilience, and northern frost endurance.",
                IsApexOrSmart = true,
                OpinionModifier = -5,
                BirthTraits = new[] { "giant", "strong", "tough", "freeze_proof" },
                SubspeciesTraits = new[] { "adaptation_permafrost", "sleep_at_winter_age" },
                CultureTraits = new[] { "hammer_lovers", "armorsmith_mastery" }
            });

            Register(new SpeciesProfile
            {
                Id = "hyena",
                DisplayName = "Hyena",
                ArchetypeTitle = "Laughing Marauders",
                Description = "Relentless opportunistic raiders (-20 opinion) driven by bloodlust and plunder.",
                IsApexOrSmart = true,
                OpinionModifier = -20,
                BirthTraits = new[] { "savage", "bloodlust", "fast", "greedy", "agile" },
                SubspeciesTraits = new[] { "heat_resistance" },
                CultureTraits = new[] { "join_or_die", "shattered_crown" }
            });

            Register(new SpeciesProfile
            {
                Id = "crocodile",
                DisplayName = "Crocodile",
                ArchetypeTitle = "Swamp Apex Sovereigns",
                Description = "Armored ambush monarchs (-15 opinion) who rule wetlands with iron hides and regeneration.",
                IsApexOrSmart = true,
                OpinionModifier = -15,
                BirthTraits = new[] { "tough", "strong", "savage", "regeneration", "poison_immune" },
                SubspeciesTraits = new[] { "adaptation_swamp", "amphibia" },
                CultureTraits = new[] { "ethnocentric_guard", "spear_lovers" }
            });

            Register(new SpeciesProfile
            {
                Id = "rhino",
                DisplayName = "Rhino",
                ArchetypeTitle = "Armored Bastion",
                Description = "Unstoppable heavy chargers (-5 opinion) who fortify strongholds and shrug off arrows.",
                IsApexOrSmart = true,
                OpinionModifier = -5,
                BirthTraits = new[] { "giant", "tough", "strong", "block" },
                SubspeciesTraits = new[] { "heat_resistance" },
                CultureTraits = new[] { "armorsmith_mastery", "tower_lovers" }
            });

            Register(new SpeciesProfile
            {
                Id = "buffalo",
                DisplayName = "Buffalo",
                ArchetypeTitle = "Thundering Herd",
                Description = "Stalwart plains nomads (+5 opinion) with heavy stamina and loyal communal defense.",
                IsApexOrSmart = false,
                OpinionModifier = 5,
                BirthTraits = new[] { "strong", "tough", "giant", "honest" },
                SubspeciesTraits = new[] { "population_large" },
                CultureTraits = new[] { "true_roots", "roads" }
            });

            Register(new SpeciesProfile
            {
                Id = "monkey",
                DisplayName = "Monkey",
                ArchetypeTitle = "Canopy Scholars",
                Description = "Hyper-intelligent builders and inventors (+5 opinion) who rapidly unlock culture tech.",
                IsApexOrSmart = true,
                OpinionModifier = 5,
                BirthTraits = new[] { "genius", "agile", "fast", "ambitious" },
                SubspeciesTraits = new[] { "accelerated_healing" },
                CultureTraits = new[] { "fast_learners", "reading_lovers", "roads" }
            });

            Register(new SpeciesProfile
            {
                Id = "cat",
                DisplayName = "Cat",
                ArchetypeTitle = "Feline Aristocracy",
                Description = "Aloof agile duelists (0 opinion) obsessed with status, grace, and grand monuments.",
                IsApexOrSmart = true,
                OpinionModifier = 0,
                BirthTraits = new[] { "agile", "fast", "attractive", "lucky", "ambitious" },
                SubspeciesTraits = new[] { "pure" },
                CultureTraits = new[] { "statue_lovers", "sword_lovers" }
            });

            Register(new SpeciesProfile
            {
                Id = "dog",
                DisplayName = "Dog",
                ArchetypeTitle = "Loyal Hound Compact",
                Description = "Steadfast allies (+30 opinion) who forge lasting pacts and defend friends fiercely.",
                IsApexOrSmart = true,
                OpinionModifier = 30,
                BirthTraits = new[] { "honest", "strong", "fast", "content", "eagle_eyed" },
                SubspeciesTraits = new[] { "accelerated_healing" },
                CultureTraits = new[] { "xenophiles", "diplomatic_ascension" }
            });

            Register(new SpeciesProfile
            {
                Id = "capybara",
                DisplayName = "Capybara",
                ArchetypeTitle = "Zen Commune",
                Description = "Universal chill pacifists (+45 opinion) who never start wars and radiate harmony.",
                IsApexOrSmart = true,
                OpinionModifier = 45,
                NeverStartsWars = true,
                BirthTraits = new[] { "peaceful", "content", "honest", "wise", "attractive" },
                SubspeciesTraits = new[] { "amphibia" },
                CultureTraits = new[] { "serenity_now", "xenophiles", "diplomatic_ascension" }
            });

            Register(new SpeciesProfile
            {
                Id = "turtle",
                DisplayName = "Turtle",
                ArchetypeTitle = "Shell Citadel",
                Description = "Patient fortress-keepers (+15 opinion) who never initiate wars and outlast every siege.",
                IsApexOrSmart = false,
                OpinionModifier = 15,
                NeverStartsWars = true,
                BirthTraits = new[] { "tough", "wise", "block", "peaceful" },
                SubspeciesTraits = new[] { "amphibia", "pure" },
                CultureTraits = new[] { "tower_lovers", "elder_reverence", "legacy_keepers" }
            });

            Register(new SpeciesProfile
            {
                Id = "armadillo",
                DisplayName = "Armadillo",
                ArchetypeTitle = "Rolling Aegis",
                Description = "Defensive desert artisans (+10 opinion) who deflect arrows and avoid offensive wars.",
                IsApexOrSmart = false,
                OpinionModifier = 10,
                NeverStartsWars = true,
                BirthTraits = new[] { "tough", "block", "deflect_projectile", "content" },
                SubspeciesTraits = new[] { "adaptation_desert", "walk_adaptation_sand" },
                CultureTraits = new[] { "armorsmith_mastery", "serenity_now" }
            });

            Register(new SpeciesProfile
            {
                Id = "crab",
                DisplayName = "Crab",
                ArchetypeTitle = "Tidal Pinch Cartel",
                Description = "Claw-armored coastal merchants (-5 opinion) who fiercely guard shores and treasure.",
                IsApexOrSmart = false,
                OpinionModifier = -5,
                BirthTraits = new[] { "tough", "greedy", "strong", "block" },
                SubspeciesTraits = new[] { "amphibia" },
                CultureTraits = new[] { "weaponsmith_mastery", "tower_lovers" }
            });

            Register(new SpeciesProfile
            {
                Id = "seal",
                DisplayName = "Seal",
                ArchetypeTitle = "Frost Coast Navigators",
                Description = "Friendly arctic mariners (+20 opinion) thriving across icy coastlines.",
                IsApexOrSmart = false,
                OpinionModifier = 20,
                BirthTraits = new[] { "freeze_proof", "content", "honest", "agile" },
                SubspeciesTraits = new[] { "adaptation_permafrost", "amphibia" },
                CultureTraits = new[] { "xenophiles", "roads" }
            });

            Register(new SpeciesProfile
            {
                Id = "frog",
                DisplayName = "Frog",
                ArchetypeTitle = "Bog Mystics",
                Description = "Regenerative swamp sages (+10 opinion) immune to venom and steeped in wetland lore.",
                IsApexOrSmart = false,
                OpinionModifier = 10,
                BirthTraits = new[] { "poison_immune", "agile", "regeneration", "wise" },
                SubspeciesTraits = new[] { "adaptation_swamp", "amphibia", "walk_adaptation_swamp" },
                CultureTraits = new[] { "reading_lovers", "elder_reverence" }
            });

            Register(new SpeciesProfile
            {
                Id = "scorpion",
                DisplayName = "Scorpion",
                ArchetypeTitle = "Dune Venom Syndicate",
                Description = "Lethal desert assassins (-20 opinion) wielding venom and armored carapaces.",
                IsApexOrSmart = true,
                OpinionModifier = -20,
                BirthTraits = new[] { "venomous", "poisonous", "poison_immune", "savage", "tough" },
                SubspeciesTraits = new[] { "adaptation_desert", "walk_adaptation_sand" },
                CultureTraits = new[] { "xenophobic", "weaponsmith_mastery" }
            });

            Register(new SpeciesProfile
            {
                Id = "rat",
                DisplayName = "Rat",
                ArchetypeTitle = "Under-Empire Swarm",
                Description = "Fast-breeding opportunists (-25 opinion) who overwhelm foes with numbers and guile.",
                IsApexOrSmart = false,
                OpinionModifier = -25,
                BirthTraits = new[] { "fast", "greedy", "poison_immune", "deceitful", "tiny" },
                SubspeciesTraits = new[] { "gestation_short", "population_large" },
                CultureTraits = new[] { "shattered_crown", "gossip_lovers" }
            });

            Register(new SpeciesProfile
            {
                Id = "rabbit",
                DisplayName = "Rabbit",
                ArchetypeTitle = "Burrow Commonwealth",
                Description = "Swift peaceful settlers (+15 opinion) who grow rapidly and never start offensive wars.",
                IsApexOrSmart = false,
                OpinionModifier = 15,
                NeverStartsWars = true,
                BirthTraits = new[] { "fast", "agile", "lucky", "peaceful", "content" },
                SubspeciesTraits = new[] { "gestation_short", "population_large" },
                CultureTraits = new[] { "serenity_now", "roads" }
            });

            Register(new SpeciesProfile
            {
                Id = "sheep",
                DisplayName = "Sheep",
                ArchetypeTitle = "Pastoral Flock",
                Description = "Gentle hillside cooperatives (+20 opinion) committed to peace and mutual defense.",
                IsApexOrSmart = false,
                OpinionModifier = 20,
                NeverStartsWars = true,
                BirthTraits = new[] { "peaceful", "content", "honest" },
                SubspeciesTraits = new[] { "population_large" },
                CultureTraits = new[] { "serenity_now", "true_roots" }
            });

            Register(new SpeciesProfile
            {
                Id = "cow",
                DisplayName = "Cow",
                ArchetypeTitle = "Meadow Matriarchy",
                Description = "Sturdy agrarian communities (+15 opinion) who never initiate wars.",
                IsApexOrSmart = false,
                OpinionModifier = 15,
                NeverStartsWars = true,
                BirthTraits = new[] { "peaceful", "tough", "content", "honest" },
                SubspeciesTraits = new[] { "population_large" },
                CultureTraits = new[] { "serenity_now", "elder_reverence" }
            });

            Register(new SpeciesProfile
            {
                Id = "alpaca",
                DisplayName = "Alpaca",
                ArchetypeTitle = "Highland Fleece Guild",
                Description = "Hardy mountain pacifists (+20 opinion) thriving in cold peaks.",
                IsApexOrSmart = false,
                OpinionModifier = 20,
                NeverStartsWars = true,
                BirthTraits = new[] { "peaceful", "freeze_proof", "honest", "content" },
                SubspeciesTraits = new[] { "cold_resistance" },
                CultureTraits = new[] { "serenity_now", "roads" }
            });

            Register(new SpeciesProfile
            {
                Id = "goat",
                DisplayName = "Goat",
                ArchetypeTitle = "Crag Mountain Rebels",
                Description = "Stubborn highland skirmishers (-5 opinion) who fiercely contest mountain passes.",
                IsApexOrSmart = false,
                OpinionModifier = -5,
                BirthTraits = new[] { "agile", "strong", "ambitious", "tough" },
                SubspeciesTraits = new[] { "cold_resistance" },
                CultureTraits = new[] { "shattered_crown", "hammer_lovers" }
            });

            Register(new SpeciesProfile
            {
                Id = "chicken",
                DisplayName = "Chicken",
                ArchetypeTitle = "Feathered Militias",
                Description = "Plucky fast-multiplying townsfolk (+5 opinion) surprisingly spirited in swarms.",
                IsApexOrSmart = false,
                OpinionModifier = 5,
                BirthTraits = new[] { "fast", "agile", "content", "tiny" },
                SubspeciesTraits = new[] { "gestation_short", "population_large" },
                CultureTraits = new[] { "gossip_lovers", "true_roots" }
            });

            Register(new SpeciesProfile
            {
                Id = "ostrich",
                DisplayName = "Ostrich",
                ArchetypeTitle = "Savanna Striders",
                Description = "High-speed plains runners (0 opinion) dominating open terrain with ranged scouts.",
                IsApexOrSmart = false,
                OpinionModifier = 0,
                BirthTraits = new[] { "fast", "agile", "strong", "eagle_eyed" },
                SubspeciesTraits = new[] { "heat_resistance", "walk_adaptation_sand" },
                CultureTraits = new[] { "bow_lovers", "roads" }
            });

            Register(new SpeciesProfile
            {
                Id = "unicorn",
                DisplayName = "Unicorn",
                ArchetypeTitle = "Mythic Protectors",
                Description = "Radiant blessed guardians (+35 opinion) who never start wars and inspire global awe.",
                IsApexOrSmart = true,
                OpinionModifier = 35,
                NeverStartsWars = true,
                BirthTraits = new[] { "blessed", "wise", "immune", "peaceful", "attractive", "fast" },
                SubspeciesTraits = new[] { "pure", "accelerated_healing" },
                CultureTraits = new[] { "serenity_now", "legacy_keepers", "statue_lovers" }
            });

            Register(new SpeciesProfile
            {
                Id = "piranha",
                DisplayName = "Piranha",
                ArchetypeTitle = "Bloodwater Frenzy",
                Description = "Hyper-aggressive aquatic marauders (-25 opinion) driven by relentless feeding frenzies.",
                IsApexOrSmart = false,
                OpinionModifier = -25,
                BirthTraits = new[] { "bloodlust", "savage", "fast", "agile" },
                SubspeciesTraits = new[] { "amphibia" },
                CultureTraits = new[] { "join_or_die", "xenophobic" }
            });
        }

        private static void Register(SpeciesProfile profile)
        {
            AllSpeciesIds.Add(profile.Id);
            Profiles[profile.Id] = profile;
        }

        public static string NormalizeSpecies(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            string s = id.Trim().ToLowerInvariant();
            if (s.StartsWith("civ_")) s = s.Substring(4);
            return s;
        }

        public static bool TryGetProfile(string speciesId, out SpeciesProfile profile)
        {
            string norm = NormalizeSpecies(speciesId);
            return Profiles.TryGetValue(norm, out profile);
        }

        public static string GetKingdomSpecies(Kingdom k)
        {
            if (k == null) return "";
            string raw = k.data?.original_actor_asset;
            if (string.IsNullOrEmpty(raw))
            {
                try
                {
                    Subspecies sub = k.getMainSubspecies();
                    if (sub != null) raw = SpeciesIdGetter?.Invoke(sub, null) as string;
                }
                catch { }
            }
            return NormalizeSpecies(raw);
        }

        public static bool IsSnakeDeceitfulMode()
        {
            return Main.Settings == null || Main.Settings.DeceitfulSnakesMode;
        }

        public static string GetEffectiveArchetypeTitle(SpeciesProfile profile)
        {
            if (profile == null) return "";
            if (profile.Id == "snake")
                return IsSnakeDeceitfulMode() ? "Serpent's Guile" : "Peaceful Ambush";
            return profile.ArchetypeTitle;
        }

        public static int GetEffectiveOpinionModifier(string speciesId)
        {
            if (!TryGetProfile(speciesId, out var profile)) return 0;
            if (profile.Id == "snake")
                return IsSnakeDeceitfulMode() ? -25 : 0;
            return profile.OpinionModifier;
        }

        public static bool IsNeverStartsWars(string speciesId)
        {
            if (!TryGetProfile(speciesId, out var profile)) return false;
            if (profile.Id == "snake")
                return !IsSnakeDeceitfulMode();
            return profile.NeverStartsWars;
        }

        public static string[] GetEffectiveBirthTraits(SpeciesProfile profile)
        {
            if (profile == null) return Array.Empty<string>();
            if (profile.Id == "snake" && !IsSnakeDeceitfulMode())
            {
                return new[] { "poisonous", "venomous", "poison_immune", "peaceful", "content", "agile" };
            }
            return profile.BirthTraits ?? Array.Empty<string>();
        }

        public static string GetWarCauseAnnotation(long warId)
        {
            return SpeciesWarReasons.TryGetValue(warId, out string reason) ? reason : "";
        }

        public static void EnsureOpinionsRegistered()
        {
            if (!_opinionLocalesRegistered && LocalizedTextManager.instance != null)
            {
                try
                {
                    LM.AddToCurrentLocale("opinion_suite_penguin_tux_pact", "Tux Diplomacy Pact");
                    LM.AddToCurrentLocale("opinion_suite_species_temperament_pos", "Kindred Species Charm");
                    LM.AddToCurrentLocale("opinion_suite_species_temperament_neg", "Species Distrust");
                    if (LocalizedTextManager.stringExists("opinion_suite_penguin_tux_pact"))
                    {
                        _opinionLocalesRegistered = true;
                    }
                }
                catch { }
            }

            if (_opinionsRegistered) return;
            var lib = AssetManager.opinion_library;
            if (lib == null || lib.list == null || lib.list.Count == 0) return;

            try
            {
                if (!lib.has(OpinionPenguinPactId))
                {
                    lib.add(new OpinionAsset
                    {
                        id = OpinionPenguinPactId,
                        translation_key = "opinion_suite_penguin_tux_pact",
                        translation_key_negative = "opinion_suite_penguin_tux_pact",
                        calc = (pMain, pTarget) =>
                        {
                            if (pMain == null || pTarget == null) return 0;
                            if (GetKingdomSpecies(pMain) == "penguin" || GetKingdomSpecies(pTarget) == "penguin")
                                return 60;
                            return 0;
                        }
                    });
                }

                if (!lib.has(OpinionSpeciesTempId))
                {
                    lib.add(new OpinionAsset
                    {
                        id = OpinionSpeciesTempId,
                        translation_key = "opinion_suite_species_temperament_pos",
                        translation_key_negative = "opinion_suite_species_temperament_neg",
                        calc = (pMain, pTarget) =>
                        {
                            if (pMain == null || pTarget == null) return 0;
                            string targetSp = GetKingdomSpecies(pTarget);
                            if (string.IsNullOrEmpty(targetSp) || targetSp == "penguin") return 0;

                            int mod = GetEffectiveOpinionModifier(targetSp);
                            if (mod == 0) return 0;

                            // Same-species kingdoms do not suffer distrust penalties against each other
                            string mainSp = GetKingdomSpecies(pMain);
                            if (mainSp == targetSp && mod < 0) return 0;

                            return mod;
                        }
                    });
                }

                _opinionsRegistered = true;
                Debug.Log("[ApexFactions] Registered species diplomacy OpinionAssets.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ApexFactions] OpinionAsset registration failed: {e.Message}");
            }
        }

        public static void ApplyProfileToSubspecies(Subspecies sub, string speciesId)
        {
            if (sub == null || !TryGetProfile(speciesId, out var profile)) return;

            string[] birthTraits = GetEffectiveBirthTraits(profile);
            for (int i = 0; i < birthTraits.Length; i++)
            {
                string tid = birthTraits[i];
                if (AssetManager.traits != null && AssetManager.traits.has(tid))
                {
                    try { sub.addBirthTrait(tid); } catch { }
                }
            }

            if (profile.SubspeciesTraits != null && AssetManager.subspecies_traits != null)
            {
                for (int i = 0; i < profile.SubspeciesTraits.Length; i++)
                {
                    string stid = profile.SubspeciesTraits[i];
                    if (AssetManager.subspecies_traits.has(stid))
                    {
                        try { sub.addTrait(stid, false); } catch { }
                    }
                }
            }
        }

        public static void ApplyProfileToActor(Actor actor, string speciesId)
        {
            if (actor == null || !actor.isAlive() || !TryGetProfile(speciesId, out var profile)) return;

            string[] birthTraits = GetEffectiveBirthTraits(profile);
            for (int i = 0; i < birthTraits.Length; i++)
            {
                string tid = birthTraits[i];
                if (AssetManager.traits != null && AssetManager.traits.has(tid))
                {
                    try { actor.addTrait(tid, false); } catch { }
                }
            }
            actor.setStatsDirty();
        }

        public static void ApplyProfileToCulture(Culture culture, string speciesId)
        {
            if (culture == null || !TryGetProfile(speciesId, out var profile)) return;
            if (profile.CultureTraits == null || AssetManager.culture_traits == null) return;

            for (int i = 0; i < profile.CultureTraits.Length; i++)
            {
                string ctid = profile.CultureTraits[i];
                if (AssetManager.culture_traits.has(ctid))
                {
                    try { culture.addTrait(ctid, false); } catch { }
                }
            }
        }

        // Enforce species peace doctrines & Penguin Tux Pact protection on AI-initiated non-forced wars
        [HarmonyPrefix]
        [HarmonyPatch(typeof(DiplomacyManager), "startWar")]
        private static bool SpeciesStartWarPrefix(Kingdom pAttacker, Kingdom pDefender, WarTypeAsset pAsset)
        {
            try
            {
                if (pAttacker == null || pDefender == null) return true;
                if (pAsset != null && pAsset.forced_war) return true; // Never block player Whisper of War / forced wars

                string attackerSp = GetKingdomSpecies(pAttacker);
                if (!string.IsNullOrEmpty(attackerSp) && IsNeverStartsWars(attackerSp))
                {
                    return false;
                }

                string defenderSp = GetKingdomSpecies(pDefender);
                if (defenderSp == "penguin")
                {
                    int opinion = 0;
                    try
                    {
                        var op = MapBox.instance?.diplomacy?.getOpinion(pAttacker, pDefender);
                        if (op != null) opinion = op.total;
                    }
                    catch { }

                    if (opinion >= -120)
                    {
                        return false; // Tux / Linux Pact shields Penguins from casual AI wars
                    }
                }
            }
            catch { }

            return true;
        }

        public static void CleanupFinishedWars()
        {
            var wars = MapBox.instance?.wars;
            if (wars == null || SpeciesWarReasons.Count == 0) return;

            List<long> dead = null;
            var warIds = new List<long>(SpeciesWarReasons.Keys);
            for (int i = 0; i < warIds.Count; i++)
            {
                long id = warIds[i];
                War w = wars.get(id);
                if (w == null || !w.isAlive())
                {
                    if (dead == null) dead = new List<long>();
                    dead.Add(id);
                }
            }
            if (dead != null)
            {
                for (int i = 0; i < dead.Count; i++)
                    SpeciesWarReasons.Remove(dead[i]);
            }
        }

        // Serpent's Guile betrayal tick (~45s): deceitful snake kingdoms backstab weaker or distracted allies
        public static void TickSnakeBetrayals()
        {
            if (!IsSnakeDeceitfulMode()) return;
            var world = MapBox.instance;
            if (world?.kingdoms == null || world.diplomacy == null || world.wars == null) return;

            var kingdoms = new List<Kingdom>(world.kingdoms);
            for (int kIdx = 0; kIdx < kingdoms.Count; kIdx++)
            {
                Kingdom snakeKingdom = kingdoms[kIdx];
                if (snakeKingdom == null || !snakeKingdom.isAlive() || !snakeKingdom.isCiv()) continue;
                if (WardenSystem.IsWardenKingdom(snakeKingdom)) continue;
                if (GetKingdomSpecies(snakeKingdom) != "snake") continue;
                if (!snakeKingdom.hasAlliance()) continue;

                Alliance alliance = snakeKingdom.getAlliance();
                if (alliance == null || alliance.kingdoms_list == null || alliance.kingdoms_list.Count < 2) continue;

                int snakeWarriors = Math.Max(1, snakeKingdom.countTotalWarriors());
                Kingdom victim = null;

                var allies = new List<Kingdom>(alliance.kingdoms_list);
                for (int i = 0; i < allies.Count; i++)
                {
                    Kingdom ally = allies[i];
                    if (ally == null || ally == snakeKingdom || !ally.isAlive() || !ally.isCiv()) continue;
                    if (WardenSystem.IsWardenKingdom(ally)) continue;
                    if (GetKingdomSpecies(ally) == "penguin") continue;
                    if (world.wars.isInWarWith(snakeKingdom, ally)) continue;

                    bool weaker = ally.countTotalWarriors() < snakeWarriors * 0.65f;
                    bool distracted = ally.hasEnemies();
                    if (weaker || distracted)
                    {
                        victim = ally;
                        break;
                    }
                }

                if (victim == null) continue;
                if (UnityEngine.Random.value >= 0.25f) continue;

                try
                {
                    alliance.leave(snakeKingdom, true);

                    War war = null;
                    if (StartWarMethod != null)
                    {
                        war = StartWarMethod.Invoke(world.diplomacy,
                            new object[] { snakeKingdom, victim, WarTypeLibrary.normal, true }) as War;
                    }
                    if (war == null)
                    {
                        war = world.wars.newWar(snakeKingdom, victim, WarTypeLibrary.normal);
                    }

                    if (war != null)
                    {
                        SpeciesWarReasons[war.id] = "Snake Betrayal (Serpent's Guile)";
                        string msg = $"{snakeKingdom.name} betrayed ally {victim.name} in a Serpent's Guile ambush!";
                        Debug.Log($"[ApexFactions] {msg}");
                        try { WorldTip.showNowTop(msg, false); } catch { }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ApexFactions] Snake betrayal failed: {e.Message}");
                }
            }
        }

        // Trash-Panda & Vulpine raid tick (~27s): steal stockpile resources from nearby rival cities + pickpocket nearby units
        public static void TickThiefRaids()
        {
            var world = MapBox.instance;
            if (world?.kingdoms == null || world.cities == null) return;

            var kingdoms = new List<Kingdom>(world.kingdoms);
            var thiefKingdoms = new List<Kingdom>();
            for (int i = 0; i < kingdoms.Count; i++)
            {
                Kingdom k = kingdoms[i];
                if (k == null || !k.isAlive() || !k.isCiv()) continue;
                string sp = GetKingdomSpecies(k);
                if (TryGetProfile(sp, out var prof) && prof.IsThiefRaider)
                {
                    thiefKingdoms.Add(k);
                }
            }

            if (thiefKingdoms.Count == 0) return;

            var allCities = new List<City>(world.cities);
            const float maxDistSq = 70f * 70f;

            for (int t = 0; t < thiefKingdoms.Count; t++)
            {
                Kingdom thief = thiefKingdoms[t];
                if (thief == null || !thief.isAlive() || thief.cities == null || thief.cities.Count == 0) continue;

                var myCities = new List<City>(thief.cities);
                Alliance thiefAlliance = thief.hasAlliance() ? thief.getAlliance() : null;
                City bestThiefCity = null;
                City bestVictimCity = null;
                float bestDistSq = maxDistSq;

                for (int vIdx = 0; vIdx < allCities.Count; vIdx++)
                {
                    City victimCity = allCities[vIdx];
                    if (victimCity == null || !victimCity.isAlive()) continue;
                    Kingdom victimKingdom = GetCityKingdom(victimCity);
                    if (victimKingdom == null || victimKingdom == thief || !victimKingdom.isCiv()) continue;
                    if (thiefAlliance != null && thiefAlliance.hasKingdom(victimKingdom)) continue;

                    WorldTile vTile = victimCity.getTile();
                    if (vTile == null) continue;

                    for (int c = 0; c < myCities.Count; c++)
                    {
                        City myCity = myCities[c];
                        if (myCity == null || !myCity.isAlive()) continue;
                        WorldTile mTile = myCity.getTile();
                        if (mTile == null) continue;

                        float dx = mTile.x - vTile.x;
                        float dy = mTile.y - vTile.y;
                        float dSq = dx * dx + dy * dy;
                        if (dSq <= bestDistSq)
                        {
                            bestDistSq = dSq;
                            bestThiefCity = myCity;
                            bestVictimCity = victimCity;
                        }
                    }
                }

                if (bestThiefCity != null && bestVictimCity != null)
                {
                    int stolenKinds = 0;
                    for (int r = 0; r < RaidLootResources.Length && stolenKinds < 3; r++)
                    {
                        string resId = RaidLootResources[r];
                        int avail = bestVictimCity.getResourcesAmount(resId);
                        if (avail <= 2) continue;

                        int stealAmt = Math.Min(avail - 1, resId == "gold" ? 12 : 6);
                        if (stealAmt <= 0) continue;

                        try
                        {
                            bestVictimCity.takeResource(resId, stealAmt);
                            bestThiefCity.addResourcesToRandomStockpile(resId, stealAmt);
                            stolenKinds++;
                        }
                        catch { }
                    }
                }
            }

            // Bounded unit-level pickpocket pass for raccoons & foxes near rival units
            var aliveRaw = world.units?.units_only_alive;
            if (aliveRaw == null || aliveRaw.Count == 0) return;
            var alive = new List<Actor>(aliveRaw);

            var thieves = new List<Actor>(16);
            for (int i = 0; i < alive.Count && thieves.Count < 24; i++)
            {
                Actor u = alive[i];
                if (u == null || !u.isAlive() || u.current_tile == null || u.asset == null) continue;
                if (u.kingdom == null || !u.kingdom.isCiv()) continue;
                string sp = NormalizeSpecies(u.asset.id);
                if (sp == "raccoon" || sp == "fox") thieves.Add(u);
            }

            if (thieves.Count == 0) return;

            int stealsDone = 0;
            for (int i = 0; i < thieves.Count && stealsDone < 8; i++)
            {
                Actor thiefUnit = thieves[i];
                Alliance thiefAlly = thiefUnit.kingdom.hasAlliance() ? thiefUnit.kingdom.getAlliance() : null;
                int tx = thiefUnit.current_tile.x;
                int ty = thiefUnit.current_tile.y;

                for (int j = 0; j < alive.Count; j++)
                {
                    Actor target = alive[j];
                    if (target == null || target == thiefUnit || !target.isAlive() || target.current_tile == null) continue;
                    if (target.kingdom == null || !target.kingdom.isCiv() || target.kingdom == thiefUnit.kingdom) continue;
                    if (thiefAlly != null && thiefAlly.hasKingdom(target.kingdom)) continue;

                    int dx = tx - target.current_tile.x;
                    int dy = ty - target.current_tile.y;
                    if (dx * dx + dy * dy <= 36) // within 6 tiles
                    {
                        try
                        {
                            if (thiefUnit.tryToStealItems(target, false))
                            {
                                stealsDone++;
                                break;
                            }
                        }
                        catch { }
                    }
                }
            }
        }

        private static Kingdom GetCityKingdom(City city)
        {
            if (city == null || CityKingdomField == null) return null;
            try { return CityKingdomField.GetValue(city) as Kingdom; }
            catch { return null; }
        }
    }
}
