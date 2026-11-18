using System.Collections;
using System.Collections.Generic;
using System.IO;
using DeadCargo.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace DeadCargo.Flow
{
    /// <summary>
    /// Covers a scene change. It fades to black and loads the scene in the background, then draws a still, labelled bar
    /// before letting the scene switch on, because the switch freezes the screen. The new scene then starts up hidden,
    /// with the player's controls held, until every load task (<see cref="LoadGate"/>) is done and the frames have
    /// settled, or a time limit passes. Then it fades in and raises <see cref="SceneFlow.Entered"/>. The bar is shared
    /// out across the stages and only moves forward; the line under it names the stage, on the comic loading card
    /// (<see cref="LoadingCardView"/>) with a tip from <see cref="SceneFlowSettings"/>. Survives the load and destroys
    /// itself when done. Blocks input while it is up. Marks each stage for the load report (<see cref="LoadReport"/>).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LoadingScreen : MonoBehaviour
    {
        private const int SortingOrder = 100;
        private const int UiLayer = 5;
        private const float FadeTime = 0.25f;

        // The least time the card stays up when there is no Scene Flow Settings asset to say.
        private const float DefaultMinimumShowTime = 0.8f;

        // Unity's load progress stops here until the loaded scene is let switch on.
        private const float ActivationProgress = 0.9f;

        // The bar's share at the end of each stage: the background load, the switch, the tasks; settling fills the rest.
        private const float LoadShare = 0.5f;
        private const float EnterShare = 0.6f;
        private const float TaskShare = 0.9f;

        // Frames drawn with the still bar before the switch, so it is on screen while the switch freezes it.
        private const int StillFrames = 2;

        // Settled: this many frames in a row, each shorter than the limit.
        private const int SettleFrames = 10;
        private const float SettleFrameSeconds = 0.025f;

        // A machine that never runs frames that fast is shown the scene anyway after this long.
        private const float SettleLimit = 3f;

        // From the switch: past this the scene is shown whatever is still running, and the report names it.
        private const float GateLimit = 15f;

        private const string LoadingStage = "LOADING";
        private const string EnteringStage = "ENTERING";
        private const string SettlingStage = "SETTLING";
        private const string ReadyStage = "READY";

        private static readonly Color BackgroundColor = new Color32(5, 10, 15, 255);

        private static LoadingScreen active;

        private static int lastTip = -1;

        private readonly PlayerControlsHold controls = new PlayerControlsHold();
        private CanvasGroup group;
        private LoadingCardView card;
        private GameObject cover;
        private LoadingArrival arrival;
        private float shownProgress;

        // When the screen went dark, and the least time the card stays up from then.
        private float shownAt = float.PositiveInfinity;
        private float minimumShowTime = DefaultMinimumShowTime;
        private bool openedGate;
        private bool raisedPriority;
        private ThreadPriority playPriority;

        /// <summary>True from the start of a load until the new scene has faded in.</summary>
        public static bool IsLoading => active != null;

        /// <summary>Starts loading a scene by project path. Returns false if a load is already running.</summary>
        internal static bool Begin(string scenePath, string loadingTitle)
        {
            if (active != null || string.IsNullOrEmpty(scenePath))
                return false;

            var root = new GameObject("Loading Screen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster))
            {
                layer = UiLayer
            };
            DontDestroyOnLoad(root);

            active = root.AddComponent<LoadingScreen>();
            active.StartCoroutine(active.Load(scenePath, loadingTitle));
            return true;
        }

        // Statics survive play sessions when domain reload is off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            active = null;
            lastTip = -1;
        }

        private void Awake()
        {
            HudFactory.ConfigureCanvas(gameObject, SortingOrder);
            var rect = (RectTransform)transform;

            group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            // Raycast target, so nothing underneath can be clicked mid-load.
            // The background and card, which the arrival's flash replaces at once; the flash and burst draw over them.
            RectTransform coverRect = HudFactory.Stretch("Cover", rect);
            cover = coverRect.gameObject;
            HudFactory.Overlay("Background", coverRect, BackgroundColor).raycastTarget = true;
            card = new LoadingCardView(coverRect);
            arrival = new LoadingArrival(rect);
        }

        private void Update()
        {
            card.Tick(Time.unscaledDeltaTime);
        }

        private void OnDestroy()
        {
            // Only does anything when cut short, such as by leaving Play Mode mid-load.
            arrival.Abort();
            SceneManager.sceneLoaded -= OnSceneWoken;
            if (openedGate && LoadGate.IsOpen)
                LoadGate.Close();
            controls.Release();
            RestorePriority();

            if (active == this)
                active = null;
        }

        private IEnumerator Load(string scenePath, string loadingTitle)
        {
            string displayName = Path.GetFileNameWithoutExtension(scenePath);
            LoadReport.BeginLoad(displayName);

            // Home is warm, everywhere else the jump's blue.
            bool home = SceneFlow.HasMothership && scenePath == SceneFlow.Settings.MothershipScene;
            minimumShowTime = MinimumShowTime(scenePath);
            card.Show(loadingTitle, displayName.ToUpperInvariant(), home ? HudFactory.CargoColor : HudFactory.JumpColor, NextTip());
            ShowStage(LoadingStage, 0f);

            yield return Fade(0f, 1f);
            LoadReport.MarkFadedOut();

            // The outgoing scene may be paused by a menu or the run report.
            Time.timeScale = 1f;
            shownAt = Time.unscaledTime;
            RaisePriority();

            AsyncOperation load = StartLoad(scenePath);
            if (load == null)
            {
                Debug.LogError($"Could not load scene '{scenePath}'. Check the path on the Scene Flow Settings asset and the Build Settings.");
                LoadReport.Cancel();
                RestorePriority();
                yield return Fade(1f, 0f);
                Destroy(gameObject);
                yield break;
            }

            // Held at 0.9, with the data in, until the screen shows a still bar for the switch to freeze. The outgoing
            // scene still runs meanwhile, so its controls are held too.
            load.allowSceneActivation = false;
            while (load.progress < ActivationProgress)
            {
                controls.Hold();
                ShowStage(LoadingStage, LoadShare * load.progress / ActivationProgress);
                yield return null;
            }

            LoadReport.MarkLoaded();
            ShowStage(EnteringStage, EnterShare);
            for (int i = 0; i < StillFrames; ++i)
            {
                controls.Hold();
                yield return null;
            }

            // Open before the switch, so the new scene's objects see it from their first Awake.
            LoadGate.Open();
            openedGate = true;
            SceneManager.sceneLoaded += OnSceneWoken;
            LoadReport.MarkActivating();
            load.allowSceneActivation = true;
            while (!load.isDone)
                yield return null;

            LoadReport.MarkSwitched();
            AudioListener.pause = false;

            // Ready early, the card stays up for its least time while the bar finishes filling.
            yield return WaitUntilReady();
            while (Time.unscaledTime - shownAt < minimumShowTime)
            {
                controls.Hold();
                ShowStage(ReadyStage, 1f);
                yield return null;
            }

            LoadReport.MarkFadeIn();
            LoadGate.Close();
            RestorePriority();

            // Into a ship away from home, the ship streaks in behind a white flash; elsewhere, a fade from black.
            if (!home && arrival.TryBegin())
                yield return Arrive();
            else
                yield return Reveal();

            LoadReport.MarkFadedIn();
            Destroy(gameObject);
        }

        private IEnumerator Reveal()
        {
            controls.Release();
            SceneFlow.RaiseEntered();
            yield return Fade(1f, 0f);
        }

        // The run starts as the scene appears; the controls wait until the ship has stopped.
        private IEnumerator Arrive()
        {
            cover.SetActive(false);
            SceneFlow.RaiseEntered();
            while (!arrival.IsOver)
            {
                arrival.Tick(Time.unscaledDeltaTime);
                if (arrival.ShipSettled)
                    controls.Release();
                else
                    controls.Hold();

                yield return null;
            }

            controls.Release();
        }

        // Waits for every load task, then for the frames to settle, holding the player's controls each frame. A task the
        // new scene begins late, even while settling, holds the screen again.
        private IEnumerator WaitUntilReady()
        {
            float started = Time.unscaledTime;
            float settlingSince = started;
            bool waitingForTasks = true;
            int settledFrames = 0;

            while (true)
            {
                controls.Hold();
                float now = Time.unscaledTime;
                LoadTask pending = LoadGate.Pending;

                if (pending != null)
                {
                    if (now - started >= GateLimit)
                    {
                        Debug.LogWarning($"The loading screen stopped waiting for '{pending.Label}' after {GateLimit:0} s.");
                        LoadReport.MarkHeldBy(pending.Label);
                        yield break;
                    }

                    waitingForTasks = true;
                    settledFrames = 0;
                    ShowStage(pending.Banner, Mathf.Lerp(EnterShare, TaskShare, LoadGate.Progress));
                    yield return null;
                    continue;
                }

                if (waitingForTasks)
                {
                    waitingForTasks = false;
                    settlingSince = now;
                    LoadReport.MarkTasksDone();
                }

                // The last frame's length, which includes any start-up work it ran.
                settledFrames = Time.unscaledDeltaTime < SettleFrameSeconds ? settledFrames + 1 : 0;
                ShowStage(SettlingStage, Mathf.Lerp(TaskShare, 1f, (float)settledFrames / SettleFrames));
                if (settledFrames >= SettleFrames)
                {
                    LoadReport.MarkSettled(true);
                    yield break;
                }

                if (now - settlingSince >= SettleLimit || now - started >= GateLimit)
                {
                    LoadReport.MarkSettled(false);
                    yield break;
                }

                yield return null;
            }
        }

        // Unity calls this after the scene's Awake and OnEnable and before its Start, splitting the frozen frame in two.
        private static void OnSceneWoken(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= OnSceneWoken;
            LoadReport.MarkWoken();
        }

        // Behind the black screen, background loading (the scene, then planets and other assets loaded asynchronously) may
        // take up to 50 ms of each frame instead of a few. Put back as the game appears.
        private void RaisePriority()
        {
            if (raisedPriority)
                return;

            raisedPriority = true;
            playPriority = Application.backgroundLoadingPriority;
            Application.backgroundLoadingPriority = ThreadPriority.High;
        }

        private void RestorePriority()
        {
            if (!raisedPriority)
                return;

            raisedPriority = false;
            Application.backgroundLoadingPriority = playPriority;
        }

        // The bar only moves forward, whatever share a stage asks for. It never runs ahead of the card's least time, so a
        // quick load still fills it steadily rather than waiting full.
        private void ShowStage(string stage, float share)
        {
            card.SetStage(stage);
            float paced = minimumShowTime > 0f ? Mathf.Clamp01((Time.unscaledTime - shownAt) / minimumShowTime) : 1f;
            shownProgress = Mathf.Max(shownProgress, Mathf.Min(share, paced));
            card.SetProgress(shownProgress);
        }

        // Every load stays up for the least time; a load into the sector longer, so its tip can be read.
        private static float MinimumShowTime(string scenePath)
        {
            SceneFlowSettings settings = SceneFlow.Settings;
            if (settings == null)
                return DefaultMinimumShowTime;

            bool sector = SceneFlow.HasSector && scenePath == settings.SectorScene;
            return Mathf.Max(settings.MinimumLoadSeconds, sector ? settings.SectorLoadSeconds : 0f);
        }

        // A different tip from the last, at random. Empty when there are none.
        private static string NextTip()
        {
            SceneFlowSettings settings = SceneFlow.Settings;
            if (settings == null || settings.LoadingTips == null || settings.LoadingTips.Count == 0)
                return string.Empty;

            IReadOnlyList<string> tips = settings.LoadingTips;
            int pick = Random.Range(0, tips.Count);
            if (tips.Count > 1 && pick == lastTip)
                pick = (pick + 1 + Random.Range(0, tips.Count - 1)) % tips.Count;

            lastTip = pick;
            return tips[pick] ?? string.Empty;
        }

        // Unscaled, because the outgoing scene may have time frozen.
        private IEnumerator Fade(float from, float to)
        {
            for (float elapsed = 0f; elapsed < FadeTime; elapsed += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(from, to, elapsed / FadeTime);
                yield return null;
            }

            group.alpha = to;
        }

        private static AsyncOperation StartLoad(string scenePath)
        {
#if UNITY_EDITOR
            // Editor play mode can load scenes that are not in the Build Settings.
            return EditorSceneManager.LoadSceneAsyncInPlayMode(scenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            return SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Single);
#endif
        }
    }
}
