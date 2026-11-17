using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace DeadCargo.Flight
{
    /// <summary>
    /// Where the autopilot steers and how fast it goes, as pure functions of the ship's place in the star system, so the
    /// drive and the self-test fly the same rule.
    /// <para>
    /// <b>Course.</b> Straight at the destination's centre. When a body other than the destination lies across that line,
    /// inside its arrival height, the ship goes round it instead: climbing while below that height, then level across its
    /// face until the destination clears its limb. A ship that takes off from the far side of a planet comes up and over,
    /// and never cuts through it.
    /// </para>
    /// <para>
    /// <b>Speed.</b> Within the sector's reach, a set speed, as the ship really flies past rocks there. Beyond it, every
    /// surface limits the speed (<see cref="CruiseMath.SurfaceLimit"/>); the destination by the distance left to where
    /// cruise ends, so the drive brakes into it. The lowest limit wins.
    /// </para>
    /// </summary>
    public static class CruiseAutopilot
    {
        /// <summary>The destination index that means the sector.</summary>
        public const int Home = -1;

        /// <summary>The destination index that means none.</summary>
        public const int None = -2;

        // A route counts as clear of a body when it passes no more than this inside the limit, in metres.
        private const double Tolerance = 1.0;

        // Climb for each metre across while inside a body's arrival height: 1 climbs at 45°.
        private const double ClimbSlope = 1.0;

        /// <summary>
        /// The unit direction to fly from <paramref name="position"/> towards <paramref name="aim"/>, and in
        /// <paramref name="around"/> the body being rounded, or -1 on a straight course. Zero when already at the aim.
        /// </summary>
        public static double3 Course(double3 position, double3 aim, IReadOnlyList<CruiseBody> bodies, int destination, out int around)
        {
            around = -1;
            double3 toAim = aim - position;
            double length = math.length(toAim);
            if (length <= 1e-9)
                return double3.zero;

            double3 direct = toAim / length;
            double nearest = double.PositiveInfinity;
            for (int i = 0; i < bodies.Count; ++i)
            {
                CruiseBody body = bodies[i];
                if (i == destination || !body.Exists)
                    continue;

                // Inside the arrival height a ship may still fly level or up, but never further down.
                double floor = Math.Min(math.length(position - body.Centre), body.ArrivalRadius) - Tolerance;
                double closest = CruiseMath.ClosestApproach(position, direct, length, body.Centre, out double along);
                if (closest < floor && along < nearest)
                {
                    nearest = along;
                    around = i;
                }
            }

            if (around < 0)
                return direct;

            CruiseBody blocker = bodies[around];
            double3 fromCentre = position - blocker.Centre;
            double3 up = math.normalize(fromCentre);
            double3 across = direct - up * math.dot(direct, up);
            double acrossLength = math.length(across);
            across = acrossLength > 1e-9 ? across / acrossLength : CruiseMath.Perpendicular(up);

            bool inside = math.length(fromCentre) < blocker.ArrivalRadius;
            return inside ? math.normalize(across + up * ClimbSlope) : across;
        }

        /// <summary>The speed to aim for at <paramref name="position"/>, flying along a unit <paramref name="heading"/>, in metres a second.</summary>
        public static double TargetSpeed(double3 position, double3 heading, IReadOnlyList<CruiseBody> bodies, int destination,
            in CruiseSector sector, in CruiseSettings settings)
        {
            double toSector = CruiseMath.DistanceToBox(position, sector.Min, sector.Max);
            if (toSector <= sector.Reach)
                return settings.SectorSpeed;

            double3 onBox = math.clamp(position, sector.Min, sector.Max);
            double sectorClearance = destination == Home ? toSector - sector.Reach : toSector;
            double limit = CruiseMath.SurfaceLimit(sectorClearance, math.dot(heading, math.normalize(onBox - position)), settings);

            for (int i = 0; i < bodies.Count; ++i)
            {
                CruiseBody body = bodies[i];
                if (!body.Exists)
                    continue;

                double3 toCentre = body.Centre - position;
                double distance = math.length(toCentre);
                double clearance = distance - (i == destination ? body.ArrivalRadius : body.Radius);
                limit = Math.Min(limit, CruiseMath.SurfaceLimit(clearance, math.dot(heading, toCentre / distance), settings));
            }

            return Math.Clamp(limit, settings.MinSpeed, Math.Max(settings.MinSpeed, settings.MaxSpeed));
        }

        /// <summary>
        /// How far along a unit <paramref name="heading"/> the ship reaches the destination planet's arrival height, or
        /// <see cref="double.PositiveInfinity"/> when it misses or the destination is not a planet.
        /// </summary>
        public static double ToArrival(double3 position, double3 heading, IReadOnlyList<CruiseBody> bodies, int destination)
        {
            if (destination < 0 || destination >= bodies.Count || !bodies[destination].Exists)
                return double.PositiveInfinity;

            CruiseBody body = bodies[destination];
            return CruiseMath.DistanceToSphere(position, heading, body.Centre, body.ArrivalRadius);
        }

        /// <summary>
        /// How far along a unit <paramref name="heading"/> the ship would meet the surface of a body it is above, or
        /// <see cref="double.PositiveInfinity"/>. The course never leads there; this is the drive's last guard.
        /// </summary>
        public static double ToSurface(double3 position, double3 heading, IReadOnlyList<CruiseBody> bodies)
        {
            double nearest = double.PositiveInfinity;
            for (int i = 0; i < bodies.Count; ++i)
            {
                CruiseBody body = bodies[i];
                if (body.Exists && CruiseMath.Altitude(position, body.Centre, body.Radius) > 0.0)
                    nearest = Math.Min(nearest, CruiseMath.DistanceToSphere(position, heading, body.Centre, body.Radius));
            }

            return nearest;
        }
    }
}
