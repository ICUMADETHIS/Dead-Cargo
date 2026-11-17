namespace DeadCargo.Sectors.Field
{
    /// <summary>
    /// Somewhere generation writes rocks and reads them back. Implemented over native memory in jobs and over a list
    /// in the self-test, so <see cref="FieldGeneration"/> is one implementation for both.
    /// </summary>
    public interface IRockBuffer
    {
        int Count { get; }

        FieldRock Get(int index);

        /// <summary>Adds a rock. Returns false, keeping nothing, when the buffer is full.</summary>
        bool Add(in FieldRock rock);
    }
}
