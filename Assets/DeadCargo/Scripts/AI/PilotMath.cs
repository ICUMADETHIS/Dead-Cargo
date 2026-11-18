using UnityEngine;

namespace DeadCargo.AI
{
    /// <summary>
    /// Steering maths for AI pilots, in world space and full 3D: nothing is flattened onto a plane or measured from
    /// the world's origin. Pure vector maths with no Unity engine calls, so it can be checked outside Unity.
    /// </summary>
    public static class PilotMath
    {
        private const float Epsilon = 1e-6f;

        /// <summary>Two unit vectors at right angles to each other and to the axis. A zero axis counts as forward.</summary>
        public static void Perpendiculars(Vector3 axis, out Vector3 first, out Vector3 second)
        {
            Vector3 normal = axis.sqrMagnitude > Epsilon ? axis.normalized : Vector3.forward;

            // Any reference that is not parallel to the axis gives a perpendicular.
            Vector3 reference = Mathf.Abs(normal.y) < 0.99f ? Vector3.up : Vector3.right;
            first = Vector3.Cross(normal, reference).normalized;
            second = Vector3.Cross(normal, first);
        }

        /// <summary>
        /// A heading for breaking away from a target: straight away from it, turned off that line by the off angle
        /// (degrees) towards a side picked by <paramref name="side"/>, which runs 0 to 1 once round. Unit length.
        /// </summary>
        public static Vector3 BreakHeading(Vector3 awayFromTarget, float offAngle, float side)
        {
            Vector3 away = awayFromTarget.sqrMagnitude > Epsilon ? awayFromTarget.normalized : Vector3.forward;
            Perpendiculars(away, out Vector3 first, out Vector3 second);

            float around = side * 2f * Mathf.PI;
            Vector3 sideways = first * Mathf.Cos(around) + second * Mathf.Sin(around);
            float off = offAngle * Mathf.Deg2Rad;
            return away * Mathf.Cos(off) + sideways * Mathf.Sin(off);
        }

        /// <summary>
        /// Turns a heading towards an offset in the plane of the two perpendiculars. The offsets run -1 to 1 and their
        /// length sets how far to turn, up to the most angle (degrees). Unit length.
        /// </summary>
        public static Vector3 Deflect(Vector3 heading, Vector3 first, Vector3 second, float firstOffset, float secondOffset, float mostAngle)
        {
            Vector3 forward = heading.sqrMagnitude > Epsilon ? heading.normalized : Vector3.forward;
            Vector3 offset = first * firstOffset + second * secondOffset;
            float amount = Mathf.Clamp01(offset.magnitude);
            if (amount < Epsilon)
                return forward;

            float angle = amount * mostAngle * Mathf.Deg2Rad;
            return forward * Mathf.Cos(angle) + offset.normalized * Mathf.Sin(angle);
        }

        /// <summary>
        /// The point to steer for while circling a centre at the radius, in the plane at right angles to the normal:
        /// on the circle, the lead angle (degrees) ahead of where the position lies, turning anticlockwise about the
        /// normal. From outside the circle this draws the ship onto it.
        /// </summary>
        public static Vector3 OrbitGoal(Vector3 centre, Vector3 normal, Vector3 position, float radius, float leadAngle)
        {
            Vector3 up = normal.sqrMagnitude > Epsilon ? normal.normalized : Vector3.up;
            Vector3 offset = position - centre;
            Vector3 radial = offset - up * Vector3.Dot(offset, up);

            if (radial.sqrMagnitude > Epsilon)
                radial = radial.normalized;
            else
                Perpendiculars(up, out radial, out _);

            Vector3 tangent = Vector3.Cross(up, radial);
            float lead = leadAngle * Mathf.Deg2Rad;
            return centre + (radial * Mathf.Cos(lead) + tangent * Mathf.Sin(lead)) * radius;
        }

        /// <summary>Where a target last seen at a position, moving at a velocity, may be now: led by at most the most lead (seconds).</summary>
        public static Vector3 PredictedPosition(Vector3 position, Vector3 velocity, float secondsSinceSeen, float mostLead)
        {
            return position + velocity * Mathf.Clamp(secondsSinceSeen, 0f, mostLead);
        }

        /// <summary>0 below the start, 1 above the full value, and an S-curve between.</summary>
        public static float Smoothstep(float start, float full, float value)
        {
            if (full <= start)
                return value >= full ? 1f : 0f;

            float t = Mathf.Clamp01((value - start) / (full - start));
            return t * t * (3f - 2f * t);
        }

        /// <summary>A direction part way from one to the other by the weight (0 to 1). Unit length; opposite directions fall to the second.</summary>
        public static Vector3 Blend(Vector3 from, Vector3 to, float weight)
        {
            Vector3 blended = from.normalized * (1f - weight) + to.normalized * weight;
            return blended.sqrMagnitude > Epsilon ? blended.normalized : to.normalized;
        }

        /// <summary>
        /// The rotation a ship needs, in its own axes and radians: x pitch and y yaw to bring the nose onto the heading,
        /// z roll. Both given in the ship's local space. Hard turns bank towards the turn, so the ship pulls through it
        /// like a fighter (fully from the bank-full angle, in degrees, starting at bank start); small ones roll back to
        /// the level up by the levelling share. A heading straight behind is reached by pulling up into a loop.
        /// </summary>
        public static Vector3 AttitudeError(Vector3 localHeading, Vector3 localLevelUp, float bankStart, float bankFull, float levelling)
        {
            Vector3 heading = localHeading.sqrMagnitude > Epsilon ? localHeading.normalized : Vector3.forward;
            float angle = Mathf.Acos(Mathf.Clamp(heading.z, -1f, 1f));
            var lateral = new Vector2(heading.x, heading.y);
            float lateralLength = lateral.magnitude;

            // Rotating the nose about cross(forward, heading) brings it onto the heading; in local axes that is (-y, x, 0).
            Vector3 error;
            if (lateralLength > Epsilon)
                error = new Vector3(-heading.y, heading.x, 0f) * (angle / lateralLength);
            else
                error = heading.z >= 0f ? Vector3.zero : new Vector3(-angle, 0f, 0f);

            float bank = Smoothstep(bankStart * Mathf.Deg2Rad, bankFull * Mathf.Deg2Rad, angle);
            var levelUp = new Vector2(localLevelUp.x, localLevelUp.y);
            float levelLength = levelUp.magnitude;

            // The roof points where the ship should roll it: into the turn when banking, up when level. Pointing
            // nearly straight up or down there is no horizon to level to.
            Vector2 roof = Vector2.zero;
            if (lateralLength > Epsilon)
                roof += lateral * (bank / lateralLength);
            if (levelLength > 0.2f)
                roof += levelUp * ((1f - bank) / levelLength);

            if (roof.sqrMagnitude > Epsilon)
                error.z = Mathf.Atan2(-roof.x, roof.y) * Mathf.Lerp(levelling, 1f, bank);

            return error;
        }

        /// <summary>
        /// The steering input (-1 to 1) for one axis that turns through the error (radians) as fast as the ship allows
        /// and stops on it without overshooting. Rate is the turn rate now, radians a second; max accel is the turn
        /// acceleration at full input and damping the body's angular damping. Response sets how quickly small errors
        /// close, braking share how much of the steering is counted on to stop, and rate gain how hard the rate is held.
        /// </summary>
        public static float SteeringInput(float error, float rate, float maxAccel, float damping, float response, float brakingShare, float rateGain)
        {
            if (maxAccel <= Epsilon)
                return 0f;

            // Turning faster than this the ship could not hold against its damping, nor brake in time.
            float maxRate = damping > Epsilon ? 0.9f * maxAccel / damping : float.MaxValue;
            float size = Mathf.Abs(error);
            // Two-argument Min: the three-argument form allocates an array every call.
            float wantedRate = Mathf.Min(Mathf.Min(response * size, Mathf.Sqrt(2f * maxAccel * brakingShare * size)), maxRate);
            wantedRate *= Mathf.Sign(error);

            float accel = damping * wantedRate + rateGain * (wantedRate - rate);
            return Mathf.Clamp(accel / maxAccel, -1f, 1f);
        }

        /// <summary>
        /// Thrust inputs, in the ship's axes, to fly the heading at the wanted speed (metres a second). Off the nose the
        /// forward speed falls towards the corner share, which tightens the turn; the side and vertical thrusters cancel
        /// sideslip and push towards the heading by the assist share. Mass, linear damping and the thrust at full input
        /// on each axis come from the ship; gain sets how quickly speed is corrected, per second. Not clamped.
        /// </summary>
        public static Vector3 ThrustInputs(Vector3 localHeading, Vector3 localVelocity, float wantedSpeed, float cornerShare, float assist,
            float mass, float damping, Vector3 maxForces, float gain)
        {
            Vector3 heading = localHeading.sqrMagnitude > Epsilon ? localHeading.normalized : Vector3.forward;
            var wanted = new Vector3(
                heading.x * wantedSpeed * assist,
                heading.y * wantedSpeed * assist,
                wantedSpeed * Mathf.Max(cornerShare, heading.z));

            // Hold the wanted velocity against damping, then close the gap.
            Vector3 force = mass * (damping * wanted + gain * (wanted - localVelocity));
            return new Vector3(Divide(force.x, maxForces.x), Divide(force.y, maxForces.y), Divide(force.z, maxForces.z));
        }

        /// <summary>
        /// Speed (metres a second) for sitting on a target at the hold distance: its speed along the line, plus up to
        /// the most closing speed, more the further off the hold distance the ship is (gain per second).
        /// </summary>
        public static float TailSpeed(float distance, float holdDistance, float targetSpeedAlongLine, float mostClosing, float gain)
        {
            return targetSpeedAlongLine + Mathf.Clamp((distance - holdDistance) * gain, -mostClosing, mostClosing);
        }

        /// <summary>Moves a point towards a target with a lag (seconds): after one lag it has closed about two thirds of the gap. No lag snaps to the target.</summary>
        public static Vector3 Follow(Vector3 current, Vector3 target, float lag, float deltaTime)
        {
            if (lag <= Epsilon)
                return target;

            float share = 1f - Mathf.Exp(-Mathf.Max(0f, deltaTime) / lag);
            return current + (target - current) * share;
        }

        /// <summary>
        /// How far to turn a bearing (a direction from the target) away from a wingman's, so attackers come at the target
        /// from different sides: zero when the two are at least the spread angle (degrees) apart, growing to one unit as
        /// they meet. It points round the target, away from the wingman. Identical bearings split by the tie side (+1 or -1).
        /// </summary>
        public static Vector3 BearingRepulsion(Vector3 own, Vector3 wingman, float spreadAngle, float tieSide)
        {
            Vector3 mine = own.sqrMagnitude > Epsilon ? own.normalized : Vector3.forward;
            Vector3 theirs = wingman.sqrMagnitude > Epsilon ? wingman.normalized : Vector3.forward;
            float cos = Mathf.Clamp(Vector3.Dot(mine, theirs), -1f, 1f);
            float angle = Mathf.Acos(cos) * Mathf.Rad2Deg;
            if (angle >= spreadAngle)
                return Vector3.zero;

            // Along the sphere at our bearing, pointing away from theirs.
            Vector3 away = mine * cos - theirs;
            if (away.sqrMagnitude < Epsilon)
            {
                Perpendiculars(mine, out Vector3 first, out _);
                away = first * tieSide;
            }

            return away.normalized * (1f - angle / spreadAngle);
        }

        /// <summary>Distance from a point to the segment from start to end, and how far along it the nearest point lies, 0 to 1.</summary>
        public static float DistanceToSegment(Vector3 point, Vector3 start, Vector3 end, out float along)
        {
            Vector3 line = end - start;
            float lengthSquared = line.sqrMagnitude;
            along = lengthSquared > Epsilon ? Mathf.Clamp01(Vector3.Dot(point - start, line) / lengthSquared) : 0f;
            return Vector3.Distance(point, start + line * along);
        }

        private static float Divide(float value, float by)
        {
            return Mathf.Abs(by) > Epsilon ? value / by : 0f;
        }
    }
}
