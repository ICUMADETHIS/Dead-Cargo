using System.Collections;
using DeadCargo.Cargo;
using UnityEngine;
using UnityEngine.UI;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.UI
{
    /// <summary>Shows the cargo hold of the vehicle the player is currently in.</summary>
    [DisallowMultipleComponent]
    public sealed class CargoHudPanel : MonoBehaviour
    {
        [Tooltip("Root that is hidden when the current vehicle has no cargo hold.")]
        [SerializeField] private GameObject content;
        [SerializeField] private Text massLabel;
        [SerializeField] private Text valueLabel;
        [SerializeField] private Text noticeLabel;

        [Tooltip("Bar whose right anchor tracks the load fraction.")]
        [SerializeField] private RectTransform loadFill;

        [Tooltip("How long the hold-full notice stays up, in seconds.")]
        [SerializeField, Min(0f)] private float noticeDuration = 2f;

        private CargoHold hold;
        private Coroutine noticeRoutine;

        private void Start()
        {
            GameAgentManager manager = GameAgentManager.Instance;
            if (manager == null)
            {
                Bind(null);
                return;
            }

            manager.onFocusedVehicleChanged.AddListener(OnFocusedVehicleChanged);
            GameAgent player = manager.FocusedGameAgent;
            OnFocusedVehicleChanged(player != null ? player.Vehicle : null);
        }

        private void OnDestroy()
        {
            if (GameAgentManager.Instance != null)
                GameAgentManager.Instance.onFocusedVehicleChanged.RemoveListener(OnFocusedVehicleChanged);

            Bind(null);
        }

        private void OnFocusedVehicleChanged(Vehicle vehicle)
        {
            Bind(vehicle != null ? vehicle.GetComponent<CargoHold>() : null);
        }

        private void Bind(CargoHold newHold)
        {
            if (hold != null)
            {
                hold.Changed -= Refresh;
                hold.CapacityExceeded -= OnCapacityExceeded;
            }

            hold = newHold;

            if (hold != null)
            {
                hold.Changed += Refresh;
                hold.CapacityExceeded += OnCapacityExceeded;
            }

            if (noticeLabel != null)
                noticeLabel.enabled = false;

            if (content != null)
                content.SetActive(hold != null);

            Refresh();
        }

        private void Refresh()
        {
            if (hold == null)
                return;

            massLabel.text = $"{hold.TotalMass:0.#} / {hold.MassCapacity:0.#} t";
            valueLabel.text = $"{hold.TotalValue:N0} cr";

            Vector2 anchorMax = loadFill.anchorMax;
            anchorMax.x = hold.LoadFraction;
            loadFill.anchorMax = anchorMax;
        }

        private void OnCapacityExceeded(CargoItemDefinition item)
        {
            if (noticeLabel == null || !isActiveAndEnabled)
                return;

            if (noticeRoutine != null)
                StopCoroutine(noticeRoutine);

            noticeRoutine = StartCoroutine(ShowNotice("HOLD FULL"));
        }

        private IEnumerator ShowNotice(string message)
        {
            noticeLabel.text = message;
            noticeLabel.enabled = true;
            yield return new WaitForSeconds(noticeDuration);
            noticeLabel.enabled = false;
            noticeRoutine = null;
        }
    }
}
