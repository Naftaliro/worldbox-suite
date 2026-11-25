using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace WorldRelics
{
    [Serializable]
    public class WorldRelicsConfig
    {
        public bool EnableCityWalls = true;
        public int MinPopForWalls = 85;
        public int MinPopForIronWalls = 260;

        public bool EnableSeaBridges = true;
        public int MaxBridgeWaterTiles = 28;

        public bool EnableRuinDecay = true;
        public int AncientStageYears = 45;
        public int MossyStageYears = 120;
        public int WildOvergrowthYears = 210;

        public bool EnableDenseCities = false;

        public bool TectonicSlowGeological = false;
        public int TectonicMaxDriftSteps = 14;
        public bool EnableNaturalTectonicEvents = false;

        public static WorldRelicsConfig LoadOrCreate(string modFolder)
        {
            string path = Path.Combine(modFolder, "worldrelics_config.json");
            try
            {
                if (File.Exists(path))
                {
                    var loaded = JsonConvert.DeserializeObject<WorldRelicsConfig>(File.ReadAllText(path));
                    if (loaded != null) return loaded;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[WorldRelics] Config load failed, using defaults: {e.Message}");
            }

            var cfg = new WorldRelicsConfig();
            cfg.Save(modFolder);
            return cfg;
        }

        public void Save(string modFolder)
        {
            try
            {
                File.WriteAllText(
                    Path.Combine(modFolder, "worldrelics_config.json"),
                    JsonConvert.SerializeObject(this, Formatting.Indented)
                );
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[WorldRelics] Config save failed: {e.Message}");
            }
        }
    }
}
