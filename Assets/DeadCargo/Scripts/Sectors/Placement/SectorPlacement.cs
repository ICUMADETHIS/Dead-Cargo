using UnityEngine;

namespace DeadCargo.Sectors.Placement
{
    /// <summary>
    /// Marks scene content that the run places by its rule, such as an extraction point or a pickup. The sector space
    /// moves it when the scene loads, before anything else wakes, and again whenever the seed changes. Where it sits
    /// in the editor is only where it waits; remove this component to keep an object where it was put.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SectorPlacement : MonoBehaviour
    {
        [Tooltip("Where this content may be placed. Shared by every object of its kind.")]
        [SerializeField] private SectorPlacementRule rule;

        public SectorPlacementRule Rule => rule;

        /// <summary>True when the last placement met every part of the rule.</summary>
        public bool MetRule { get; private set; }

        /// <summary>Moves the content to its spot for this run.</summary>
        public void Place(Vector3 worldPosition, float heading, bool metRule)
        {
            transform.SetPositionAndRotation(worldPosition, Quaternion.Euler(0f, heading, 0f));
            MetRule = metRule;
        }
    }
}
