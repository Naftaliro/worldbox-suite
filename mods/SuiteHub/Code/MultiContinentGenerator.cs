using System;
using UnityEngine;

namespace SuiteHub
{
    public enum ContinentLayout
    {
        TwinContinentsEastWest = 0,
        FourQuadrants = 1,
        NorthSouthDivide = 2,
        SuperArchipelago = 3
    }

    public enum BiomeTheme
    {
        TemperateGrass = 0,
        LushJungle = 1,
        SunSavanna = 2,
        EnchantedForest = 3,
        FrozenPermafrost = 4,
        InfernalVolcanic = 5,
        MysticSwamp = 6
    }

    public static class MultiContinentGenerator
    {
        private static Rect _rect = new Rect(340, 96, 360, 415);

        private static readonly (string label, int wChunks, int hChunks)[] SizePresets =
        {
            ("Twin Standard (8x4 chunks)", 8, 4),
            ("Twin Wide (10x4 chunks)", 10, 4),
            ("Continental Wide (12x6 chunks)", 12, 6),
            ("Colossal Square (12x12 chunks)", 12, 12),
            ("Mega Twin (16x8 chunks)", 16, 8),
            ("Continental Square (16x16 chunks)", 16, 16),
            ("Ultra Twin (20x10 chunks)", 20, 10),
        };

        private static int _sizeIdx = 1;
        private static ContinentLayout _layout = ContinentLayout.TwinContinentsEastWest;
        private static BiomeTheme _biomeA = BiomeTheme.TemperateGrass;
        private static BiomeTheme _biomeB = BiomeTheme.LushJungle;
        private static float _channelWidthFrac = 0.16f;
        private static bool _steppingStoneIslands = true;
        private static bool _seedCivilizations = true;
        private static string _statusMsg = "Ready to sculpt a fresh multi-continent world.";

        public static void Draw(System.Action onClose)
        {
            _rect = GUI.Window(941211, _rect, id => DrawContent(onClose), "Multi-Continent World Builder (F2)", GameUiStyle.WindowStyle);
        }

        private static void DrawContent(System.Action onClose)
        {
            GameUiStyle.DrawCloseButton(_rect, onClose);
            var preset = SizePresets[_sizeIdx];
            GameUiStyle.Subtle("Sculpt a fresh pre-stitched multi-continent map right in-game.");
            GUILayout.Space(4);

            if (GameUiStyle.Button($"Map Size: {preset.label}", "World Dimensions|Click to cycle map presets from Twin Standard (8x4 chunks) up to Ultra Twin (20x10 chunks).|1 chunk = 64x64 tiles"))
            {
                _sizeIdx = (_sizeIdx + 1) % SizePresets.Length;
            }

            if (GameUiStyle.Button($"Layout: {_layout}", "Continent Layout|Choose how landmasses are divided: East/West twins, Four Quadrants, North/South divide, or Super Archipelago."))
            {
                int count = Enum.GetValues(typeof(ContinentLayout)).Length;
                _layout = (ContinentLayout)(((int)_layout + 1) % count);
            }

            if (GameUiStyle.Button($"Continent A Biome: {_biomeA}", "Primary Continent Biome|Click to cycle the dominant biome painted across Continent A (grass, jungle, savanna, enchanted, permafrost, infernal, swamp)."))
            {
                int count = Enum.GetValues(typeof(BiomeTheme)).Length;
                _biomeA = (BiomeTheme)(((int)_biomeA + 1) % count);
            }

            if (GameUiStyle.Button($"Continent B Biome: {_biomeB}", "Secondary Continent Biome|Click to cycle the dominant biome painted across Continent B for contrasting civilizations."))
            {
                int count = Enum.GetValues(typeof(BiomeTheme)).Length;
                _biomeB = (BiomeTheme)(((int)_biomeB + 1) % count);
            }

            GUILayout.Space(4);
            _channelWidthFrac = GameUiStyle.SliderRow(
                $"Ocean Divide Width: {(int)(_channelWidthFrac * 100)}%",
                _channelWidthFrac,
                0.06f,
                0.30f,
                "Ocean Channel Width|Controls how wide the sea separating the continents will be. Narrower channels encourage early sea bridges and raids.");

            if (GameUiStyle.ToggleButton(
                $"Stepping-Stone Islands: {(_steppingStoneIslands ? "ON" : "OFF")}",
                _steppingStoneIslands,
                "Stepping-Stone Archipelago|Scatters small contestable islands inside the central ocean divide as naval staging points."))
            {
                _steppingStoneIslands = !_steppingStoneIslands;
            }

            if (GameUiStyle.ToggleButton(
                $"Seed Starting Settlers: {(_seedCivilizations ? "ON" : "OFF")}",
                _seedCivilizations,
                "Seed Starting Settlers|Automatically drops 12 human settlers onto each continent so rival empires start building immediately."))
            {
                _seedCivilizations = !_seedCivilizations;
            }

            GUILayout.Space(8);
            if (GameUiStyle.Button("Generate Multi-Continent World Now", "Generate New World|Replaces the current map with a freshly generated multi-continent world using the settings above.|Save your current world first if you want to keep it!"))
            {
                GenerateNow();
            }

            GUILayout.Space(4);
            GameUiStyle.Label(_statusMsg, null, isHeader: true);
            GameUiStyle.CaptureTooltip();
            GUI.DragWindow();
        }

        public static string CyclePresetSummary()
        {
            int layoutCount = Enum.GetValues(typeof(ContinentLayout)).Length;
            _layout = (ContinentLayout)(((int)_layout + 1) % layoutCount);
            if (_layout == ContinentLayout.TwinContinentsEastWest)
            {
                _sizeIdx = (_sizeIdx + 1) % SizePresets.Length;
            }
            var preset = SizePresets[_sizeIdx];
            return $"World Builder: {_layout} · {preset.label}";
        }

        public static string CurrentSummary()
        {
            var preset = SizePresets[_sizeIdx];
            return $"{_layout} · {preset.label}";
        }

        public static void GenerateNow()
        {
            var world = MapBox.instance;
            if (world == null)
            {
                _statusMsg = "No active MapBox instance.";
                return;
            }

            var preset = SizePresets[_sizeIdx];
            try
            {
                world.clearWorld();
                world.setMapSize(preset.wChunks, preset.hChunks);

                int w = MapBox.width;
                int h = MapBox.height;
                float seedX = UnityEngine.Random.Range(10f, 999f);
                float seedY = UnityEngine.Random.Range(10f, 999f);
                float halfGap = _channelWidthFrac * 0.5f;

                WorldTile seedTileA = null;
                WorldTile seedTileB = null;

                for (int y = 0; y < h; y++)
                {
                    float ny = (float)y / h;
                    for (int x = 0; x < w; x++)
                    {
                        float nx = (float)x / w;
                        WorldTile tile = world.GetTile(x, y);
                        if (tile == null) continue;

                        float n1 = Mathf.PerlinNoise(seedX + nx * 5.2f, seedY + ny * 5.2f);
                        float n2 = Mathf.PerlinNoise(seedX * 1.7f + nx * 12.5f, seedY * 1.7f + ny * 12.5f) * 0.35f;
                        float elev = (n1 * 0.72f + n2);

                        // Outer ocean falloff
                        float edgeDist = Mathf.Min(Mathf.Min(nx, 1f - nx), Mathf.Min(ny, 1f - ny));
                        float edgeMask = Mathf.Clamp01(edgeDist / 0.08f);
                        elev *= (0.35f + 0.65f * edgeMask);

                        bool isContinentB = false;
                        float channelPenalty = 0f;

                        if (_layout == ContinentLayout.TwinContinentsEastWest)
                        {
                            isContinentB = nx > 0.5f;
                            float d = Mathf.Abs(nx - 0.5f);
                            if (d < halfGap) channelPenalty = 1f - (d / halfGap);
                        }
                        else if (_layout == ContinentLayout.NorthSouthDivide)
                        {
                            isContinentB = ny > 0.5f;
                            float d = Mathf.Abs(ny - 0.5f);
                            if (d < halfGap) channelPenalty = 1f - (d / halfGap);
                        }
                        else if (_layout == ContinentLayout.FourQuadrants)
                        {
                            isContinentB = (nx > 0.5f) ^ (ny > 0.5f);
                            float dx = Mathf.Abs(nx - 0.5f);
                            float dy = Mathf.Abs(ny - 0.5f);
                            float pX = dx < halfGap ? 1f - (dx / halfGap) : 0f;
                            float pY = dy < halfGap ? 1f - (dy / halfGap) : 0f;
                            channelPenalty = Mathf.Max(pX, pY);
                        }
                        else
                        {
                            // SuperArchipelago: raise frequency and keep more sea
                            isContinentB = nx > 0.5f;
                            elev *= 0.86f;
                        }

                        if (channelPenalty > 0f)
                        {
                            if (_steppingStoneIslands)
                            {
                                float isl = Mathf.PerlinNoise(seedX + 51f + nx * 16f, seedY + 29f + ny * 16f);
                                elev = elev * (1f - channelPenalty * 0.85f) + (isl > 0.72f ? (isl - 0.72f) * 2.1f : 0f);
                            }
                            else
                            {
                                elev *= (1f - channelPenalty * 0.92f);
                            }
                        }

                        BiomeTheme theme = isContinentB ? _biomeB : _biomeA;
                        PaintTileByElevation(tile, elev, theme);

                        if (elev > 0.54f && elev < 0.74f)
                        {
                            if (!isContinentB && seedTileA == null && nx > 0.16f && nx < 0.38f && ny > 0.35f && ny < 0.65f)
                                seedTileA = tile;
                            if (isContinentB && seedTileB == null && nx > 0.62f && nx < 0.84f && ny > 0.35f && ny < 0.65f)
                                seedTileB = tile;
                        }
                    }
                }

                world.allDirty();
                world.redrawTiles();

                if (_seedCivilizations && world.units != null)
                {
                    SpawnSettlers(world, seedTileA, "human", 12);
                    SpawnSettlers(world, seedTileB, "human", 12);
                }

                _statusMsg = $"Generated {preset.wChunks}x{preset.hChunks} ({w}x{h} tiles)!";
                Debug.Log($"[SuiteHub] {_statusMsg}");
            }
            catch (Exception e)
            {
                _statusMsg = "Gen error: " + e.Message;
                Debug.LogError($"[SuiteHub] Multi-continent generation failed: {e}");
            }
        }

        private static void SpawnSettlers(MapBox world, WorldTile center, string species, int count)
        {
            if (center == null) return;
            for (int i = 0; i < count; i++)
            {
                world.units.spawnNewUnit(species, center);
            }
        }

        private static void PaintTileByElevation(WorldTile tile, float elev, BiomeTheme theme)
        {
            if (elev < 0.30f)
            {
                tile.setTileType(TileLibrary.deep_ocean);
            }
            else if (elev < 0.39f)
            {
                tile.setTileType(TileLibrary.close_ocean);
            }
            else if (elev < 0.45f)
            {
                tile.setTileType(TileLibrary.shallow_waters);
            }
            else if (elev < 0.49f)
            {
                tile.setTileType(TileLibrary.sand);
            }
            else if (elev < 0.63f)
            {
                tile.setTileTypes(TileLibrary.soil_low, GetBiomeTop(theme, high: false));
            }
            else if (elev < 0.76f)
            {
                tile.setTileTypes(TileLibrary.soil_high, GetBiomeTop(theme, high: true));
            }
            else if (elev < 0.84f)
            {
                tile.setTileType(TileLibrary.hills);
            }
            else if (elev < 0.91f)
            {
                tile.setTileType(TileLibrary.mountains);
            }
            else
            {
                tile.setTileType(TileLibrary.summit);
            }
        }

        private static TopTileType GetBiomeTop(BiomeTheme theme, bool high)
        {
            switch (theme)
            {
                case BiomeTheme.LushJungle: return high ? TopTileLibrary.jungle_high : TopTileLibrary.jungle_low;
                case BiomeTheme.SunSavanna: return high ? TopTileLibrary.savanna_high : TopTileLibrary.savanna_low;
                case BiomeTheme.EnchantedForest: return high ? TopTileLibrary.enchanted_high : TopTileLibrary.enchanted_low;
                case BiomeTheme.FrozenPermafrost: return high ? TopTileLibrary.permafrost_high : TopTileLibrary.permafrost_low;
                case BiomeTheme.InfernalVolcanic: return high ? TopTileLibrary.infernal_high : TopTileLibrary.infernal_low;
                case BiomeTheme.MysticSwamp: return high ? TopTileLibrary.swamp_high : TopTileLibrary.swamp_low;
                default: return high ? TopTileLibrary.grass_high : TopTileLibrary.grass_low;
            }
        }
    }
}
