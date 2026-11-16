namespace DeadCargo.Destruction.Voxels
{
    /// <summary>The shape of rock a cut removes.</summary>
    public enum CarveShape : byte
    {
        /// <summary>A ball: impacts, blasts, drills.</summary>
        Sphere,

        /// <summary>A thin disc: saws and cutting beams that slice a rock apart.</summary>
        Slab,

        /// <summary>A ragged crack right through the rock, placed so a set share of it lies beyond: rock breaking apart.</summary>
        Fracture
    }
}
