using Unity.Mathematics;

namespace DeadCargo.Sectors
{
    /// <summary>
    /// The shape and strength of one sector region as plain values, with no managed references, so generation jobs
    /// can read it. An ellipsoid turned about the vertical axis. Built with <see cref="Create"/>, which works out the
    /// turn and bounds once, so jobs never repeat them.
    /// </summary>
    public struct RegionVolume
    {
        /// <summary>Centre in sector space, in metres.</summary>
        public float3 Centre;

        /// <summary>Radius along each of the region's own axes, in metres.</summary>
        public float3 Radii;

        /// <summary>Strength at the centre, 0 to 1.</summary>
        public float Density;

        /// <summary>Share of the radius over which strength fades out towards the surface.</summary>
        public float Falloff;

        public SectorRegionKind Kind;

        /// <summary>Turn about the vertical axis, in degrees. Positive turns from +Z towards +X.</summary>
        public float Yaw;

        /// <summary>Half the size of the region's axis-aligned bounding box, in metres.</summary>
        public float3 Extent;

        /// <summary>
        /// Radius round the centre that no rock may reach into, whatever its size or wherever its centre falls, in
        /// metres. 0 for authored regions; placed content shelters its spot, so it is never buried.
        /// </summary>
        public float Shelter;

        // Sine and cosine of the yaw.
        private float sin;
        private float cos;

        /// <summary>True when the region holds content, rather than clearing it.</summary>
        public bool IsField => Kind != SectorRegionKind.Clearing;

        public static RegionVolume Create(float3 centre, float3 radii, float yaw, float density, float falloff, SectorRegionKind kind)
        {
            math.sincos(math.radians(yaw), out float s, out float c);
            float3 extent = new float3(
                math.sqrt(radii.x * radii.x * c * c + radii.z * radii.z * s * s),
                radii.y,
                math.sqrt(radii.x * radii.x * s * s + radii.z * radii.z * c * c));

            return new RegionVolume
            {
                Centre = centre,
                Radii = radii,
                Density = density,
                Falloff = falloff,
                Kind = kind,
                Yaw = yaw,
                Extent = extent,
                sin = s,
                cos = c
            };
        }

        /// <summary>A sector-space position relative to the centre, along the region's own axes.</summary>
        public float3 ToLocal(float3 position)
        {
            float3 offset = position - Centre;
            return new float3(offset.x * cos - offset.z * sin, offset.y, offset.x * sin + offset.z * cos);
        }

        /// <summary>Turns a direction given along the region's own axes into sector space.</summary>
        public float3 RotateToSector(float3 local)
        {
            return new float3(local.x * cos + local.z * sin, local.y, local.z * cos - local.x * sin);
        }

        /// <summary>Whether the region reaches into an axis-aligned box, judged by its bounding box.</summary>
        public bool MayOverlap(float3 boxMin, float3 boxMax)
        {
            return math.all(Centre + Extent >= boxMin) && math.all(Centre - Extent <= boxMax);
        }
    }
}
