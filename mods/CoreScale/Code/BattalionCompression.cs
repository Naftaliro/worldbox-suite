using System.Collections.Generic;
using UnityEngine;

namespace CoreScale
{
    public static class BattalionCompressionSystem
    {
        public static int TotalSoldiersConsolidated { get; private set; }
        public static int RegimentsForged { get; private set; }

        /// <summary>
        /// Consolidates oversized armies into reinforced Regiment Champions to cut per-tick
        /// AI/pathfinding overhead during mega-wars while preserving army combat weight.
        /// </summary>
        public static int CompressMegaArmies(bool force = false)
        {
            var world = MapBox.instance;
            var settings = Main.Settings;
            if (world?.units?.units_only_alive == null || settings == null) return 0;

            var aliveRaw = world.units.units_only_alive;
            if (!force && aliveRaw.Count < settings.BattalionPopThreshold) return 0;

            var alive = new List<Actor>(aliveRaw);
            // Group active campaigning soldiers by Army
            var byArmy = new Dictionary<Army, List<Actor>>();
            for (int i = 0; i < alive.Count; i++)
            {
                Actor u = alive[i];
                if (u == null || !u.isAlive() || u.army == null) continue;
                if (u.asset != null && u.asset.is_boat) continue;

                if (!byArmy.TryGetValue(u.army, out var squad))
                {
                    squad = new List<Actor>(32);
                    byArmy[u.army] = squad;
                }
                squad.Add(u);
            }

            int consolidatedThisPass = 0;
            int minSize = Mathf.Max(10, settings.MinArmySizeForCompression);
            int mergeCount = Mathf.Max(2, settings.SoldiersPerRegimentMerge);

            foreach (var kv in byArmy)
            {
                Army army = kv.Key;
                List<Actor> squad = kv.Value;
                if (army == null || squad.Count < minSize) continue;

                Actor champion = army.getCaptain();
                if (champion == null || !champion.isAlive())
                {
                    champion = squad[0];
                }
                if (champion == null || !champion.isAlive()) continue;

                int absorbed = 0;
                int pooledHealth = 0;
                for (int i = squad.Count - 1; i >= 0 && absorbed < mergeCount; i--)
                {
                    Actor candidate = squad[i];
                    if (candidate == null || candidate == champion || !candidate.isAlive()) continue;
                    if (candidate.isKing() || candidate.isCityLeader()) continue;

                    pooledHealth += Mathf.Max(50, candidate.getHealth());
                    candidate.dieAndDestroy(AttackType.None);
                    absorbed++;
                }

                if (absorbed > 0 && champion.isAlive())
                {
                    // Reinforce the champion into a heavy Regiment Banner unit
                    champion.addTrait("veteran");
                    champion.addTrait("strong");
                    champion.addTrait("tough");
                    if (absorbed >= 5)
                    {
                        champion.addTrait("giant");
                    }
                    champion.setStatsDirty();
                    champion.restoreHealth(pooledHealth);
                    champion.addRenown(absorbed * 2);

                    consolidatedThisPass += absorbed;
                    TotalSoldiersConsolidated += absorbed;
                    RegimentsForged++;
                }
            }

            if (consolidatedThisPass > 0)
            {
                Debug.Log($"[CoreScale] Battalion compression consolidated {consolidatedThisPass} soldiers into reinforced regiments.");
            }
            return consolidatedThisPass;
        }
    }
}
