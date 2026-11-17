using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace DeadCargo.Sectors.Field
{
    /// <summary>
    /// Builds the rocks of a batch of cells, one cell per index, each into its own run of <see cref="Rocks"/>
    /// (<see cref="FieldSettings.CellCapacity"/> long) with its count in <see cref="Counts"/>. Each cell's rocks are
    /// left largest first.
    /// </summary>
    [BurstCompile]
    public struct FieldCellJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<CellCoord> Cells;
        [ReadOnly] public NativeArray<RegionVolume> Regions;
        public FieldSettings Settings;
        public SectorGrid Grid;
        public uint SectorSeed;

        [NativeDisableParallelForRestriction] public NativeArray<FieldRock> Rocks;
        [WriteOnly] public NativeArray<int> Counts;

        public void Execute(int index)
        {
            int capacity = Settings.CellCapacity;
            var rocks = new SlotRockBuffer(Rocks, index * capacity, capacity);
            var scratch = new NativeArray<FieldRock>(FieldGeneration.NearbyCapacity(Settings, Grid.CellSize), Allocator.Temp,
                NativeArrayOptions.UninitializedMemory);
            var nearby = new SlotRockBuffer(scratch, 0, scratch.Length);

            FieldGeneration.Cell(Settings, new NativeRegionSet(Regions), Grid, SectorSeed, Cells[index], ref rocks, ref nearby);
            Counts[index] = rocks.Count;
            scratch.Dispose();

            // Largest first, so whoever reads the cell can stop at the first rock too small to matter to it.
            FieldSort.Sort(Rocks, index * capacity, rocks.Count, new LargestRockFirst());
        }
    }
}
