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
        private int _lastSnapshotYear = -1;
        private float _pollTimer = 0f;
        private readonly Dictionary<long, int> _kingdomEraTier = new Dictionary<long, int>();
        private readonly List<string> _recentEvents = new List<string>();
        private Rect _windowRect = new Rect(360, 320, 320, 290);

        private Texture2D _bgTex;
        private Texture2D _btnTex;
        private GUIStyle _boxStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _btnStyle;

        public ModDeclare GetDeclaration() => _declare;
        public GameObject GetGameObject() => _gameObject;
        public string GetUrl() => "https://github.com/naftaliro/worldbox-suite";

        public void OnLoad(ModDeclare pModDecl, GameObject pGameObject)
        {
            Instance = this;
            _declare = pModDecl;
            _gameObject = pGameObject;
            Settings = EraChronicleConfig.LoadOrCreate(_declare.FolderPath);

            _chroniclesDir = Path.Combine(_declare.FolderPath, "chronicles");
            Directory.CreateDirectory(_chroniclesDir);

            Debug.Log($"[EraChronicle] Loaded v{_declare.Version} (chronicles={_chroniclesDir})");
        }

        public void OnUnload()
        {
            Debug.Log("[EraChronicle] Unloaded.");
        }

        public void ToggleHud() => _showHud = !_showHud;

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
                _showHud = !_showHud;
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

            foreach (var k in world.kingdoms)
            {
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

            var alive = MapBox.instance?.units?.units_only_alive;
            if (alive == null) return;
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
                foreach (var k in world.kingdoms)
                {
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
                foreach (var w in world.wars.getActiveWars())
                {
                    if (w == null) continue;
                    entry.ActiveWars.Add(new WarSnapshot
                    {
                        Id = w.id,
                        Name = w.name,
                        TotalDeaths = w.getDeadAttackers() + w.getDeadDefenders()
                    });
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

        private void EnsureStyles()
        {
            if (_boxStyle != null) return;
            _bgTex = MakeTex(new Color(0.094f, 0.094f, 0.145f, 0.94f));
            _btnTex = MakeTex(new Color(0.192f, 0.196f, 0.267f, 0.96f));

            _boxStyle = new GUIStyle(GUI.skin.window) { padding = new RectOffset(12, 12, 24, 12) };
            _boxStyle.normal.background = _bgTex;
            _boxStyle.onNormal.background = _bgTex;
            _boxStyle.normal.textColor = new Color(0.976f, 0.886f, 0.686f); // Catppuccin Yellow

            _headerStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 13 };
            _headerStyle.normal.textColor = new Color(0.706f, 0.745f, 0.996f);

            _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            _labelStyle.normal.textColor = new Color(0.804f, 0.839f, 0.957f);

            _btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 12 };
            _btnStyle.normal.background = _btnTex;
            _btnStyle.normal.textColor = new Color(0.804f, 0.839f, 0.957f);
        }

        private static Texture2D MakeTex(Color col)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, col);
            t.Apply();
            return t;
        }

        private void OnGUI()
        {
            if (!_showHud || Settings == null) return;
            EnsureStyles();
            _windowRect = GUI.Window(941207, _windowRect, DrawHud, "EraChronicle (F7)", _boxStyle);
        }

        private void DrawHud(int id)
        {
            var stats = GetMapStats();
            int year = stats != null ? stats.history_current_year : 0;

            GUILayout.Label($"Year {year}  |  Tracked Kingdoms: {_kingdomEraTier.Count}", _headerStyle);
            GUILayout.Label($"Last Snapshot Year: {(_lastSnapshotYear < 0 ? "None" : _lastSnapshotYear.ToString())}", _labelStyle);
            GUILayout.Space(6);

            if (GUILayout.Button($"Era Progression: {(Settings.EnableEraProgression ? "ON" : "OFF")}", _btnStyle))
            {
                Settings.EnableEraProgression = !Settings.EnableEraProgression;
                Settings.Save(_declare.FolderPath);
            }

            if (GUILayout.Button($"Auto Snapshots (every {Settings.SnapshotIntervalYears}y): {(Settings.EnableAutoSnapshots ? "ON" : "OFF")}", _btnStyle))
            {
                Settings.EnableAutoSnapshots = !Settings.EnableAutoSnapshots;
                Settings.Save(_declare.FolderPath);
            }

            if (GUILayout.Button("Record Chronicle Snapshot Now", _btnStyle) && stats != null)
            {
                RecordSnapshot(stats);
                _lastSnapshotYear = year;
            }

            GUILayout.Space(6);
            GUILayout.Label("Recent Milestones:", _headerStyle);
            int shown = 0;
            for (int i = _recentEvents.Count - 1; i >= 0 && shown < 4; i--, shown++)
            {
                GUILayout.Label("• " + _recentEvents[i], _labelStyle);
            }

            GUI.DragWindow();
        }
    }
}
