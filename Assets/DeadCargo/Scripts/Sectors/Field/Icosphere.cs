using System.Collections.Generic;
using Unity.Mathematics;

namespace DeadCargo.Sectors.Field
{
    /// <summary>
    /// Unit spheres made by splitting an icosahedron's faces in four, again and again: even triangles with no poles.
    /// Each level keeps every vertex of the level before and adds the new ones after, so one array of directions
    /// serves every level, each using a longer start of it. Every added vertex remembers the two it was split between,
    /// which is where the level before draws that point: the start of a morph between levels. Built once and shared.
    /// </summary>
    public static class Icosphere
    {
        private static float3[] directions;
        private static int2[] parents;
        private static int[] vertexCounts;
        private static ushort[][] indices;

        /// <summary>
        /// For each vertex, the two vertices of the level before whose edge it splits; -1 for the first twelve.
        /// </summary>
        public static int2[] Parents
        {
            get
            {
                Build();
                return parents;
            }
        }

        /// <summary>Directions of the finest level's vertices; each level uses the first <see cref="VertexCount"/>.</summary>
        public static float3[] Directions
        {
            get
            {
                Build();
                return directions;
            }
        }

        public static int VertexCount(int level)
        {
            Build();
            return vertexCounts[level];
        }

        /// <summary>A level's triangles, three vertex indices each, wound to face outwards.</summary>
        public static ushort[] Indices(int level)
        {
            Build();
            return indices[level];
        }

        private static void Build()
        {
            if (directions != null)
                return;

            float t = (1f + math.sqrt(5f)) * 0.5f;
            var points = new List<float3>
            {
                new float3(-1f, t, 0f), new float3(1f, t, 0f), new float3(-1f, -t, 0f), new float3(1f, -t, 0f),
                new float3(0f, -1f, t), new float3(0f, 1f, t), new float3(0f, -1f, -t), new float3(0f, 1f, -t),
                new float3(t, 0f, -1f), new float3(t, 0f, 1f), new float3(-t, 0f, -1f), new float3(-t, 0f, 1f)
            };

            for (int i = 0; i < points.Count; ++i)
                points[i] = math.normalize(points[i]);

            var faces = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            };

            FaceOutwards(points, faces);

            var splits = new List<int2>();
            for (int i = 0; i < points.Count; ++i)
                splits.Add(new int2(-1, -1));

            vertexCounts = new int[FieldRockShape.Levels];
            indices = new ushort[FieldRockShape.Levels][];
            var midpoints = new Dictionary<long, int>();
            for (int level = 0; level < FieldRockShape.Levels; ++level)
            {
                if (level > 0)
                    faces = Split(points, splits, faces, midpoints);

                vertexCounts[level] = points.Count;
                indices[level] = new ushort[faces.Count];
                for (int i = 0; i < faces.Count; ++i)
                    indices[level][i] = (ushort)faces[i];
            }

            parents = splits.ToArray();
            directions = points.ToArray();
        }

        // Unity treats a triangle as facing the way cross(b - a, c - a) points; every face must point away from the centre.
        private static void FaceOutwards(List<float3> points, List<int> faces)
        {
            for (int f = 0; f < faces.Count; f += 3)
            {
                float3 a = points[faces[f]];
                float3 b = points[faces[f + 1]];
                float3 c = points[faces[f + 2]];
                if (math.dot(math.cross(b - a, c - a), a + b + c) < 0f)
                    (faces[f + 1], faces[f + 2]) = (faces[f + 2], faces[f + 1]);
            }
        }

        // Each triangle becomes four, keeping its winding; a shared edge's midpoint is made once.
        private static List<int> Split(List<float3> points, List<int2> splits, List<int> faces, Dictionary<long, int> midpoints)
        {
            var split = new List<int>(faces.Count * 4);
            for (int f = 0; f < faces.Count; f += 3)
            {
                int a = faces[f];
                int b = faces[f + 1];
                int c = faces[f + 2];
                int ab = Midpoint(points, splits, midpoints, a, b);
                int bc = Midpoint(points, splits, midpoints, b, c);
                int ca = Midpoint(points, splits, midpoints, c, a);
                split.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }

            return split;
        }

        private static int Midpoint(List<float3> points, List<int2> splits, Dictionary<long, int> midpoints, int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (midpoints.TryGetValue(key, out int index))
                return index;

            index = points.Count;
            points.Add(math.normalize(points[a] + points[b]));
            splits.Add(new int2(a, b));
            midpoints.Add(key, index);
            return index;
        }
    }
}
