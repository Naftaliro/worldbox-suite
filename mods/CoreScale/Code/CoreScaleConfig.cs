using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace CoreScale
{
    [Serializable]
    public class CoreScaleConfig
    {
        public bool EnableCustomMapSizes = true;
        public bool EnableAdaptiveLOD = true;
        public bool SkipAnimationsInStrategicZoom = true;
        public bool UnlockParallelism = true;
        public bool ForceDisableShadows = false;
        public bool EnableBattalionCompression = true;
        public int BattalionPopThreshold = 8000;
        public int MinArmySizeForCompression = 24;
        public int SoldiersPerRegimentMerge = 6;

        // Population thresholds for progressive batch time-slicing
        public int LodTier1Population = 3000;
        public int LodTier2Population = 8000;
        public int LodTier3Population = 15000;

        // Camera zoom bounds for massive and stitched maps
        public float ZoomOutMultiplier = 2.4f;
        public float ZoomInMin = 4.0f;

        public KeyCode OverlayToggleKey = KeyCode.F8;

        public static CoreScaleConfig LoadOrCreate(string modFolder)
        {
            string path = Path.Combine(modFolder, "corescale_config.json");
            try
            {
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var loaded = JsonConvert.DeserializeObject<CoreScaleConfig>(json);
                    if (loaded != null) return loaded;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CoreScale] Failed to read config, using defaults: {e.Message}");
            }

            var cfg = new CoreScaleConfig();
            cfg.Save(modFolder);
            return cfg;
        }

        public void Save(string modFolder)
        {
            try
            {
                string path = Path.Combine(modFolder, "corescale_config.json");
                File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CoreScale] Failed to save config: {e.Message}");
            }
        }
    }
}
