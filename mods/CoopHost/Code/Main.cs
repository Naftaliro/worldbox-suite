using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
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
        private int _stateEncodeBusy = 0;
        private int _terrainEncodeBusy = 0;
        private int _terrainRevision = 1;
        private int _lastBuiltTerrainRev = -1;
        private int _lastTerrainW = -1;
        private int _lastTerrainH = -1;

        private bool _showHud = false;
        private GuiOverlayRunner _cursorRunner;
        private GuiOverlayRunner _panelRunner;
        private float _stateTimer = 0f;
        private float _terrainTimer = 0f;
        private float _lastUpdateWarnTime = -99f;
        private bool? _modernAgeLoadedCache;
        private string _lanIp = "127.0.0.1";

        // Live guest cursor overlay inside host's WorldBox view
        private string _guestName = "";
        private float _guestX = -1f;
        private float _guestY = -1f;
        private float _guestLastSeen = -99f;

        private Rect _windowRect = new Rect(695, 86, 365, 385);
        private Texture2D _cursorTex;
        private GUIStyle _cursorLabelStyle;

        public ModDeclare GetDeclaration() => _declare;
        public GameObject GetGameObject() => _gameObject;
        public string GetUrl() => "https://github.com/naftaliro/worldbox-suite";

        public void OnLoad(ModDeclare pModDecl, GameObject pGameObject)
        {
            Instance = this;
            _declare = pModDecl;
            _gameObject = pGameObject;
            _cursorRunner = pGameObject.AddComponent<GuiOverlayRunner>();
            _cursorRunner.Configure(false, DrawGuestCursorGui);
            _panelRunner = pGameObject.AddComponent<GuiOverlayRunner>();
            _panelRunner.Configure(true, DrawPanelGui);
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

        public void ToggleHud()
        {
            _showHud = !_showHud;
            if (_panelRunner != null) _panelRunner.enabled = _showHud;
        }

        public bool GetState(string key)
        {
            if (key == "server") return _server != null && _server.IsRunning;
            return false;
        }

        public string ToggleServer()
        {
            if (_server == null) return "Co-Op server not ready.";
            if (_server.IsRunning)
            {
                _server.Stop();
                if (_cursorRunner != null) _cursorRunner.enabled = false;
                return "Co-Op God Mode Server: STOPPED";
            }
            _terrainRevision++;
            bool ok = _server.Start();
            return ok
                ? $"Co-Op LIVE: http://{_lanIp}:{Settings.Port}  (PIN: {Settings.JoinPin})"
                : $"Co-Op Error: {_server.LastError}";
        }

        private void Update()
        {
            if (Settings == null) return;

            if ((Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) && Input.GetKeyDown(KeyCode.C))
            {
                ToggleHud();
            }

            if (_server == null || !_server.IsRunning || MapBox.instance == null) return;

            try
            {
                bool guestCursorVisible = Settings.ShowGuestCursorInGame && _guestX >= 0 && Time.unscaledTime - _guestLastSeen < 12f;
                if (_cursorRunner != null && _cursorRunner.enabled != guestCursorVisible)
                {
                    _cursorRunner.enabled = guestCursorVisible;
                }

                // 1. Drain incoming guest actions safely on Unity's main thread
                int processed = 0;
                while (processed < 24 && _actionQueue.TryDequeue(out CoopActionRequest req))
                {
                    ExecuteGuestAction(req);
                    processed++;
                }

                // Skip main-thread JSON & terrain raster serialization when no co-op browser client is connected
                if (!_server.HasRecentClient) return;

                // 2. Refresh live state snapshot at ~3 Hz
                _stateTimer += Time.unscaledDeltaTime;
                if (_stateTimer >= 0.35f)
                {
                    _stateTimer = 0f;
                    RefreshStateSnapshot();
                }

                // 3. Refresh terrain raster when world size changes or terrain was painted
                _terrainTimer += Time.unscaledDeltaTime;
                if (_terrainTimer >= 1.5f)
                {
                    _terrainTimer = 0f;
                    if (_lastBuiltTerrainRev != _terrainRevision || _lastTerrainW != MapBox.width || _lastTerrainH != MapBox.height)
                    {
                        RefreshTerrainSnapshot();
                    }
                }
            }
            catch (Exception e)
            {
                float now = Time.unscaledTime;
                if (now - _lastUpdateWarnTime >= 30f)
                {
                    _lastUpdateWarnTime = now;
                    Debug.LogWarning($"[CoopHost] Update tick exception: {e.Message}");
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
                        case "deep_ocean": MapAction.terraformMain(t, TileLibrary.deep_ocean, false); break;
                        case "shallow": MapAction.terraformMain(t, TileLibrary.shallow_waters, false); break;
                        case "sand": MapAction.terraformMain(t, TileLibrary.sand, false); break;
                        case "mountains": MapAction.terraformMain(t, TileLibrary.mountains, false); break;
                        case "jungle":
                            MapAction.terraformMain(t, TileLibrary.soil_high, false);
                            MapAction.terraformTop(t, TopTileLibrary.jungle_high, false);
                            break;
                        case "permafrost":
                            MapAction.terraformMain(t, TileLibrary.soil_high, false);
                            MapAction.terraformTop(t, TopTileLibrary.permafrost_high, false);
                            break;
                        case "road":
                            if (t.Type != null && !t.Type.liquid) MapAction.terraformTop(t, TopTileLibrary.road, false);
                            break;
                        case "wall":
                            if (t.Type != null && !t.Type.liquid) MapAction.terraformTop(t, TopTileLibrary.wall_order, false);
                            break;
                        default:
                            MapAction.terraformMain(t, TileLibrary.soil_high, false);
                            MapAction.terraformTop(t, TopTileLibrary.grass_high, false);
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
                        var lightningOpt = AssetManager.terraform?.get("lightning_normal");
                        if (lightningOpt != null)
                        {
                            MapAction.damageWorld(center, 2, lightningOpt);
                        }
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
                        var meteorOpt = AssetManager.terraform?.get("meteorite");
                        if (meteorOpt != null)
                        {
                            MapAction.damageWorld(center, Math.Max(4, rad + 2), meteorOpt);
                        }
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
                        var alive = new List<Actor>(world.units.units_only_alive);
                        for (int i = 0; i < alive.Count; i++)
                        {
                            Actor u = alive[i];
                            if (u == null || !u.isAlive() || u.current_tile == null) continue;
                            int dx = u.current_tile.x - center.x;
                            int dy = u.current_tile.y - center.y;
                            if (dx * dx + dy * dy <= rSq)
                            {
                                u.restoreHealthPercent(1f);
                                if (AssetManager.traits != null && AssetManager.traits.has("blessed"))
                                {
                                    u.addTrait("blessed");
                                }
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
            var kingdomsSnapshot = new List<Kingdom>(world.kingdoms);
            for (int i = 0; i < kingdomsSnapshot.Count; i++)
            {
                Kingdom k = kingdomsSnapshot[i];
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
            if (Interlocked.CompareExchange(ref _terrainEncodeBusy, 1, 0) != 0) return;

            int rev = _terrainRevision;
            _lastBuiltTerrainRev = rev;
            _lastTerrainW = w;
            _lastTerrainH = h;

            // Copy immutable tile type ID references on the main thread, then build palette + Base64 + JSON on a worker thread
            string[] tileIds = new string[w * h];
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
                    tileIds[rowOff + x] = key;
                }
            }

            Task.Run(() =>
            {
                try
                {
                    var palette = new List<string>(32);
                    var palIdx = new Dictionary<string, byte>(32);
                    byte[] grid = new byte[tileIds.Length];

                    for (int i = 0; i < tileIds.Length; i++)
                    {
                        string key = tileIds[i] ?? "deep_ocean";
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
                        grid[i] = idx;
                    }

                    var payload = new
                    {
                        width = w,
                        height = h,
                        revision = rev,
                        palette,
                        gridBase64 = Convert.ToBase64String(grid)
                    };

                    _cachedTerrainJson = JsonConvert.SerializeObject(payload);
                }
                catch { }
                finally
                {
                    Interlocked.Exchange(ref _terrainEncodeBusy, 0);
                }
            });
        }

        private void RefreshStateSnapshot()
        {
            var world = MapBox.instance;
            if (world == null) return;
            if (Interlocked.CompareExchange(ref _stateEncodeBusy, 1, 0) != 0) return;

            var stats = MapStatsField?.GetValue(world) as MapStats;
            var kingdomsOut = new List<object>();
            var citiesOut = new List<object>();
            var warsOut = new List<object>();
            var unitsOut = new List<int[]>();

            if (world.kingdoms != null)
            {
                var kingdomsSnapshot = new List<Kingdom>(world.kingdoms);
                for (int i = 0; i < kingdomsSnapshot.Count; i++)
                {
                    Kingdom k = kingdomsSnapshot[i];
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
                var citiesSnapshot = new List<City>(world.cities);
                for (int i = 0; i < citiesSnapshot.Count; i++)
                {
                    City c = citiesSnapshot[i];
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
                var activeWars = world.wars.getActiveWars();
                if (activeWars != null)
                {
                    var warsSnapshot = new List<War>(activeWars);
                    for (int i = 0; i < warsSnapshot.Count; i++)
                    {
                        War w = warsSnapshot[i];
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
            }

            if (world.units?.units_only_alive != null)
            {
                var alive = new List<Actor>(world.units.units_only_alive);
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

            if (!_modernAgeLoadedCache.HasValue)
            {
                _modernAgeLoadedCache = AccessTools.TypeByName("ModernAge.Main") != null;
            }

            var payload = new
            {
                worldName = stats?.name ?? "WorldBox",
                year = stats?.history_current_year ?? 0,
                paused = Config.paused,
                width = MapBox.width,
                height = MapBox.height,
                terrainRevision = _terrainRevision,
                modernAgeLoaded = _modernAgeLoadedCache.Value,
                kingdoms = kingdomsOut,
                cities = citiesOut,
                wars = warsOut,
                units = unitsOut,
                log = new List<string>(_activityLog)
            };

            Task.Run(() =>
            {
                try
                {
                    _cachedStateJson = JsonConvert.SerializeObject(payload);
                }
                catch { }
                finally
                {
                    Interlocked.Exchange(ref _stateEncodeBusy, 0);
                }
            });
        }

        private void EnsureCursorStyles()
        {
            if (_cursorTex != null) return;

            const int sz = 32;
            _cursorTex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
            float half = (sz - 1) * 0.5f;
            for (int y = 0; y < sz; y++)
                for (int x = 0; x < sz; x++)
                {
                    float d = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                    float a = (d >= 0.65f && d <= 0.96f) ? 0.95f : (d < 0.25f ? 0.95f : 0f);
                    _cursorTex.SetPixel(x, y, new Color(1.0f, 0.875f, 0.45f, a));
                }
            _cursorTex.Apply();

            _cursorLabelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 11, fontStyle = FontStyle.Bold };
            _cursorLabelStyle.normal.textColor = new Color(1.0f, 0.875f, 0.45f);
        }

        private void DrawGuestCursorGui()
        {
            if (Settings == null || !Settings.ShowGuestCursorInGame || _guestX < 0 || Time.unscaledTime - _guestLastSeen >= 12f) return;
            if (Event.current.type != EventType.Repaint) return;
            Camera cam = Camera.main;
            if (cam == null) return;

            EnsureCursorStyles();
            Vector3 scr = cam.WorldToScreenPoint(new Vector3(_guestX, _guestY, 0f));
            if (scr.z >= 0f)
            {
                float sx = scr.x;
                float sy = Screen.height - scr.y;
                GUI.DrawTexture(new Rect(sx - 14f, sy - 14f, 28f, 28f), _cursorTex);
                GUI.Label(new Rect(sx - 60f, sy - 30f, 120f, 16f), _guestName, _cursorLabelStyle);
            }
        }

        private void DrawPanelGui()
        {
            if (!_showHud || Settings == null) return;
            GameUiStyle.BeginFrame();
            _windowRect = GUI.Window(941216, _windowRect, DrawHud, "CoopHost: Shared God Mode (Alt+C)", GameUiStyle.WindowStyle);
            GameUiStyle.DrawTooltip(949009);
        }

        private void DrawHud(int id)
        {
            GameUiStyle.DrawCloseButton(_windowRect, ToggleHud);
            bool running = _server != null && _server.IsRunning;
            GameUiStyle.Label($"Status: {(running ? "HOSTING LIVE" : "STOPPED")}", "Co-Op Server Status|When HOSTING LIVE, a second player can open your URL in any browser and share god powers with you.", isHeader: true);
            GameUiStyle.Label($"LAN URL: http://{_lanIp}:{Settings.Port}", "Join Address|Have your friend open this URL in their browser on the same Wi-Fi/LAN, or forward port " + Settings.Port + " with Tailscale or playit.gg.");
            GameUiStyle.Label($"Join PIN: {Settings.JoinPin}   (LAN / Tailscale / playit.gg)", "4-Digit Security PIN|Your guest enters this PIN in the browser console before they can spawn units, paint terrain, or cast powers.", isHeader: true);

            if (!string.IsNullOrEmpty(_server?.LastError))
            {
                GameUiStyle.Subtle("Error: " + _server.LastError);
            }

            GUILayout.Space(6);
            if (GameUiStyle.ToggleButton(
                running ? "Co-Op Server: RUNNING (Click to Stop)" : "Start Co-Op Server",
                running,
                "Toggle Co-Op HTTP Server|Starts or stops the zero-admin browser server on port " + Settings.Port + " without closing your WorldBox game."))
            {
                if (running) _server.Stop();
                else
                {
                    _terrainRevision++;
                    _server.Start();
                }
            }

            if (GameUiStyle.Button(
                "Generate New 4-Digit PIN",
                "Rotate Join PIN|Creates a fresh random 4-digit PIN and saves it immediately so older guest sessions must re-enter the new code."))
            {
                Settings.JoinPin = new System.Random().Next(1000, 9999).ToString();
                Settings.Save(_declare.FolderPath);
            }

            if (GameUiStyle.ToggleButton(
                $"Show Guest Cursor In-Game: {(Settings.ShowGuestCursorInGame ? "ON" : "OFF")}",
                Settings.ShowGuestCursorInGame,
                "In-Game Guest Reticle|Draws a live golden crosshair and nametag inside your WorldBox view showing where your co-op partner is pointing."))
            {
                Settings.ShowGuestCursorInGame = !Settings.ShowGuestCursorInGame;
                Settings.Save(_declare.FolderPath);
            }

            if (GameUiStyle.Button(
                "Force Resync Terrain to Guest",
                "Resend Full Terrain Map|Rebuilds the terrain raster snapshot and pushes it to the guest browser console if you painted major terrain changes."))
            {
                _terrainRevision++;
            }

            GUILayout.Space(8);
            GameUiStyle.Label("Recent Co-Op Activity:", "Guest Action Log|Live feed of units spawned, terrain painted, and god powers triggered by your co-op guest.", isHeader: true);
            int shown = 0;
            for (int i = _activityLog.Count - 1; i >= 0 && shown < 5; i--, shown++)
            {
                GameUiStyle.Subtle("• " + _activityLog[i]);
            }

            GameUiStyle.CaptureTooltip();
            GUI.DragWindow();
        }
    }
}
