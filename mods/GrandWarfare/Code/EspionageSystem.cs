using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using NeoModLoader.General;
using UnityEngine;

namespace GrandWarfare
{
    public enum SpyStage
    {
        Traveling = 0,
        Infiltrating = 1,
        CourtInsider = 2,
        CityLeader = 3,
        UsurpedThrone = 4
    }

    public class SpyMission
    {
        public long SpyActorId;
        public string SpyName;
        public long OriginKingdomId;
        public string OriginName;
        public long TargetKingdomId;
        public string TargetName;
        public SpyStage Stage;
        public float Influence;
        public float Cover;
        public float ConsolidationTimer;
        public int TravelDistanceLeft;
        public float LastTravelDist;
        public int StalledTravelTicks;
    }

    public static class EspionageSystem
    {
        public const string TraitSpyId = "suite_spy";
        public const string TraitCounterIntelId = "suite_counter_intel";
        public const string OpinionScandalId = "suite_espionage_scandal";

        private const float TravelTickInterval = 2.0f;
        private const float MissionTickInterval = 5.0f;
        private const float AutoDispatchCooldownSeconds = 300.0f;
        private const int MaxGlobalAutoMissions = 2;
        private const float PendingOriginTimeoutSeconds = 45.0f;
        private const float ScandalDurationSeconds = 600.0f;
        private const int MaxRecentEvents = 24;

        public static readonly List<SpyMission> ActiveMissions = new List<SpyMission>();
        public static readonly Dictionary<long, string> EspionageWarCauses = new Dictionary<long, string>();
        public static readonly List<string> RecentEvents = new List<string>();

        public static int SpiesDispatched { get; private set; }
        public static int SpiesCaught { get; private set; }
        public static int CoupsSucceeded { get; private set; }
        public static int PeacefulTakeovers { get; private set; }

        // (victimKingdomId, originKingdomId) -> simTime expiry
        private static readonly Dictionary<(long, long), float> _scandalGrudges = new Dictionary<(long, long), float>();
        // originKingdomId -> simTime when next autonomous dispatch is permitted
        private static readonly Dictionary<long, float> _originCooldowns = new Dictionary<long, float>();

        private static readonly MethodInfo StartWarMethod =
            AccessTools.Method(typeof(DiplomacyManager), "startWar",
                new[] { typeof(Kingdom), typeof(Kingdom), typeof(WarTypeAsset), typeof(bool) });

        private static readonly MethodInfo CancelAllBehMethod =
            AccessTools.Method(typeof(Actor), "cancelAllBeh", Type.EmptyTypes);

        private static readonly MethodInfo SetCurrentTilePositionMethod =
            AccessTools.Method(typeof(Actor), "setCurrentTilePosition", new[] { typeof(WorldTile) });

        private static readonly MethodInfo SpeciesIdGetter =
            AccessTools.PropertyGetter(typeof(Subspecies), "species_id");

        private static readonly FieldInfo StatsField =
            AccessTools.Field(typeof(BaseSimObject), "stats");

        private static readonly FieldInfo CityKingdomField =
            AccessTools.Field(typeof(City), "kingdom");

        private static readonly AccessTools.FieldRef<BaseSimObject, BaseStats> StatsRef;

        static EspionageSystem()
        {
            try
            {
                StatsRef = AccessTools.FieldRefAccess<BaseSimObject, BaseStats>("stats");
            }
            catch { }
        }

        private static MethodInfo _wardenCheckMethod;
        private static bool _wardenCheckResolved;
        private static float _wardenCheckRetryAt;

        private static bool _localesRegistered;
        private static bool _traitsRegistered;
        private static bool _traitsSanitized;
        private static bool _opinionRegistered;

        private static long _pendingOriginKingdomId = -1;
        private static float _pendingOriginTimestamp = -999f;

        private static float _simTime;
        private static float _travelTimer;
        private static float _dispatchTimer;
        private static float _missionTimer;
        private static float _cleanupTimer;

        public static void EnsureAssetsRegistered()
        {
            if (!_localesRegistered && LocalizedTextManager.instance != null)
            {
                try
                {
                    LM.AddToCurrentLocale("trait_suite_spy", "Covert Agent");
                    LM.AddToCurrentLocale("trait_suite_spy_info", "A trained foreign operative skilled in court intrigue, deception, and regime subversion.");
                    LM.AddToCurrentLocale("trait_suite_counter_intel", "Spymaster");
                    LM.AddToCurrentLocale("trait_suite_counter_intel_info", "A veteran counter-intelligence leader who roots out foreign moles and palace plots.");
                    LM.AddToCurrentLocale("opinion_suite_espionage_scandal", "Caught Foreign Spy");

                    if (LocalizedTextManager.stringExists("trait_suite_spy"))
                    {
                        _localesRegistered = true;
                    }
                }
                catch { }
            }

            if (!_traitsRegistered && AssetManager.traits != null && AssetManager.traits.has("deceitful") && AssetManager.traits.has("wise"))
            {
                try
                {
                    RegisterCustomTraits();
                    _traitsRegistered = true;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[GrandWarfare] Trait registration deferred: {e.Message}");
                }
            }

            if (!_traitsSanitized && AssetManager.traits?.list != null && AssetManager.traits.list.Count > 0)
            {
                SanitizeTraitOpposites();
                _traitsSanitized = true;
            }

            if (!_opinionRegistered && AssetManager.opinion_library != null && AssetManager.opinion_library.list != null && AssetManager.opinion_library.list.Count > 0)
            {
                try
                {
                    if (!AssetManager.opinion_library.has(OpinionScandalId))
                    {
                        AssetManager.opinion_library.add(new OpinionAsset
                        {
                            id = OpinionScandalId,
                            translation_key = "opinion_suite_espionage_scandal",
                            translation_key_negative = "opinion_suite_espionage_scandal",
                            calc = (pMain, pTarget) =>
                            {
                                if (pMain == null || pTarget == null) return 0;
                                if (_scandalGrudges.TryGetValue((pMain.id, pTarget.id), out float expiresAt) && _simTime < expiresAt)
                                {
                                    return -65;
                                }
                                return 0;
                            }
                        });
                    }
                    _opinionRegistered = true;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[GrandWarfare] Opinion registration deferred: {e.Message}");
                }
            }
        }

        private static void RegisterCustomTraits()
        {
            var lib = AssetManager.traits;

            ActorTrait spy = lib.has(TraitSpyId) ? lib.get(TraitSpyId) : lib.clone(TraitSpyId, "deceitful");
            if (spy != null)
            {
                spy.id = TraitSpyId;
                spy.can_be_given = true;
                spy.only_active_on_era_flag = false;
                spy.base_stats = new BaseStats();
                spy.base_stats["intelligence"] = 4f;
                spy.base_stats["diplomacy"] = 4f;
                spy.base_stats["speed"] = 2f;
                spy.base_stats["critical_chance"] = 0.20f;
                IsolateTraitMetadata(spy, "trait_suite_spy", "trait_suite_spy_info");
            }

            ActorTrait ci = lib.has(TraitCounterIntelId) ? lib.get(TraitCounterIntelId) : lib.clone(TraitCounterIntelId, "wise");
            if (ci != null)
            {
                ci.id = TraitCounterIntelId;
                ci.can_be_given = true;
                ci.only_active_on_era_flag = false;
                ci.base_stats = new BaseStats();
                ci.base_stats["intelligence"] = 5f;
                ci.base_stats["diplomacy"] = 3f;
                ci.base_stats["stewardship"] = 2f;
                IsolateTraitMetadata(ci, "trait_suite_counter_intel", "trait_suite_counter_intel_info");
            }

            SanitizeTraitOpposites();
        }

        private static void IsolateTraitMetadata(ActorTrait trait, string localeId, string localeDesc)
        {
            if (trait == null) return;

            trait.same_trait_mod = 0;
            trait.opposite_trait_mod = 0;
            trait.likeability = 0f;
            trait.rate_birth = 0;
            trait.rate_inherit = 0;
            trait.rate_acquire_grow_up = 0;
            trait.spawn_random_trait_allowed = false;
            trait.spawn_random_rate = 0;
            trait.is_mutation_box_allowed = false;
            trait.in_training_dummy_combat_pot = false;

            trait.opposite_traits = new HashSet<ActorTrait>();
            trait.opposite_list = new List<string>();
            trait.traits_to_remove = Array.Empty<ActorTrait>();
            trait.traits_to_remove_ids = Array.Empty<string>();
            trait.default_for_actor_assets = new List<ActorAsset>();
            trait.base_stats_meta = new BaseStats();

            trait.special_locale_id = localeId;
            trait.special_locale_description = localeDesc;
            trait.special_locale_description_2 = null;
            trait.has_localized_id = true;
            trait.has_description_1 = true;
            trait.has_description_2 = false;
        }

        private static void SanitizeTraitOpposites()
        {
            var list = AssetManager.traits?.list;
            if (list == null) return;

            for (int i = 0; i < list.Count; i++)
            {
                ActorTrait trait = list[i];
                if (trait == null) continue;
                if (trait.opposite_trait_mod != 0 && trait.opposite_traits == null)
                {
                    trait.opposite_traits = new HashSet<ActorTrait>();
                }
            }
        }

        public static void Tick(float dt)
        {
            EnsureAssetsRegistered();

            var settings = Main.Settings;
            var world = MapBox.instance;
            if (settings == null || world == null) return;

            _simTime += dt;

            _cleanupTimer += dt;
            if (_cleanupTimer >= 30f)
            {
                _cleanupTimer = 0f;
                CleanupStaleState(world);
            }

            if (!settings.EnableEspionage) return;

            _travelTimer += dt;
            if (_travelTimer >= TravelTickInterval)
            {
                _travelTimer = 0f;
                TickTravelingMissions(world);
            }

            _missionTimer += dt;
            if (_missionTimer >= MissionTickInterval)
            {
                _missionTimer = 0f;
                TickActiveMissions(world);
            }

            if (settings.EnableAutoEspionage)
            {
                float dispatchInterval = settings.ChaoticEspionageMode ? 20f : 35f;
                _dispatchTimer += dt;
                if (_dispatchTimer >= dispatchInterval)
                {
                    _dispatchTimer = 0f;
                    TryAutoDispatchSpy(world);
                }
            }
        }

        public static string GetWarCause(long warId)
        {
            return EspionageWarCauses.TryGetValue(warId, out string cause) ? cause : "";
        }

        public static string FormatStage(SpyStage stage)
        {
            switch (stage)
            {
                case SpyStage.Traveling: return "Traveling";
                case SpyStage.Infiltrating: return "Infiltrating";
                case SpyStage.CourtInsider: return "Court Insider";
                case SpyStage.CityLeader: return "City Leader";
                case SpyStage.UsurpedThrone: return "Usurped Throne";
                default: return stage.ToString();
            }
        }

        public static string FormatMissionStage(SpyMission m)
        {
            if (m == null) return "";
            if (m.Stage == SpyStage.Traveling)
            {
                return $"Traveling ({Math.Max(1, m.TravelDistanceLeft)} tiles)";
            }
            return FormatStage(m.Stage);
        }

        public static float ComputeSpySkill(Actor spy, Kingdom origin)
        {
            if (spy == null) return 25f;

            float score = 25f;
            score += GetActorStat(spy, "intelligence") * 1.4f;
            score += GetActorStat(spy, "diplomacy") * 1.2f;

            if (HasTraitSafe(spy, "deceitful")) score += 15f;
            if (HasTraitSafe(spy, "genius")) score += 12f;
            if (HasTraitSafe(spy, "ambitious")) score += 10f;
            if (HasTraitSafe(spy, "agile")) score += 8f;
            if (HasTraitSafe(spy, "lucky")) score += 8f;
            if (HasTraitSafe(spy, "attractive")) score += 8f;
            if (HasTraitSafe(spy, TraitSpyId)) score += 12f;

            string sp = GetKingdomSpecies(origin);
            switch (sp)
            {
                case "snake": score += 22f; break;
                case "fox": score += 18f; break;
                case "raccoon": score += 18f; break;
                case "rat": score += 12f; break;
                case "cat": score += 10f; break;
                case "elf": score += 8f; break;
            }

            return Mathf.Max(10f, score);
        }

        public static float ComputeCounterIntel(Kingdom target)
        {
            if (target == null) return 28f;

            float score = 28f;
            Actor king = target.king;
            if (king != null && king.isAlive())
            {
                score += GetActorStat(king, "intelligence") * 1.5f;
                score += GetActorStat(king, "stewardship") * 0.8f;

                if (HasTraitSafe(king, "genius")) score += 16f;
                if (HasTraitSafe(king, "wise")) score += 12f;
                if (HasTraitSafe(king, "paranoid")) score += 18f;
                if (HasTraitSafe(king, "strong_minded")) score += 10f;
                if (HasTraitSafe(king, "eagle_eyed")) score += 10f;
                if (HasTraitSafe(king, TraitCounterIntelId)) score += 16f;
            }

            int cityCount = target.cities != null ? target.cities.Count : 1;
            score += Math.Min(25, Math.Max(1, cityCount) * 3);

            string sp = GetKingdomSpecies(target);
            if (sp == "warden" || IsWardenKingdom(target))
            {
                score += 45f;
            }
            else
            {
                switch (sp)
                {
                    case "penguin": score += 22f; break;
                    case "turtle": score += 18f; break;
                    case "dwarf": score += 14f; break;
                    case "monkey": score += 14f; break;
                    case "dog": score += 12f; break;
                }
            }

            return Mathf.Max(10f, score);
        }

        public static void TryAutoDispatchSpy(MapBox world)
        {
            if (world?.kingdoms == null || world.units == null || world.wars == null) return;
            var settings = Main.Settings;
            if (settings == null || !settings.EnableEspionage || !settings.EnableAutoEspionage) return;

            int maxGlobal = MaxGlobalAutoMissions;
            int maxPerOrigin = 1;
            if (ActiveMissions.Count >= maxGlobal) return;

            var kingdoms = new List<Kingdom>(world.kingdoms);
            var civs = new List<Kingdom>();
            for (int i = 0; i < kingdoms.Count; i++)
            {
                Kingdom k = kingdoms[i];
                if (k != null && k.isAlive() && k.isCiv())
                {
                    civs.Add(k);
                }
            }
            if (civs.Count < 2) return;

            // Shuffle candidate scan order so all kingdoms get fair opportunities
            Kingdom chosenOrigin = null;
            Kingdom chosenTarget = null;
            float bestPairScore = float.MinValue;

            for (int i = 0; i < civs.Count; i++)
            {
                Kingdom origin = civs[i];
                if (IsPacifistOrigin(origin)) continue;
                if (origin.getPopulationPeople() < 12) continue;
                if (CountActiveFromOrigin(origin.id) >= maxPerOrigin) continue;

                if (_originCooldowns.TryGetValue(origin.id, out float readyAt) && _simTime < readyAt)
                    continue;

                int originPop = origin.getPopulationPeople();
                int originWarriors = origin.countTotalWarriors();
                float speciesBias = GetOriginEspionageAffinity(origin);

                for (int j = 0; j < civs.Count; j++)
                {
                    if (i == j) continue;
                    Kingdom target = civs[j];
                    if (target.getPopulationPeople() < 15) continue;
                    if (GetTargetCapitalTile(target) == null) continue;
                    if (AreAllied(origin, target)) continue;
                    if (world.wars.isInWarWith(origin, target)) continue;
                    if (HasMissionPair(origin.id, target.id)) continue;

                    int opinion = GetOpinionScore(world, origin, target);
                    if (opinion > 10) continue;

                    int targetPop = target.getPopulationPeople();
                    int targetWarriors = target.countTotalWarriors();
                    bool comparableOrStronger =
                        targetWarriors >= originWarriors * 0.95f || targetPop > originPop * 1.05f;
                    if (!comparableOrStronger) continue;

                    float pairScore = (-opinion) * 0.8f
                        + (targetPop - originPop) * 0.25f
                        + speciesBias
                        + UnityEngine.Random.Range(0f, 35f);

                    if (pairScore > bestPairScore)
                    {
                        bestPairScore = pairScore;
                        chosenOrigin = origin;
                        chosenTarget = target;
                    }
                }
            }

            if (chosenOrigin == null || chosenTarget == null) return;

            // In balanced mode, add a natural restraint roll so espionage feels deliberate
            if (!settings.ChaoticEspionageMode && UnityEngine.Random.value > 0.65f)
                return;

            Actor candidate = FindCandidateSpy(world, chosenOrigin);
            if (candidate == null) return;

            LaunchSpyMission(chosenOrigin, chosenTarget, candidate, showToast: false, isAutoDispatch: true);
        }

        public static string SelectSpyKingdomAtTile(WorldTile tile)
        {
            EnsureAssetsRegistered();
            var world = MapBox.instance;
            if (world?.kingdoms == null || world.units == null || tile == null)
            {
                return "Click inside a civilized kingdom to select Spy Origin / Target.";
            }

            Kingdom clickedKingdom = FindKingdomAtOrNearTile(world, tile, 20f);
            if (clickedKingdom == null || !clickedKingdom.isAlive() || !clickedKingdom.isCiv())
            {
                return "Click inside a civilized kingdom to select Spy Origin / Target.";
            }

            // Expire stale pending selection after 45s
            if (_pendingOriginKingdomId > 0 && (Time.unscaledTime - _pendingOriginTimestamp > PendingOriginTimeoutSeconds))
            {
                _pendingOriginKingdomId = -1;
            }

            Kingdom pendingOrigin = _pendingOriginKingdomId > 0 ? world.kingdoms.get(_pendingOriginKingdomId) : null;
            if (pendingOrigin == null || !pendingOrigin.isAlive() || !pendingOrigin.isCiv())
            {
                _pendingOriginKingdomId = -1;
                pendingOrigin = null;
            }

            // 1st Click: Lock in Spy Origin Kingdom
            if (pendingOrigin == null)
            {
                _pendingOriginKingdomId = clickedKingdom.id;
                _pendingOriginTimestamp = Time.unscaledTime;
                return $"Spy Origin: {clickedKingdom.name} — now click a rival Target Kingdom to infiltrate!";
            }

            // 2nd Click on same kingdom: Cancel selection
            if (clickedKingdom.id == _pendingOriginKingdomId)
            {
                _pendingOriginKingdomId = -1;
                return $"Cancelled spy dispatch from {clickedKingdom.name}.";
            }

            // 2nd Click on rival Target Kingdom: Dispatch covert agent physically toward target capital
            _pendingOriginKingdomId = -1;

            if (GetTargetCapitalTile(clickedKingdom) == null)
            {
                return $"Target kingdom {clickedKingdom.name} has no active capital to infiltrate.";
            }

            if (Main.Settings != null && !Main.Settings.EnableEspionage)
            {
                Main.Settings.EnableEspionage = true;
            }

            Actor spy = FindCandidateSpy(world, pendingOrigin, allowExtendedFallback: true);
            if (spy == null)
            {
                if (GetKingdomSpecies(pendingOrigin) == "warden")
                {
                    return "Sentinel Wardens follow the Sleeping Giant code and refuse covert infiltration.";
                }
                return $"No eligible citizen available in {pendingOrigin.name} for covert ops.";
            }

            SpyMission m = LaunchSpyMission(pendingOrigin, clickedKingdom, spy, showToast: false, isAutoDispatch: false);
            if (m == null)
            {
                return $"Failed to dispatch covert agent from {pendingOrigin.name} toward {clickedKingdom.name}.";
            }

            return $"Spy Dispatched: {m.SpyName} ({m.OriginName}) is traveling to infiltrate {m.TargetName} ({m.TravelDistanceLeft} tiles)!";
        }

        public static string DispatchSpyNow()
        {
            EnsureAssetsRegistered();
            var world = MapBox.instance;
            if (world?.kingdoms == null || world.units == null || world.wars == null)
            {
                return "Load a world first to dispatch a covert spy.";
            }

            var kingdoms = new List<Kingdom>(world.kingdoms);
            var civs = new List<Kingdom>();
            for (int i = 0; i < kingdoms.Count; i++)
            {
                Kingdom k = kingdoms[i];
                if (k != null && k.isAlive() && k.isCiv() && k.getPopulationPeople() >= 3)
                {
                    civs.Add(k);
                }
            }

            if (civs.Count < 2)
            {
                return "Need at least 2 active kingdoms to dispatch a spy.";
            }

            Kingdom bestOrigin = null;
            Kingdom bestTarget = null;
            float bestScore = float.MinValue;

            // Pass 1: Prefer a hostile/rival weaker-vs-stronger non-allied pair
            for (int i = 0; i < civs.Count; i++)
            {
                Kingdom origin = civs[i];
                if (IsPacifistOrigin(origin)) continue;
                int oPop = origin.getPopulationPeople();
                int oWar = origin.countTotalWarriors();

                for (int j = 0; j < civs.Count; j++)
                {
                    if (i == j) continue;
                    Kingdom target = civs[j];
                    if (GetTargetCapitalTile(target) == null) continue;
                    if (AreAllied(origin, target)) continue;
                    if (world.wars.isInWarWith(origin, target)) continue;
                    if (HasMissionPair(origin.id, target.id)) continue;

                    int op = GetOpinionScore(world, origin, target);
                    int tPop = target.getPopulationPeople();
                    int tWar = target.countTotalWarriors();
                    bool rival = op <= 10;
                    bool stronger = tWar >= oWar * 0.95f || tPop > oPop * 1.05f;
                    if (!rival || !stronger) continue;

                    float sc = (-op) + (tPop - oPop) * 0.3f + GetOriginEspionageAffinity(origin) + UnityEngine.Random.Range(0f, 15f);
                    if (sc > bestScore)
                    {
                        bestScore = sc;
                        bestOrigin = origin;
                        bestTarget = target;
                    }
                }
            }

            // Pass 2: Fallback to any two non-allied civ kingdoms (preferring non-pacifist origin & larger target)
            if (bestOrigin == null || bestTarget == null)
            {
                civs.Sort((a, b) => b.getPopulationPeople().CompareTo(a.getPopulationPeople()));
                for (int i = 0; i < civs.Count && bestOrigin == null; i++)
                {
                    Kingdom target = civs[i];
                    if (GetTargetCapitalTile(target) == null) continue;
                    for (int j = civs.Count - 1; j >= 0; j--)
                    {
                        if (i == j) continue;
                        Kingdom origin = civs[j];
                        if (AreAllied(origin, target)) continue;
                        if (HasMissionPair(origin.id, target.id)) continue;
                        if (FindCandidateSpy(world, origin) == null) continue;

                        bestOrigin = origin;
                        bestTarget = target;
                        break;
                    }
                }
            }

            if (bestOrigin == null || bestTarget == null)
            {
                return "No eligible non-allied rival kingdoms found for espionage.";
            }

            Actor spy = FindCandidateSpy(world, bestOrigin);
            if (spy == null)
            {
                return $"No eligible adult citizen available in {bestOrigin.name} for covert ops.";
            }

            SpyMission m = LaunchSpyMission(bestOrigin, bestTarget, spy, showToast: false, isAutoDispatch: false);
            if (m == null)
            {
                return "Failed to dispatch covert spy.";
            }

            return $"Spy Dispatched: {m.SpyName} ({m.OriginName}) is traveling toward {m.TargetName} ({m.TravelDistanceLeft} tiles)!";
        }

        private static SpyMission LaunchSpyMission(Kingdom origin, Kingdom target, Actor spy, bool showToast, bool isAutoDispatch = false)
        {
            if (origin == null || !origin.isAlive() || target == null || !target.isAlive() || spy == null || !spy.isAlive())
                return null;

            City targetCapital = (target.capital != null && target.capital.isAlive()) ? target.capital : FirstAliveCity(target);
            WorldTile capTile = targetCapital?.getTile();
            if (targetCapital == null || !targetCapital.isAlive() || capTile == null || spy.current_tile == null)
                return null;

            EnsureAssetsRegistered();

            float dist;
            try
            {
                if (!spy.isAlive() || !targetCapital.isAlive() || !target.isAlive())
                    return null;

                if (AssetManager.traits != null)
                {
                    if (AssetManager.traits.has(TraitSpyId) && !spy.hasTrait(TraitSpyId)) spy.addTrait(TraitSpyId, false);
                    if (AssetManager.traits.has("deceitful") && !spy.hasTrait("deceitful")) spy.addTrait("deceitful", false);
                    if (AssetManager.traits.has("agile") && !spy.hasTrait("agile")) spy.addTrait("agile", false);
                    if (AssetManager.traits.has("fast") && !spy.hasTrait("fast")) spy.addTrait("fast", false);
                }
                spy.setStatsDirty();
                spy.restoreHealthPercent(1f);
                spy.setMaxHealth();
                spy.setMaxStamina();

                float dx = capTile.x - spy.current_tile.x;
                float dy = capTile.y - spy.current_tile.y;
                dist = Mathf.Sqrt(dx * dx + dy * dy);

                // Leave the spy at their actual origin tile and command physical travel to the target capital
                CancelAllBehMethod?.Invoke(spy, null);
                spy.goTo(capTile, pPathOnWater: true, pWalkOnBlocks: true);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GrandWarfare] Spy dispatch failed: {e.Message}");
                return null;
            }

            string spyName = SafeActorName(spy);
            var mission = new SpyMission
            {
                SpyActorId = spy.id,
                SpyName = spyName,
                OriginKingdomId = origin.id,
                OriginName = origin.name ?? "Unknown",
                TargetKingdomId = target.id,
                TargetName = target.name ?? "Unknown",
                Stage = SpyStage.Traveling,
                Influence = 10f,
                Cover = 100f,
                ConsolidationTimer = 0f,
                TravelDistanceLeft = Mathf.RoundToInt(dist),
                LastTravelDist = dist,
                StalledTravelTicks = 0
            };

            ActiveMissions.Add(mission);
            SpiesDispatched++;

            if (isAutoDispatch)
            {
                _originCooldowns[origin.id] = _simTime + AutoDispatchCooldownSeconds;
            }

            string msg = $"{mission.OriginName} dispatched covert agent {mission.SpyName} toward {mission.TargetName} ({mission.TravelDistanceLeft} tiles).";
            RecordEvent(msg, showToast);
            return mission;
        }

        private static void TickTravelingMissions(MapBox world)
        {
            if (ActiveMissions.Count == 0 || world?.units == null || world.kingdoms == null) return;

            var snapshot = new List<SpyMission>(ActiveMissions);
            var toRemove = new List<SpyMission>();

            for (int i = 0; i < snapshot.Count; i++)
            {
                SpyMission m = snapshot[i];
                if (m.Stage != SpyStage.Traveling) continue;

                Actor spy = world.units.get(m.SpyActorId);
                Kingdom origin = world.kingdoms.get(m.OriginKingdomId);
                Kingdom target = world.kingdoms.get(m.TargetKingdomId);

                if (spy == null || !spy.isAlive() || spy.current_tile == null)
                {
                    RecordEvent($"Agent {m.SpyName} ({m.OriginName}) perished en route to {m.TargetName}.", showToast: false);
                    toRemove.Add(m);
                    continue;
                }

                if (origin == null || !origin.isAlive() || !origin.isCiv() ||
                    target == null || !target.isAlive() || !target.isCiv())
                {
                    toRemove.Add(m);
                    continue;
                }

                City targetCap = (target.capital != null && target.capital.isAlive()) ? target.capital : FirstAliveCity(target);
                WorldTile dest = targetCap?.getTile();
                if (targetCap == null || !targetCap.isAlive() || dest == null)
                {
                    toRemove.Add(m);
                    continue;
                }

                m.SpyName = SafeActorName(spy);
                m.OriginName = origin.name ?? m.OriginName;
                m.TargetName = target.name ?? m.TargetName;

                float dx = dest.x - spy.current_tile.x;
                float dy = dest.y - spy.current_tile.y;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                m.TravelDistanceLeft = Mathf.RoundToInt(dist);

                if (dist <= 10f)
                {
                    try
                    {
                        CancelAllBehMethod?.Invoke(spy, null);
                        if (spy.city != null && spy.city.leader == spy)
                        {
                            spy.city.removeLeader();
                        }
                        spy.joinCity(targetCap);
                        spy.setMetasFromCity(targetCap);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[GrandWarfare] Spy arrival infiltration failed: {e.Message}");
                    }

                    if (!spy.isAlive() || spy.kingdom != target)
                    {
                        toRemove.Add(m);
                        continue;
                    }

                    m.Stage = SpyStage.Infiltrating;
                    m.TravelDistanceLeft = 0;
                    RecordEvent($"{origin.name}'s spy {m.SpyName} arrived in {target.name} and infiltrated {targetCap.name}!", showToast: true);
                    continue;
                }

                // Still en route: keep issuing goTo and step smoothly across oceans/obstacles if pathfinding stalls
                bool progressed = (m.LastTravelDist - dist) >= 1.5f;
                if (progressed)
                {
                    m.StalledTravelTicks = 0;
                }
                else
                {
                    m.StalledTravelTicks++;
                }
                m.LastTravelDist = dist;

                bool separatedByOcean = spy.current_tile.region?.island != dest.region?.island;
                if ((separatedByOcean && m.StalledTravelTicks > 2) || m.StalledTravelTicks > 3)
                {
                    float stepLen = Mathf.Min(UnityEngine.Random.Range(4, 7), Mathf.Max(1f, dist - 4f));
                    WorldTile stepTile = FindSafeTravelStepTile(world, spy.current_tile, dx / dist, dy / dist, stepLen);
                    if (stepTile != null && stepTile != spy.current_tile)
                    {
                        try
                        {
                            CancelAllBehMethod?.Invoke(spy, null);
                            SetCurrentTilePositionMethod?.Invoke(spy, new object[] { stepTile });
                        }
                        catch { }

                        if (spy.current_tile != null)
                        {
                            float ndx = dest.x - spy.current_tile.x;
                            float ndy = dest.y - spy.current_tile.y;
                            float newDist = Mathf.Sqrt(ndx * ndx + ndy * ndy);
                            m.TravelDistanceLeft = Mathf.RoundToInt(newDist);
                            m.LastTravelDist = newDist;
                        }
                    }
                }

                try
                {
                    spy.goTo(dest, pPathOnWater: true, pWalkOnBlocks: true);
                }
                catch { }
            }

            if (toRemove.Count > 0)
            {
                for (int i = 0; i < toRemove.Count; i++)
                {
                    ActiveMissions.Remove(toRemove[i]);
                }
            }
        }

        private static WorldTile FindSafeTravelStepTile(MapBox world, WorldTile current, float dirX, float dirY, float stepLen)
        {
            if (world == null || current == null) return null;
            int tx = Mathf.Clamp(Mathf.RoundToInt(current.x + dirX * stepLen), 1, MapBox.width - 2);
            int ty = Mathf.Clamp(Mathf.RoundToInt(current.y + dirY * stepLen), 1, MapBox.height - 2);
            WorldTile direct = world.GetTile(tx, ty);
            if (direct != null && (direct.Type == null || !direct.Type.lava))
            {
                return direct;
            }

            for (int oy = -1; oy <= 1; oy++)
            {
                for (int ox = -1; ox <= 1; ox++)
                {
                    WorldTile cand = world.GetTile(tx + ox, ty + oy);
                    if (cand != null && (cand.Type == null || !cand.Type.lava))
                    {
                        return cand;
                    }
                }
            }
            return direct;
        }

        private static Kingdom FindKingdomAtOrNearTile(MapBox world, WorldTile tile, float maxDistTiles)
        {
            if (tile?.zone?.city != null && tile.zone.city.isAlive())
            {
                Kingdom directKingdom = GetCityKingdom(tile.zone.city);
                if (directKingdom != null && directKingdom.isAlive() && directKingdom.isCiv())
                {
                    return directKingdom;
                }
            }

            float bestDistSq = maxDistTiles * maxDistTiles;
            Kingdom bestKingdom = null;

            if (world?.cities != null)
            {
                var cities = new List<City>(world.cities);
                for (int i = 0; i < cities.Count; i++)
                {
                    City c = cities[i];
                    if (c == null || !c.isAlive()) continue;
                    Kingdom k = GetCityKingdom(c);
                    if (k == null || !k.isAlive() || !k.isCiv()) continue;
                    WorldTile cTile = c.getTile();
                    if (cTile == null) continue;

                    float dx = cTile.x - tile.x;
                    float dy = cTile.y - tile.y;
                    float dSq = dx * dx + dy * dy;
                    if (dSq <= bestDistSq)
                    {
                        bestDistSq = dSq;
                        bestKingdom = k;
                    }
                }
            }

            if (world?.units?.units_only_alive != null)
            {
                var alive = new List<Actor>(world.units.units_only_alive);
                for (int i = 0; i < alive.Count; i++)
                {
                    Actor u = alive[i];
                    if (u == null || !u.isAlive() || u.current_tile == null) continue;
                    if (u.kingdom == null || !u.kingdom.isAlive() || !u.kingdom.isCiv()) continue;

                    float dx = u.current_tile.x - tile.x;
                    float dy = u.current_tile.y - tile.y;
                    float dSq = dx * dx + dy * dy;
                    if (dSq <= bestDistSq)
                    {
                        bestDistSq = dSq;
                        bestKingdom = u.kingdom;
                    }
                }
            }

            return bestKingdom;
        }

        private static Kingdom GetCityKingdom(City c)
        {
            if (c == null || CityKingdomField == null) return null;
            try { return CityKingdomField.GetValue(c) as Kingdom; }
            catch { return null; }
        }

        private static void TickActiveMissions(MapBox world)
        {
            if (ActiveMissions.Count == 0 || world?.units == null || world.kingdoms == null) return;

            var snapshot = new List<SpyMission>(ActiveMissions);
            var toRemove = new List<SpyMission>();

            for (int i = 0; i < snapshot.Count; i++)
            {
                SpyMission m = snapshot[i];
                if (m.Stage == SpyStage.Traveling) continue;

                Actor spy = world.units.get(m.SpyActorId);
                Kingdom origin = world.kingdoms.get(m.OriginKingdomId);
                Kingdom target = world.kingdoms.get(m.TargetKingdomId);

                if (spy == null || !spy.isAlive())
                {
                    RecordEvent($"Agent {m.SpyName} ({m.OriginName}) died in {m.TargetName}; cell closed.", showToast: false);
                    toRemove.Add(m);
                    continue;
                }

                if (origin == null || !origin.isAlive() || !origin.isCiv() ||
                    target == null || !target.isAlive() || !target.isCiv())
                {
                    toRemove.Add(m);
                    continue;
                }

                if (spy.kingdom != target)
                {
                    toRemove.Add(m);
                    continue;
                }

                m.SpyName = SafeActorName(spy);
                m.OriginName = origin.name ?? m.OriginName;
                m.TargetName = target.name ?? m.TargetName;

                float spySkill = ComputeSpySkill(spy, origin);
                float counterIntel = ComputeCounterIntel(target);
                float counterRatio = counterIntel / Mathf.Max(1f, counterIntel + spySkill);
                float spyRatio = 1f - counterRatio;

                // Counter-Intelligence detection roll (escalates at higher infiltration stages)
                float stageRisk;
                switch (m.Stage)
                {
                    case SpyStage.Infiltrating: stageRisk = 0.09f; break;
                    case SpyStage.CourtInsider: stageRisk = 0.15f; break;
                    case SpyStage.CityLeader: stageRisk = 0.22f; break;
                    default: stageRisk = 0.25f; break;
                }

                float coverPenalty = 1f + (100f - m.Cover) * 0.012f;
                float detectChance = Mathf.Clamp(counterRatio * stageRisk * coverPenalty, 0.015f, 0.70f);
                m.Cover = Mathf.Clamp(m.Cover - counterRatio * 5.5f, 5f, 100f);

                if (UnityEngine.Random.value < detectChance)
                {
                    HandleSpyExposed(world, m, spy, origin, target);
                    toRemove.Add(m);
                    continue;
                }

                // Undetected: advance mission stage
                bool chaotic = Main.Settings != null && Main.Settings.ChaoticEspionageMode;
                float gain = UnityEngine.Random.Range(10f, 22f) * (0.75f + spyRatio * 0.65f);
                if (chaotic) gain *= 1.35f;

                switch (m.Stage)
                {
                    case SpyStage.Infiltrating:
                        m.Influence = Mathf.Min(100f, m.Influence + gain);
                        if (m.Influence >= 45f)
                        {
                            m.Stage = SpyStage.CourtInsider;
                            m.Cover = Mathf.Min(100f, m.Cover + 10f);
                            RecordEvent($"Agent {m.SpyName} ({m.OriginName}) gained court insider status in {m.TargetName}.", showToast: false);
                        }
                        break;

                    case SpyStage.CourtInsider:
                        m.Influence = Mathf.Min(100f, m.Influence + gain);
                        if (m.Influence >= 80f)
                        {
                            City targetCapital = (target.capital != null && target.capital.isAlive()) ? target.capital : FirstAliveCity(target);
                            if (targetCapital != null && targetCapital.isAlive() && target.isAlive() && spy.isAlive())
                            {
                                try
                                {
                                    if (spy.city != targetCapital)
                                    {
                                        spy.joinCity(targetCapital);
                                        spy.setMetasFromCity(targetCapital);
                                    }
                                    if (spy.isAlive() && targetCapital.isAlive())
                                    {
                                        targetCapital.setLeader(spy, true);
                                        m.Stage = SpyStage.CityLeader;
                                        RecordEvent($"Mole {m.SpyName} ({m.OriginName}) rose to leader of {targetCapital.name} in {m.TargetName}!", showToast: true);
                                    }
                                }
                                catch (Exception e)
                                {
                                    Debug.LogWarning($"[GrandWarfare] Spy setLeader failed: {e.Message}");
                                }
                            }
                        }
                        break;

                    case SpyStage.CityLeader:
                        m.Influence = Mathf.Min(100f, m.Influence + gain * 0.85f);

                        if (target.king == null || !target.king.isAlive())
                        {
                            try
                            {
                                if (target.isAlive() && spy.isAlive())
                                {
                                    target.setKing(spy, false);
                                    if (target.king == spy)
                                    {
                                        m.Stage = SpyStage.UsurpedThrone;
                                        m.ConsolidationTimer = 0f;
                                        CoupsSucceeded++;
                                        RecordEvent($"Throne Seized! {m.OriginName}'s mole {m.SpyName} claimed the vacant throne of {m.TargetName}!", showToast: true);
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                Debug.LogWarning($"[GrandWarfare] Vacant throne claim failed: {e.Message}");
                            }
                        }
                        else if (target.king == spy)
                        {
                            m.Stage = SpyStage.UsurpedThrone;
                            m.ConsolidationTimer = 0f;
                            CoupsSucceeded++;
                            RecordEvent($"Throne Seized! {m.OriginName}'s mole {m.SpyName} ascended to the throne of {m.TargetName}!", showToast: true);
                        }
                        else if (m.Influence >= 100f)
                        {
                            float coupChance = Mathf.Clamp(0.32f + (spyRatio - 0.5f) * 0.50f, 0.25f, 0.55f);
                            if (UnityEngine.Random.value < coupChance)
                            {
                                try
                                {
                                    Actor oldKing = target.king;
                                    if (oldKing != null && oldKing.isAlive() && oldKing != spy)
                                    {
                                        oldKing.dieSimpleNone();
                                    }
                                    if (target.isAlive() && spy.isAlive())
                                    {
                                        target.setKing(spy, false);
                                        if (target.king == spy)
                                        {
                                            m.Stage = SpyStage.UsurpedThrone;
                                            m.ConsolidationTimer = 0f;
                                            CoupsSucceeded++;
                                            RecordEvent($"Palace Coup! {m.OriginName}'s mole {m.SpyName} seized the throne of {m.TargetName}!", showToast: true);
                                        }
                                        else
                                        {
                                            m.Influence = 72f;
                                            m.Cover = Mathf.Max(5f, m.Cover - 30f);
                                            RecordEvent($"Throne Blocked: {target.name}'s succession laws denied mole {m.SpyName} ({m.OriginName}) the crown.", showToast: false);
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    Debug.LogWarning($"[GrandWarfare] Palace coup failed: {e.Message}");
                                }
                            }
                            else
                            {
                                // Coup failed: sharp cover loss + immediate exposure roll
                                m.Cover = Mathf.Max(5f, m.Cover - 45f);
                                m.Influence = 72f;
                                float exposeAfterFail = Mathf.Clamp(counterRatio * 1.05f, 0.38f, 0.85f);
                                if (UnityEngine.Random.value < exposeAfterFail)
                                {
                                    HandleSpyExposed(world, m, spy, origin, target);
                                    toRemove.Add(m);
                                }
                                else
                                {
                                    RecordEvent($"Botched coup in {m.TargetName}: mole {m.SpyName} ({m.OriginName}) lost cover but survived.", showToast: false);
                                }
                            }
                        }
                        break;

                    case SpyStage.UsurpedThrone:
                        if (target.king != spy)
                        {
                            if ((target.king == null || !target.king.isAlive()) && target.isAlive() && spy.isAlive())
                            {
                                try { target.setKing(spy, false); } catch { }
                            }
                        }

                        if (target.king != spy)
                        {
                            m.Stage = SpyStage.CityLeader;
                            m.Influence = 75f;
                            m.Cover = Mathf.Max(10f, m.Cover - 25f);
                            break;
                        }

                        m.ConsolidationTimer += MissionTickInterval;
                        float consolidationWindow = chaotic ? 15f : 25f;
                        if (m.ConsolidationTimer >= consolidationWindow)
                        {
                            ExecutePeacefulUnion(m, spy, origin, target);
                            toRemove.Add(m);
                        }
                        break;
                }
            }

            if (toRemove.Count > 0)
            {
                for (int i = 0; i < toRemove.Count; i++)
                {
                    ActiveMissions.Remove(toRemove[i]);
                }
            }
        }

        private static void HandleSpyExposed(MapBox world, SpyMission m, Actor spy, Kingdom origin, Kingdom target)
        {
            SpiesCaught++;

            try
            {
                if (spy != null && spy.isAlive())
                {
                    spy.dieSimpleNone();
                }
            }
            catch { }

            // Reward the defending king (or capital leader) with Spymaster
            try
            {
                Actor spymaster = (target.king != null && target.king.isAlive())
                    ? target.king
                    : target.capital?.leader;
                if (spymaster != null && spymaster.isAlive() && AssetManager.traits != null && AssetManager.traits.has(TraitCounterIntelId))
                {
                    spymaster.addTrait(TraitCounterIntelId, false);
                    spymaster.setStatsDirty();
                }
            }
            catch { }

            // Record diplomatic espionage scandal grudge (-65 opinion for 600s)
            _scandalGrudges[(target.id, origin.id)] = _simTime + ScandalDurationSeconds;

            // Retaliation war roll: 100% if victim is a Warden kingdom, ~55% otherwise
            bool targetIsWarden = IsWardenKingdom(target);
            float retaliationChance = targetIsWarden ? 1.0f : 0.55f;

            if (world.wars != null && world.diplomacy != null &&
                !world.wars.isInWarWith(target, origin) &&
                UnityEngine.Random.value < retaliationChance)
            {
                try
                {
                    if (AreAllied(target, origin))
                    {
                        target.getAlliance()?.leave(target, true);
                    }

                    // For Warden kingdoms, start war as (origin -> target) so ApexFactions triggers
                    // the Sleeping Giant provocation strike natively.
                    Kingdom attacker = targetIsWarden ? origin : target;
                    Kingdom defender = targetIsWarden ? target : origin;

                    War war = null;
                    if (StartWarMethod != null)
                    {
                        war = StartWarMethod.Invoke(world.diplomacy,
                            new object[] { attacker, defender, WarTypeLibrary.normal, true }) as War;
                    }

                    if (war != null)
                    {
                        EspionageWarCauses[war.id] = $"Caught Spy ({m.SpyName} of {origin.name})";
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[GrandWarfare] Retaliation war start failed: {e.Message}");
                }
            }

            RecordEvent($"Counter-Intel: {target.name} exposed and executed spy {m.SpyName} from {origin.name}!", showToast: true);
        }

        private static void ExecutePeacefulUnion(SpyMission m, Actor spy, Kingdom origin, Kingdom target)
        {
            if (origin == null || !origin.isAlive() || target == null || !target.isAlive()) return;
            if (spy == null || !spy.isAlive() || target.king != spy)
            {
                RecordEvent($"Union aborted: {m.SpyName} no longer holds the throne of {target.name}.", showToast: false);
                return;
            }

            // Cleanly end or exit active wars involving the target kingdom before transferring its cities,
            // preventing War.update from dereferencing a dissolved kingdom.
            var world = MapBox.instance;
            if (world?.wars != null)
            {
                try
                {
                    var activeWarsRaw = world.wars.getActiveWars();
                    if (activeWarsRaw != null)
                    {
                        var activeWars = new List<War>(activeWarsRaw);
                        for (int i = 0; i < activeWars.Count; i++)
                        {
                            War war = activeWars[i];
                            if (war == null || !war.isAlive() || war.hasEnded()) continue;
                            if (!war.hasKingdom(target)) continue;

                            bool isMainOrSoleMember =
                                war.getMainAttacker() == target ||
                                war.getMainDefender() == target ||
                                (war.isAttacker(target) && war.countAttackers() <= 1) ||
                                (war.isDefender(target) && war.countDefenders() <= 1);

                            if (isMainOrSoleMember)
                            {
                                world.wars.endWar(war, WarWinner.Merged);
                            }
                            else
                            {
                                war.leaveWar(target);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[GrandWarfare] Pre-union war cleanup failed for {target.name}: {e.Message}");
                }
            }

            var citiesSnapshot = target.cities != null ? new List<City>(target.cities) : new List<City>();
            int transferred = 0;

            for (int i = 0; i < citiesSnapshot.Count; i++)
            {
                City city = citiesSnapshot[i];
                if (city == null || !city.isAlive() || !origin.isAlive()) continue;
                try
                {
                    city.joinAnotherKingdom(origin, false, false);
                    transferred++;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[GrandWarfare] Peaceful city transfer failed for {city.name}: {e.Message}");
                }
            }

            if (transferred > 0)
            {
                try
                {
                    if (spy != null && spy.isAlive() && spy.city != null && spy.city.isAlive() && spy.kingdom != origin)
                    {
                        spy.setMetasFromCity(spy.city);
                    }
                }
                catch { }

                PeacefulTakeovers++;
                RecordEvent($"Peaceful Takeover! Mole King {m.SpyName} united {target.name} into {origin.name}!", showToast: true);
            }
            else
            {
                RecordEvent($"Union stalled: {target.name} had no cities left to join {origin.name}.", showToast: false);
            }
        }

        private static Actor FindCandidateSpy(MapBox world, Kingdom origin, bool allowExtendedFallback = false)
        {
            var aliveRaw = world.units?.units_only_alive;
            if (aliveRaw == null || aliveRaw.Count == 0 || origin == null) return null;

            var alive = new List<Actor>(aliveRaw);
            Actor bestCivilian = null;
            float bestCivScore = float.MinValue;
            Actor bestFallback = null;
            float bestFallbackScore = float.MinValue;
            Actor bestExtended = null;
            float bestExtendedScore = float.MinValue;

            for (int i = 0; i < alive.Count; i++)
            {
                Actor u = alive[i];
                if (u == null || !u.isAlive() || u.kingdom != origin || u.current_tile == null) continue;
                if (u.asset == null || u.asset.is_boat || u.asset.id == "warden" || NavalCombatSystem.IsInsideBoat(u)) continue;
                if (u == origin.king) continue;
                if (IsActiveSpy(u.id)) continue;

                float score = ComputeSpySkill(u, origin) + UnityEngine.Random.Range(0f, 12f);

                if (!u.isAdult() || (u.city != null && u.city.leader == u))
                {
                    if (allowExtendedFallback && score > bestExtendedScore)
                    {
                        bestExtendedScore = score;
                        bestExtended = u;
                    }
                    continue;
                }

                if (u.army == null)
                {
                    if (score > bestCivScore)
                    {
                        bestCivScore = score;
                        bestCivilian = u;
                    }
                }
                else
                {
                    if (score > bestFallbackScore)
                    {
                        bestFallbackScore = score;
                        bestFallback = u;
                    }
                }
            }

            return bestCivilian ?? bestFallback ?? (allowExtendedFallback ? bestExtended : null);
        }

        private static void CleanupStaleState(MapBox world)
        {
            if (_scandalGrudges.Count > 0)
            {
                var keys = new List<(long, long)>(_scandalGrudges.Keys);
                for (int i = 0; i < keys.Count; i++)
                {
                    if (_simTime >= _scandalGrudges[keys[i]])
                        _scandalGrudges.Remove(keys[i]);
                }
            }

            if (_originCooldowns.Count > 0)
            {
                var keys = new List<long>(_originCooldowns.Keys);
                for (int i = 0; i < keys.Count; i++)
                {
                    if (_simTime >= _originCooldowns[keys[i]])
                        _originCooldowns.Remove(keys[i]);
                }
            }

            if (world?.wars != null && EspionageWarCauses.Count > 0)
            {
                var warIds = new List<long>(EspionageWarCauses.Keys);
                for (int i = 0; i < warIds.Count; i++)
                {
                    War w = world.wars.get(warIds[i]);
                    if (w == null || !w.isAlive())
                    {
                        EspionageWarCauses.Remove(warIds[i]);
                    }
                }
            }
        }

        private static bool IsPacifistOrigin(Kingdom k)
        {
            if (k == null) return true;
            if (IsWardenKingdom(k)) return true;
            string sp = GetKingdomSpecies(k);
            return sp == "warden" || sp == "penguin" || sp == "capybara" || sp == "unicorn";
        }

        private static bool IsWardenKingdom(Kingdom k)
        {
            if (k == null) return false;
            if (GetKingdomSpecies(k) == "warden") return true;

            if (!_wardenCheckResolved && Time.unscaledTime >= _wardenCheckRetryAt)
            {
                Type t = AccessTools.TypeByName("ApexFactions.WardenSystem");
                if (t != null)
                {
                    _wardenCheckMethod = AccessTools.Method(t, "IsWardenKingdom", new[] { typeof(Kingdom) });
                    _wardenCheckResolved = true;
                }
                else
                {
                    _wardenCheckRetryAt = Time.unscaledTime + 5f;
                }
            }

            if (_wardenCheckMethod != null)
            {
                try
                {
                    if (_wardenCheckMethod.Invoke(null, new object[] { k }) is bool b && b)
                        return true;
                }
                catch { }
            }
            return false;
        }

        private static float GetOriginEspionageAffinity(Kingdom origin)
        {
            string sp = GetKingdomSpecies(origin);
            switch (sp)
            {
                case "snake": return 22f;
                case "fox": return 18f;
                case "raccoon": return 18f;
                case "rat": return 12f;
                case "cat": return 10f;
                case "elf": return 8f;
                default: return 0f;
            }
        }

        private static string GetKingdomSpecies(Kingdom k)
        {
            if (k == null) return "";
            string raw = k.data?.original_actor_asset;
            if (string.IsNullOrEmpty(raw))
            {
                try
                {
                    Subspecies sub = k.getMainSubspecies();
                    if (sub != null) raw = SpeciesIdGetter?.Invoke(sub, null) as string;
                }
                catch { }
            }
            if (string.IsNullOrEmpty(raw)) return "";
            string norm = raw.Trim().ToLowerInvariant();
            if (norm.StartsWith("civ_")) norm = norm.Substring(4);
            return norm;
        }

        private static float GetActorStat(Actor a, string statId)
        {
            if (a == null) return 0f;
            try
            {
                BaseStats st = StatsRef != null ? StatsRef(a) : StatsField?.GetValue(a) as BaseStats;
                return st != null ? st[statId] : 0f;
            }
            catch
            {
                return 0f;
            }
        }

        private static bool HasTraitSafe(Actor a, string traitId)
        {
            if (a == null || string.IsNullOrEmpty(traitId)) return false;
            try
            {
                return a.hasTrait(traitId);
            }
            catch
            {
                return false;
            }
        }

        private static int GetOpinionScore(MapBox world, Kingdom a, Kingdom b)
        {
            try
            {
                KingdomOpinion op = world?.diplomacy?.getOpinion(a, b);
                return op != null ? op.total : 0;
            }
            catch
            {
                return 0;
            }
        }

        private static bool AreAllied(Kingdom a, Kingdom b)
        {
            if (a == null || b == null) return false;
            try
            {
                if (!a.hasAlliance()) return false;
                Alliance al = a.getAlliance();
                return al != null && al.hasKingdom(b);
            }
            catch
            {
                return false;
            }
        }

        private static City FirstAliveCity(Kingdom k)
        {
            if (k?.cities == null) return null;
            var cities = new List<City>(k.cities);
            for (int i = 0; i < cities.Count; i++)
            {
                City c = cities[i];
                if (c != null && c.isAlive() && c.getTile() != null) return c;
            }
            return null;
        }

        private static WorldTile GetTargetCapitalTile(Kingdom target)
        {
            if (target == null || !target.isAlive()) return null;
            City cap = (target.capital != null && target.capital.isAlive()) ? target.capital : FirstAliveCity(target);
            if (cap == null || !cap.isAlive()) return null;
            return cap.getTile();
        }

        private static int CountActiveFromOrigin(long originId)
        {
            int count = 0;
            for (int i = 0; i < ActiveMissions.Count; i++)
            {
                if (ActiveMissions[i].OriginKingdomId == originId) count++;
            }
            return count;
        }

        private static bool HasMissionPair(long originId, long targetId)
        {
            for (int i = 0; i < ActiveMissions.Count; i++)
            {
                if (ActiveMissions[i].OriginKingdomId == originId && ActiveMissions[i].TargetKingdomId == targetId)
                    return true;
            }
            return false;
        }

        private static bool IsActiveSpy(long actorId)
        {
            for (int i = 0; i < ActiveMissions.Count; i++)
            {
                if (ActiveMissions[i].SpyActorId == actorId) return true;
            }
            return false;
        }

        private static string SafeActorName(Actor a)
        {
            if (a == null) return "Agent";
            try
            {
                string n = a.getName();
                if (!string.IsNullOrEmpty(n)) return n;
            }
            catch { }
            return "Agent";
        }

        private static void RecordEvent(string msg, bool showToast)
        {
            if (string.IsNullOrEmpty(msg)) return;
            RecentEvents.Add(msg);
            if (RecentEvents.Count > MaxRecentEvents)
            {
                RecentEvents.RemoveAt(0);
            }
            Debug.Log($"[GrandWarfare][Intel] {msg}");
            if (showToast)
            {
                try { WorldTip.showNowTop(msg, false); } catch { }
            }
        }
    }
}
