using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace LinkedContinents
{
    public enum OceanEdge
    {
        East = 0,
        West = 1,
        North = 2,
        South = 3
    }

    [Serializable]
    public class PortalUnitRecord
    {
        public string SpeciesId;
        public string Name;
        public List<string> Traits = new List<string>();
        public int Level;
        public int Health;
    }

    [Serializable]
    public class PortalFleetPacket
    {
        public string Id;
        public string SourceWorld;
        public string TargetWorld;
        public string KingdomName;
        public OceanEdge ArrivalEdge;
        public string TimestampUtc;
        public List<PortalUnitRecord> Units = new List<PortalUnitRecord>();
    }

    [Serializable]
    public class LinkedContinentsConfig
    {
        public bool AutoEdgeTransfers = true;
        public bool AutoReceiveOnLoad = true;
        public int EdgeBandTiles = 6;
        public float ScanIntervalSeconds = 4.0f;
        public OceanEdge OutboundEdge = OceanEdge.East;
        public string LinkedTargetWorld = "Never Box";
        public KeyCode PortalHudKey = KeyCode.F11;

        public static LinkedContinentsConfig LoadOrCreate(string modFolder)
        {
            string path = Path.Combine(modFolder, "linked_continents_config.json");
            try
            {
                if (File.Exists(path))
                {
                    var loaded = JsonConvert.DeserializeObject<LinkedContinentsConfig>(File.ReadAllText(path));
                    if (loaded != null) return loaded;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LinkedContinents] Config load failed, using defaults: {e.Message}");
            }

            var cfg = new LinkedContinentsConfig();
            cfg.Save(modFolder);
            return cfg;
        }

        public void Save(string modFolder)
        {
            try
            {
                File.WriteAllText(
                    Path.Combine(modFolder, "linked_continents_config.json"),
                    JsonConvert.SerializeObject(this, Formatting.Indented)
                );
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LinkedContinents] Config save failed: {e.Message}");
            }
        }
    }
}
