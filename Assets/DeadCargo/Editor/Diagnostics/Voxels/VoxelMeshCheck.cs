using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace DeadCargo.Destruction.Voxels.Diagnostics
{
    /// <summary>
    /// Meshes every chunk of a grid and measures the result as one surface: whether it is closed, how much it encloses,
    /// and whether its faces point out. Shared vertices on chunk seams must match exactly for the surface to close.
    /// </summary>
    internal sealed unsafe class VoxelMeshCheck
    {
        private readonly Dictionary<EdgeKey, int> edges = new Dictionary<EdgeKey, int>();

        public int Chunks;
        public int Triangles;
        public int Vertices;
        public int OpenEdges;
        public int BadValues;
        public int OutwardTriangles;
        public bool Overflow;
        public double Volume;
        public double MaxRadiusError;
        public double MeshMilliseconds;
        public int OreVertices;
        public readonly List<float3> Positions = new List<float3>();

        public bool IsClosed => OpenEdges == 0;

        /// <param name="centre">Body-local point the surface should face away from, for the outward count.</param>
        /// <param name="sphereRadius">When above zero, how far every vertex should be from the centre.</param>
        public static VoxelMeshCheck Run(VoxelTestMemory memory, in VoxelGrid grid, float voxelSize, float3 origin,
            float4* palette, int paletteCount, float3 centre, float sphereRadius)
        {
            var check = new VoxelMeshCheck();
            MeshBuffers buffers = memory.MeshBuffers();
            int3 chunks = SurfaceNets.ChunkCount(grid.Size);
            var timer = System.Diagnostics.Stopwatch.StartNew();

            for (int z = 0; z < chunks.z; ++z)
            {
                for (int y = 0; y < chunks.y; ++y)
                {
                    for (int x = 0; x < chunks.x; ++x)
                    {
                        timer.Start();
                        SurfaceNets.MeshChunk(grid, new int3(x, y, z) * SurfaceNets.ChunkSize, voxelSize, origin, palette, paletteCount, ref buffers);
                        timer.Stop();
                        check.Add(ref buffers, centre, sphereRadius);
                    }
                }
            }

            check.Chunks = chunks.x * chunks.y * chunks.z;
            check.MeshMilliseconds = timer.Elapsed.TotalMilliseconds;
            foreach (KeyValuePair<EdgeKey, int> edge in check.edges)
            {
                if (edge.Value != 0)
                    check.OpenEdges++;
            }

            return check;
        }

        private void Add(ref MeshBuffers buffers, float3 centre, float sphereRadius)
        {
            Overflow |= buffers.Overflow;
            Vertices += buffers.VertexCount;
            for (int i = 0; i < buffers.VertexCount; ++i)
            {
                VoxelVertex vertex = buffers.Vertices[i];
                float3 p = vertex.Position;
                float3 n = VoxelVertex.UnpackNormal(vertex.Normal);
                if (!math.all(math.isfinite(p)) || math.lengthsq(n) < 0.5f)
                    BadValues++;

                if (VoxelVertex.UnpackColor(vertex.Color).w > 0.5f)
                    OreVertices++;

                if (sphereRadius > 0f)
                    MaxRadiusError = Math.Max(MaxRadiusError, Math.Abs(math.distance(p, centre) - sphereRadius));

                Positions.Add(p);
            }

            for (int t = 0; t < buffers.IndexCount; t += 3)
            {
                float3 a = buffers.Vertices[buffers.Indices[t]].Position;
                float3 b = buffers.Vertices[buffers.Indices[t + 1]].Position;
                float3 c = buffers.Vertices[buffers.Indices[t + 2]].Position;
                Triangles++;

                // Signed volume against the body origin: positive for faces wound clockwise seen from outside.
                Volume += math.dot(a, math.cross(b, c)) / 6.0;

                float3 normal = math.cross(b - a, c - a);
                if (math.dot(normal, (a + b + c) / 3f - centre) > 0f)
                    OutwardTriangles++;

                AddEdge(a, b);
                AddEdge(b, c);
                AddEdge(c, a);
            }
        }

        // Each directed edge must be matched by the same edge running the other way somewhere in the surface.
        private void AddEdge(float3 from, float3 to)
        {
            var forward = new EdgeKey(from, to);
            var backward = new EdgeKey(to, from);
            if (edges.TryGetValue(backward, out int pending) && pending > 0)
            {
                if (pending == 1)
                    edges.Remove(backward);
                else
                    edges[backward] = pending - 1;
                return;
            }

            edges.TryGetValue(forward, out int count);
            edges[forward] = count + 1;
        }

        private readonly struct EdgeKey : IEquatable<EdgeKey>
        {
            private readonly int ax, ay, az, bx, by, bz;

            public EdgeKey(float3 a, float3 b)
            {
                ax = BitConverter.SingleToInt32Bits(a.x + 0f);
                ay = BitConverter.SingleToInt32Bits(a.y + 0f);
                az = BitConverter.SingleToInt32Bits(a.z + 0f);
                bx = BitConverter.SingleToInt32Bits(b.x + 0f);
                by = BitConverter.SingleToInt32Bits(b.y + 0f);
                bz = BitConverter.SingleToInt32Bits(b.z + 0f);
            }

            public bool Equals(EdgeKey other)
            {
                return ax == other.ax && ay == other.ay && az == other.az && bx == other.bx && by == other.by && bz == other.bz;
            }

            public override bool Equals(object obj)
            {
                return obj is EdgeKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = ax;
                    hash = hash * 397 ^ ay;
                    hash = hash * 397 ^ az;
                    hash = hash * 397 ^ bx;
                    hash = hash * 397 ^ by;
                    return hash * 397 ^ bz;
                }
            }
        }
    }
}
