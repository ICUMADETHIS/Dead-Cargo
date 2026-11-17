using System;

namespace DeadCargo.Sectors.Far
{
    /// <summary>The parts a planet may play in a run's system.</summary>
    [Flags]
    public enum BodyRoles
    {
        None = 0,

        /// <summary>The massive world the sector lies near.</summary>
        Primary = 1 << 0,

        /// <summary>A small body beside the primary.</summary>
        Moon = 1 << 1,

        /// <summary>A small planet far across the system.</summary>
        Distant = 1 << 2,

        All = Primary | Moon | Distant
    }
}
