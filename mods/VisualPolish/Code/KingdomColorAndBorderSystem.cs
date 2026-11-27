using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace VisualPolish
{
    public static class KingdomColorAndBorderSystem
    {
        private const float MinDistinctRgbDistance = 95f;
        private const float RecolorCheckInterval = 6.0f;
        private const float EnhancedBorderBrightness = 1.65f;
        private const float VanillaBorderBrightness = 1.0f;
        private const float EnhancedMinimapOpacityBoost = 0.12f;

        private static readonly FieldInfo ZoneCalculatorField =
            AccessTools.Field(typeof(MapBox), "zone_calculator");

        private static readonly MethodInfo ZoneSetDirtyMethod =
            AccessTools.Method(typeof(ZoneCalculator), "setDirty");

        private static ZoneCalculator GetZoneCalculator(MapBox world)
        {
            if (world == null || ZoneCalculatorField == null) return null;
            return ZoneCalculatorField.GetValue(world) as ZoneCalculator;
        }

        private static ZoneCalculator _trackedZoneCalculator;
        private static float _vanillaMinimapOpacity = 0.78f;
        private static float _checkTimer = 0f;
        private static bool _appliedInitialBorders = false;

        public static void Tick(float dt)
        {
            try
            {
                var settings = Main.Settings;
                var world = MapBox.instance;
                if (settings == null || world == null) return;

                ZoneCalculator zc = GetZoneCalculator(world);
                if (zc != null &&
                    (!ReferenceEquals(zc, _trackedZoneCalculator) || !_appliedInitialBorders))
                {
                    CaptureVanillaDefaults(zc);
                    ApplyBorderSettings(zc, dirtyRedraw: false);
                    _appliedInitialBorders = true;
                }

                if (!settings.AutoDistinctKingdomColors)
                {
                    _checkTimer = 0f;
                    return;
                }

                _checkTimer += dt;
                if (_checkTimer >= RecolorCheckInterval)
                {
                    _checkTimer = 0f;
                    EnsureDistinctKingdomColors();
                }
            }
            catch { }
        }

        public static int ApplyNow(bool runColorPass)
        {
            int recolored = 0;
            try
            {
                var world = MapBox.instance;
                ZoneCalculator zc = GetZoneCalculator(world);
                if (zc != null)
                {
                    CaptureVanillaDefaults(zc);
                    ApplyBorderSettings(zc, dirtyRedraw: true);
                    _appliedInitialBorders = true;
                }

                if (runColorPass && Main.Settings != null && Main.Settings.AutoDistinctKingdomColors)
                {
                    _checkTimer = 0f;
                    recolored = EnsureDistinctKingdomColors();
                }
            }
            catch { }
            return recolored;
        }

        public static void OnZoneCalculatorCreated(ZoneCalculator zc)
        {
            if (zc == null) return;
            try
            {
                CaptureVanillaDefaults(zc);
                ApplyBorderSettings(zc, dirtyRedraw: false);
                _appliedInitialBorders = true;
            }
            catch { }
        }

        private static void CaptureVanillaDefaults(ZoneCalculator zc)
        {
            if (zc == null) return;
            if (ReferenceEquals(zc, _trackedZoneCalculator)) return;

            _trackedZoneCalculator = zc;
            _vanillaMinimapOpacity = zc.minimap_opacity > 0.05f ? zc.minimap_opacity : 0.78f;
        }

        public static void ApplyBorderSettings(ZoneCalculator zc, bool dirtyRedraw)
        {
            if (zc == null || Main.Settings == null) return;
            try
            {
                CaptureVanillaDefaults(zc);
                bool enhanced = Main.Settings.EnhancedBorders;
                zc.border_brightness = enhanced ? EnhancedBorderBrightness : VanillaBorderBrightness;
                zc.minimap_opacity = enhanced
                    ? Mathf.Clamp(_vanillaMinimapOpacity + EnhancedMinimapOpacityBoost, 0.72f, 0.92f)
                    : _vanillaMinimapOpacity;

                if (dirtyRedraw)
                {
                    MarkZonesDirty(zc);
                }
            }
            catch { }
        }

        private static void MarkZonesDirty(ZoneCalculator zc)
        {
            if (zc == null) return;
            try
            {
                zc.dirtyAndClear();
                ZoneSetDirtyMethod?.Invoke(zc, null);
            }
            catch { }
        }

        public static int EnsureDistinctKingdomColors()
        {
            try
            {
                var world = MapBox.instance;
                if (world?.kingdoms == null || AssetManager.kingdom_colors_library?.list == null)
                    return 0;

                var palette = AssetManager.kingdom_colors_library.list;
                if (palette.Count < 2) return 0;

                var active = new List<Kingdom>();
                var snapshot = new List<Kingdom>(world.kingdoms);
                for (int i = 0; i < snapshot.Count; i++)
                {
                    Kingdom k = snapshot[i];
                    if (k != null && k.isAlive() && k.isCiv())
                    {
                        active.Add(k);
                    }
                }

                if (active.Count < 2) return 0;

                // Preserve larger and older kingdoms; recolor smaller or newer ones on collision
                active.Sort((a, b) =>
                {
                    int cmp = b.countCities().CompareTo(a.countCities());
                    if (cmp != 0) return cmp;
                    cmp = b.getPopulationPeople().CompareTo(a.getPopulationPeople());
                    if (cmp != 0) return cmp;
                    return a.id.CompareTo(b.id);
                });

                int count = active.Count;
                var colors = new Color32[count];
                var assets = new ColorAsset[count];
                for (int i = 0; i < count; i++)
                {
                    assets[i] = active[i].getColor();
                    colors[i] = GetMainRgb(assets[i]);
                }

                int recolored = 0;
                for (int i = 1; i < count; i++)
                {
                    float minDistEarlier = float.MaxValue;
                    for (int j = 0; j < i; j++)
                    {
                        float d = RgbDistance(colors[i], colors[j]);
                        if (d < minDistEarlier) minDistEarlier = d;
                    }

                    // Already visually distinct from all larger/older kingdoms
                    if (minDistEarlier >= MinDistinctRgbDistance) continue;

                    ColorAsset currentAsset = assets[i];
                    ColorAsset bestAsset = null;
                    Color32 bestRgb = default;
                    float bestMinDist = minDistEarlier;

                    for (int p = 0; p < palette.Count; p++)
                    {
                        ColorAsset cand = palette[p];
                        if (cand == null) continue;
                        if (ReferenceEquals(cand, currentAsset)) continue;
                        if (currentAsset != null && !string.IsNullOrEmpty(cand.id) && cand.id == currentAsset.id) continue;

                        Color32 candRgb = GetMainRgb(cand);
                        float candMinDist = float.MaxValue;
                        for (int m = 0; m < count; m++)
                        {
                            if (m == i) continue;
                            float d = RgbDistance(candRgb, colors[m]);
                            if (d < candMinDist) candMinDist = d;
                        }

                        if (candMinDist > bestMinDist)
                        {
                            bestMinDist = candMinDist;
                            bestAsset = cand;
                            bestRgb = candRgb;
                        }
                    }

                    if (bestAsset != null && bestMinDist > minDistEarlier + 1f)
                    {
                        if (active[i].updateColor(bestAsset))
                        {
                            assets[i] = bestAsset;
                            colors[i] = bestRgb;
                            recolored++;
                        }
                    }
                }

                if (recolored > 0)
                {
                    ZoneCalculator zc = GetZoneCalculator(world);
                    if (zc != null)
                    {
                        MarkZonesDirty(zc);
                    }
                }

                return recolored;
            }
            catch
            {
                return 0;
            }
        }

        private static Color32 GetMainRgb(ColorAsset asset)
        {
            if (asset == null) return new Color32(128, 128, 128, 255);
            asset.initColor();
            Color32 main = asset.getColorMain32();
            if (main.r == 0 && main.g == 0 && main.b == 0 && main.a == 0)
            {
                return asset.k_color_0;
            }
            return main;
        }

        private static float RgbDistance(Color32 a, Color32 b)
        {
            float dr = a.r - b.r;
            float dg = a.g - b.g;
            float db = a.b - b.b;
            return Mathf.Sqrt(dr * dr + dg * dg + db * db);
        }
    }

    [HarmonyPatch(typeof(ZoneCalculator))]
    public static class ZoneCalculatorBorderPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch("create")]
        private static void CreatePostfix(ZoneCalculator __instance)
        {
            KingdomColorAndBorderSystem.OnZoneCalculatorCreated(__instance);
        }
    }
}
