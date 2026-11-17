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
        /// The fastest the drive may go with a surface <paramref name="clearance"/> metres away. <paramref name="closing"/>
        /// is the share of the speed spent heading towards it, from -1 (straight away) to 1 (straight at it): speed towards
        /// the surface is held to <see cref="CruiseSettings.ApproachRate"/> times the clearance, any other speed to
        /// <see cref="CruiseSettings.CrossRate"/> times it.
        /// </summary>
        public static double SurfaceLimit(double clearance, double closing, in CruiseSettings settings)
        {
            double rate = closing > 0.0 ? Math.Min(settings.CrossRate, settings.ApproachRate / closing) : settings.CrossRate;
            return Math.Max(0.0, rate) * Math.Max(0.0, clearance);
        }

        /// <summary>Speed after <paramref name="deltaTime"/> seconds of easing towards <paramref name="target"/>.</summary>
        public static double Ease(double speed, double target, double response, double deltaTime)
        {
            return target + (speed - target) * Math.Exp(-Math.Max(0.0, response) * Math.Max(0.0, deltaTime));
        }

        /// <summary>
        /// A unit <paramref name="nose"/> turned towards a unit <paramref name="course"/> for <paramref name="deltaTime"/>
        /// seconds: at <see cref="CruiseSettings.TurnRate"/>, slowing near the end so the turn settles without overshoot.
        /// </summary>
        public static double3 Turn(double3 nose, double3 course, in CruiseSettings settings, double deltaTime)
        {
            double angle = AngleBetween(nose, course);
            if (angle <= 1e-9)
                return course;

            double step = Math.Min(math.radians(settings.TurnRate), angle * settings.TurnEase) * Math.Max(0.0, deltaTime);
            if (step >= angle)
                return course;

            double3 axis = math.cross(nose, course);
            double axisLength = math.length(axis);
            axis = axisLength > 1e-12 ? axis / axisLength : Perpendicular(nose);

            // About an axis square to the nose, a turn is the nose and its quarter turn blended.
            math.sincos(step, out double sin, out double cos);
            return math.normalize(nose * cos + math.cross(axis, nose) * sin);
        }

        /// <summary>Angle between two unit directions, in radians.</summary>
        public static double AngleBetween(double3 a, double3 b)
        {
            return Math.Acos(Math.Clamp(math.dot(a, b), -1.0, 1.0));
        }

        /// <summary>Any unit direction square to a unit <paramref name="direction"/>.</summary>
        public static double3 Perpendicular(double3 direction)
        {
            double3 other = Math.Abs(direction.y) < 0.9 ? new double3(0.0, 1.0, 0.0) : new double3(1.0, 0.0, 0.0);
            return math.normalize(math.cross(direction, other));
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

        /// <summary>
        /// The closest a straight path of <paramref name="length"/> metres from <paramref name="start"/> along a unit
        /// <paramref name="direction"/> comes to <paramref name="point"/>, and how far along it that is.
        /// </summary>
        public static double ClosestApproach(double3 start, double3 direction, double length, double3 point, out double along)
        {
            along = Math.Clamp(math.dot(point - start, direction), 0.0, Math.Max(0.0, length));
            return math.length(start + direction * along - point);
        }
    }
}
