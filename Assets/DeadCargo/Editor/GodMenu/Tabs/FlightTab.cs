using System.Collections.Generic;
using DeadCargo.Flight;
using DeadCargo.Sectors;
using DeadCargo.Sectors.Far;
using DeadCargo.Visuals;
using Unity.Mathematics;
using UnityEngine;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.EditorTools.GodMenu
{
    /// <summary>The cruise drive and where the player is in the star system, with jumps to a planet's orbit and home.</summary>
    internal sealed class FlightTab : GodMenuTab
    {
        private const double MetresPerKilometre = 1000.0;

        // Height above a planet's sea level where an orbit jump puts the ship, in metres: well above the clouds.
        private const double OrbitHeight = 25000.0;

        // Height above sea level for a low pass, in metres: under Ixara's clouds and above its highest ground.
        private const double LowHeight = 6000.0;

        // Marks the jump home in the list of jump targets.
        private const int HomeJump = -1;

        private const string EngageLabel = "Engage";
        private const string CancelLabel = "Cancel Charge";
        private const string DropLabel = "Drop Out";

        private readonly string[] cruiseLabels = { EngageLabel };
        private readonly bool[] cruiseEnabled = { true };
        private readonly List<string> jumpLabels = new List<string>();
        private readonly List<int> jumpPlanets = new List<int>();
        private readonly List<double> jumpHeights = new List<double>();
        private string[] jumpLabelArray = new string[0];
        private int labelledBuild = -1;

        private CruiseDrive drive;
        private SectorSpace space;
        private SectorPresence presence;
        private BackdropBodies bodies;

        public override string Title => "Flight";
        public override GodMenuGroup Group => GodMenuGroup.Run;
        public override int Order => 55;

        public override bool IsAvailable => drive != null && space != null;

        public override string Summary
        {
            get
            {
                if (drive == null || space == null)
                    return null;

                switch (drive.State)
                {
                    case CruiseState.Cruising:
                        return $"Cruise  ·  {drive.Speed / MetresPerKilometre:N0} km/s";
                    case CruiseState.Charging:
                        return $"Charging  ·  {Mathf.CeilToInt(drive.ChargeRemaining)} s";
                }

                return drive.IsAway ? $"Away  ·  {SectorDistance() / MetresPerKilometre:N0} km out" : "In the sector";
            }
        }

        public override void OnUpdate()
        {
            Vehicle vehicle = GodMenuContext.IsPlaying ? GodMenuContext.PlayerVehicle : null;
            drive = vehicle != null ? vehicle.GetComponent<CruiseDrive>() : null;
            space = SectorSpace.Active;
            presence = SectorPresence.Active;
            bodies = BackdropBodies.Active;
            RefreshJumps();
        }

        public override void OnGUI()
        {
            if (drive == null || space == null)
                return;

            DrawCruise();
            DrawWhere();
            DrawGround();
            DrawJumps();
        }

        // The planet's ground under the ship: whether it is solid, how high above it, and the pull.
        private void DrawGround()
        {
            PlanetGround ground = PlanetGround.Active;
            if (ground == null)
                return;

            using (var section = new GodMenuGUI.SectionScope("Ground"))
            {
                if (!section.Expanded)
                    return;

                bool solid = ground.HasGround;
                string planet = solid && bodies != null ? bodies.NameOf(ground.Planet) : "no planet";
                GodMenuGUI.Status(solid, solid ? $"Ground solid  ·  {planet}" : "No ground: above 40 km, in cruise, or in the sector");
                if (double.IsInfinity(ground.Altitude))
                    return;

                GodMenuGUI.Field("Above Sea Level", $"{ground.Altitude:N0} m");
                if (solid)
                {
                    GodMenuGUI.Field("Above Ground", $"{ground.GroundDistance:N0} m");
                    GodMenuGUI.Field("Gravity", $"{ground.Gravity:0.00} m/s²");
                }

                GodMenuGUI.Hint("Ground range, detail, gravity and the near clip are on the Planet Ground component of the Far Space object.");
            }
        }

        private void DrawCruise()
        {
            using (var section = new GodMenuGUI.SectionScope("Cruise Drive"))
            {
                if (!section.Expanded)
                    return;

                GodMenuGUI.Field("State", drive.State == CruiseState.Charging
                    ? $"Charging  ·  {drive.ChargeRemaining:0.0} s"
                    : drive.State.ToString());
                GodMenuGUI.Field("Speed", $"{drive.Speed / MetresPerKilometre:N1} km/s");
                GodMenuGUI.Field("Nearest Surface", $"{drive.Clearance / MetresPerKilometre:N0} km  ·  {NearestName()}");
                GodMenuGUI.Field("Ahead", HeadingName());
                if (drive.State == CruiseState.Ready)
                    GodMenuGUI.Status(!drive.IsBelowFloor, drive.IsBelowFloor ? "Below the drop height: climb to engage" : "Clear to engage");

                cruiseLabels[0] = drive.State == CruiseState.Cruising ? DropLabel : drive.State == CruiseState.Charging ? CancelLabel : EngageLabel;
                cruiseEnabled[0] = drive.State != CruiseState.Ready || drive.CanEngage;
                if (GodMenuGUI.ButtonRow(cruiseLabels, cruiseEnabled) == 0)
                    drive.Toggle();

                GodMenuGUI.Hint("B in game does the same. Speed and the drop-out height are on the Cruise Drive component of the ship.");
            }
        }

        private void DrawWhere()
        {
            using (var section = new GodMenuGUI.SectionScope("Where"))
            {
                if (!section.Expanded)
                    return;

                double3 position = drive.SystemPosition / MetresPerKilometre;
                GodMenuGUI.Field("System Position", $"{position.x:N0}, {position.y:N0}, {position.z:N0} km");
                GodMenuGUI.Field("From The Sector", $"{SectorDistance() / MetresPerKilometre:N1} km");

                bool asleep = presence != null && presence.IsAsleep;
                GodMenuGUI.Status(!asleep, asleep ? $"Sector asleep  ·  {presence.SleepingCount} objects" : "Sector awake");

                if (bodies == null)
                    return;

                for (int i = 0; i < bodies.Plans.Count; ++i)
                {
                    if (!bodies.IsBuilt(i))
                        continue;

                    double altitude = CruiseMath.Altitude(drive.SystemPosition, bodies.CentreOf(i), bodies.RadiusOf(i));
                    GodMenuGUI.Field(bodies.NameOf(i), $"{altitude / MetresPerKilometre:N0} km above  ·  radius {bodies.RadiusOf(i) / MetresPerKilometre:N0} km");
                }
            }
        }

        private void DrawJumps()
        {
            using (var section = new GodMenuGUI.SectionScope("Jumps"))
            {
                if (!section.Expanded)
                    return;

                int clicked = GodMenuGUI.ButtonRow(jumpLabelArray);
                if (clicked >= 0 && clicked < jumpPlanets.Count)
                {
                    int planet = jumpPlanets[clicked];
                    if (planet == HomeJump)
                        drive.JumpTo(double3.zero, Vector3.forward, Vector3.up);
                    else
                        JumpAbove(planet, jumpHeights[clicked]);
                }

                GodMenuGUI.Hint("Jumps put the ship there at rest, over the sunlit side. Orbit is 25 km up, facing the planet; Low " +
                                "Over is 6 km up, level, facing the horizon. The sector sleeps while you are away and wakes when you " +
                                "come back.");
            }
        }

        // Over the point under the sun, so the ground is lit: high up facing the planet, or low and level along the horizon.
        private void JumpAbove(int planet, double height)
        {
            double3 centre = bodies.CentreOf(planet);
            Vector3 up = SceneSun.Direction.normalized;
            double3 spot = centre + (double3)(float3)up * (bodies.RadiusOf(planet) + height);
            if (height >= OrbitHeight)
            {
                drive.JumpTo(spot, -up, Vector3.Cross(up, Vector3.right).sqrMagnitude > 1e-4f ? Vector3.Cross(up, Vector3.right) : Vector3.forward);
                return;
            }

            Vector3 horizon = Vector3.ProjectOnPlane(Vector3.forward, up);
            if (horizon.sqrMagnitude < 1e-4f)
                horizon = Vector3.ProjectOnPlane(Vector3.right, up);

            drive.JumpTo(spot, horizon.normalized, up);
        }

        private void RefreshJumps()
        {
            int build = bodies != null ? bodies.BuildCount : -1;
            if (build == labelledBuild && jumpLabelArray.Length > 0)
                return;

            labelledBuild = build;
            jumpLabels.Clear();
            jumpPlanets.Clear();
            jumpHeights.Clear();
            if (bodies != null)
            {
                for (int i = 0; i < bodies.Plans.Count; ++i)
                {
                    AddJump("Orbit " + bodies.NameOf(i), i, OrbitHeight);
                    AddJump("Low Over " + bodies.NameOf(i), i, LowHeight);
                }
            }

            string home = space != null && space.Definition != null ? space.Definition.DisplayName : "Sector";
            AddJump("Back To " + home, HomeJump, 0.0);
            jumpLabelArray = jumpLabels.ToArray();
        }

        private void AddJump(string label, int planet, double height)
        {
            jumpLabels.Add(label);
            jumpPlanets.Add(planet);
            jumpHeights.Add(height);
        }

        private string NearestName()
        {
            if (drive.NearestPlanet >= 0 && bodies != null)
                return bodies.NameOf(drive.NearestPlanet);

            return space.Definition != null ? $"{space.Definition.DisplayName}'s edge" : "the sector's edge";
        }

        private string HeadingName()
        {
            switch (drive.HeadingIndex)
            {
                case CruiseDrive.NoHeading:
                    return "nothing within 30° of the nose";
                case CruiseDrive.HomeHeading:
                    return space.Definition != null ? space.Definition.DisplayName : "the sector";
                default:
                    return bodies != null
                        ? $"{bodies.NameOf(drive.HeadingIndex)}  ·  {drive.HeadingDistance / MetresPerKilometre:N0} km"
                        : "a planet";
            }
        }

        private double SectorDistance()
        {
            SectorGrid grid = space.Grid;
            double3 min = (double3)(float3)grid.Min;
            return CruiseMath.DistanceToBox(drive.SystemPosition, min, min + (double3)(float3)grid.Size);
        }
    }
}
