using Unity.Mathematics;

namespace DeadCargo.Destruction.Voxels
{
    /// <summary>
    /// Builds a smooth surface for one chunk of a voxel grid with naive Surface Nets: one vertex inside every cell the
    /// surface passes through, at the mean of its edge crossings, and one quad across every grid edge that changes
    /// sign. A chunk owns the edges whose lower sample lies inside it and reads a two-sample apron around itself, so
    /// neighbouring chunks compute identical shared vertices and meet with no seam.
    /// Vertices are body-local, in metres. Faces are wound clockwise seen from outside, as Unity expects.
    /// </summary>
    public static unsafe class SurfaceNets
    {
        /// <summary>Cells along each edge of a chunk.</summary>
        public const int ChunkSize = 32;

        /// <summary>Samples along each edge of the block a chunk reads: the chunk and a two-sample apron.</summary>
        public const int BlockSize = ChunkSize + 4;

        /// <summary>Cells along each edge of the cell map: the chunk's cells and one apron cell below.</summary>
        public const int CellMapSize = ChunkSize + 1;

        public const int BlockLength = BlockSize * BlockSize * BlockSize;
        public const int CellMapLength = CellMapSize * CellMapSize * CellMapSize;

        /// <summary>The most vertices a chunk can produce: one per cell in the map. Always fits 16-bit indices.</summary>
        public const int MaxVertices = CellMapLength;

        private const int StrideY = BlockSize;
        private const int StrideZ = BlockSize * BlockSize;
        private const float MinGradient = 1e-6f;

        /// <summary>The chunks needed to cover a grid of the given size, along each axis.</summary>
        public static int3 ChunkCount(int3 gridSize)
        {
            // A chunk owns the edges that start inside it, and the last sample on each axis starts none.
            return (gridSize - 2) / ChunkSize + 1;
        }

        /// <param name="chunkMin">The chunk's first sample, a multiple of <see cref="ChunkSize"/>.</param>
        /// <param name="voxelSize">Sample spacing, in metres.</param>
        /// <param name="origin">Body-local position of sample (0, 0, 0), in metres.</param>
        /// <param name="palette">Colour of each material slot: rgb tint, alpha strength.</param>
        public static void MeshChunk(in VoxelGrid grid, int3 chunkMin, float voxelSize, float3 origin,
            float4* palette, int paletteCount, ref MeshBuffers buffers)
        {
            MeshChunk(grid, chunkMin, voxelSize, origin, palette, paletteCount, 1, ref buffers);
        }

        /// <summary>
        /// Meshes a chunk at reduced detail, reading every <paramref name="stride"/>-th sample: 2 gives a quarter of the
        /// triangles. Neighbouring chunks at the same stride still meet with no seam, since every chunk starts on a
        /// multiple of <see cref="ChunkSize"/>.
        /// </summary>
        /// <param name="stride">1, 2 or 4. Anything else meshes at full detail.</param>
        public static void MeshChunk(in VoxelGrid grid, int3 chunkMin, float voxelSize, float3 origin,
            float4* palette, int paletteCount, int stride, ref MeshBuffers buffers)
        {
            buffers.Reset();
            if (stride != 2 && stride != 4)
                stride = 1;

            int cells = ChunkSize / stride;
            if (!Gather(grid, chunkMin, stride, cells, ref buffers))
                return;

            for (int i = 0; i < CellMapLength; ++i)
                buffers.CellVertex[i] = -1;

            var context = new Context
            {
                ChunkMin = chunkMin,
                Stride = stride,
                VoxelSize = voxelSize,
                Origin = origin,
                Palette = palette,
                PaletteCount = paletteCount
            };

            for (int z = 0; z < cells; ++z)
            {
                for (int y = 0; y < cells; ++y)
                {
                    for (int x = 0; x < cells; ++x)
                    {
                        int b = BlockIndex(x + 2, y + 2, z + 2);
                        bool solid = buffers.Block[b] > 0;
                        var l = new int3(x, y, z);

                        if (solid != buffers.Block[b + 1] > 0)
                            EmitQuad(new int3(x, y - 1, z - 1), new int3(x, y, z - 1), l, new int3(x, y - 1, z), solid, ref context, ref buffers);

                        if (solid != buffers.Block[b + StrideY] > 0)
                            EmitQuad(new int3(x - 1, y, z - 1), new int3(x - 1, y, z), l, new int3(x, y, z - 1), solid, ref context, ref buffers);

                        if (solid != buffers.Block[b + StrideZ] > 0)
                            EmitQuad(new int3(x - 1, y - 1, z), new int3(x, y - 1, z), l, new int3(x - 1, y, z), solid, ref context, ref buffers);
                    }
                }
            }
        }

        private struct Context
        {
            public int3 ChunkMin;
            public int Stride;
            public float VoxelSize;
            public float3 Origin;
            public float4* Palette;
            public int PaletteCount;
        }

        private static int BlockIndex(int x, int y, int z)
        {
            return x + StrideY * y + StrideZ * z;
        }

        // Copies the chunk and its apron out of the grid, every stride-th sample, into the start of the block. Returns
        // false when the block holds no surface.
        private static bool Gather(in VoxelGrid grid, int3 chunkMin, int stride, int cells, ref MeshBuffers buffers)
        {
            int3 start = chunkMin - 2 * stride;
            int span = cells + 4;
            bool anySolid = false;
            bool anyEmpty = false;

            for (int z = 0; z < span; ++z)
            {
                int sz = start.z + z * stride;
                for (int y = 0; y < span; ++y)
                {
                    int sy = start.y + y * stride;
                    bool rowInside = sz >= 0 && sz < grid.Size.z && sy >= 0 && sy < grid.Size.y;
                    int rowStart = rowInside ? grid.Index(0, sy, sz) : 0;
                    int b = BlockIndex(0, y, z);

                    for (int x = 0; x < span; ++x, ++b)
                    {
                        int sx = start.x + x * stride;
                        sbyte density = VoxelEncoding.Empty;
                        byte material = 0;
                        if (rowInside && sx >= 0 && sx < grid.Size.x)
                        {
                            density = grid.Density[rowStart + sx];
                            material = grid.Material[rowStart + sx];
                        }

                        buffers.Block[b] = density;
                        buffers.BlockMaterial[b] = material;
                        if (density > 0)
                            anySolid = true;
                        else
                            anyEmpty = true;
                    }
                }
            }

            return anySolid && anyEmpty;
        }

        // Cells are chunk-local and between -1 and ChunkSize - 1 on each axis. The quad faces +axis when the solid side
        // is the edge's lower sample, and is split along its shorter diagonal.
        private static void EmitQuad(int3 a, int3 b, int3 c, int3 d, bool facesPositive, ref Context context, ref MeshBuffers buffers)
        {
            int ia = GetVertex(a, ref context, ref buffers);
            int ib = GetVertex(b, ref context, ref buffers);
            int ic = GetVertex(c, ref context, ref buffers);
            int id = GetVertex(d, ref context, ref buffers);
            if (ia < 0 || ib < 0 || ic < 0 || id < 0)
                return;

            if (buffers.IndexCount + 6 > buffers.IndexCapacity)
            {
                buffers.Overflow = true;
                return;
            }

            float3 pa = buffers.Vertices[ia].Position;
            float3 pb = buffers.Vertices[ib].Position;
            float3 pc = buffers.Vertices[ic].Position;
            float3 pd = buffers.Vertices[id].Position;
            bool splitAc = math.lengthsq(pa - pc) <= math.lengthsq(pb - pd);

            ushort* o = buffers.Indices + buffers.IndexCount;
            if (splitAc)
            {
                if (facesPositive)
                    Write(o, ia, ib, ic, ia, ic, id);
                else
                    Write(o, ia, ic, ib, ia, id, ic);
            }
            else
            {
                if (facesPositive)
                    Write(o, ia, ib, id, ib, ic, id);
                else
                    Write(o, ia, id, ib, ib, id, ic);
            }

            buffers.IndexCount += 6;
        }

        private static void Write(ushort* o, int i0, int i1, int i2, int i3, int i4, int i5)
        {
            o[0] = (ushort)i0;
            o[1] = (ushort)i1;
            o[2] = (ushort)i2;
            o[3] = (ushort)i3;
            o[4] = (ushort)i4;
            o[5] = (ushort)i5;
        }

        private static int GetVertex(int3 cell, ref Context context, ref MeshBuffers buffers)
        {
            int3 m = cell + 1;
            int mapIndex = m.x + CellMapSize * (m.y + CellMapSize * m.z);
            int existing = buffers.CellVertex[mapIndex];
            if (existing >= 0)
                return existing;

            if (buffers.VertexCount >= buffers.VertexCapacity)
            {
                buffers.Overflow = true;
                return -1;
            }

            int b0 = BlockIndex(cell.x + 2, cell.y + 2, cell.z + 2);
            float* v = stackalloc float[8];
            for (int k = 0; k < 8; ++k)
                v[k] = buffers.Block[b0 + CornerOffset(k)];

            float3 sum = float3.zero;
            int crossings = 0;
            for (int k = 0; k < 8; k += 2)
                AddCrossing(v, k, k + 1, ref sum, ref crossings);
            for (int k = 0; k < 8; k = (k & 1) == 0 ? k + 1 : k + 3)
                AddCrossing(v, k, k + 2, ref sum, ref crossings);
            for (int k = 0; k < 4; ++k)
                AddCrossing(v, k, k + 4, ref sum, ref crossings);

            float3 local = crossings > 0 ? sum / crossings : new float3(0.5f);

            // Outward normal: down the density gradient, from central differences at the corners blended to the vertex.
            float3 gradient = float3.zero;
            float4 color = float4.zero;
            float colorWeight = 0f;
            for (int k = 0; k < 8; ++k)
            {
                float3 corner = Corner(k);
                float3 w3 = math.select(1f - local, local, corner > 0.5f);
                int bk = b0 + CornerOffset(k);
                gradient += w3.x * w3.y * w3.z * Gradient(buffers.Block, bk);

                if (v[k] > 0f)
                {
                    int slot = buffers.BlockMaterial[bk];
                    color += context.Palette[slot < context.PaletteCount ? slot : 0] * v[k];
                    colorWeight += v[k];
                }
            }

            float gradientLength = math.length(gradient);
            float3 normal = gradientLength > MinGradient ? -gradient / gradientLength : new float3(0f, 1f, 0f);
            // The whole-voxel part is added in integers, so a vertex shared by two chunks comes out bit-for-bit the same
            // from both and their surfaces meet with no gap. At full detail this is exactly the original sum.
            float3 position = context.Origin + ((float3)(context.ChunkMin + cell * context.Stride) + local * context.Stride) * context.VoxelSize;

            int index = buffers.VertexCount++;
            buffers.Vertices[index] = new VoxelVertex
            {
                Position = position,
                Normal = VoxelVertex.PackNormal(normal),
                Color = VoxelVertex.PackColor(colorWeight > 0f ? color / colorWeight : context.Palette[0])
            };

            buffers.BoundsMin = math.min(buffers.BoundsMin, position);
            buffers.BoundsMax = math.max(buffers.BoundsMax, position);
            buffers.CellVertex[mapIndex] = index;
            return index;
        }

        private static int CornerOffset(int k)
        {
            return (k & 1) + ((k >> 1) & 1) * StrideY + ((k >> 2) & 1) * StrideZ;
        }

        private static float3 Corner(int k)
        {
            return new float3(k & 1, (k >> 1) & 1, (k >> 2) & 1);
        }

        private static void AddCrossing(float* v, int a, int b, ref float3 sum, ref int crossings)
        {
            if (v[a] > 0f == v[b] > 0f)
                return;

            float t = v[a] / (v[a] - v[b]);
            sum += math.lerp(Corner(a), Corner(b), t);
            ++crossings;
        }

        private static float3 Gradient(sbyte* block, int b)
        {
            return new float3(
                block[b + 1] - block[b - 1],
                block[b + StrideY] - block[b - StrideY],
                block[b + StrideZ] - block[b - StrideZ]) * 0.5f;
        }
    }
}
