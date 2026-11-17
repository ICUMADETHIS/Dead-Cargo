using UnityEngine;

namespace DeadCargo.Sectors.Field
{
    /// <summary>
    /// What a sector's asteroid fields are made of: how many rocks, how big, how shaped and how they tumble. Where the
    /// fields are, and how thick, comes from the sector's regions. Changing a value here changes every rock, so
    /// treat it like the sector seed once a layout has been learned.
    /// </summary>
    [CreateAssetMenu(fileName = "AsteroidField", menuName = "Dead Cargo/Asteroid Field Definition")]
    public sealed class AsteroidFieldDefinition : ScriptableObject
    {
        [Tooltip("Rocks per cubic kilometre where a field is at full density, every size together. Rocks then sit " +
                 "about 100 m apart at 1,000, mostly small.")]
        [SerializeField, Min(0f)] private float rocksPerCubicKilometre = 1000f;

        [Tooltip("Smallest rock, across, in metres.")]
        [SerializeField, Min(0.5f)] private float minDiameter = 4f;

        [Tooltip("Largest rock, across, in metres.")]
        [SerializeField, Min(1f)] private float maxDiameter = 1200f;

        [Tooltip("Rocks this wide or wider, in metres, are placed first, and smaller rocks keep out of them.")]
        [SerializeField, Min(1f)] private float largeDiameter = 100f;

        [Tooltip("How fast rock counts fall with size. Rocks over twice a size are 2 to this power fewer: at 2, a quarter " +
                 "as many. Higher values give more small rocks and fewer giants.")]
        [SerializeField, Range(1f, 3.5f)] private float sizeExponent = 2f;

        [Tooltip("How far a rock may stretch or squash along each axis, as a share of its size.")]
        [SerializeField, Range(0f, 0.6f)] private float stretch = 0.3f;

        [Tooltip("Small rocks keep out of a large rock to this many times its radius.")]
        [SerializeField, Range(1f, 2f)] private float keepOut = 1.3f;

        [Tooltip("Different rock shapes. Each is built once, at load, from the sector seed.")]
        [SerializeField, Range(1, FieldRockShape.MaxShapes)] private int shapes = 8;

        [Tooltip("Tumble of a 10 m rock, in degrees per second. Smaller rocks turn faster and giants barely move; 0 holds " +
                 "every rock still.")]
        [SerializeField, Range(0f, 30f)] private float tumble = 4f;

        [Tooltip("How strongly rocks gather into clumps with gaps between. 0 spreads them evenly; 1 leaves the gaps empty. " +
                 "Clumps keep the full density above, so clumping thins the field overall.")]
        [SerializeField, Range(0f, 1f)] private float clumping = 0.75f;

        [Tooltip("Distance across a clump and the gap beside it, in metres.")]
        [SerializeField, Range(200f, 3000f)] private float clumpSize = 700f;

        [Tooltip("Share of the small rocks near a large rock that gather round it as rubble. Giants gather the most.")]
        [SerializeField, Range(0f, 0.5f)] private float rubble = 0.2f;

        /// <summary>Rocks per cubic kilometre at full density.</summary>
        public float RocksPerCubicKilometre => rocksPerCubicKilometre;

        /// <summary>The settings generation jobs read, for cells of the given size in metres.</summary>
        public FieldSettings ToSettings(float cellSize)
        {
            return FieldSettings.Create(rocksPerCubicKilometre, minDiameter, maxDiameter, largeDiameter, sizeExponent, stretch,
                keepOut, shapes, tumble, cellSize, clumping, clumpSize, rubble);
        }

        private void OnValidate()
        {
            maxDiameter = Mathf.Max(maxDiameter, minDiameter + 1f);
            largeDiameter = Mathf.Clamp(largeDiameter, minDiameter, maxDiameter);
        }
    }
}
