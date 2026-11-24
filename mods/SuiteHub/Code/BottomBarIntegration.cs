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

                tab.SetLayout(new List<string> { "suite_controls", "suite_conflict", "suite_worlds" });

                PowerButton hubBtn = PowerButtonCreator.CreateSimpleButton(
                    "suite_open_hub",
                    () => Main.Instance?.ToggleHub(),
                    LoadButtonSprite(Path.Combine(resDir, "btn_hub.png")) ?? tabIcon
                );
                tab.AddPowerButton("suite_controls", hubBtn);

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

                PowerButton worldBtn = PowerButtonCreator.CreateSimpleButton(
                    "suite_world_builder",
                    () => Main.Instance?.ToggleWorldBuilder(),
                    LoadButtonSprite(Path.Combine(resDir, "btn_world_builder.png")) ?? tabIcon
                );
                tab.AddPowerButton("suite_worlds", worldBtn);

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
