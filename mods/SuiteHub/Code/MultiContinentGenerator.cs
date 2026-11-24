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

        public static void Draw(GUIStyle box, GUIStyle header, GUIStyle label, GUIStyle btn)
        {
            _rect = GUI.Window(941211, _rect, id => DrawContent(header, label, btn), "Multi-Continent World Builder (F2)", box);
        }

        private static void DrawContent(GUIStyle header, GUIStyle label, GUIStyle btn)
        {
            var preset = SizePresets[_sizeIdx];
            GUILayout.Label("Generate a pre-stitched multi-continent map right in-game.", label);
            GUILayout.Space(4);

            if (GUILayout.Button($"Map Size: {preset.label}", btn))
            {
                _sizeIdx = (_sizeIdx + 1) % SizePresets.Length;
            }

            if (GUILayout.Button($"Layout: {_layout}", btn))
            {
                int count = Enum.GetValues(typeof(ContinentLayout)).Length;
                _layout = (ContinentLayout)(((int)_layout + 1) % count);
            }

            if (GUILayout.Button($"Continent A Biome: {_biomeA}", btn))
            {
                int count = Enum.GetValues(typeof(BiomeTheme)).Length;
                _biomeA = (BiomeTheme)(((int)_biomeA + 1) % count);
            }

            if (GUILayout.Button($"Continent B Biome: {_biomeB}", btn))
            {
                int count = Enum.GetValues(typeof(BiomeTheme)).Length;
                _biomeB = (BiomeTheme)(((int)_biomeB + 1) % count);
            }

            GUILayout.Space(4);
            GUILayout.Label($"Ocean Divide Width: {(int)(_channelWidthFrac * 100)}%", label);
            _channelWidthFrac = GUILayout.HorizontalSlider(_channelWidthFrac, 0.06f, 0.30f);

            if (GUILayout.Button($"Stepping-Stone Islands: {(_steppingStoneIslands ? "ON" : "OFF")}", btn))
            {
                _steppingStoneIslands = !_steppingStoneIslands;
            }

            if (GUILayout.Button($"Seed Starting Settlers on Each Continent: {(_seedCivilizations ? "ON" : "OFF")}", btn))
            {
                _seedCivilizations = !_seedCivilizations;
            }

            GUILayout.Space(8);
            if (GUILayout.Button("Generate Multi-Continent World Now", btn))
            {
                GenerateNow();
            }

            GUILayout.Space(4);
            GUILayout.Label(_statusMsg, header);
            GUI.DragWindow();
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
