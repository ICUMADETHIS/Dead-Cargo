using System.Collections.Generic;
using UnityEngine;

namespace DeadCargo.Sectors.Far
{
    /// <summary>
    /// Plans a run's backdrop system from its seed, top down, parent before child: the primary, then its moons, then any
    /// distant planet. Each body draws from its own stream of the system seed, so adding a moon never moves the primary
    /// and a pinned seed always gives the same sky. Pure managed maths, so the self-test runs it outside Unity.
    /// </summary>
    public static class BackdropLayout
    {
        /// <summary>Most moons, and most distant planets, a system can hold.</summary>
        public const int MaxPerKind = 4;

        // Spots tried for each body before it settles for the closest miss (the primary) or is left out (the rest).
        private const int Attempts = 64;

        // Streams of the system seed. Each body has its own, so one body never shifts another.
        private const uint PrimaryStream = 1u;
        private const uint MoonCountStream = 2u;
        private const uint DistantCountStream = 3u;
        private const uint MoonStream = 0x100u;
        private const uint DistantStream = 0x200u;
        private const uint SpinStream = 0x5917u;

        /// <summary>
        /// Fills <paramref name="plans"/> with the system for <paramref name="systemSeed"/>. Directions are unit vectors
        /// in sector space. <paramref name="sunRadius"/> is the sun's angular radius, corona included, in degrees.
        /// </summary>
        public static void Plan(uint systemSeed, in BackdropSettings settings, Vector3 towardsSun, float sunRadius,
            Vector3 startView, IReadOnlyList<PlanetEntry> entries, List<BackdropPlan> plans)
        {
            plans.Clear();
            if (entries == null || entries.Count == 0)
                return;

            var sky = new Sky(settings, Unit(towardsSun, Vector3.up), Mathf.Max(0f, sunRadius), Unit(startView, Vector3.forward));

            bool hasPrimary = PlanPrimary(SectorHash.Combine(systemSeed, PrimaryStream), sky, entries, plans);

            // Bodies within the minimum count are promised, so they settle for the closest miss; the rest are left out
            // when the sky has no room, as a crowded sky reads better than one body over another.
            var moonCount = new SectorRandom(SectorHash.Combine(systemSeed, MoonCountStream));
            int moons = CountOf(ref moonCount, settings.MinMoons, settings.MaxMoons);
            for (int i = 0; i < moons && hasPrimary; ++i)
                PlanSatellite(SectorHash.Combine(systemSeed, MoonStream + (uint)i), BodyRoles.Moon, i < settings.MinMoons, sky, entries, plans);

            var distantCount = new SectorRandom(SectorHash.Combine(systemSeed, DistantCountStream));
            int distant = CountOf(ref distantCount, settings.MinDistant, settings.MaxDistant);
            for (int i = 0; i < distant; ++i)
                PlanSatellite(SectorHash.Combine(systemSeed, DistantStream + (uint)i), BodyRoles.Distant, i < settings.MinDistant, sky, entries, plans);
        }

        /// <summary>
        /// How far a body at <paramref name="direction"/> with <paramref name="radius"/> degrees falls short of the rules
        /// for <paramref name="role"/>, in degrees. 0 when it meets them all. The primary must already be in
        /// <paramref name="plans"/> for a moon.
        /// </summary>
        public static float Miss(Vector3 direction, float radius, BodyRoles role, in BackdropSettings settings, Vector3 towardsSun,
            float sunRadius, Vector3 startView, List<BackdropPlan> plans)
        {
            var sky = new Sky(settings, Unit(towardsSun, Vector3.up), Mathf.Max(0f, sunRadius), Unit(startView, Vector3.forward));
            return Miss(direction, radius, role, sky, plans);
        }

        private static bool PlanPrimary(uint seed, in Sky sky, IReadOnlyList<PlanetEntry> entries, List<BackdropPlan> plans)
        {
            // Every value is drawn whether or not it is used, so the chance never changes what the rest draws.
            var random = new SectorRandom(seed);
            bool present = random.NextFloat() < sky.Settings.PrimaryChance;
            int entry = PickEntry(random.NextFloat(), entries, BodyRoles.Primary, plans);
            float size = random.Range(sky.Settings.PrimaryMinSize, Mathf.Max(sky.Settings.PrimaryMinSize, sky.Settings.PrimaryMaxSize));
            if (!present || entry < 0)
                return false;

            float radius = size * 0.5f;
            Vector3 closest = Vector3.forward;
            float closestMiss = float.MaxValue;
            for (int i = 0; i < Attempts; ++i)
            {
                Vector3 direction = InBand(ref random, sky.Settings.MaxElevation);
                float miss = Miss(direction, radius, BodyRoles.Primary, sky, plans);
                if (miss <= 0f)
                {
                    plans.Add(Body(seed, BodyRoles.Primary, entry, direction, size, true, sky.Settings));
                    return true;
                }

                if (miss < closestMiss)
                {
                    closestMiss = miss;
                    closest = direction;
                }
            }

            plans.Add(Body(seed, BodyRoles.Primary, entry, closest, size, false, sky.Settings));
            return true;
        }

        // A moon or a distant planet. When no spot meets every rule, a required one takes the closest miss and any other
        // is left out.
        private static void PlanSatellite(uint seed, BodyRoles role, bool required, in Sky sky, IReadOnlyList<PlanetEntry> entries,
            List<BackdropPlan> plans)
        {
            var random = new SectorRandom(seed);
            int entry = PickEntry(random.NextFloat(), entries, role, plans);
            float size = role == BodyRoles.Moon
                ? random.Range(sky.Settings.MoonMinSize, Mathf.Max(sky.Settings.MoonMinSize, sky.Settings.MoonMaxSize))
                : random.Range(sky.Settings.DistantMinSize, Mathf.Max(sky.Settings.DistantMinSize, sky.Settings.DistantMaxSize));
            if (entry < 0)
                return;

            float radius = size * 0.5f;
            int primary = role == BodyRoles.Moon ? IndexOf(plans, BodyRoles.Primary) : -1;
            if (role == BodyRoles.Moon && primary < 0)
                return;

            Vector3 closest = Vector3.forward;
            float closestMiss = float.MaxValue;
            for (int i = 0; i < Attempts; ++i)
            {
                Vector3 direction = role == BodyRoles.Moon
                    ? NearPrimary(ref random, plans[primary], radius, sky.Settings)
                    : InBand(ref random, sky.Settings.MaxElevation);

                float miss = Miss(direction, radius, role, sky, plans);
                if (miss <= 0f)
                {
                    plans.Add(Body(seed, role, entry, direction, size, true, sky.Settings));
                    return;
                }

                if (miss < closestMiss)
                {
                    closestMiss = miss;
                    closest = direction;
                }
            }

            if (required)
                plans.Add(Body(seed, role, entry, closest, size, false, sky.Settings));
        }

        private static float Miss(Vector3 direction, float radius, BodyRoles role, in Sky sky, List<BackdropPlan> plans)
        {
            BackdropSettings settings = sky.Settings;
            float fromSun = Vector3.Angle(direction, sky.Sun);
            float miss = Mathf.Max(0f, sky.SunRadius + settings.SunClearance + radius - fromSun);

            if (role == BodyRoles.Primary)
            {
                miss = Mathf.Max(miss, settings.PrimaryMinFromSun - fromSun);
                miss = Mathf.Max(miss, fromSun - settings.PrimaryMaxFromSun);
                if (settings.StartViewClearance > 0f)
                    miss = Mathf.Max(miss, settings.StartViewClearance + radius - Vector3.Angle(direction, sky.StartView));
            }

            if (role != BodyRoles.Moon)
                miss = Mathf.Max(miss, Mathf.Abs(Elevation(direction)) - settings.MaxElevation);

            for (int i = 0; i < plans.Count; ++i)
            {
                BackdropPlan other = plans[i];
                float apart = Vector3.Angle(direction, other.Direction);
                miss = Mathf.Max(miss, other.AngularRadius + radius + settings.BodyClearance - apart);

                if (role == BodyRoles.Moon && other.Role == BodyRoles.Primary)
                {
                    float gap = apart - other.AngularRadius;
                    miss = Mathf.Max(miss, settings.MoonMinGap - gap);
                    miss = Mathf.Max(miss, gap - settings.MoonMaxGap);
                }
            }

            return miss;
        }

        // Even over the band's area: height uniform in the sine of elevation. Two draws.
        private static Vector3 InBand(ref SectorRandom random, float maxElevation)
        {
            float band = Mathf.Sin(Mathf.Clamp(maxElevation, 0f, 90f) * Mathf.Deg2Rad);
            float height = random.Range(-band, band);
            float azimuth = random.Range(0f, 2f * Mathf.PI);
            float across = Mathf.Sqrt(Mathf.Max(0f, 1f - height * height));
            return new Vector3(Mathf.Sin(azimuth) * across, height, Mathf.Cos(azimuth) * across);
        }

        // A spot beside the primary, its centre a gap from the primary's edge, on any side. Two draws.
        private static Vector3 NearPrimary(ref SectorRandom random, in BackdropPlan primary, float radius, in BackdropSettings settings)
        {
            float gap = random.Range(settings.MoonMinGap, Mathf.Max(settings.MoonMinGap, settings.MoonMaxGap));
            float around = random.Range(0f, 2f * Mathf.PI);
            float offset = (primary.AngularRadius + gap) * Mathf.Deg2Rad;

            Vector3 centre = primary.Direction;
            Vector3 helper = Mathf.Abs(centre.y) < 0.9f ? Vector3.up : Vector3.right;
            Vector3 side = Vector3.Cross(centre, helper).normalized;
            Vector3 up = Vector3.Cross(centre, side);
            Vector3 aside = side * Mathf.Cos(around) + up * Mathf.Sin(around);
            return (centre * Mathf.Cos(offset) + aside * Mathf.Sin(offset)).normalized;
        }

        private static BackdropPlan Body(uint seed, BodyRoles role, int entry, Vector3 direction, float size, bool met, in BackdropSettings settings)
        {
            var random = new SectorRandom(SectorHash.Combine(seed, SpinStream));
            float tilt = random.Range(0f, Mathf.Clamp(settings.MaxTilt, 0f, 90f)) * Mathf.Deg2Rad;
            float heading = random.Range(0f, 2f * Mathf.PI);
            float phase = random.Range(0f, 360f);
            float speed = random.Range(settings.MinSpin, Mathf.Max(settings.MinSpin, settings.MaxSpin));
            var axis = new Vector3(Mathf.Sin(tilt) * Mathf.Cos(heading), Mathf.Cos(tilt), Mathf.Sin(tilt) * Mathf.Sin(heading));
            return new BackdropPlan(role, entry, direction, size, axis, phase, speed, met);
        }

        // Weighted by each entry's weight, among those that can play the role and are not already in the system; when
        // every such entry is in use, repeats are allowed. -1 when none can play the role. One draw, made by the caller.
        private static int PickEntry(float roll, IReadOnlyList<PlanetEntry> entries, BodyRoles role, List<BackdropPlan> plans)
        {
            float unusedTotal = 0f;
            float total = 0f;
            for (int i = 0; i < entries.Count; ++i)
            {
                PlanetEntry entry = entries[i];
                if (entry == null || !entry.CanPlay(role))
                    continue;

                total += entry.Weight;
                if (!IsUsed(plans, i))
                    unusedTotal += entry.Weight;
            }

            if (total <= 0f)
                return -1;

            bool unusedOnly = unusedTotal > 0f;
            float target = roll * (unusedOnly ? unusedTotal : total);
            int last = -1;
            for (int i = 0; i < entries.Count; ++i)
            {
                PlanetEntry entry = entries[i];
                if (entry == null || !entry.CanPlay(role) || (unusedOnly && IsUsed(plans, i)))
                    continue;

                last = i;
                target -= entry.Weight;
                if (target < 0f)
                    return i;
            }

            return last;
        }

        private static int CountOf(ref SectorRandom random, int min, int max)
        {
            int low = Mathf.Clamp(min, 0, MaxPerKind);
            int high = Mathf.Clamp(max, low, MaxPerKind);
            return random.Range(low, high + 1);
        }

        private static bool IsUsed(List<BackdropPlan> plans, int entry)
        {
            for (int i = 0; i < plans.Count; ++i)
            {
                if (plans[i].Entry == entry)
                    return true;
            }

            return false;
        }

        private static int IndexOf(List<BackdropPlan> plans, BodyRoles role)
        {
            for (int i = 0; i < plans.Count; ++i)
            {
                if (plans[i].Role == role)
                    return i;
            }

            return -1;
        }

        /// <summary>Degrees above (positive) or below the sector's plane.</summary>
        public static float Elevation(Vector3 direction)
        {
            return Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) * Mathf.Rad2Deg;
        }

        private static Vector3 Unit(Vector3 value, Vector3 fallback)
        {
            return value.sqrMagnitude > 1e-8f ? value.normalized : fallback;
        }

        // What every body is measured against, worked out once per plan.
        private readonly struct Sky
        {
            public Sky(in BackdropSettings settings, Vector3 sun, float sunRadius, Vector3 startView)
            {
                Settings = settings;
                Sun = sun;
                SunRadius = sunRadius;
                StartView = startView;
            }

            public BackdropSettings Settings { get; }
            public Vector3 Sun { get; }
            public float SunRadius { get; }
            public Vector3 StartView { get; }
        }
    }
}
