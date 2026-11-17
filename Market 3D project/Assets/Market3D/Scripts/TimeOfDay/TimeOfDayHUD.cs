using UnityEngine;

namespace TenPercent2D.Market3D
{
    /// <summary>
    /// 左下角的时间 HUD：当前时间 / 当前时段 / 可点击的时段按钮 / 可拖动的时间条。
    /// 只画 IMGUI，不依赖 Canvas 或中文字体，所以文字全用英文。
    /// </summary>
    [RequireComponent(typeof(TimeOfDaySystem))]
    public class TimeOfDayHUD : MonoBehaviour
    {
        public bool visible = true;

        TimeOfDaySystem m_System;
        Texture2D m_White;
        GUIStyle m_TimeStyle;
        GUIStyle m_PresetStyle;
        GUIStyle m_KeyStyle;
        GUIStyle m_HintStyle;
        bool m_StylesReady;

        bool m_Scrubbing;
        int m_HoverPreset = -1;
        bool m_HoverAuto;
        bool m_HoverHide;
        bool m_HoverBar;

        void Awake()
        {
            m_System = GetComponent<TimeOfDaySystem>();
            m_White = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            m_White.SetPixel(0, 0, Color.white);
            m_White.Apply();
            m_White.hideFlags = HideFlags.HideAndDontSave;
        }

        void OnDestroy()
        {
            if (m_White != null) Destroy(m_White);
        }

        void EnsureStyles()
        {
            if (m_StylesReady || m_System == null) return;

            m_TimeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 30,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft,
                richText = false
            };
            m_TimeStyle.normal.textColor = new Color(0.96f, 0.96f, 0.94f);

            m_PresetStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft,
                richText = false
            };
            m_PresetStyle.normal.textColor = new Color(0.96f, 0.78f, 0.50f);

            m_KeyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.UpperLeft,
                richText = false
            };

            m_HintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.UpperLeft,
                richText = false
            };
            m_HintStyle.normal.textColor = new Color(0.62f, 0.66f, 0.72f);

            m_StylesReady = true;
        }

        void OnGUI()
        {
            if (m_System == null) m_System = GetComponent<TimeOfDaySystem>();
            if (m_System == null || !visible || !m_System.showHud || !Application.isPlaying) return;

            EnsureStyles();
            if (!m_StylesReady) return;

            float s = Mathf.Clamp(Screen.height / 760f, 0.90f, 1.70f);
            float pad = 14f * s;
            float panelW = 286f * s;
            float panelH = 122f * s;
            Rect panel = new Rect(22f * s, Screen.height - panelH - 22f * s, panelW, panelH);

            float hour = Mathf.Repeat(m_System.DisplayHour, 24f);
            int hInt = Mathf.FloorToInt(hour);
            int mInt = Mathf.FloorToInt((hour - hInt) * 60f);
            if (mInt >= 60) { mInt = 0; hInt = (hInt + 1) % 24; }

            int active = m_System.CurrentPresetIndex;
            string[] keys = { "1  DAY", "2  DUSK", "3  NIGHT" };

            float keyY = panel.y + 65f * s;
            Rect[] keyRects = new Rect[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                keyRects[i] = new Rect(panel.x + pad + i * 86f * s, keyY - 3f * s, 84f * s, 20f * s);
            }

            Rect autoRect = new Rect(panel.x + pad, panel.y + 96f * s, 126f * s, 18f * s);
            Rect hideRect = new Rect(panel.x + pad + 136f * s, panel.y + 96f * s, 76f * s, 18f * s);
            Rect bar = new Rect(panel.x + pad, panel.y + 87f * s, panelW - pad * 2f, 2f * s);
            Rect barHit = new Rect(bar.x - 4f * s, bar.y - 6f * s, bar.width + 8f * s, 16f * s);

            HandlePointer(panel, keyRects, autoRect, hideRect, bar, barHit);

            // 只在实际绘制帧画，避免 Layout 帧的 DrawTexture 留下残影。
            if (Event.current.type != EventType.Repaint) return;

            // ---- 面板背景 ----
            Color old = GUI.color;
            GUI.color = new Color(0.025f, 0.030f, 0.040f, 0.72f);
            GUI.DrawTexture(panel, m_White);
            GUI.color = new Color(0.96f, 0.78f, 0.50f, 0.92f);
            GUI.DrawTexture(new Rect(panel.x, panel.y, 3f * s, panel.height), m_White);
            GUI.color = old;

            // ---- 时间 ----
            GUIStyle timeStyle = m_TimeStyle;
            timeStyle.fontSize = Mathf.RoundToInt(30f * s);
            GUI.Label(new Rect(panel.x + pad, panel.y + 8f * s, panelW - pad * 2f, 34f * s),
                string.Format("{0:00}:{1:00}", hInt, mInt), timeStyle);

            // ---- 当前时段 ----
            GUIStyle presetStyle = m_PresetStyle;
            presetStyle.fontSize = Mathf.RoundToInt(11f * s);
            GUI.Label(new Rect(panel.x + pad, panel.y + 47f * s, panelW - pad * 2f, 16f * s),
                m_System.CurrentPresetName, presetStyle);

            // ---- 三个可点击的时段 ----
            GUIStyle keyStyle = m_KeyStyle;
            keyStyle.fontSize = Mathf.RoundToInt(10f * s);
            for (int i = 0; i < keys.Length; i++)
            {
                bool hover = m_HoverPreset == i;
                bool isActive = i == active;

                if (hover)
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.08f);
                    GUI.DrawTexture(keyRects[i], m_White);
                }

                keyStyle.normal.textColor = isActive
                    ? new Color(0.98f, 0.80f, 0.52f)
                    : (hover ? new Color(0.82f, 0.84f, 0.88f) : new Color(0.56f, 0.60f, 0.66f));

                GUI.Label(new Rect(keyRects[i].x, keyRects[i].y + 3f * s, keyRects[i].width, 14f * s), keys[i], keyStyle);

                // 当前时段 / 悬停时给一条下划线，暗示可点
                if (isActive || hover)
                {
                    GUI.color = isActive
                        ? new Color(0.98f, 0.80f, 0.52f, 0.95f)
                        : new Color(0.98f, 0.80f, 0.52f, 0.45f);
                    GUI.DrawTexture(new Rect(keyRects[i].x, keyRects[i].yMax - 2f * s, keyRects[i].width, 1.5f * s), m_White);
                }
            }
            GUI.color = old;

            // ---- 24 小时时间条（可拖动） ----
            if (m_HoverBar || m_Scrubbing)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.07f);
                GUI.DrawTexture(new Rect(bar.x, bar.y - 3f * s, bar.width, 8f * s), m_White);
            }
            GUI.color = new Color(1f, 1f, 1f, 0.16f);
            GUI.DrawTexture(bar, m_White);

            if (m_System.presets != null)
            {
                for (int i = 0; i < m_System.presets.Length; i++)
                {
                    float px = bar.x + Mathf.Repeat(m_System.presets[i].hour / 24f, 1f) * bar.width;
                    GUI.color = new Color(0.96f, 0.78f, 0.50f, 0.55f);
                    GUI.DrawTexture(new Rect(px - 0.5f * s, bar.y - 2f * s, 1f * s, 6f * s), m_White);
                }
            }

            float dotX = bar.x + Mathf.Repeat(hour / 24f, 1f) * bar.width;
            GUI.color = new Color(1f, 0.86f, 0.60f, 1f);
            GUI.DrawTexture(new Rect(dotX - 2f * s, bar.y - 1f * s, 4f * s, 4f * s), m_White);

            // ---- AUTO / HIDE 按钮 ----
            GUIStyle hintStyle = m_HintStyle;
            hintStyle.fontSize = Mathf.RoundToInt(9f * s);

            if (m_HoverAuto)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.08f);
                GUI.DrawTexture(autoRect, m_White);
            }
            if (m_HoverHide)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.08f);
                GUI.DrawTexture(hideRect, m_White);
            }

            hintStyle.normal.textColor = m_System.IsAuto
                ? new Color(0.98f, 0.80f, 0.52f)
                : (m_HoverAuto ? new Color(0.82f, 0.84f, 0.88f) : new Color(0.58f, 0.62f, 0.68f));
            string autoText = m_System.IsAuto ? "T  AUTO ON" : "T  AUTO OFF";
            GUI.Label(new Rect(autoRect.x, autoRect.y + 2f * s, autoRect.width, 14f * s), autoText, hintStyle);

            hintStyle.normal.textColor = m_HoverHide
                ? new Color(0.82f, 0.84f, 0.88f)
                : new Color(0.58f, 0.62f, 0.68f);
            GUI.Label(new Rect(hideRect.x, hideRect.y + 2f * s, hideRect.width, 14f * s), "H  HIDE", hintStyle);

            GUI.color = old;
        }

        void HandlePointer(Rect panel, Rect[] keyRects, Rect autoRect, Rect hideRect, Rect bar, Rect barHit)
        {
            Vector2 mouse = Event.current.mousePosition;

            m_HoverPreset = -1;
            for (int i = 0; i < keyRects.Length; i++)
                if (keyRects[i].Contains(mouse)) m_HoverPreset = i;
            m_HoverAuto = autoRect.Contains(mouse);
            m_HoverHide = hideRect.Contains(mouse);
            m_HoverBar = barHit.Contains(mouse);

            EventType type = Event.current.type;
            if (type == EventType.MouseDown && Event.current.button == 0)
            {
                if (m_HoverPreset >= 0)
                {
                    m_System.JumpToPreset(m_HoverPreset);
                    Event.current.Use();
                    return;
                }
                if (m_HoverAuto)
                {
                    m_System.ToggleAuto();
                    Event.current.Use();
                    return;
                }
                if (m_HoverHide)
                {
                    m_System.showHud = false;
                    Event.current.Use();
                    return;
                }
                if (m_HoverBar)
                {
                    m_Scrubbing = true;
                    ScrubTo(mouse.x, bar);
                    Event.current.Use();
                    return;
                }
                if (panel.Contains(mouse)) Event.current.Use();
            }
            else if (type == EventType.MouseDrag && m_Scrubbing)
            {
                ScrubTo(mouse.x, bar);
                Event.current.Use();
            }
            else if (type == EventType.MouseUp && m_Scrubbing)
            {
                m_Scrubbing = false;
                Event.current.Use();
            }
        }

        void ScrubTo(float mouseX, Rect bar)
        {
            float t = Mathf.Clamp01((mouseX - bar.x) / Mathf.Max(1f, bar.width));
            m_System.SetHour(t * 24f);
        }
    }
}
