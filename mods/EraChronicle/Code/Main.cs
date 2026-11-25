using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using NeoModLoader.api;
using Newtonsoft.Json;
using UnityEngine;

namespace EraChronicle
{
    [Serializable]
    public class EraChronicleConfig
    {
        public bool EnableEraProgression = true;
        public bool EnableAutoSnapshots = true;
        public int SnapshotIntervalYears = 5;
        public KeyCode ChronicleHudKey = KeyCode.F7;

        public static EraChronicleConfig LoadOrCreate(string modFolder)
        {
            string path = Path.Combine(modFolder, "erachronicle_config.json");
            try
            {
                if (File.Exists(path))
                {
                    var loaded = JsonConvert.DeserializeObject<EraChronicleConfig>(File.ReadAllText(path));
                    if (loaded != null) return loaded;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[EraChronicle] Config load failed, using defaults: {e.Message}");
            }

            var cfg = new EraChronicleConfig();
            cfg.Save(modFolder);
            return cfg;
        }

        public void Save(string modFolder)
        {
            try
            {
                File.WriteAllText(
                    Path.Combine(modFolder, "erachronicle_config.json"),
                    JsonConvert.SerializeObject(this, Formatting.Indented)
                );
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[EraChronicle] Config save failed: {e.Message}");
            }
        }
    }

    [Serializable]
    public class KingdomSnapshot
    {
        public long Id;
        public string Name;
        public string Era;
        public int Population;
        public int Cities;
        public string King;
    }

    [Serializable]
    public class WarSnapshot
    {
        public long Id;
        public string Name;
        public long TotalDeaths;
    }

    [Serializable]
    public class ChronicleEntry
    {
        public int Year;
        public string WorldAge;
        public int TotalPopulation;
        public List<KingdomSnapshot> Kingdoms = new List<KingdomSnapshot>();
        public List<WarSnapshot> ActiveWars = new List<WarSnapshot>();
        public List<string> Events = new List<string>();
    }

    [Serializable]
    public class WorldChronicleFile
    {
        public string WorldName;
        public List<ChronicleEntry> Entries = new List<ChronicleEntry>();
    }

    public class Main : MonoBehaviour, IMod
    {
        public static Main Instance { get; private set; }
        public static EraChronicleConfig Settings { get; private set; }

        private static readonly FieldInfo MapStatsField = AccessTools.Field(typeof(MapBox), "map_stats");
        private static readonly string[] EraNames =
        {
            "Dawn Age",
            "Bronze Kingdoms",
            "Iron Empires",
            "Gunpowder & Sail",
            "High Hegemony"
        };

        private ModDeclare _declare;
        private GameObject _gameObject;
        private string _chroniclesDir;

        private bool _showHud = false;
        private GuiOverlayRunner _panelRunner;
        private int _lastSnapshotYear = -1;
        private float _pollTimer = 0f;
        private readonly Dictionary<long, int> _kingdomEraTier = new Dictionary<long, int>();
        private readonly List<string> _recentEvents = new List<string>();
        private Rect _windowRect = new Rect(360, 320, 330, 315);

        public ModDeclare GetDeclaration() => _declare;
        public GameObject GetGameObject() => _gameObject;
        public string GetUrl() => "https://github.com/naftaliro/worldbox-suite";

        public void OnLoad(ModDeclare pModDecl, GameObject pGameObject)
        {
            Instance = this;
            _declare = pModDecl;
            _gameObject = pGameObject;
            _panelRunner = pGameObject.AddComponent<GuiOverlayRunner>();
            _panelRunner.Configure(true, DrawPanelGui);
            Settings = EraChronicleConfig.LoadOrCreate(_declare.FolderPath);

            _chroniclesDir = Path.Combine(_declare.FolderPath, "chronicles");
            Directory.CreateDirectory(_chroniclesDir);

            Debug.Log($"[EraChronicle] Loaded v{_declare.Version} (chronicles={_chroniclesDir})");
        }

        public void OnUnload()
        {
            Debug.Log("[EraChronicle] Unloaded.");
        }

        public void ToggleHud()
        {
            _showHud = !_showHud;
            if (_panelRunner != null) _panelRunner.enabled = _showHud;
        }

        public bool GetState(string key)
        {
            if (Settings == null) return false;
            if (key == "eras") return Settings.EnableEraProgression;
            if (key == "snapshots") return Settings.EnableAutoSnapshots;
            return false;
        }

        public string ToggleEraProgression()
        {
            Settings.EnableEraProgression = !Settings.EnableEraProgression;
            Settings.Save(_declare.FolderPath);
            return $"Era Progression: {(Settings.EnableEraProgression ? "ON" : "OFF")}";
        }

        public string ToggleAutoSnapshots()
        {
            Settings.EnableAutoSnapshots = !Settings.EnableAutoSnapshots;
            Settings.Save(_declare.FolderPath);
            return $"Auto Chronicles: {(Settings.EnableAutoSnapshots ? "ON" : "OFF")}";
        }

        public string RecordSnapshotNow()
        {
            var stats = GetMapStats();
            if (stats == null) return "Load a world to record a chronicle snapshot.";
            RecordSnapshot(stats);
            _lastSnapshotYear = stats.history_current_year;
            return $"Saved chronicle snapshot for Year {stats.history_current_year}!";
        }

        private static MapStats GetMapStats()
        {
            var world = MapBox.instance;
            return world != null ? MapStatsField?.GetValue(world) as MapStats : null;
        }

        private void Update()
        {
            if (Settings == null) return;

            if (Input.GetKeyDown(Settings.ChronicleHudKey))
            {
                ToggleHud();
            }

            if (Config.paused || MapBox.instance == null) return;

            _pollTimer += Time.deltaTime;
            if (_pollTimer < 2.0f) return;
            _pollTimer = 0f;

            var stats = GetMapStats();
            if (stats == null) return;

            int year = stats.history_current_year;

            if (Settings.EnableEraProgression)
            {
                EvaluateKingdomEras(year);
            }

            if (Settings.EnableAutoSnapshots)
            {
                int interval = Math.Max(1, Settings.SnapshotIntervalYears);
                if (_lastSnapshotYear < 0 || year >= _lastSnapshotYear + interval)
                {
                    RecordSnapshot(stats);
                    _lastSnapshotYear = year;
                }
            }
        }

        private static int ComputeEraTier(Kingdom k)
        {
            int pop = k.getPopulationPeople();
            int cities = k.cities != null ? k.cities.Count : 0;
            if (pop >= 800 && cities >= 8) return 4;
            if (pop >= 400 && cities >= 5) return 3;
            if (pop >= 160 && cities >= 3) return 2;
            if (pop >= 60 && cities >= 2) return 1;
            return 0;
        }

        private void EvaluateKingdomEras(int year)
        {
            var world = MapBox.instance;
            if (world?.kingdoms == null) return;

            var kingdoms = new List<Kingdom>(world.kingdoms);
            for (int i = 0; i < kingdoms.Count; i++)
            {
                Kingdom k = kingdoms[i];
                if (k == null || !k.isCiv() || !k.isAlive()) continue;

                int tier = ComputeEraTier(k);
                _kingdomEraTier.TryGetValue(k.id, out int prevTier);
                if (tier > prevTier)
                {
                    _kingdomEraTier[k.id] = tier;
                    string eraName = EraNames[Math.Min(tier, EraNames.Length - 1)];
                    string ev = $"Year {year}: {k.name} entered the {eraName}!";
                    _recentEvents.Add(ev);
                    if (_recentEvents.Count > 24) _recentEvents.RemoveAt(0);
                    GrantEraMilestoneBonus(k, tier);
                    Debug.Log($"[EraChronicle] {ev}");
                }
            }
        }

        private static void GrantEraMilestoneBonus(Kingdom k, int tier)
        {
            // Reward the ruling king and active army soldiers on era advancement
            if (k.king != null && k.king.isAlive())
            {
                k.king.restoreHealthPercent(1f);
                if (tier >= 2) k.king.addTrait("wise");
                if (tier >= 4) k.king.addTrait("blessed");
            }

            var aliveRaw = MapBox.instance?.units?.units_only_alive;
            if (aliveRaw == null || aliveRaw.Count == 0) return;
            var alive = new List<Actor>(aliveRaw);
            int buffed = 0;
            for (int i = 0; i < alive.Count && buffed < 30; i++)
            {
                Actor u = alive[i];
                if (u != null && u.isAlive() && u.kingdom == k && u.army != null)
                {
                    u.restoreHealthPercent(0.5f);
                    if (tier >= 3) u.addTrait("veteran");
                    buffed++;
                }
            }
        }

        public void RecordSnapshot(MapStats stats)
        {
            var world = MapBox.instance;
            if (world == null || stats == null) return;

            string worldName = string.IsNullOrEmpty(stats.name) ? "Untitled_World" : stats.name;
            string safeName = SanitizeFileName(worldName);
            string filePath = Path.Combine(_chroniclesDir, $"{safeName}_chronicle.json");

            WorldChronicleFile doc;
            try
            {
                doc = File.Exists(filePath)
                    ? JsonConvert.DeserializeObject<WorldChronicleFile>(File.ReadAllText(filePath)) ?? new WorldChronicleFile()
                    : new WorldChronicleFile();
            }
            catch
            {
                doc = new WorldChronicleFile();
            }

            doc.WorldName = worldName;

            var entry = new ChronicleEntry
            {
                Year = stats.history_current_year,
                WorldAge = stats.world_age_id ?? "",
                TotalPopulation = world.units?.units_only_alive?.Count ?? 0,
                Events = new List<string>(_recentEvents)
            };
            _recentEvents.Clear();

            if (world.kingdoms != null)
            {
                var kingdoms = new List<Kingdom>(world.kingdoms);
                for (int i = 0; i < kingdoms.Count; i++)
                {
                    Kingdom k = kingdoms[i];
                    if (k == null || !k.isCiv() || !k.isAlive()) continue;
                    int tier = _kingdomEraTier.TryGetValue(k.id, out int t) ? t : ComputeEraTier(k);
                    entry.Kingdoms.Add(new KingdomSnapshot
                    {
                        Id = k.id,
                        Name = k.name,
                        Era = EraNames[Math.Min(tier, EraNames.Length - 1)],
                        Population = k.getPopulationPeople(),
                        Cities = k.cities != null ? k.cities.Count : 0,
                        King = k.king != null && k.king.isAlive() ? k.king.getName() : ""
                    });
                }
            }

            if (world.wars != null)
            {
                var activeWars = world.wars.getActiveWars();
                if (activeWars != null)
                {
                    var wars = new List<War>(activeWars);
                    for (int i = 0; i < wars.Count; i++)
                    {
                        War w = wars[i];
                        if (w == null) continue;
                        entry.ActiveWars.Add(new WarSnapshot
                        {
                            Id = w.id,
                            Name = w.name,
                            TotalDeaths = w.getDeadAttackers() + w.getDeadDefenders()
                        });
                    }
                }
            }

            doc.Entries.Add(entry);
            try
            {
                Directory.CreateDirectory(_chroniclesDir);
                File.WriteAllText(filePath, JsonConvert.SerializeObject(doc, Formatting.Indented));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[EraChronicle] Failed to write chronicle: {e.Message}");
            }
        }

        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name.Replace(' ', '_');
        }

        private void DrawPanelGui()
        {
            if (!_showHud || Settings == null) return;
            GameUiStyle.BeginFrame();
            _windowRect = GUI.Window(941207, _windowRect, DrawHud, "EraChronicle (F7)", GameUiStyle.WindowStyle);
            GameUiStyle.DrawTooltip(949006);
        }

        private void DrawHud(int id)
        {
            GameUiStyle.DrawCloseButton(_windowRect, ToggleHud);
            var stats = GetMapStats();
            int year = stats != null ? stats.history_current_year : 0;

            GameUiStyle.Label($"Year {year}  |  Tracked Kingdoms: {_kingdomEraTier.Count}", "Chronicle Status|Current world year and how many kingdoms have unlocked era milestones so far.", isHeader: true);
            GameUiStyle.Label($"Last Snapshot Year: {(_lastSnapshotYear < 0 ? "None" : _lastSnapshotYear.ToString())}", "Snapshot History|Most recent year saved to JSON for population and war charts in WorldBox Studio.");
            GUILayout.Space(6);

            if (GameUiStyle.ToggleButton(
                $"Era Progression: {(Settings.EnableEraProgression ? "ON" : "OFF")}",
                Settings.EnableEraProgression,
                "5-Tier Kingdom Eras|Advances kingdoms from Dawn Age up to High Hegemony based on population and cities, granting kings and soldiers milestone traits."))
            {
                Settings.EnableEraProgression = !Settings.EnableEraProgression;
                Settings.Save(_declare.FolderPath);
            }

            if (GameUiStyle.ToggleButton(
                $"Auto Snapshots (every {Settings.SnapshotIntervalYears}y): {(Settings.EnableAutoSnapshots ? "ON" : "OFF")}",
                Settings.EnableAutoSnapshots,
                "Automated World Chronicle|Periodically writes kingdom populations, eras, and war casualties to JSON files readable in WorldBox Studio."))
            {
                Settings.EnableAutoSnapshots = !Settings.EnableAutoSnapshots;
                Settings.Save(_declare.FolderPath);
            }

            if (GameUiStyle.Button(
                "Record Chronicle Snapshot Now",
                "Save Snapshot Immediately|Captures a fresh history entry for the current year and saves it to EraChronicle/chronicles/.") && stats != null)
            {
                RecordSnapshot(stats);
                _lastSnapshotYear = year;
            }

            GUILayout.Space(6);
            GameUiStyle.Label("Recent Milestones:", "Recent Era Events|Latest kingdoms to ascend to a higher historical era.", isHeader: true);
            int shown = 0;
            for (int i = _recentEvents.Count - 1; i >= 0 && shown < 4; i--, shown++)
            {
                GameUiStyle.Subtle("• " + _recentEvents[i]);
            }

            GameUiStyle.CaptureTooltip();
            GUI.DragWindow();
        }
    }
}
