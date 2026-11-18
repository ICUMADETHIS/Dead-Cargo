using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DeadCargo.EditorTools
{
    /// <summary>
    /// Plays the game in a window of its own: when Play starts, a floating Game view opens at a chosen 16:9 size, focused,
    /// where it was last left (centred over the editor the first time), and it closes when Play stops. Settings are kept
    /// per machine. Unity has no public way to float a Game view, so the window is the editor's own Game view type,
    /// created by name.
    /// <para>
    /// While it is open, every other Game view is pointed at an unused display, where no camera renders, so the game is
    /// drawn once; each gets its own display back when the window closes. If a script reload in Play loses track of
    /// them, any Game view still on that display goes back to Display 1 when the editor next leaves Play.
    /// </para>
    /// </summary>
    [InitializeOnLoad]
    internal static class PlayWindow
    {
        /// <summary>The sizes offered for the game picture, all 16:9, in pixels.</summary>
        internal static readonly Vector2Int[] Sizes = { new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(1920, 1080) };

        private const string EnabledKey = "DeadCargo.PlayWindow.Enabled";
        private const string SizeKey = "DeadCargo.PlayWindow.Size";
        private const string PlacedKey = "DeadCargo.PlayWindow.Placed";
        private const string XKey = "DeadCargo.PlayWindow.X";
        private const string YKey = "DeadCargo.PlayWindow.Y";
        private const string GameViewTypeName = "UnityEditor.GameView";
        private const string WindowTitle = "Dead Cargo";
        private const int DefaultSize = 1;

        // The Game view's own toolbar sits above the picture, so the window is this much taller than the picture.
        private const float ToolbarHeight = 21f;

        // Room kept free round the window, and for the window's title bar and the taskbar, in editor units.
        private const float ScreenMargin = 16f;
        private const float TitleAndTaskbar = 80f;

        // Other Game views are pointed here while the window is open: Display 8, the last Unity supports, which no camera uses.
        private const int BlankDisplay = 7;
        private const string BlankedKey = "DeadCargo.PlayWindow.Blanked";
        private const string DisplayPropertyName = "targetDisplay";

        private static readonly List<EditorWindow> blankedViews = new List<EditorWindow>();
        private static readonly List<int> blankedDisplays = new List<int>();

        private static EditorWindow window;
        private static PropertyInfo displayProperty;

        static PlayWindow()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        /// <summary>True to open the window whenever Play starts.</summary>
        internal static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledKey, false);
            set => EditorPrefs.SetBool(EnabledKey, value);
        }

        /// <summary>Which of <see cref="Sizes"/> the window opens at.</summary>
        internal static int SizeIndex
        {
            get => Mathf.Clamp(EditorPrefs.GetInt(SizeKey, DefaultSize), 0, Sizes.Length - 1);
            set => EditorPrefs.SetInt(SizeKey, Mathf.Clamp(value, 0, Sizes.Length - 1));
        }

        /// <summary>True while the window is open.</summary>
        internal static bool IsOpen => window != null;

        /// <summary>The game picture's size in screen pixels when the window last opened.</summary>
        internal static Vector2Int PictureSize { get; private set; }

        /// <summary>How many other Game views are blanked while the window is open.</summary>
        internal static int BlankedCount => blankedViews.Count;

        /// <summary>Opens the window, or focuses it when it is open.</summary>
        internal static void Open()
        {
            if (window != null)
            {
                window.Focus();
                return;
            }

            Type gameView = FindGameViewType();
            if (gameView == null)
            {
                Debug.LogWarning("Play Window could not find Unity's Game view type, so the game plays in the editor's Game tab.");
                return;
            }

            Vector2 picture = FittedPicture(Sizes[SizeIndex]);
            PictureSize = Vector2Int.RoundToInt(picture * EditorGUIUtility.pixelsPerPoint);
            var size = new Vector2(picture.x, picture.y + ToolbarHeight);
            window = (EditorWindow)ScriptableObject.CreateInstance(gameView);
            window.titleContent = new GUIContent(WindowTitle);
            window.Show();
            window.position = new Rect(Placement(size), size);
            SetDisplay(window, 0);
            BlankOtherViews(gameView);
            window.Focus();
        }

        /// <summary>Closes the window, remembers where it was, and gives the other Game views their displays back.</summary>
        internal static void Close()
        {
            EditorWindow closing = Release();
            if (closing != null)
                closing.Close();
        }

        // Unity is still telling every Game view that Play is ending, the pop-out among them, and a view closed now is told
        // anyway and throws. So the window closes on the editor's next update; the other views get their displays back now.
        private static void CloseAfterPlay()
        {
            EditorWindow closing = Release();
            if (closing != null)
                EditorApplication.delayCall += () => CloseIfOpen(closing);
        }

        private static void CloseIfOpen(EditorWindow view)
        {
            if (view != null)
                view.Close();
        }

        // Remembers where the window was, gives the other Game views their displays back, and forgets the window, which
        // the caller closes.
        private static EditorWindow Release()
        {
            EditorWindow released = window;
            if (released != null)
            {
                Rect rect = released.position;
                EditorPrefs.SetFloat(XKey, rect.x);
                EditorPrefs.SetFloat(YKey, rect.y);
                EditorPrefs.SetBool(PlacedKey, true);
                window = null;
            }

            RestoreOtherViews();
            return released;
        }

        /// <summary>Opens the window centred over the editor next time, rather than where it was left.</summary>
        internal static void ForgetPlacement()
        {
            EditorPrefs.DeleteKey(PlacedKey);
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && Enabled)
                Open();
            else if (change == PlayModeStateChange.ExitingPlayMode)
                CloseAfterPlay();
            else if (change == PlayModeStateChange.EnteredEditMode)
                RecoverLostViews();
        }

        // Every other Game view draws nothing while the window is open, so the game is not drawn twice.
        private static void BlankOtherViews(Type gameView)
        {
            foreach (UnityEngine.Object found in Resources.FindObjectsOfTypeAll(gameView))
            {
                var view = found as EditorWindow;
                if (view == null || view == window)
                    continue;

                int display = GetDisplay(view);
                if (display == BlankDisplay)
                    continue;

                blankedViews.Add(view);
                blankedDisplays.Add(display);
                SetDisplay(view, BlankDisplay);
            }

            if (blankedViews.Count > 0)
                EditorPrefs.SetBool(BlankedKey, true);
        }

        private static void RestoreOtherViews()
        {
            for (int i = 0; i < blankedViews.Count; ++i)
            {
                if (blankedViews[i] != null)
                    SetDisplay(blankedViews[i], blankedDisplays[i]);
            }

            blankedViews.Clear();
            blankedDisplays.Clear();
            EditorPrefs.DeleteKey(BlankedKey);
        }

        // A script reload in Play forgets which views were blanked; any still on the blank display goes back to Display 1.
        private static void RecoverLostViews()
        {
            if (!EditorPrefs.GetBool(BlankedKey, false))
                return;

            Type gameView = FindGameViewType();
            if (gameView != null)
            {
                foreach (UnityEngine.Object found in Resources.FindObjectsOfTypeAll(gameView))
                {
                    if (found is EditorWindow view && GetDisplay(view) == BlankDisplay)
                        SetDisplay(view, 0);
                }
            }

            EditorPrefs.DeleteKey(BlankedKey);
        }

        // Unity keeps a Game view's display behind a property with a protected setter, on its base class.
        private static PropertyInfo DisplayProperty
        {
            get
            {
                if (displayProperty != null)
                    return displayProperty;

                for (Type type = FindGameViewType(); type != null && displayProperty == null; type = type.BaseType)
                {
                    displayProperty = type.GetProperty(DisplayPropertyName,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                }

                return displayProperty;
            }
        }

        private static int GetDisplay(EditorWindow view)
        {
            PropertyInfo property = DisplayProperty;
            return property != null && property.GetValue(view) is int display ? display : 0;
        }

        private static void SetDisplay(EditorWindow view, int display)
        {
            MethodInfo setter = DisplayProperty != null ? DisplayProperty.GetSetMethod(true) : null;
            if (setter == null)
                return;

            setter.Invoke(view, new object[] { display });
            view.Repaint();
        }

        // The picture in editor units for a size in screen pixels: Windows display scaling makes a unit more than a pixel.
        // Shrunk, keeping 16:9, when the window would not fit the screen with its title bar and the taskbar.
        private static Vector2 FittedPicture(Vector2Int pixels)
        {
            float pixelsPerPoint = Mathf.Max(0.01f, EditorGUIUtility.pixelsPerPoint);
            var picture = new Vector2(pixels.x, pixels.y) / pixelsPerPoint;
            Resolution screen = Screen.currentResolution;
            float widthRoom = screen.width / pixelsPerPoint - ScreenMargin;
            float heightRoom = screen.height / pixelsPerPoint - ScreenMargin - TitleAndTaskbar - ToolbarHeight;
            float fit = Mathf.Min(1f, Mathf.Min(widthRoom / picture.x, heightRoom / picture.y));
            return fit > 0f ? picture * fit : picture;
        }

        private static Vector2 Placement(Vector2 size)
        {
            if (EditorPrefs.GetBool(PlacedKey, false))
                return new Vector2(EditorPrefs.GetFloat(XKey), EditorPrefs.GetFloat(YKey));

            Rect editor = EditorGUIUtility.GetMainWindowPosition();
            return editor.center - size * 0.5f;
        }

        private static Type FindGameViewType()
        {
            Type type = typeof(EditorWindow).Assembly.GetType(GameViewTypeName);
            if (type != null)
                return type;

            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(GameViewTypeName);
                if (type != null)
                    return type;
            }

            return null;
        }
    }
}
