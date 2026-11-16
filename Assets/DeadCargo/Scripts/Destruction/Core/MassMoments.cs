using Unity.Mathematics;

namespace DeadCargo.Destruction.Voxels
{
    /// <summary>
    /// A rock's volume and its first and second moments: everything its centre of mass and inertia come from.
    /// Moments add and subtract, so a body follows every crater and every piece it loses exactly, without measuring
    /// itself again. Sums run in double, as a large rock adds up millions of small values.
    /// </summary>
    public struct MassMoments
    {
        public double Volume;

        /// <summary>Sum of volume times position.</summary>
        public double3 First;

        /// <summary>Sum of volume times x², y² and z².</summary>
        public double3 Squares;

        /// <summary>Sum of volume times xy, xz and yz.</summary>
        public double3 Products;

        /// <summary>Centre of mass, or zero when empty.</summary>
        public double3 Centre => Volume > 0.0 ? First / Volume : double3.zero;

        public void Add(double volume, double3 position)
        {
            Volume += volume;
            First += volume * position;
            Squares += volume * position * position;
            Products += volume * new double3(position.x * position.y, position.x * position.z, position.y * position.z);
        }

        /// <summary>
        /// Adds a row of samples along x at one y and z, from the row's sums of volume, volume times x and volume
        /// times x². A scan then keeps three running sums per sample instead of ten.
        /// </summary>
        public void AddRow(double volume, double sumX, double sumXX, double y, double z)
        {
            Volume += volume;
            First += new double3(sumX, volume * y, volume * z);
            Squares += new double3(sumXX, volume * y * y, volume * z * z);
            Products += new double3(sumX * y, sumX * z, volume * y * z);
        }

        /// <summary>
        /// The same rock with every position moved to <paramref name="offset"/> + <paramref name="scale"/> × position
        /// and every volume scaled by the cube of <paramref name="scale"/>: grid samples to metres, or one grid's
        /// coordinates to another's.
        /// </summary>
        public MassMoments Transformed(double scale, double3 offset)
        {
            double cube = scale * scale * scale;
            double3 first = First * scale;
            double3 o = offset;
            return new MassMoments
            {
                Volume = Volume * cube,
                First = cube * (Volume * o + first),
                Squares = cube * (Volume * o * o + 2.0 * o * first + scale * scale * Squares),
                Products = cube * (Volume * new double3(o.x * o.y, o.x * o.z, o.y * o.z) +
                                   new double3(o.x * first.y + o.y * first.x, o.x * first.z + o.z * first.x, o.y * first.z + o.z * first.y) +
                                   scale * scale * Products)
            };
        }

        /// <summary>
        /// Inertia tensor about the centre of mass for a density of one, in volume times length squared. Each sample
        /// also counts as a small cube of side <paramref name="sampleSize"/>, so even a thin sliver turns stably.
        /// </summary>
        public double3x3 InertiaAboutCentre(double sampleSize)
        {
            if (!(Volume > 0.0))
                return double3x3.zero;

            double3 c = First / Volume;
            double3 squares = math.max(Squares - Volume * c * c, 0.0);
            double3 products = Products - Volume * new double3(c.x * c.y, c.x * c.z, c.y * c.z);
            double cube = Volume * sampleSize * sampleSize / 6.0;

            return new double3x3(
                squares.y + squares.z + cube, -products.x, -products.y,
                -products.x, squares.x + squares.z + cube, -products.z,
                -products.y, -products.z, squares.x + squares.y + cube);
        }

        public static MassMoments operator +(MassMoments a, MassMoments b)
        {
            return new MassMoments
            {
                Volume = a.Volume + b.Volume,
                First = a.First + b.First,
                Squares = a.Squares + b.Squares,
                Products = a.Products + b.Products
            };
        }

        public static MassMoments operator -(MassMoments a, MassMoments b)
        {
            return new MassMoments
            {
                Volume = a.Volume - b.Volume,
                First = a.First - b.First,
                Squares = a.Squares - b.Squares,
                Products = a.Products - b.Products
            };
        }
    }
}
