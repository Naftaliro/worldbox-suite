using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SuiteHub
{
    public static class DiplomacyWarOverlay
    {
        private static readonly FieldInfo Opinion1Field = AccessTools.Field(typeof(DiplomacyRelation), "opinion_k1");
        private static readonly FieldInfo Opinion2Field = AccessTools.Field(typeof(DiplomacyRelation), "opinion_k2");

        private static bool _rectInitialized = false;
        private static Rect _rect = new Rect(20, 76, 355, 390);
        private static Vector2 _scroll;
        private static int _subTab = 0; // 0 = Active Wars & Battles, 1 = Diplomacy Matrix

        public static void Draw(System.Action onClose)
        {
            if (!_rectInitialized)
            {
                _rectInitialized = true;
                _rect = new Rect(Screen.width > 420 ? Screen.width - 375 : 20, 76, 365, 405);
            }
            _rect = GUI.Window(941210, _rect, id => DrawContent(onClose), "War & Diplomacy Monitor (F4)", GameUiStyle.WindowStyle);
        }

        private static void DrawContent(System.Action onClose)
        {
            GameUiStyle.DrawCloseButton(_rect, onClose);
            var world = MapBox.instance;
            if (world == null)
            {
                GameUiStyle.Label("Load a world to view diplomacy.");
                GameUiStyle.CaptureTooltip();
                GUI.DragWindow();
                return;
            }

            GUILayout.BeginHorizontal();
            if (GameUiStyle.ToggleButton("Wars & Battles", _subTab == 0, "Wars & Live Battles|Show ongoing wars, total casualties, and active skirmish hotspots with 1-click camera jump buttons."))
                _subTab = 0;
            if (GameUiStyle.ToggleButton("Relations Matrix", _subTab == 1, "Kingdom Relations Matrix|Inspect pairwise opinion scores and war status between the top civilized kingdoms."))
                _subTab = 1;
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(315));

            if (_subTab == 0)
            {
                DrawWarsAndHotspots(world);
            }
            else
            {
                DrawRelationsMatrix(world);
            }

            GUILayout.EndScrollView();
            GameUiStyle.CaptureTooltip();
            GUI.DragWindow();
        }

        private static void DrawWarsAndHotspots(MapBox world)
        {
            var wars = new List<War>();
            if (world.wars != null)
            {
                foreach (var w in world.wars.getActiveWars())
                {
                    if (w != null) wars.Add(w);
                }
            }

            GameUiStyle.Label($"Active Wars ({wars.Count})", "Active Wars|All wars currently underway in this world, along with combined casualties.", isHeader: true);
            if (wars.Count == 0)
            {
                GameUiStyle.Subtle("The world is currently at peace.");
            }
            else
            {
                foreach (War w in wars)
                {
                    Kingdom atk = w.getMainAttacker();
                    Kingdom def = w.getMainDefender();
                    long dead = w.getDeadAttackers() + w.getDeadDefenders();

                    GUILayout.BeginHorizontal();
                    GameUiStyle.Label($"{w.name} ({dead} dead)", $"War Details|{atk?.name ?? "?"} vs {def?.name ?? "?"} — {dead} total casualties so far.");
                    if (def?.capital?.getTile() != null && GameUiStyle.Button("Jump", $"Jump to Capital|Pan the camera straight to {def.name}'s capital.", GUILayout.Width(54)))
                    {
                        JumpCameraToTile(def.capital.getTile());
                    }
                    GUILayout.EndHorizontal();
                    GameUiStyle.Subtle($"   {atk?.name ?? "?"}  vs  {def?.name ?? "?"}");
                }
            }

            GUILayout.Space(8);
            GameUiStyle.Label($"Live Battle Hotspots ({BattleRadarSystem.Hotspots.Count})", "Battle Hotspots|Recent clusters of combat hits detected by the Battlefield Conflict Radar.", isHeader: true);
            if (BattleRadarSystem.Hotspots.Count == 0)
            {
                GameUiStyle.Subtle("No active skirmishes detected right now.");
            }
            else
            {
                for (int i = BattleRadarSystem.Hotspots.Count - 1; i >= 0; i--)
                {
                    var h = BattleRadarSystem.Hotspots[i];
                    if (h.Hits < 2) continue;
                    GUILayout.BeginHorizontal();
                    GameUiStyle.Label($"{h.SideA} vs {h.SideB} ({h.Hits} hits)", $"Active Skirmish|{h.Hits} recent combat strikes between {h.SideA} and {h.SideB} at ({(int)h.X}, {(int)h.Y}).");
                    if (GameUiStyle.Button("Go", "Jump to Battle|Pan the camera directly to this active skirmish.", GUILayout.Width(44)))
                    {
                        JumpCameraToCoords(h.X, h.Y);
                    }
                    GUILayout.EndHorizontal();
                }
            }
        }

        private static void DrawRelationsMatrix(MapBox world)
        {
            var civs = new List<Kingdom>();
            if (world.kingdoms != null)
            {
                foreach (var k in world.kingdoms)
                {
                    if (k != null && k.isCiv() && k.isAlive()) civs.Add(k);
                }
            }
            civs.Sort((a, b) => b.getPopulationPeople().CompareTo(a.getPopulationPeople()));

            GameUiStyle.Label($"Civilized Kingdoms ({civs.Count})", "Top Kingdoms|Sorted by population. Hover or click Find to jump straight to a kingdom's capital.", isHeader: true);
            if (civs.Count == 0)
            {
                GameUiStyle.Subtle("No active kingdoms yet.");
                return;
            }

            int limit = Mathf.Min(civs.Count, 8);
            for (int i = 0; i < limit; i++)
            {
                Kingdom k = civs[i];
                GUILayout.BeginHorizontal();
                GameUiStyle.Label($"{k.name} ({k.getPopulationPeople()} pop, {k.cities?.Count ?? 0} cities)", $"Kingdom Overview|{k.name} controls {k.cities?.Count ?? 0} cities with {k.getPopulationPeople()} citizens.", isHeader: true);
                if (k.capital?.getTile() != null && GameUiStyle.Button("Find", $"Find {k.name}|Center the camera on {k.name}'s capital city.", GUILayout.Width(48)))
                {
                    JumpCameraToTile(k.capital.getTile());
                }
                GUILayout.EndHorizontal();

                for (int j = i + 1; j < limit; j++)
                {
                    Kingdom other = civs[j];
                    string status;
                    if (k.isInWarWith(other))
                    {
                        status = "AT WAR";
                    }
                    else
                    {
                        int op = 0;
                        if (world.diplomacy != null)
                        {
                            DiplomacyRelation rel = world.diplomacy.getRelation(k, other);
                            if (rel != null)
                            {
                                int o1 = Opinion1Field?.GetValue(rel) is int v1 ? v1 : 0;
                                int o2 = Opinion2Field?.GetValue(rel) is int v2 ? v2 : 0;
                                op = (o1 + o2) / 2;
                            }
                        }
                        status = op <= -30 ? $"Hostile ({op})" : (op >= 30 ? $"Friendly (+{op})" : $"Neutral ({op})");
                    }
                    GameUiStyle.Label($"   ↔ {other.name}: {status}", $"Diplomatic Standing|Average bilateral opinion between {k.name} and {other.name}: {status}.");
                }
                GUILayout.Space(4);
            }
        }

        public static void JumpCameraToTile(WorldTile tile)
        {
            if (tile == null) return;
            JumpCameraToCoords(tile.x, tile.y);
        }

        public static void JumpCameraToCoords(float x, float y)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 pos = cam.transform.position;
            cam.transform.position = new Vector3(x, y, pos.z);
        }
    }
}
