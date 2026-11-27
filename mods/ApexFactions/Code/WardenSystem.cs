using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ApexFactions
{
    public class RetaliationTracker
    {
        public long WarId;
        public long WardenKingdomId;
        public long AggressorKingdomId;
        public string WardenName;
        public string AggressorName;
        public int StrikeNumber;
        public float EffectiveMercyPercent;
        public int BaselinePop;
        public int MercyTargetPop;
        public int CurrentAggressorPop;
    }

    [HarmonyPatch]
    public static class WardenSystem
    {
        public static readonly HashSet<long> WardenKingdomIds = new HashSet<long>();
        public static readonly Dictionary<long, RetaliationTracker> ActiveRetaliations = new Dictionary<long, RetaliationTracker>();
        // Three-strikes grudge memory: (wardenKingdomId, aggressorKingdomId) -> strike count
        public static readonly Dictionary<(long, long), int> AggressorStrikes = new Dictionary<(long, long), int>();

        private static readonly HashSet<long> _activeRetaliationWardenIds = new HashSet<long>();

        // Internal flag so our own Merciful Ceasefire call is never blocked by our endWar guard
        private static bool _allowingMercifulCeasefire = false;

        private static WorldTile _pendingWardenSpawnTile;
        private static Actor _pendingWardenLeader;
        private static float _pendingWardenTimer;

        public static bool IsWardenKingdom(Kingdom k)
        {
            if (k == null) return false;
            if (WardenKingdomIds.Contains(k.id)) return true;

            if (string.Equals(k.data?.original_actor_asset, WardenSpeciesRegistry.WardenAssetId, StringComparison.OrdinalIgnoreCase))
            {
                WardenKingdomIds.Add(k.id);
                return true;
            }
            return false;
        }

        public static bool HasActiveRetaliation(long kingdomId)
        {
            return _activeRetaliationWardenIds.Contains(kingdomId);
        }

        private static void RebuildActiveRetaliationWardenCache()
        {
            _activeRetaliationWardenIds.Clear();
            foreach (var tr in ActiveRetaliations.Values)
            {
                _activeRetaliationWardenIds.Add(tr.WardenKingdomId);
            }
        }

        public static string GetWarCauseAnnotation(long warId)
        {
            if (ActiveRetaliations.TryGetValue(warId, out var tr))
            {
                string mercyText = tr.EffectiveMercyPercent <= 0f
                    ? "No Mercy"
                    : $"{tr.EffectiveMercyPercent:0}% Mercy";
                return $"Warden Retaliation (Strike #{tr.StrikeNumber}, {mercyText})";
            }
            return "";
        }

        public static int CountAliveWardenKingdoms()
        {
            var world = MapBox.instance;
            if (world?.kingdoms == null) return WardenKingdomIds.Count;
            int count = 0;
            var kingdoms = new List<Kingdom>(world.kingdoms);
            for (int i = 0; i < kingdoms.Count; i++)
            {
                Kingdom k = kingdoms[i];
                if (k != null && k.isAlive() && k.isCiv() && IsWardenKingdom(k))
                    count++;
            }
            return count;
        }

        public static void AnointKingdomAsWardens(Kingdom k)
        {
            if (k == null || !k.isCiv()) return;
            WardenKingdomIds.Add(k.id);

            var aliveRaw = MapBox.instance?.units?.units_only_alive;
            if (aliveRaw != null && aliveRaw.Count > 0)
            {
                var alive = new List<Actor>(aliveRaw);
                for (int i = 0; i < alive.Count; i++)
                {
                    Actor u = alive[i];
                    if (u != null && u.isAlive() && u.kingdom == k)
                    {
                        ApplyWardenPeacetimeTraits(u);
                    }
                }
            }

            Debug.Log($"[ApexFactions] {k.name} anointed with the Warden Sleeping Giant Doctrine.");
        }

        public static void SpawnWardenColonyAtCamera()
        {
            WorldTile center = FindSafeLandNearCamera();
            SpawnWardenColonyAtTile(center);
        }

        public static int SpawnWardenColonyAtTile(WorldTile center, out string foundedKingdomName)
        {
            foundedKingdomName = null;
            var world = MapBox.instance;
            if (world?.units == null || center == null) return 0;

            bool registered = WardenSpeciesRegistry.EnsureRegistered();
            string unitAssetId = registered && AssetManager.actor_library.has(WardenSpeciesRegistry.WardenAssetId)
                ? WardenSpeciesRegistry.WardenAssetId
                : "human";

            const int colonySize = 10;
            var settlers = new List<Actor>(colonySize);
            for (int i = 0; i < colonySize; i++)
            {
                WorldTile spawnTile = center;
                if (i > 0)
                {
                    int ox = (i % 3) - 1;
                    int oy = ((i / 3) % 3) - 1;
                    WorldTile cand = world.GetTile(center.x + ox, center.y + oy);
                    if (cand != null && cand.Type != null && !cand.Type.liquid && cand.Type.ground)
                        spawnTile = cand;
                }

                Actor u = world.units.spawnNewUnit(unitAssetId, spawnTile);
                if (u == null) continue;
                ApplyWardenPeacetimeTraits(u);
                settlers.Add(u);
            }

            if (settlers.Count == 0) return 0;

            // Immediately found a brand-new Warden city & kingdom if placed on unclaimed buildable land
            Actor leader = settlers[0];
            try
            {
                if (leader.buildCityAndStartCivilization() && leader.city != null && leader.kingdom != null && leader.kingdom.isCiv())
                {
                    City newCity = leader.city;
                    Kingdom newKingdom = leader.kingdom;
                    WardenKingdomIds.Add(newKingdom.id);
                    for (int i = 1; i < settlers.Count; i++)
                    {
                        try
                        {
                            settlers[i].joinCity(newCity);
                            settlers[i].setMetasFromCity(newCity);
                        }
                        catch { }
                    }
                    AnointKingdomAsWardens(newKingdom);
                    foundedKingdomName = newKingdom.name;
                    Debug.Log($"[ApexFactions] Founded Warden Kingdom '{newKingdom.name}' at ({center.x},{center.y}) with {settlers.Count} Wardens.");
                    return settlers.Count;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ApexFactions] Immediate Warden city founding deferred: {e.Message}");
            }

            // Fallback: remember tile & leader so we auto-anoint their kingdom as soon as they claim land
            _pendingWardenSpawnTile = center;
            _pendingWardenLeader = leader;
            _pendingWardenTimer = 45f;
            Debug.Log($"[ApexFactions] Spawned {settlers.Count} Warden settlers at ({center.x},{center.y}).");
            return settlers.Count;
        }

        public static int SpawnWardenColonyAtTile(WorldTile center)
        {
            return SpawnWardenColonyAtTile(center, out _);
        }

        private static void ApplyWardenPeacetimeTraits(Actor u)
        {
            // Keep calibrated 1v30 Warden base stats untouched; only bestow peacetime mind traits
            u.addTrait("peaceful", false);
            u.addTrait("wise", false);
            u.addTrait("genius", false);
            u.setStatsDirty();
            u.restoreHealthPercent(1f);
        }

        private static void RallyWardenWarriors(Kingdom warden)
        {
            var aliveRaw = MapBox.instance?.units?.units_only_alive;
            if (aliveRaw == null || aliveRaw.Count == 0) return;

            var alive = new List<Actor>(aliveRaw);
            int rallied = 0;
            for (int i = 0; i < alive.Count; i++)
            {
                Actor u = alive[i];
                if (u == null || !u.isAlive() || u.kingdom != warden) continue;
                if (u.asset != null && u.asset.is_boat) continue;

                u.removeTrait("peaceful");
                u.addTrait("veteran", false);
                u.setStatsDirty();
                u.restoreHealthPercent(1f);
                rallied++;
            }
            Debug.Log($"[ApexFactions] Sleeping Giant awakened: rallied {rallied} Wardens of {warden.name}!");
        }

        private static void StandDownWardenWarriors(Kingdom warden)
        {
            var aliveRaw = MapBox.instance?.units?.units_only_alive;
            if (aliveRaw == null || aliveRaw.Count == 0) return;

            var alive = new List<Actor>(aliveRaw);
            for (int i = 0; i < alive.Count; i++)
            {
                Actor u = alive[i];
                if (u != null && u.isAlive() && u.kingdom == warden)
                {
                    if (u.asset != null && u.asset.is_boat) continue;
                    u.addTrait("peaceful", false);
                }
            }
        }

        public static void Tick(float dt)
        {
            WardenSpeciesRegistry.EnsureRegistered();

            var world = MapBox.instance;
            if (world == null || Main.Settings == null || !Main.Settings.EnableWardenDoctrine) return;

            List<Kingdom> kingdomsSnapshot = null;

            // Keep save-loaded Warden kingdoms synchronized in WardenKingdomIds
            if (world.kingdoms != null)
            {
                kingdomsSnapshot = new List<Kingdom>(world.kingdoms);
                for (int i = 0; i < kingdomsSnapshot.Count; i++)
                {
                    Kingdom k = kingdomsSnapshot[i];
                    if (k != null && k.isAlive() && k.isCiv())
                        IsWardenKingdom(k);
                }
            }

            // Auto-anoint freshly founded kingdom from SpawnWardenColonyAtTile
            if (_pendingWardenSpawnTile != null || _pendingWardenLeader != null)
            {
                _pendingWardenTimer -= dt;
                if (_pendingWardenLeader != null && _pendingWardenLeader.isAlive() && _pendingWardenLeader.kingdom != null && _pendingWardenLeader.kingdom.isCiv())
                {
                    if (!WardenKingdomIds.Contains(_pendingWardenLeader.kingdom.id))
                    {
                        AnointKingdomAsWardens(_pendingWardenLeader.kingdom);
                    }
                    _pendingWardenSpawnTile = null;
                    _pendingWardenLeader = null;
                }
                else if (_pendingWardenTimer <= 0f)
                {
                    _pendingWardenSpawnTile = null;
                    _pendingWardenLeader = null;
                }
                else if (_pendingWardenSpawnTile != null && kingdomsSnapshot != null)
                {
                    for (int i = 0; i < kingdomsSnapshot.Count; i++)
                    {
                        Kingdom k = kingdomsSnapshot[i];
                        if (k == null || !k.isCiv() || !k.isAlive() || IsWardenKingdom(k)) continue;
                        WorldTile cap = k.capital?.getTile();
                        if (cap != null)
                        {
                            float dx = cap.x - _pendingWardenSpawnTile.x;
                            float dy = cap.y - _pendingWardenSpawnTile.y;
                            if (dx * dx + dy * dy <= 40f * 40f)
                            {
                                AnointKingdomAsWardens(k);
                                _pendingWardenSpawnTile = null;
                                _pendingWardenLeader = null;
                                break;
                            }
                        }
                    }
                }
            }

            // Evaluate active Retaliation Wars against the Mercy Threshold (e.g. 15% survivors left)
            if (ActiveRetaliations.Count == 0 || world.wars == null || world.kingdoms == null) return;

            var finished = new List<long>();
            var retaliationSnapshot = new List<KeyValuePair<long, RetaliationTracker>>(ActiveRetaliations);
            for (int i = 0; i < retaliationSnapshot.Count; i++)
            {
                var kv = retaliationSnapshot[i];
                RetaliationTracker tr = kv.Value;
                War war = world.wars.get(tr.WarId);
                Kingdom aggressor = world.kingdoms.get(tr.AggressorKingdomId);
                Kingdom warden = world.kingdoms.get(tr.WardenKingdomId);

                if (war == null || !war.isAlive() || aggressor == null || !aggressor.isAlive() || warden == null || !warden.isAlive())
                {
                    if (warden != null && warden.isAlive()) StandDownWardenWarriors(warden);
                    finished.Add(kv.Key);
                    continue;
                }

                int curPop = aggressor.getPopulationPeople();
                tr.CurrentAggressorPop = curPop;

                if (tr.EffectiveMercyPercent > 0f && curPop <= tr.MercyTargetPop)
                {
                    Debug.Log(
                        $"[ApexFactions] Merciful Ceasefire (Strike #{tr.StrikeNumber}): {warden.name} reduced {aggressor.name} from " +
                        $"{tr.BaselinePop} to {curPop} (<= {tr.MercyTargetPop}). Spared survivors to tell the tale."
                    );
                    _allowingMercifulCeasefire = true;
                    try
                    {
                        world.wars.endWar(war, WarWinner.Defenders);
                    }
                    finally
                    {
                        _allowingMercifulCeasefire = false;
                    }
                    StandDownWardenWarriors(warden);
                    finished.Add(kv.Key);
                }
            }

            if (finished.Count > 0)
            {
                for (int i = 0; i < finished.Count; i++)
                    ActiveRetaliations.Remove(finished[i]);
                RebuildActiveRetaliationWardenCache();
            }
        }

        // 1. Wardens NEVER declare offensive wars
        [HarmonyPrefix]
        [HarmonyPatch(typeof(DiplomacyManager), "startWar")]
        private static bool StartWarPrefix(Kingdom pAttacker)
        {
            try
            {
                if (Main.Settings == null || !Main.Settings.EnableWardenDoctrine || pAttacker == null) return true;
                if (IsWardenKingdom(pAttacker))
                {
                    return false; // Peace doctrine: never initiate aggression
                }
            }
            catch { }
            return true;
        }

        // 2. If someone attacks a Warden kingdom, awaken the Sleeping Giant and lock baseline pop
        [HarmonyPostfix]
        [HarmonyPatch(typeof(DiplomacyManager), "startWar")]
        private static void StartWarPostfix(Kingdom pAttacker, Kingdom pDefender, War __result)
        {
            try
            {
                if (Main.Settings == null || !Main.Settings.EnableWardenDoctrine) return;
                if (__result == null || pAttacker == null || pDefender == null) return;
                if (!IsWardenKingdom(pDefender)) return;

                var strikeKey = (pDefender.id, pAttacker.id);
                AggressorStrikes.TryGetValue(strikeKey, out int prevStrikes);
                int strikeNum = prevStrikes + 1;
                AggressorStrikes[strikeKey] = strikeNum;

                // Three-strikes grudge escalation: Strike 1 = 15% mercy, Strike 2 = 5%, Strike 3+ = 0% (no mercy)
                float basePct = Main.Settings.MercyThresholdPercent;
                float effectivePct = strikeNum == 1 ? basePct : (strikeNum == 2 ? Mathf.Min(5f, basePct * 0.35f) : 0f);

                int baseline = Math.Max(1, pAttacker.getPopulationPeople());
                int mercyPop = effectivePct <= 0f
                    ? 0
                    : Math.Max(Main.Settings.MinSurvivorsFloor, Mathf.RoundToInt(baseline * (effectivePct / 100f)));

                ActiveRetaliations[__result.id] = new RetaliationTracker
                {
                    WarId = __result.id,
                    WardenKingdomId = pDefender.id,
                    AggressorKingdomId = pAttacker.id,
                    WardenName = pDefender.name,
                    AggressorName = pAttacker.name,
                    StrikeNumber = strikeNum,
                    EffectiveMercyPercent = effectivePct,
                    BaselinePop = baseline,
                    MercyTargetPop = mercyPop,
                    CurrentAggressorPop = baseline
                };
                RebuildActiveRetaliationWardenCache();

                Debug.Log($"[ApexFactions] {pAttacker.name} provoked {pDefender.name} (Strike #{strikeNum}, mercy={effectivePct:0}%)!");
                RallyWardenWarriors(pDefender);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ApexFactions] StartWarPostfix skipped: {e.Message}");
            }
        }

        // 3. Refuse premature peace treaties while aggressor is still above the mercy threshold
        [HarmonyPrefix]
        [HarmonyPatch(typeof(WarManager), nameof(WarManager.endWar))]
        private static bool EndWarPrefix(War pWar, WarWinner pWinner)
        {
            try
            {
                if (_allowingMercifulCeasefire || pWinner == WarWinner.Merged || pWar == null || Main.Settings == null || !Main.Settings.EnableWardenDoctrine)
                    return true;

                if (ActiveRetaliations.TryGetValue(pWar.id, out RetaliationTracker tr))
                {
                    if (pWar.getMainAttacker() == null || pWar.getMainDefender() == null)
                        return true;

                    var kingdoms = MapBox.instance?.kingdoms;
                    Kingdom aggressor = kingdoms?.get(tr.AggressorKingdomId);
                    Kingdom warden = kingdoms?.get(tr.WardenKingdomId);

                    if (aggressor == null || !aggressor.isAlive() || aggressor.countCities() <= 0 ||
                        warden == null || !warden.isAlive() || warden.countCities() <= 0)
                    {
                        return true;
                    }

                    if (aggressor.getPopulationPeople() > tr.MercyTargetPop)
                    {
                        return false; // Relentless retaliation until mercy target is reached
                    }
                }
            }
            catch { }
            return true;
        }

        private static readonly AccessTools.FieldRef<City, Kingdom> CityKingdomRef = InitCityKingdomRef();
        private static readonly AccessTools.FieldRef<Actor, ActorData> ActorDataRef = InitActorDataRef();

        private static AccessTools.FieldRef<City, Kingdom> InitCityKingdomRef()
        {
            try { return AccessTools.FieldRefAccess<City, Kingdom>("kingdom"); }
            catch { return null; }
        }

        private static AccessTools.FieldRef<Actor, ActorData> InitActorDataRef()
        {
            try { return AccessTools.FieldRefAccess<Actor, ActorData>("data"); }
            catch { return null; }
        }

        // 4. Warden Tradition Lock: Wardens never join non-Warden civ cities
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Actor), nameof(Actor.joinCity))]
        private static bool JoinCityPrefix(Actor __instance, City pCity)
        {
            try
            {
                if (__instance?.asset?.id == WardenSpeciesRegistry.WardenAssetId && pCity != null)
                {
                    Kingdom cityKingdom = CityKingdomRef != null ? CityKingdomRef(pCity) : null;
                    if (cityKingdom != null && cityKingdom.isCiv() && !IsWardenKingdom(cityKingdom))
                    {
                        return false;
                    }
                }
            }
            catch { }
            return true;
        }

        // 5. Warden Tradition Lock: Wardens never defect to non-Warden civ kingdoms
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Actor), "setKingdom")]
        private static bool SetKingdomPrefix(Actor __instance, Kingdom pKingdomToSet)
        {
            try
            {
                if (__instance?.asset?.id == WardenSpeciesRegistry.WardenAssetId &&
                    pKingdomToSet != null && pKingdomToSet.isCiv() &&
                    !IsWardenKingdom(pKingdomToSet))
                {
                    return false;
                }
            }
            catch { }
            return true;
        }

        // 6. Warden Tradition Lock: Warden kingdoms always crown a Warden king if one is alive
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Kingdom), nameof(Kingdom.setKing))]
        private static void SetKingPrefix(Kingdom __instance, ref Actor pActor, bool pFromLoad)
        {
            try
            {
                if (pFromLoad || __instance == null || !IsWardenKingdom(__instance))
                    return;

                if (pActor == null || pActor.asset?.id == WardenSpeciesRegistry.WardenAssetId)
                    return;

                Actor bestWarden = FindStrongestAliveWarden(__instance);
                if (bestWarden != null)
                {
                    if (bestWarden.city != null && bestWarden.city.leader == bestWarden)
                    {
                        bestWarden.city.removeLeader();
                    }
                    pActor = bestWarden;
                }
            }
            catch { }
        }

        private static Actor FindStrongestAliveWarden(Kingdom kingdom)
        {
            if (kingdom == null) return null;

            Actor best = null;
            int bestLevel = -1;
            int bestKills = -1;
            int bestMaxHp = -1;

            var units = kingdom.getUnits();
            if (units == null) return null;

            foreach (Actor u in units)
            {
                if (u == null || !u.isAlive()) continue;
                if (u.asset == null || u.asset.is_boat) continue;
                if (u.asset.id != WardenSpeciesRegistry.WardenAssetId) continue;

                ActorData actorData = ActorDataRef != null ? ActorDataRef(u) : null;
                int level = actorData != null ? actorData.level : u.level;
                int kills = actorData != null ? actorData.kills : 0;
                int maxHp = u.getMaxHealth();

                if (best == null ||
                    level > bestLevel ||
                    (level == bestLevel && kills > bestKills) ||
                    (level == bestLevel && kills == bestKills && maxHp > bestMaxHp))
                {
                    best = u;
                    bestLevel = level;
                    bestKills = kills;
                    bestMaxHp = maxHp;
                }
            }

            return best;
        }

        public static WorldTile FindSafeLandNearCamera()
        {
            var world = MapBox.instance;
            if (world == null) return null;

            Camera cam = Camera.main;
            int cx = cam != null ? Mathf.Clamp(Mathf.RoundToInt(cam.transform.position.x), 2, MapBox.width - 3) : MapBox.width / 2;
            int cy = cam != null ? Mathf.Clamp(Mathf.RoundToInt(cam.transform.position.y), 2, MapBox.height - 3) : MapBox.height / 2;

            for (int r = 0; r < 60; r += 2)
            {
                for (int dy = -r; dy <= r; dy += Math.Max(1, r))
                {
                    for (int dx = -r; dx <= r; dx += Math.Max(1, r))
                    {
                        WorldTile t = world.GetTile(cx + dx, cy + dy);
                        if (t != null && t.Type != null && !t.Type.liquid && t.Type.ground)
                            return t;
                    }
                }
            }
            return world.GetTile(cx, cy);
        }
    }
}
