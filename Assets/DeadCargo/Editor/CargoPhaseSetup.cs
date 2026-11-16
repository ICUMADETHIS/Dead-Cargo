using DeadCargo.Cargo;
using DeadCargo.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.EditorTools
{
    /// <summary>Adds the phase 1 cargo systems to the open scene. Safe to run more than once.</summary>
    internal static class CargoPhaseSetup
    {
        private const string MenuPath = "Dead Cargo/Setup/Phase 1 - Cargo (Open Scene)";
        private const string DataFolder = "Assets/DeadCargo/Data/Cargo";
        private const string PickupRootName = "Cargo Pickups";
        private const string HudName = "Extraction HUD";

        [MenuItem(MenuPath, priority = 200)]
        private static void Run()
        {
            Scene scene = SceneManager.GetActiveScene();
            VehicleEngines3D ship = SceneSetupUtility.FindShip(scene);
            if (ship == null)
            {
                EditorUtility.DisplayDialog("Phase 1 - Cargo",
                    "Select the player's ship in the Hierarchy, then run this again.", "OK");
                return;
            }

            Undo.SetCurrentGroupName("Set Up Phase 1 Cargo");
            int undoGroup = Undo.GetCurrentGroup();

            CargoItemDefinition[] items = EnsureItems();
            EnsureHold(ship.gameObject);
            EnsurePickups(scene, ship.transform, items);
            SceneHierarchy.EnsureObject<ExtractionHud>(scene, HudName, SceneHierarchy.UI, true);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"Phase 1 cargo set up on '{ship.name}' in scene '{scene.name}'.", ship);
        }

        [MenuItem(MenuPath, true)]
        private static bool CanRun()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        private static CargoItemDefinition[] EnsureItems()
        {
            SceneSetupUtility.EnsureFolder(DataFolder);

            return new[]
            {
                EnsureItem("ScrapMetal", "Scrap Metal", 2f, 40),
                EnsureItem("FuelCells", "Fuel Cells", 4f, 150),
                EnsureItem("ReactorCore", "Reactor Core", 12f, 900)
            };
        }

        private static CargoItemDefinition EnsureItem(string fileName, string displayName, float unitMass, int unitValue)
        {
            string path = $"{DataFolder}/{fileName}.asset";
            var item = AssetDatabase.LoadAssetAtPath<CargoItemDefinition>(path);
            if (item != null)
                return item;

            item = ScriptableObject.CreateInstance<CargoItemDefinition>();
            var serialized = new SerializedObject(item);
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("unitMass").floatValue = unitMass;
            serialized.FindProperty("unitValue").intValue = unitValue;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(item, path);
            return item;
        }

        private static void EnsureHold(GameObject ship)
        {
            if (ship.GetComponent<CargoHold>() == null)
                Undo.AddComponent<CargoHold>(ship);
            if (ship.GetComponent<CargoLoadHandling>() == null)
                Undo.AddComponent<CargoLoadHandling>(ship);
        }

        private static void EnsurePickups(Scene scene, Transform ship, CargoItemDefinition[] items)
        {
            if (SceneSetupUtility.FindInScene<CargoPickup>(scene).Count > 0)
                return;

            var root = new GameObject(PickupRootName);
            Undo.RegisterCreatedObjectUndo(root, "Create Cargo Pickups");

            // Total mass exceeds the default hold so the full-hold case can be tested.
            (CargoItemDefinition Item, int Quantity)[] layout =
            {
                (items[0], 3), (items[0], 3), (items[1], 2), (items[1], 2), (items[2], 1), (items[2], 1)
            };

            Vector3 origin = ship.position + Vector3.up * 25f;
            for (int i = 0; i < layout.Length; ++i)
            {
                float side = i % 2 == 0 ? -12f : 12f;
                Vector3 position = origin + ship.forward * (60f + i * 35f) + ship.right * side;
                CreatePickup(root.transform, position, layout[i].Item, layout[i].Quantity);
            }
        }

        private static void CreatePickup(Transform parent, Vector3 position, CargoItemDefinition item, int quantity)
        {
            GameObject crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crate.name = $"Pickup - {item.DisplayName} x{quantity}";
            crate.transform.SetParent(parent, false);
            crate.transform.position = position;
            crate.transform.localScale = Vector3.one * 3f;

            // Keeps weapon and targeting raycasts from hitting the pickup volume.
            crate.layer = SceneSetupUtility.IgnoreRaycastLayer;

            Object.DestroyImmediate(crate.GetComponent<BoxCollider>());
            var trigger = crate.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 1.5f;

            crate.AddComponent<CargoPickup>().Initialize(item, quantity);
        }
    }
}
