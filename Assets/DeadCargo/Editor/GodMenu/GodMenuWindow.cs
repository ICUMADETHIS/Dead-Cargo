using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadCargo.EditorTools.GodMenu
{
    /// <summary>
    /// The Dead Cargo control panel. Hosts every <see cref="GodMenuTab"/> in the project as a collapsible tab.
    /// </summary>
    internal sealed class GodMenuWindow : EditorWindow
    {
        // Ctrl+Alt+G. Ctrl+Shift+G is taken by Unity's Create Empty Parent.
        private const string MenuPath = "Dead Cargo/God Menu %&g";
        private const string TabStateKeyPrefix = "DeadCargo.GodMenu.Tab.";
        private const float HeaderHeight = 48f;
        private const float PageLabelWidth = 110f;
        private const float CompactLabelWidth = 70f;

        private static readonly GUIContent CollapseAllContent = new GUIContent(string.Empty, "Collapse or expand all tabs");

        private readonly List<GodMenuTab> tabs = new List<GodMenuTab>();
        private bool[] tabExpanded = new bool[0];
        private Vector2 scroll;

        [MenuItem(MenuPath, priority = 0)]
        private static void Open()
        {
            var window = GetWindow<GodMenuWindow>();
            window.titleContent = new GUIContent("God Menu");
            window.minSize = new Vector2(200f, 240f);
            window.Show();
        }

        private void OnEnable()
        {
            CreateTabs();
            GodMenuContext.RefreshAssets();
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void OnProjectChange()
        {
            GodMenuContext.RefreshAssets();
            Repaint();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            Repaint();
        }

        private void OnInspectorUpdate()
        {
            foreach (GodMenuTab tab in tabs)
                tab.OnUpdate();

            // Keeps live readouts current without repainting every editor frame.
            if (EditorApplication.isPlaying)
                Repaint();
        }

        private void OnGUI()
        {
            GodMenuGUI.EnsureStyles();
            GodMenuGUI.SetWindowWidth(position.width);
            DrawHeader();
            DrawTabs();
        }

        // Instantiates every concrete tab type in the project, so new tabs need no registration.
        private void CreateTabs()
        {
            tabs.Clear();
            foreach (Type type in TypeCache.GetTypesDerivedFrom<GodMenuTab>())
            {
                if (!type.IsAbstract)
                    tabs.Add((GodMenuTab)Activator.CreateInstance(type));
            }

            tabs.Sort((a, b) => a.Order.CompareTo(b.Order));

            // Only the first tab starts open, so a fresh window is not a wall of controls.
            tabExpanded = new bool[tabs.Count];
            for (int i = 0; i < tabs.Count; ++i)
                tabExpanded[i] = EditorPrefs.GetBool(TabStateKeyPrefix + tabs[i].Title, i == 0);
        }

        private void SetTabExpanded(int index, bool expanded)
        {
            if (tabExpanded[index] == expanded)
                return;

            tabExpanded[index] = expanded;
            EditorPrefs.SetBool(TabStateKeyPrefix + tabs[index].Title, expanded);
        }

        private bool AnyTabExpanded()
        {
            foreach (bool expanded in tabExpanded)
            {
                if (expanded)
                    return true;
            }

            return false;
        }

        private void DrawHeader()
        {
            Rect rect = GUILayoutUtility.GetRect(0f, HeaderHeight, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, GodMenuGUI.HeaderBackground);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), GodMenuGUI.Accent);

            DrawCollapseAllButton(new Rect(rect.x + 8f, rect.y + 12f, 24f, 24f));

            // In a narrow window the play-state pill shrinks to a dot, leaving room for the title.
            bool compact = GodMenuGUI.Compact;
            const float textLeft = 40f;
            float pillSpace = compact ? 28f : 104f;
            var titleRect = new Rect(rect.x + textLeft, rect.y + 6f, rect.width - textLeft - pillSpace, 22f);
            var subtitleRect = new Rect(rect.x + textLeft, rect.y + 26f, rect.width - textLeft - pillSpace, 14f);
            GUI.Label(titleRect, "DEAD CARGO", GodMenuGUI.HeaderTitle);
            GUI.Label(subtitleRect, $"God Menu  ·  {SceneManager.GetActiveScene().name}", GodMenuGUI.HeaderSubtitle);

            string state;
            Color stateColor;
            if (!EditorApplication.isPlaying)
            {
                state = "EDIT MODE";
                stateColor = GodMenuGUI.Neutral;
            }
            else if (EditorApplication.isPaused)
            {
                state = "PAUSED";
                stateColor = GodMenuGUI.Accent;
            }
            else
            {
                state = "PLAYING";
                stateColor = GodMenuGUI.Positive;
            }

            if (compact)
            {
                EditorGUI.DrawRect(new Rect(rect.xMax - 20f, rect.y + 18f, 10f, 10f), stateColor);
                return;
            }

            var pillRect = new Rect(rect.xMax - 92f, rect.y + 14f, 84f, 18f);
            EditorGUI.DrawRect(pillRect, stateColor);
            GUI.Label(pillRect, state, GodMenuGUI.Pill);
        }

        // Three-line icon, drawn by hand so it does not depend on a font glyph.
        // Collapses every tab, or expands them all when none is open.
        private void DrawCollapseAllButton(Rect rect)
        {
            if (GUI.Button(rect, CollapseAllContent, GUIStyle.none))
            {
                bool expand = !AnyTabExpanded();
                for (int i = 0; i < tabs.Count; ++i)
                    SetTabExpanded(i, expand);
            }

            if (Event.current.type != EventType.Repaint)
                return;

            for (int i = 0; i < 3; ++i)
                EditorGUI.DrawRect(new Rect(rect.x + 4f, rect.y + 6f + i * 5f, 16f, 2f), Color.white);
        }

        private void DrawTabs()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            float previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = GodMenuGUI.Compact ? CompactLabelWidth : PageLabelWidth;

            // Shown once here rather than in every tab.
            GodMenuGUI.PlayModeNotice();

            for (int i = 0; i < tabs.Count; ++i)
            {
                SetTabExpanded(i, GodMenuGUI.TabHeader(tabs[i].Title, tabExpanded[i]));
                if (tabExpanded[i])
                {
                    tabs[i].OnGUI();
                    GUILayout.Space(6f);
                }
            }

            EditorGUIUtility.labelWidth = previousLabelWidth;

            GUILayout.Space(8f);
            EditorGUILayout.EndScrollView();
        }
    }
}
