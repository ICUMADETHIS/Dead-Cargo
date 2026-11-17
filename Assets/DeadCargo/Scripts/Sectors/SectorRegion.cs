using System;
using UnityEngine;

namespace DeadCargo.Sectors
{
    /// <summary>
    /// An ellipsoid of a sector with its own content and density, turned about the vertical axis. Density is full at
    /// the centre and fades to nothing at the surface. Authoring data, in sector space; each run varies it within its
    /// <see cref="Variation"/> (see <see cref="SectorLayout"/>).
    /// </summary>
    [Serializable]
    public struct SectorRegion
    {
        [SerializeField] private string name;
        [SerializeField] private SectorRegionKind kind;

        [Tooltip("Centre of the region in sector space, in metres.")]
        [SerializeField] private Vector3 centre;

        [Tooltip("Radius along each of the region's own axes, in metres.")]
        [SerializeField] private Vector3 radii;

        [Tooltip("Turn about the vertical axis, in degrees.")]
        [SerializeField, Range(-180f, 180f)] private float yaw;

        [Tooltip("How full the region is at its centre. For a clearing, how much of the fields it removes.")]
        [SerializeField, Range(0f, 1f)] private float density;

        [Tooltip("Share of the radius over which density fades out towards the surface. 0 gives a hard edge.")]
        [SerializeField, Range(0f, 1f)] private float falloff;

        [Tooltip("How far the region may change from one run to the next.")]
        [SerializeField] private RegionVariation variation;

        public SectorRegion(string name, SectorRegionKind kind, Vector3 centre, Vector3 radii, float density, float falloff)
            : this(name, kind, centre, radii, 0f, density, falloff, default)
        {
        }

        public SectorRegion(string name, SectorRegionKind kind, Vector3 centre, Vector3 radii, float yaw, float density, float falloff,
            RegionVariation variation)
        {
            this.name = name;
            this.kind = kind;
            this.centre = centre;
            this.radii = radii;
            this.yaw = yaw;
            this.density = Mathf.Clamp01(density);
            this.falloff = Mathf.Clamp01(falloff);
            this.variation = variation;
        }

        public string Name => name;
        public SectorRegionKind Kind => kind;

        /// <summary>Centre in sector space, in metres.</summary>
        public Vector3 Centre => centre;

        /// <summary>Radius along each of the region's own axes, in metres.</summary>
        public Vector3 Radii => radii;

        /// <summary>Turn about the vertical axis, in degrees.</summary>
        public float Yaw => yaw;

        public float Density => density;
        public float Falloff => falloff;
        public RegionVariation Variation => variation;

        /// <summary>The region as authored, as plain values for generation jobs.</summary>
        public RegionVolume Volume => RegionVolume.Create(centre, radii, yaw, density, falloff, kind);

        /// <summary>The region's strength at a sector-space position, from 0 outside it to its density at the centre.</summary>
        public float StrengthAt(Vector3 sectorPosition)
        {
            return SectorDensity.Strength(Volume, sectorPosition);
        }
    }
}
