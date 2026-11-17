using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace DeadCargo.Sectors.Field
{
    /// <summary>
    /// How thick the asteroid fields are across the whole sector, clumps and gaps included, as one small volume
    /// texture that shaders read: field dust lies where the rock is. Built once in a Burst job when the field is
    /// built, and published as shader globals; anything drawn in the field can sample it for the cost of one lookup.
    /// </summary>
    internal sealed class FieldDensityMap : IDisposable
    {
        // Samples along a cell's edge, and the most along the whole sector's.
        private const int SamplesPerCell = 4;
        private const int MaxSamples = 256;

        // The globals ComicSurface-style shaders read. FieldDust.shader declares them with these names.
        private static readonly int MapId = Shader.PropertyToID("_FieldDensityMap");

        // xyz: the map's lowest corner in world space; w: 1 while a map is published, else 0.
        private static readonly int OriginId = Shader.PropertyToID("_FieldDensityOrigin");

        // xyz: one over the map's size in metres.
        private static readonly int ScaleId = Shader.PropertyToID("_FieldDensityScale");

        // The density at the camera, 0 to 1, for effects that only need to know how deep in a field it is.
        private static readonly int HereId = Shader.PropertyToID("_FieldDensityHere");

        private Texture3D texture;

        /// <summary>Builds the map for a sector and publishes it.</summary>
        /// <param name="worldMin">World position of the grid's lowest corner.</param>
        public void Build(NativeArray<RegionVolume> regions, in FieldSettings settings, in SectorGrid grid, uint sectorSeed, Vector3 worldMin)
        {
            Clear();

            var cells = new int3(grid.CountX, grid.CountY, grid.CountZ);
            int3 size = math.min(cells * SamplesPerCell, MaxSamples);
            float3 extent = (float3)cells * grid.CellSize;
            var values = new NativeArray<byte>(size.x * size.y * size.z, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            new FieldDensityJob
            {
                Regions = regions,
                Settings = settings,
                ClumpOffset = FieldGeneration.ClumpOffset(sectorSeed),
                Min = grid.Min,
                Step = extent / size,
                Size = size,
                Values = values
            }.Schedule(values.Length, 2048).Complete();

            texture = new Texture3D(size.x, size.y, size.z, TextureFormat.R8, false)
            {
                name = "Field Density",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixelData(values, 0);
            texture.Apply(false, true);
            values.Dispose();

            Shader.SetGlobalTexture(MapId, texture);
            Shader.SetGlobalVector(OriginId, new Vector4(worldMin.x, worldMin.y, worldMin.z, 1f));
            Shader.SetGlobalVector(ScaleId, new Vector4(1f / extent.x, 1f / extent.y, 1f / extent.z, 0f));
        }

        /// <summary>Publishes the density at the camera.</summary>
        public static void SetDensityHere(float density)
        {
            Shader.SetGlobalFloat(HereId, density);
        }

        /// <summary>Withdraws the map: shaders see no field anywhere.</summary>
        public void Clear()
        {
            Shader.SetGlobalVector(OriginId, Vector4.zero);
            Shader.SetGlobalFloat(HereId, 0f);
            if (texture == null)
                return;

            UnityEngine.Object.Destroy(texture);
            texture = null;
        }

        public void Dispose()
        {
            Clear();
        }
    }

    /// <summary>One sample of the density map per index: the asteroid fields' density there, clumps and gaps included.</summary>
    [BurstCompile(FloatMode = FloatMode.Fast)]
    internal struct FieldDensityJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<RegionVolume> Regions;
        public FieldSettings Settings;
        public float3 ClumpOffset;

        /// <summary>The grid's lowest corner in sector space, and the distance between samples.</summary>
        public float3 Min;
        public float3 Step;
        public int3 Size;

        [WriteOnly] public NativeArray<byte> Values;

        public void Execute(int index)
        {
            int x = index % Size.x;
            int y = index / Size.x % Size.y;
            int z = index / (Size.x * Size.y);
            float3 position = Min + (new float3(x, y, z) + 0.5f) * Step;
            float density = SectorDensity.DensityOf(new NativeRegionSet(Regions), SectorRegionKind.AsteroidField, position) *
                            FieldGeneration.Clump(Settings, ClumpOffset, position);
            Values[index] = (byte)math.round(math.saturate(density) * 255f);
        }
    }
}
