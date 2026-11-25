using System;
using System.Collections.Generic;
using UnityEngine;

namespace SuiteHub
{
    public static class WarTickerOverlay
    {
        public static bool Visible { get; set; } = true;
        public static bool Collapsed { get; set; } = false;

        private const int MaxDisplayedWars = 5;
        private const float ExpandedWidth = 322f;
        private const float CollapsedWidth = 148f;

        private static bool _rectInitialized = false;
        private static Rect _rect = new Rect(20f, 12f, ExpandedWidth, 120f);
        private static readonly List<War> _warsBuf = new List<War>();

        public static int CountActiveWars()
        {
            var world = MapBox.instance;
            if (world?.wars == null) return 0;
            try
            {
                return (int)world.wars.countActiveWars();
            }
            catch
            {
                int count = 0;
                var activeSnapshot = new List<War>(world.wars.getActiveWars());
                foreach (var w in activeSnapshot)
                {
                    if (w != null) count++;
                }
                return count;
            }
        }

        public static void Draw(Action onOpenMonitor, Action onHide)
        {
            if (!Visible) return;
            var world = MapBox.instance;
            if (world?.wars == null) return;

            _warsBuf.Clear();
            var activeSnapshot = new List<War>(world.wars.getActiveWars());
            foreach (var w in activeSnapshot)
            {
                if (w != null) _warsBuf.Add(w);
            }
            if (_warsBuf.Count == 0) return;

            if (!_rectInitialized)
            {
                _rectInitialized = true;
                float startX = Screen.width > 350 ? Screen.width - 335f : 12f;
                _rect = new Rect(startX, 12f, ExpandedWidth, 120f);
            }

            float targetWidth = Collapsed ? CollapsedWidth : ExpandedWidth;
            int shown = Mathf.Min(_warsBuf.Count, MaxDisplayedWars);
            float targetHeight = Collapsed
                ? 54f
                : 46f + shown * 42f + (_warsBuf.Count > MaxDisplayedWars ? 18f : 0f);

            if (Mathf.Abs(_rect.width - targetWidth) > 1f)
            {
                float rightEdge = _rect.x + _rect.width;
                _rect.width = targetWidth;
                _rect.x = Mathf.Clamp(rightEdge - targetWidth, 6f, Mathf.Max(6f, Screen.width - targetWidth - 6f));
            }
            _rect.height = targetHeight;
            _rect.x = Mathf.Clamp(_rect.x, 4f, Mathf.Max(4f, Screen.width - _rect.width - 4f));
            _rect.y = Mathf.Clamp(_rect.y, 4f, Mathf.Max(4f, Screen.height - _rect.height - 4f));

            string title = Collapsed ? $"Wars: {_warsBuf.Count}" : $"Live Wars ({_warsBuf.Count})";
            _rect = GUI.Window(941215, _rect, id => DrawWindow(onOpenMonitor, onHide), title, GameUiStyle.WindowStyle);
        }

        private static void DrawWindow(Action onOpenMonitor, Action onHide)
        {
            if (Collapsed)
            {
                GUILayout.BeginHorizontal();
                if (GameUiStyle.Button(
                    $"Wars: {_warsBuf.Count} [+]",
                    "Expand Live War Ticker|Show up to 5 active wars with causes, casualties, and instant Go jump buttons.|Hide anytime with F3"))
                {
                    Collapsed = false;
                }
                if (GameUiStyle.Button(
                    "F4",
                    "War & Diplomacy Monitor|Open the full War & Diplomacy Monitor with war reasons, opinion drivers, and Force Peace.|Hotkey: F4",
                    GUILayout.Width(32f)))
                {
                    onOpenMonitor?.Invoke();
                }
                GUILayout.EndHorizontal();

                GameUiStyle.CaptureTooltip();
                GUI.DragWindow();
                return;
            }

            // Header corner buttons: [-], F4, X
            if (GUI.Button(
                new Rect(_rect.width - 78f, 4f, 22f, 18f),
                new GUIContent("-", "Collapse Ticker|Shrink the Live War Ticker into a compact 1-line pill."),
                GameUiStyle.ButtonStyle))
            {
                Collapsed = true;
            }

            if (GUI.Button(
                new Rect(_rect.width - 53f, 4f, 26f, 18f),
                new GUIContent("F4", "Open War & Diplomacy Monitor|Inspect full war causes, opinion drivers, relations matrix, and Force Peace.|Hotkey: F4"),
                GameUiStyle.ButtonStyle))
            {
                onOpenMonitor?.Invoke();
            }

            GameUiStyle.DrawCloseButton(_rect, onHide);

            int limit = Mathf.Min(_warsBuf.Count, MaxDisplayedWars);
            for (int i = 0; i < limit; i++)
            {
                War w = _warsBuf[i];
                Kingdom atk = w.getMainAttacker();
                Kingdom def = w.getMainDefender();
                string atkName = TrimName(atk?.name ?? "?", 13);
                string defName = TrimName(def?.name ?? "?", 13);
                long dead = w.getDeadAttackers() + w.getDeadDefenders();
                string fullCause = DiplomacyWarOverlay.ResolveWarCause(w);
                string shortCause = DiplomacyWarOverlay.ShortenWarCause(fullCause);

                string rowTip = $"{w.name}|Cause: {fullCause} ({w.getDuration()}y, {dead} dead)|Click Go to jump to fighting, or F4 for full details";

                GUILayout.BeginHorizontal();
                GameUiStyle.Label($"{atkName} vs {defName}", rowTip, isHeader: true);
                if (GameUiStyle.Button("Go", $"Jump to {w.name}|Pan camera to active fighting or defender capital.", GUILayout.Width(34f)))
                {
                    DiplomacyWarOverlay.JumpToWar(w);
                }
                GUILayout.EndHorizontal();

                GameUiStyle.Subtle($"[{shortCause}]  •  {dead} dead", rowTip);
            }

            if (_warsBuf.Count > MaxDisplayedWars)
            {
                GameUiStyle.Subtle($"+{_warsBuf.Count - MaxDisplayedWars} more active wars — press F4 for all");
            }

            GameUiStyle.CaptureTooltip();
            GUI.DragWindow();
        }

        private static string TrimName(string name, int maxLen)
        {
            if (string.IsNullOrEmpty(name) || name.Length <= maxLen) return name ?? "?";
            return name.Substring(0, maxLen - 1) + "…";
        }
    }
}
