using DeadCargo.Extraction;
using UnityEditor;
using UnityEngine;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.EditorTools.GodMenu
{
    /// <summary>Inspect the run and drive the jump drive.</summary>
    internal sealed class ExtractionTab : GodMenuTab
    {
        private static readonly string[] RunLabels = { "Restart Run" };
        private static readonly string[] DriveLabels = { "Start Spool", "Cancel Spool", "Jump Now" };

        public override string Title => "Extraction";
        public override int Order => 38;

        public override void OnGUI()
        {
            RunTracker tracker = GodMenuContext.IsPlaying ? RunTracker.Active : null;
            if (GodMenuContext.IsPlaying && tracker == null)
                EditorGUILayout.HelpBox("No run tracker in the scene. Run the Phase 5 setup from the Project tab.", MessageType.Warning);

            using (new EditorGUI.DisabledScope(tracker == null))
            {
                DrawRun(tracker);
                DrawJumpDrive(tracker);
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

                if (drive.IsSpooling)
                    GodMenuGUI.Bar(drive.SpoolProgress, $"SPOOLING   {drive.SpoolRemaining:0.0} s", GodMenuGUI.Positive);
                else
                    GodMenuGUI.Bar(0f, drive.HasJumped ? "JUMPED" : $"READY   {drive.EstimatedSpoolTime:0.0} s spool", GodMenuGUI.Neutral);

                bool[] enabled = { drive.CanSpool, drive.IsSpooling, drive.IsSpooling || drive.CanSpool };

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

            return tracker.Result.Outcome == RunOutcome.Extracted ? "Extracted" : "Ship lost";
        }

        private static string FormatDuration(float seconds)
        {
            int total = Mathf.FloorToInt(seconds);
            return $"{total / 60:00}:{total % 60:00}";
        }
    }
}
