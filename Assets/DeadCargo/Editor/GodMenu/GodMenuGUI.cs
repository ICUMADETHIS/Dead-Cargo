using UnityEditor;
using UnityEngine;

namespace DeadCargo.EditorTools.GodMenu
{
    /// <summary>
    /// Styles and drawing helpers that give the God Menu a consistent look.
    /// The layout helpers reflow to the window width, so tabs stay usable in a narrow docked window.
    /// </summary>
    internal static class GodMenuGUI
    {
        public const float ButtonHeight = 24f;

        private const string SectionStateKeyPrefix = "DeadCargo.GodMenu.Section.";

        // Window width lost to the scrollbar and to a section's margins and padding.
        private const float WindowChromeWidth = 50f;
        private const float CompactContentWidth = 280f;
        private const float ButtonSpacing = 4f;

        public static readonly Color Accent = new Color(1f, 0.72f, 0.2f);
        public static readonly Color Positive = new Color(0.4f, 0.8f, 0.45f);
        public static readonly Color Negative = new Color(0.95f, 0.35f, 0.3f);
        public static readonly Color Neutral = new Color(0.5f, 0.5f, 0.52f);

        private static readonly GUIContent MeasureContent = new GUIContent();

        private static GUIStyle headerTitle;
        private static GUIStyle headerSubtitle;
        private static GUIStyle pill;
        private static GUIStyle tabTitle;
        private static GUIStyle tabSummary;
        private static GUIStyle groupTitle;
        private static GUIStyle hint;
        private static GUIStyle section;
        private static GUIStyle sectionTitle;
        private static GUIStyle barLabel;

        public static GUIStyle HeaderTitle => headerTitle;
        public static GUIStyle HeaderSubtitle => headerSubtitle;
        public static GUIStyle Pill => pill;

        /// <summary>Width available to controls inside a section, in pixels.</summary>
        public static float ContentWidth { get; private set; } = 400f;

        /// <summary>True when the window is narrow enough that rows should stack.</summary>
        public static bool Compact => ContentWidth < CompactContentWidth;

        public static Color HeaderBackground =>
            EditorGUIUtility.isProSkin ? new Color(0.11f, 0.11f, 0.12f) : new Color(0.24f, 0.24f, 0.26f);

        private static Color TabHeaderBackground =>
            EditorGUIUtility.isProSkin ? new Color(0.17f, 0.17f, 0.18f) : new Color(0.7f, 0.7f, 0.72f);

        private static Color BarBackground =>
            EditorGUIUtility.isProSkin ? new Color(0.14f, 0.14f, 0.15f) : new Color(0.6f, 0.6f, 0.62f);

        /// <summary>Tells the helpers how wide the window is. Call once at the start of OnGUI.</summary>
        public static void SetWindowWidth(float windowWidth)
        {
            ContentWidth = Mathf.Max(100f, windowWidth - WindowChromeWidth);
        }

        /// <summary>Builds the styles. Must be called from OnGUI, where editor styles are available.</summary>
        public static void EnsureStyles()
        {
            if (headerTitle != null)
                return;

            headerTitle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                normal = { textColor = Color.white }
            };

            headerSubtitle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                normal = { textColor = new Color(0.75f, 0.75f, 0.78f) }
            };

            pill = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.08f, 0.08f, 0.08f) }
            };

            tabTitle = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold, fontSize = 13 };

            tabSummary = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                clipping = TextClipping.Clip,
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.7f, 0.72f, 0.75f) : new Color(0.2f, 0.2f, 0.22f) }
            };

            groupTitle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.LowerLeft,
                normal = { textColor = Accent }
            };

            hint = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(10, 10, 4, 4)
            };

            section = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 6, 8),
                margin = new RectOffset(8, 8, 6, 2)
            };

            sectionTitle = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };

            barLabel = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
        }

        /// <summary>
        /// A titled, collapsible group of controls. Dispose to close it.
        /// Skip drawing the body when <see cref="Expanded"/> is false.
        /// </summary>
        public sealed class SectionScope : GUI.Scope
        {
            public SectionScope(string title)
            {
                string stateKey = SectionStateKeyPrefix + title;
                bool wasExpanded = EditorPrefs.GetBool(stateKey, true);

                EditorGUILayout.BeginVertical(section);

                Rect header = GUILayoutUtility.GetRect(0f, 20f, GUILayout.ExpandWidth(true));
                Expanded = EditorGUI.Foldout(header, wasExpanded, title.ToUpperInvariant(), true, sectionTitle);
                if (Expanded != wasExpanded)
                    EditorPrefs.SetBool(stateKey, Expanded);

                if (Expanded)
                    GUILayout.Space(4f);
            }

            public bool Expanded { get; }

            protected override void CloseScope()
            {
                EditorGUILayout.EndVertical();
            }
        }

        /// <summary>Draws a small heading above a group of tabs.</summary>
        public static void GroupHeader(string title)
        {
            GUILayout.Space(8f);
            Rect rect = GUILayoutUtility.GetRect(0f, 18f, GUILayout.ExpandWidth(true));
            GUI.Label(new Rect(rect.x + 10f, rect.y, rect.width - 20f, rect.height - 3f), title, groupTitle);
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(new Rect(rect.x + 10f, rect.yMax - 1f, rect.width - 20f, 1f), new Color(Accent.r, Accent.g, Accent.b, 0.35f));
        }

        /// <summary>
        /// Draws a tab's full-width header bar, with an optional live status on the right. Returns the new expanded state.
        /// </summary>
        public static bool TabHeader(string title, string summary, bool expanded)
        {
            GUILayout.Space(2f);
            Rect rect = GUILayoutUtility.GetRect(0f, 28f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, TabHeaderBackground);
                if (expanded)
                    EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), Accent);
            }

            // The summary takes up to half the bar; the title keeps the rest.
            float summaryWidth = 0f;
            if (!string.IsNullOrEmpty(summary))
            {
                MeasureContent.text = summary;
                summaryWidth = Mathf.Min(tabSummary.CalcSize(MeasureContent).x, rect.width * 0.5f);
                GUI.Label(new Rect(rect.xMax - summaryWidth - 10f, rect.y, summaryWidth, rect.height), summary, tabSummary);
            }

            var foldout = new Rect(rect.x + 10f, rect.y, rect.width - 24f - summaryWidth, rect.height);
            return EditorGUI.Foldout(foldout, expanded, title.ToUpperInvariant(), true, tabTitle);
        }

        /// <summary>A centred line of muted guidance text.</summary>
        public static void Hint(string text)
        {
            GUILayout.Label(text, hint);
        }

        /// <summary>Draws a row of buttons that wraps onto extra lines when the window is too narrow.</summary>
        /// <returns>The index of the clicked button, or -1.</returns>
        public static int ButtonRow(params string[] labels)
        {
            return ButtonRow(labels, null, -1, Color.white);
        }

        /// <summary>Wrapping button row with a per-button enabled flag.</summary>
        public static int ButtonRow(string[] labels, bool[] enabled)
        {
            return ButtonRow(labels, enabled, -1, Color.white);
        }

        /// <summary>Wrapping button row with enabled flags and one tinted button.</summary>
        public static int ButtonRow(string[] labels, bool[] enabled, int highlightedIndex, Color highlightColor)
        {
            int clicked = -1;
            float usedWidth = 0f;
            bool rowOpen = false;

            for (int i = 0; i < labels.Length; ++i)
            {
                MeasureContent.text = labels[i];
                float width = GUI.skin.button.CalcSize(MeasureContent).x + ButtonSpacing;

                if (!rowOpen || usedWidth + width > ContentWidth)
                {
                    if (rowOpen)
                        EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    rowOpen = true;
                    usedWidth = 0f;
                }

                usedWidth += width;

                using (new EditorGUI.DisabledScope(enabled != null && !enabled[i]))
                {
                    Color previousColor = GUI.backgroundColor;
                    if (i == highlightedIndex)
                        GUI.backgroundColor = highlightColor;

                    if (GUILayout.Button(labels[i], GUILayout.Height(ButtonHeight)))
                        clicked = i;

                    GUI.backgroundColor = previousColor;
                }
            }

            if (rowOpen)
                EditorGUILayout.EndHorizontal();

            return clicked;
        }

        /// <summary>
        /// Starts a list row: a label, optional detail text, then the caller's mini buttons.
        /// In a narrow window the text takes its own line and the buttons drop below it.
        /// Close with <see cref="EndItemRow"/>.
        /// </summary>
        public static void BeginItemRow(string label, string detail = null)
        {
            if (Compact)
            {
                GUILayout.Label(detail == null ? label : $"{label}   {detail}", EditorStyles.miniBoldLabel);
                EditorGUILayout.BeginHorizontal();
                return;
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label);
            GUILayout.FlexibleSpace();
            if (detail != null)
                GUILayout.Label(detail, EditorStyles.miniLabel);
        }

        public static void EndItemRow()
        {
            if (Compact)
                GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();
        }

        public static bool MiniButton(string label, float width)
        {
            return GUILayout.Button(label, EditorStyles.miniButton, GUILayout.Width(width));
        }

        public static void Field(string label, string value)
        {
            EditorGUILayout.LabelField(label, value);
        }

        /// <summary>Draws a filled bar with a centred label.</summary>
        public static void Bar(float fraction, string label, Color color)
        {
            Rect rect = GUILayoutUtility.GetRect(0f, 18f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, BarBackground);

            Rect fill = rect;
            fill.width *= Mathf.Clamp01(fraction);
            EditorGUI.DrawRect(fill, color);

            GUI.Label(rect, label, barLabel);
            GUILayout.Space(2f);
        }

        /// <summary>Draws a coloured status dot followed by a label.</summary>
        public static void Status(bool ok, string label)
        {
            // A plain layout rect has no minimum width, so the row still fits a narrow window.
            Rect rect = GUILayoutUtility.GetRect(0f, EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(true));
            var dot = new Rect(rect.x + 2f, rect.y + (rect.height - 8f) * 0.5f, 8f, 8f);
            EditorGUI.DrawRect(dot, ok ? Positive : Neutral);

            rect.xMin += 18f;
            GUI.Label(rect, label);
        }
    }
}
