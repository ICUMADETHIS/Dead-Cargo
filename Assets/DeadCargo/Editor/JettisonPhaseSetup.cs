using DeadCargo.Cargo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.EditorTools
{
    /// <summary>Adds the phase 2 jettison systems to the open scene. Safe to run more than once.</summary>
    internal static class JettisonPhaseSetup
    {
        private const string MenuPath = "Dead Cargo/Setup/Phase 2 - Jettison (Open Scene)";
        private const string DialogTitle = "Phase 2 - Jettison";
        private const string PrefabFolder = "Assets/DeadCargo/Prefabs/Cargo";
        private const string CratePrefabPath = PrefabFolder + "/CargoCrate.prefab";
        private const string ControlsName = "CargoControls";

        [MenuItem(MenuPath, priority = 201)]
        private static void Run()
        {
            Scene scene = SceneManager.GetActiveScene();

            VehicleEngines3D ship = SceneSetupUtility.FindShip(scene);
            if (ship == null)
            {
                EditorUtility.DisplayDialog(DialogTitle,
                    "Select the player's ship in the Hierarchy, then run this again.", "OK");
                return;
            }

            if (ship.GetComponent<CargoHold>() == null)
            {
                EditorUtility.DisplayDialog(DialogTitle,
                    $"'{ship.name}' has no cargo hold. Run the Phase 1 setup first.", "OK");
                return;
            }

            GameAgent player = SceneSetupUtility.FindPlayer(scene);
            if (player == null)
            {
                EditorUtility.DisplayDialog(DialogTitle, "No player game agent was found in the open scene.", "OK");
                return;
            }

            Undo.SetCurrentGroupName("Set Up Phase 2 Jettison");
            int undoGroup = Undo.GetCurrentGroup();

            CargoPickup cratePrefab = EnsureCratePrefab();
            EnsureJettison(ship.gameObject, cratePrefab);
            EnsureInput(scene, player);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"Phase 2 jettison set up on '{ship.name}' in scene '{scene.name}'.", ship);
        }

        [MenuItem(MenuPath, true)]
        private static bool CanRun()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        private static CargoPickup EnsureCratePrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<CargoPickup>(CratePrefabPath);
            if (existing != null)
                return existing;

            SceneSetupUtility.EnsureFolder(PrefabFolder);

            GameObject crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crate.name = "CargoCrate";
            crate.transform.localScale = Vector3.one * 3f;

            // Keeps weapon and targeting raycasts from hitting the pickup volume.
            crate.layer = SceneSetupUtility.IgnoreRaycastLayer;

            Object.DestroyImmediate(crate.GetComponent<BoxCollider>());
            var trigger = crate.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 1.5f;

            // Lets the crate drift after ejection and settle over time.
            var body = crate.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.linearDamping = 0.5f;
            body.angularDamping = 0.5f;

            crate.AddComponent<CargoPickup>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(crate, CratePrefabPath);
            Object.DestroyImmediate(crate);
            return prefab.GetComponent<CargoPickup>();
        }

        private static void EnsureJettison(GameObject ship, CargoPickup cratePrefab)
        {
            CargoJettison jettison = ship.GetComponent<CargoJettison>();
            if (jettison == null)
                jettison = Undo.AddComponent<CargoJettison>(ship);

            var serialized = new SerializedObject(jettison);
            SerializedProperty prefabProperty = serialized.FindProperty("cratePrefab");
            if (prefabProperty.objectReferenceValue == null)
            {
                prefabProperty.objectReferenceValue = cratePrefab;
                serialized.ApplyModifiedProperties();
            }
        }

        // The kit's game agent collects input scripts from its children when play starts.
        private static void EnsureInput(Scene scene, GameAgent player)
        {
            if (SceneSetupUtility.FindInScene<CargoJettisonInput>(scene).Count > 0)
                return;

            var controls = new GameObject(ControlsName, typeof(CargoJettisonInput));
            Undo.RegisterCreatedObjectUndo(controls, "Create Cargo Controls");
            Undo.SetTransformParent(controls.transform, player.transform, "Parent Cargo Controls");
            controls.transform.localPosition = Vector3.zero;
        }
    }
}
