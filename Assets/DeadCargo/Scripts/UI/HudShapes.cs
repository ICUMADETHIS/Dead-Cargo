using UnityEngine;
using UnityEngine.UI;

namespace DeadCargo.UI
{
    /// <summary>
    /// Anti-aliased shapes for the comic HUD's meshes. Screen-space canvases get no multisampling, so each edge fades over
    /// a feather: solid up to half a feather inside the edge, clear half a feather outside it. Built on the main thread
    /// only; the helpers share scratch buffers.
    /// </summary>
    internal static class HudShapes
    {
        internal const int MaxCorners = 24;

        /// <summary>How far an outline's corner may reach, as a multiple of the outline's width.</summary>
        internal const float MiterLimit = 2f;

        private const int DiscSegments = 20;
        private const int RingSegments = 32;

        private static readonly Vector2[] Normals = new Vector2[MaxCorners];
        private static readonly Vector2[] Shifts = new Vector2[MaxCorners];
        private static readonly Vector2[] Points = new Vector2[MaxCorners];
        private static readonly float[] Uniform = new float[MaxCorners];

        /// <summary>A polygon with every edge feathered. See <see cref="AddPolygon(VertexHelper, Vector2[], int, Color, float[])"/>.</summary>
        internal static void AddPolygon(VertexHelper mesh, Vector2[] corners, int count, Color tint, float feather)
        {
            for (int i = 0; i < count && i < MaxCorners; ++i)
                Uniform[i] = feather * 0.5f;

            AddPolygon(mesh, corners, count, tint, Uniform);
        }

        /// <summary>
        /// A polygon in either winding that is star-shaped from its first corner: convex, or an arrow with a notch when
        /// the tip comes first. Edge i runs from corner i to the next, and <paramref name="softness"/>[i] is how far it
        /// fades each way; zero leaves it hard, so two shapes can meet along it without a seam.
        /// </summary>
        internal static void AddPolygon(VertexHelper mesh, Vector2[] corners, int count, Color tint, float[] softness)
        {
            if (count < 3 || count > MaxCorners || !FindNormals(corners, count))
                return;

            Color clear = tint;
            clear.a = 0f;
            int start = mesh.currentVertCount;

            // Each corner moves so both its edges shift by their softness: inwards for the solid core, outwards for the rim.
            for (int i = 0; i < count; ++i)
            {
                int previous = (i + count - 1) % count;
                Shifts[i] = CornerShift(Normals[previous], softness[previous], Normals[i], softness[i]);
                mesh.AddVert(corners[i] - Shifts[i], tint, Vector2.zero);
            }

            for (int i = 0; i < count; ++i)
                mesh.AddVert(corners[i] + Shifts[i], clear, Vector2.zero);

            for (int i = 1; i < count - 1; ++i)
                mesh.AddTriangle(start, start + i, start + i + 1);

            for (int i = 0; i < count; ++i)
            {
                if (softness[i] <= 0f)
                    continue;

                int next = (i + 1) % count;
                mesh.AddTriangle(start + i, start + count + i, start + count + next);
                mesh.AddTriangle(start + i, start + count + next, start + next);
            }
        }

        /// <summary>
        /// Writes the polygon grown outwards by <paramref name="distance"/> on every edge into <paramref name="grown"/>, for
        /// ink outlines. A sharp corner moves out no more than <see cref="MiterLimit"/> times the distance, so it stays
        /// blunt instead of spiking.
        /// </summary>
        internal static void Expand(Vector2[] corners, int count, float distance, Vector2[] grown)
        {
            if (count < 3 || count > MaxCorners || !FindNormals(corners, count))
            {
                for (int i = 0; i < count; ++i)
                    grown[i] = corners[i];

                return;
            }

            float limit = distance * MiterLimit;
            for (int i = 0; i < count; ++i)
            {
                int previous = (i + count - 1) % count;
                grown[i] = corners[i] + Vector2.ClampMagnitude(CornerShift(Normals[previous], distance, Normals[i], distance), limit);
            }
        }

        /// <summary>A straight stroke with square ends that reach half its width past each end point.</summary>
        internal static void AddStroke(VertexHelper mesh, Vector2 from, Vector2 to, float width, Color tint, float feather)
        {
            Vector2 along = to - from;
            float length = along.magnitude;
            if (length < 1e-4f || width <= 0f)
                return;

            along *= width * 0.5f / length;
            var side = new Vector2(-along.y, along.x);
            Points[0] = from - along - side;
            Points[1] = from - along + side;
            Points[2] = to + along + side;
            Points[3] = to + along - side;
            AddPolygon(mesh, Points, 4, tint, feather);
        }

        /// <summary>A circle's outline, <paramref name="width"/> wide and centred on <paramref name="radius"/>, soft on both edges.</summary>
        internal static void AddRing(VertexHelper mesh, Vector2 centre, float radius, float width, Color tint, float feather)
        {
            float half = width * 0.5f;
            if (half <= 0f || radius <= 0f)
                return;

            // Four circles from the outside in: clear rim, solid edge, solid edge, clear rim.
            float soft = Mathf.Min(feather * 0.5f, half * 0.5f);
            float outerRim = radius + half + soft;
            float outerEdge = radius + half - soft;
            float innerEdge = Mathf.Max(0f, radius - half + soft);
            float innerRim = Mathf.Max(0f, radius - half - soft);
            Color clear = tint;
            clear.a = 0f;

            int start = mesh.currentVertCount;
            for (int i = 0; i < RingSegments; ++i)
            {
                float angle = i * (2f * Mathf.PI / RingSegments);
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                mesh.AddVert(centre + direction * outerRim, clear, Vector2.zero);
                mesh.AddVert(centre + direction * outerEdge, tint, Vector2.zero);
                mesh.AddVert(centre + direction * innerEdge, tint, Vector2.zero);
                mesh.AddVert(centre + direction * innerRim, clear, Vector2.zero);
            }

            for (int i = 0; i < RingSegments; ++i)
            {
                int here = start + i * 4;
                int next = start + (i + 1) % RingSegments * 4;
                for (int band = 0; band < 3; ++band)
                {
                    mesh.AddTriangle(here + band, next + band, next + band + 1);
                    mesh.AddTriangle(here + band, next + band + 1, here + band + 1);
                }
            }
        }

        /// <summary>A filled circle.</summary>
        internal static void AddDisc(VertexHelper mesh, Vector2 centre, float radius, Color tint, float feather)
        {
            if (radius <= 0f)
                return;

            for (int i = 0; i < DiscSegments; ++i)
            {
                float angle = i * (2f * Mathf.PI / DiscSegments);
                Points[i] = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }

            AddPolygon(mesh, Points, DiscSegments, tint, feather);
        }

        // Outward unit normals of every edge. False for a polygon with no area.
        private static bool FindNormals(Vector2[] corners, int count)
        {
            float area = 0f;
            for (int i = 0; i < count; ++i)
            {
                Vector2 a = corners[i];
                Vector2 b = corners[(i + 1) % count];
                area += a.x * b.y - b.x * a.y;
            }

            if (Mathf.Abs(area) < 1e-6f)
                return false;

            // Clockwise outlines have their outside on the left of each edge, anticlockwise on the right.
            float outside = area < 0f ? 1f : -1f;
            for (int i = 0; i < count; ++i)
            {
                Vector2 edge = corners[(i + 1) % count] - corners[i];
                Normals[i] = new Vector2(-edge.y, edge.x).normalized * outside;
            }

            return true;
        }

        // The move that shifts a corner's two edges outwards by their own distances, where they meet.
        private static Vector2 CornerShift(Vector2 normalA, float distanceA, Vector2 normalB, float distanceB)
        {
            float determinant = normalA.x * normalB.y - normalA.y * normalB.x;
            if (Mathf.Abs(determinant) < 1e-4f)
                return normalA * Mathf.Max(distanceA, distanceB);

            return new Vector2((distanceA * normalB.y - distanceB * normalA.y) / determinant,
                (normalA.x * distanceB - normalB.x * distanceA) / determinant);
        }
    }
}
