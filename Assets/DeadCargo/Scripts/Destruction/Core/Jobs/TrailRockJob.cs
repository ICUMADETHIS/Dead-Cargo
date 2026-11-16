using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace DeadCargo.Destruction.Voxels
{
    /// <summary>
    /// Moves every trail rock one frame and writes the matrix it is drawn with.
    /// <para>
    /// A rock rides with its asteroid: its velocity eases towards the asteroid's own, plus a slow slide back along the
    /// trail. So when the asteroid drifts, turns or is pushed, its trail follows a few seconds behind, the way rubble
    /// held loosely by a rock's gravity would. Rocks that wander wider than the asteroid ease back towards the trail's
    /// line. A rock knocked loose, or whose asteroid is gone, simply flies on.
    /// </para>
    /// Rocks a physics stand-in moves are only drawn: physics already placed them. New rocks grow in and old ones shrink
    /// away, so none pops.
    /// </summary>
    [BurstCompile]
    public struct TrailRockJob : IJobParallelFor
    {
        public NativeArray<TrailRock> Rocks;

        [ReadOnly] public NativeArray<TrailOwner> Owners;

        /// <summary>
        /// Unity's own matrix type: instanced drawing only accepts <see cref="Matrix4x4"/> or a struct with an
        /// objectToWorld field, and rejects <see cref="float4x4"/>.
        /// </summary>
        [WriteOnly] public NativeArray<Matrix4x4> Matrices;

        public float DeltaTime;

        /// <summary>Seconds a rock takes to settle into its asteroid's motion after it changes.</summary>
        public float FollowTime;

        /// <summary>Speed a rock wandering wider than the trail eases back in at, per metre too wide, per second.</summary>
        public float GatherRate;

        /// <summary>Fastest a wandering rock eases back in, in metres per second.</summary>
        public float MaxGatherSpeed;

        /// <summary>Seconds a new rock takes to grow to full size.</summary>
        public float GrowTime;

        /// <summary>Seconds an old rock takes to shrink away.</summary>
        public float ShrinkTime;

        public void Execute(int i)
        {
            TrailRock rock = Rocks[i];
            rock.Age += DeltaTime;
            if (rock.Proxy < 0)
            {
                if (rock.Owner >= 0 && rock.Owner < Owners.Length && Owners[rock.Owner].Active)
                {
                    float3 target = Target(rock, Owners[rock.Owner], GatherRate, MaxGatherSpeed);
                    rock.Velocity = math.lerp(rock.Velocity, target, 1f - math.exp(-DeltaTime / FollowTime));
                }

                rock.Position += rock.Velocity * DeltaTime;
                float speed = math.length(rock.Spin);
                if (speed > 1e-5f)
                    rock.Rotation = math.normalize(math.mul(quaternion.AxisAngle(rock.Spin / speed, speed * DeltaTime), rock.Rotation));
            }

            Rocks[i] = rock;

            float grow = math.saturate(rock.Age / GrowTime);
            float shrink = math.saturate((rock.Lifetime - rock.Age) / ShrinkTime);

            // Never quite zero: the renderer inverts every matrix.
            float size = math.max(math.min(grow, shrink), 1e-3f);
            Matrices[i] = float4x4.TRS(rock.Position, rock.Rotation, rock.Scale * size);
        }

        /// <summary>The velocity a trail rock settles to: its asteroid's, plus its slide back and its own spread.</summary>
        /// <param name="gatherRate">See <see cref="GatherRate"/>.</param>
        /// <param name="maxGatherSpeed">See <see cref="MaxGatherSpeed"/>.</param>
        public static float3 Target(in TrailRock rock, in TrailOwner owner, float gatherRate, float maxGatherSpeed)
        {
            float3 target = owner.Velocity + owner.Back * rock.BackSpeed + rock.Spread;

            float3 offset = rock.Position - owner.Centre;
            float3 lateral = offset - math.dot(offset, owner.Back) * owner.Back;
            float width = math.length(lateral);
            float wide = width - owner.Radius;
            if (wide > 0f)
                target -= lateral / width * math.min(wide * gatherRate, maxGatherSpeed);

            return target;
        }
    }
}
