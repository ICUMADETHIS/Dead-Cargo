using UnityEngine;

namespace DeadCargo.Destruction.Voxels
{
    /// <summary>
    /// Rock removed from a voxel body by one cut, per material slot, in cubic metres. Mining reads this to turn what
    /// was dug out into cargo. Slot 0 is the plain rock; ores follow in the order of the asteroid type.
    /// </summary>
    public readonly struct VoxelYield
    {
        public readonly VoxelBody Body;
        public readonly DamageKind Kind;

        /// <summary>World position of the cut.</summary>
        public readonly Vector3 Point;

        private readonly float slot0, slot1, slot2, slot3, slot4, slot5, slot6, slot7;

        public VoxelYield(VoxelBody body, DamageKind kind, Vector3 point, float s0, float s1, float s2, float s3,
            float s4, float s5, float s6, float s7)
        {
            Body = body;
            Kind = kind;
            Point = point;
            slot0 = s0;
            slot1 = s1;
            slot2 = s2;
            slot3 = s3;
            slot4 = s4;
            slot5 = s5;
            slot6 = s6;
            slot7 = s7;
        }

        /// <summary>Cubic metres of the given slot removed.</summary>
        public float this[int slot]
        {
            get
            {
                switch (slot)
                {
                    case 0: return slot0;
                    case 1: return slot1;
                    case 2: return slot2;
                    case 3: return slot3;
                    case 4: return slot4;
                    case 5: return slot5;
                    case 6: return slot6;
                    case 7: return slot7;
                    default: return 0f;
                }
            }
        }

        /// <summary>Cubic metres removed in all.</summary>
        public float Total => slot0 + slot1 + slot2 + slot3 + slot4 + slot5 + slot6 + slot7;

        /// <summary>The slot with the most removed: what the chips look like.</summary>
        public int DominantSlot
        {
            get
            {
                int best = 0;
                for (int slot = 1; slot < 8; ++slot)
                {
                    if (this[slot] > this[best])
                        best = slot;
                }

                return best;
            }
        }
    }
}
