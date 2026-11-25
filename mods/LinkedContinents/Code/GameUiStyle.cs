using System;
using UnityEngine;

namespace LinkedContinents
{
    /// <summary>
    /// Dedicated IMGUI runner that stays disabled (0.00ms Unity IMGUI overhead) when no overlay/panel is visible.
    /// </summary>
    public class GuiOverlayRunner : MonoBehaviour
    {
        public Action OnGuiCallback;

        public void Configure(bool useLayout, Action callback)
        {
            useGUILayout = useLayout;
            OnGuiCallback = callback;
            enabled = false;
        }

        private void OnGUI()
        {
            OnGuiCallback?.Invoke();
        }
    }

    /// <summary>
    /// WorldBox-matched pixel-framed IMGUI styling and native-style hover tooltip renderer.
    /// Tooltip text supports "Title|Description" or "Title|Description|Hint".
    /// </summary>
    public static class GameUiStyle
    {
        private static bool _ready;
        private static Texture2D _windowTex;
        private static Texture2D _btnNormalTex;
        private static Texture2D _btnHoverTex;
        private static Texture2D _btnActiveTex;
        private static Texture2D _btnOnTex;
        private static Texture2D _btnOnHoverTex;
        private static Texture2D _btnCloseTex;
        private static Texture2D _btnCloseHoverTex;
        private static Texture2D _fieldTex;
        private static Texture2D _tooltipTex;
        private static Texture2D _dividerTex;

        public static GUIStyle WindowStyle { get; private set; }
        public static GUIStyle HeaderStyle { get; private set; }
        public static GUIStyle LabelStyle { get; private set; }
        public static GUIStyle SubtleStyle { get; private set; }
        public static GUIStyle ButtonStyle { get; private set; }
        public static GUIStyle ToggleOnStyle { get; private set; }
        public static GUIStyle CloseButtonStyle { get; private set; }
        public static GUIStyle TextFieldStyle { get; private set; }

        private static GUIStyle _tooltipBoxStyle;
        private static GUIStyle _tooltipTitleStyle;
        private static GUIStyle _tooltipBodyStyle;
        private static GUIStyle _tooltipHintStyle;

        private static string _activeTooltip = "";
        private static string _lastRepaintTooltip = "";
        private static string _currentRepaintTooltip = "";

        public static void Ensure()
        {
            if (_ready) return;
            _ready = true;

            Color outline    = Hex(0x14, 0x0F, 0x0B, 1.00f);
            Color goldBright = Hex(0xD8, 0xAC, 0x56, 1.00f);
            Color goldMid    = Hex(0x9E, 0x7A, 0x42, 1.00f);
            Color goldDark   = Hex(0x5E, 0x47, 0x28, 1.00f);
            Color woodDark   = Hex(0x28, 0x20, 0x1A, 0.96f);
            Color woodHeader = Hex(0x37, 0x2B, 0x21, 0.98f);
            Color woodBtn    = Hex(0x45, 0x36, 0x2A, 0.98f);
            Color woodHover  = Hex(0x5E, 0x49, 0x36, 1.00f);
            Color woodPress  = Hex(0x31, 0x25, 0x1C, 1.00f);
            Color btnHi      = Hex(0x73, 0x5B, 0x43, 1.00f);
            Color btnLo      = Hex(0x2B, 0x20, 0x18, 1.00f);

            Color greenFill  = Hex(0x2E, 0x6A, 0x3E, 0.98f);
            Color greenHover = Hex(0x3D, 0x85, 0x50, 1.00f);
            Color greenHi    = Hex(0x6D, 0xB8, 0x76, 1.00f);
            Color greenLo    = Hex(0x1D, 0x44, 0x28, 1.00f);

            Color redFill    = Hex(0x7A, 0x2E, 0x2C, 0.98f);
            Color redHover   = Hex(0xA4, 0x3D, 0x3A, 1.00f);
            Color redHi      = Hex(0xC9, 0x62, 0x5E, 1.00f);
            Color redLo      = Hex(0x4B, 0x1B, 0x1A, 1.00f);

            Color tipBg      = Hex(0x1B, 0x15, 0x11, 0.98f);
            Color fieldBg    = Hex(0x1C, 0x16, 0x12, 0.98f);

            Color textGold   = Hex(0xFF, 0xDF, 0x73, 1.00f);
            Color textCream  = Hex(0xF5, 0xEC, 0xD6, 1.00f);
            Color textMuted  = Hex(0xC9, 0xB8, 0x9B, 1.00f);
            Color textHint   = Hex(0x96, 0xE0, 0x88, 1.00f);

            _windowTex        = MakeWindowTex(woodDark, woodHeader, outline, goldBright, goldDark);
            _btnNormalTex     = MakeBevelTex(woodBtn, outline, btnHi, btnLo);
            _btnHoverTex      = MakeBevelTex(woodHover, outline, goldBright, goldDark);
            _btnActiveTex     = MakeBevelTex(woodPress, outline, goldDark, btnHi);
            _btnOnTex         = MakeBevelTex(greenFill, outline, greenHi, greenLo);
            _btnOnHoverTex    = MakeBevelTex(greenHover, outline, goldBright, greenLo);
            _btnCloseTex      = MakeBevelTex(redFill, outline, redHi, redLo);
            _btnCloseHoverTex = MakeBevelTex(redHover, outline, goldBright, redLo);
            _fieldTex         = MakeBevelTex(fieldBg, outline, goldDark, goldMid);
            _tooltipTex       = MakeBevelTex(tipBg, outline, goldBright, goldMid);
            _dividerTex       = MakeSolidTex(Hex(0x9E, 0x7A, 0x42, 0.55f));

            WindowStyle = new GUIStyle(GUI.skin.window)
            {
                border = new RectOffset(6, 6, 26, 6),
                padding = new RectOffset(12, 12, 28, 10),
                contentOffset = new Vector2(-10f, -20f),
                alignment = TextAnchor.UpperCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 13
            };
            WindowStyle.normal.background = _windowTex;
            WindowStyle.onNormal.background = _windowTex;
            WindowStyle.normal.textColor = textGold;
            WindowStyle.onNormal.textColor = textGold;

            HeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12,
                wordWrap = true
            };
            HeaderStyle.normal.textColor = textGold;

            LabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true
            };
            LabelStyle.normal.textColor = textCream;

            SubtleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                wordWrap = true
            };
            SubtleStyle.normal.textColor = textMuted;

            ButtonStyle = new GUIStyle(GUI.skin.button)
            {
                border = new RectOffset(4, 4, 4, 4),
                padding = new RectOffset(8, 8, 5, 5),
                margin = new RectOffset(2, 2, 3, 3),
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            ButtonStyle.normal.background = _btnNormalTex;
            ButtonStyle.hover.background = _btnHoverTex;
            ButtonStyle.active.background = _btnActiveTex;
            ButtonStyle.normal.textColor = textCream;
            ButtonStyle.hover.textColor = Color.white;
            ButtonStyle.active.textColor = textGold;

            ToggleOnStyle = new GUIStyle(ButtonStyle);
            ToggleOnStyle.normal.background = _btnOnTex;
            ToggleOnStyle.hover.background = _btnOnHoverTex;
            ToggleOnStyle.active.background = _btnActiveTex;
            ToggleOnStyle.normal.textColor = Color.white;
            ToggleOnStyle.hover.textColor = Color.white;

            CloseButtonStyle = new GUIStyle(ButtonStyle)
            {
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
            CloseButtonStyle.normal.background = _btnCloseTex;
            CloseButtonStyle.hover.background = _btnCloseHoverTex;
            CloseButtonStyle.active.background = _btnActiveTex;
            CloseButtonStyle.normal.textColor = textCream;
            CloseButtonStyle.hover.textColor = Color.white;

            TextFieldStyle = new GUIStyle(GUI.skin.textField)
            {
                border = new RectOffset(4, 4, 4, 4),
                padding = new RectOffset(6, 6, 4, 4),
                fontSize = 12
            };
            TextFieldStyle.normal.background = _fieldTex;
            TextFieldStyle.focused.background = _fieldTex;
            TextFieldStyle.hover.background = _fieldTex;
            TextFieldStyle.normal.textColor = textCream;
            TextFieldStyle.focused.textColor = Color.white;

            _tooltipBoxStyle = new GUIStyle(GUI.skin.box)
            {
                border = new RectOffset(4, 4, 4, 4),
                padding = new RectOffset(10, 10, 8, 8)
            };
            _tooltipBoxStyle.normal.background = _tooltipTex;

            _tooltipTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12,
                wordWrap = true,
                padding = new RectOffset(0, 0, 0, 2)
            };
            _tooltipTitleStyle.normal.textColor = textGold;

            _tooltipBodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                wordWrap = true,
                padding = new RectOffset(0, 0, 2, 0)
            };
            _tooltipBodyStyle.normal.textColor = textCream;

            _tooltipHintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Italic,
                wordWrap = true,
                padding = new RectOffset(0, 0, 3, 0)
            };
            _tooltipHintStyle.normal.textColor = textHint;
        }

        public static void BeginFrame()
        {
            Ensure();
            if (Event.current.type == EventType.Layout)
            {
                _activeTooltip = _lastRepaintTooltip;
            }
            else if (Event.current.type == EventType.Repaint)
            {
                _currentRepaintTooltip = "";
            }
        }

        public static void CaptureTooltip()
        {
            if (Event.current.type == EventType.Repaint && !string.IsNullOrEmpty(GUI.tooltip))
            {
                _currentRepaintTooltip = GUI.tooltip;
            }
        }

        public static bool Button(string text, string tooltip, params GUILayoutOption[] options)
        {
            Ensure();
            return GUILayout.Button(new GUIContent(text, tooltip ?? ""), ButtonStyle, options);
        }

        public static bool ToggleButton(string text, bool isOn, string tooltip, params GUILayoutOption[] options)
        {
            Ensure();
            return GUILayout.Button(new GUIContent(text, tooltip ?? ""), isOn ? ToggleOnStyle : ButtonStyle, options);
        }

        public static void Label(string text, string tooltip = null, bool isHeader = false)
        {
            Ensure();
            GUILayout.Label(new GUIContent(text, tooltip ?? ""), isHeader ? HeaderStyle : LabelStyle);
        }

        public static void Subtle(string text, string tooltip = null)
        {
            Ensure();
            GUILayout.Label(new GUIContent(text, tooltip ?? ""), SubtleStyle);
        }

        public static float SliderRow(string labelText, float value, float min, float max, string tooltip)
        {
            Ensure();
            GUILayout.Label(new GUIContent(labelText, tooltip ?? ""), LabelStyle);
            Rect r = GUILayoutUtility.GetRect(10f, 18f, GUILayout.ExpandWidth(true));
            float next = GUI.HorizontalSlider(r, value, min, max);
            if (!string.IsNullOrEmpty(tooltip) && r.Contains(Event.current.mousePosition) && Event.current.type == EventType.Repaint)
            {
                _currentRepaintTooltip = tooltip;
            }
            return next;
        }

        public static void DrawCloseButton(Rect windowRect, Action onClose)
        {
            Ensure();
            Rect closeRect = new Rect(windowRect.width - 24f, 4f, 20f, 18f);
            if (GUI.Button(closeRect, new GUIContent("X", "Close Panel|Hide this window.|Reopen anytime from the WorldBox Suite tab or its hotkey."), CloseButtonStyle))
            {
                onClose?.Invoke();
            }
        }

        public static void DrawTooltip(int tooltipWindowId)
        {
            if (Event.current.type == EventType.Repaint)
            {
                _lastRepaintTooltip = _currentRepaintTooltip;
            }
            if (string.IsNullOrEmpty(_activeTooltip)) return;
            Ensure();

            string[] parts = _activeTooltip.Split('|');
            string title = parts.Length >= 2 ? parts[0].Trim() : "";
            string body  = parts.Length >= 2 ? parts[1].Trim() : parts[0].Trim();
            string hint  = parts.Length >= 3 ? parts[2].Trim() : "";

            const float maxWidth = 270f;
            float innerW = maxWidth - 20f;

            float h = 16f;
            float titleH = 0f;
            float bodyH = 0f;
            float hintH = 0f;

            if (!string.IsNullOrEmpty(title))
            {
                titleH = _tooltipTitleStyle.CalcHeight(new GUIContent(title), innerW);
                h += titleH + 5f;
            }
            if (!string.IsNullOrEmpty(body))
            {
                bodyH = _tooltipBodyStyle.CalcHeight(new GUIContent(body), innerW);
                h += bodyH;
            }
            if (!string.IsNullOrEmpty(hint))
            {
                hintH = _tooltipHintStyle.CalcHeight(new GUIContent(hint), innerW);
                h += hintH;
            }

            Vector2 mouse = Event.current.mousePosition;
            float x = mouse.x + 16f;
            float y = mouse.y + 18f;
            if (x + maxWidth > Screen.width - 8f) x = mouse.x - maxWidth - 12f;
            if (y + h > Screen.height - 8f) y = mouse.y - h - 12f;
            x = Mathf.Clamp(x, 8f, Mathf.Max(8f, Screen.width - maxWidth - 8f));
            y = Mathf.Clamp(y, 8f, Mathf.Max(8f, Screen.height - h - 8f));

            Rect tipRect = new Rect(x, y, maxWidth, h);
            GUI.Window(tooltipWindowId, tipRect, _ =>
            {
                float cy = 8f;
                if (!string.IsNullOrEmpty(title))
                {
                    GUI.Label(new Rect(10f, cy, innerW, titleH + 2f), title, _tooltipTitleStyle);
                    cy += titleH + 2f;
                    GUI.DrawTexture(new Rect(10f, cy, innerW, 1f), _dividerTex);
                    cy += 3f;
                }
                if (!string.IsNullOrEmpty(body))
                {
                    GUI.Label(new Rect(10f, cy, innerW, bodyH + 2f), body, _tooltipBodyStyle);
                    cy += bodyH;
                }
                if (!string.IsNullOrEmpty(hint))
                {
                    GUI.Label(new Rect(10f, cy, innerW, hintH + 2f), hint, _tooltipHintStyle);
                }
            }, GUIContent.none, _tooltipBoxStyle);
            GUI.BringWindowToFront(tooltipWindowId);
        }

        private static Color Hex(byte r, byte g, byte b, float a) => new Color(r / 255f, g / 255f, b / 255f, a);

        private static Texture2D MakeSolidTex(Color col)
        {
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            t.SetPixel(0, 0, col);
            t.Apply();
            return t;
        }

        private static Texture2D MakeBevelTex(Color fill, Color outline, Color hi, Color lo)
        {
            const int sz = 16;
            var t = new Texture2D(sz, sz, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < sz; y++)
            {
                for (int x = 0; x < sz; x++)
                {
                    Color c = fill;
                    if (x == 0 || y == 0 || x == sz - 1 || y == sz - 1) c = outline;
                    else if (y == sz - 2 || x == 1) c = hi;
                    else if (y == 1 || x == sz - 2) c = lo;
                    t.SetPixel(x, y, c);
                }
            }
            t.Apply();
            return t;
        }

        private static Texture2D MakeWindowTex(Color fill, Color headerFill, Color outline, Color hi, Color lo)
        {
            const int w = 32;
            const int h = 48;
            const int headerH = 24;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool inHeader = y >= h - headerH;
                    Color c = inHeader ? headerFill : fill;
                    if (x == 0 || y == 0 || x == w - 1 || y == h - 1) c = outline;
                    else if (x == 1 || y == h - 2) c = hi;
                    else if (x == w - 2 || y == 1) c = lo;
                    else if (x == 2 || y == 2 || x == w - 3 || y == h - 3) c = outline;
                    else if (y == h - headerH) c = lo;
                    else if (y == h - headerH + 1) c = hi;
                    t.SetPixel(x, y, c);
                }
            }
            t.Apply();
            return t;
        }
    }
}
