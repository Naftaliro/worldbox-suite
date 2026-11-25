using System;
using System.Reflection;
using HarmonyLib;
using NeoModLoader.api;
using UnityEngine;

namespace ApexFactions
{
    public class Main : MonoBehaviour, IMod
    {
        public static Main Instance { get; private set; }
        public static ApexFactionsConfig Settings { get; private set; }

        private ModDeclare _declare;
        private GameObject _gameObject;
        private Harmony _harmony;

        private bool _showHud = false;
        private GuiOverlayRunner _overlayRunner;
        private GuiOverlayRunner _panelRunner;
        private float _wardenTimer = 0f;
        private float _wildTimer = 0f;
        private Rect _windowRect = new Rect(695, 320, 365, 440);
        private Vector2 _scroll;
        private string _status = "";

        public ModDeclare GetDeclaration() => _declare;
        public GameObject GetGameObject() => _gameObject;
        public string GetUrl() => "https://github.com/naftaliro/worldbox-suite";

        public void OnLoad(ModDeclare pModDecl, GameObject pGameObject)
        {
            Instance = this;
            _declare = pModDecl;
            _gameObject = pGameObject;
            _overlayRunner = pGameObject.AddComponent<GuiOverlayRunner>();
            _overlayRunner.Configure(false, WildSovereigntySystem.DrawDomainOverlays);
            _panelRunner = pGameObject.AddComponent<GuiOverlayRunner>();
            _panelRunner.Configure(true, DrawPanelGui);
            Settings = ApexFactionsConfig.LoadOrCreate(_declare.FolderPath);

            try
            {
                _harmony = new Harmony(_declare.UID);
                _harmony.PatchAll(Assembly.GetExecutingAssembly());
                Debug.Log($"[ApexFactions] Loaded v{_declare.Version} (mercy={Settings.MercyThresholdPercent}%)");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ApexFactions] Init failed: {e}");
            }
        }

        public void OnUnload()
        {
            _harmony?.UnpatchSelf();
            Debug.Log("[ApexFactions] Unloaded.");
        }

        private void SyncRunners()
        {
            bool needOverlay = Settings != null && Settings.ShowFeralDomainRings && WildSovereigntySystem.ActiveDomains.Count > 0;
            if (_overlayRunner != null) _overlayRunner.enabled = needOverlay;
            if (_panelRunner != null) _panelRunner.enabled = _showHud;
        }

        public void ToggleHud()
        {
            _showHud = !_showHud;
            SyncRunners();
        }

        public bool GetState(string key)
        {
            if (Settings == null) return false;
            if (key == "domains") return Settings.ShowFeralDomainRings;
            if (key == "wardens") return Settings.EnableWardenDoctrine;
            return false;
        }

        public string SpawnWardenAtTile(WorldTile tile)
        {
            int n = WardenSystem.SpawnWardenColonyAtTile(tile);
            return n > 0 ? $"Dropped {n} Warden settlers!" : "Click on solid ground to drop Wardens.";
        }

        public string SpawnWolfkinAtTile(WorldTile tile)
        {
            int n = WildSovereigntySystem.SpawnWolfkinAtTile(tile, 12);
            return n > 0 ? $"Dropped {n} sapient Wolfkin!" : "Click on solid ground to drop Wolfkin.";
        }

        public string AnointTopKingdom()
        {
            Kingdom top = FindLargestNonWardenKingdom();
            if (top == null) return "No non-Warden kingdom found yet.";
            WardenSystem.AnointKingdomAsWardens(top);
            return $"Anointed {top.name} as Sleeping Giant Wardens!";
        }

        public string AwakenWolves()
        {
            int n = WildSovereigntySystem.AwakenWildSubspecies("wolf");
            return n > 0 ? $"Awakened {n} wild wolf subspecies into sapience!" : "No wild wolf subspecies found on map.";
        }

        public string AwakenAllAnimals()
        {
            int n = WildSovereigntySystem.AwakenWildSubspecies(null);
            return n > 0 ? $"Awakened {n} wild animal subspecies into sapience!" : "No wild animal subspecies found.";
        }

        public string ToggleDomainRings()
        {
            Settings.ShowFeralDomainRings = !Settings.ShowFeralDomainRings;
            Settings.Save(_declare.FolderPath);
            SyncRunners();
            return $"Feral Domain Borders: {(Settings.ShowFeralDomainRings ? "ON" : "OFF")}";
        }

        private void Update()
        {
            if (Settings == null) return;

            if (Input.GetKeyDown(Settings.HudToggleKey))
            {
                ToggleHud();
            }

            if (Config.paused || MapBox.instance == null) return;

            float dt = Time.deltaTime;
            _wardenTimer += dt;
            if (_wardenTimer >= 2.0f)
            {
                WardenSystem.Tick(_wardenTimer);
                _wardenTimer = 0f;
            }

            _wildTimer += dt;
            if (_wildTimer >= 10.0f)
            {
                _wildTimer = 0f;
                WildSovereigntySystem.ScanFeralDomains();
                SyncRunners();
            }
        }

        private void DrawPanelGui()
        {
            if (!_showHud || Settings == null) return;
            GameUiStyle.BeginFrame();
            _windowRect = GUI.Window(941212, _windowRect, DrawHud, "ApexFactions (F12)", GameUiStyle.WindowStyle);
            GameUiStyle.DrawTooltip(949007);
        }

        private void DrawHud(int id)
        {
            GameUiStyle.DrawCloseButton(_windowRect, ToggleHud);
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(390));

            GameUiStyle.Label($"Warden Doctrine ({WardenSystem.WardenKingdomIds.Count} kingdoms)", "Sleeping Giant Wardens|Peaceful powerhouse kingdoms that never start wars, rally fiercely when attacked, and track a 3-strikes grudge per aggressor.", isHeader: true);
            string mercyDesc = Settings.MercyThresholdPercent > 0f
                ? $"Mercy Ceasefire: stop retaliation at {Settings.MercyThresholdPercent:0}% pop"
                : "Mercy Ceasefire: OFF (total elimination)";
            float newPct = GameUiStyle.SliderRow(
                mercyDesc,
                Settings.MercyThresholdPercent,
                0f,
                50f,
                "Mercy Ceasefire Threshold|On Strike #1, Wardens force peace once an aggressor drops to this % of its war-start pop (5% on Strike #2, zero mercy on Strike #3+).");
            if (Mathf.Abs(newPct - Settings.MercyThresholdPercent) >= 1f)
            {
                Settings.MercyThresholdPercent = Mathf.Round(newPct);
                Settings.Save(_declare.FolderPath);
            }

            if (GameUiStyle.Button(
                "Spawn Warden Colony (14 Enhanced Humans)",
                "Spawn Warden Settlers|Drops 14 elite human colonists at your camera center and marks their new kingdom with the peaceful Warden doctrine."))
            {
                WardenSystem.SpawnWardenColonyAtCamera();
                _status = "Spawned Warden colony at camera!";
            }

            if (GameUiStyle.Button(
                "Anoint Largest Kingdom as Wardens",
                "Anoint Existing Empire|Bestows the Warden Sleeping Giant doctrine onto the largest non-Warden civilized kingdom in the world."))
            {
                Kingdom top = FindLargestNonWardenKingdom();
                if (top != null)
                {
                    WardenSystem.AnointKingdomAsWardens(top);
                    _status = $"Anointed {top.name} as Wardens.";
                }
            }

            if (WardenSystem.ActiveRetaliations.Count > 0)
            {
                GUILayout.Space(4);
                GameUiStyle.Label("Active Sleeping Giant Retaliations:", "Active Warden Wars|Tracks each aggressor's strike count and live population toward the mercy ceasefire threshold.", isHeader: true);
                foreach (var tr in WardenSystem.ActiveRetaliations.Values)
                {
                    string mercyTxt = tr.EffectiveMercyPercent > 0f
                        ? $"ceasefire @ {tr.MercyTargetPop} ({tr.EffectiveMercyPercent:0}%)"
                        : "NO MERCY";
                    GameUiStyle.Label(
                        $"• [Strike #{tr.StrikeNumber}] {tr.WardenName} -> {tr.AggressorName}: {tr.CurrentAggressorPop}/{tr.BaselinePop} ({mercyTxt})",
                        $"Grudge Tracker|{tr.AggressorName} provoked {tr.WardenName} (Strike #{tr.StrikeNumber}). Current pop: {tr.CurrentAggressorPop} of {tr.BaselinePop} war-start pop.");
                }
            }

            GUILayout.Space(8);
            GameUiStyle.Label($"Wild Sovereignty ({WildSovereigntySystem.ActiveDomains.Count} Feral Domains)", "Wild Realms|Awaken wild animal subspecies into sapient civilizations or monitor feral hunting territories ruled by giant Alphas.", isHeader: true);

            if (GameUiStyle.Button(
                "Spawn Wolfkin Kingdom (12 civ_wolf)",
                "Spawn Wolfkin Settlers|Places 12 sapient Wolfkin settlers at your camera center so they found their own civilization."))
            {
                int n = WildSovereigntySystem.SpawnWolfkinKingdomAtCamera(12);
                _status = $"Spawned {n} Wolfkin settlers!";
            }

            if (GameUiStyle.Button(
                "Awaken All Wild Wolves (Instant Sapience)",
                "Awaken Wild Wolves|Grants instant sapience (makeSapient) to every wild wolf subspecies on the map so they can build kingdoms."))
            {
                int n = WildSovereigntySystem.AwakenWildSubspecies("wolf");
                _status = n > 0 ? $"Awakened {n} wolf subspecies!" : "No wild wolf subspecies found.";
            }

            if (GameUiStyle.Button(
                "Awaken All Wild Animals (Full Sapience)",
                "Awaken Every Wild Species|Grants instant sapience to all wild animal subspecies across the entire world."))
            {
                int n = WildSovereigntySystem.AwakenWildSubspecies(null);
                _status = $"Awakened {n} wild animal subspecies!";
            }

            if (GameUiStyle.ToggleButton(
                $"Feral Domain Borders: {(Settings.ShowFeralDomainRings ? "ON" : "OFF")}",
                Settings.ShowFeralDomainRings,
                "Feral Domain Rings|Draws territory rings and labels on the map over large wild pack hunting grounds (Land of the Wolves, Bears, etc.)."))
            {
                Settings.ShowFeralDomainRings = !Settings.ShowFeralDomainRings;
                Settings.Save(_declare.FolderPath);
            }

            foreach (var d in WildSovereigntySystem.ActiveDomains)
            {
                GUILayout.BeginHorizontal();
                GameUiStyle.Label($"• {d.DomainTitle} ({d.PackCount})", $"Feral Territory|{d.DomainTitle} holds a pack of {d.PackCount} wild animals led by an Alpha.");
                if (GameUiStyle.Button("Go", $"Inspect {d.DomainTitle}|Center the camera on this feral hunting ground.", GUILayout.Width(42)))
                {
                    Camera cam = Camera.main;
                    if (cam != null) cam.transform.position = new Vector3(d.CenterX, d.CenterY, cam.transform.position.z);
                }
                GUILayout.EndHorizontal();
            }

            if (!string.IsNullOrEmpty(_status))
            {
                GUILayout.Space(6);
                GameUiStyle.Label(_status, null, isHeader: true);
            }

            GUILayout.EndScrollView();
            GameUiStyle.CaptureTooltip();
            GUI.DragWindow();
        }

        private static Kingdom FindLargestNonWardenKingdom()
        {
            var world = MapBox.instance;
            if (world?.kingdoms == null) return null;
            Kingdom best = null;
            foreach (var k in world.kingdoms)
            {
                if (k == null || !k.isCiv() || !k.isAlive() || WardenSystem.WardenKingdomIds.Contains(k.id)) continue;
                if (best == null || k.getPopulationPeople() > best.getPopulationPeople()) best = k;
            }
            return best;
        }
    }
}
