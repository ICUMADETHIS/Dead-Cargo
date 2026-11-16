using DeadCargo.Heat;
using UnityEngine;
using UnityEngine.UI;

namespace DeadCargo.UI
{
    /// <summary>Shows the current heat level and tier.</summary>
    [DisallowMultipleComponent]
    public sealed class HeatHudPanel : MonoBehaviour
    {
        [Tooltip("Root that is hidden when the scene has no heat tracker.")]
        [SerializeField] private GameObject content;
        [SerializeField] private Text tierLabel;

        [Tooltip("Bar whose right anchor tracks normalized heat.")]
        [SerializeField] private RectTransform heatFill;
        [SerializeField] private Image heatFillImage;

        private HeatTracker tracker;

        private void Start()
        {
            tracker = HeatTracker.Active;
            content.SetActive(tracker != null);
            if (tracker == null)
                return;

            tracker.Changed += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (tracker != null)
                tracker.Changed -= Refresh;
        }

        private void Refresh()
        {
            HeatTier tier = tracker.CurrentTier;
            tierLabel.text = tier.Label;
            tierLabel.color = tier.Color;
            heatFillImage.color = tier.Color;

            Vector2 anchorMax = heatFill.anchorMax;
            anchorMax.x = tracker.NormalizedHeat;
            heatFill.anchorMax = anchorMax;
        }
    }
}
