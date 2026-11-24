using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace CoopHost
{
    [Serializable]
    public class CoopHostConfig
    {
        public int Port = 27777;
        public string JoinPin = "4829";
        public bool AutoStartServer = false;
        public bool ShowGuestCursorInGame = true;
        public int MaxStreamedUnits = 1200;

        public static CoopHostConfig LoadOrCreate(string modFolder)
        {
            string path = Path.Combine(modFolder, "coophost_config.json");
            try
            {
                if (File.Exists(path))
                {
                    var loaded = JsonConvert.DeserializeObject<CoopHostConfig>(File.ReadAllText(path));
                    if (loaded != null) return loaded;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CoopHost] Config load failed, using defaults: {e.Message}");
            }

            var cfg = new CoopHostConfig
            {
                JoinPin = new System.Random().Next(1000, 9999).ToString()
            };
            cfg.Save(modFolder);
            return cfg;
        }

        public void Save(string modFolder)
        {
            try
            {
                File.WriteAllText(
                    Path.Combine(modFolder, "coophost_config.json"),
                    JsonConvert.SerializeObject(this, Formatting.Indented)
                );
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CoopHost] Config save failed: {e.Message}");
            }
        }
    }
}
