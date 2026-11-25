using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SuiteHub
{
    public class BattleHotspot
    {
        public float X;
        public float Y;
        public int Hits;
        public float LastUpdateTime;
        public string SideA;
        public string SideB;
        public long SideAId;
        public long SideBId;
    }

    [HarmonyPatch(typeof(Actor))]
    public static class BattleRadarSystem
    {
        public static bool Enabled { get; set; } = true;
        public static readonly List<BattleHotspot> Hotspots = new List<BattleHotspot>();

        private const float ClusterRadius = 18f;
        private const float HotspotLifetime = 16f;

        // Silently track live combat hotspots between civilized factions for Jump-to-Battle and the War Monitor
        [HarmonyPostfix]
        [HarmonyPatch("getHit")]
        private static void ActorGetHitPostfix(Actor __instance, BaseSimObject pAttacker)
        {
            if (!Enabled || __instance == null || __instance.current_tile == null) return;
            if (pAttacker == null || __instance.kingdom == null || pAttacker.kingdom == null) return;
            if (__instance.kingdom == pAttacker.kingdom) return;
            if (!__instance.kingdom.isCiv() && !pAttacker.kingdom.isCiv()) return;

            RecordHit(
                __instance.current_tile.x,
                __instance.current_tile.y,
                pAttacker.kingdom.name ?? "Attackers",
                __instance.kingdom.name ?? "Defenders",
                pAttacker.kingdom.id,
                __instance.kingdom.id
            );
        }

        public static void RecordHit(float x, float y, string sideA, string sideB)
        {
            RecordHit(x, y, sideA, sideB, 0L, 0L);
        }

        public static void RecordHit(float x, float y, string sideA, string sideB, long sideAId, long sideBId)
        {
            float now = Time.unscaledTime;
            float rSq = ClusterRadius * ClusterRadius;

            for (int i = 0; i < Hotspots.Count; i++)
            {
                BattleHotspot h = Hotspots[i];
                float dx = h.X - x;
                float dy = h.Y - y;
                if (dx * dx + dy * dy <= rSq)
                {
                    h.X = Mathf.Lerp(h.X, x, 0.25f);
                    h.Y = Mathf.Lerp(h.Y, y, 0.25f);
                    h.Hits++;
                    h.LastUpdateTime = now;
                    return;
                }
            }

            if (Hotspots.Count >= 40)
            {
                Hotspots.RemoveAt(0);
            }

            Hotspots.Add(new BattleHotspot
            {
                X = x,
                Y = y,
                Hits = 1,
                LastUpdateTime = now,
                SideA = sideA,
                SideB = sideB,
                SideAId = sideAId,
                SideBId = sideBId
            });
        }

        public static void PruneExpired()
        {
            float cutoff = Time.unscaledTime - HotspotLifetime;
            for (int i = Hotspots.Count - 1; i >= 0; i--)
            {
                if (Hotspots[i].LastUpdateTime < cutoff)
                {
                    Hotspots.RemoveAt(i);
                }
            }
        }

        public static BattleHotspot FindHotspotForWar(War w)
        {
            if (w == null || Hotspots.Count == 0) return null;

            Kingdom mainAtk = w.getMainAttacker();
            Kingdom mainDef = w.getMainDefender();

            var atkIds = new HashSet<long>();
            var defIds = new HashSet<long>();
            if (mainAtk != null) atkIds.Add(mainAtk.id);
            if (mainDef != null) defIds.Add(mainDef.id);

            foreach (Kingdom k in w.getAttackers())
            {
                if (k != null) atkIds.Add(k.id);
            }
            foreach (Kingdom k in w.getDefenders())
            {
                if (k != null) defIds.Add(k.id);
            }

            BattleHotspot best = null;
            for (int i = Hotspots.Count - 1; i >= 0; i--)
            {
                BattleHotspot h = Hotspots[i];
                if (h.Hits < 2) continue;

                bool match = false;
                if (h.SideAId != 0L && h.SideBId != 0L)
                {
                    match = (atkIds.Contains(h.SideAId) && defIds.Contains(h.SideBId))
                         || (atkIds.Contains(h.SideBId) && defIds.Contains(h.SideAId));
                }
                else if (mainAtk != null && mainDef != null)
                {
                    match = (h.SideA == mainAtk.name && h.SideB == mainDef.name)
                         || (h.SideA == mainDef.name && h.SideB == mainAtk.name);
                }

                if (match && (best == null || h.Hits > best.Hits))
                {
                    best = h;
                }
            }

            return best;
        }
    }
}
