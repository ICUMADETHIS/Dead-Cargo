#if UNITY_EDITOR || DEVELOPMENT_BUILD || DDT_ENABLED
using System.Diagnostics;
using System.Text;
using DeadCargo.Sectors;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DeadCargo.Debugging
{
    /// <summary>
    /// The in-game debug overlay: a frame rate counter that is always there, and a panel of live readouts for every
    /// <see cref="DebugChannel"/> switched on, with the channel's drawing in the world through Draw Debug Tools.
    /// <para>
    /// F3 shows or hides it; F4 opens the channel picker, whose number keys switch channels. The God Menu and the dev
    /// console switch the same <see cref="DebugSettings"/>. It starts itself in the editor and in development builds and
    /// is compiled out of release builds, like Draw Debug Tools.
    /// </para>
    /// Panels refresh five times a second; the frame graph and world drawing run every frame. Its own main-thread
    /// cost shows in the Performance panel.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    public sealed class DebugOverlay : MonoBehaviour
    {
        public const KeyCode OverlayKey = KeyCode.F3;
        public const KeyCode PickerKey = KeyCode.F4;

        private const float RefreshInterval = 0.2f;
        private const float HintDuration = 10f;
        private const int SortingOrder = 32000;
        private const int FontSize = 13;
        private const float Margin = 10f;
        private const float Spacing = 4f;
        private const float GraphHeight = 56f;
        private const float MinPanelWidth = 250f;

        private static readonly Color PanelColor = new Color(0.05f, 0.06f, 0.08f, 0.78f);
        private static readonly Color TextColor = new Color(0.9f, 0.92f, 0.95f);

        private readonly StringBuilder text = new StringBuilder(1024);
        private readonly Stopwatch timer = new Stopwatch();
        private readonly Text[] channelTexts = new Text[DebugChannels.Count];
        private readonly GameObject[] channelPanels = new GameObject[DebugChannels.Count];
        private DebugFrameStats stats;
        private DebugContext context;
        private DebugWorldDraw world;
        private Canvas canvas;
        private CanvasScaler scaler;
        private RectTransform column;
        private VerticalLayoutGroup columnLayout;
        private Text counterText;
        private Text pickerText;
        private GameObject pickerPanel;
        private DebugFrameGraph graph;
        private Font font;
        private float nextRefresh;
        private float hintUntil;
        private bool sectorViewApplied;

        /// <summary>The running overlay, or null outside Play Mode.</summary>
        public static DebugOverlay Instance { get; private set; }

        /// <summary>Frame timing and engine counters, for other tools to read.</summary>
        public DebugFrameStats Stats => stats;

        /// <summary>Whether the channel picker is open.</summary>
        public bool PickerOpen { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Instance != null)
                return;

            var root = new GameObject("Debug Overlay");
            DontDestroyOnLoad(root);
            root.AddComponent<DebugOverlay>();
        }

        /// <summary>Opens or closes the channel picker.</summary>
        public void SetPickerOpen(bool open)
        {
            PickerOpen = open;
            nextRefresh = 0f;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            stats = new DebugFrameStats();
            context = new DebugContext(stats);
            world = new DebugWorldDraw();
            DebugLog.Start();
            font = ResolveFont();
            BuildCanvas();
            ApplyLayout();
            context.OnSceneChanged(SceneManager.GetActiveScene());
            ApplySectorView();
            hintUntil = Time.unscaledTime + HintDuration;
        }

        private void OnEnable()
        {
            DebugSettings.Changed += OnSettingsChanged;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            DebugSettings.Changed -= OnSettingsChanged;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance != this)
                return;

            Instance = null;
            stats?.Dispose();
            DebugLog.Stop();
        }

        private void Update()
        {
            stats.Sample(Time.unscaledDeltaTime);

            if (Input.GetKeyDown(OverlayKey))
                DebugSettings.OverlayVisible = !DebugSettings.OverlayVisible;

            if (!DebugSettings.OverlayVisible)
                return;

            if (Input.GetKeyDown(PickerKey))
                SetPickerOpen(!PickerOpen);

            if (PickerOpen)
            {
                for (int i = 0; i < DebugChannels.Count; ++i)
                {
                    var channel = (DebugChannel)i;
                    if (Input.GetKeyDown(DebugChannels.PickerKey(channel)))
                        DebugSettings.Toggle(channel);
                }
            }
        }

        private void LateUpdate()
        {
            if (!DebugSettings.OverlayVisible)
                return;

            timer.Restart();
            context.Update(Time.unscaledDeltaTime);

            if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + RefreshInterval;
                Refresh();
            }

            if (DebugSettings.IsOn(DebugChannel.Performance))
                graph.SetVerticesDirty();

            world.Draw(context);
            timer.Stop();
            context.OverlayMs = (float)timer.Elapsed.TotalMilliseconds;
        }

        private void Refresh()
        {
            stats.Refresh();
            world.Refresh(context);

            text.Clear();
            DebugReadouts.WriteCounter(text, context, Time.unscaledTime < hintUntil);
            counterText.text = text.ToString();

            pickerPanel.SetActive(PickerOpen);
            if (PickerOpen)
            {
                text.Clear();
                DebugReadouts.WritePicker(text);
                pickerText.text = text.ToString();
            }

            for (int i = 0; i < DebugChannels.Count; ++i)
            {
                var channel = (DebugChannel)i;
                bool on = DebugSettings.IsOn(channel);
                if (channelPanels[i].activeSelf != on)
                    channelPanels[i].SetActive(on);
                if (!on)
                    continue;

                text.Clear();
                DebugReadouts.Write(channel, text, context);
                channelTexts[i].text = text.ToString();
            }
        }

        private void OnSettingsChanged()
        {
            canvas.enabled = DebugSettings.OverlayVisible;
            if (!DebugSettings.OverlayVisible)
                PickerOpen = false;

            ApplyLayout();
            ApplySectorView();
            nextRefresh = 0f;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            context.OnSceneChanged(scene);
            sectorViewApplied = false;
            ApplySectorView();
        }

        // The Sector channel shows the sector's own debug view, which draws its bounds, regions and cells. It is only
        // switched when the channel changes, so settings made in the God Menu Sector tab otherwise stand.
        private void ApplySectorView()
        {
            bool on = DebugSettings.OverlayVisible && DebugSettings.IsOn(DebugChannel.Sector);
            if (on == sectorViewApplied)
                return;

            sectorViewApplied = on;
            SectorSpace space = SectorSpace.Active;
            SectorDebugView view = space != null ? space.GetComponent<SectorDebugView>() : null;
            if (view == null)
                return;

            view.ShowBounds = on;
            view.ShowRegions = on;
            view.ShowCells = on;
        }

        private void ApplyLayout()
        {
            scaler.scaleFactor = DebugSettings.TextScale;
            canvas.enabled = DebugSettings.OverlayVisible;

            Vector2 corner;
            TextAnchor alignment;
            switch (DebugSettings.Corner)
            {
                case DebugCorner.TopRight:
                    corner = new Vector2(1f, 1f);
                    alignment = TextAnchor.UpperRight;
                    break;
                case DebugCorner.BottomLeft:
                    corner = new Vector2(0f, 0f);
                    alignment = TextAnchor.LowerLeft;
                    break;
                case DebugCorner.BottomRight:
                    corner = new Vector2(1f, 0f);
                    alignment = TextAnchor.LowerRight;
                    break;
                default:
                    corner = new Vector2(0f, 1f);
                    alignment = TextAnchor.UpperLeft;
                    break;
            }

            column.anchorMin = column.anchorMax = column.pivot = corner;
            column.anchoredPosition = new Vector2(corner.x > 0.5f ? -Margin : Margin, corner.y > 0.5f ? -Margin : Margin);
            columnLayout.childAlignment = alignment;

            // Panels stack away from the corner, so the frame rate counter always sits in the corner itself.
            columnLayout.reverseArrangement = corner.y < 0.5f;
        }

        private void BuildCanvas()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            var columnObject = new GameObject("Panels", typeof(RectTransform));
            column = (RectTransform)columnObject.transform;
            column.SetParent(transform, false);
            columnLayout = columnObject.AddComponent<VerticalLayoutGroup>();
            columnLayout.spacing = Spacing;
            columnLayout.childControlWidth = true;
            columnLayout.childControlHeight = true;
            columnLayout.childForceExpandWidth = false;
            columnLayout.childForceExpandHeight = false;
            var fitter = columnObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            counterText = CreatePanel("Frame Rate", 0f).Text;

            (GameObject Panel, Text Text) picker = CreatePanel("Channel Picker", MinPanelWidth);
            pickerPanel = picker.Panel;
            pickerText = picker.Text;
            pickerPanel.SetActive(false);

            for (int i = 0; i < DebugChannels.Count; ++i)
            {
                (GameObject Panel, Text Text) panel = CreatePanel(DebugChannels.Name((DebugChannel)i), MinPanelWidth);
                channelPanels[i] = panel.Panel;
                channelTexts[i] = panel.Text;
                panel.Panel.SetActive(false);
            }

            // The frame graph sits under the Performance readouts, in the same panel.
            var graphObject = new GameObject("Frame Graph", typeof(RectTransform));
            graphObject.transform.SetParent(channelPanels[(int)DebugChannel.Performance].transform, false);
            graph = graphObject.AddComponent<DebugFrameGraph>();
            graph.Stats = stats;
            graph.raycastTarget = false;
            var graphLayout = graphObject.AddComponent<LayoutElement>();
            graphLayout.preferredHeight = GraphHeight;
            graphLayout.minWidth = MinPanelWidth - 16f;
        }

        private (GameObject Panel, Text Text) CreatePanel(string title, float minWidth)
        {
            var panel = new GameObject(title, typeof(RectTransform));
            panel.transform.SetParent(column, false);
            var background = panel.AddComponent<Image>();
            background.color = PanelColor;
            background.raycastTarget = false;

            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 5, 6);
            layout.spacing = 4f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            if (minWidth > 0f)
                panel.AddComponent<LayoutElement>().minWidth = minWidth;

            var label = new GameObject("Text", typeof(RectTransform));
            label.transform.SetParent(panel.transform, false);
            var text = label.AddComponent<Text>();
            text.font = font;
            text.fontSize = FontSize;
            text.color = TextColor;
            text.supportRichText = true;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.alignment = TextAnchor.UpperLeft;
            return (panel, text);
        }

        // Draw Debug Tools' monospaced font, so columns of figures line up; Unity's built-in font if it is missing.
        private static Font ResolveFont()
        {
            GameObject prefab = Resources.Load<GameObject>("Prefabs/DDTLogText");
            Text source = prefab != null ? prefab.GetComponentInChildren<Text>(true) : null;
            if (source != null && source.font != null)
                return source.font;

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
#endif
