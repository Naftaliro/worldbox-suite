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
                Sprite tabIcon            = LoadButtonSprite(Path.Combine(modFolder, "icon.png"));
                Sprite iconHub            = LoadButtonSprite(Path.Combine(resDir, "btn_hub.png")) ?? tabIcon;
                Sprite iconScale          = LoadButtonSprite(Path.Combine(resDir, "btn_scale.png")) ?? tabIcon;
                Sprite iconHyperSpeed     = LoadButtonSprite(Path.Combine(resDir, "btn_hyper_speed.png")) ?? iconScale;
                Sprite iconWar            = LoadButtonSprite(Path.Combine(resDir, "btn_war_matrix.png")) ?? tabIcon;
                Sprite iconWarfareSuite   = LoadButtonSprite(Path.Combine(resDir, "btn_warfare_suite.png")) ?? iconWar;
                Sprite iconArmoryBlessing = LoadButtonSprite(Path.Combine(resDir, "btn_armory_blessing.png")) ?? iconWar;
                Sprite iconAnointHero     = LoadButtonSprite(Path.Combine(resDir, "btn_anoint_hero.png")) ?? iconWar;
                Sprite iconBlessKing      = LoadButtonSprite(Path.Combine(resDir, "btn_bless_king.png")) ?? iconAnointHero;
                Sprite iconMercDrop       = LoadButtonSprite(Path.Combine(resDir, "btn_merc_drop.png")) ?? iconWar;
                Sprite iconRadar          = LoadButtonSprite(Path.Combine(resDir, "btn_battle_radar.png")) ?? tabIcon;
                Sprite iconWorld          = LoadButtonSprite(Path.Combine(resDir, "btn_world_builder.png")) ?? tabIcon;
                Sprite iconPortal         = LoadButtonSprite(Path.Combine(resDir, "btn_portal.png")) ?? tabIcon;
                Sprite iconApex           = LoadButtonSprite(Path.Combine(resDir, "btn_apex.png")) ?? tabIcon;
                Sprite iconWardenDrop     = LoadButtonSprite(Path.Combine(resDir, "btn_warden_drop.png")) ?? iconApex;
                Sprite iconAnimalKingdoms = LoadButtonSprite(Path.Combine(resDir, "btn_animal_kingdoms.png")) ?? iconApex;
                Sprite iconAlwaysDay      = LoadButtonSprite(Path.Combine(resDir, "btn_always_day.png")) ?? tabIcon;
                Sprite iconDistinctColors = LoadButtonSprite(Path.Combine(resDir, "btn_distinct_colors.png")) ?? iconAlwaysDay;
                Sprite iconHdZoom         = LoadButtonSprite(Path.Combine(resDir, "btn_hd_zoom.png")) ?? iconAlwaysDay;
                Sprite iconWarTicker      = LoadButtonSprite(Path.Combine(resDir, "btn_war_ticker.png")) ?? iconWar;
                Sprite iconDispatchSpy    = LoadButtonSprite(Path.Combine(resDir, "btn_dispatch_spy.png")) ?? iconWar;
                Sprite iconSpeciesPanel   = LoadButtonSprite(Path.Combine(resDir, "btn_species_panel.png")) ?? iconApex;
                Sprite iconChron          = LoadButtonSprite(Path.Combine(resDir, "btn_chronicle.png")) ?? tabIcon;
                Sprite iconRelics         = LoadButtonSprite(Path.Combine(resDir, "btn_relics.png")) ?? tabIcon;
                Sprite iconHighways       = LoadButtonSprite(Path.Combine(resDir, "btn_highways_citadels.png")) ?? iconRelics;
                Sprite iconDenseCities    = LoadButtonSprite(Path.Combine(resDir, "btn_dense_cities.png")) ?? iconRelics;
                Sprite iconPangaeaDrift   = LoadButtonSprite(Path.Combine(resDir, "btn_pangaea_drift.png")) ?? iconWorld;
                Sprite iconTectonicRift   = LoadButtonSprite(Path.Combine(resDir, "btn_tectonic_rift.png")) ?? iconWorld;
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
                    var pGroups = new List<string> { "p_factions", "p_war", "p_relics", "p_worlds" };
                    if (hasModern) pGroups.Add("p_modern");
                    powersTab.SetLayout(pGroups);

                    // Group 1: Factions & Creatures
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

                    AddAction(powersTab, "p_factions", "suite_open_species", iconSpeciesPanel,
                        () => Main.Instance?.ToggleCompanionHud("ApexFactions.Main"));

                    AddAction(powersTab, "p_factions", "suite_awaken_animals", iconAnimalKingdoms,
                        () => Main.ShowToast(Main.InvokeCompanionAction("ApexFactions.Main", "AwakenAllAnimals")));

                    // Group 2: Warfare, Heroes & Mercenaries
                    AddAction(powersTab, "p_war", "suite_open_war_monitor", iconWar,
                        () => Main.Instance?.ToggleWarMatrix());

                    AddDropPower(powersTab, "p_war", "suite_armory_blessing", iconArmoryBlessing, (tile, _) =>
                    {
                        if (tile == null)
                        {
                            Main.ShowToast("Click on a city or army to bestow an Armory Blessing.");
                            return false;
                        }
                        Main.ShowToast(Main.InvokeCompanionAction("GrandWarfare.Main", "ArmoryBlessingAtTile", tile));
                        return true;
                    });

                    AddDropPower(powersTab, "p_war", "suite_anoint_hero", iconAnointHero, (tile, _) =>
                    {
                        if (tile == null)
                        {
                            Main.ShowToast("Click near a warrior or city to anoint a War Hero.");
                            return false;
                        }
                        Main.ShowToast(Main.InvokeCompanionAction("GrandWarfare.Main", "AnointWarHeroAtTile", tile));
                        return true;
                    });

                    AddDropPower(powersTab, "p_war", "suite_bless_king", iconBlessKing, (tile, _) =>
                    {
                        if (tile == null)
                        {
                            Main.ShowToast("Click inside a kingdom to bestow a Royal Dynasty Blessing.");
                            return false;
                        }
                        Main.ShowToast(Main.InvokeCompanionAction("GrandWarfare.Main", "BlessKingAndDynastyAtTile", tile));
                        return true;
                    });

                    AddDropPower(powersTab, "p_war", "suite_merc_drop", iconMercDrop, (tile, _) =>
                    {
                        if (tile == null || tile.Type == null || tile.Type.liquid)
                        {
                            Main.ShowToast("Click on solid land to deploy a Mercenary Company.");
                            return false;
                        }
                        Main.ShowToast(Main.InvokeCompanionAction("GrandWarfare.Main", "DeployMercenariesAtTile", tile));
                        return true;
                    });

                    AddDropPower(powersTab, "p_war", "suite_dispatch_spy", iconDispatchSpy, (tile, _) =>
                    {
                        if (tile == null)
                        {
                            Main.ShowToast("1st click: Spy Origin Kingdom · 2nd click: Target Kingdom to infiltrate.");
                            return false;
                        }
                        Main.ShowToast(Main.InvokeCompanionAction("GrandWarfare.Main", "SelectSpyKingdomAtTile", tile));
                        return true;
                    });

                    AddAction(powersTab, "p_war", "suite_jump_battle", iconRadar,
                        () => Main.ShowToast(Main.JumpToActiveBattle()));

                    // Group 3: Highways, Citadels, Walls, Bridges & Ruins
                    AddAction(powersTab, "p_relics", "suite_build_infra", iconHighways,
                        () => Main.ShowToast(Main.InvokeCompanionAction("WorldRelics.Main", "BuildInfrastructureNow")));

                    AddAction(powersTab, "p_relics", "suite_fortify_now", iconRelics,
                        () => Main.ShowToast(Main.InvokeCompanionAction("WorldRelics.Main", "FortifyNow")));

                    AddAction(powersTab, "p_relics", "suite_bridges_now", iconRelics,
                        () => Main.ShowToast(Main.InvokeCompanionAction("WorldRelics.Main", "BuildBridgesNow")));

                    AddAction(powersTab, "p_relics", "suite_age_ruins", iconRelics,
                        () => Main.ShowToast(Main.InvokeCompanionAction("WorldRelics.Main", "AgeRuinsNow")));

                    // Group 4: Tectonics, Multi-Continent Builder, Portals & Chronicles
                    AddAction(powersTab, "p_worlds", "suite_pangaea_drift", iconPangaeaDrift,
                        () => Main.ShowToast(Main.InvokeCompanionAction("WorldRelics.Main", "TriggerPangaeaDrift")));

                    AddAction(powersTab, "p_worlds", "suite_tectonic_rift", iconTectonicRift,
                        () => Main.ShowToast(Main.InvokeCompanionAction("WorldRelics.Main", "TriggerTectonicRift")));

                    AddAction(powersTab, "p_worlds", "suite_gen_continent", iconWorld, () =>
                    {
                        MultiContinentGenerator.GenerateNow();
                        Main.ShowToast("Generated " + MultiContinentGenerator.CurrentSummary());
                    });

                    AddAction(powersTab, "p_worlds", "suite_dispatch_fleet", iconPortal,
                        () => Main.ShowToast(Main.InvokeCompanionAction("LinkedContinents.Main", "SendExpeditionNow")));

                    AddAction(powersTab, "p_worlds", "suite_record_chronicle", iconChron,
                        () => Main.ShowToast(Main.InvokeCompanionAction("EraChronicle.Main", "RecordSnapshotNow")));

                    AddAction(powersTab, "p_worlds", "suite_cycle_speed", iconHyperSpeed,
                        () => Main.ShowToast(Main.InvokeCompanionAction("CoreScale.Main", "CycleHyperSpeed")));

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
                // TAB 2: SUITE RULES & TOGGLES (8 crisp master buttons, zero clutter!)
                // =========================================================================
                PowersTab rulesTab = TabManager.CreateTab(
                    "worldbox_suite_rules",
                    "tab_suite_rules",
                    "tab_suite_rules_desc",
                    iconHub
                );

                if (rulesTab != null)
                {
                    var rGroups = new List<string> { "r_core", "r_world" };
                    if (hasModern) rGroups.Add("r_modern");
                    rulesTab.SetLayout(rGroups);

                    // Group 1: Core Performance, Visuals & Live War Ticker
                    AddToggle(rulesTab, "r_core", "suite_tog_turbo", iconScale,
                        () => Main.GetCompanionState("CoreScale.Main", "turbo"),
                        () => Main.InvokeCompanionAction("CoreScale.Main", "ToggleTurboMode"));

                    AddToggle(rulesTab, "r_core", "suite_tog_always_day", iconAlwaysDay,
                        () => Main.GetCompanionState("VisualPolish.Main", "always_day"),
                        () => Main.InvokeCompanionAction("VisualPolish.Main", "ToggleAlwaysDay"));

                    AddToggle(rulesTab, "r_core", "suite_tog_borders", iconDistinctColors,
                        () => Main.GetCompanionState("VisualPolish.Main", "colors_borders"),
                        () => Main.InvokeCompanionAction("VisualPolish.Main", "ToggleColorsAndBorders"));

                    AddToggle(rulesTab, "r_core", "suite_tog_hd_zoom", iconHdZoom,
                        () => Main.GetCompanionState("VisualPolish.Main", "hd_zoom"),
                        () => Main.InvokeCompanionAction("VisualPolish.Main", "ToggleExtendedHdZoom"));

                    AddToggle(rulesTab, "r_core", "suite_tog_war_ticker", iconWarTicker,
                        () => WarTickerOverlay.Visible,
                        () => Main.Instance?.ToggleWarTicker() ?? "");

                    // Group 2: World Simulation Bundles & Co-Op
                    AddToggle(rulesTab, "r_world", "suite_tog_warfare_suite", iconWarfareSuite,
                        () => Main.GetCompanionState("GrandWarfare.Main", "warfare_suite"),
                        () => Main.InvokeCompanionAction("GrandWarfare.Main", "ToggleWarfareAndIntelBundle"));

                    AddToggle(rulesTab, "r_world", "suite_tog_auto_kingdoms", iconAnimalKingdoms,
                        () => Main.GetAnyCompanionState("ApexFactions.Main", "auto_kingdoms", "auto_animal_kingdoms", "animal_kingdoms"),
                        () => Main.InvokeFirstCompanionAction("ApexFactions.Main", new[] { "ToggleAutoAnimalKingdoms", "ToggleAutoKingdoms", "ToggleAnimalKingdoms" }));

                    AddToggle(rulesTab, "r_world", "suite_tog_dense_cities", iconDenseCities,
                        () => Main.GetCompanionState("WorldRelics.Main", "relics_cities"),
                        () => Main.InvokeCompanionAction("WorldRelics.Main", "ToggleRelicsAndCitiesBundle"));

                    AddToggle(rulesTab, "r_world", "suite_tog_coop", iconCoop,
                        () => Main.GetCompanionState("CoopHost.Main", "server"),
                        () => Main.InvokeCompanionAction("CoopHost.Main", "ToggleServer"));

                    if (hasModern)
                    {
                        AddToggle(rulesTab, "r_modern", "suite_tog_modern_auto", iconModern,
                            () => Main.GetCompanionState("ModernAge.Main", "auto"),
                            () => Main.InvokeCompanionAction("ModernAge.Main", "ToggleAutoProgression"));
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
