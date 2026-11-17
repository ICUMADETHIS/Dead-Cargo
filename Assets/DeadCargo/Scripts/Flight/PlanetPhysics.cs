using System;

namespace DeadCargo.Flight
{
    /// <summary>
    /// A planet's pull and its air, in double precision. Pure, so the self-test runs it outside Unity.
    /// <para>
    /// <b>The air.</b> It reaches <c>airHeight</c> above sea level and thickens towards the ground. The thinner it is, the
    /// faster a ship flies: its top speed is a multiple of its open-space speed (<see cref="SpeedFactor"/>), high at the
    /// top of the air and low near the ground, so a descent starts fast and slows as the ground comes up. Density falls by
    /// e every eighth of the air's height, so it is almost nothing at the top.
    /// </para>
    /// </summary>
    public static class PlanetPhysics
    {
        // The air's density falls by e over this share of its height.
        private const double ScaleHeightShare = 1.0 / 8.0;

        private const double MetresPerKilometre = 1000.0;

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

        /// <summary>
        /// The ship's top speed at <paramref name="altitude"/> metres above sea level, as a multiple of its open-space speed:
        /// <see cref="AirSettings.GroundFactor"/> at the ground, rising to <see cref="AirSettings.TopFactor"/> at the top of
        /// the air, held above it, then easing back to 1 over the outer half of <see cref="AirSettings.Range"/>.
        /// </summary>
        public static double SpeedFactor(double altitude, double airHeight, in AirSettings settings)
        {
            if (airHeight <= 0.0)
                return 1.0;

            if (altitude < airHeight)
            {
                double share = Math.Pow(Math.Clamp(altitude / airHeight, 0.0, 1.0), Math.Max(0.01, settings.Curve));
                return settings.GroundFactor + (settings.TopFactor - settings.GroundFactor) * share;
            }

            return 1.0 + (settings.TopFactor - 1.0) * Reach(altitude, airHeight, settings);
        }

        /// <summary>
        /// How fully the planet's reach holds at <paramref name="altitude"/>, 0 to 1: whole within the air and the inner half of
        /// <see cref="AirSettings.Range"/> above it, easing to nothing over the outer half.
        /// </summary>
        public static double Reach(double altitude, double airHeight, in AirSettings settings)
        {
            double above = altitude - Math.Max(0.0, airHeight);
            double fadeStart = settings.Range * 0.5;
            if (above <= fadeStart)
                return 1.0;

            if (above >= settings.Range)
                return 0.0;

            double fade = (above - fadeStart) / Math.Max(1.0, settings.Range - fadeStart);
            return 1.0 - fade * fade * (3.0 - 2.0 * fade);
        }

        /// <summary>The air's density at <paramref name="altitude"/>, 1 at sea level, 0 above the air or with none.</summary>
        public static double AirDensity(double altitude, double airHeight)
        {
            if (airHeight <= 0.0 || altitude >= airHeight)
                return 0.0;

            return Math.Exp(-Math.Max(0.0, altitude) / (airHeight * ScaleHeightShare));
        }

        /// <summary>Heating on a ship moving at <paramref name="speed"/> metres a second through air of <paramref name="density"/>.</summary>
        public static double Heating(double density, double speed)
        {
            double kilometresPerSecond = Math.Max(0.0, speed) / MetresPerKilometre;
            return Math.Max(0.0, density) * kilometresPerSecond * kilometresPerSecond * kilometresPerSecond;
        }

        /// <summary>The entry glow for a heating value, 0 to 1, rising smoothly between the settings' start and full.</summary>
        public static double Glow(double heating, in AirSettings settings)
        {
            double span = Math.Max(1e-9, settings.GlowFull - settings.GlowStart);
            double share = Math.Clamp((heating - settings.GlowStart) / span, 0.0, 1.0);
            return share * share * (3.0 - 2.0 * share);
        }
    }
}
