using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace VisualPolish
{
    public enum VisualPreset
    {
        Vanilla = 0,
        CrispVibrant = 1,
        GoldenHour = 2,
        MoonlitNight = 3,
        ParchmentAtlas = 4,
        MochaTwilight = 5
    }

    [Serializable]
    public class VisualPolishConfig
    {
        public VisualPreset Preset = VisualPreset.Vanilla;
        public bool DynamicDayNightCycle = false;
        public float CycleDurationSeconds = 180f;
        public float VignetteStrength = 0f;
        public float TintStrength = 0.14f;
        public bool UnlockCloseZoom = true;
        public float CloseZoomMin = 2.5f;
        public float StrategicBorderBoost = 1.25f;
        public bool AutoDistinctKingdomColors = true;
        public bool EnhancedBorders = true;
        public bool ExtendedHdZoom = true;
        public float HdZoomMultiplier = 2.5f;
        public bool AlwaysDayClientSide = false;
        public bool LockAgeOfHope = false;

        public KeyCode PanelToggleKey = KeyCode.F9;
        public KeyCode PhotoModeKey = KeyCode.F6;

        public static VisualPolishConfig LoadOrCreate(string modFolder)
        {
            string path = Path.Combine(modFolder, "visualpolish_config.json");
            try
            {
                if (File.Exists(path))
                {
                    var loaded = JsonConvert.DeserializeObject<VisualPolishConfig>(File.ReadAllText(path));
                    if (loaded != null) return loaded;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VisualPolish] Config load failed, using defaults: {e.Message}");
            }

            var cfg = new VisualPolishConfig();
            cfg.Save(modFolder);
            return cfg;
        }

        public void Save(string modFolder)
        {
            try
            {
                File.WriteAllText(
                    Path.Combine(modFolder, "visualpolish_config.json"),
                    JsonConvert.SerializeObject(this, Formatting.Indented)
                );
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VisualPolish] Config save failed: {e.Message}");
            }
        }
    }
}
