using System;
using System.Collections.Generic;
using HarmonyLib;
using NeoModLoader.General;
using UnityEngine;

namespace ApexFactions
{
    [HarmonyPatch]
    public static class AlienThreatSystem
    {
        public const string OpinionCoalitionId = "suite_anti_alien_coalition";
        private const int KillsRequiredForBlasterSalvage = 3;

        public static bool IsAlienThreatActive { get; private set; }
        public static int ActiveAlienUnits { get; private set; }
        public static int TotalAliensSlain { get; private set; }
        public static int TotalBlastersSalvaged { get; private set; }
        public static readonly Dictionary<long, int> AlienKillsByKingdom = new Dictionary<long, int>();

        private static bool _opinionRegistered;
        private static bool _localesRegistered;
        private static float _scanTimer;

        public static void EnsureOpinionRegistered()
        {
            if (!_localesRegistered && LocalizedTextManager.instance != null)
            {
                try
                {
                    LM.AddToCurrentLocale("opinion_suite_anti_alien_coalition", "United Anti-Alien Coalition");
                    LM.AddToCurrentLocale("opinion_suite_anti_alien_coalition_neg", "Hostile Extraterrestrial Invaders");
                    if (LocalizedTextManager.stringExists("opinion_suite_anti_alien_coalition"))
                    {
                        _localesRegistered = true;
                    }
                }
                catch { }
            }

            if (_opinionRegistered) return;
            var lib = AssetManager.opinion_library;
            if (lib == null || !lib.has("opinion_king")) return;

            try
            {
                if (!lib.has(OpinionCoalitionId))
                {
                    lib.add(new OpinionAsset
                    {
                        id = OpinionCoalitionId,
                        translation_key = "opinion_suite_anti_alien_coalition",
                        translation_key_negative = "opinion_suite_anti_alien_coalition_neg",
                        calc = (pMain, pTarget) =>
                        {
                            if (pMain == null || pTarget == null) return 0;
                            if (Main.Settings == null || !Main.Settings.EnableAlienCountermeasures) return 0;
                            if (!IsAlienThreatActive) return 0;

                            bool mainAlien = IsAlienKingdom(pMain);
                            bool targetAlien = IsAlienKingdom(pTarget);

                            if (mainAlien && targetAlien) return 0;
                            if (mainAlien || targetAlien) return -100;
                            return 85;
                        }
                    });
                }

                _opinionRegistered = true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ApexFactions] Failed to register Anti-Alien Coalition opinion: {e.Message}");
            }
        }

        public static void Tick(float dt)
        {
            EnsureOpinionRegistered();
            if (Main.Settings == null || !Main.Settings.EnableAlienCountermeasures)
            {
                IsAlienThreatActive = false;
                ActiveAlienUnits = 0;
                return;
            }

            _scanTimer += dt;
            if (_scanTimer < 4.0f) return;
            _scanTimer = 0f;

            var world = MapBox.instance;
            if (world == null) return;

            bool alienKingdomFound = false;
            if (world.kingdoms != null)
            {
                var kingdoms = new List<Kingdom>(world.kingdoms);
                for (int i = 0; i < kingdoms.Count; i++)
                {
                    Kingdom k = kingdoms[i];
                    if (k != null && k.isAlive() && k.isCiv() && IsAlienKingdom(k))
                    {
                        alienKingdomFound = true;
                        break;
                    }
                }
            }

            int alienUnits = 0;
            if (world.units?.units_only_alive != null)
            {
                var alive = new List<Actor>(world.units.units_only_alive);
                for (int i = 0; i < alive.Count; i++)
                {
                    Actor u = alive[i];
                    if (u != null && u.isAlive() && IsAlienOrUfoActor(u))
                    {
                        alienUnits++;
                    }
                }
            }

            ActiveAlienUnits = alienUnits;
            bool wasActive = IsAlienThreatActive;
            IsAlienThreatActive = alienKingdomFound || alienUnits >= 2;

            if (!wasActive && IsAlienThreatActive)
            {
                PauseNativeWarsForCoalition(world);
            }
        }

        private static void PauseNativeWarsForCoalition(MapBox world)
        {
            if (world?.wars == null) return;
            int truces = 0;

            try
            {
                var activeWars = world.wars.getActiveWars();
                if (activeWars != null)
                {
                    var snapshot = new List<War>(activeWars);
                    for (int i = 0; i < snapshot.Count; i++)
                    {
                        War w = snapshot[i];
                        if (w == null || !w.isAlive()) continue;
                        if (SpeciesPersonalityRegistry.IsNonCancelableWarType(w.getAsset())) continue;

                        Kingdom atk = w.getMainAttacker();
                        Kingdom def = w.getMainDefender();
                        if (atk != null && def != null && !IsAlienKingdom(atk) && !IsAlienKingdom(def))
                        {
                            world.wars.endWar(w, WarWinner.Peace);
                            truces++;
                        }
                    }
                }
            }
            catch { }

            string msg = truces > 0
                ? $"Anti-Alien Coalition formed! {truces} mortal war(s) paused to repel the xeno invasion."
                : "Anti-Alien Coalition formed! World empires rally (+85 opinion) against the xeno threat.";
            Debug.Log($"[ApexFactions] {msg}");
            try { WorldTip.showNowTop(msg, false); } catch { }
        }

        // Prevent AI kingdoms from declaring petty diplomatic wars on each other while an Alien threat is active
        [HarmonyPrefix]
        [HarmonyPatch(typeof(DiplomacyManager), "startWar")]
        private static bool CoalitionStartWarPrefix(Kingdom pAttacker, Kingdom pDefender, WarTypeAsset pAsset)
        {
            try
            {
                if (Main.Settings == null || !Main.Settings.EnableAlienCountermeasures || !IsAlienThreatActive) return true;
                if (pAttacker == null || pDefender == null) return true;
                if (SpeciesPersonalityRegistry.IsNonCancelableWarType(pAsset)) return true; // Never block rebellions, Spite, Inspire, or Whisper of War

                if (!IsAlienKingdom(pAttacker) && !IsAlienKingdom(pDefender))
                {
                    return false;
                }
            }
            catch { }
            return true;
        }

        // Reverse-engineer Alien alloys, fireproofing, and salvaged Alien Blasters when kingdoms slay Aliens/UFOs
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Actor), "newKillAction")]
        private static void OnKillAlienPostfix(Actor __instance, [HarmonyArgument(0)] Actor deadUnit)
        {
            try
            {
                if (Main.Settings == null || !Main.Settings.EnableAlienCountermeasures) return;
                if (__instance == null || !__instance.isAlive() || deadUnit == null) return;
                if (!IsAlienOrUfoActor(deadUnit)) return;

                Kingdom k = __instance.kingdom;
                if (k == null || !k.isAlive() || !k.isCiv() || IsAlienKingdom(k)) return;

                TotalAliensSlain++;
                AlienKillsByKingdom.TryGetValue(k.id, out int prevKills);
                int kills = prevKills + 1;
                AlienKillsByKingdom[k.id] = kills;

                // 1. Veteran battlefield tempering & plasma burn resistance for the slayer
                AddTraitSafe(__instance, "veteran");
                AddTraitSafe(__instance, "fire_proof");
                __instance.setStatsDirty();
                __instance.restoreHealthPercent(1f);
                __instance.setMaxHealth();
                __instance.setMaxStamina();

                // 2. Salvage alien hull alloys into the slayer's city or kingdom capital
                City salvageCity = (__instance.city != null && __instance.city.isAlive())
                    ? __instance.city
                    : (k.capital != null && k.capital.isAlive() ? k.capital : null);
                if (salvageCity != null)
                {
                    salvageCity.addResourcesToRandomStockpile("mythril", 3);
                    salvageCity.addResourcesToRandomStockpile("adamantine", 2);
                    salvageCity.addResourcesToRandomStockpile("common_metals", 5);
                }

                // 3. Reverse-engineer salvaged Alien Blaster after 3 confirmed xeno kills
                if (kills >= KillsRequiredForBlasterSalvage)
                {
                    bool equippedBlaster = TryEquipSalvagedBlaster(__instance);
                    if (!equippedBlaster)
                    {
                        // Literal animal slayers that cannot hold weapons gain xeno-adapted carapace instead
                        AddTraitSafe(__instance, "tough");
                        __instance.setStatsDirty();
                        __instance.setMaxHealth();
                    }

                    if (kills == KillsRequiredForBlasterSalvage)
                    {
                        string unlockMsg = $"{k.name} reverse-engineered xeno tech after {kills} Alien kills!";
                        Debug.Log($"[ApexFactions] {unlockMsg}");
                        try { WorldTip.showNowTop(unlockMsg, false); } catch { }
                    }
                }
            }
            catch { }
        }

        private static bool TryEquipSalvagedBlaster(Actor slayer)
        {
            var world = MapBox.instance;
            if (world?.items == null || slayer?.equipment == null || slayer.asset == null) return false;
            if (AssetManager.items == null || !AssetManager.items.has("alien_blaster")) return false;

            try
            {
                EquipmentAsset blasterAsset = AssetManager.items.get("alien_blaster");
                if (blasterAsset == null || !slayer.asset.canEditItem(blasterAsset)) return false;

                ActorEquipmentSlot slot = slayer.equipment.getSlot(EquipmentType.Weapon);
                if (slot != null && !slot.isEmpty() && slot.getItem()?.getAsset()?.id == "alien_blaster")
                {
                    return true;
                }

                Item blaster = world.items.newItem(blasterAsset);
                if (blaster != null)
                {
                    slayer.equipment.setItem(blaster, slayer);
                    TotalBlastersSalvaged++;
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static void AddTraitSafe(Actor a, string traitId)
        {
            if (a == null || string.IsNullOrEmpty(traitId) || AssetManager.traits == null) return;
            try
            {
                if (AssetManager.traits.has(traitId) && !a.hasTrait(traitId))
                {
                    a.addTrait(traitId, false);
                }
            }
            catch { }
        }

        public static bool IsAlienKingdom(Kingdom k)
        {
            if (k == null) return false;
            string sp = SpeciesPersonalityRegistry.GetKingdomSpecies(k);
            if (sp == "alien") return true;
            string raw = k.data?.original_actor_asset;
            return !string.IsNullOrEmpty(raw) && raw.IndexOf("alien", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool IsAlienOrUfoActor(Actor a)
        {
            if (a == null) return false;
            string id = a.asset?.id;
            if (!string.IsNullOrEmpty(id))
            {
                if (id == "alien" || id == "civ_alien" || id == "ufo") return true;
            }
            return a.kingdom != null && IsAlienKingdom(a.kingdom);
        }
    }
}
