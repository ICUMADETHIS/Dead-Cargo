using System;
using UnityEngine;

namespace DeadCargo.Destruction.Voxels
{
    /// <summary>One kind of ore running through an asteroid type, as thin sheets of veins.</summary>
    [Serializable]
    public sealed class OreVein
    {
        [SerializeField] private VoxelMaterialDefinition material;

        [Tooltip("Vein spacing, per metre. Higher values give more, closer veins.")]
        [SerializeField, Range(0.01f, 1f)] private float frequency = 0.1f;

        [Tooltip("Share of the rock the veins fill, roughly. Higher values give thicker veins.")]
        [SerializeField, Range(0.01f, 0.5f)] private float thickness = 0.1f;

        [Tooltip("Depth below the surface where the ore starts, in metres.")]
        [SerializeField, Min(0f)] private float minDepth = 1f;

        public VoxelMaterialDefinition Material => material;
        public float Frequency => frequency;
        public float Thickness => thickness;
        public float MinDepth => minDepth;
    }
}
