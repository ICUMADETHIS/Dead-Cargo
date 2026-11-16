namespace DeadCargo.Destruction.Voxels
{
    /// <summary>How big a chunk a fracture breaks off a rock. See <see cref="VoxelBody.Fracture(FractureSize, DamageKind)"/>.</summary>
    public enum FractureSize
    {
        /// <summary>4 to 10% of the rock.</summary>
        Small,

        /// <summary>12 to 25% of the rock.</summary>
        Medium,

        /// <summary>30 to 50% of the rock: up to a clean break in two.</summary>
        Large
    }
}
