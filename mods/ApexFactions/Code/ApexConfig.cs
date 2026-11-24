using System;
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

        // Wild Sovereignty / Feral Pack Domains
        public bool EnableFeralDomains = true;
        public bool ShowFeralDomainRings = true;
        public int MinPackSizeForDomain = 5;
        public float FeralDomainRadiusTiles = 26f;

        public KeyCode HudToggleKey = KeyCode.F12;

        public static ApexFactionsConfig LoadOrCreate(string modFolder)
        {
            string path = Path.Combine(modFolder, "apexfactions_config.json");
            try
            {
                if (File.Exists(path))
                {
                    var loaded = JsonConvert.DeserializeObject<ApexFactionsConfig>(File.ReadAllText(path));
                    if (loaded != null) return loaded;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ApexFactions] Config load failed, using defaults: {e.Message}");
            }

            var cfg = new ApexFactionsConfig();
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
