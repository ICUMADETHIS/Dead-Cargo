using System;
using Unity.Mathematics;

namespace DeadCargo.Flight
{
    /// <summary>
    /// The cruise drive's maths, in double precision because the star system spans thousands of kilometres. Pure, so the
    /// self-test runs it outside Unity.
    /// </summary>
    public static class CruiseMath
    {
        /// <summary>
        /// The speed the drive aims for, in metres a second: a fraction of the distance to the nearest surface, so it
        /// climbs in open space and falls near a surface, and every trip takes about as long whatever its length.
        /// </summary>
        public static double TargetSpeed(double clearance, in CruiseSettings settings)
        {
            return Math.Clamp(settings.Rate * Math.Max(0.0, clearance), settings.MinSpeed, Math.Max(settings.MinSpeed, settings.MaxSpeed));
        }

        /// <summary>Speed after <paramref name="deltaTime"/> seconds of easing towards <paramref name="target"/>.</summary>
        public static double Ease(double speed, double target, double response, double deltaTime)
        {
            return target + (speed - target) * Math.Exp(-Math.Max(0.0, response) * Math.Max(0.0, deltaTime));
        }

        /// <summary>Distance from a point to a box, 0 inside it.</summary>
        public static double DistanceToBox(double3 point, double3 min, double3 max)
        {
            double3 outside = math.max(math.max(min - point, point - max), 0.0);
            return math.length(outside);
        }

        /// <summary>Height above a sphere's surface. Negative inside it.</summary>
        public static double Altitude(double3 point, double3 centre, double radius)
        {
            return math.length(point - centre) - radius;
        }

        /// <summary>
        /// How far along a unit <paramref name="direction"/> from <paramref name="start"/> the path first comes within
        /// <paramref name="radius"/> of <paramref name="centre"/>, or <see cref="double.PositiveInfinity"/> when it never
        /// does. 0 when it starts inside.
        /// </summary>
        public static double DistanceToSphere(double3 start, double3 direction, double3 centre, double radius)
        {
            double3 toStart = start - centre;
            double c = math.dot(toStart, toStart) - radius * radius;
            if (c <= 0.0)
                return 0.0;

            double b = math.dot(toStart, direction);
            double discriminant = b * b - c;
            if (b >= 0.0 || discriminant < 0.0)
                return double.PositiveInfinity;

            return -b - Math.Sqrt(discriminant);
        }
    }
}
