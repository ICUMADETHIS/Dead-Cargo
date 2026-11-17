using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace DeadCargo.Sectors.Field
{
    /// <summary>
    /// The meshes field rocks are drawn with: every shape at every level of detail, all of radius about 1. Built from
    /// the sector seed in one Burst job, so each sector, and each new seed, has rocks of its own. The meshes carry
    /// positions, normals and a vertex colour, as the comic surface's projected texture expects: no texture
    /// coordinates, so no seams. Each vertex also carries where the next coarser level draws it, and that level's
    /// normal there, in the second and third texture channels, which the comic surface's detail morph moves from.
    /// </summary>
    internal sealed class FieldRockMeshes : IDisposable
    {
        // A vertex colour of white with no strength: plain rock, no ore tint.
        private static readonly Color32 PlainRock = new Color32(255, 255, 255, 0);

        // Texture channels holding each vertex's place and normal on the next coarser level. ComicSurface.shader reads
        // them as TEXCOORD1 and TEXCOORD2.
        private const int CoarseVertexChannel = 1;
        private const int CoarseNormalChannel = 2;

        private readonly Mesh[] meshes = new Mesh[FieldRockShape.MaxShapes * FieldRockShape.Levels];

        public int Shapes { get; private set; }

        public Mesh Get(int shape, int level)
        {
            return meshes[shape * FieldRockShape.Levels + level];
        }

        /// <summary>Builds or rebuilds every shape. Takes a few milliseconds; run at load or on a new seed.</summary>
        public void Build(uint sectorSeed, int shapes)
        {
            Shapes = math.clamp(shapes, 1, FieldRockShape.MaxShapes);
            float3[] finest = Icosphere.Directions;
            int count = finest.Length;

            var directions = new NativeArray<float3>(finest, Allocator.TempJob);
            var offsets = new NativeArray<float3>(Shapes, Allocator.TempJob);
            var positions = new NativeArray<float3>(Shapes * count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var normals = new NativeArray<float3>(Shapes * count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var colors = new NativeArray<Color32>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            for (int s = 0; s < Shapes; ++s)
                offsets[s] = FieldRockShape.ShapeOffset(sectorSeed, s);
            for (int i = 0; i < count; ++i)
                colors[i] = PlainRock;

            new RockShapeJob { Directions = directions, Offsets = offsets, Positions = positions, Normals = normals }
                .Schedule(Shapes * count, 256).Complete();

            var coarseVertices = new NativeArray<float3>(count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            var coarseNormals = new NativeArray<float3>(count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            var bounds = new Bounds(Vector3.zero, Vector3.one * (2f * FieldRockShape.MaxRadius));
            for (int s = 0; s < Shapes; ++s)
            {
                for (int level = 0; level < FieldRockShape.Levels; ++level)
                {
                    int slot = s * FieldRockShape.Levels + level;
                    Mesh mesh = meshes[slot];
                    if (mesh == null)
                        meshes[slot] = mesh = new Mesh { name = $"Field Rock {s + 1} (detail {level})" };
                    else
                        mesh.Clear();

                    int vertices = Icosphere.VertexCount(level);
                    FillCoarse(positions, normals, s * count, level, coarseVertices, coarseNormals);
                    mesh.SetVertices(positions, s * count, vertices);
                    mesh.SetNormals(normals, s * count, vertices);
                    mesh.SetColors(colors, 0, vertices);
                    mesh.SetUVs(CoarseVertexChannel, coarseVertices, 0, vertices);
                    mesh.SetUVs(CoarseNormalChannel, coarseNormals, 0, vertices);
                    mesh.SetIndices(Icosphere.Indices(level), MeshTopology.Triangles, 0, false);
                    mesh.bounds = bounds;
                }
            }

            coarseVertices.Dispose();
            coarseNormals.Dispose();
            directions.Dispose();
            offsets.Dispose();
            positions.Dispose();
            normals.Dispose();
            colors.Dispose();
        }

        // Where the level before draws each vertex of a level: a vertex it already had stays put; a vertex this level
        // added sits halfway along the edge it split, with that edge's blended normal, exactly as the coarser mesh shows
        // that point. The coarsest level has nothing before it, so its vertices stay put.
        private static void FillCoarse(NativeArray<float3> positions, NativeArray<float3> normals, int start, int level,
            NativeArray<float3> coarseVertices, NativeArray<float3> coarseNormals)
        {
            int2[] parents = Icosphere.Parents;
            int kept = level > 0 ? Icosphere.VertexCount(level - 1) : Icosphere.VertexCount(0);
            int count = Icosphere.VertexCount(level);
            for (int v = 0; v < count; ++v)
            {
                if (v < kept)
                {
                    coarseVertices[v] = positions[start + v];
                    coarseNormals[v] = normals[start + v];
                    continue;
                }

                int2 edge = parents[v];
                coarseVertices[v] = (positions[start + edge.x] + positions[start + edge.y]) * 0.5f;
                coarseNormals[v] = math.normalizesafe(normals[start + edge.x] + normals[start + edge.y], normals[start + v]);
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < meshes.Length; ++i)
            {
                if (meshes[i] != null)
                    UnityEngine.Object.Destroy(meshes[i]);
                meshes[i] = null;
            }
        }
    }
}
