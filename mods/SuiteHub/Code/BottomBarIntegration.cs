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

                AddSuiteButton(tab, "suite_controls", "suite_open_hub",
                    () => Main.Instance?.ToggleHub(),
                    LoadButtonSprite(Path.Combine(resDir, "btn_hub.png")) ?? tabIcon);

                AddSuiteButton(tab, "suite_controls", "suite_core_scale",
                    () => Main.Instance?.ToggleCompanionHud("CoreScale.Main"),
                    LoadButtonSprite(Path.Combine(resDir, "btn_scale.png")) ?? tabIcon);

                AddSuiteButton(tab, "suite_controls", "suite_visual_polish",
                    () => Main.Instance?.ToggleCompanionHud("VisualPolish.Main"),
                    LoadButtonSprite(Path.Combine(resDir, "btn_visual.png")) ?? tabIcon);

                AddSuiteButton(tab, "suite_conflict", "suite_war_matrix",
                    () => Main.Instance?.ToggleWarMatrix(),
                    LoadButtonSprite(Path.Combine(resDir, "btn_war_matrix.png")) ?? tabIcon);

                AddSuiteButton(tab, "suite_conflict", "suite_battle_radar",
                    () => Main.Instance?.ToggleBattleRadar(),
                    LoadButtonSprite(Path.Combine(resDir, "btn_battle_radar.png")) ?? tabIcon);

                AddSuiteButton(tab, "suite_conflict", "suite_grand_warfare",
                    () => Main.Instance?.ToggleCompanionHud("GrandWarfare.Main"),
                    LoadButtonSprite(Path.Combine(resDir, "btn_war_matrix.png")) ?? tabIcon);

                AddSuiteButton(tab, "suite_worlds", "suite_world_builder",
                    () => Main.Instance?.ToggleWorldBuilder(),
                    LoadButtonSprite(Path.Combine(resDir, "btn_world_builder.png")) ?? tabIcon);

                AddSuiteButton(tab, "suite_worlds", "suite_linked_continents",
                    () => Main.Instance?.ToggleCompanionHud("LinkedContinents.Main"),
                    LoadButtonSprite(Path.Combine(resDir, "btn_portal.png")) ?? tabIcon);

                AddSuiteButton(tab, "suite_modules", "suite_apex_factions",
                    () => Main.Instance?.ToggleCompanionHud("ApexFactions.Main"),
                    LoadButtonSprite(Path.Combine(resDir, "btn_apex.png")) ?? tabIcon);

                AddSuiteButton(tab, "suite_modules", "suite_era_chronicle",
                    () => Main.Instance?.ToggleCompanionHud("EraChronicle.Main"),
                    LoadButtonSprite(Path.Combine(resDir, "btn_chronicle.png")) ?? tabIcon);

                AddSuiteButton(tab, "suite_modules", "suite_world_relics",
                    () => Main.Instance?.ToggleCompanionHud("WorldRelics.Main"),
                    LoadButtonSprite(Path.Combine(resDir, "btn_relics.png")) ?? tabIcon);

                AddSuiteButton(tab, "suite_modules", "suite_coop_host",
                    () => Main.Instance?.ToggleCompanionHud("CoopHost.Main"),
                    LoadButtonSprite(Path.Combine(resDir, "btn_coop.png")) ?? tabIcon);

                // Only add the ModernAge expansion button when the opt-in expansion is installed
                if (Main.IsCompanionLoaded("ModernAge.Main"))
                {
                    AddSuiteButton(tab, "suite_modules", "suite_modern_age",
                        () => Main.Instance?.ToggleCompanionHud("ModernAge.Main"),
                        LoadButtonSprite(Path.Combine(resDir, "btn_modern.png")) ?? tabIcon);
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

        private static void AddSuiteButton(PowersTab tab, string group, string id, UnityEngine.Events.UnityAction onClick, Sprite sprite)
        {
            PowerButton btn = PowerButtonCreator.CreateSimpleButton(id, onClick, sprite);
            if (btn != null)
            {
                WireNativeTooltip(btn, id);
                tab.AddPowerButton(group, btn);
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
                    tip.type = "normal";
                }
            }
            catch
            {
                // Ignore if TipButton layout differs
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
