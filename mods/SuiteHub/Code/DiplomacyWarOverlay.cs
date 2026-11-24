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

        private static Rect _rect = new Rect(Screen.width > 420 ? Screen.width - 375 : 20, 76, 355, 390);
        private static Vector2 _scroll;
        private static int _subTab = 0; // 0 = Active Wars & Battles, 1 = Diplomacy Matrix

        public static void Draw(GUIStyle box, GUIStyle header, GUIStyle label, GUIStyle btn)
        {
            _rect = GUI.Window(941210, _rect, id => DrawContent(header, label, btn), "War & Diplomacy Monitor (F4)", box);
        }

        private static void DrawContent(GUIStyle header, GUIStyle label, GUIStyle btn)
        {
            var world = MapBox.instance;
            if (world == null)
            {
                GUILayout.Label("Load a world to view diplomacy.", label);
                GUI.DragWindow();
                return;
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_subTab == 0 ? "[Wars & Battles]" : "Wars & Battles", btn)) _subTab = 0;
            if (GUILayout.Button(_subTab == 1 ? "[Relations Matrix]" : "Relations Matrix", btn)) _subTab = 1;
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(305));

            if (_subTab == 0)
            {
                DrawWarsAndHotspots(world, header, label, btn);
            }
            else
            {
                DrawRelationsMatrix(world, header, label, btn);
            }

            GUILayout.EndScrollView();
            GUI.DragWindow();
        }

        private static void DrawWarsAndHotspots(MapBox world, GUIStyle header, GUIStyle label, GUIStyle btn)
        {
            var wars = new List<War>();
            if (world.wars != null)
            {
                foreach (var w in world.wars.getActiveWars())
                {
                    if (w != null) wars.Add(w);
                }
            }

            GUILayout.Label($"Active Wars ({wars.Count})", header);
            if (wars.Count == 0)
            {
                GUILayout.Label("The world is currently at peace.", label);
            }
            else
            {
                foreach (War w in wars)
                {
                    Kingdom atk = w.getMainAttacker();
                    Kingdom def = w.getMainDefender();
                    long dead = w.getDeadAttackers() + w.getDeadDefenders();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"{w.name} ({dead} dead)", label);
                    if (def?.capital?.getTile() != null && GUILayout.Button("Jump", btn, GUILayout.Width(52)))
                    {
                        JumpCameraToTile(def.capital.getTile());
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.Label($"   {atk?.name ?? "?"}  vs  {def?.name ?? "?"}", label);
                }
            }

            GUILayout.Space(8);
            GUILayout.Label($"Live Battle Hotspots ({BattleRadarSystem.Hotspots.Count})", header);
            if (BattleRadarSystem.Hotspots.Count == 0)
            {
                GUILayout.Label("No active skirmishes detected right now.", label);
            }
            else
            {
                for (int i = BattleRadarSystem.Hotspots.Count - 1; i >= 0; i--)
                {
                    var h = BattleRadarSystem.Hotspots[i];
                    if (h.Hits < 2) continue;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"{h.SideA} vs {h.SideB} ({h.Hits} hits)", label);
                    if (GUILayout.Button("Go", btn, GUILayout.Width(42)))
                    {
                        JumpCameraToCoords(h.X, h.Y);
                    }
                    GUILayout.EndHorizontal();
                }
            }
        }

        private static void DrawRelationsMatrix(MapBox world, GUIStyle header, GUIStyle label, GUIStyle btn)
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

            GUILayout.Label($"Civilized Kingdoms ({civs.Count})", header);
            if (civs.Count == 0)
            {
                GUILayout.Label("No active kingdoms yet.", label);
                return;
            }

            int limit = Mathf.Min(civs.Count, 8);
            for (int i = 0; i < limit; i++)
            {
                Kingdom k = civs[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{k.name} ({k.getPopulationPeople()} pop, {k.cities?.Count ?? 0} cities)", header);
                if (k.capital?.getTile() != null && GUILayout.Button("Find", btn, GUILayout.Width(46)))
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
                    GUILayout.Label($"   ↔ {other.name}: {status}", label);
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
