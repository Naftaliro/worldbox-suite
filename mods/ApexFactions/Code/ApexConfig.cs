using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace ApexFactions
{
    [Serializable]
    public class ApexFactionsConfig
    {
        // Warden "Sleeping Giant" Doctrine
        public bool EnableWardenDoctrine = true;
        public float MercyThresholdPercent = 15f; // Stop retaliation once aggressor drops to 15% of pre-war pop
        public int MinSurvivorsFloor = 4;

        // Auto-Founding Literal Animal Kingdoms
        public bool AutoFoundAnimalKingdoms = true;
        public int ClusterSizeToFound = 10; // range 4..25
        public float ClusterRadiusTiles = 18f;
        public string AnimalFoundingPreset = "AllEligible"; // "AllEligible", "ApexAndSmart", "Custom"
        public bool DeceitfulSnakesMode = true; // true = Serpent's Guile backstabbers, false = Peaceful Ambush isolationists
        public Dictionary<string, bool> SpeciesAllowed = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        // Wild Sovereignty / Feral Pack Domains
        public bool EnableFeralDomains = true;
        public bool ShowFeralDomainRings = false;
        public int MinPackSizeForDomain = 5;
        public float FeralDomainRadiusTiles = 26f;

        public KeyCode HudToggleKey = KeyCode.F12;

        public void EnsureDefaults()
        {
            ClusterSizeToFound = Mathf.Clamp(ClusterSizeToFound, 4, 25);
            ClusterRadiusTiles = Mathf.Clamp(ClusterRadiusTiles, 8f, 40f);
            MercyThresholdPercent = Mathf.Clamp(MercyThresholdPercent, 0f, 50f);

            if (string.IsNullOrEmpty(AnimalFoundingPreset) ||
                (AnimalFoundingPreset != "AllEligible" && AnimalFoundingPreset != "ApexAndSmart" && AnimalFoundingPreset != "Custom"))
            {
                AnimalFoundingPreset = "AllEligible";
            }

            // Re-wrap with case-insensitive comparer after JSON deserialization
            var normalized = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            if (SpeciesAllowed != null)
            {
                foreach (var kv in SpeciesAllowed)
                {
                    if (!string.IsNullOrEmpty(kv.Key))
                        normalized[SpeciesPersonalityRegistry.NormalizeSpecies(kv.Key)] = kv.Value;
                }
            }
            SpeciesAllowed = normalized;

            // Ensure all 30 verified normal animal species exist in SpeciesAllowed
            foreach (string sp in SpeciesPersonalityRegistry.AllSpeciesIds)
            {
                if (!SpeciesAllowed.ContainsKey(sp))
                {
                    SpeciesAllowed[sp] = DefaultAllowedForPreset(sp, AnimalFoundingPreset);
                }
            }
        }

        private static bool DefaultAllowedForPreset(string speciesId, string preset)
        {
            if (preset == "ApexAndSmart")
            {
                return SpeciesPersonalityRegistry.TryGetProfile(speciesId, out var p) && p.IsApexOrSmart;
            }
            return true;
        }

        public void ApplyPreset(string preset)
        {
            AnimalFoundingPreset = preset;
            if (preset == "AllEligible" || preset == "ApexAndSmart")
            {
                foreach (string sp in SpeciesPersonalityRegistry.AllSpeciesIds)
                {
                    SpeciesAllowed[sp] = DefaultAllowedForPreset(sp, preset);
                }
            }
        }

        public bool IsSpeciesAllowedToFound(string speciesId)
        {
            string norm = SpeciesPersonalityRegistry.NormalizeSpecies(speciesId);
            if (string.IsNullOrEmpty(norm)) return false;
            if (!SpeciesPersonalityRegistry.Profiles.ContainsKey(norm)) return false;

            if (AnimalFoundingPreset == "AllEligible") return true;
            if (AnimalFoundingPreset == "ApexAndSmart")
                return SpeciesPersonalityRegistry.TryGetProfile(norm, out var p) && p.IsApexOrSmart;

            return SpeciesAllowed != null && SpeciesAllowed.TryGetValue(norm, out bool allowed) && allowed;
        }

        public void SetSpeciesAllowed(string speciesId, bool allowed)
        {
            string norm = SpeciesPersonalityRegistry.NormalizeSpecies(speciesId);
            if (string.IsNullOrEmpty(norm)) return;
            if (SpeciesAllowed == null)
                SpeciesAllowed = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            // Sync current effective values before flipping into Custom mode
            if (AnimalFoundingPreset != "Custom")
            {
                foreach (string sp in SpeciesPersonalityRegistry.AllSpeciesIds)
                {
                    SpeciesAllowed[sp] = IsSpeciesAllowedToFound(sp);
                }
                AnimalFoundingPreset = "Custom";
            }

            SpeciesAllowed[norm] = allowed;
        }

        public static ApexFactionsConfig LoadOrCreate(string modFolder)
        {
            string path = Path.Combine(modFolder, "apexfactions_config.json");
            try
            {
                if (File.Exists(path))
                {
                    var loaded = JsonConvert.DeserializeObject<ApexFactionsConfig>(File.ReadAllText(path));
                    if (loaded != null)
                    {
                        loaded.EnsureDefaults();
                        return loaded;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ApexFactions] Config load failed, using defaults: {e.Message}");
            }

            var cfg = new ApexFactionsConfig();
            cfg.EnsureDefaults();
            cfg.Save(modFolder);
            return cfg;
        }

        public void Save(string modFolder)
        {
            try
            {
                File.WriteAllText(
                    Path.Combine(modFolder, "apexfactions_config.json"),
                    JsonConvert.SerializeObject(this, Formatting.Indented)
                );
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ApexFactions] Config save failed: {e.Message}");
            }
        }
    }
}
