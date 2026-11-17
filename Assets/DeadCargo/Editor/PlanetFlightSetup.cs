using System.Collections.Generic;
using DeadCargo.Controls;
using DeadCargo.Flight;
using DeadCargo.Sectors;
using DeadCargo.Sectors.Far;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.EditorTools
{
    /// <summary>
    /// Adds planet flight to the open sector: the cruise drive, atmosphere flight and entry glow on the player's ship; the
    /// drive's controls and level flight under the player, with the kit's auto roll switched off; the sector's presence on
    /// the Sector Space object, so the sector sleeps while the player is away; and, where the scene has planets, their
    /// ground on the Far Space object, on a layer of its own. Safe to run more than once; scene changes can be undone, the
    /// new layer is kept.
    /// </summary>
    internal static class PlanetFlightSetup
    {
        public const string MenuPath = "Dead Cargo/Setup/Phase 6.12 - Planet Flight (Open Scene)";

        private const string DialogTitle = "Phase 6.12 - Planet Flight";
        private const string UndoName = "Set Up Planet Flight";
        private const string ControlsName = "CruiseControls";
        private const string LevelControlsName = "LevelFlightControls";
        private const string KitAutoRollProperty = "autoRollEnabled";
        private const string KitAutoRollStrengthProperty = "autoRollStrength";
        private const string KitMaxAutoRollProperty = "maxAutoRoll";
        private const string LevelStrengthProperty = "strength";
        private const string LevelMaxRollProperty = "maxRoll";
        private const string LayerProperty = "layer";
        private const string GlowShaderProperty = "shader";

        /// <summary>
        /// True when the ship has a cruise drive, atmosphere flight and an entry glow with its shader, the player the drive's
        /// controls and level flight, and the sector its presence, and any planets their ground on its own layer.
        /// </summary>
        public static bool IsInstalled(Scene scene)
        {
            VehicleEngines3D ship = SceneSetupUtility.FindShip(scene);
            EntryGlow glow = ship != null ? ship.GetComponent<EntryGlow>() : null;
            bool flight = ship != null && ship.GetComponent<CruiseDrive>() != null && ship.GetComponent<AtmosphereFlight>() != null &&
                          glow != null && glow.HasShader &&
                          SceneSetupUtility.FindInScene<CruiseDriveInput>(scene).Count > 0 &&
                          SceneSetupUtility.FindInScene<LevelFlightInput>(scene).Count > 0 &&
                          SceneSetupUtility.FindInScene<SectorPresence>(scene).Count > 0;

            List<FarSpace> far = SceneSetupUtility.FindInScene<FarSpace>(scene);
            if (!flight || far.Count == 0)
                return flight;

            PlanetGround ground = far[0].GetComponent<PlanetGround>();
            return ground != null && ground.Layer > 0 && ground.Layer < 32 && LayerMask.LayerToName(ground.Layer) == PlanetGround.LayerName;
        }

        [MenuItem(MenuPath, priority = 213)]
        private static void Run()
        {
            Apply();
        }

        [MenuItem(MenuPath, true)]
        private static bool CanRun()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        /// <summary>Sets up the open scene. Returns false when it cannot, after a dialog explains why.</summary>
        internal static bool Apply()
        {
            Scene scene = SceneManager.GetActiveScene();
            VehicleEngines3D ship = SceneSetupUtility.FindShip(scene);
            if (ship == null || ship.GetComponent<Rigidbody>() == null)
                return Fail("Select the player's ship in the Hierarchy, then run this again.");

            GameAgent player = SceneSetupUtility.FindPlayer(scene);
            if (player == null)
                return Fail("No player game agent was found in the open scene.");

            List<SectorSpace> spaces = SceneSetupUtility.FindInScene<SectorSpace>(scene);
            if (spaces.Count == 0)
                return Fail("The open scene has no Sector Space. Run Set Up Sector first.");

            Shader glowShader = Shader.Find(EntryGlow.ShaderName);
            if (glowShader == null)
                return Fail($"The {EntryGlow.ShaderName} shader is missing. Click into Unity so it imports Shaders/EntryGlow.shader, then run this again.");

            List<FarSpace> far = SceneSetupUtility.FindInScene<FarSpace>(scene);
            int groundLayer = -1;
            if (far.Count > 0 && far[0].GetComponent<BackdropBodies>() != null)
            {
                groundLayer = ProjectLayers.Ensure(PlanetGround.LayerName);
                if (groundLayer < 0)
                    return Fail($"Every layer from {ProjectLayers.FirstFree} to {ProjectLayers.LastFree} is in use, so planets' ground has " +
                                "no layer. Free one in Project Settings > Tags and Layers, then run this again.");
            }

            Undo.SetCurrentGroupName(UndoName);
            int undoGroup = Undo.GetCurrentGroup();

            if (ship.GetComponent<CruiseDrive>() == null)
                Undo.AddComponent<CruiseDrive>(ship.gameObject);

            if (ship.GetComponent<AtmosphereFlight>() == null)
                Undo.AddComponent<AtmosphereFlight>(ship.gameObject);

            EntryGlow entryGlow = ship.GetComponent<EntryGlow>();
            if (entryGlow == null)
                entryGlow = Undo.AddComponent<EntryGlow>(ship.gameObject);

            var serializedGlow = new SerializedObject(entryGlow);
            serializedGlow.FindProperty(GlowShaderProperty).objectReferenceValue = glowShader;
            serializedGlow.ApplyModifiedProperties();

            if (spaces[0].GetComponent<SectorPresence>() == null)
                Undo.AddComponent<SectorPresence>(spaces[0].gameObject);

            // The kit's game agent collects input scripts from its children when play starts.
            if (SceneSetupUtility.FindInScene<CruiseDriveInput>(scene).Count == 0)
            {
                var controls = new GameObject(ControlsName, typeof(CruiseDriveInput));
                Undo.RegisterCreatedObjectUndo(controls, "Create Cruise Controls");
                Undo.SetTransformParent(controls.transform, player.transform, "Parent Cruise Controls");
                controls.transform.localPosition = Vector3.zero;
            }

            AddLevelFlight(scene, player);

            if (groundLayer > 0)
            {
                PlanetGround ground = far[0].GetComponent<PlanetGround>();
                if (ground == null)
                    ground = Undo.AddComponent<PlanetGround>(far[0].gameObject);

                var serializedGround = new SerializedObject(ground);
                serializedGround.FindProperty(LayerProperty).intValue = groundLayer;
                serializedGround.ApplyModifiedProperties();
            }

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"Planet flight set up in '{scene.name}': cruise drive on '{ship.name}', B to engage. Save the scene.", ship);
            return true;
        }

        // Wings level to the planet below, not the world's up: the kit's auto roll is switched off and its feel carried over.
        private static void AddLevelFlight(Scene scene, GameAgent player)
        {
            List<LevelFlightInput> found = SceneSetupUtility.FindInScene<LevelFlightInput>(scene);
            LevelFlightInput level;
            if (found.Count > 0)
            {
                level = found[0];
            }
            else
            {
                var controls = new GameObject(LevelControlsName, typeof(LevelFlightInput));
                Undo.RegisterCreatedObjectUndo(controls, "Create Level Flight Controls");
                Undo.SetTransformParent(controls.transform, player.transform, "Parent Level Flight Controls");
                controls.transform.localPosition = Vector3.zero;
                level = controls.GetComponent<LevelFlightInput>();
            }

            foreach (PlayerSpaceVehicleFlightControls kit in SceneSetupUtility.FindInScene<PlayerSpaceVehicleFlightControls>(scene))
            {
                var serializedKit = new SerializedObject(kit);
                SerializedProperty autoRoll = serializedKit.FindProperty(KitAutoRollProperty);
                if (autoRoll == null || !autoRoll.boolValue)
                    continue;

                var serializedLevel = new SerializedObject(level);
                serializedLevel.FindProperty(LevelStrengthProperty).floatValue = serializedKit.FindProperty(KitAutoRollStrengthProperty).floatValue;
                serializedLevel.FindProperty(LevelMaxRollProperty).floatValue = serializedKit.FindProperty(KitMaxAutoRollProperty).floatValue;
                serializedLevel.ApplyModifiedProperties();

                autoRoll.boolValue = false;
                serializedKit.ApplyModifiedProperties();
            }
        }

        private static bool Fail(string message)
        {
            EditorUtility.DisplayDialog(DialogTitle, message, "OK");
            return false;
        }
    }
}
