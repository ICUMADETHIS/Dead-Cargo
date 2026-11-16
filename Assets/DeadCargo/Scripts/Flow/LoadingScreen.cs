using System.Collections;
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
    /// Covers a scene change: fades to black, loads the scene asynchronously with a progress bar, then fades back in.
    /// Survives the load and destroys itself when done. Blocks input while it is up.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LoadingScreen : MonoBehaviour
    {
        private const int SortingOrder = 100;
        private const int UiLayer = 5;
        private const float FadeTime = 0.25f;
        private const float MinimumShowTime = 0.8f;
        private const float PanelWidth = 560f;

        private static readonly Color BackgroundColor = new Color32(5, 10, 15, 255);

        private static LoadingScreen active;

        private CanvasGroup group;
        private Text title;
        private Text sceneName;
        private HudMeter progress;

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
        }

        private void Awake()
        {
            HudFactory.ConfigureCanvas(gameObject, SortingOrder);
            var rect = (RectTransform)transform;

            group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            // Raycast target, so nothing underneath can be clicked mid-load.
            HudFactory.Overlay("Background", rect, BackgroundColor).raycastTarget = true;

            RectTransform panel = HudFactory.Anchored("Panel", rect, new Vector2(0.5f, 0.5f), PanelWidth, 110f);
            title = HudFactory.Label("Title", panel, "", 0f, 0f, PanelWidth, 44f, 36, HudFactory.Ink, TextAnchor.MiddleCenter);
            sceneName = HudFactory.Label("Scene", panel, "", 0f, 46f, PanelWidth, 26f, 18, HudFactory.Muted, TextAnchor.MiddleCenter);
            progress = HudFactory.Meter("Progress", panel, 40f, 92f, PanelWidth - 80f, 6f);
        }

        private void OnDestroy()
        {
            if (active == this)
                active = null;
        }

        private IEnumerator Load(string scenePath, string loadingTitle)
        {
            title.text = loadingTitle;
            sceneName.text = Path.GetFileNameWithoutExtension(scenePath).ToUpperInvariant();
            progress.SetProgress(0f, HudFactory.JumpColor);

            yield return Fade(0f, 1f);

            // The outgoing scene may be paused by a menu or the run report.
            Time.timeScale = 1f;
            float shownAt = Time.unscaledTime;

            AsyncOperation load = StartLoad(scenePath);
            if (load == null)
            {
                Debug.LogError($"Could not load scene '{scenePath}'. Check the path on the Scene Flow Settings asset and the Build Settings.");
                yield return Fade(1f, 0f);
                Destroy(gameObject);
                yield break;
            }

            while (!load.isDone)
            {
                progress.SetProgress(Mathf.Clamp01(load.progress / 0.9f), HudFactory.JumpColor);
                yield return null;
            }

            progress.SetProgress(1f, HudFactory.JumpColor);
            AudioListener.pause = false;

            while (Time.unscaledTime - shownAt < MinimumShowTime)
                yield return null;

            yield return Fade(1f, 0f);
            Destroy(gameObject);
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
