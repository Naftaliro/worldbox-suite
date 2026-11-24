using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace Arsenal
{
    [Serializable]
    public class ArsenalConfig
    {
        public bool EnableAutoSupply = true;
        public bool EraGateEnabled = true;
        public int UnlockPopulation = 250;
        public int UnlockCities = 3;

        // Rock-paper-scissors tuning
        public float BastionBulletReduction = 0.60f;   // 60% damage reduction vs firearms
        public float HalberdVsGunBonus = 0.45f;        // +45% bonus damage vs firearm wielders
        public float AegisShieldCooldown = 12f;        // seconds between auto ward triggers

        public static ArsenalConfig LoadOrCreate(string modFolder)
        {
            string path = Path.Combine(modFolder, "arsenal_config.json");
            try
            {
                if (File.Exists(path))
                {
                    var loaded = JsonConvert.DeserializeObject<ArsenalConfig>(File.ReadAllText(path));
                    if (loaded != null) return loaded;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Arsenal] Config load failed, using defaults: {e.Message}");
            }

            var cfg = new ArsenalConfig();
            cfg.Save(modFolder);
            return cfg;
        }

        public void Save(string modFolder)
        {
            try
            {
                File.WriteAllText(
                    Path.Combine(modFolder, "arsenal_config.json"),
                    JsonConvert.SerializeObject(this, Formatting.Indented)
                );
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Arsenal] Config save failed: {e.Message}");
            }
        }
    }
}
