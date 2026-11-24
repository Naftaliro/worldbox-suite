using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace ModernAge
{
    public enum GovernmentType
    {
        Monarchy = 0,
        Republic = 1,
        Collectivist = 2,
        MerchantLeague = 3,
        MilitaryJunta = 4,
        Technocracy = 5
    }

    public enum TradeSpecialty
    {
        Balanced = 0,
        Breadbasket = 1,
        ArmoryState = 2,
        MaritimeHub = 3,
        ResearchHub = 4
    }

    [Serializable]
    public class ModernAgeConfig
    {
        public bool EnableAutoProgression = true;
        public bool EnableHighways = true;
        public bool EnableRadarUmbrella = true;
        public bool ShowRadarRings = true;
        public bool EnableTradeAndLendLease = true;

        // Tier unlock population thresholds (Technocracy/Research allies unlock 25% earlier)
        public int Tier1Pop = 140;
        public int Tier2Pop = 320;
        public int Tier3Pop = 580;

        // Rock-paper-scissors balance multipliers
        public float BastionBulletReduction = 0.60f;
        public float HalberdVsGunBonus = 0.45f;
        public float AntiTankBonus = 1.65f;
        public float AntiAirBonus = 1.85f;
        public float RadarRangeTiles = 44f;

        public static ModernAgeConfig LoadOrCreate(string modFolder)
        {
            string path = Path.Combine(modFolder, "modernage_config.json");
            try
            {
                if (File.Exists(path))
                {
                    var loaded = JsonConvert.DeserializeObject<ModernAgeConfig>(File.ReadAllText(path));
                    if (loaded != null) return loaded;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ModernAge] Config load failed, using defaults: {e.Message}");
            }

            var cfg = new ModernAgeConfig();
            cfg.Save(modFolder);
            return cfg;
        }

        public void Save(string modFolder)
        {
            try
            {
                File.WriteAllText(
                    Path.Combine(modFolder, "modernage_config.json"),
                    JsonConvert.SerializeObject(this, Formatting.Indented)
                );
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ModernAge] Config save failed: {e.Message}");
            }
        }
    }
}
