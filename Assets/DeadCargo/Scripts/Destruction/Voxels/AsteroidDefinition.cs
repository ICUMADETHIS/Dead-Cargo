using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace DeadCargo.Destruction.Voxels
{
    /// <summary>
    /// A type of voxel asteroid: its shape, the ore inside it, its look, and how it takes damage and breaks apart.
    /// Each asteroid is built from a type, a size and a seed, so the same seed always gives the same rock.
    /// </summary>
    [CreateAssetMenu(menuName = "Dead Cargo/Destruction/Asteroid Type", fileName = "Asteroid")]
    public sealed class AsteroidDefinition : ScriptableObject
    {
        [SerializeField] private string displayName = "Asteroid";

        [Header("Look")]
        [Tooltip("Material for the rock surface. Must be DeadCargo/Comic Surface with Triplanar on.")]
        [SerializeField] private Material surfaceMaterial;

        [Tooltip("The rock itself: material slot 0.")]
        [SerializeField] private VoxelMaterialDefinition rock;

        [Header("Detail")]
        [Tooltip("Voxel spacing, in metres. Smaller voxels give finer craters and cost more memory. Large rocks use " +
                 "bigger voxels automatically so their grid stays within budget.")]
        [SerializeField, Range(0.25f, 4f)] private float voxelSize = 0.5f;

        [Header("Shape")]
        [Tooltip("How far each axis may stretch or squash, as a share of the radius.")]
        [SerializeField, Range(0f, 0.5f)] private float elongation = 0.2f;

        [Tooltip("Height of the rolling lumps, as a share of the radius.")]
        [SerializeField, Range(0f, 0.5f)] private float bumpiness = 0.2f;

        [Tooltip("Lumps around the rock. Higher values give more, smaller lumps.")]
        [SerializeField, Range(0.5f, 6f)] private float bumpFrequency = 1.6f;

        [SerializeField, Range(1, 5)] private int bumpOctaves = 3;

        [Tooltip("Crater depth, as a share of the radius. 0 for none.")]
        [SerializeField, Range(0f, 0.3f)] private float craterDepth = 0.1f;

        [Tooltip("Craters around the rock. Higher values give more, smaller craters.")]
        [SerializeField, Range(0.5f, 6f)] private float craterFrequency = 2.2f;

        [Tooltip("How much of the surface the craters cover, 0 to 1.")]
        [SerializeField, Range(0.1f, 0.9f)] private float craterSize = 0.45f;

        [Tooltip("Height of the fine surface roughness, in metres.")]
        [SerializeField, Min(0f)] private float detail = 0.4f;

        [Tooltip("Fine roughness frequency, per metre.")]
        [SerializeField, Range(0.02f, 2f)] private float detailFrequency = 0.35f;

        [Header("Ore")]
        [Tooltip("Up to four ores, in priority order where veins cross.")]
        [SerializeField] private OreVein[] veins = new OreVein[0];

        [Header("Damage")]
        [Tooltip("Rock removed per point of weapon damage, in cubic metres. Kit guns deal about 100 per hit.")]
        [SerializeField, Min(0f)] private float cubicMetresPerDamage = 0.02f;

        [Tooltip("How much more an explosion removes than a gun hit of the same damage.")]
        [SerializeField, Min(0f)] private float explosionMultiplier = 1.6f;

        [Tooltip("Smallest crater radius, in metres, so every hit leaves a mark.")]
        [SerializeField, Min(0.05f)] private float minCraterRadius = 0.35f;

        [Tooltip("Largest crater radius, in metres.")]
        [SerializeField, Min(0.1f)] private float maxCraterRadius = 6f;

        [Tooltip("How ragged crater edges are, 0 for clean spheres.")]
        [SerializeField, Range(0f, 1f)] private float craterRoughness = 0.6f;

        [Header("Breaking Apart")]
        [Tooltip("Speed a piece leaves at when cut free, in metres per second. Small pieces leave faster.")]
        [SerializeField, Min(0f)] private float splitSpeed = 2.5f;

        [Tooltip("Tumble of a piece cut free, in degrees per second. Small pieces tumble faster.")]
        [SerializeField, Min(0f)] private float splitSpin = 10f;

        [Tooltip("Optional effect played where a piece breaks away.")]
        [SerializeField] private GameObject splitEffect;

        public string DisplayName => displayName;
        public Material SurfaceMaterial => surfaceMaterial;
        public VoxelMaterialDefinition Rock => rock;
        public float VoxelSize => voxelSize;
        public float CraterRoughness => craterRoughness;
        public float SplitSpeed => splitSpeed;
        public float SplitSpin => splitSpin;
        public GameObject SplitEffect => splitEffect;

        /// <summary>Material slots in use: rock, then each ore.</summary>
        public int SlotCount => 1 + math.min(veins.Length, AsteroidShape.MaxOreLayers);

        /// <summary>The material in a slot. Slot 0 is the rock; ores follow in order. Null when empty.</summary>
        public VoxelMaterialDefinition GetMaterial(int slot)
        {
            if (slot == 0)
                return rock;

            int vein = slot - 1;
            return vein < veins.Length && vein < AsteroidShape.MaxOreLayers ? veins[vein].Material : null;
        }

        /// <summary>The shape of one asteroid of this type.</summary>
        /// <param name="diameter">Mean diameter before relief, in metres.</param>
        public AsteroidShape BuildShape(float diameter, uint seed)
        {
            var random = new Unity.Mathematics.Random(math.hash(new uint2(seed, 0x9E3779B9u)) | 1u);
            float radius = diameter * 0.5f;
            var shape = new AsteroidShape
            {
                Radii = radius * (1f + elongation * random.NextFloat3(-1f, 1f)),
                Bumpiness = bumpiness,
                BumpFrequency = bumpFrequency,
                BumpOctaves = bumpOctaves,
                CraterDepth = craterDepth,
                CraterFrequency = craterFrequency,
                CraterSize = craterSize,
                Detail = detail,
                DetailFrequency = detailFrequency,
                Offset = random.NextFloat3(-500f, 500f)
            };

            int count = math.min(veins.Length, AsteroidShape.MaxOreLayers);
            for (int i = 0; i < count; ++i)
            {
                OreVein vein = veins[i];
                shape.SetOre(i, new OreLayer
                {
                    Material = (byte)(i + 1),
                    Frequency = vein.Frequency,
                    Thickness = vein.Thickness,
                    MinDepth = vein.MinDepth,
                    Offset = random.NextFloat3(-500f, 500f)
                });
            }

            shape.OreCount = count;
            return shape;
        }

        /// <summary>The radius of rock a hit removes, in metres.</summary>
        public float CraterRadius(in DamageEvent damage)
        {
            if (damage.Radius > 0f)
                return math.min(damage.Radius, maxCraterRadius);

            float multiplier = damage.Kind == DamageKind.Explosion ? explosionMultiplier : 1f;
            float volume = math.max(0f, damage.Amount) * cubicMetresPerDamage * multiplier;
            float radius = math.pow(volume * (3f / (4f * math.PI)), 1f / 3f);
            return math.clamp(radius, minCraterRadius, maxCraterRadius);
        }

        /// <summary>Fills the palette the mesher tints veins with: rgb colour and strength per slot.</summary>
        public void FillPalette(NativeArray<float4> palette)
        {
            for (int slot = 0; slot < palette.Length; ++slot)
            {
                VoxelMaterialDefinition material = slot == 0 ? null : GetMaterial(slot);
                palette[slot] = material != null
                    ? new float4(material.VeinColor.r, material.VeinColor.g, material.VeinColor.b, material.VeinStrength)
                    : new float4(1f, 1f, 1f, 0f);
            }
        }

        /// <summary>Chip colour for a slot, falling back to the rock's.</summary>
        public Color ChipColor(int slot)
        {
            VoxelMaterialDefinition material = GetMaterial(slot);
            if (material != null)
                return material.ChipColor;

            return rock != null ? rock.ChipColor : new Color(0.55f, 0.46f, 0.38f);
        }

        private void OnValidate()
        {
            maxCraterRadius = Mathf.Max(maxCraterRadius, minCraterRadius);
        }
    }
}
