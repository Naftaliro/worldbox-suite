using System;
using System.Collections.Generic;
using System.IO;
using NeoModLoader.General;
using NeoModLoader.General.UI.Tab;
using NeoModLoader.utils;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;

namespace SuiteHub
{
    public static class BottomBarIntegration
    {
        private static bool _initialized = false;
        private static readonly Color OnTint  = new Color(0.75f, 1.00f, 0.78f, 1.00f);
        private static readonly Color OffTint = new Color(0.58f, 0.56f, 0.54f, 0.78f);

        public static void RegisterBottomTabs(string modFolder)
        {
            if (_initialized) return;
            try
            {
                EnsureLocalesLoaded(modFolder);

                string resDir = Path.Combine(modFolder, "GameResources");
                Sprite tabIcon      = LoadButtonSprite(Path.Combine(modFolder, "icon.png"));
                Sprite iconHub      = LoadButtonSprite(Path.Combine(resDir, "btn_hub.png")) ?? tabIcon;
                Sprite iconScale    = LoadButtonSprite(Path.Combine(resDir, "btn_scale.png")) ?? tabIcon;
                Sprite iconVisual   = LoadButtonSprite(Path.Combine(resDir, "btn_visual.png")) ?? tabIcon;
                Sprite iconWar      = LoadButtonSprite(Path.Combine(resDir, "btn_war_matrix.png")) ?? tabIcon;
                Sprite iconRadar    = LoadButtonSprite(Path.Combine(resDir, "btn_battle_radar.png")) ?? tabIcon;
                Sprite iconWorld    = LoadButtonSprite(Path.Combine(resDir, "btn_world_builder.png")) ?? tabIcon;
                Sprite iconPortal   = LoadButtonSprite(Path.Combine(resDir, "btn_portal.png")) ?? tabIcon;
                Sprite iconApex           = LoadButtonSprite(Path.Combine(resDir, "btn_apex.png")) ?? tabIcon;
                Sprite iconWardenDrop     = LoadButtonSprite(Path.Combine(resDir, "btn_warden_drop.png")) ?? iconApex;
                Sprite iconAnimalKingdoms = LoadButtonSprite(Path.Combine(resDir, "btn_animal_kingdoms.png")) ?? iconApex;
                Sprite iconSnakeMode      = LoadButtonSprite(Path.Combine(resDir, "btn_snake_mode.png")) ?? iconApex;
                Sprite iconAlwaysDay      = LoadButtonSprite(Path.Combine(resDir, "btn_always_day.png")) ?? iconVisual;
                Sprite iconAgeHope        = LoadButtonSprite(Path.Combine(resDir, "btn_age_hope.png")) ?? iconVisual;
                Sprite iconWarTicker      = LoadButtonSprite(Path.Combine(resDir, "btn_war_ticker.png")) ?? iconWar;
                Sprite iconSpeciesPanel   = LoadButtonSprite(Path.Combine(resDir, "btn_species_panel.png")) ?? iconApex;
                Sprite iconChron          = LoadButtonSprite(Path.Combine(resDir, "btn_chronicle.png")) ?? tabIcon;
                Sprite iconRelics         = LoadButtonSprite(Path.Combine(resDir, "btn_relics.png")) ?? tabIcon;
                Sprite iconDenseCities    = LoadButtonSprite(Path.Combine(resDir, "btn_dense_cities.png")) ?? iconRelics;
                Sprite iconPangaeaDrift   = LoadButtonSprite(Path.Combine(resDir, "btn_pangaea_drift.png")) ?? iconWorld;
                Sprite iconTectonicRift   = LoadButtonSprite(Path.Combine(resDir, "btn_tectonic_rift.png")) ?? iconWorld;
                Sprite iconTectonicSpeed  = LoadButtonSprite(Path.Combine(resDir, "btn_tectonic_speed.png")) ?? iconRelics;
                Sprite iconCoop           = LoadButtonSprite(Path.Combine(resDir, "btn_coop.png")) ?? tabIcon;
                Sprite iconModern         = LoadButtonSprite(Path.Combine(resDir, "btn_modern.png")) ?? tabIcon;

                bool hasModern = Main.IsCompanionLoaded("ModernAge.Main");

                // =========================================================================
                // TAB 1: SUITE POWERS (Direct click-on-map brushes & 1-click god actions!)
                // =========================================================================
                PowersTab powersTab = TabManager.CreateTab(
                    "worldbox_suite_powers",
                    "tab_suite_powers",
                    "tab_suite_powers_desc",
                    tabIcon
                );

                if (powersTab != null)
                {
                    var pGroups = new List<string> { "p_factions", "p_relics", "p_war", "p_worlds" };
                    if (hasModern) pGroups.Add("p_modern");
                    powersTab.SetLayout(pGroups);

                    // Group 1: Factions & Creatures (Real click-on-map drop brushes + instant powers!)
                    AddDropPower(powersTab, "p_factions", "suite_drop_wardens", iconWardenDrop, (tile, _) =>
                    {
                        if (tile == null || tile.Type == null || tile.Type.liquid)
                        {
                            Main.ShowToast("Click on solid land to drop a Sentinel Warden Colony.");
                            return false;
                        }
                        Main.ShowToast(Main.InvokeCompanionAction("ApexFactions.Main", "SpawnWardenAtTile", tile));
                        return true;
                    });

                    AddDropPower(powersTab, "p_factions", "suite_drop_wolfkin", iconApex, (tile, _) =>
                    {
                        if (tile == null || tile.Type == null || tile.Type.liquid)
                        {
                            Main.ShowToast("Click on solid land to drop Wolfkin settlers.");
                            return false;
                        }
                        Main.ShowToast(Main.InvokeCompanionAction("ApexFactions.Main", "SpawnWolfkinAtTile", tile));
                        return true;
                    });

                    AddAction(powersTab, "p_factions", "suite_anoint_wardens", iconApex,
                        () => Main.ShowToast(Main.InvokeCompanionAction("ApexFactions.Main", "AnointTopKingdom")));

                    AddAction(powersTab, "p_factions", "suite_awaken_wolves", iconApex,
                        () => Main.ShowToast(Main.InvokeCompanionAction("ApexFactions.Main", "AwakenWolves")));

                    AddAction(powersTab, "p_factions", "suite_awaken_animals", iconAnimalKingdoms,
                        () => Main.ShowToast(Main.InvokeCompanionAction("ApexFactions.Main", "AwakenAllAnimals")));

                    AddAction(powersTab, "p_factions", "suite_open_species", iconSpeciesPanel,
                        () => Main.Instance?.ToggleCompanionHud("ApexFactions.Main"));

                    // Group 2: Walls, Bridges & Ruins
                    AddAction(powersTab, "p_relics", "suite_fortify_now", iconRelics,
                        () => Main.ShowToast(Main.InvokeCompanionAction("WorldRelics.Main", "FortifyNow")));

                    AddAction(powersTab, "p_relics", "suite_bridges_now", iconRelics,
                        () => Main.ShowToast(Main.InvokeCompanionAction("WorldRelics.Main", "BuildBridgesNow")));

                    AddAction(powersTab, "p_relics", "suite_age_ruins", iconRelics,
                        () => Main.ShowToast(Main.InvokeCompanionAction("WorldRelics.Main", "AgeRuinsNow")));

                    // Group 3: Warfare & Armies
                    AddAction(powersTab, "p_war", "suite_open_war_monitor", iconWar,
                        () => Main.Instance?.ToggleWarMatrix());

                    AddAction(powersTab, "p_war", "suite_jump_battle", iconRadar,
                        () => Main.ShowToast(Main.JumpToActiveBattle()));

                    AddAction(powersTab, "p_war", "suite_form_pact", iconWar,
                        () => Main.ShowToast(Main.InvokeCompanionAction("GrandWarfare.Main", "FormTopDefensePact")));

                    AddAction(powersTab, "p_war", "suite_compress_armies", iconScale,
                        () => Main.ShowToast(Main.InvokeCompanionAction("CoreScale.Main", "CompressArmiesNow")));

                    // Group 4: Multi-Continent Builder, Portals & Chronicles
                    AddAction(powersTab, "p_worlds", "suite_cycle_continent", iconWorld,
                        () => Main.ShowToast(MultiContinentGenerator.CyclePresetSummary()));

                    AddAction(powersTab, "p_worlds", "suite_gen_continent", iconWorld, () =>
                    {
                        MultiContinentGenerator.GenerateNow();
                        Main.ShowToast("Generated " + MultiContinentGenerator.CurrentSummary());
                    });

                    AddAction(powersTab, "p_worlds", "suite_pangaea_drift", iconPangaeaDrift,
                        () => Main.ShowToast(Main.InvokeCompanionAction("WorldRelics.Main", "TriggerPangaeaDrift")));

                    AddAction(powersTab, "p_worlds", "suite_tectonic_rift", iconTectonicRift,
                        () => Main.ShowToast(Main.InvokeCompanionAction("WorldRelics.Main", "TriggerTectonicRift")));

                    AddAction(powersTab, "p_worlds", "suite_dispatch_fleet", iconPortal,
                        () => Main.ShowToast(Main.InvokeCompanionAction("LinkedContinents.Main", "SendExpeditionNow")));

                    AddAction(powersTab, "p_worlds", "suite_land_fleets", iconPortal,
                        () => Main.ShowToast(Main.InvokeCompanionAction("LinkedContinents.Main", "ReceiveFleetsNow")));

                    AddAction(powersTab, "p_worlds", "suite_record_chronicle", iconChron,
                        () => Main.ShowToast(Main.InvokeCompanionAction("EraChronicle.Main", "RecordSnapshotNow")));

                    // Group 5: ModernAge (only when installed)
                    if (hasModern)
                    {
                        AddAction(powersTab, "p_modern", "suite_modern_advance", iconModern,
                            () => Main.ShowToast(Main.InvokeCompanionAction("ModernAge.Main", "AdvanceTopKingdom")));

                        AddDropPower(powersTab, "p_modern", "suite_drop_armor", iconModern, (tile, _) =>
                        {
                            Main.ShowToast(Main.InvokeCompanionAction("ModernAge.Main", "DeployArmorAtTile", tile));
                            return true;
                        });

                        AddDropPower(powersTab, "p_modern", "suite_drop_air", iconModern, (tile, _) =>
                        {
                            Main.ShowToast(Main.InvokeCompanionAction("ModernAge.Main", "ScrambleAirAtTile", tile));
                            return true;
                        });
                    }

                    powersTab.UpdateLayout();
                }

                // =========================================================================
                // TAB 2: SUITE RULES & TOGGLES (Direct ON/OFF buttons, zero floating menus!)
                // =========================================================================
                PowersTab rulesTab = TabManager.CreateTab(
                    "worldbox_suite_rules",
                    "tab_suite_rules",
                    "tab_suite_rules_desc",
                    iconHub
                );

                if (rulesTab != null)
                {
                    var rGroups = new List<string> { "r_perf", "r_visual", "r_war", "r_factions", "r_world" };
                    if (hasModern) rGroups.Add("r_modern");
                    rulesTab.SetLayout(rGroups);

                    // Group 1: Performance (CoreScale)
                    AddToggle(rulesTab, "r_perf", "suite_tog_turbo", iconScale,
                        () => Main.GetCompanionState("CoreScale.Main", "turbo"),
                        () => Main.InvokeCompanionAction("CoreScale.Main", "ToggleTurboMode"));

                    AddToggle(rulesTab, "r_perf", "suite_tog_lod", iconScale,
                        () => Main.GetCompanionState("CoreScale.Main", "lod"),
                        () => Main.InvokeCompanionAction("CoreScale.Main", "ToggleAdaptiveLod"));

                    AddToggle(rulesTab, "r_perf", "suite_tog_anim", iconScale,
                        () => Main.GetCompanionState("CoreScale.Main", "anim_cull"),
                        () => Main.InvokeCompanionAction("CoreScale.Main", "ToggleAnimCull"));

                    AddToggle(rulesTab, "r_perf", "suite_tog_shadows", iconScale,
                        () => Main.GetCompanionState("CoreScale.Main", "no_shadows"),
                        () => Main.InvokeCompanionAction("CoreScale.Main", "ToggleShadows"));

                    AddToggle(rulesTab, "r_perf", "suite_tog_battalions", iconScale,
                        () => Main.GetCompanionState("CoreScale.Main", "battalions"),
                        () => Main.InvokeCompanionAction("CoreScale.Main", "ToggleAutoBattalions"));

                    // Group 2: Visuals (VisualPolish)
                    AddToggle(rulesTab, "r_visual", "suite_tog_always_day", iconAlwaysDay,
                        () => Main.GetCompanionState("VisualPolish.Main", "always_day"),
                        () => Main.InvokeCompanionAction("VisualPolish.Main", "ToggleAlwaysDay"));

                    AddToggle(rulesTab, "r_visual", "suite_tog_age_hope", iconAgeHope,
                        () => Main.GetCompanionState("VisualPolish.Main", "age_hope"),
                        () => Main.InvokeCompanionAction("VisualPolish.Main", "ToggleLockAgeOfHope"));

                    AddToggle(rulesTab, "r_visual", "suite_tog_preset", iconVisual,
                        () => Main.GetCompanionState("VisualPolish.Main", "preset"),
                        () => Main.InvokeCompanionAction("VisualPolish.Main", "CyclePreset"));

                    AddToggle(rulesTab, "r_visual", "suite_tog_daynight", iconVisual,
                        () => Main.GetCompanionState("VisualPolish.Main", "daynight"),
                        () => Main.InvokeCompanionAction("VisualPolish.Main", "ToggleDayNight"));

                    AddToggle(rulesTab, "r_visual", "suite_tog_vignette", iconVisual,
                        () => Main.GetCompanionState("VisualPolish.Main", "vignette"),
                        () => Main.InvokeCompanionAction("VisualPolish.Main", "ToggleVignette"));

                    AddToggle(rulesTab, "r_visual", "suite_tog_photo", iconVisual,
                        () => Main.GetCompanionState("VisualPolish.Main", "photo"),
                        () => Main.InvokeCompanionAction("VisualPolish.Main", "TogglePhotoMode"));

                    // Group 3: Warfare & Live War Ticker
                    AddToggle(rulesTab, "r_war", "suite_tog_war_ticker", iconWarTicker,
                        () => WarTickerOverlay.Visible,
                        () => Main.Instance?.ToggleWarTicker() ?? "");

                    AddToggle(rulesTab, "r_war", "suite_tog_naval", iconWar,
                        () => Main.GetCompanionState("GrandWarfare.Main", "naval"),
                        () => Main.InvokeCompanionAction("GrandWarfare.Main", "ToggleNavalCombat"));

                    AddToggle(rulesTab, "r_war", "suite_tog_coastal", iconWar,
                        () => Main.GetCompanionState("GrandWarfare.Main", "coastal"),
                        () => Main.InvokeCompanionAction("GrandWarfare.Main", "ToggleCoastalBombardment"));

                    AddToggle(rulesTab, "r_war", "suite_tog_blockades", iconWar,
                        () => Main.GetCompanionState("GrandWarfare.Main", "blockades"),
                        () => Main.InvokeCompanionAction("GrandWarfare.Main", "ToggleNavalBlockades"));

                    AddToggle(rulesTab, "r_war", "suite_tog_supply", iconWar,
                        () => Main.GetCompanionState("GrandWarfare.Main", "supply"),
                        () => Main.InvokeCompanionAction("GrandWarfare.Main", "ToggleSupplyAttrition"));

                    AddToggle(rulesTab, "r_war", "suite_tog_pacts", iconWar,
                        () => Main.GetCompanionState("GrandWarfare.Main", "pacts"),
                        () => Main.InvokeCompanionAction("GrandWarfare.Main", "ToggleDefensePacts"));

                    // Group 4: Factions & Species Rules (ApexFactions)
                    AddToggle(rulesTab, "r_factions", "suite_tog_auto_kingdoms", iconAnimalKingdoms,
                        () => Main.GetAnyCompanionState("ApexFactions.Main", "auto_kingdoms", "auto_animal_kingdoms", "animal_kingdoms"),
                        () => Main.InvokeFirstCompanionAction("ApexFactions.Main", new[] { "ToggleAutoAnimalKingdoms", "ToggleAutoKingdoms", "ToggleAnimalKingdoms" }));

                    AddToggle(rulesTab, "r_factions", "suite_tog_snake_mode", iconSnakeMode,
                        () => Main.GetAnyCompanionState("ApexFactions.Main", "snake_deceitful", "snake_mode", "snake", "snakes"),
                        () => Main.InvokeFirstCompanionAction("ApexFactions.Main", new[] { "ToggleSnakeMode", "ToggleSnakes" }));

                    // Group 5: World Systems
                    AddToggle(rulesTab, "r_world", "suite_tog_dense_cities", iconDenseCities,
                        () => Main.GetCompanionState("WorldRelics.Main", "dense_cities"),
                        () => Main.InvokeCompanionAction("WorldRelics.Main", "ToggleDenseCities"));

                    AddToggle(rulesTab, "r_world", "suite_tog_walls", iconRelics,
                        () => Main.GetCompanionState("WorldRelics.Main", "walls"),
                        () => Main.InvokeCompanionAction("WorldRelics.Main", "ToggleCityWalls"));

                    AddToggle(rulesTab, "r_world", "suite_tog_bridges", iconRelics,
                        () => Main.GetCompanionState("WorldRelics.Main", "bridges"),
                        () => Main.InvokeCompanionAction("WorldRelics.Main", "ToggleSeaBridges"));

                    AddToggle(rulesTab, "r_world", "suite_tog_ruins", iconRelics,
                        () => Main.GetCompanionState("WorldRelics.Main", "ruins"),
                        () => Main.InvokeCompanionAction("WorldRelics.Main", "ToggleRuinDecay"));

                    AddToggle(rulesTab, "r_world", "suite_tog_tectonic_speed", iconTectonicSpeed,
                        () => Main.GetCompanionState("WorldRelics.Main", "tectonic_slow"),
                        () => Main.InvokeCompanionAction("WorldRelics.Main", "ToggleTectonicSpeed"));

                    AddToggle(rulesTab, "r_world", "suite_tog_eras", iconChron,
                        () => Main.GetCompanionState("EraChronicle.Main", "eras"),
                        () => Main.InvokeCompanionAction("EraChronicle.Main", "ToggleEraProgression"));

                    AddToggle(rulesTab, "r_world", "suite_tog_snapshots", iconChron,
                        () => Main.GetCompanionState("EraChronicle.Main", "snapshots"),
                        () => Main.InvokeCompanionAction("EraChronicle.Main", "ToggleAutoSnapshots"));

                    AddToggle(rulesTab, "r_world", "suite_tog_portals", iconPortal,
                        () => Main.GetCompanionState("LinkedContinents.Main", "auto"),
                        () => Main.InvokeCompanionAction("LinkedContinents.Main", "ToggleAutoTransfers"));

                    AddToggle(rulesTab, "r_world", "suite_tog_coop", iconCoop,
                        () => Main.GetCompanionState("CoopHost.Main", "server"),
                        () => Main.InvokeCompanionAction("CoopHost.Main", "ToggleServer"));

                    if (hasModern)
                    {
                        AddToggle(rulesTab, "r_modern", "suite_tog_modern_auto", iconModern,
                            () => Main.GetCompanionState("ModernAge.Main", "auto"),
                            () => Main.InvokeCompanionAction("ModernAge.Main", "ToggleAutoProgression"));

                        AddToggle(rulesTab, "r_modern", "suite_tog_modern_radar", iconModern,
                            () => Main.GetCompanionState("ModernAge.Main", "radar"),
                            () => Main.InvokeCompanionAction("ModernAge.Main", "ToggleRadarRings"));
                    }

                    rulesTab.UpdateLayout();
                }

                _initialized = true;
                Debug.Log("[SuiteHub] Registered native WorldBox Suite: Powers and Suite: Rules bottom tabs.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SuiteHub] Bottom-bar registration deferred or skipped: {e.Message}");
            }
        }

        private static void AddDropPower(PowersTab tab, string group, string id, Sprite sprite, PowerActionWithID onClickTile)
        {
            if (AssetManager.powers != null && !AssetManager.powers.has(id))
            {
                AssetManager.powers.add(new GodPower
                {
                    id = id,
                    name = id,
                    force_brush = "circ_1",
                    hold_action = false,
                    show_tool_sizes = false,
                    click_action = onClickTile
                });
            }

            PowerButton btn = PowerButtonCreator.CreateGodPowerButton(id, sprite);
            if (btn != null)
            {
                WireNativeTooltip(btn, id);
                tab.AddPowerButton(group, btn);
            }
        }

        private static void AddAction(PowersTab tab, string group, string id, Sprite sprite, UnityEngine.Events.UnityAction onClick)
        {
            PowerButton btn = PowerButtonCreator.CreateSimpleButton(id, onClick, sprite);
            if (btn != null)
            {
                WireNativeTooltip(btn, id);
                tab.AddPowerButton(group, btn);
            }
        }

        private static void AddToggle(PowersTab tab, string group, string id, Sprite sprite, Func<bool> getState, Func<string> toggleAndMessage)
        {
            PowerButton btn = null;
            btn = PowerButtonCreator.CreateSimpleButton(id, () =>
            {
                string msg = toggleAndMessage?.Invoke() ?? "";
                ApplyToggleTint(btn, getState != null && getState());
                Main.ShowToast(msg);
            }, sprite);

            if (btn != null)
            {
                WireNativeTooltip(btn, id);
                ApplyToggleTint(btn, getState != null && getState());
                tab.AddPowerButton(group, btn);
            }
        }

        private static void ApplyToggleTint(PowerButton btn, bool isOn)
        {
            if (btn == null) return;
            try
            {
                if (btn.icon != null)
                {
                    btn.icon.color = isOn ? OnTint : OffTint;
                }
            }
            catch { }
        }

        private static void EnsureLocalesLoaded(string modFolder)
        {
            try
            {
                string enPath = Path.Combine(modFolder, "Locales", "en.json");
                if (!File.Exists(enPath)) return;
                var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(enPath));
                if (dict == null || dict.Count == 0) return;

                foreach (var kv in dict)
                {
                    if (string.IsNullOrEmpty(kv.Key)) continue;
                    LM.AddToCurrentLocale(kv.Key, kv.Value);
                }
                LM.ApplyLocale(false);
                bool ok = LocalizedTextManager.stringExists("suite_drop_wardens");
                string sample = ok ? LocalizedTextManager.getText("suite_drop_wardens") : "MISSING";
                Debug.Log($"[SuiteHub] Loaded {dict.Count} tooltip locale strings (verified={ok}, sample='{sample}').");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SuiteHub] Failed to apply locales: {e.Message}");
            }
        }

        private static void WireNativeTooltip(PowerButton btn, string id)
        {
            try
            {
                TipButton tip = btn.GetComponent<TipButton>();
                if (tip != null)
                {
                    tip.textOnClick = id;
                    tip.textOnClickDescription = id + "_description";
                    tip.text_description_2 = id + "_tip";
                    // Keep PowerButtonCreator's native tip.type ("tip" for simple/toggle buttons, "normal" for GodPower buttons)
                }
            }
            catch { }
        }

        private static Sprite LoadButtonSprite(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                return SpriteLoadUtils.LoadSingleSprite(path);
            }
            catch
            {
                return null;
            }
        }
    }
}
