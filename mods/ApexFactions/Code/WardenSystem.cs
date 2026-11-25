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

        // Internal flag so our own Merciful Ceasefire call is never blocked by our endWar guard
        private static bool _allowingMercifulCeasefire = false;

        public static void AnointKingdomAsWardens(Kingdom k)
        {
            if (k == null || !k.isCiv()) return;
            WardenKingdomIds.Add(k.id);

            // Elevate current citizens with Warden traits (intelligent, long-lived, strong, peaceful)
            var alive = MapBox.instance?.units?.units_only_alive;
            if (alive != null)
            {
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

        private static readonly System.Reflection.MethodInfo SetCityMethod =
            AccessTools.Method(typeof(Actor), "setCity", new[] { typeof(City) });

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

            var settlers = new List<Actor>(14);
            for (int i = 0; i < 14; i++)
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

                Actor u = world.units.spawnNewUnit("human", spawnTile);
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
                    for (int i = 1; i < settlers.Count; i++)
                    {
                        if (SetCityMethod != null)
                        {
                            try { SetCityMethod.Invoke(settlers[i], new object[] { newCity }); } catch { }
                        }
                    }
                    AnointKingdomAsWardens(newKingdom);
                    foundedKingdomName = newKingdom.name;
                    Debug.Log($"[ApexFactions] Founded Warden Kingdom '{newKingdom.name}' at ({center.x},{center.y}) with {settlers.Count} settlers.");
                    return settlers.Count;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ApexFactions] Immediate city founding deferred: {e.Message}");
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

        private static WorldTile _pendingWardenSpawnTile;
        private static Actor _pendingWardenLeader;
        private static float _pendingWardenTimer;

        private static void ApplyWardenPeacetimeTraits(Actor u)
        {
            u.addTrait("genius");
            u.addTrait("wise");
            u.addTrait("strong");
            u.addTrait("tough");
            u.addTrait("peaceful");
            u.setStatsDirty();
            u.restoreHealthPercent(1f);
        }

        private static void RallyWardenWarriors(Kingdom warden)
        {
            var alive = MapBox.instance?.units?.units_only_alive;
            if (alive == null) return;

            int rallied = 0;
            for (int i = 0; i < alive.Count; i++)
            {
                Actor u = alive[i];
                if (u == null || !u.isAlive() || u.kingdom != warden) continue;
                if (u.asset != null && u.asset.is_boat) continue;

                u.removeTrait("peaceful");
                u.addTrait("veteran");
                u.addTrait("strong");
                u.addTrait("tough");
                u.addTrait("agile");
                u.setStatsDirty();
                u.restoreHealthPercent(1f);
                rallied++;
            }
            Debug.Log($"[ApexFactions] Sleeping Giant awakened: rallied {rallied} Wardens of {warden.name}!");
        }

        private static void StandDownWardenWarriors(Kingdom warden)
        {
            var alive = MapBox.instance?.units?.units_only_alive;
            if (alive == null) return;

            for (int i = 0; i < alive.Count; i++)
            {
                Actor u = alive[i];
                if (u != null && u.isAlive() && u.kingdom == warden)
                {
                    u.addTrait("peaceful");
                }
            }
        }

        public static void Tick(float dt)
        {
            var world = MapBox.instance;
            if (world == null || Main.Settings == null || !Main.Settings.EnableWardenDoctrine) return;

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
                else if (_pendingWardenSpawnTile != null && world.kingdoms != null)
                {
                    foreach (var k in world.kingdoms)
                    {
                        if (k == null || !k.isCiv() || !k.isAlive() || WardenKingdomIds.Contains(k.id)) continue;
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
            foreach (var kv in ActiveRetaliations)
            {
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

            foreach (long id in finished)
            {
                ActiveRetaliations.Remove(id);
            }
        }

        // 1. Wardens NEVER declare offensive wars
        [HarmonyPrefix]
        [HarmonyPatch(typeof(DiplomacyManager), "startWar")]
        private static bool StartWarPrefix(Kingdom pAttacker)
        {
            if (Main.Settings == null || !Main.Settings.EnableWardenDoctrine) return true;
            if (pAttacker != null && WardenKingdomIds.Contains(pAttacker.id))
            {
                return false; // Peace doctrine: never initiate aggression
            }
            return true;
        }

        // 2. If someone attacks a Warden kingdom, awaken the Sleeping Giant and lock baseline pop
        [HarmonyPostfix]
        [HarmonyPatch(typeof(DiplomacyManager), "startWar")]
        private static void StartWarPostfix(Kingdom pAttacker, Kingdom pDefender, War __result)
        {
            if (Main.Settings == null || !Main.Settings.EnableWardenDoctrine) return;
            if (__result == null || pAttacker == null || pDefender == null) return;
            if (!WardenKingdomIds.Contains(pDefender.id)) return;

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

            Debug.Log($"[ApexFactions] {pAttacker.name} provoked {pDefender.name} (Strike #{strikeNum}, mercy={effectivePct:0}%)!");
            RallyWardenWarriors(pDefender);
        }

        // 3. Refuse premature peace treaties while aggressor is still above the 15% mercy threshold
        [HarmonyPrefix]
        [HarmonyPatch(typeof(WarManager), nameof(WarManager.endWar))]
        private static bool EndWarPrefix(War pWar)
        {
            if (_allowingMercifulCeasefire || pWar == null || Main.Settings == null || !Main.Settings.EnableWardenDoctrine)
                return true;

            if (ActiveRetaliations.TryGetValue(pWar.id, out RetaliationTracker tr))
            {
                Kingdom aggressor = MapBox.instance?.kingdoms?.get(tr.AggressorKingdomId);
                if (aggressor != null && aggressor.isAlive() && aggressor.getPopulationPeople() > tr.MercyTargetPop)
                {
                    return false; // Relentless retaliation until mercy target is reached
                }
            }
            return true;
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
