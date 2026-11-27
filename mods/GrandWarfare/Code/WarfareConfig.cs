using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace GrandWarfare
{
    [Serializable]
    public class GrandWarfareConfig
    {
        public bool EnableNavalCombat = true;
        public float NavalAttackRangeTiles = 26f;
        public float NavalVolleyCooldown = 3.0f;
        public int NavalDamage = 18;
        public bool EnableCoastalBombardment = true;
        public bool EnableNavalBlockades = true;
        public float BlockadeRangeTiles = 24f;

        public bool EnableSupplyAttrition = true;
        public float MaxSupplyDistanceTiles = 135f;
        public float SupplyCheckInterval = 6.0f;
        public int AttritionHealthLoss = 4;

        public bool EnableDefensePacts = true;
        public bool EnableEspionage = true;
        public bool EnableAutoEspionage = false;
        public bool ChaoticEspionageMode = false;
        public bool EnableGeopoliticsAndTrade = true;
        public KeyCode WarRoomKey = KeyCode.F10;

        public static GrandWarfareConfig LoadOrCreate(string modFolder)
        {
            string path = Path.Combine(modFolder, "grandwarfare_config.json");
            try
            {
                if (File.Exists(path))
                {
                    var loaded = JsonConvert.DeserializeObject<GrandWarfareConfig>(File.ReadAllText(path));
                    if (loaded != null) return loaded;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GrandWarfare] Config load failed, using defaults: {e.Message}");
            }

            var cfg = new GrandWarfareConfig();
            cfg.Save(modFolder);
            return cfg;
        }

        public void Save(string modFolder)
        {
            try
            {
                File.WriteAllText(
                    Path.Combine(modFolder, "grandwarfare_config.json"),
                    JsonConvert.SerializeObject(this, Formatting.Indented)
                );
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GrandWarfare] Config save failed: {e.Message}");
            }
        }
    }
}
