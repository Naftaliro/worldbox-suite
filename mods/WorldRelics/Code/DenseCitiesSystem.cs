using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorldRelics
{
    /// <summary>
    /// Reversible Dense Cities (Condensed Buildings) mode.
    /// Tightens civilized building footprints, scales residential/hall housing slots,
    /// and reduces construction progress needed while caching vanilla values for clean restoration.
    /// </summary>
    public static class DenseCitiesSystem
    {
        private sealed class OriginalBuildingValues
        {
            public BuildingFundament Fundament;
            public int HousingSlots;
            public bool CanUnitsLiveHere;
            public int ConstructionProgressNeeded;
        }

        private static readonly Dictionary<string, OriginalBuildingValues> Originals =
            new Dictionary<string, OriginalBuildingValues>(StringComparer.Ordinal);

        private static int _lastLibraryCount = 0;

        public static bool IsApplied { get; private set; }
        public static int ModifiedCount { get; private set; }

        public static bool NeedsSync(bool desiredEnabled)
        {
            if (desiredEnabled != IsApplied) return true;
            if (desiredEnabled)
            {
                var list = AssetManager.buildings?.list;
                if (list != null && list.Count != _lastLibraryCount) return true;
            }
            return false;
        }

        public static bool Apply(bool enable)
        {
            BuildingLibrary lib = AssetManager.buildings;
            if (lib == null || lib.list == null || lib.list.Count == 0)
            {
                return false;
            }

            var assets = new List<BuildingAsset>(lib.list);
            _lastLibraryCount = assets.Count;

            if (enable)
            {
                int modified = 0;
                for (int i = 0; i < assets.Count; i++)
                {
                    BuildingAsset asset = assets[i];
                    if (!IsEligibleCityBuilding(asset)) continue;

                    if (!Originals.TryGetValue(asset.id, out OriginalBuildingValues orig))
                    {
                        orig = new OriginalBuildingValues
                        {
                            Fundament = asset.fundament != null
                                ? new BuildingFundament(
                                    asset.fundament.left,
                                    asset.fundament.right,
                                    asset.fundament.top,
                                    asset.fundament.bottom)
                                : null,
                            HousingSlots = asset.housing_slots,
                            CanUnitsLiveHere = asset.can_units_live_here,
                            ConstructionProgressNeeded = asset.construction_progress_needed
                        };
                        Originals[asset.id] = orig;
                    }

                    bool isHall = IsHallAsset(asset);
                    bool isResidentialHouse = orig.HousingSlots > 0 && !isHall;

                    // 1. Condense building footprint margins safely (all margins >= 0, valid width/height)
                    if (orig.Fundament != null)
                    {
                        int newL = orig.Fundament.left > 1
                            ? orig.Fundament.left - 1
                            : Math.Max(0, orig.Fundament.left);
                        int newR = orig.Fundament.right > 1
                            ? orig.Fundament.right - 1
                            : Math.Max(0, orig.Fundament.right);
                        int newT = isResidentialHouse
                            ? Math.Max(0, orig.Fundament.top - 1)
                            : (orig.Fundament.top > 1 ? orig.Fundament.top - 1 : Math.Max(0, orig.Fundament.top));
                        int newB = isResidentialHouse
                            ? Math.Max(0, orig.Fundament.bottom - 1)
                            : (orig.Fundament.bottom > 1 ? orig.Fundament.bottom - 1 : Math.Max(0, orig.Fundament.bottom));

                        asset.fundament = new BuildingFundament(newL, newR, newT, newB);
                    }

                    // 2. Boost residential and hall housing capacity for dense urban cores
                    if (orig.HousingSlots > 0)
                    {
                        int newSlots;
                        if (isHall || asset.upgrade_level >= 3)
                        {
                            newSlots = Math.Max(orig.HousingSlots + 4, Mathf.RoundToInt(orig.HousingSlots * 2.5f));
                        }
                        else if (asset.upgrade_level >= 1)
                        {
                            newSlots = Math.Max(orig.HousingSlots + 2, Mathf.RoundToInt(orig.HousingSlots * 1.9f));
                        }
                        else
                        {
                            newSlots = Math.Max(orig.HousingSlots + 1, Mathf.RoundToInt(orig.HousingSlots * 1.4f));
                        }

                        asset.setHousingSlots(newSlots);
                    }

                    // 3. Speed up complex building construction & upgrades (~30% faster, floor 10)
                    if (orig.ConstructionProgressNeeded > 0)
                    {
                        int minFloor = Math.Min(orig.ConstructionProgressNeeded, 10);
                        int reduced = Mathf.RoundToInt(orig.ConstructionProgressNeeded * 0.7f);
                        asset.construction_progress_needed = Math.Max(minFloor, reduced);
                    }

                    modified++;
                }

                bool changed = !IsApplied || ModifiedCount != modified;
                IsApplied = true;
                ModifiedCount = modified;
                if (changed)
                {
                    Debug.Log($"[WorldRelics] Dense Cities ON: condensed {modified} city building assets.");
                }
                return true;
            }
            else
            {
                int restored = 0;
                if (Originals.Count > 0)
                {
                    for (int i = 0; i < assets.Count; i++)
                    {
                        BuildingAsset asset = assets[i];
                        if (asset == null || string.IsNullOrEmpty(asset.id)) continue;
                        if (!Originals.TryGetValue(asset.id, out OriginalBuildingValues orig)) continue;

                        if (orig.Fundament != null)
                        {
                            asset.fundament = new BuildingFundament(
                                orig.Fundament.left,
                                orig.Fundament.right,
                                orig.Fundament.top,
                                orig.Fundament.bottom);
                        }

                        asset.housing_slots = orig.HousingSlots;
                        asset.can_units_live_here = orig.CanUnitsLiveHere;
                        asset.construction_progress_needed = orig.ConstructionProgressNeeded;
                        restored++;
                    }
                }

                bool wasApplied = IsApplied;
                IsApplied = false;
                ModifiedCount = 0;
                if (wasApplied)
                {
                    Debug.Log($"[WorldRelics] Dense Cities OFF: restored {restored} vanilla building assets.");
                }
                return true;
            }
        }

        public static void RestoreVanilla()
        {
            if (IsApplied || Originals.Count > 0)
            {
                Apply(false);
            }
        }

        private static bool IsEligibleCityBuilding(BuildingAsset asset)
        {
            if (asset == null || string.IsNullOrEmpty(asset.id)) return false;
            if (asset.id.StartsWith("$", StringComparison.Ordinal)) return false;
            if (!asset.city_building) return false;

            // Skip water/dock structures and special clearance utilities
            if (asset.docks || asset.can_be_placed_on_liquid || asset.is_stockpile) return false;

            string bType = asset.type ?? "";
            if (string.Equals(bType, "type_docks", StringComparison.OrdinalIgnoreCase)
                || string.Equals(bType, "type_bonfire", StringComparison.OrdinalIgnoreCase)
                || string.Equals(bType, "type_mine", StringComparison.OrdinalIgnoreCase)
                || string.Equals(bType, "type_stockpile", StringComparison.OrdinalIgnoreCase)
                || string.Equals(bType, "type_well", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string id = asset.id;
            if (id.IndexOf("dock", StringComparison.OrdinalIgnoreCase) >= 0
                || id.IndexOf("port", StringComparison.OrdinalIgnoreCase) >= 0
                || id.IndexOf("bridge", StringComparison.OrdinalIgnoreCase) >= 0
                || id.IndexOf("mine", StringComparison.OrdinalIgnoreCase) >= 0
                || id.IndexOf("bonfire", StringComparison.OrdinalIgnoreCase) >= 0
                || id.IndexOf("stockpile", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            return true;
        }

        private static bool IsHallAsset(BuildingAsset asset)
        {
            if (asset == null) return false;
            if (string.Equals(asset.type, "type_hall", StringComparison.OrdinalIgnoreCase)) return true;
            return !string.IsNullOrEmpty(asset.id)
                && asset.id.IndexOf("hall", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
