using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using NeoModLoader.api;
using Newtonsoft.Json;
using UnityEngine;

namespace LinkedContinents
{
    public class Main : MonoBehaviour, IMod
    {
        public static Main Instance { get; private set; }
        public static LinkedContinentsConfig Settings { get; private set; }

        public static int FleetsSent { get; private set; }
        public static int FleetsReceived { get; private set; }

        private static readonly FieldInfo MapStatsField = AccessTools.Field(typeof(MapBox), "map_stats");
        private static readonly FieldInfo ActorDataField = AccessTools.Field(typeof(Actor), "data");
        private static readonly FieldInfo BoatPassengersField = AccessTools.Field(typeof(Boat), "_passengers");
        private static readonly MethodInfo SetKingdomMethod = AccessTools.Method(typeof(Actor), "setKingdom", new[] { typeof(Kingdom) });

        private ModDeclare _declare;
        private GameObject _gameObject;
        private string _queuePath;
        private bool _showHud = false;
        private float _scanTimer = 0f;
        private int _pendingIncoming = 0;
        private Rect _windowRect = new Rect(18, 320, 320, 305);

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

            Settings = LinkedContinentsConfig.LoadOrCreate(_declare.FolderPath);
            _queuePath = Path.Combine(_declare.FolderPath, "portal_queue.json");
            RefreshPendingCount();

            Debug.Log($"[LinkedContinents] Loaded v{_declare.Version} (queue={_queuePath})");
        }

        public void OnUnload()
        {
            Debug.Log("[LinkedContinents] Unloaded.");
        }

        public void ToggleHud()
        {
            _showHud = !_showHud;
            RefreshPendingCount();
        }

        private string CurrentWorldName()
        {
            var world = MapBox.instance;
            if (world != null && MapStatsField?.GetValue(world) is MapStats stats && !string.IsNullOrEmpty(stats.name))
            {
                return stats.name;
            }
            return "Unknown World";
        }

        private static ActorData GetActorData(Actor a)
        {
            return a != null ? ActorDataField?.GetValue(a) as ActorData : null;
        }

        private static IEnumerable<Actor> GetBoatPassengers(Boat boat)
        {
            if (boat == null || BoatPassengersField == null) return null;
            return BoatPassengersField.GetValue(boat) as IEnumerable<Actor>;
        }

        private List<PortalFleetPacket> ReadQueue()
        {
            try
            {
                if (File.Exists(_queuePath))
                {
                    var list = JsonConvert.DeserializeObject<List<PortalFleetPacket>>(File.ReadAllText(_queuePath));
                    if (list != null) return list;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LinkedContinents] Could not read portal queue: {e.Message}");
            }
            return new List<PortalFleetPacket>();
        }

        private void WriteQueue(List<PortalFleetPacket> packets)
        {
            try
            {
                File.WriteAllText(_queuePath, JsonConvert.SerializeObject(packets, Formatting.Indented));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LinkedContinents] Could not write portal queue: {e.Message}");
            }
            RefreshPendingCount();
        }

        private void RefreshPendingCount()
        {
            string cur = CurrentWorldName();
            int count = 0;
            foreach (var p in ReadQueue())
            {
                if (string.Equals(p.TargetWorld, cur, StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(p.TargetWorld))
                    count++;
            }
            _pendingIncoming = count;
        }

        private void Update()
        {
            if (Settings == null) return;

            if (Input.GetKeyDown(Settings.PortalHudKey))
            {
                _showHud = !_showHud;
                RefreshPendingCount();
            }

            if (Config.paused || MapBox.instance == null) return;

            _scanTimer += Time.deltaTime;
            if (_scanTimer >= Settings.ScanIntervalSeconds)
            {
                _scanTimer = 0f;
                if (Settings.AutoEdgeTransfers)
                {
                    ScanEdgeForDepartingFleets();
                }
                if (Settings.AutoReceiveOnLoad)
                {
                    RefreshPendingCount();
                    if (_pendingIncoming > 0)
                    {
                        ReceiveIncomingFleets();
                    }
                }
            }
        }

        private bool IsOnOutboundEdge(WorldTile tile)
        {
            int band = Math.Max(2, Settings.EdgeBandTiles);
            switch (Settings.OutboundEdge)
            {
                case OceanEdge.East: return tile.x >= MapBox.width - band;
                case OceanEdge.West: return tile.x < band;
                case OceanEdge.North: return tile.y >= MapBox.height - band;
                case OceanEdge.South: return tile.y < band;
                default: return false;
            }
        }

        private static OceanEdge OppositeEdge(OceanEdge edge)
        {
            switch (edge)
            {
                case OceanEdge.East: return OceanEdge.West;
                case OceanEdge.West: return OceanEdge.East;
                case OceanEdge.North: return OceanEdge.South;
                default: return OceanEdge.North;
            }
        }

        public void ScanEdgeForDepartingFleets()
        {
            var world = MapBox.instance;
            if (world?.units?.units_only_alive == null) return;

            var alive = world.units.units_only_alive;
            var toDispatch = new List<Actor>();

            for (int i = 0; i < alive.Count; i++)
            {
                Actor u = alive[i];
                if (u == null || !u.isAlive() || u.current_tile == null || u.kingdom == null || !u.kingdom.isCiv())
                    continue;
                if (u.asset == null || !u.asset.is_boat) continue;
                if (!IsOnOutboundEdge(u.current_tile)) continue;

                Boat boat = u.getSimpleComponent<Boat>();
                var pax = GetBoatPassengers(boat);
                if (pax != null)
                {
                    foreach (var _ in pax)
                    {
                        toDispatch.Add(u);
                        break;
                    }
                }
            }

            if (toDispatch.Count == 0) return;

            var queue = ReadQueue();
            foreach (Actor ship in toDispatch)
            {
                Boat boat = ship.getSimpleComponent<Boat>();
                var rawPax = GetBoatPassengers(boat);
                if (rawPax == null) continue;

                var packet = new PortalFleetPacket
                {
                    Id = Guid.NewGuid().ToString("N"),
                    SourceWorld = CurrentWorldName(),
                    TargetWorld = Settings.LinkedTargetWorld,
                    KingdomName = ship.kingdom?.name ?? "Expedition",
                    ArrivalEdge = OppositeEdge(Settings.OutboundEdge),
                    TimestampUtc = DateTime.UtcNow.ToString("o")
                };

                var passengers = new List<Actor>(rawPax);
                foreach (Actor p in passengers)
                {
                    if (p == null || !p.isAlive()) continue;
                    ActorData pData = GetActorData(p);
                    var rec = new PortalUnitRecord
                    {
                        SpeciesId = p.asset?.id ?? "human",
                        Name = p.getName(),
                        Level = pData?.level ?? 1,
                        Health = p.getHealth()
                    };
                    if (pData?.saved_traits != null)
                    {
                        rec.Traits.AddRange(pData.saved_traits);
                    }
                    packet.Units.Add(rec);
                    p.dieAndDestroy(AttackType.None);
                }

                if (packet.Units.Count > 0)
                {
                    queue.Add(packet);
                    FleetsSent++;
                    ship.dieAndDestroy(AttackType.None);
                    Debug.Log($"[LinkedContinents] Dispatched fleet of {packet.Units.Count} from {packet.KingdomName} -> {packet.TargetWorld}");
                }
            }

            WriteQueue(queue);
        }

        public void LaunchManualExpedition()
        {
            var world = MapBox.instance;
            if (world?.units?.units_only_alive == null) return;

            // Gather up to 12 soldiers from the strongest kingdom to dispatch immediately
            Kingdom top = null;
            foreach (var k in world.kingdoms)
            {
                if (k != null && k.isCiv() && k.isAlive())
                {
                    if (top == null || k.getPopulationPeople() > top.getPopulationPeople())
                        top = k;
                }
            }
            if (top == null) return;

            var packet = new PortalFleetPacket
            {
                Id = Guid.NewGuid().ToString("N"),
                SourceWorld = CurrentWorldName(),
                TargetWorld = Settings.LinkedTargetWorld,
                KingdomName = top.name,
                ArrivalEdge = OppositeEdge(Settings.OutboundEdge),
                TimestampUtc = DateTime.UtcNow.ToString("o")
            };

            var alive = world.units.units_only_alive;
            var picked = new List<Actor>();
            for (int i = 0; i < alive.Count && picked.Count < 12; i++)
            {
                Actor u = alive[i];
                if (u != null && u.isAlive() && u.kingdom == top && u.asset != null && !u.asset.is_boat && u.army != null)
                {
                    picked.Add(u);
                }
            }

            foreach (Actor u in picked)
            {
                ActorData uData = GetActorData(u);
                var rec = new PortalUnitRecord
                {
                    SpeciesId = u.asset?.id ?? "human",
                    Name = u.getName(),
                    Level = uData?.level ?? 1,
                    Health = u.getHealth()
                };
                if (uData?.saved_traits != null) rec.Traits.AddRange(uData.saved_traits);
                packet.Units.Add(rec);
                u.dieAndDestroy(AttackType.None);
            }

            if (packet.Units.Count > 0)
            {
                var queue = ReadQueue();
                queue.Add(packet);
                WriteQueue(queue);
                FleetsSent++;
                Debug.Log($"[LinkedContinents] Manual expedition: {packet.Units.Count} soldiers from {top.name} -> {Settings.LinkedTargetWorld}");
            }
        }

        public void ReceiveIncomingFleets()
        {
            var world = MapBox.instance;
            if (world?.units == null || MapBox.width <= 0) return;

            string cur = CurrentWorldName();
            var queue = ReadQueue();
            var remaining = new List<PortalFleetPacket>();
            bool changed = false;

            foreach (var packet in queue)
            {
                bool matches = string.Equals(packet.TargetWorld, cur, StringComparison.OrdinalIgnoreCase)
                               || string.IsNullOrEmpty(packet.TargetWorld);
                if (!matches)
                {
                    remaining.Add(packet);
                    continue;
                }

                WorldTile landing = FindLandingTile(packet.ArrivalEdge);
                if (landing == null)
                {
                    remaining.Add(packet);
                    continue;
                }

                Kingdom matchedKingdom = FindKingdomByName(packet.KingdomName);

                foreach (var rec in packet.Units)
                {
                    string species = string.IsNullOrEmpty(rec.SpeciesId) ? "human" : rec.SpeciesId;
                    Actor spawned = world.units.spawnNewUnit(species, landing);
                    if (spawned == null) continue;

                    if (!string.IsNullOrEmpty(rec.Name))
                    {
                        spawned.setName(rec.Name);
                    }
                    if (matchedKingdom != null && SetKingdomMethod != null)
                    {
                        SetKingdomMethod.Invoke(spawned, new object[] { matchedKingdom });
                    }
                    if (rec.Traits != null)
                    {
                        foreach (string tr in rec.Traits)
                        {
                            spawned.addTrait(tr);
                        }
                    }
                }

                FleetsReceived++;
                changed = true;
                Debug.Log($"[LinkedContinents] Landed {packet.Units.Count} expedition units from {packet.SourceWorld} ({packet.KingdomName}) at ({landing.x},{landing.y})");
            }

            if (changed)
            {
                WriteQueue(remaining);
            }
        }

        private static Kingdom FindKingdomByName(string name)
        {
            if (string.IsNullOrEmpty(name) || MapBox.instance?.kingdoms == null) return null;
            foreach (var k in MapBox.instance.kingdoms)
            {
                if (k != null && k.isCiv() && k.isAlive() && string.Equals(k.name, name, StringComparison.OrdinalIgnoreCase))
                    return k;
            }
            return null;
        }

        private static WorldTile FindLandingTile(OceanEdge edge)
        {
            var world = MapBox.instance;
            if (world == null) return null;

            int w = MapBox.width;
            int h = MapBox.height;

            // Scan inward from the arrival ocean edge to find the first solid ground tile
            for (int depth = 2; depth < Math.Min(w, h) / 2; depth += 2)
            {
                for (int step = 0; step < 32; step++)
                {
                    int x, y;
                    float frac = (step + 0.5f) / 32f;
                    switch (edge)
                    {
                        case OceanEdge.West:
                            x = Math.Min(w - 2, depth);
                            y = Mathf.Clamp((int)(frac * h), 2, h - 2);
                            break;
                        case OceanEdge.East:
                            x = Math.Max(1, w - 1 - depth);
                            y = Mathf.Clamp((int)(frac * h), 2, h - 2);
                            break;
                        case OceanEdge.South:
                            x = Mathf.Clamp((int)(frac * w), 2, w - 2);
                            y = Math.Min(h - 2, depth);
                            break;
                        default:
                            x = Mathf.Clamp((int)(frac * w), 2, w - 2);
                            y = Math.Max(1, h - 1 - depth);
                            break;
                    }

                    WorldTile tile = world.GetTile(x, y);
                    if (tile != null && tile.Type != null && !tile.Type.liquid && tile.Type.ground)
                    {
                        return tile;
                    }
                }
            }
            return world.GetTile(w / 2, h / 2);
        }

        private void EnsureStyles()
        {
            if (_boxStyle != null) return;
            _bgTex = MakeTex(new Color(0.094f, 0.094f, 0.145f, 0.94f));
            _btnTex = MakeTex(new Color(0.192f, 0.196f, 0.267f, 0.96f));

            _boxStyle = new GUIStyle(GUI.skin.window) { padding = new RectOffset(12, 12, 24, 12) };
            _boxStyle.normal.background = _bgTex;
            _boxStyle.onNormal.background = _bgTex;
            _boxStyle.normal.textColor = new Color(0.580f, 0.886f, 0.835f); // Catppuccin Teal

            _headerStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 13 };
            _headerStyle.normal.textColor = new Color(0.537f, 0.706f, 0.980f); // Catppuccin Blue

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
            _windowRect = GUI.Window(941206, _windowRect, DrawHud, "LinkedContinents Portal (F11)", _boxStyle);
        }

        private void DrawHud(int id)
        {
            GUILayout.Label($"World: {CurrentWorldName()}", _headerStyle);
            GUILayout.Label($"Sent: {FleetsSent}  |  Landed: {FleetsReceived}  |  Incoming: {_pendingIncoming}", _labelStyle);
            GUILayout.Space(6);

            GUILayout.Label("Target Linked Continent Name:", _labelStyle);
            Settings.LinkedTargetWorld = GUILayout.TextField(Settings.LinkedTargetWorld ?? "");

            if (GUILayout.Button($"Outbound Ocean Edge: {Settings.OutboundEdge}", _btnStyle))
            {
                Settings.OutboundEdge = (OceanEdge)(((int)Settings.OutboundEdge + 1) % 4);
                Settings.Save(_declare.FolderPath);
            }

            if (GUILayout.Button($"Auto Edge Transfers: {(Settings.AutoEdgeTransfers ? "ON" : "OFF")}", _btnStyle))
            {
                Settings.AutoEdgeTransfers = !Settings.AutoEdgeTransfers;
                Settings.Save(_declare.FolderPath);
            }

            if (GUILayout.Button("Dispatch Expedition Force (12 Soldiers)", _btnStyle))
            {
                Settings.Save(_declare.FolderPath);
                LaunchManualExpedition();
            }

            if (GUILayout.Button($"Land Incoming Fleets ({_pendingIncoming} waiting)", _btnStyle))
            {
                ReceiveIncomingFleets();
            }

            GUI.DragWindow();
        }
    }
}
