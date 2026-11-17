using UnityEngine;

namespace DeadCargo.Sectors.Placement
{
    /// <summary>
    /// Where one kind of content may be placed in a run: how far from the arrival point, how high, how deep into a field,
    /// and how far from other content. Shared by every object of that kind through <see cref="SectorPlacement"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "Placement", menuName = "Dead Cargo/Sector Placement Rule")]
    public sealed class SectorPlacementRule : ScriptableObject
    {
        [Tooltip("Rules with a lower order place first, so content placed later keeps its distance from them.")]
        [SerializeField] private int order;

        [Header("Distance")]
        [Tooltip("Closest to the arrival point, in metres.")]
        [SerializeField, Min(0f)] private float minDistance = 2000f;

        [Tooltip("Furthest from the arrival point, in metres.")]
        [SerializeField, Min(0f)] private float maxDistance = 9000f;

        [Tooltip("Furthest above or below the arrival point, in metres.")]
        [SerializeField, Min(0f)] private float maxHeight = 1000f;

        [Header("Fields")]
        [Tooltip("Least field density allowed at the spot. 0 allows open space.")]
        [SerializeField, Range(0f, 1f)] private float minDensity;

        [Tooltip("Most field density allowed at the spot. 1 allows the thick of a field.")]
        [SerializeField, Range(0f, 1f)] private float maxDensity = 1f;

        [Header("Room")]
        [Tooltip("Closest any content placed before it may be, in metres.")]
        [SerializeField, Min(0f)] private float spacing = 500f;

        [Tooltip("Radius kept clear round it, in metres: fields thin towards it and no rock, however large, reaches into it. " +
                 "0 clears nothing, and it may then be buried in rock.")]
        [SerializeField, Min(0f)] private float keepClear;

        [Tooltip("Turn it to face the arrival point. Otherwise it gets a random heading.")]
        [SerializeField] private bool faceArrival;

        public int Order => order;

        public PlacementSettings Settings => new PlacementSettings
        {
            MinDistance = Mathf.Min(minDistance, maxDistance),
            MaxDistance = Mathf.Max(minDistance, maxDistance),
            MaxHeight = maxHeight,
            MinDensity = Mathf.Min(minDensity, maxDensity),
            MaxDensity = Mathf.Max(minDensity, maxDensity),
            Spacing = spacing,
            KeepClear = keepClear,
            FaceArrival = faceArrival
        };

        /// <summary>Sets every value at once. For setup tools that create a rule.</summary>
        public void Configure(int newOrder, in PlacementSettings settings)
        {
            order = newOrder;
            minDistance = settings.MinDistance;
            maxDistance = settings.MaxDistance;
            maxHeight = settings.MaxHeight;
            minDensity = settings.MinDensity;
            maxDensity = settings.MaxDensity;
            spacing = settings.Spacing;
            keepClear = settings.KeepClear;
            faceArrival = settings.FaceArrival;
        }
    }
}
