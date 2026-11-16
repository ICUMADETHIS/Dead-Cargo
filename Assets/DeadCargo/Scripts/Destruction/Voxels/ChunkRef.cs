namespace DeadCargo.Destruction.Voxels
{
    /// <summary>
    /// One chunk of one body, as queued for meshing. Carries the body's grid version, so a reference queued before the
    /// body was rebuilt or reused is recognised as stale and dropped.
    /// </summary>
    internal readonly struct ChunkRef
    {
        public readonly VoxelBody Body;
        public readonly int Index;
        public readonly int Version;

        public ChunkRef(VoxelBody body, int index)
        {
            Body = body;
            Index = index;
            Version = body.Version;
        }

        public bool IsCurrent => Body != null && Body.IsAllocated && !Body.ReleaseRequested && Body.Version == Version;
    }
}
