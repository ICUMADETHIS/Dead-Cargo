using System.Collections.Generic;
using DeadCargo.Cameras;
using DeadCargo.Flight;
using DeadCargo.Sectors;
using DeadCargo.Sectors.Far;
using DeadCargo.Visuals;
using Unity.Mathematics;
using UnityEngine;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.EditorTools.GodMenu
{
    /// <summary>
    /// The chase camera, the cruise drive and where the player is in the star system, with jumps to a planet's orbit and home.
    /// </summary>
    internal sealed class FlightTab : GodMenuTab
    {
        private const double MetresPerKilometre = 1000.0;

        private static readonly string[] CameraLabels = { "Smooth Camera", "Kit Camera" };
        private static readonly bool[] CameraEnabled = { true, true };

        // Height above the top of the autopilot's arrival where an orbit jump puts the ship, in metres.
        private const double OrbitAbove = 10000.0;

        // Height above sea level for a low pass, in metres: under Ixara's clouds and above its highest ground.
        private const double LowHeight = 6000.0;

        private const string EngageLabel = "Engage";
        private const string CancelLabel = "Cancel Charge";
        private const string DropLabel = "Drop Out";

        private readonly string[] cruiseLabels = { EngageLabel };
        private readonly bool[] cruiseEnabled = { true };
        private readonly List<string> jumpLabels = new List<string>();
        private readonly List<int> jumpPlanets = new List<int>();
        private readonly List<JumpKind> jumpKinds = new List<JumpKind>();
        private string[] jumpLabelArray = new string[0];
        private int labelledBuild = -1;

        private CruiseDrive drive;
        private AtmosphereFlight air;
        private EntryGlow glow;
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
                        return $"Autopilot  ·  {drive.Speed / MetresPerKilometre:N0} km/s";
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
            air = vehicle != null ? vehicle.GetComponent<AtmosphereFlight>() : null;
            glow = vehicle != null ? vehicle.GetComponent<EntryGlow>() : null;
            space = SectorSpace.Active;
            presence = SectorPresence.Active;
            bodies = BackdropBodies.Active;
            RefreshJumps();
        }

        public override void OnGUI()
        {
            if (drive == null || space == null)
                return;

            DrawCamera();
            DrawCruise();
            DrawWhere();
            DrawAir();
            DrawGround();
            DrawJumps();
        }

        // Which camera follows the ship, for comparing the smooth chase camera with the kit's, and the ship's physics.
        private static void DrawCamera()
        {
            ChaseCamera chase = ChaseCamera.Active;
            if (chase == null)
                return;

            using (var section = new GodMenuGUI.SectionScope("Camera"))
            {
                if (!section.Expanded)
                    return;

                GodMenuGUI.Status(chase.IsSmooth, chase.IsSmooth
                    ? "Smooth: follows every frame, ship interpolated"
                    : "Kit: follows on the 50 Hz physics step, ship not interpolated");

                int clicked = GodMenuGUI.ButtonRow(CameraLabels, CameraEnabled, chase.IsSmooth ? 0 : 1, GodMenuGUI.Accent);
                if (clicked >= 0)
                    chase.SetSmooth(clicked == 0);

                Rigidbody body = chase.Body;
                if (body != null)
                {
                    GodMenuGUI.Field("Interpolation", body.interpolation.ToString());
                    GodMenuGUI.Field("Collision", body.collisionDetectionMode.ToString());
                }

                float frameTime = Time.smoothDeltaTime;
                GodMenuGUI.Field("Frame Rate", frameTime > 0f ? $"{1f / frameTime:N0} fps  ·  physics {1f / Time.fixedDeltaTime:N0} Hz" : "-");
                GodMenuGUI.Hint("The difference shows most above 50 fps, flying past rock or alongside a hunter.");
            }
        }

        // The air of the planet the ship is near: how high it reaches, how thick it is here, how fast the ship may go, and
        // how hot the hull runs.
        private void DrawAir()
        {
            if (air == null)
                return;

            using (var section = new GodMenuGUI.SectionScope("Air"))
            {
                if (!section.Expanded)
                    return;

                bool near = air.Planet >= 0;
                string planet = near && bodies != null ? bodies.NameOf(air.Planet) : "a planet";
                GodMenuGUI.Status(near, near
                    ? $"In {planet}'s reach  ·  air up to {air.AirHeight / MetresPerKilometre:N0} km"
                    : "No planet near: open-space flight");
                if (!near)
                    return;

                GodMenuGUI.Field("Altitude", $"{air.Altitude / MetresPerKilometre:N1} km");
                GodMenuGUI.Field("Density", $"{air.Density:0.000} of sea level");
                GodMenuGUI.Field("Top Speed", $"{air.TopSpeed:N0} m/s  ·  {air.SpeedFactor:0.0}x open space");
                GodMenuGUI.Field("Air Speed", $"{air.AirSpeed:N0} m/s");
                GodMenuGUI.Field("Gravity", $"{air.Gravity:0.00} m/s²");
                string shown = glow != null ? $", shown {glow.Shown * 100f:0}%" : "";
                GodMenuGUI.Field("Heating", $"{air.Heating:0.000}  ·  glow {air.Glow * 100f:0}%{shown}");
                GodMenuGUI.Hint("Air reach, speeds, gravity and heating are on the ship's Atmosphere Flight; the glow's look on its Entry Glow.");
            }
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
                    GodMenuGUI.Field("Above Ground", $"{ground.GroundDistance:N0} m");

                GodMenuGUI.Hint("Ground range, detail and the near clip are on the Planet Ground component of the Far Space object.");
            }
        }

        private void DrawCruise()
        {
            using (var section = new GodMenuGUI.SectionScope("Cruise Drive"))
            {
                if (!section.Expanded)
                    return;

                GodMenuGUI.Field("State", StateText());
                if (drive.State != CruiseState.Ready)
                {
                    GodMenuGUI.Field("Destination", DestinationName(drive.Destination, double.NaN));
                    GodMenuGUI.Field("Course", drive.Rounding >= 0 && bodies != null ? $"Round {bodies.NameOf(drive.Rounding)}" : "Straight");
                }

                GodMenuGUI.Field("Speed", $"{drive.Speed / MetresPerKilometre:N1} km/s");
                GodMenuGUI.Field("Nearest Surface", $"{drive.Clearance / MetresPerKilometre:N0} km  ·  {NearestName()}");
                GodMenuGUI.Field("Ahead", DestinationName(drive.HeadingIndex, drive.HeadingDistance));
                if (drive.State == CruiseState.Ready)
                    GodMenuGUI.Status(drive.Block == CruiseBlock.None, BlockText());

                cruiseLabels[0] = drive.State == CruiseState.Cruising ? DropLabel : drive.State == CruiseState.Charging ? CancelLabel : EngageLabel;
                cruiseEnabled[0] = drive.State != CruiseState.Ready || drive.CanEngage;
                if (GodMenuGUI.ButtonRow(cruiseLabels, cruiseEnabled) == 0)
                    drive.Toggle();

                GodMenuGUI.Hint("B in game does the same: the autopilot flies to the destination ahead with the controls locked. " +
                                "Turning, speed and arrival are on the Cruise Drive component of the ship.");
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
                    Vector3 sunward = SceneSun.Direction.normalized;
                    switch (jumpKinds[clicked])
                    {
                        case JumpKind.Orbit:
                            JumpAbove(planet, sunward, drive.ArrivalHeightOf(planet) + OrbitAbove, true);
                            break;
                        case JumpKind.Low:
                            JumpAbove(planet, sunward, LowHeight, false);
                            break;
                        case JumpKind.FarSide:
                            JumpAbove(planet, (float3)math.normalize(bodies.CentreOf(planet)), LowHeight, false);
                            break;
                        default:
                            drive.JumpTo(double3.zero, Vector3.forward, Vector3.up);
                            break;
                    }
                }

                GodMenuGUI.Hint("Jumps put the ship there at rest. Orbit is 10 km above where the autopilot arrives, facing the " +
                                "planet, over its sunlit side; Low Over is 6 km up there, level, facing the horizon; Far Side is " +
                                "6 km up on the side facing away from the sector, so the way home lies through the planet. The " +
                                "sector sleeps while you are away and wakes when you come back.");
            }
        }

        // Over the point of the planet in direction up from its centre: high up facing the planet, or low and level along
        // the horizon.
        private void JumpAbove(int planet, Vector3 up, double height, bool facingPlanet)
        {
            double3 centre = bodies.CentreOf(planet);
            double3 spot = centre + (double3)(float3)up * (bodies.RadiusOf(planet) + height);
            if (facingPlanet)
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
            jumpKinds.Clear();
            if (bodies != null)
            {
                for (int i = 0; i < bodies.Plans.Count; ++i)
                {
                    string name = bodies.NameOf(i);
                    AddJump("Orbit " + name, i, JumpKind.Orbit);
                    AddJump("Low Over " + name, i, JumpKind.Low);
                    AddJump("Far Side " + name, i, JumpKind.FarSide);
                }
            }

            string home = space != null && space.Definition != null ? space.Definition.DisplayName : "Sector";
            AddJump("Back To " + home, -1, JumpKind.Home);
            jumpLabelArray = jumpLabels.ToArray();
        }

        private void AddJump(string label, int planet, JumpKind kind)
        {
            jumpLabels.Add(label);
            jumpPlanets.Add(planet);
            jumpKinds.Add(kind);
        }

        private enum JumpKind
        {
            Home,
            Orbit,
            Low,
            FarSide
        }

        private string StateText()
        {
            switch (drive.State)
            {
                case CruiseState.Charging:
                    return drive.IsAligning
                        ? $"Aligning  ·  {drive.CourseError:0.0}° off course"
                        : $"Charging  ·  {drive.ChargeRemaining:0.0} s  ·  {drive.CourseError:0}° off course";
                case CruiseState.Cruising:
                    return "Autopilot";
                default:
                    return "Ready";
            }
        }

        private string BlockText()
        {
            switch (drive.Block)
            {
                case CruiseBlock.NoDestination:
                    return "Nothing within 30° of the nose";
                case CruiseBlock.AlreadyThere:
                    return "Already where cruise to it ends";
                case CruiseBlock.Field:
                    return $"Asteroid field on the route, {drive.FieldDistance / MetresPerKilometre:N1} km ahead";
                default:
                    return "Clear to engage";
            }
        }

        private string NearestName()
        {
            if (drive.NearestPlanet >= 0 && bodies != null)
                return bodies.NameOf(drive.NearestPlanet);

            return space.Definition != null ? $"{space.Definition.DisplayName}'s edge" : "the sector's edge";
        }

        // A destination's name, with its distance when one is given.
        private string DestinationName(int destination, double distance)
        {
            string name;
            switch (destination)
            {
                case CruiseDrive.NoHeading:
                    return "nothing within 30° of the nose";
                case CruiseDrive.HomeHeading:
                    name = space.Definition != null ? space.Definition.DisplayName : "the sector";
                    break;
                default:
                    name = bodies != null ? bodies.NameOf(destination) : "a planet";
                    break;
            }

            return double.IsNaN(distance) ? name : $"{name}  ·  {distance / MetresPerKilometre:N0} km";
        }

        private double SectorDistance()
        {
            SectorGrid grid = space.Grid;
            double3 min = (double3)(float3)grid.Min;
            return CruiseMath.DistanceToBox(drive.SystemPosition, min, min + (double3)(float3)grid.Size);
        }
    }
}
