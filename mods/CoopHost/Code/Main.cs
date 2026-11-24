using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using NeoModLoader.api;
using Newtonsoft.Json;
using UnityEngine;

namespace CoopHost
{
    public class Main : MonoBehaviour, IMod
    {
        public static Main Instance { get; private set; }
        public static CoopHostConfig Settings { get; private set; }

        private static readonly FieldInfo MapStatsField = AccessTools.Field(typeof(MapBox), "map_stats");
        private static readonly FieldInfo CityKingdomField = AccessTools.Field(typeof(City), "kingdom");
        private static readonly MethodInfo LightningMethod = AccessTools.Method(typeof(MapAction), "applyLightningEffect");
        private static readonly MethodInfo StartFireMethod = AccessTools.Method(typeof(WorldTile), "startFire");
        private static readonly MethodInfo StopFireMethod = AccessTools.Method(typeof(WorldTile), "stopFire");

        private ModDeclare _declare;
        private GameObject _gameObject;
        private CoopHttpServer _server;

        private readonly ConcurrentQueue<CoopActionRequest> _actionQueue = new ConcurrentQueue<CoopActionRequest>();
        private readonly List<string> _activityLog = new List<string>();

        private volatile string _cachedStateJson = "{}";
        private volatile string _cachedTerrainJson = "{}";
        private int _terrainRevision = 1;
        private int _lastBuiltTerrainRev = -1;
        private int _lastTerrainW = -1;
        private int _lastTerrainH = -1;

        private bool _showHud = false;
        private float _stateTimer = 0f;
        private float _terrainTimer = 0f;
        private string _lanIp = "127.0.0.1";

        // Live guest cursor overlay inside host's WorldBox view
        private string _guestName = "";
        private float _guestX = -1f;
        private float _guestY = -1f;
        private float _guestLastSeen = -99f;

        private Rect _windowRect = new Rect(695, 86, 355, 360);
        private Texture2D _bgTex;
        private Texture2D _btnTex;
        private Texture2D _cursorTex;
        private GUIStyle _boxStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _btnStyle;
        private GUIStyle _cursorLabelStyle;

        public ModDeclare GetDeclaration() => _declare;
        public GameObject GetGameObject() => _gameObject;
        public string GetUrl() => "https://github.com/naftaliro/worldbox-suite";

        public void OnLoad(ModDeclare pModDecl, GameObject pGameObject)
        {
            Instance = this;
            _declare = pModDecl;
            _gameObject = pGameObject;
            Settings = CoopHostConfig.LoadOrCreate(_declare.FolderPath);
            _lanIp = CoopHttpServer.GetLocalLanIp();

            string webClientPath = Path.Combine(_declare.FolderPath, "WebClient", "index.html");
            _server = new CoopHttpServer(
                Settings.Port,
                webClientPath,
                () => Settings.JoinPin,
                () => _cachedStateJson,
                () => _cachedTerrainJson,
                req => _actionQueue.Enqueue(req)
            );

            if (Settings.AutoStartServer)
            {
                _server.Start();
            }

            Debug.Log($"[CoopHost] Loaded v{_declare.Version} (port={Settings.Port}, pin={Settings.JoinPin})");
        }

        public void OnUnload()
        {
            _server?.Stop();
            Debug.Log("[CoopHost] Stopped and unloaded.");
        }

        public void ToggleHud() => _showHud = !_showHud;

        private void Update()
        {
            if (Settings == null) return;

            if ((Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) && Input.GetKeyDown(KeyCode.C))
            {
                ToggleHud();
            }

            if (_server == null || !_server.IsRunning || MapBox.instance == null) return;

            // 1. Drain incoming guest actions safely on Unity's main thread
            int processed = 0;
            while (processed < 24 && _actionQueue.TryDequeue(out CoopActionRequest req))
            {
                ExecuteGuestAction(req);
                processed++;
            }

            // 2. Refresh live state snapshot at ~4 Hz
            _stateTimer += Time.unscaledDeltaTime;
            if (_stateTimer >= 0.25f)
            {
                _stateTimer = 0f;
                RefreshStateSnapshot();
            }

            // 3. Refresh terrain raster when world size changes or terrain was painted
            _terrainTimer += Time.unscaledDeltaTime;
            if (_terrainTimer >= 1.2f)
            {
                _terrainTimer = 0f;
                if (_lastBuiltTerrainRev != _terrainRevision || _lastTerrainW != MapBox.width || _lastTerrainH != MapBox.height)
                {
                    RefreshTerrainSnapshot();
                }
            }
        }

        private void ExecuteGuestAction(CoopActionRequest req)
        {
            var world = MapBox.instance;
            if (world == null || req == null) return;

            string who = string.IsNullOrEmpty(req.guestName) ? "Co-Op Guest" : req.guestName;
            _guestName = who;
            _guestLastSeen = Time.unscaledTime;

            if (req.x >= 0 && req.x < MapBox.width && req.y >= 0 && req.y < MapBox.height)
            {
                _guestX = req.x;
                _guestY = req.y;
            }

            if (req.type == "cursor") return;

            WorldTile center = world.GetTile(Mathf.Clamp(req.x, 0, Math.Max(0, MapBox.width - 1)), Mathf.Clamp(req.y, 0, Math.Max(0, MapBox.height - 1)));
            int rad = Mathf.Clamp(req.radius, 1, 14);

            if (req.type == "spawn" && center != null && world.units != null)
            {
                string species = string.IsNullOrEmpty(req.asset) ? "human" : req.asset;
                int count = Mathf.Clamp(rad, 1, 12);
                int spawned = 0;
                for (int i = 0; i < count; i++)
                {
                    if (AssetManager.actor_library != null && AssetManager.actor_library.has(species))
                    {
                        if (world.units.spawnNewUnit(species, center) != null) spawned++;
                    }
                }
                AddLog($"{who} spawned {spawned}x {species} at ({center.x},{center.y})");
                return;
            }

            if (req.type == "terrain" && center != null)
            {
                PaintTerrainBrush(world, center.x, center.y, rad, req.asset ?? "grass");
                world.redrawTiles();
                _terrainRevision++;
                AddLog($"{who} painted {req.asset} (r={rad}) at ({center.x},{center.y})");
                return;
            }

            if (req.type == "power")
            {
                ExecuteGodPower(world, center, rad, req.asset ?? "", who);
            }
        }

        private static void PaintTerrainBrush(MapBox world, int cx, int cy, int r, string brush)
        {
            int rSq = r * r;
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    if (dx * dx + dy * dy > rSq) continue;
                    WorldTile t = world.GetTile(cx + dx, cy + dy);
                    if (t == null) continue;

                    switch (brush)
                    {
                        case "deep_ocean": t.setTileType(TileLibrary.deep_ocean); break;
                        case "shallow": t.setTileType(TileLibrary.shallow_waters); break;
                        case "sand": t.setTileType(TileLibrary.sand); break;
                        case "mountains": t.setTileType(TileLibrary.mountains); break;
                        case "jungle": t.setTileTypes(TileLibrary.soil_high, TopTileLibrary.jungle_high); break;
                        case "permafrost": t.setTileTypes(TileLibrary.soil_high, TopTileLibrary.permafrost_high); break;
                        case "road":
                            if (t.Type != null && !t.Type.liquid) t.setTopTileType(TopTileLibrary.road);
                            break;
                        case "wall":
                            if (t.Type != null && !t.Type.liquid) t.setTopTileType(TopTileLibrary.wall_order);
                            break;
                        default:
                            t.setTileTypes(TileLibrary.soil_high, TopTileLibrary.grass_high);
                            break;
                    }
                }
            }
        }

        private void ExecuteGodPower(MapBox world, WorldTile center, int rad, string power, string who)
        {
            switch (power)
            {
                case "lightning":
                    if (center != null)
                    {
                        if (LightningMethod != null)
                        {
                            var ps = LightningMethod.GetParameters();
                            object[] args = ps.Length == 1 ? new object[] { center } : new object[] { center, 1 };
                            try { LightningMethod.Invoke(null, args); } catch { }
                        }
                        MapAction.damageWorld(center, 2, AssetManager.terraform.get("lightning_normal"));
                        AddLog($"{who} struck Lightning at ({center.x},{center.y})");
                    }
                    break;
                case "fire":
                    if (center != null && StartFireMethod != null)
                    {
                        int rSq = rad * rad;
                        bool oneParam = StartFireMethod.GetParameters().Length == 1;
                        for (int dy = -rad; dy <= rad; dy++)
                            for (int dx = -rad; dx <= rad; dx++)
                                if (dx * dx + dy * dy <= rSq)
                                {
                                    WorldTile ft = world.GetTile(center.x + dx, center.y + dy);
                                    if (ft != null)
                                    {
                                        try { StartFireMethod.Invoke(ft, oneParam ? new object[] { true } : Array.Empty<object>()); } catch { }
                                    }
                                }
                        AddLog($"{who} ignited Fire at ({center.x},{center.y})");
                    }
                    break;
                case "rain":
                    if (center != null && StopFireMethod != null)
                    {
                        int rSq = rad * rad;
                        for (int dy = -rad; dy <= rad; dy++)
                            for (int dx = -rad; dx <= rad; dx++)
                                if (dx * dx + dy * dy <= rSq)
                                {
                                    WorldTile ft = world.GetTile(center.x + dx, center.y + dy);
                                    if (ft != null)
                                    {
                                        try { StopFireMethod.Invoke(ft, Array.Empty<object>()); } catch { }
                                    }
                                }
                        AddLog($"{who} rained on ({center.x},{center.y})");
                    }
                    break;
                case "meteor":
                    if (center != null)
                    {
                        MapAction.damageWorld(center, Math.Max(4, rad + 2), AssetManager.terraform.get("meteorite"));
                        world.redrawTiles();
                        _terrainRevision++;
                        AddLog($"{who} dropped a Meteor at ({center.x},{center.y})!");
                    }
                    break;
                case "bless":
                    if (center != null && world.units?.units_only_alive != null)
                    {
                        int rSq = (rad + 4) * (rad + 4);
                        int blessed = 0;
                        foreach (Actor u in world.units.units_only_alive)
                        {
                            if (u == null || !u.isAlive() || u.current_tile == null) continue;
                            int dx = u.current_tile.x - center.x;
                            int dy = u.current_tile.y - center.y;
                            if (dx * dx + dy * dy <= rSq)
                            {
                                u.restoreHealthPercent(1f);
                                u.addTrait("blessed");
                                blessed++;
                            }
                        }
                        AddLog($"{who} blessed {blessed} units at ({center.x},{center.y})");
                    }
                    break;
                case "pause":
                    Config.paused = !Config.paused;
                    AddLog($"{who} {(Config.paused ? "paused" : "resumed")} the world");
                    break;
                case "peace":
                    world.wars?.stopAllWars();
                    AddLog($"{who} declared World Peace");
                    break;
                case "war":
                    TriggerTopTwoWar(world, who);
                    break;
            }
        }

        private void TriggerTopTwoWar(MapBox world, string who)
        {
            if (world?.kingdoms == null || world.wars == null) return;
            var civs = new List<Kingdom>();
            foreach (var k in world.kingdoms)
            {
                if (k != null && k.isCiv() && k.isAlive()) civs.Add(k);
            }
            civs.Sort((a, b) => b.getPopulationPeople().CompareTo(a.getPopulationPeople()));
            if (civs.Count >= 2 && !civs[0].isInWarWith(civs[1]))
            {
                world.wars.newWar(civs[0], civs[1], WarTypeLibrary.normal);
                AddLog($"{who} sparked war: {civs[0].name} vs {civs[1].name}!");
            }
        }

        private void AddLog(string msg)
        {
            _activityLog.Add(msg);
            if (_activityLog.Count > 10) _activityLog.RemoveAt(0);
            Debug.Log($"[CoopHost] {msg}");
        }

        private void RefreshTerrainSnapshot()
        {
            var world = MapBox.instance;
            int w = MapBox.width;
            int h = MapBox.height;
            if (world == null || w <= 0 || h <= 0) return;

            // Compact 1-byte-per-tile color palette index buffer encoded as Base64
            var palette = new List<string>();
            var palIdx = new Dictionary<string, byte>();
            byte[] grid = new byte[w * h];

            for (int y = 0; y < h; y++)
            {
                int rowOff = y * w;
                for (int x = 0; x < w; x++)
                {
                    WorldTile t = world.GetTile(x, y);
                    string key = "deep_ocean";
                    if (t != null)
                    {
                        if (t.top_type != null) key = t.top_type.id;
                        else if (t.main_type != null) key = t.main_type.id;
                    }
                    if (!palIdx.TryGetValue(key, out byte idx))
                    {
                        if (palette.Count < 255)
                        {
                            idx = (byte)palette.Count;
                            palette.Add(key);
                            palIdx[key] = idx;
                        }
                        else idx = 0;
                    }
                    grid[rowOff + x] = idx;
                }
            }

            var payload = new
            {
                width = w,
                height = h,
                revision = _terrainRevision,
                palette,
                gridBase64 = Convert.ToBase64String(grid)
            };

            _cachedTerrainJson = JsonConvert.SerializeObject(payload);
            _lastBuiltTerrainRev = _terrainRevision;
            _lastTerrainW = w;
            _lastTerrainH = h;
        }

        private void RefreshStateSnapshot()
        {
            var world = MapBox.instance;
            if (world == null) return;

            var stats = MapStatsField?.GetValue(world) as MapStats;
            var kingdomsOut = new List<object>();
            var citiesOut = new List<object>();
            var warsOut = new List<object>();
            var unitsOut = new List<int[]>();

            if (world.kingdoms != null)
            {
                foreach (Kingdom k in world.kingdoms)
                {
                    if (k == null || !k.isCiv() || !k.isAlive()) continue;
                    Color32 col = k.getColor() != null ? k.getColor().getColorMain32() : new Color32(203, 166, 247, 255);
                    kingdomsOut.Add(new
                    {
                        id = k.id,
                        name = k.name,
                        pop = k.getPopulationPeople(),
                        cities = k.cities?.Count ?? 0,
                        atWar = k.hasEnemies(),
                        color = $"#{col.r:X2}{col.g:X2}{col.b:X2}"
                    });
                }
            }

            if (world.cities != null)
            {
                foreach (City c in world.cities)
                {
                    if (c == null || !c.isAlive()) continue;
                    WorldTile ct = c.getTile();
                    if (ct == null) continue;
                    Kingdom ck = CityKingdomField?.GetValue(c) as Kingdom;
                    citiesOut.Add(new
                    {
                        name = c.name,
                        x = ct.x,
                        y = ct.y,
                        pop = c.getPopulationPeople(),
                        kingdomId = ck != null ? ck.id : -1
                    });
                }
            }

            if (world.wars != null)
            {
                foreach (War w in world.wars.getActiveWars())
                {
                    if (w == null) continue;
                    warsOut.Add(new
                    {
                        name = w.name,
                        dead = w.getDeadAttackers() + w.getDeadDefenders(),
                        atk = w.getMainAttacker()?.name ?? "?",
                        def = w.getMainDefender()?.name ?? "?"
                    });
                }
            }

            if (world.units?.units_only_alive != null)
            {
                var alive = world.units.units_only_alive;
                int max = Math.Max(200, Settings.MaxStreamedUnits);
                int stride = Math.Max(1, alive.Count / max);
                for (int i = 0; i < alive.Count && unitsOut.Count < max; i += stride)
                {
                    Actor u = alive[i];
                    if (u == null || !u.isAlive() || u.current_tile == null) continue;
                    int kind = 0;
                    string aid = u.asset?.id ?? "";
                    if (u.asset != null && u.asset.is_boat) kind = 2;
                    else if (u.isKing()) kind = 3;
                    else if (aid.Contains("tank") || aid.Contains("mbt")) kind = 4;
                    else if (aid.Contains("jet") || aid.Contains("heli") || aid.Contains("fighter")) kind = 5;
                    else if (u.army != null) kind = 1;
                    else if (u.kingdom == null || !u.kingdom.isCiv()) kind = 6;

                    unitsOut.Add(new[] { u.current_tile.x, u.current_tile.y, (int)(u.kingdom?.id ?? -1), kind });
                }
            }

            var payload = new
            {
                worldName = stats?.name ?? "WorldBox",
                year = stats?.history_current_year ?? 0,
                paused = Config.paused,
                width = MapBox.width,
                height = MapBox.height,
                terrainRevision = _terrainRevision,
                modernAgeLoaded = AccessTools.TypeByName("ModernAge.Main") != null,
                kingdoms = kingdomsOut,
                cities = citiesOut,
                wars = warsOut,
                units = unitsOut,
                log = _activityLog
            };

            _cachedStateJson = JsonConvert.SerializeObject(payload);
        }

        private void EnsureStyles()
        {
            if (_boxStyle != null) return;
            _bgTex = MakeTex(new Color(0.094f, 0.094f, 0.145f, 0.94f));
            _btnTex = MakeTex(new Color(0.192f, 0.196f, 0.267f, 0.96f));

            const int sz = 32;
            _cursorTex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
            float half = (sz - 1) * 0.5f;
            for (int y = 0; y < sz; y++)
                for (int x = 0; x < sz; x++)
                {
                    float d = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                    float a = (d >= 0.65f && d <= 0.96f) ? 0.95f : (d < 0.25f ? 0.95f : 0f);
                    _cursorTex.SetPixel(x, y, new Color(0.455f, 0.780f, 0.925f, a));
                }
            _cursorTex.Apply();

            _boxStyle = new GUIStyle(GUI.skin.window) { padding = new RectOffset(12, 12, 24, 12) };
            _boxStyle.normal.background = _bgTex;
            _boxStyle.onNormal.background = _bgTex;
            _boxStyle.normal.textColor = new Color(0.455f, 0.780f, 0.925f);

            _headerStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 13 };
            _headerStyle.normal.textColor = new Color(0.796f, 0.651f, 0.969f);

            _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            _labelStyle.normal.textColor = new Color(0.804f, 0.839f, 0.957f);

            _btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 12 };
            _btnStyle.normal.background = _btnTex;
            _btnStyle.normal.textColor = new Color(0.804f, 0.839f, 0.957f);

            _cursorLabelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 11, fontStyle = FontStyle.Bold };
            _cursorLabelStyle.normal.textColor = new Color(0.455f, 0.780f, 0.925f);
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
            if (Settings == null) return;

            // Draw live Guest Cursor right inside your WorldBox screen
            if (Settings.ShowGuestCursorInGame && _guestX >= 0 && Time.unscaledTime - _guestLastSeen < 12f && Event.current.type == EventType.Repaint)
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    EnsureStyles();
                    Vector3 scr = cam.WorldToScreenPoint(new Vector3(_guestX, _guestY, 0f));
                    if (scr.z >= 0f)
                    {
                        float sx = scr.x;
                        float sy = Screen.height - scr.y;
                        GUI.DrawTexture(new Rect(sx - 14f, sy - 14f, 28f, 28f), _cursorTex);
                        GUI.Label(new Rect(sx - 60f, sy - 30f, 120f, 16f), _guestName, _cursorLabelStyle);
                    }
                }
            }

            if (!_showHud) return;
            EnsureStyles();
            _windowRect = GUI.Window(941216, _windowRect, DrawHud, "CoopHost: Shared God Mode (Alt+C)", _boxStyle);
        }

        private void DrawHud(int id)
        {
            bool running = _server != null && _server.IsRunning;
            GUILayout.Label($"Status: {(running ? "HOSTING LIVE" : "STOPPED")}", _headerStyle);
            GUILayout.Label($"LAN URL: http://{_lanIp}:{Settings.Port}", _labelStyle);
            GUILayout.Label($"Join PIN: {Settings.JoinPin}   (works with Tailscale / playit.gg)", _headerStyle);

            if (!string.IsNullOrEmpty(_server?.LastError))
            {
                GUILayout.Label("Error: " + _server.LastError, _labelStyle);
            }

            GUILayout.Space(6);
            if (GUILayout.Button(running ? "Stop Co-Op Server" : "Start Co-Op Server", _btnStyle))
            {
                if (running) _server.Stop();
                else
                {
                    _terrainRevision++;
                    _server.Start();
                }
            }

            if (GUILayout.Button("Generate New 4-Digit PIN", _btnStyle))
            {
                Settings.JoinPin = new System.Random().Next(1000, 9999).ToString();
                Settings.Save(_declare.FolderPath);
            }

            if (GUILayout.Button($"Show Guest Cursor In-Game: {(Settings.ShowGuestCursorInGame ? "ON" : "OFF")}", _btnStyle))
            {
                Settings.ShowGuestCursorInGame = !Settings.ShowGuestCursorInGame;
                Settings.Save(_declare.FolderPath);
            }

            if (GUILayout.Button("Force Resync Terrain to Guest", _btnStyle))
            {
                _terrainRevision++;
            }

            GUILayout.Space(8);
            GUILayout.Label("Recent Co-Op Activity:", _headerStyle);
            int shown = 0;
            for (int i = _activityLog.Count - 1; i >= 0 && shown < 5; i--, shown++)
            {
                GUILayout.Label("• " + _activityLog[i], _labelStyle);
            }

            GUI.DragWindow();
        }
    }
}
