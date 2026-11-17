using System;

namespace DeadCargo.Flight
{
    /// <summary>A planet's pull at a distance, in double precision. Pure, so the self-test runs it outside Unity.</summary>
    public static class PlanetPhysics
    {
        /// <summary>
        /// Pull towards the centre, in metres per second squared, at <paramref name="distance"/> metres from it: the surface
        /// pull, weaker with the square of the distance, and never stronger than at the surface.
        /// </summary>
        public static double GravityAt(double distance, double radius, double surfaceGravity)
        {
            if (radius <= 0.0 || surfaceGravity <= 0.0)
                return 0.0;

            double ratio = radius / Math.Max(distance, radius);
            return surfaceGravity * ratio * ratio;
        }
    }
}
