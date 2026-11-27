using System;
using System.Collections.Generic;
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
        private int _activeTab = 0; // 0 = Wardens & Rules, 1 = Species Manager
        private GuiOverlayRunner _panelRunner;

        private float _wardenTimer = 0f;
        private float _autoFoundTimer = 0f;
        private float _wildTimer = 0f;
        private float _thiefRaidTimer = 0f;
        private float _snakeBetrayalTimer = 0f;
        private float _lastUpdateWarnTime = -99f;

        private Rect _windowRect = new Rect(640, 200, 420, 500);
        private Vector2 _rulesScroll;
        private Vector2 _speciesScroll;
        private string _status = "";

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

            Settings = ApexFactionsConfig.LoadOrCreate(_declare.FolderPath);
            WardenSprites.Load(_declare.FolderPath);
            WardenSpeciesRegistry.EnsureRegistered();
            SpeciesPersonalityRegistry.EnsureOpinionsRegistered();
            AlienThreatSystem.EnsureOpinionRegistered();

            try
            {
                _harmony = new Harmony(_declare.UID);
                _harmony.PatchAll(Assembly.GetExecutingAssembly());
                Debug.Log($"[ApexFactions] Loaded v{_declare.Version} (mercy={Settings.MercyThresholdPercent}%, autoFound={Settings.AutoFoundAnimalKingdoms}, preset={Settings.AnimalFoundingPreset})");
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
            if (_panelRunner != null) _panelRunner.enabled = _showHud;
        }

        public void ToggleHud()
        {
            _showHud = !_showHud;
            SyncRunners();
        }

        // Public cross-mod API for SuiteHub
        public bool GetState(string key)
        {
            if (Settings == null || string.IsNullOrEmpty(key)) return false;
            switch (key)
            {
                case "warden_doctrine":
                case "wardens":
                    return Settings.EnableWardenDoctrine;
                case "auto_kingdoms":
                case "auto_animal_kingdoms":
                case "animal_kingdoms":
                    return Settings.AutoFoundAnimalKingdoms;
                case "snake_deceitful":
                case "snake_mode":
                case "snake":
                case "snakes":
                    return Settings.DeceitfulSnakesMode;
                case "domain_rings":
                    return Settings.ShowFeralDomainRings;
                case "alien_defense":
                    return Settings.EnableAlienCountermeasures;
                default:
                    return false;
            }
        }

        public string GetWarCause(long warId)
        {
            string wardenCause = WardenSystem.GetWarCauseAnnotation(warId);
            if (!string.IsNullOrEmpty(wardenCause)) return wardenCause;
            return SpeciesPersonalityRegistry.GetWarCauseAnnotation(warId);
        }

        public string SpawnWardenAtTile(WorldTile tile)
        {
            int n = WardenSystem.SpawnWardenColonyAtTile(tile, out string kName);
            if (n <= 0) return "Click on solid land to drop Wardens.";
            return !string.IsNullOrEmpty(kName)
                ? $"Founded Warden Kingdom: {kName} ({n} Wardens)!"
                : $"Dropped {n} elite Wardens (drop on unclaimed land to found a new kingdom immediately).";
        }

        public string SpawnWolfkinAtTile(WorldTile tile)
        {
            int n = WildSovereigntySystem.SpawnWolfkinAtTile(tile, 12, out string kName);
            if (n <= 0) return "Click on solid land to drop Wolfkin.";
            return !string.IsNullOrEmpty(kName)
                ? $"Founded Wolfkin Kingdom: {kName} ({n} Wolfkin)!"
                : $"Dropped {n} sapient Wolfkin (drop on unclaimed land to found a kingdom immediately).";
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

        public string ToggleAutoAnimalKingdoms()
        {
            if (Settings == null) return "Config not ready.";
            Settings.AutoFoundAnimalKingdoms = !Settings.AutoFoundAnimalKingdoms;
            Settings.Save(_declare.FolderPath);
            string msg = $"Auto Animal Kingdoms: {(Settings.AutoFoundAnimalKingdoms ? "ON" : "OFF")}";
            _status = msg;
            return msg;
        }

        public string ToggleSnakeMode()
        {
            if (Settings == null) return "Config not ready.";
            Settings.DeceitfulSnakesMode = !Settings.DeceitfulSnakesMode;
            Settings.Save(_declare.FolderPath);
            string msg = Settings.DeceitfulSnakesMode
                ? "Snakes: Serpent's Guile (Betrayers)"
                : "Snakes: Peaceful Ambush (Isolationist)";
            _status = msg;
            return msg;
        }

        public string CycleFoundingPreset()
        {
            if (Settings == null) return "Config not ready.";
            string next = Settings.AnimalFoundingPreset == "AllEligible"
                ? "ApexAndSmart"
                : (Settings.AnimalFoundingPreset == "ApexAndSmart" ? "Custom" : "AllEligible");
            Settings.ApplyPreset(next);
            Settings.Save(_declare.FolderPath);
            string msg = $"Animal Founding Preset: {FormatPresetName(next)}";
            _status = msg;
            return msg;
        }

        public string ToggleDomainRings()
        {
            if (Settings == null) return "Config not ready.";
            Settings.ShowFeralDomainRings = !Settings.ShowFeralDomainRings;
            Settings.Save(_declare.FolderPath);
            SyncRunners();
            return $"Feral Domain Borders: {(Settings.ShowFeralDomainRings ? "ON" : "OFF")}";
        }

        public string ToggleAlienCountermeasures()
        {
            if (Settings == null) return "Config not ready.";
            Settings.EnableAlienCountermeasures = !Settings.EnableAlienCountermeasures;
            Settings.Save(_declare.FolderPath);
            string msg = $"Anti-Alien Coalition & Xeno Salvage: {(Settings.EnableAlienCountermeasures ? "ON" : "OFF")}";
            _status = msg;
            return msg;
        }

        private void Update()
        {
            if (Settings == null) return;

            if (Input.GetKeyDown(Settings.HudToggleKey))
            {
                ToggleHud();
            }

            if (MapBox.instance == null) return;

            try
            {
                // Ensure species & diplomacy opinions register as soon as AssetManager finishes initializing
                WardenSpeciesRegistry.EnsureRegistered();
                SpeciesPersonalityRegistry.EnsureOpinionsRegistered();
                AlienThreatSystem.EnsureOpinionRegistered();

                if (Config.paused) return;

                float dt = Time.deltaTime;
                AlienThreatSystem.Tick(dt);

                _wardenTimer += dt;
                if (_wardenTimer >= 2.0f)
                {
                    WardenSystem.Tick(_wardenTimer);
                    _wardenTimer = 0f;
                }

                _autoFoundTimer += dt;
                if (_autoFoundTimer >= 4.0f)
                {
                    _autoFoundTimer = 0f;
                    WildSovereigntySystem.ScanAndAutoFoundAnimalKingdoms();
                }

                _wildTimer += dt;
                if (_wildTimer >= 10.0f)
                {
                    _wildTimer = 0f;
                    WildSovereigntySystem.ScanFeralDomains();
                    SpeciesPersonalityRegistry.CleanupFinishedWars();
                    SyncRunners();
                }

                _thiefRaidTimer += dt;
                if (_thiefRaidTimer >= 27.0f)
                {
                    _thiefRaidTimer = 0f;
                    SpeciesPersonalityRegistry.TickThiefRaids();
                }

                _snakeBetrayalTimer += dt;
                if (_snakeBetrayalTimer >= 45.0f)
                {
                    _snakeBetrayalTimer = 0f;
                    SpeciesPersonalityRegistry.TickSnakeBetrayals();
                }
            }
            catch (Exception e)
            {
                float now = Time.unscaledTime;
                if (now - _lastUpdateWarnTime >= 30f)
                {
                    _lastUpdateWarnTime = now;
                    Debug.LogWarning($"[ApexFactions] Update tick exception: {e.Message}");
                }
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

            // 2-Tab Navigation Bar
            GUILayout.BeginHorizontal();
            if (GameUiStyle.ToggleButton(
                "Wardens & Rules",
                _activeTab == 0,
                "Wardens & Rules|Manage Sleeping Giant Warden kingdoms, mercy thresholds, and wild sovereignty actions."))
            {
                _activeTab = 0;
            }
            if (GameUiStyle.ToggleButton(
                "Species Manager (30)",
                _activeTab == 1,
                "Species Manager|Configure auto-founding animal kingdoms, presets, cluster size, snake doctrine, and all 30 species toggles."))
            {
                _activeTab = 1;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            if (_activeTab == 0)
            {
                DrawWardensAndRulesTab();
            }
            else
            {
                DrawSpeciesManagerTab();
            }

            if (!string.IsNullOrEmpty(_status))
            {
                GUILayout.Space(4);
                GameUiStyle.Label(_status, null, isHeader: true);
            }

            GameUiStyle.CaptureTooltip();
            GUI.DragWindow();
        }

        private void DrawWardensAndRulesTab()
        {
            _rulesScroll = GUILayout.BeginScrollView(_rulesScroll, GUILayout.Height(405));

            int aliveWardens = WardenSystem.CountAliveWardenKingdoms();
            GameUiStyle.Label(
                $"Warden Doctrine ({aliveWardens} kingdoms)",
                "Sleeping Giant Wardens|Dedicated elite Warden species (1v30 calibrated). Peaceful until attacked; tracks a 3-strikes grudge per aggressor.",
                isHeader: true);

            if (GameUiStyle.ToggleButton(
                $"Warden Sleeping Giant Doctrine: {(Settings.EnableWardenDoctrine ? "ON" : "OFF")}",
                Settings.EnableWardenDoctrine,
                "Warden Doctrine|Prevents Wardens from starting offensive wars and triggers mercy-scaled retaliation when provoked."))
            {
                Settings.EnableWardenDoctrine = !Settings.EnableWardenDoctrine;
                Settings.Save(_declare.FolderPath);
                _status = $"Warden Doctrine: {(Settings.EnableWardenDoctrine ? "ON" : "OFF")}";
            }

            string threatLabel = AlienThreatSystem.IsAlienThreatActive ? $"ACTIVE ({AlienThreatSystem.ActiveAlienUnits})" : "Clear";
            if (GameUiStyle.ToggleButton(
                $"Anti-Alien Coalition & Salvage: {(Settings.EnableAlienCountermeasures ? "ON" : "OFF")} [{threatLabel}]",
                Settings.EnableAlienCountermeasures,
                $"Anti-Alien Coalition & Xeno Reverse-Engineering|Unites non-Alien kingdoms (+85 opinion & war truce) during Alien invasions. Slaying Aliens grants fireproofing, mythril/adamantine salvage, and salvaged Alien Blasters at 3+ kills.\nAliens Slain: {AlienThreatSystem.TotalAliensSlain}  |  Blasters Salvaged: {AlienThreatSystem.TotalBlastersSalvaged}"))
            {
                ToggleAlienCountermeasures();
            }

            string mercyDesc = Settings.MercyThresholdPercent > 0f
                ? $"Mercy Ceasefire: stop Strike #1 at {Settings.MercyThresholdPercent:0}% pop"
                : "Mercy Ceasefire: OFF (total elimination)";
            float newPct = GameUiStyle.SliderRow(
                mercyDesc,
                Settings.MercyThresholdPercent,
                0f,
                50f,
                "Mercy Ceasefire Threshold|Strike #1 forces peace at this % of war-start pop; Strike #2 drops to 5%; Strike #3+ shows no mercy.");
            if (Mathf.Abs(newPct - Settings.MercyThresholdPercent) >= 1f)
            {
                Settings.MercyThresholdPercent = Mathf.Round(newPct);
                Settings.Save(_declare.FolderPath);
            }

            if (GameUiStyle.Button(
                "Spawn Warden Colony (10 Elite Wardens)",
                "Spawn Warden Settlers|Drops 10 dedicated Warden species colonists at camera center to found a peaceful Warden kingdom."))
            {
                WardenSystem.SpawnWardenColonyAtCamera();
                _status = "Spawned 10 elite Wardens at camera!";
            }

            if (GameUiStyle.Button(
                "Anoint Largest Kingdom as Wardens",
                "Anoint Existing Empire|Bestows the Warden Sleeping Giant doctrine onto the largest non-Warden civilized kingdom."))
            {
                _status = AnointTopKingdom();
            }

            if (WardenSystem.ActiveRetaliations.Count > 0)
            {
                GUILayout.Space(4);
                GameUiStyle.Label("Active Sleeping Giant Retaliations:", "Active Warden Wars|Live retaliation trackers showing strike number and remaining aggressor population.", isHeader: true);
                var retaliations = new List<RetaliationTracker>(WardenSystem.ActiveRetaliations.Values);
                for (int i = 0; i < retaliations.Count; i++)
                {
                    var tr = retaliations[i];
                    string mercyTxt = tr.EffectiveMercyPercent > 0f
                        ? $"ceasefire @ {tr.MercyTargetPop} ({tr.EffectiveMercyPercent:0}%)"
                        : "NO MERCY";
                    GameUiStyle.Label(
                        $"• [Strike #{tr.StrikeNumber}] {tr.WardenName} -> {tr.AggressorName}: {tr.CurrentAggressorPop}/{tr.BaselinePop} ({mercyTxt})",
                        $"Grudge Tracker|{tr.AggressorName} provoked {tr.WardenName} (Strike #{tr.StrikeNumber}).");
                }
            }

            GUILayout.Space(8);
            GameUiStyle.Label(
                $"Wild Sovereignty ({WildSovereigntySystem.ActiveDomains.Count} Feral Domains)",
                "Wild Realms|Literal animal kingdoms that self-found when wild packs gather, plus feral Alpha hunting territories.",
                isHeader: true);

            if (GameUiStyle.ToggleButton(
                $"Auto-Found Animal Kingdoms: {(Settings.AutoFoundAnimalKingdoms ? "ON" : "OFF")}",
                Settings.AutoFoundAnimalKingdoms,
                $"Auto-Founding|When {Settings.ClusterSizeToFound}+ allowed wild animals gather on unclaimed land, they awaken sapience and found a literal animal kingdom."))
            {
                ToggleAutoAnimalKingdoms();
            }

            if (GameUiStyle.ToggleButton(
                Settings.DeceitfulSnakesMode ? "Snake Mode: Serpent's Guile (Betrayers)" : "Snake Mode: Peaceful Ambush (Isolationist)",
                Settings.DeceitfulSnakesMode,
                "Snake Doctrine|Serpent's Guile (-25 opinion, alliance backstabs) vs Peaceful Ambush (0 opinion, defensive venomous isolationists)."))
            {
                ToggleSnakeMode();
            }

            if (GameUiStyle.Button(
                "Spawn Wolfkin Kingdom (12 Wolfkin)",
                "Spawn Wolfkin Settlers|Places 12 sapient Wolfkin settlers with Wolf Pack Warlord traits at camera center."))
            {
                int n = WildSovereigntySystem.SpawnWolfkinKingdomAtCamera(12);
                _status = $"Spawned {n} Wolfkin settlers!";
            }

            GUILayout.BeginHorizontal();
            if (GameUiStyle.Button(
                "Awaken Wild Wolves",
                "Awaken Wild Wolves|Grants instant sapience + Wolf archetype traits to every wild wolf subspecies on the map."))
            {
                _status = AwakenWolves();
            }
            if (GameUiStyle.Button(
                "Awaken All Animals",
                "Awaken Every Wild Species|Grants instant sapience + species traits to all eligible wild animal subspecies."))
            {
                _status = AwakenAllAnimals();
            }
            GUILayout.EndHorizontal();

            var domains = new List<FeralPackDomain>(WildSovereigntySystem.ActiveDomains);
            for (int i = 0; i < domains.Count; i++)
            {
                var d = domains[i];
                GUILayout.BeginHorizontal();
                GameUiStyle.Label($"• {d.DomainTitle} ({d.PackCount})", $"Feral Territory|{d.DomainTitle} holds a pack of {d.PackCount} wild animals led by an Alpha.");
                if (GameUiStyle.Button("Go", $"Inspect {d.DomainTitle}|Center the camera on this feral hunting ground.", GUILayout.Width(42)))
                {
                    Camera cam = Camera.main;
                    if (cam != null) cam.transform.position = new Vector3(d.CenterX, d.CenterY, cam.transform.position.z);
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
        }

        private void DrawSpeciesManagerTab()
        {
            // Preset switcher
            GameUiStyle.Label(
                $"Founding Preset: {FormatPresetName(Settings.AnimalFoundingPreset)}",
                "Animal Founding Preset|Choose whether all 30 normal animals, only apex/smart animals, or a custom selection can auto-found kingdoms.",
                isHeader: true);

            GUILayout.BeginHorizontal();
            if (GameUiStyle.ToggleButton("All Eligible", Settings.AnimalFoundingPreset == "AllEligible", "All 30 verified normal animal species may auto-found kingdoms."))
            {
                Settings.ApplyPreset("AllEligible");
                Settings.Save(_declare.FolderPath);
                _status = "Preset: All Eligible (30 species)";
            }
            if (GameUiStyle.ToggleButton("Apex & Smart", Settings.AnimalFoundingPreset == "ApexAndSmart", "Only apex predators, clever mammals, penguins, snakes, and unicorns auto-found."))
            {
                Settings.ApplyPreset("ApexAndSmart");
                Settings.Save(_declare.FolderPath);
                _status = "Preset: Apex & Smart";
            }
            if (GameUiStyle.ToggleButton("Custom", Settings.AnimalFoundingPreset == "Custom", "Toggle individual species ON or OFF below."))
            {
                Settings.ApplyPreset("Custom");
                Settings.Save(_declare.FolderPath);
                _status = "Preset: Custom";
            }
            GUILayout.EndHorizontal();

            // Cluster size slider (4..25)
            float nextCluster = GameUiStyle.SliderRow(
                $"Cluster Size to Found: {Settings.ClusterSizeToFound} animals (radius {Settings.ClusterRadiusTiles:0}t)",
                Settings.ClusterSizeToFound,
                4f,
                25f,
                "Cluster Size|Minimum wild animals of the same species within cluster radius on unclaimed land required to found a kingdom.");
            int roundedCluster = Mathf.Clamp(Mathf.RoundToInt(nextCluster), 4, 25);
            if (roundedCluster != Settings.ClusterSizeToFound)
            {
                Settings.ClusterSizeToFound = roundedCluster;
                Settings.Save(_declare.FolderPath);
            }

            // Snake mode toggle
            if (GameUiStyle.ToggleButton(
                Settings.DeceitfulSnakesMode ? "Snakes: Serpent's Guile (Betrayers)" : "Snakes: Peaceful Ambush (Defensive)",
                Settings.DeceitfulSnakesMode,
                "Snake Doctrine|Toggle whether Snake kingdoms act as deceitful alliance-betrayers or peaceful venomous isolationists."))
            {
                ToggleSnakeMode();
            }

            GUILayout.Space(4);
            _speciesScroll = GUILayout.BeginScrollView(_speciesScroll, GUILayout.Height(285));

            // Active animal species kingdoms summary
            var activeAnimalKingdoms = CollectActiveAnimalKingdoms();
            if (activeAnimalKingdoms.Count > 0)
            {
                GameUiStyle.Label(
                    $"Active Animal Kingdoms ({activeAnimalKingdoms.Count})",
                    "Living Animal Civilizations|Animal kingdoms currently thriving on this map with active species personalities.",
                    isHeader: true);

                for (int i = 0; i < activeAnimalKingdoms.Count; i++)
                {
                    var item = activeAnimalKingdoms[i];
                    GameUiStyle.Subtle(
                        $"• {item.KingdomName} — {item.SpeciesDisplay} ({item.Archetype}, pop {item.Pop})",
                        $"{item.KingdomName}|{item.SpeciesDisplay} civilization following the {item.Archetype} doctrine.");
                }
                GUILayout.Space(6);
            }

            GameUiStyle.Label(
                "All 30 Species Auto-Founding Toggles:",
                "Species Allowlist|Click any species toggle to allow or block auto-founding. Hover for full archetype and diplomacy details.",
                isHeader: true);

            for (int i = 0; i < SpeciesPersonalityRegistry.AllSpeciesIds.Count; i++)
            {
                string spId = SpeciesPersonalityRegistry.AllSpeciesIds[i];
                if (!SpeciesPersonalityRegistry.TryGetProfile(spId, out var profile)) continue;

                bool allowed = Settings.IsSpeciesAllowedToFound(spId);
                string arch = SpeciesPersonalityRegistry.GetEffectiveArchetypeTitle(profile);
                int opMod = SpeciesPersonalityRegistry.GetEffectiveOpinionModifier(spId);
                string opStr = opMod > 0 ? $"+{opMod}" : (opMod < 0 ? $"{opMod}" : "0");
                string traitsLine = string.Join(", ", SpeciesPersonalityRegistry.GetEffectiveBirthTraits(profile));
                string tip = $"{profile.DisplayName} — {arch}|{profile.Description}|Opinion {opStr} • Traits: {traitsLine}";

                GUILayout.BeginHorizontal();
                if (GameUiStyle.ToggleButton(allowed ? "ON" : "OFF", allowed, tip, GUILayout.Width(44)))
                {
                    Settings.SetSpeciesAllowed(spId, !allowed);
                    Settings.Save(_declare.FolderPath);
                    _status = $"{profile.DisplayName} auto-founding: {(!allowed ? "ON" : "OFF")}";
                }
                GameUiStyle.Label($"{profile.DisplayName} — {arch} ({opStr})", tip);
                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
        }

        private struct ActiveAnimalKingdomInfo
        {
            public string KingdomName;
            public string SpeciesDisplay;
            public string Archetype;
            public int Pop;
        }

        private static List<ActiveAnimalKingdomInfo> CollectActiveAnimalKingdoms()
        {
            var result = new List<ActiveAnimalKingdomInfo>();
            var world = MapBox.instance;
            if (world?.kingdoms == null) return result;

            var kingdoms = new List<Kingdom>(world.kingdoms);
            for (int i = 0; i < kingdoms.Count; i++)
            {
                Kingdom k = kingdoms[i];
                if (k == null || !k.isAlive() || !k.isCiv()) continue;
                string sp = SpeciesPersonalityRegistry.GetKingdomSpecies(k);
                if (SpeciesPersonalityRegistry.TryGetProfile(sp, out var prof))
                {
                    result.Add(new ActiveAnimalKingdomInfo
                    {
                        KingdomName = k.name,
                        SpeciesDisplay = prof.DisplayName,
                        Archetype = SpeciesPersonalityRegistry.GetEffectiveArchetypeTitle(prof),
                        Pop = k.getPopulationPeople()
                    });
                }
            }
            return result;
        }

        private static string FormatPresetName(string preset)
        {
            switch (preset)
            {
                case "AllEligible": return "All Eligible";
                case "ApexAndSmart": return "Apex & Smart";
                case "Custom": return "Custom";
                default: return preset ?? "All Eligible";
            }
        }

        private static Kingdom FindLargestNonWardenKingdom()
        {
            var world = MapBox.instance;
            if (world?.kingdoms == null) return null;
            Kingdom best = null;
            var kingdoms = new List<Kingdom>(world.kingdoms);
            for (int i = 0; i < kingdoms.Count; i++)
            {
                Kingdom k = kingdoms[i];
                if (k == null || !k.isCiv() || !k.isAlive() || WardenSystem.IsWardenKingdom(k)) continue;
                if (best == null || k.getPopulationPeople() > best.getPopulationPeople()) best = k;
            }
            return best;
        }
    }
}
