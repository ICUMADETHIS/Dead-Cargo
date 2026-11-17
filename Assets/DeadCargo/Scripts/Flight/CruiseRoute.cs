using DeadCargo.Sectors;
using Unity.Mathematics;

namespace DeadCargo.Flight
{
    /// <summary>
    /// Whether a straight route through the sector crosses an asteroid field, read from the same density the field is
    /// generated from. Pure, so the self-test runs it outside Unity.
    /// </summary>
    public static class CruiseRoute
    {
        /// <summary>
        /// Distance from <paramref name="from"/> towards <paramref name="to"/>, in metres, to the first point inside the
        /// sector's box where a field is denser than <paramref name="limit"/>, checked every <paramref name="spacing"/>
        /// metres; <see cref="float.PositiveInfinity"/> when the route is clear. 0 when it starts in a field.
        /// </summary>
        public static float FieldAlong<TRegions>(in TRegions regions, float3 from, float3 to, float3 boxMin, float3 boxMax,
            float spacing, float limit) where TRegions : struct, IRegionSet
        {
            float3 delta = to - from;
            float length = math.length(delta);
            float3 direction = length > 1e-6f ? delta / length : new float3(0f, 0f, 1f);
            if (!ClipToBox(from, direction, length, boxMin, boxMax, out float enter, out float exit))
                return float.PositiveInfinity;

            float step = math.max(1f, spacing);
            for (float along = enter; ; along += step)
            {
                along = math.min(along, exit);
                if (SectorDensity.DensityAt(regions, from + direction * along, out _) > limit)
                    return along;

                if (along >= exit)
                    return float.PositiveInfinity;
            }
        }

        // The stretch of a path inside a box, by its distances along the path; false when it misses the box.
        private static bool ClipToBox(float3 start, float3 direction, float length, float3 min, float3 max, out float enter, out float exit)
        {
            enter = 0f;
            exit = length;
            for (int axis = 0; axis < 3; ++axis)
            {
                if (math.abs(direction[axis]) < 1e-9f)
                {
                    if (start[axis] < min[axis] || start[axis] > max[axis])
                        return false;

                    continue;
                }

                float near = (min[axis] - start[axis]) / direction[axis];
                float far = (max[axis] - start[axis]) / direction[axis];
                enter = math.max(enter, math.min(near, far));
                exit = math.min(exit, math.max(near, far));
            }

            return enter <= exit;
        }
    }
}
