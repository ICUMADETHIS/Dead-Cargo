using Unity.Mathematics;

namespace DeadCargo.Sectors.Field
{
    /// <summary>
    /// What a field rock looks like: the lumpy shapes every rock is drawn from, and the turn, stretch and tumble each
    /// rock takes from its seed. A shape is a radius for every direction, from three layers of noise (broad lobes,
    /// bumps and grit), so every level of detail samples the same surface and a switch between them barely shows.
    /// Plain value maths, so it runs in Burst.
    /// </summary>
    public static class FieldRockShape
    {
        /// <summary>Most shapes a field may use.</summary>
        public const int MaxShapes = 16;

        /// <summary>Levels of detail per shape, from 20 triangles up to 20,480.</summary>
        public const int Levels = 6;

        private const float LobeAmplitude = 0.22f;
        private const float LobeFrequency = 1.1f;
        private const float BumpAmplitude = 0.1f;
        private const float BumpFrequency = 2.7f;
        private const float GritAmplitude = 0.04f;
        private const float GritFrequency = 6.3f;

        /// <summary>Furthest and nearest a shape's surface reaches from its centre, for a rock of radius 1.</summary>
        public const float MaxRadius = 1f + LobeAmplitude + BumpAmplitude + GritAmplitude;
        public const float MinRadius = 1f - LobeAmplitude - BumpAmplitude - GritAmplitude;

        // Small rocks tumble faster than large ones, up to this many times the field's tumble for a 10 m rock.
        private const float ReferenceDiameter = 10f;
        private const float MaxTumbleFactor = 2f;

        private const float TwoPi = math.PI * 2f;

        // Below this length squared the camera's path is a point. ComicSurface.shader uses the same floor.
        private const float MinAheadSquared = 1e-4f;

        // Separates shape seeds from every other use of the sector seed.
        private const uint ShapeStream = 0x5A9E5EEDu;

        /// <summary>Where in the noise one shape is taken from, so each shape of a sector has its own form.</summary>
        public static float3 ShapeOffset(uint sectorSeed, int shape)
        {
            var random = new SectorRandom(SectorHash.Combine(SectorHash.Combine(sectorSeed, ShapeStream), (uint)shape));
            return new float3(random.NextFloat(), random.NextFloat(), random.NextFloat()) * 1000f;
        }

        /// <summary>A shape's radius in a unit direction, near 1, and its surface normal there.</summary>
        public static float Radius(float3 direction, float3 offset, out float3 normal)
        {
            float lobe = noise.snoise(direction * LobeFrequency + offset, out float3 lobeSlope);
            float bump = noise.snoise(direction * BumpFrequency + offset * 1.7f, out float3 bumpSlope);
            float grit = noise.snoise(direction * GritFrequency + offset * 2.3f, out float3 gritSlope);
            float radius = math.clamp(1f + lobe * LobeAmplitude + bump * BumpAmplitude + grit * GritAmplitude, MinRadius, MaxRadius);

            // The surface tilts away from the direction its radius grows in: only the gradient across the surface
            // counts, and the normal is the direction scaled by the radius, less that gradient.
            float3 gradient = lobeSlope * (LobeAmplitude * LobeFrequency) + bumpSlope * (BumpAmplitude * BumpFrequency) +
                              gritSlope * (GritAmplitude * GritFrequency);
            float3 across = gradient - math.dot(gradient, direction) * direction;
            normal = math.normalizesafe(direction * radius - across, direction);
            return radius;
        }

        /// <summary>The shape a rock is drawn with: the first value its seed gives, so culling reads it cheaply.</summary>
        public static int ShapeOf(uint seed, int shapes)
        {
            var random = new SectorRandom(seed);
            return random.Range(0, shapes);
        }

        /// <summary>
        /// A rock's size for its level of detail: its radius times the mean of its stretch. The comic surface's detail
        /// morph measures the same from the rock's matrix, so both agree on when a level starts.
        /// </summary>
        public static float DetailSize(in FieldRock rock, float stretch)
        {
            var random = new SectorRandom(rock.Seed);
            random.NextUInt();
            RandomRotation(ref random);
            float3 axes = Stretch(ref random, stretch);
            return rock.Radius * (axes.x + axes.y + axes.z) / 3f;
        }

        /// <summary>
        /// Size on screen, in pixels across, of a rock this big this far away. The comic surface's detail morph uses the
        /// same sum.
        /// </summary>
        public static float Pixels(float size, float distance, float pixelsPerRadian)
        {
            return size * 2f * pixelsPerRadian / math.max(distance, size);
        }

        /// <summary>
        /// The distance a rock's detail is judged from: to the nearest point of the camera's path over the next moments,
        /// from where it is to where it is heading. A fast camera so finds the rocks ahead already detailed when it
        /// reaches them. The comic surface's detail morph uses the same sum.
        /// </summary>
        /// <param name="ahead">From the camera to where it will be shortly. Zero judges from the camera alone.</param>
        public static float DetailDistance(float3 position, float3 eye, float3 ahead)
        {
            float3 toRock = position - eye;
            float along = math.saturate(math.dot(toRock, ahead) / math.max(math.lengthsq(ahead), MinAheadSquared));
            return math.length(toRock - ahead * along);
        }

        /// <summary>
        /// How a rock sits and turns, all from its seed: its own turn, its stretch along each axis, and the axis and rate,
        /// in radians a second, of its tumble.
        /// </summary>
        public static void Motion(in FieldRock rock, in FieldSettings settings, out quaternion turn, out float3 stretch,
            out float3 axis, out float rate)
        {
            var random = new SectorRandom(rock.Seed);
            random.NextUInt();
            turn = RandomRotation(ref random);
            stretch = Stretch(ref random, settings.Stretch);
            axis = RandomDirection(ref random);

            float diameter = math.max(rock.Radius * 2f, 0.1f);
            rate = settings.Tumble * math.min(math.sqrt(ReferenceDiameter / diameter), MaxTumbleFactor);
        }

        /// <summary>A rock's rotation at a time: its own turn, tumbled about its axis.</summary>
        public static quaternion Rotation(quaternion turn, float3 axis, float rate, float time)
        {
            return math.mul(quaternion.AxisAngle(axis, math.fmod(rate * time, TwoPi)), turn);
        }

        /// <summary>
        /// Where and how a rock is drawn at a time: its own turn and stretch from its seed, tumbling about its own axis.
        /// </summary>
        /// <param name="originOffset">World position of the sector's origin.</param>
        /// <param name="shrink">Share of its size the rock is drawn at, so the smallest grow in rather than pop.</param>
        public static float4x4 Matrix(in FieldRock rock, in FieldSettings settings, float time, float3 originOffset, float shrink = 1f)
        {
            Motion(rock, settings, out quaternion turn, out float3 stretch, out float3 axis, out float rate);
            return float4x4.TRS(rock.Position + originOffset, Rotation(turn, axis, rate, time), stretch * (rock.Radius * shrink));
        }

        // How far a rock stretches along each axis. Drawn after its turn: the order of draws is part of every layout.
        private static float3 Stretch(ref SectorRandom random, float stretch)
        {
            float x = random.NextFloat();
            float y = random.NextFloat();
            float z = random.NextFloat();
            return 1f + (new float3(x, y, z) * 2f - 1f) * stretch;
        }

        // Uniform over every orientation (Shoemake's method).
        private static quaternion RandomRotation(ref SectorRandom random)
        {
            float u1 = random.NextFloat();
            float u2 = random.NextFloat() * TwoPi;
            float u3 = random.NextFloat() * TwoPi;
            float a = math.sqrt(1f - u1);
            float b = math.sqrt(u1);
            return new quaternion(a * math.sin(u2), a * math.cos(u2), b * math.sin(u3), b * math.cos(u3));
        }

        private static float3 RandomDirection(ref SectorRandom random)
        {
            float z = random.NextFloat() * 2f - 1f;
            float angle = random.NextFloat() * TwoPi;
            float r = math.sqrt(math.max(0f, 1f - z * z));
            return new float3(r * math.cos(angle), r * math.sin(angle), z);
        }
    }
}
