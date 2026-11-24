using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SuiteHub
{
    public class BattleHotspot
    {
        public float X;
        public float Y;
        public int Hits;
        public float LastUpdateTime;
        public string SideA;
        public string SideB;
    }

    [HarmonyPatch(typeof(Actor))]
    public static class BattleRadarSystem
    {
        public static bool Enabled { get; set; } = true;
        public static readonly List<BattleHotspot> Hotspots = new List<BattleHotspot>();

        private const float ClusterRadius = 18f;
        private const float HotspotLifetime = 16f;

        private static Texture2D _ringTex;
        private static GUIStyle _tagStyle;

        // Track live combat damage between civilized factions
        [HarmonyPostfix]
        [HarmonyPatch("getHit")]
        private static void ActorGetHitPostfix(Actor __instance, BaseSimObject pAttacker)
        {
            if (!Enabled || __instance == null || __instance.current_tile == null) return;
            if (pAttacker == null || __instance.kingdom == null || pAttacker.kingdom == null) return;
            if (__instance.kingdom == pAttacker.kingdom) return;
            if (!__instance.kingdom.isCiv() && !pAttacker.kingdom.isCiv()) return;

            RecordHit(
                __instance.current_tile.x,
                __instance.current_tile.y,
                pAttacker.kingdom.name ?? "Attackers",
                __instance.kingdom.name ?? "Defenders"
            );
        }

        public static void RecordHit(float x, float y, string sideA, string sideB)
        {
            float now = Time.unscaledTime;
            float rSq = ClusterRadius * ClusterRadius;

            for (int i = 0; i < Hotspots.Count; i++)
            {
                BattleHotspot h = Hotspots[i];
                float dx = h.X - x;
                float dy = h.Y - y;
                if (dx * dx + dy * dy <= rSq)
                {
                    h.X = Mathf.Lerp(h.X, x, 0.25f);
                    h.Y = Mathf.Lerp(h.Y, y, 0.25f);
                    h.Hits++;
                    h.LastUpdateTime = now;
                    return;
                }
            }

            if (Hotspots.Count >= 40)
            {
                Hotspots.RemoveAt(0);
            }

            Hotspots.Add(new BattleHotspot
            {
                X = x,
                Y = y,
                Hits = 1,
                LastUpdateTime = now,
                SideA = sideA,
                SideB = sideB
            });
        }

        public static void PruneExpired()
        {
            float cutoff = Time.unscaledTime - HotspotLifetime;
            for (int i = Hotspots.Count - 1; i >= 0; i--)
            {
                if (Hotspots[i].LastUpdateTime < cutoff)
                {
                    Hotspots.RemoveAt(i);
                }
            }
        }

        private static void EnsureVisuals()
        {
            if (_ringTex != null) return;

            const int sz = 48;
            _ringTex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
            float half = (sz - 1) * 0.5f;
            for (int y = 0; y < sz; y++)
            {
                for (int x = 0; x < sz; x++)
                {
                    float dist = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                    float alpha = 0f;
                    if (dist >= 0.72f && dist <= 0.96f) alpha = 0.92f;
                    else if (dist < 0.72f) alpha = 0.16f;
                    // Catppuccin Red (#f38ba8)
                    _ringTex.SetPixel(x, y, new Color(0.953f, 0.545f, 0.659f, alpha));
                }
            }
            _ringTex.Apply();

            _tagStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };
            _tagStyle.normal.textColor = new Color(0.976f, 0.886f, 0.686f); // Catppuccin Yellow
        }

        public static void DrawBattleMarkers()
        {
            if (!Enabled || Hotspots.Count == 0 || Event.current.type != EventType.Repaint) return;
            Camera cam = Camera.main;
            if (cam == null) return;

            EnsureVisuals();
            float now = Time.unscaledTime;

            for (int i = 0; i < Hotspots.Count; i++)
            {
                BattleHotspot h = Hotspots[i];
                if (h.Hits < 2) continue;

                float age = now - h.LastUpdateTime;
                float fade = Mathf.Clamp01(1f - (age / HotspotLifetime));
                Vector3 screen = cam.WorldToScreenPoint(new Vector3(h.X, h.Y, 0f));
                if (screen.z < 0f) continue;

                float sx = screen.x;
                float sy = Screen.height - screen.y;
                if (sx < -40f || sx > Screen.width + 40f || sy < -40f || sy > Screen.height + 40f) continue;

                float pulse = 1f + 0.16f * Mathf.Sin(now * 5.5f + i);
                float baseRadius = h.Hits >= 15 ? 46f : (h.Hits >= 6 ? 34f : 24f);
                float size = baseRadius * pulse;

                Color prev = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, fade);
                GUI.DrawTexture(new Rect(sx - size * 0.5f, sy - size * 0.5f, size, size), _ringTex);

                string badge = h.Hits >= 15 ? $"MAJOR BATTLE ({h.Hits})" : $"CLASH ({h.Hits})";
                GUI.Label(new Rect(sx - 68f, sy - size * 0.5f - 16f, 136f, 18f), badge, _tagStyle);
                GUI.color = prev;
            }
        }
    }
}
