using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Unity.Mathematics;

namespace DeadCargo.Destruction.Voxels.Diagnostics
{
    /// <summary>
    /// Checks the voxel core that asteroid destruction is built on: encoding, generation, meshing, carving, splitting
    /// and the accounting mining relies on. Pure checks with no scene or engine access, so they run in the editor and
    /// outside Unity alike.
    /// </summary>
    public static unsafe class VoxelSelfTest
    {
        private const float VoxelSize = 0.5f;
        private const int IslandCapacity = 16;

        /// <summary>The outcome of a run: one line per check, and the checks that failed.</summary>
        public sealed class Result
        {
            public readonly StringBuilder Report = new StringBuilder();
            public readonly List<string> Failures = new List<string>();
            public int Checks;

            public bool Passed => Failures.Count == 0;

            public void Check(bool passed, string name, string detail)
            {
                ++Checks;
                Report.Append(passed ? "  pass  " : "  FAIL  ").Append(name).Append("  ·  ").AppendLine(detail);
                if (!passed)
                    Failures.Add($"{name}: {detail}");
            }

            public void Note(string name, string detail)
            {
                Report.Append("  info  ").Append(name).Append("  ·  ").AppendLine(detail);
            }
        }

        public static Result Run()
        {
            var result = new Result();
            using (var memory = new VoxelTestMemory())
            {
                float4* palette = memory.Alloc<float4>(VoxelEncoding.MaterialSlots);
                palette[0] = new float4(1f, 1f, 1f, 0f);
                palette[1] = new float4(0.1f, 0.8f, 0.7f, 1f);

                Run(result, "Encoding", () => CheckEncoding(result));
                Run(result, "Sphere", () => CheckSphere(result, memory, palette));
                Run(result, "Reduced detail", () => CheckReducedDetail(result, memory, palette));
                Run(result, "Asteroid", () => CheckAsteroid(result, memory, palette));
                Run(result, "Determinism", () => CheckDeterminism(result, memory));
                Run(result, "Carving", () => CheckCarving(result, memory, palette));
                Run(result, "Islands", () => CheckIslands(result, memory, palette));
                Run(result, "Slab cut", () => CheckSlabCut(result, memory, palette));
                Run(result, "Ore", () => CheckOre(result, memory, palette));
                Run(result, "Overflow", () => CheckOverflow(result, memory, palette));
                Run(result, "Depletion", () => CheckDepletion(result, memory, palette));
            }

            return result;
        }

        private static void Run(Result result, string name, Action check)
        {
            try
            {
                check();
            }
            catch (Exception exception)
            {
                result.Check(false, name, "threw " + exception.GetType().Name + ": " + exception.Message);
            }
        }

        private static void CheckEncoding(Result result)
        {
            bool monotonic = true;
            for (float d = -3f; d < 3f; d += 0.01f)
                monotonic &= VoxelEncoding.Encode(d) <= VoxelEncoding.Encode(d + 0.01f);

            result.Check(monotonic, "Encoding is monotonic", "distance -3 to 3 voxels");
            result.Check(VoxelEncoding.Encode(10f) == VoxelEncoding.Full && VoxelEncoding.Encode(-10f) == VoxelEncoding.Empty,
                "Encoding saturates", $"full {VoxelEncoding.Encode(10f)}, empty {VoxelEncoding.Encode(-10f)}");
            result.Check(Math.Abs(VoxelEncoding.Occupancy(0) - 0.5f) < 1e-6f && VoxelEncoding.Occupancy(VoxelEncoding.Full) == 1f &&
                         VoxelEncoding.Occupancy(VoxelEncoding.Empty) == 0f,
                "Occupancy", "half at the surface, whole inside, none outside");

            float3 n = math.normalize(new float3(0.3f, -0.8f, 0.5f));
            float error = math.length(VoxelVertex.UnpackNormal(VoxelVertex.PackNormal(n)) - n);
            float4 c = new float4(0.2f, 0.4f, 0.6f, 0.8f);
            float colorError = math.cmax(math.abs(VoxelVertex.UnpackColor(VoxelVertex.PackColor(c)) - c));
            result.Check(error < 0.02f && colorError < 0.003f, "Vertex packing", $"normal error {error:0.0000}, colour error {colorError:0.0000}");
        }

        private static void CheckSphere(Result result, VoxelTestMemory memory, float4* palette)
        {
            const float radius = 20f;
            VoxelGrid grid = GridFor(memory, radius, out float3 origin);
            var shape = new AsteroidShape { Radii = new float3(radius) };
            var timer = Stopwatch.StartNew();
            VoxelGeneration.Fill(grid, shape, VoxelSize, origin, 0, grid.Size.z);
            double generateMs = timer.Elapsed.TotalMilliseconds;

            float expected = 4f / 3f * math.PI * radius * radius * radius;
            float stored = StoredVolume(memory, grid);
            result.Check(Within(stored, expected, 0.01f), "Stored volume", $"{stored:N0} m³ of {expected:N0} m³ ({Error(stored, expected)})");

            VoxelMeshCheck mesh = VoxelMeshCheck.Run(memory, grid, VoxelSize, origin, palette, 2, float3.zero, radius);
            result.Check(mesh.IsClosed, "Surface is closed", $"{mesh.Triangles:N0} triangles over {mesh.Chunks} chunks, {mesh.OpenEdges} open edges");
            result.Check(mesh.BadValues == 0 && !mesh.Overflow, "Vertices are valid", $"{mesh.Vertices:N0} vertices, {mesh.BadValues} bad");
            result.Check(Within((float)mesh.Volume, expected, 0.02f), "Mesh volume", $"{mesh.Volume:N0} m³ ({Error((float)mesh.Volume, expected)})");
            result.Check(mesh.MaxRadiusError < VoxelSize * 0.6f, "Surface position", $"max error {mesh.MaxRadiusError:0.000} m");
            result.Check(mesh.OutwardTriangles == mesh.Triangles, "Faces point out", $"{mesh.OutwardTriangles:N0} of {mesh.Triangles:N0}");
            result.Note("Timing", $"{grid.Count:N0} voxels generated in {generateMs:0} ms, meshed in {mesh.MeshMilliseconds:0} ms (plain C#; Burst in game)");
        }

        // Distant rock meshes every second or fourth sample: far fewer triangles, but still closed and still the same rock.
        private static void CheckReducedDetail(Result result, VoxelTestMemory memory, float4* palette)
        {
            const float radius = 20f;
            VoxelGrid grid = GridFor(memory, radius, out float3 origin);
            VoxelGeneration.Fill(grid, new AsteroidShape { Radii = new float3(radius) }, VoxelSize, origin, 0, grid.Size.z);
            float expected = 4f / 3f * math.PI * radius * radius * radius;
            VoxelMeshCheck full = VoxelMeshCheck.Run(memory, grid, VoxelSize, origin, palette, 2, float3.zero, radius);

            for (int stride = 2; stride <= 4; stride *= 2)
            {
                string name = stride == 2 ? "Half detail" : "Quarter detail";
                float tolerance = stride == 2 ? 0.04f : 0.1f;
                float cell = VoxelSize * stride;
                VoxelMeshCheck mesh = VoxelMeshCheck.Run(memory, grid, VoxelSize, origin, palette, 2, float3.zero, radius, stride);

                result.Check(mesh.IsClosed && mesh.BadValues == 0 && !mesh.Overflow, name + " is closed",
                    $"{mesh.Triangles:N0} triangles, {mesh.OpenEdges} open edges, {mesh.BadValues} bad vertices");
                result.Check(Within((float)mesh.Volume, expected, tolerance) && mesh.MaxRadiusError < cell * 0.75f, name + " keeps the shape",
                    $"volume {Error((float)mesh.Volume, expected)}, surface within {mesh.MaxRadiusError:0.00} m of a {cell:0.0} m cell");
                result.Check(mesh.Triangles * stride * stride < full.Triangles * 1.5f && mesh.OutwardTriangles == mesh.Triangles,
                    name + " is lighter", $"{mesh.Triangles:N0} of {full.Triangles:N0} triangles, all facing out");
            }
        }

        private static void CheckAsteroid(Result result, VoxelTestMemory memory, float4* palette)
        {
            AsteroidShape shape = RockShape(25f, new float3(11.3f, 4.1f, -7.9f));
            VoxelGrid grid = GridFor(memory, shape.MaxRadius, out float3 origin);
            VoxelGeneration.Fill(grid, shape, VoxelSize, origin, 0, grid.Size.z);

            int outside = 0;
            int hollow = 0;
            float outerLimit = shape.MaxRadius + VoxelSize;
            float innerLimit = shape.MinRadius - VoxelSize * 3f;
            for (int i = 0; i < grid.Count; ++i)
            {
                float distance = math.length(origin + (float3)grid.Coord(i) * VoxelSize);
                if (grid.Density[i] > 0 && distance > outerLimit)
                    outside++;
                if (grid.Density[i] <= 0 && distance < innerLimit)
                    hollow++;
            }

            result.Check(outside == 0 && hollow == 0, "Rock stays in its bounds", $"{outside} solid beyond the maximum radius, {hollow} empty inside the core");

            VoxelMeshCheck mesh = VoxelMeshCheck.Run(memory, grid, VoxelSize, origin, palette, 2, float3.zero, 0f);
            result.Check(mesh.IsClosed && mesh.BadValues == 0 && !mesh.Overflow, "Cratered rock meshes cleanly",
                $"{mesh.Triangles:N0} triangles, {mesh.OpenEdges} open edges, {mesh.BadValues} bad vertices");
            float stored = StoredVolume(memory, grid);
            result.Check(Within((float)mesh.Volume, stored, 0.03f), "Mesh matches stored rock", $"mesh {mesh.Volume:N0} m³, stored {stored:N0} m³");

            // Worst case for the piece search: a diagonal saw cut across the whole rock, searched over the whole grid.
            float* removed = memory.Alloc<float>(VoxelEncoding.MaterialSlots);
            var cut = new CarveRequest
            {
                Shape = CarveShape.Slab,
                Centre = -origin / VoxelSize + new float3(6f, 0f, 0f),
                Normal = math.normalize(new float3(1f, 1f, 1f)),
                HalfThickness = 1.5f,
                Radius = grid.Size.x
            };

            VoxelCarving.Carve(grid, cut, removed, out _, out _);
            IslandBuffers islands = memory.IslandBuffers(grid.Count, IslandCapacity);
            var timer = Stopwatch.StartNew();
            VoxelIslands.Find(grid, int3.zero, grid.Size - 1, ref islands);
            double searchMs = timer.Elapsed.TotalMilliseconds;
            int pieceVoxels = 0;
            for (int i = 0; i < islands.IslandCount; ++i)
                pieceVoxels += islands.Islands[i].Count;

            result.Check(!islands.SeedsTruncated && islands.IslandCount >= 1, "A cut across a whole rock is searched in full",
                $"{islands.IslandCount} piece(s) of {pieceVoxels:N0} voxels, {islands.Visited:N0} visited in {searchMs:0} ms (plain C#)");
        }

        private static void CheckDeterminism(Result result, VoxelTestMemory memory)
        {
            AsteroidShape shape = RockShape(12f, new float3(3.7f, -1.2f, 9.4f));
            VoxelGrid a = GridFor(memory, shape.MaxRadius, out float3 origin);
            VoxelGrid b = memory.Grid(a.Size);
            VoxelGrid c = memory.Grid(a.Size);
            VoxelGeneration.Fill(a, shape, VoxelSize, origin, 0, a.Size.z);

            // Filled in two halves, as parallel jobs do.
            VoxelGeneration.Fill(b, shape, VoxelSize, origin, a.Size.z / 2, a.Size.z);
            VoxelGeneration.Fill(b, shape, VoxelSize, origin, 0, a.Size.z / 2);

            shape.Offset += new float3(5f, 0f, 0f);
            VoxelGeneration.Fill(c, shape, VoxelSize, origin, 0, a.Size.z);

            int same = 0;
            int changed = 0;
            for (int i = 0; i < a.Count; ++i)
            {
                if (a.Density[i] != b.Density[i] || a.Material[i] != b.Material[i])
                    same++;
                if (a.Density[i] != c.Density[i])
                    changed++;
            }

            result.Check(same == 0, "Same seed, same rock", $"{same} samples differ, including when filled in slices");
            result.Check(changed > a.Count / 100, "New seed, new rock", $"{changed:N0} samples differ");
        }

        private static void CheckCarving(Result result, VoxelTestMemory memory, float4* palette)
        {
            const float radius = 20f;
            const float craterRadius = 3f;
            VoxelGrid grid = GridFor(memory, radius, out float3 origin);
            VoxelGeneration.Fill(grid, new AsteroidShape { Radii = new float3(radius) }, VoxelSize, origin, 0, grid.Size.z);
            float before = StoredVolume(memory, grid);

            float* removed = memory.Alloc<float>(VoxelEncoding.MaterialSlots);
            float3 centre = (new float3(radius, 0f, 0f) - origin) / VoxelSize;
            bool changed = VoxelCarving.CarveSphere(grid, centre, craterRadius / VoxelSize, 0f, float3.zero, removed, out int3 min, out int3 max);

            float removedVolume = Sum(removed) * VoxelSize * VoxelSize * VoxelSize;
            float lens = LensVolume(radius, craterRadius, radius);
            float after = StoredVolume(memory, grid);
            bool boundsHold = math.all(centre >= (float3)min) && math.all(centre <= (float3)max);

            result.Check(changed && boundsHold, "Crater reports its bounds", $"{min} to {max} around {centre}");
            result.Check(Within(removedVolume, lens, 0.15f), "Crater size", $"{removedVolume:0.0} m³ removed, sphere overlap {lens:0.0} m³ ({Error(removedVolume, lens)})");
            result.Check(Math.Abs(before - after - removedVolume) < 0.01f, "Every bit removed is accounted for",
                $"stored rock fell by {before - after:0.000} m³, reported {removedVolume:0.000} m³");

            VoxelMeshCheck mesh = VoxelMeshCheck.Run(memory, grid, VoxelSize, origin, palette, 2, float3.zero, 0f);
            result.Check(mesh.IsClosed && mesh.BadValues == 0, "Cratered surface is closed", $"{mesh.OpenEdges} open edges");

            float* again = memory.Alloc<float>(VoxelEncoding.MaterialSlots);
            bool repeat = VoxelCarving.CarveSphere(grid, centre, craterRadius / VoxelSize, 0f, float3.zero, again, out _, out _);
            result.Check(!repeat && Sum(again) == 0f, "Carving the same hole again removes nothing", $"{Sum(again):0.000} voxels");

            float* rough = memory.Alloc<float>(VoxelEncoding.MaterialSlots);
            float3 other = (new float3(0f, radius, 0f) - origin) / VoxelSize;
            VoxelCarving.CarveSphere(grid, other, craterRadius / VoxelSize, 1f, new float3(3f, 1f, 2f), rough, out _, out _);
            VoxelMeshCheck roughMesh = VoxelMeshCheck.Run(memory, grid, VoxelSize, origin, palette, 2, float3.zero, 0f);
            result.Check(Sum(rough) > 0f && roughMesh.IsClosed && roughMesh.BadValues == 0, "Rough crater",
                $"{Sum(rough) * VoxelSize * VoxelSize * VoxelSize:0.0} m³ removed, {roughMesh.OpenEdges} open edges");
        }

        private static void CheckIslands(Result result, VoxelTestMemory memory, float4* palette)
        {
            // Two rocks joined by a bar, the right one smaller. Cutting the bar must free the right one.
            var leftCentre = new float3(-14f, 0f, 0f);
            var rightCentre = new float3(14f, 0f, 0f);
            const float leftRadius = 9f;
            const float rightRadius = 6f;
            const float barRadius = 2.5f;
            VoxelGrid grid = GridFor(memory, 24f, out float3 origin);

            for (int i = 0; i < grid.Count; ++i)
            {
                int3 c = grid.Coord(i);
                float3 p = origin + (float3)c * VoxelSize;
                float barDistance = barRadius - math.length(new float2(p.y, p.z));
                if (math.abs(p.x) > 14f)
                    barDistance = -1e3f;

                float solid = math.max(math.max(leftRadius - math.distance(p, leftCentre), rightRadius - math.distance(p, rightCentre)), barDistance);
                bool border = math.any(c == 0) || math.any(c == grid.Size - 1);
                grid.Density[i] = border ? VoxelEncoding.Empty : VoxelEncoding.Encode(solid / VoxelSize);
                grid.Material[i] = p.x > 0f ? (byte)1 : (byte)0;
            }

            IslandBuffers islands = memory.IslandBuffers(grid.Count, IslandCapacity);
            float3 cut = (new float3(3f, 0f, 0f) - origin) / VoxelSize;
            int3 regionMin = (int3)math.floor(cut) - 8;
            int3 regionMax = (int3)math.ceil(cut) + 8;

            VoxelIslands.Find(grid, regionMin, regionMax, ref islands);
            int intactVisits = islands.Visited;
            result.Check(islands.IslandCount == 0, "Whole rock has no loose pieces", $"{islands.IslandCount} found");
            result.Check(intactVisits < grid.Count / 50, "Search stops early when nothing splits", $"{intactVisits:N0} of {grid.Count:N0} voxels visited");

            int solidBefore = CountSolid(grid);
            float* removed = memory.Alloc<float>(VoxelEncoding.MaterialSlots);
            VoxelCarving.CarveSphere(grid, cut, 3.5f / VoxelSize, 0f, float3.zero, removed, out _, out _);
            int solidAfterCut = CountSolid(grid);

            int rightSide = 0;
            for (int i = 0; i < grid.Count; ++i)
            {
                if (grid.Density[i] > 0 && origin.x + grid.Coord(i).x * VoxelSize > 3f)
                    rightSide++;
            }

            VoxelIslands.Find(grid, regionMin, regionMax, ref islands);
            bool one = islands.IslandCount == 1 && !islands.Islands[0].IsOverflow;
            result.Check(one, "Cutting the bar frees one piece", $"{islands.IslandCount} found");
            if (!one)
                return;

            IslandInfo island = islands.Islands[0];
            result.Check(island.Count == rightSide, "The freed piece is the smaller rock", $"{island.Count:N0} voxels, {rightSide:N0} right of the cut");
            result.Check(islands.Visited < island.Count * 3 + intactVisits * 2, "Split search stays near the smaller piece",
                $"{islands.Visited:N0} visited for a {island.Count:N0}-voxel piece");

            const int margin = 3;
            int3 targetSize = island.Max - island.Min + 1 + margin * 2;
            VoxelGrid target = memory.Grid(targetSize);
            VoxelIslands.Extract(grid, islands.Voxels + island.Start, island.Count, island.Min, target, new int3(margin));

            int sourceSolid = CountSolid(grid);
            int targetSolid = CountSolid(target);
            result.Check(sourceSolid == solidAfterCut - island.Count && targetSolid == island.Count, "No rock is lost or doubled",
                $"before {solidBefore:N0}, after cut {solidAfterCut:N0} = body {sourceSolid:N0} + piece {targetSolid:N0}");

            float3 targetOrigin = origin + (float3)(island.Min - margin) * VoxelSize;
            VoxelMeshCheck pieceMesh = VoxelMeshCheck.Run(memory, target, VoxelSize, targetOrigin, palette, 2, rightCentre, 0f);
            VoxelMeshCheck bodyMesh = VoxelMeshCheck.Run(memory, grid, VoxelSize, origin, palette, 2, leftCentre, 0f);
            result.Check(pieceMesh.IsClosed && bodyMesh.IsClosed, "Both parts are closed", $"piece {pieceMesh.OpenEdges} and body {bodyMesh.OpenEdges} open edges");
            result.Check(pieceMesh.OreVertices > 0 && bodyMesh.OreVertices == 0, "Materials travel with the piece",
                $"{pieceMesh.OreVertices} ore vertices on the piece, {bodyMesh.OreVertices} on the body");

            bool placed = true;
            foreach (float3 p in pieceMesh.Positions)
                placed &= p.x > 2.5f;
            result.Check(placed, "The piece's surface stays where it was", "every vertex right of the cut");

            VoxelIslands.Find(grid, regionMin, regionMax, ref islands);
            result.Check(islands.IslandCount == 0, "Labels are cleared after a search", $"{islands.IslandCount} found on the second search");
        }

        private static void CheckSlabCut(Result result, VoxelTestMemory memory, float4* palette)
        {
            // A saw cut straight through a sphere leaves two halves of about the same size.
            const float radius = 12f;
            const float halfThickness = 0.5f;
            VoxelGrid grid = GridFor(memory, radius, out float3 origin);
            VoxelGeneration.Fill(grid, new AsteroidShape { Radii = new float3(radius) }, VoxelSize, origin, 0, grid.Size.z);
            int solidBefore = CountSolid(grid);

            float3 centre = -origin / VoxelSize;
            float3 normal = math.normalize(new float3(1f, 0.3f, -0.2f));
            float* removed = memory.Alloc<float>(VoxelEncoding.MaterialSlots);
            var request = new CarveRequest
            {
                Shape = CarveShape.Slab,
                Centre = centre,
                Normal = normal,
                HalfThickness = halfThickness / VoxelSize,
                Radius = (radius + 2f) / VoxelSize
            };

            bool changed = VoxelCarving.Carve(grid, request, removed, out int3 min, out int3 max);
            float removedVolume = Sum(removed) * VoxelSize * VoxelSize * VoxelSize;
            float disc = math.PI * radius * radius * halfThickness * 2f;
            result.Check(changed && Within(removedVolume, disc, 0.1f), "Slab removes a disc",
                $"{removedVolume:0.0} m³ removed, disc {disc:0.0} m³ ({Error(removedVolume, disc)})");

            IslandBuffers islands = memory.IslandBuffers(grid.Count, IslandCapacity);
            VoxelIslands.Find(grid, min - 1, max + 1, ref islands);
            bool one = islands.IslandCount == 1;
            int solidAfterCut = CountSolid(grid);
            int pieceCount = one ? islands.Islands[0].Count : 0;
            int bodyCount = solidAfterCut - pieceCount;
            result.Check(one && Math.Abs(pieceCount - bodyCount) < solidAfterCut / 20, "Cut in half, two halves",
                $"{islands.IslandCount} piece; halves of {pieceCount:N0} and {bodyCount:N0} voxels, {islands.Visited:N0} visited");
            if (!one)
                return;

            IslandInfo island = islands.Islands[0];
            int3 targetSize = island.Max - island.Min + 7;
            VoxelGrid target = memory.Grid(targetSize);
            VoxelIslands.Extract(grid, islands.Voxels + island.Start, island.Count, island.Min, target, new int3(3));

            float3 targetOrigin = origin + (float3)(island.Min - 3) * VoxelSize;
            VoxelMeshCheck piece = VoxelMeshCheck.Run(memory, target, VoxelSize, targetOrigin, palette, 2, float3.zero, 0f);
            VoxelMeshCheck body = VoxelMeshCheck.Run(memory, grid, VoxelSize, origin, palette, 2, float3.zero, 0f);
            result.Check(piece.IsClosed && body.IsClosed && CountSolid(grid) + CountSolid(target) == solidAfterCut,
                "Both halves are whole", $"{solidBefore:N0} before, {solidAfterCut:N0} after the cut, open edges {piece.OpenEdges} and {body.OpenEdges}");
        }

        private static void CheckOre(Result result, VoxelTestMemory memory, float4* palette)
        {
            AsteroidShape shape = RockShape(20f, new float3(-4.4f, 7.7f, 1.3f));
            shape.OreCount = 1;
            shape.Ore0 = new OreLayer { Material = 1, Frequency = 0.12f, Thickness = 0.12f, MinDepth = 1f, Offset = new float3(17f, 3f, 5f) };
            VoxelGrid grid = GridFor(memory, shape.MaxRadius, out float3 origin);
            VoxelGeneration.Fill(grid, shape, VoxelSize, origin, 0, grid.Size.z);

            int solid = 0;
            int ore = 0;
            for (int i = 0; i < grid.Count; ++i)
            {
                if (grid.Density[i] <= 0)
                    continue;

                solid++;
                if (grid.Material[i] == 1)
                    ore++;
            }

            float share = solid > 0 ? (float)ore / solid : 0f;
            result.Check(share > 0.03f && share < 0.4f, "Ore veins", $"{share:P1} of the rock is ore");

            float* removed = memory.Alloc<float>(VoxelEncoding.MaterialSlots);
            VoxelCarving.CarveSphere(grid, (float3)grid.Size * 0.5f, 6f / VoxelSize, 0.5f, float3.zero, removed, out _, out _);
            result.Check(removed[0] > 0f && removed[1] > 0f, "Digging out ore is reported by material",
                $"rock {removed[0] * 0.125f:0.0} m³, ore {removed[1] * 0.125f:0.0} m³");

            VoxelMeshCheck mesh = VoxelMeshCheck.Run(memory, grid, VoxelSize, origin, palette, 2, float3.zero, 0f);
            result.Check(mesh.OreVertices > 0 && mesh.IsClosed, "Ore shows on the surface", $"{mesh.OreVertices:N0} ore-tinted vertices");
        }

        private static void CheckOverflow(Result result, VoxelTestMemory memory, float4* palette)
        {
            const float radius = 10f;
            const int guard = 64;
            const byte sentinel = 0xAB;
            VoxelGrid grid = GridFor(memory, radius, out float3 origin);
            VoxelGeneration.Fill(grid, new AsteroidShape { Radii = new float3(radius) }, VoxelSize, origin, 0, grid.Size.z);

            MeshBuffers buffers = memory.MeshBuffers();
            int vertexCapacity = 100;
            int indexCapacity = 240;
            buffers.Vertices = memory.Alloc<VoxelVertex>(vertexCapacity + guard);
            buffers.Indices = memory.Alloc<ushort>(indexCapacity + guard);
            buffers.VertexCapacity = vertexCapacity;
            buffers.IndexCapacity = indexCapacity;

            byte* vertexGuard = (byte*)(buffers.Vertices + vertexCapacity);
            byte* indexGuard = (byte*)(buffers.Indices + indexCapacity);
            for (int i = 0; i < guard * sizeof(VoxelVertex); ++i)
                vertexGuard[i] = sentinel;
            for (int i = 0; i < guard * sizeof(ushort); ++i)
                indexGuard[i] = sentinel;

            SurfaceNets.MeshChunk(grid, int3.zero, VoxelSize, origin, palette, 2, ref buffers);

            bool intact = true;
            for (int i = 0; i < guard * sizeof(VoxelVertex); ++i)
                intact &= vertexGuard[i] == sentinel;
            for (int i = 0; i < guard * sizeof(ushort); ++i)
                intact &= indexGuard[i] == sentinel;

            bool indicesValid = true;
            for (int i = 0; i < buffers.IndexCount; ++i)
                indicesValid &= buffers.Indices[i] < buffers.VertexCount;

            result.Check(buffers.Overflow && intact && indicesValid && buffers.IndexCount <= indexCapacity, "A mesh too big for its buffers is cut short safely",
                $"{buffers.VertexCount} vertices, {buffers.IndexCount} indices, nothing written past the end");
        }

        private static void CheckDepletion(Result result, VoxelTestMemory memory, float4* palette)
        {
            const float radius = 5f;
            VoxelGrid grid = GridFor(memory, radius, out float3 origin);
            VoxelGeneration.Fill(grid, new AsteroidShape { Radii = new float3(radius) }, VoxelSize, origin, 0, grid.Size.z);
            float before = StoredVolume(memory, grid);

            float* removed = memory.Alloc<float>(VoxelEncoding.MaterialSlots);
            VoxelCarving.CarveSphere(grid, (float3)grid.Size * 0.5f, 40f, 0f, float3.zero, removed, out _, out _);

            VoxelMeshCheck mesh = VoxelMeshCheck.Run(memory, grid, VoxelSize, origin, palette, 2, float3.zero, 0f);
            float removedVolume = Sum(removed) * VoxelSize * VoxelSize * VoxelSize;
            result.Check(CountSolid(grid) == 0 && mesh.Triangles == 0 && Math.Abs(removedVolume - before) < 0.01f, "A rock dug out entirely leaves nothing",
                $"{CountSolid(grid)} solid voxels, {mesh.Triangles} triangles, {removedVolume:0.0} of {before:0.0} m³ removed");
        }

        /// <summary>A grid centred on the body origin, big enough for a rock of the given radius in metres.</summary>
        private static VoxelGrid GridFor(VoxelTestMemory memory, float radius, out float3 origin)
        {
            int half = (int)math.ceil(radius / VoxelSize) + 4;
            origin = new float3(-half * VoxelSize);
            return memory.Grid(new int3(half * 2 + 1));
        }

        private static AsteroidShape RockShape(float radius, float3 offset)
        {
            return new AsteroidShape
            {
                Radii = new float3(radius, radius * 0.8f, radius * 0.9f),
                Bumpiness = 0.22f,
                BumpFrequency = 1.6f,
                BumpOctaves = 3,
                CraterDepth = 0.12f,
                CraterFrequency = 2.2f,
                CraterSize = 0.45f,
                Detail = 0.4f,
                DetailFrequency = 0.35f,
                Offset = offset
            };
        }

        private static float StoredVolume(VoxelTestMemory memory, in VoxelGrid grid)
        {
            float* materials = memory.Alloc<float>(VoxelEncoding.MaterialSlots);
            VoxelIslands.Measure(grid, materials, out float volume, out _, out _);
            return volume * VoxelSize * VoxelSize * VoxelSize;
        }

        private static int CountSolid(in VoxelGrid grid)
        {
            int count = 0;
            for (int i = 0; i < grid.Count; ++i)
            {
                if (grid.Density[i] > 0)
                    count++;
            }

            return count;
        }

        private static float Sum(float* values)
        {
            float sum = 0f;
            for (int i = 0; i < VoxelEncoding.MaterialSlots; ++i)
                sum += values[i];
            return sum;
        }

        // Volume shared by spheres of radius r1 and r2 whose centres are d apart.
        private static float LensVolume(float r1, float r2, float d)
        {
            float a = r1 + r2 - d;
            return math.PI * a * a * (d * d + 2f * d * r2 - 3f * r2 * r2 + 2f * d * r1 + 6f * r2 * r1 - 3f * r1 * r1) / (12f * d);
        }

        private static bool Within(float value, float expected, float tolerance)
        {
            return Math.Abs(value - expected) <= Math.Abs(expected) * tolerance;
        }

        private static string Error(float value, float expected)
        {
            return expected != 0f ? $"{(value - expected) / expected:+0.0%;-0.0%}" : "n/a";
        }
    }
}
