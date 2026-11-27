using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using NeoModLoader.General;
using UnityEngine;

namespace GrandWarfare
{
    public enum GovernmentType
    {
        Monarchy = 0,
        Republic = 1,
        Collectivist = 2,
        MerchantLeague = 3,
        MilitaryJunta = 4,
        Technocracy = 5
    }

    public enum TradeSpecialty
    {
        Balanced = 0,
        Breadbasket = 1,
        ArmoryState = 2,
        MaritimeHub = 3,
        ResearchHub = 4
    }

    public class KingdomGeopoliticsState
    {
        public long KingdomId;
        public GovernmentType Government;
        public TradeSpecialty Specialty;
        public int ShipmentsSent;
        public int ShipmentsReceived;
    }

    public static class GeopoliticsAndTradeSystem
    {
        public const string OpinionGovernmentId = "suite_government_diplomacy";

        private const float TickIntervalSeconds = 18.0f;
        private const int MaxTradeLogs = 24;
        private const int MaxTradingBoatsPerKingdom = 6;

        private static readonly string[] FoodResourceIds = { "bread", "meat", "fish", "berries" };
        private static readonly string[] WarMaterialResourceIds = { "common_metals", "stone", "wood" };

        public static readonly Dictionary<long, KingdomGeopoliticsState> States = new Dictionary<long, KingdomGeopoliticsState>();
        public static readonly List<string> RecentTradeLogs = new List<string>();

        public static int TotalShipments { get; private set; }
        public static int TotalResourcesTraded { get; private set; }
        public static int TradeBoatsLaunched { get; private set; }

        private static readonly FieldInfo BuildingAssetField =
            AccessTools.Field(typeof(Building), "asset");

        private static readonly FieldInfo BuildingDocksField =
            AccessTools.Field(typeof(Building), "component_docks");

        private static readonly MethodInfo SetKingdomMethod =
            AccessTools.Method(typeof(Actor), "setKingdom", new[] { typeof(Kingdom) });

        private static readonly MethodInfo SpeciesIdGetter =
            AccessTools.PropertyGetter(typeof(Subspecies), "species_id");

        private static bool _localesRegistered;
        private static bool _opinionRegistered;
        private static float _tickTimer;

        public static void EnsureAssetsRegistered()
        {
            if (!_localesRegistered && LocalizedTextManager.instance != null)
            {
                try
                {
                    LM.AddToCurrentLocale("opinion_suite_government_diplomacy", "Diplomatic Regime");
                    if (LocalizedTextManager.stringExists("opinion_suite_government_diplomacy"))
                    {
                        _localesRegistered = true;
                    }
                }
                catch { }
            }

            if (!_opinionRegistered && AssetManager.opinion_library != null && AssetManager.opinion_library.list != null && AssetManager.opinion_library.list.Count > 0)
            {
                try
                {
                    if (!AssetManager.opinion_library.has(OpinionGovernmentId))
                    {
                        AssetManager.opinion_library.add(new OpinionAsset
                        {
                            id = OpinionGovernmentId,
                            translation_key = "opinion_suite_government_diplomacy",
                            translation_key_negative = "opinion_suite_government_diplomacy",
                            calc = (pMain, pTarget) =>
                            {
                                if (Main.Settings == null || !Main.Settings.EnableGeopoliticsAndTrade) return 0;
                                if (pMain == null || pTarget == null || !pMain.isCiv() || !pTarget.isCiv()) return 0;

                                var mainSt = GetOrCreateState(pMain);
                                var targetSt = GetOrCreateState(pTarget);
                                if (mainSt == null || targetSt == null) return 0;

                                int bonus = 0;
                                if (mainSt.Government == GovernmentType.Republic && targetSt.Government == GovernmentType.Republic)
                                {
                                    bonus += 28;
                                }
                                else if (mainSt.Government == GovernmentType.Republic || targetSt.Government == GovernmentType.Republic)
                                {
                                    bonus += 16;
                                }
                                else if (mainSt.Government == targetSt.Government)
                                {
                                    bonus += 8;
                                }
                                return bonus;
                            }
                        });
                    }
                    _opinionRegistered = true;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[GrandWarfare] Government opinion registration deferred: {e.Message}");
                }
            }
        }

        public static void Tick(float dt)
        {
            EnsureAssetsRegistered();

            var settings = Main.Settings;
            var world = MapBox.instance;
            if (settings == null || !settings.EnableGeopoliticsAndTrade || world == null) return;

            _tickTimer += dt;
            if (_tickTimer < TickIntervalSeconds) return;
            _tickTimer = 0f;

            try
            {
                TickGovernments(world);
                TickAllianceTrade(world);
                PruneDeadStates(world);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GrandWarfare] Geopolitics & trade tick error: {e.Message}");
            }
        }

        public static string RunTradePulseNow()
        {
            var world = MapBox.instance;
            if (world == null || world.kingdoms == null) return "Load a world first.";
            EnsureAssetsRegistered();

            int beforeShipments = TotalShipments;
            int beforeBoats = TradeBoatsLaunched;
            try
            {
                TickGovernments(world);
                TickAllianceTrade(world);
            }
            catch (Exception e)
            {
                return $"Trade pulse error: {e.Message}";
            }

            int sent = TotalShipments - beforeShipments;
            int boats = TradeBoatsLaunched - beforeBoats;
            if (sent > 0)
            {
                return $"Alliance Trade Pulse: {sent} shipment(s) delivered{(boats > 0 ? $" & {boats} trade boat(s) launched" : "")}!";
            }
            return "Geopolitics updated. Form an alliance with 2+ kingdoms to see cross-border trade shipments.";
        }

        public static KingdomGeopoliticsState GetOrCreateState(Kingdom k)
        {
            if (k == null) return null;
            if (States.TryGetValue(k.id, out var existing))
            {
                return existing;
            }

            var st = new KingdomGeopoliticsState
            {
                KingdomId = k.id,
                Government = AssignDeterministicGovernment(k),
                Specialty = AssignDeterministicSpecialty(k)
            };
            States[k.id] = st;
            return st;
        }

        public static int GetGovernmentIndex(Kingdom k)
        {
            var st = GetOrCreateState(k);
            return st != null ? (int)st.Government : -1;
        }

        public static int GetSpecialtyIndex(Kingdom k)
        {
            var st = GetOrCreateState(k);
            return st != null ? (int)st.Specialty : -1;
        }

        public static void SetGovernmentIndex(Kingdom k, int govIndex)
        {
            var st = GetOrCreateState(k);
            if (st == null) return;
            int count = Enum.GetValues(typeof(GovernmentType)).Length;
            int clamped = ((govIndex % count) + count) % count;
            st.Government = (GovernmentType)clamped;
        }

        public static void SetSpecialtyIndex(Kingdom k, int specIndex)
        {
            var st = GetOrCreateState(k);
            if (st == null) return;
            int count = Enum.GetValues(typeof(TradeSpecialty)).Length;
            int clamped = ((specIndex % count) + count) % count;
            st.Specialty = (TradeSpecialty)clamped;
        }

        public static void CycleGovernment(Kingdom k)
        {
            var st = GetOrCreateState(k);
            if (st == null) return;
            int count = Enum.GetValues(typeof(GovernmentType)).Length;
            st.Government = (GovernmentType)(((int)st.Government + 1) % count);
        }

        public static void CycleSpecialty(Kingdom k)
        {
            var st = GetOrCreateState(k);
            if (st == null) return;
            int count = Enum.GetValues(typeof(TradeSpecialty)).Length;
            st.Specialty = (TradeSpecialty)(((int)st.Specialty + 1) % count);
        }

        public static string FormatGovernment(GovernmentType gov)
        {
            switch (gov)
            {
                case GovernmentType.Monarchy: return "Monarchy";
                case GovernmentType.Republic: return "Republic";
                case GovernmentType.Collectivist: return "Collectivist";
                case GovernmentType.MerchantLeague: return "Merchant League";
                case GovernmentType.MilitaryJunta: return "Military Junta";
                case GovernmentType.Technocracy: return "Technocracy";
                default: return gov.ToString();
            }
        }

        public static string FormatSpecialty(TradeSpecialty spec)
        {
            switch (spec)
            {
                case TradeSpecialty.Balanced: return "Balanced";
                case TradeSpecialty.Breadbasket: return "Breadbasket";
                case TradeSpecialty.ArmoryState: return "Armory State";
                case TradeSpecialty.MaritimeHub: return "Maritime Hub";
                case TradeSpecialty.ResearchHub: return "Research Hub";
                default: return spec.ToString();
            }
        }

        private static GovernmentType AssignDeterministicGovernment(Kingdom k)
        {
            string sp = GetKingdomSpecies(k);
            long hash = Math.Abs(k.id * 31 + 17);
            bool coastal = HasCoastalDock(k);

            if (sp == "warden")
            {
                return (hash % 3 == 0) ? GovernmentType.MilitaryJunta : GovernmentType.Monarchy;
            }
            if (coastal && (hash % 3 == 0))
            {
                return GovernmentType.MerchantLeague;
            }

            switch (sp)
            {
                case "orc":
                case "wolf":
                case "rhino":
                case "crocodile":
                case "hyena":
                    return (hash % 2 == 0) ? GovernmentType.MilitaryJunta : GovernmentType.Monarchy;
                case "dwarf":
                case "ant":
                case "bee":
                case "bear":
                    return (hash % 3 == 0) ? GovernmentType.Collectivist : (hash % 3 == 1 ? GovernmentType.Technocracy : GovernmentType.Monarchy);
                case "elf":
                case "owl":
                case "monkey":
                case "penguin":
                    return (hash % 3 == 0) ? GovernmentType.Technocracy : (hash % 3 == 1 ? GovernmentType.Republic : GovernmentType.Monarchy);
                case "fox":
                case "cat":
                case "raccoon":
                case "crab":
                    return (hash % 2 == 0) ? GovernmentType.MerchantLeague : GovernmentType.Republic;
                default:
                    int govCount = Enum.GetValues(typeof(GovernmentType)).Length;
                    return (GovernmentType)(int)(hash % govCount);
            }
        }

        private static TradeSpecialty AssignDeterministicSpecialty(Kingdom k)
        {
            string sp = GetKingdomSpecies(k);
            long hash = Math.Abs(k.id * 53 + 7);
            bool coastal = HasCoastalDock(k);

            if (coastal && (hash % 2 == 0))
            {
                return TradeSpecialty.MaritimeHub;
            }

            switch (sp)
            {
                case "dwarf":
                case "orc":
                case "warden":
                case "rhino":
                    return (hash % 3 == 0) ? TradeSpecialty.Balanced : TradeSpecialty.ArmoryState;
                case "elf":
                case "owl":
                case "monkey":
                case "penguin":
                    return (hash % 3 == 0) ? TradeSpecialty.Breadbasket : TradeSpecialty.ResearchHub;
                case "crab":
                case "frog":
                case "turtle":
                case "crocodile":
                    return TradeSpecialty.MaritimeHub;
                case "cow":
                case "sheep":
                case "chicken":
                case "rabbit":
                case "capybara":
                    return TradeSpecialty.Breadbasket;
                default:
                    int specCount = Enum.GetValues(typeof(TradeSpecialty)).Length;
                    return (TradeSpecialty)(int)(hash % specCount);
            }
        }

        private static void TickGovernments(MapBox world)
        {
            if (world?.kingdoms == null) return;

            var kingdomsSnapshot = new List<Kingdom>(world.kingdoms);
            var aliveUnits = world.units?.units_only_alive != null
                ? new List<Actor>(world.units.units_only_alive)
                : new List<Actor>();

            for (int i = 0; i < kingdomsSnapshot.Count; i++)
            {
                Kingdom k = kingdomsSnapshot[i];
                if (k == null || !k.isAlive() || !k.isCiv()) continue;

                var st = GetOrCreateState(k);
                City cap = GetAliveCapital(k);

                try
                {
                    switch (st.Government)
                    {
                        case GovernmentType.Monarchy:
                            ApplyMonarchyBuff(k);
                            break;

                        case GovernmentType.Republic:
                            ApplyRepublicBuff(k, cap);
                            break;

                        case GovernmentType.Collectivist:
                            ApplyCollectivistBuff(k, aliveUnits);
                            break;

                        case GovernmentType.MerchantLeague:
                            ApplyMerchantLeagueBuff(k, cap);
                            break;

                        case GovernmentType.MilitaryJunta:
                            ApplyMilitaryJuntaBuff(k, aliveUnits);
                            break;

                        case GovernmentType.Technocracy:
                            ApplyTechnocracyBuff(k, cap);
                            break;
                    }

                    // Maritime hubs generate extra coastal port tariff gold even outside active alliance trades
                    if (st.Specialty == TradeSpecialty.MaritimeHub && cap != null && HasCoastalDock(k))
                    {
                        cap.addResourcesToRandomStockpile("gold", 8);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[GrandWarfare] Government buff skipped for {k.name}: {e.Message}");
                }
            }
        }

        private static void ApplyMonarchyBuff(Kingdom k)
        {
            Actor king = k.king;
            if (king == null || !king.isAlive()) return;

            bool dirty = false;
            if (AssetManager.traits != null)
            {
                if (!king.hasTrait("veteran") && AssetManager.traits.has("veteran"))
                {
                    king.addTrait("veteran", false);
                    dirty = true;
                }
                if (!king.hasTrait("strong_minded") && AssetManager.traits.has("strong_minded"))
                {
                    king.addTrait("strong_minded", false);
                    dirty = true;
                }
            }
            if (dirty) king.setStatsDirty();
            king.restoreHealthPercent(0.20f);
        }

        private static void ApplyRepublicBuff(Kingdom k, City cap)
        {
            // Opinion diplomacy bonus is handled continuously via OpinionGovernmentId.
            // Civic leadership stays wise and prosperous.
            if (cap == null) return;
            Actor leader = cap.leader;
            if (leader != null && leader.isAlive() && AssetManager.traits != null &&
                !leader.hasTrait("wise") && AssetManager.traits.has("wise"))
            {
                leader.addTrait("wise", false);
                leader.setStatsDirty();
            }
            cap.addResourcesToRandomStockpile("gold", 6);
        }

        private static void ApplyCollectivistBuff(Kingdom k, List<Actor> aliveUnits)
        {
            int bolstered = 0;
            for (int i = 0; i < aliveUnits.Count && bolstered < 15; i++)
            {
                Actor u = aliveUnits[i];
                if (u == null || !u.isAlive() || u.kingdom != k) continue;
                if (u.asset == null || u.asset.is_boat) continue;
                if (u.army == null && !u.isWarrior() && bolstered >= 8) continue;

                u.restoreHealthPercent(0.18f);
                u.setMaxStamina();
                u.addNutritionFromEating(12, false, false);
                bolstered++;
            }
        }

        private static void ApplyMerchantLeagueBuff(Kingdom k, City cap)
        {
            if (cap == null) return;
            int cityCount = k.cities != null ? k.cities.Count : 1;
            int goldIncome = Mathf.Clamp(12 + (cityCount - 1) * 4, 12, 26);
            cap.addResourcesToRandomStockpile("gold", goldIncome);
        }

        private static void ApplyMilitaryJuntaBuff(Kingdom k, List<Actor> aliveUnits)
        {
            int trained = 0;
            for (int i = 0; i < aliveUnits.Count && trained < 4; i++)
            {
                Actor u = aliveUnits[i];
                if (u == null || !u.isAlive() || u.kingdom != k) continue;
                if (u.asset == null || u.asset.is_boat) continue;
                if (u.army == null && !u.isWarrior()) continue;

                bool dirty = false;
                if (AssetManager.traits != null)
                {
                    if (!u.hasTrait("veteran") && AssetManager.traits.has("veteran"))
                    {
                        u.addTrait("veteran", false);
                        dirty = true;
                    }
                    else if (!u.hasTrait("tough") && AssetManager.traits.has("tough"))
                    {
                        u.addTrait("tough", false);
                        dirty = true;
                    }
                }
                if (dirty) u.setStatsDirty();
                u.setMaxStamina();
                trained++;
            }
        }

        private static void ApplyTechnocracyBuff(Kingdom k, City cap)
        {
            Actor scholar = (k.king != null && k.king.isAlive()) ? k.king : cap?.leader;
            if (scholar != null && scholar.isAlive() && AssetManager.traits != null)
            {
                if (!scholar.hasTrait("genius") && AssetManager.traits.has("genius"))
                {
                    scholar.addTrait("genius", false);
                    scholar.setStatsDirty();
                }
                else if (!scholar.hasTrait("wise") && AssetManager.traits.has("wise"))
                {
                    scholar.addTrait("wise", false);
                    scholar.setStatsDirty();
                }
            }

            if (cap != null)
            {
                cap.addResourcesToRandomStockpile("common_metals", 4);
                cap.addResourcesToRandomStockpile("gold", 5);
            }
        }

        public static void TickAllianceTrade(MapBox world)
        {
            if (world?.alliances == null) return;

            var alliancesSnapshot = new List<Alliance>(world.alliances);
            if (alliancesSnapshot.Count == 0) return;

            var aliveUnits = world.units?.units_only_alive != null
                ? new List<Actor>(world.units.units_only_alive)
                : new List<Actor>();

            for (int aIdx = 0; aIdx < alliancesSnapshot.Count; aIdx++)
            {
                Alliance alliance = alliancesSnapshot[aIdx];
                if (alliance == null || !alliance.isAlive() || alliance.kingdoms_list == null) continue;

                var membersRaw = new List<Kingdom>(alliance.kingdoms_list);
                var members = new List<Kingdom>();
                for (int i = 0; i < membersRaw.Count; i++)
                {
                    Kingdom m = membersRaw[i];
                    if (m != null && m.isAlive() && m.isCiv() && GetAliveCapital(m) != null)
                    {
                        members.Add(m);
                    }
                }
                if (members.Count < 2) continue;

                try
                {
                    RunAllianceFoodExport(world, members, aliveUnits);
                    RunAllianceWarMaterialExport(world, members, aliveUnits);
                    RunAllianceMaritimeGoldExport(world, members, aliveUnits);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[GrandWarfare] Alliance trade error ({alliance.name}): {e.Message}");
                }
            }
        }

        private static void RunAllianceFoodExport(MapBox world, List<Kingdom> members, List<Actor> aliveUnits)
        {
            Kingdom donor = null;
            City donorCap = null;
            string bestFoodId = null;
            int bestFoodAmt = 25;
            bool donorIsBreadbasket = false;

            for (int i = 0; i < members.Count; i++)
            {
                Kingdom k = members[i];
                City cap = GetAliveCapital(k);
                if (cap == null) continue;

                var st = GetOrCreateState(k);
                bool isBread = st.Specialty == TradeSpecialty.Breadbasket;

                for (int f = 0; f < FoodResourceIds.Length; f++)
                {
                    string resId = FoodResourceIds[f];
                    int amt = cap.getResourcesAmount(resId);
                    if (amt <= 25) continue;

                    bool better = (isBread && !donorIsBreadbasket) ||
                                  (isBread == donorIsBreadbasket && amt > bestFoodAmt);
                    if (better)
                    {
                        donor = k;
                        donorCap = cap;
                        bestFoodId = resId;
                        bestFoodAmt = amt;
                        donorIsBreadbasket = isBread;
                    }
                }
            }

            if (donor == null || donorCap == null || string.IsNullOrEmpty(bestFoodId)) return;

            // Pick lowest-food ally capital in the alliance
            Kingdom recip = null;
            City recipCap = null;
            int lowestFood = int.MaxValue;
            for (int i = 0; i < members.Count; i++)
            {
                Kingdom cand = members[i];
                if (cand == donor) continue;
                City cap = GetAliveCapital(cand);
                if (cap == null) continue;
                int totalFood = cap.getTotalFood();
                if (totalFood < lowestFood)
                {
                    lowestFood = totalFood;
                    recip = cand;
                    recipCap = cap;
                }
            }

            if (recip == null || recipCap == null) return;

            int maxTransfer = Math.Max(10, bestFoodAmt - 10);
            int transfer = Math.Min(maxTransfer, UnityEngine.Random.Range(10, 21));
            if (transfer <= 0) return;

            ExecuteTradeTransfer(world, donor, donorCap, recip, recipCap, bestFoodId, transfer, aliveUnits);
        }

        private static void RunAllianceWarMaterialExport(MapBox world, List<Kingdom> members, List<Actor> aliveUnits)
        {
            for (int i = 0; i < members.Count; i++)
            {
                Kingdom donor = members[i];
                var st = GetOrCreateState(donor);
                if (st.Specialty != TradeSpecialty.ArmoryState) continue;

                City donorCap = GetAliveCapital(donor);
                if (donorCap == null) continue;

                // Find an allied member currently fighting a war
                Kingdom recip = null;
                City recipCap = null;
                int lowestStock = int.MaxValue;
                for (int j = 0; j < members.Count; j++)
                {
                    Kingdom cand = members[j];
                    if (cand == donor) continue;
                    bool atWar = cand.hasEnemies() || (world.wars != null && world.wars.hasWars(cand));
                    if (!atWar) continue;

                    City cap = GetAliveCapital(cand);
                    if (cap == null) continue;
                    int metals = cap.getResourcesAmount("common_metals") + cap.getResourcesAmount("wood") + cap.getResourcesAmount("stone");
                    if (metals < lowestStock)
                    {
                        lowestStock = metals;
                        recip = cand;
                        recipCap = cap;
                    }
                }

                if (recip == null || recipCap == null) continue;

                string chosenMat = null;
                int chosenAmt = 12;
                for (int m = 0; m < WarMaterialResourceIds.Length; m++)
                {
                    string resId = WarMaterialResourceIds[m];
                    int have = donorCap.getResourcesAmount(resId);
                    if (have > chosenAmt)
                    {
                        chosenAmt = have;
                        chosenMat = resId;
                    }
                }

                if (string.IsNullOrEmpty(chosenMat)) continue;

                int transfer = Math.Min(chosenAmt - 6, UnityEngine.Random.Range(6, 13));
                if (transfer <= 0) continue;

                ExecuteTradeTransfer(world, donor, donorCap, recip, recipCap, chosenMat, transfer, aliveUnits);
            }
        }

        private static void RunAllianceMaritimeGoldExport(MapBox world, List<Kingdom> members, List<Actor> aliveUnits)
        {
            for (int i = 0; i < members.Count; i++)
            {
                Kingdom donor = members[i];
                var st = GetOrCreateState(donor);
                if (st.Specialty != TradeSpecialty.MaritimeHub) continue;

                City donorCap = GetAliveCapital(donor);
                if (donorCap == null) continue;

                int donorGold = donorCap.getResourcesAmount("gold");
                if (donorGold <= 25) continue;

                Kingdom recip = null;
                City recipCap = null;
                int lowestGold = int.MaxValue;
                for (int j = 0; j < members.Count; j++)
                {
                    Kingdom cand = members[j];
                    if (cand == donor) continue;
                    City cap = GetAliveCapital(cand);
                    if (cap == null) continue;
                    int g = cap.getResourcesAmount("gold");
                    if (g < lowestGold)
                    {
                        lowestGold = g;
                        recip = cand;
                        recipCap = cap;
                    }
                }

                if (recip == null || recipCap == null) continue;

                int transfer = Math.Min(donorGold - 10, UnityEngine.Random.Range(10, 21));
                if (transfer <= 0) continue;

                ExecuteTradeTransfer(world, donor, donorCap, recip, recipCap, "gold", transfer, aliveUnits);
            }
        }

        private static void ExecuteTradeTransfer(
            MapBox world,
            Kingdom donor,
            City donorCap,
            Kingdom recip,
            City recipCap,
            string resourceId,
            int amount,
            List<Actor> aliveUnits)
        {
            if (donorCap == null || recipCap == null || amount <= 0) return;

            donorCap.takeResource(resourceId, amount);
            recipCap.addResourcesToRandomStockpile(resourceId, amount);

            TotalShipments++;
            TotalResourcesTraded += amount;
            var donorSt = GetOrCreateState(donor);
            var recipSt = GetOrCreateState(recip);
            if (donorSt != null) donorSt.ShipmentsSent++;
            if (recipSt != null) recipSt.ShipmentsReceived++;

            bool launchedBoat = TryLaunchTradingBoat(world, donor, aliveUnits);
            if (!launchedBoat)
            {
                ApplyTradeReceptionBoost(recip, recipCap, aliveUnits);
            }

            string prettyRes = FormatResourceName(resourceId);
            string boatSuffix = launchedBoat ? " [Trade Boat]" : "";
            RecordTradeLog($"{donor.name} → {recip.name}: {amount} {prettyRes}{boatSuffix}");
        }

        private static bool TryLaunchTradingBoat(MapBox world, Kingdom donor, List<Actor> aliveUnits)
        {
            if (world?.units == null || donor == null || AssetManager.actor_library == null) return false;

            int tradingBoats = 0;
            for (int i = 0; i < aliveUnits.Count; i++)
            {
                Actor u = aliveUnits[i];
                if (u == null || !u.isAlive() || u.kingdom != donor || u.asset == null || !u.asset.is_boat) continue;
                string id = u.asset.id ?? "";
                string bt = u.asset.boat_type ?? "";
                if (bt.Contains("trading") || id.Contains("trading"))
                {
                    tradingBoats++;
                }
            }
            if (tradingBoats >= MaxTradingBoatsPerKingdom) return false;

            if (!TryFindUsableDock(donor, out City dockCity, out Building dockBuilding, out Docks docks, out WorldTile oceanTile))
            {
                return false;
            }

            string boatId = "boat_trading_human";
            if (BuildingAssetField?.GetValue(dockBuilding) is BuildingAsset bAsset &&
                !string.IsNullOrEmpty(bAsset.boat_type_trading) &&
                AssetManager.actor_library.has(bAsset.boat_type_trading))
            {
                boatId = bAsset.boat_type_trading;
            }

            if (!AssetManager.actor_library.has(boatId)) return false;

            try
            {
                Actor boat = world.units.spawnNewUnit(boatId, oceanTile);
                if (boat == null) return false;

                if (dockCity != null && dockCity.isAlive())
                {
                    boat.joinCity(dockCity);
                    boat.setMetasFromCity(dockCity);
                }
                else if (SetKingdomMethod != null)
                {
                    SetKingdomMethod.Invoke(boat, new object[] { donor });
                }

                docks.addBoatToDock(boat);
                aliveUnits.Add(boat);
                TradeBoatsLaunched++;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GrandWarfare] Trading boat launch skipped: {e.Message}");
                return false;
            }
        }

        private static void ApplyTradeReceptionBoost(Kingdom recip, City recipCap, List<Actor> aliveUnits)
        {
            if (recipCap == null || !recipCap.isAlive()) return;
            try
            {
                recipCap.addResourcesToRandomStockpile("gold", 4);
                int boosted = 0;
                for (int i = 0; i < aliveUnits.Count && boosted < 6; i++)
                {
                    Actor u = aliveUnits[i];
                    if (u == null || !u.isAlive() || u.city != recipCap || u.kingdom != recip) continue;
                    if (u.asset == null || u.asset.is_boat) continue;
                    u.restoreHealthPercent(0.20f);
                    u.addNutritionFromEating(15, false, false);
                    boosted++;
                }
            }
            catch { }
        }

        private static bool HasCoastalDock(Kingdom k)
        {
            return TryFindUsableDock(k, out _, out _, out _, out _);
        }

        private static bool TryFindUsableDock(
            Kingdom k,
            out City dockCity,
            out Building dockBuilding,
            out Docks docks,
            out WorldTile oceanTile)
        {
            dockCity = null;
            dockBuilding = null;
            docks = null;
            oceanTile = null;

            if (k?.cities == null || BuildingDocksField == null) return false;

            var citiesSnapshot = new List<City>(k.cities);
            for (int cIdx = 0; cIdx < citiesSnapshot.Count; cIdx++)
            {
                City c = citiesSnapshot[cIdx];
                if (c == null || !c.isAlive() || c.buildings == null) continue;

                var bSnapshot = new List<Building>(c.buildings);
                for (int bIdx = 0; bIdx < bSnapshot.Count; bIdx++)
                {
                    Building b = bSnapshot[bIdx];
                    if (b == null || !b.isAlive() || !b.isUsable()) continue;

                    Docks comp = BuildingDocksField.GetValue(b) as Docks;
                    if (comp == null || !comp.hasOceanTiles() || comp.tiles_ocean == null || comp.tiles_ocean.Count == 0)
                        continue;

                    WorldTile tile = comp.tiles_ocean[0];
                    if (tile == null) continue;

                    dockCity = c;
                    dockBuilding = b;
                    docks = comp;
                    oceanTile = tile;
                    return true;
                }
            }

            return false;
        }

        private static City GetAliveCapital(Kingdom k)
        {
            if (k == null || !k.isAlive()) return null;
            if (k.capital != null && k.capital.isAlive()) return k.capital;
            if (k.cities == null) return null;

            var cities = new List<City>(k.cities);
            for (int i = 0; i < cities.Count; i++)
            {
                City c = cities[i];
                if (c != null && c.isAlive()) return c;
            }
            return null;
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

        private static void PruneDeadStates(MapBox world)
        {
            if (world?.kingdoms == null || States.Count == 0) return;
            var ids = new List<long>(States.Keys);
            for (int i = 0; i < ids.Count; i++)
            {
                Kingdom k = world.kingdoms.get(ids[i]);
                if (k == null || !k.isAlive())
                {
                    States.Remove(ids[i]);
                }
            }
        }

        private static string FormatResourceName(string resId)
        {
            switch (resId)
            {
                case "common_metals": return "Metals";
                case "bread": return "Bread";
                case "meat": return "Meat";
                case "fish": return "Fish";
                case "berries": return "Berries";
                case "stone": return "Stone";
                case "wood": return "Wood";
                case "gold": return "Gold";
                default: return resId;
            }
        }

        private static void RecordTradeLog(string entry)
        {
            if (string.IsNullOrEmpty(entry)) return;
            RecentTradeLogs.Add(entry);
            if (RecentTradeLogs.Count > MaxTradeLogs)
            {
                RecentTradeLogs.RemoveAt(0);
            }
        }
    }
}
