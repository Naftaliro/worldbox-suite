using System;
using System.Collections.Generic;
using System.IO;
using NeoModLoader.General;
using NeoModLoader.General.UI.Tab;
using NeoModLoader.utils;
using UnityEngine;

namespace SuiteHub
{
    public static class BottomBarIntegration
    {
        private static bool _initialized = false;

        public static void RegisterBottomTab(string modFolder)
        {
            if (_initialized) return;
            try
            {
                string resDir = Path.Combine(modFolder, "GameResources");
                Sprite tabIcon = LoadButtonSprite(Path.Combine(modFolder, "icon.png"));

                PowersTab tab = TabManager.CreateTab(
                    "worldbox_suite",
                    "tab_worldbox_suite",
                    "tab_worldbox_suite_desc",
                    tabIcon
                );

                if (tab == null)
                {
                    Debug.LogWarning("[SuiteHub] TabManager.CreateTab returned null; hotkeys F2-F5 remain active.");
                    return;
                }

                tab.SetLayout(new List<string> { "suite_controls", "suite_conflict", "suite_worlds", "suite_modules" });

                PowerButton hubBtn = PowerButtonCreator.CreateSimpleButton(
                    "suite_open_hub",
                    () => Main.Instance?.ToggleHub(),
                    LoadButtonSprite(Path.Combine(resDir, "btn_hub.png")) ?? tabIcon
                );
                tab.AddPowerButton("suite_controls", hubBtn);

                PowerButton scaleBtn = PowerButtonCreator.CreateSimpleButton(
                    "suite_core_scale",
                    () => Main.Instance?.ToggleCompanionHud("CoreScale.Main"),
                    LoadButtonSprite(Path.Combine(resDir, "btn_scale.png")) ?? tabIcon
                );
                tab.AddPowerButton("suite_controls", scaleBtn);

                PowerButton visBtn = PowerButtonCreator.CreateSimpleButton(
                    "suite_visual_polish",
                    () => Main.Instance?.ToggleCompanionHud("VisualPolish.Main"),
                    LoadButtonSprite(Path.Combine(resDir, "btn_visual.png")) ?? tabIcon
                );
                tab.AddPowerButton("suite_controls", visBtn);

                PowerButton warBtn = PowerButtonCreator.CreateSimpleButton(
                    "suite_war_matrix",
                    () => Main.Instance?.ToggleWarMatrix(),
                    LoadButtonSprite(Path.Combine(resDir, "btn_war_matrix.png")) ?? tabIcon
                );
                tab.AddPowerButton("suite_conflict", warBtn);

                PowerButton radarBtn = PowerButtonCreator.CreateSimpleButton(
                    "suite_battle_radar",
                    () => Main.Instance?.ToggleBattleRadar(),
                    LoadButtonSprite(Path.Combine(resDir, "btn_battle_radar.png")) ?? tabIcon
                );
                tab.AddPowerButton("suite_conflict", radarBtn);

                PowerButton gwBtn = PowerButtonCreator.CreateSimpleButton(
                    "suite_grand_warfare",
                    () => Main.Instance?.ToggleCompanionHud("GrandWarfare.Main"),
                    LoadButtonSprite(Path.Combine(resDir, "btn_war_matrix.png")) ?? tabIcon
                );
                tab.AddPowerButton("suite_conflict", gwBtn);

                PowerButton worldBtn = PowerButtonCreator.CreateSimpleButton(
                    "suite_world_builder",
                    () => Main.Instance?.ToggleWorldBuilder(),
                    LoadButtonSprite(Path.Combine(resDir, "btn_world_builder.png")) ?? tabIcon
                );
                tab.AddPowerButton("suite_worlds", worldBtn);

                PowerButton portalBtn = PowerButtonCreator.CreateSimpleButton(
                    "suite_linked_continents",
                    () => Main.Instance?.ToggleCompanionHud("LinkedContinents.Main"),
                    LoadButtonSprite(Path.Combine(resDir, "btn_portal.png")) ?? tabIcon
                );
                tab.AddPowerButton("suite_worlds", portalBtn);

                PowerButton apexBtn = PowerButtonCreator.CreateSimpleButton(
                    "suite_apex_factions",
                    () => Main.Instance?.ToggleCompanionHud("ApexFactions.Main"),
                    LoadButtonSprite(Path.Combine(resDir, "btn_apex.png")) ?? tabIcon
                );
                tab.AddPowerButton("suite_modules", apexBtn);

                PowerButton chrBtn = PowerButtonCreator.CreateSimpleButton(
                    "suite_era_chronicle",
                    () => Main.Instance?.ToggleCompanionHud("EraChronicle.Main"),
                    LoadButtonSprite(Path.Combine(resDir, "btn_chronicle.png")) ?? tabIcon
                );
                tab.AddPowerButton("suite_modules", chrBtn);

                PowerButton relicsBtn = PowerButtonCreator.CreateSimpleButton(
                    "suite_world_relics",
                    () => Main.Instance?.ToggleCompanionHud("WorldRelics.Main"),
                    LoadButtonSprite(Path.Combine(resDir, "btn_relics.png")) ?? tabIcon
                );
                tab.AddPowerButton("suite_modules", relicsBtn);

                PowerButton coopBtn = PowerButtonCreator.CreateSimpleButton(
                    "suite_coop_host",
                    () => Main.Instance?.ToggleCompanionHud("CoopHost.Main"),
                    LoadButtonSprite(Path.Combine(resDir, "btn_coop.png")) ?? tabIcon
                );
                tab.AddPowerButton("suite_modules", coopBtn);

                // Only add the ModernAge expansion button when the opt-in expansion is installed
                if (Main.IsCompanionLoaded("ModernAge.Main"))
                {
                    PowerButton modBtn = PowerButtonCreator.CreateSimpleButton(
                        "suite_modern_age",
                        () => Main.Instance?.ToggleCompanionHud("ModernAge.Main"),
                        LoadButtonSprite(Path.Combine(resDir, "btn_modern.png")) ?? tabIcon
                    );
                    tab.AddPowerButton("suite_modules", modBtn);
                }

                tab.UpdateLayout();
                _initialized = true;
                Debug.Log("[SuiteHub] Registered native WorldBox Suite bottom power-bar tab.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SuiteHub] Bottom-bar registration deferred or skipped: {e.Message}");
            }
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
