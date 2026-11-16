using DeadCargo.Cargo;
using UnityEngine;

namespace DeadCargo.Destruction.Voxels
{
    /// <summary>
    /// A material asteroids are made of: plain rock or an ore. Sets how it looks in the rock and in flying chips, how
    /// heavy it is, and what it becomes when mined.
    /// </summary>
    [CreateAssetMenu(menuName = "Dead Cargo/Destruction/Voxel Material", fileName = "VoxelMaterial")]
    public sealed class VoxelMaterialDefinition : ScriptableObject
    {
        [SerializeField] private string displayName = "Rock";

        [Tooltip("Colour of the material where it shows in the rock. Plain rock leaves this unused.")]
        [SerializeField] private Color veinColor = Color.white;

        [Tooltip("How strongly the vein colour replaces the rock's own, 0 to 1. 0 for plain rock.")]
        [SerializeField, Range(0f, 1f)] private float veinStrength;

        [Tooltip("Colour of the chips knocked off it.")]
        [SerializeField] private Color chipColor = new Color(0.55f, 0.46f, 0.38f);

        [Tooltip("Mass of one cubic metre, in tonnes.")]
        [SerializeField, Min(0f)] private float tonnesPerCubicMetre = 2.5f;

        [Tooltip("What mining it yields. Empty for material that is not worth collecting.")]
        [SerializeField] private CargoItemDefinition minedItem;

        public string DisplayName => displayName;
        public Color VeinColor => veinColor;
        public float VeinStrength => veinStrength;
        public Color ChipColor => chipColor;
        public float TonnesPerCubicMetre => tonnesPerCubicMetre;
        public CargoItemDefinition MinedItem => minedItem;
    }
}
