using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace DeadCargo.Sectors.Field
{
    /// <summary>
    /// Shapes the finest icosphere into each rock shape: every vertex moved out to the shape's radius in its direction,
    /// with the exact surface normal there. One index per vertex of every shape, shape by shape.
    /// </summary>
    [BurstCompile]
    public struct RockShapeJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> Directions;
        [ReadOnly] public NativeArray<float3> Offsets;

        [WriteOnly] public NativeArray<float3> Positions;
        [WriteOnly] public NativeArray<float3> Normals;

        public void Execute(int index)
        {
            int count = Directions.Length;
            int shape = index / count;
            float3 direction = Directions[index - shape * count];
            float radius = FieldRockShape.Radius(direction, Offsets[shape], out float3 normal);
            Positions[index] = direction * radius;
            Normals[index] = normal;
        }
    }
}
