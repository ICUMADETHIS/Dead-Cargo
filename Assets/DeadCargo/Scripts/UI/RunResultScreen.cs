using DeadCargo.Extraction;
using DeadCargo.Flow;
using UnityEngine;
using UnityEngine.UI;

namespace DeadCargo.UI
{
    /// <summary>
    /// Shows the run report when the run ends and pauses the world behind it.
    /// An extraction opens with a white jump flash. A loss waits so the destruction plays out.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas), typeof(CanvasScaler))]
    public sealed class RunResultScreen : MonoBehaviour
    {
        private const KeyCode ContinueKey = KeyCode.Return;
        private const KeyCode ContinueKeypadKey = KeyCode.KeypadEnter;
        private const string MothershipAction = "RETURN TO MOTHERSHIP";
        private const string RestartAction = "NEW RUN";
        private const int SortingOrder = 50;
        private const float FlashRiseTime = 0.12f;
        private const float FlashFadeTime = 0.6f;
        private const float FadeInTime = 0.3f;

        [Tooltip("Time from the jump to the report, while the screen flashes white, in seconds.")]
        [SerializeField, Min(0f)] private float jumpRevealDelay = 0.5f;

        [Tooltip("Time from the loss to the report, so the destruction plays out, in seconds.")]
        [SerializeField, Min(0f)] private float lossRevealDelay = 2.5f;

        private RunResultView view;
        private RunTracker tracker;
        private RunResult result;
        private float endTime;
        private bool revealed;

        private void Awake()
        {
            HudFactory.ConfigureCanvas(gameObject, SortingOrder);

            // Lets the backdrop block clicks. Added here because scenes set up earlier have no raycaster.
            if (!TryGetComponent(out GraphicRaycaster _))
                gameObject.AddComponent<GraphicRaycaster>();

            view = new RunResultView((RectTransform)transform);
        }

        private void Start()
        {
            tracker = RunTracker.Active;
            if (tracker == null)
            {
                Debug.LogWarning("Run result screen needs a Run Tracker in the scene.", this);
                enabled = false;
                return;
            }

            tracker.Ended += OnRunEnded;
        }

        private void OnDestroy()
        {
            if (tracker != null)
                tracker.Ended -= OnRunEnded;
        }

        private void Update()
        {
            if (result == null)
                return;

            // Unscaled, because the world is paused once the report is up.
            float elapsed = Time.unscaledTime - endTime;
            bool extracted = result.Outcome == RunOutcome.Extracted;
            float delay = extracted ? jumpRevealDelay : lossRevealDelay;

            if (extracted)
            {
                float flash = elapsed < delay
                    ? Mathf.Clamp01(elapsed / FlashRiseTime)
                    : 1f - Mathf.Clamp01((elapsed - delay) / FlashFadeTime);
                view.SetFlash(flash);
            }

            if (!revealed)
            {
                if (elapsed >= delay)
                    Reveal();
                return;
            }

            view.SetOpacity(Mathf.Clamp01((elapsed - delay) / FadeInTime));

            if (Input.GetKeyDown(ContinueKey) || Input.GetKeyDown(ContinueKeypadKey))
                tracker.ReturnToMothership();
        }

        private void OnRunEnded(RunResult runResult)
        {
            result = runResult;
            endTime = Time.unscaledTime;
        }

        private void Reveal()
        {
            revealed = true;
            view.SetOpacity(0f);
            view.Show(result, SceneFlow.HasMothership ? MothershipAction : RestartAction);

            // The report is a hard stop. The world stays paused until a new run starts.
            Time.timeScale = 0f;
            AudioListener.pause = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
