using System;

namespace DeadCargo.Sectors
{
    /// <summary>The whole-number address of one cell in a sector's grid.</summary>
    public readonly struct CellCoord : IEquatable<CellCoord>
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Z;

        public CellCoord(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public bool Equals(CellCoord other)
        {
            return X == other.X && Y == other.Y && Z == other.Z;
        }

        public override bool Equals(object obj)
        {
            return obj is CellCoord other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (int)SectorHash.Combine(SectorHash.Combine((uint)X, (uint)Y), (uint)Z);
            }
        }

        public override string ToString()
        {
            return $"({X}, {Y}, {Z})";
        }

        public static bool operator ==(CellCoord left, CellCoord right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CellCoord left, CellCoord right)
        {
            return !left.Equals(right);
        }
    }
}
