using DeadCargo.Extraction;
using UnityEditor;
using UnityEngine;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.EditorTools.GodMenu
{
    /// <summary>Inspect the run, and drive both ways out: the jump drive and the extraction point.</summary>
    internal sealed class ExtractionTab : GodMenuTab
    {
        // Distance in front of the point, in metres, where Go To Point leaves the ship, facing the ring.
        private const float ApproachDistance = 80f;

        private static readonly string[] RunLabels = { "Restart Run" };
        private static readonly string[] DriveLabels = { "Start Spool", "Cancel Spool", "Jump Now" };
        private static readonly string[] PointLabels = { "Go To Point", "Start Uplink", "Open Now", "Close" };

        public override string Title => "Extraction";
        public override GodMenuGroup Group => GodMenuGroup.Run;
        public override int Order => 40;

        public override bool IsAvailable => GodMenuContext.IsPlaying && RunTracker.Active != null;

        public override string Summary
        {
            get
            {
                RunTracker tracker = GodMenuContext.IsPlaying ? RunTracker.Active : null;
                if (tracker == null)
                    return null;

                if (tracker.Drive != null && tracker.Drive.IsSpooling)
                    return $"{(tracker.Drive.IsHeld ? "Held" : "Spooling")}  {Mathf.CeilToInt(tracker.Drive.SpoolRemaining)} s";

                return tracker.IsOver ? StateLabel(tracker) : FormatDuration(tracker.Elapsed);
            }
        }

        public override void OnGUI()
        {
            RunTracker tracker = GodMenuContext.IsPlaying ? RunTracker.Active : null;
            using (new EditorGUI.DisabledScope(tracker == null))
            {
                DrawRun(tracker);
                DrawJumpDrive(tracker);
                DrawExtractionPoint();
            }
        }

        private static void DrawExtractionPoint()
        {
            using (var section = new GodMenuGUI.SectionScope("Extraction Point"))
            {
                if (!section.Expanded)
                    return;

                Vehicle vehicle = GodMenuContext.PlayerVehicle;
                Vector3 from = vehicle != null ? vehicle.transform.position : Vector3.zero;
                ExtractionPoint point = ExtractionPoint.FindNearest(from);
                if (point == null)
                {
                    GUILayout.Label("No extraction point in the scene. Run Set Up Sector in Edit Mode.", EditorStyles.wordWrappedMiniLabel);
                    return;
                }

                switch (point.State)
                {
                    case ExtractionPointState.Uplinking:
                        GodMenuGUI.Bar(point.Progress, $"UPLINK   {point.TimeRemaining:0} s to open", GodMenuGUI.Accent);
                        break;
                    case ExtractionPointState.Open:
                        GodMenuGUI.Bar(point.Progress, $"OPEN   {point.TimeRemaining:0} s left", GodMenuGUI.Positive);
                        break;
                    default:
                        GodMenuGUI.Bar(0f, "DORMANT", GodMenuGUI.Neutral);
                        break;
                }

                if (vehicle != null)
                    GodMenuGUI.Field("Distance", $"{point.DistanceTo(from):0} m");

                // Start Uplink ignores the activation range, so it can be tested from anywhere.
                bool[] enabled =
                {
                    vehicle != null,
                    point.CanStartUplink,
                    point.State != ExtractionPointState.Open,
                    point.State != ExtractionPointState.Dormant
                };

                switch (GodMenuGUI.ButtonRow(PointLabels, enabled))
                {
                    case 0:
                        MoveToPoint(vehicle, point);
                        break;
                    case 1:
                        point.StartUplink();
                        break;
                    case 2:
                        point.OpenNow();
                        break;
                    case 3:
                        point.Close();
                        break;
                }
            }
        }

        // Stops the ship in front of the ring, facing it, so flying straight on goes through.
        private static void MoveToPoint(Vehicle vehicle, ExtractionPoint point)
        {
            Transform ring = point.transform;
            Vector3 position = ring.position + ring.forward * ApproachDistance;
            vehicle.transform.SetPositionAndRotation(position, Quaternion.LookRotation(-ring.forward, Vector3.up));

            Rigidbody body = vehicle.CachedRigidbody;
            if (body != null && !body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }

        private static void DrawRun(RunTracker tracker)
        {
            using (var section = new GodMenuGUI.SectionScope("Run"))
            {
                if (!section.Expanded)
                    return;

                if (tracker == null)
                {
                    GodMenuGUI.Field("State", "Not running");
                    return;
                }

                GodMenuGUI.Field("State", StateLabel(tracker));
                GodMenuGUI.Field("Run Time", FormatDuration(tracker.Elapsed));
                GodMenuGUI.Field("Run Ship", tracker.Ship != null ? tracker.Ship.Label : "None boarded");

                if (tracker.IsOver)
                {
                    RunResult result = tracker.Result;
                    string label = result.Outcome == RunOutcome.Extracted ? "Cargo Banked" : "Cargo Lost";
                    GodMenuGUI.Field(label, $"{result.CargoValue:N0} cr  ·  {result.CargoMass:0.#} t");
                }

                if (GodMenuGUI.ButtonRow(RunLabels) == 0)
                    tracker.Restart();
            }
        }

        private static void DrawJumpDrive(RunTracker tracker)
        {
            using (var section = new GodMenuGUI.SectionScope("Jump Drive"))
            {
                if (!section.Expanded)
                    return;

                JumpDrive drive = FindDrive(tracker);
                if (drive == null)
                {
                    GUILayout.Label(GodMenuContext.IsPlaying ? "Board a ship that has a jump drive." : "Shown in Play Mode.", EditorStyles.miniLabel);
                    return;
                }

                if (drive.IsSpooling && drive.IsHeld)
                    GodMenuGUI.Bar(drive.SpoolProgress, $"HELD, TOO FAST (over {drive.MaxSpoolSpeed:0} m/s)   {drive.SpoolRemaining:0.0} s", GodMenuGUI.Negative);
                else if (drive.IsSpooling)
                    GodMenuGUI.Bar(drive.SpoolProgress, $"SPOOLING   {drive.SpoolRemaining:0.0} s", GodMenuGUI.Positive);
                else if (drive.HasJumped)
                    GodMenuGUI.Bar(1f, "JUMPED", GodMenuGUI.Neutral);
                else if (!drive.HasFuel)
                    GodMenuGUI.Bar((float)drive.DarkMatterAboard / drive.DarkMatterPerJump,
                        $"NEEDS DARK MATTER   {drive.DarkMatterAboard} / {drive.DarkMatterPerJump}", GodMenuGUI.Neutral);
                else
                    GodMenuGUI.Bar(0f, $"READY   {drive.EstimatedSpoolTime:0.0} s spool", GodMenuGUI.Neutral);

                // Jump Now forces a jump without fuel, so it stays usable for testing.
                bool[] enabled = { drive.CanSpool, drive.IsSpooling, drive.CanCompleteSpool };

                switch (GodMenuGUI.ButtonRow(DriveLabels, enabled))
                {
                    case 0:
                        drive.StartSpool();
                        break;
                    case 1:
                        drive.CancelSpool();
                        break;
                    case 2:
                        drive.CompleteSpool();
                        break;
                }
            }
        }

        // Prefers the drive of the ship the player is in, then the run ship's.
        private static JumpDrive FindDrive(RunTracker tracker)
        {
            Vehicle vehicle = GodMenuContext.PlayerVehicle;
            JumpDrive drive = vehicle != null ? vehicle.GetComponent<JumpDrive>() : null;
            if (drive == null && tracker != null)
                drive = tracker.Drive;

            return drive;
        }

        private static string StateLabel(RunTracker tracker)
        {
            if (!tracker.IsOver)
                return tracker.Drive != null && tracker.Drive.IsSpooling ? "Extracting" : "In progress";

            if (tracker.Result.Outcome == RunOutcome.Extracted)
                return "Extracted";

            return tracker.Result.ShipLost ? "Ship lost" : "Run lost";
        }

        private static string FormatDuration(float seconds)
        {
            int total = Mathf.FloorToInt(seconds);
            return $"{total / 60:00}:{total % 60:00}";
        }
    }
}
