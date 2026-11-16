using System;
using UnityEngine;

namespace DeadCargo.Sectors
{
    /// <summary>
    /// An ellipsoid of a sector with its own content and density. Density is full at the centre
    /// and fades to nothing at the surface. Authoring data, in sector space.
    /// </summary>
    [Serializable]
    public struct SectorRegion
    {
        [SerializeField] private string name;
        [SerializeField] private SectorRegionKind kind;

        [Tooltip("Centre of the region in sector space, in metres.")]
        [SerializeField] private Vector3 centre;

        [Tooltip("Radius along each axis, in metres.")]
        [SerializeField] private Vector3 radii;

        [Tooltip("How full the region is at its centre. For a clearing, how much of the fields it removes.")]
        [SerializeField, Range(0f, 1f)] private float density;

        [Tooltip("Share of the radius over which density fades out towards the surface. 0 gives a hard edge.")]
        [SerializeField, Range(0f, 1f)] private float falloff;

        public SectorRegion(string name, SectorRegionKind kind, Vector3 centre, Vector3 radii, float density, float falloff)
        {
            this.name = name;
            this.kind = kind;
            this.centre = centre;
            this.radii = radii;
            this.density = Mathf.Clamp01(density);
            this.falloff = Mathf.Clamp01(falloff);
        }

        public string Name => name;
        public SectorRegionKind Kind => kind;

        /// <summary>Centre in sector space, in metres.</summary>
        public Vector3 Centre => centre;

        /// <summary>Radius along each axis, in metres.</summary>
        public Vector3 Radii => radii;

        /// <summary>The region's strength at a sector-space position, from 0 outside it to its density at the centre.</summary>
        public float StrengthAt(Vector3 sectorPosition)
        {
            if (radii.x <= 0f || radii.y <= 0f || radii.z <= 0f)
                return 0f;

            // Distance from the centre as a share of the radius: 0 at the centre, 1 on the surface.
            Vector3 offset = sectorPosition - centre;
            float x = offset.x / radii.x;
            float y = offset.y / radii.y;
            float z = offset.z / radii.z;
            float distanceSquared = x * x + y * y + z * z;
            if (distanceSquared >= 1f)
                return 0f;

            if (falloff <= 0f)
                return density;

            float fade = (1f - Mathf.Sqrt(distanceSquared)) / falloff;
            if (fade >= 1f)
                return density;

            // Smoothstep, so the edge has no visible ring.
            return density * fade * fade * (3f - 2f * fade);
        }
    }
}
