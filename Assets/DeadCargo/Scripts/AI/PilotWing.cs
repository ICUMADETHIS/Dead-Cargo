using System.Collections.Generic;
using UnityEngine;

namespace DeadCargo.AI
{
    /// <summary>
    /// Pilots hunting together. Each attacker comes at the target from its own side, pushed off the bearings of
    /// wingmen that are attacking from close to its own, and none fires while a wingman sits in its line of fire.
    /// Whoever sends the pilots owns the wing: pilots join when they arrive and leave when they go.
    /// </summary>
    public sealed class PilotWing
    {
        private readonly List<PilotBrain> members = new List<PilotBrain>();

        public IReadOnlyList<PilotBrain> Members => members;

        public void Join(PilotBrain pilot)
        {
            if (pilot != null && !members.Contains(pilot))
                members.Add(pilot);
        }

        public void Leave(PilotBrain pilot)
        {
            members.Remove(pilot);
        }

        public void Clear()
        {
            members.Clear();
        }

        /// <summary>
        /// The bearing (a unit direction from the target) the pilot should attack along: its own, pushed away from
        /// those of engaged wingmen within the spread angle (degrees) of it.
        /// </summary>
        public Vector3 SpreadBearing(PilotBrain pilot, Vector3 targetPosition, float spreadAngle)
        {
            Vector3 offset = pilot.Vehicle.transform.position - targetPosition;
            Vector3 own = offset.sqrMagnitude > 1e-6f ? offset.normalized : -pilot.Vehicle.transform.forward;

            int index = members.IndexOf(pilot);
            Vector3 push = Vector3.zero;
            for (int i = 0; i < members.Count; ++i)
            {
                PilotBrain wingman = members[i];
                if (wingman == pilot || !IsFlying(wingman) || wingman.State != PilotState.Engaging)
                    continue;

                // The earlier of two pilots on the very same bearing goes one way, the later the other.
                Vector3 theirs = wingman.Vehicle.transform.position - targetPosition;
                push += PilotMath.BearingRepulsion(own, theirs, spreadAngle, i < index ? 1f : -1f);
            }

            Vector3 bearing = own + push;
            return bearing.sqrMagnitude > 1e-6f ? bearing.normalized : own;
        }

        /// <summary>True when a wingman's ship is within the clearance (metres) of the line of fire between two points.</summary>
        public bool IsLineBlocked(PilotBrain pilot, Vector3 from, Vector3 to, float clearance)
        {
            for (int i = 0; i < members.Count; ++i)
            {
                PilotBrain wingman = members[i];
                if (wingman == pilot || !IsFlying(wingman))
                    continue;

                float distance = PilotMath.DistanceToSegment(wingman.Vehicle.transform.position, from, to, out float along);
                if (along > 0f && along < 1f && distance < clearance)
                    return true;
            }

            return false;
        }

        // Wingmen asleep with the sector, or wrecked, are not in the fight.
        private static bool IsFlying(PilotBrain wingman)
        {
            return wingman != null && wingman.isActiveAndEnabled && wingman.Vehicle != null && !wingman.Vehicle.Destroyed &&
                wingman.Vehicle.gameObject.activeInHierarchy;
        }
    }
}
