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
        private const string HudName = "Extraction HUD";

        [MenuItem(MenuPath, priority = 200)]
        private static void Run()
        {
            Apply();
        }

        /// <summary>Sets up the open scene. Returns false when it cannot, after a dialog explains why.</summary>
        internal static bool Apply()
        {
            Scene scene = SceneManager.GetActiveScene();
            VehicleEngines3D ship = SceneSetupUtility.FindShip(scene);
            if (ship == null)
            {
                EditorUtility.DisplayDialog("Phase 1 - Cargo",
                    "Select the player's ship in the Hierarchy, then run this again.", "OK");
                return false;
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
            return true;
        }

        [MenuItem(MenuPath, true)]
        private static bool CanRun()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        // The first three are the range's pickups. The rest are placeholder loot for the hold grid (phase 6.15).
        private static readonly ItemSpec[] Items =
        {
            new ItemSpec("ScrapMetal", "Scrap Metal", "Scrap", 2f, 40, 1, 1, new Color32(158, 182, 185, 255)),
            new ItemSpec("FuelCells", "Fuel Cells", "Fuel", 4f, 150, 1, 2, new Color32(255, 121, 91, 255)),
            new ItemSpec("ReactorCore", "Reactor Core", "Reactor", 12f, 900, 2, 2, new Color32(242, 163, 58, 255)),
            new ItemSpec("GunCrate", "Gun Crate", "Guns", 6f, 380, 3, 2, new Color32(243, 238, 228, 255)),
            new ItemSpec("DataShards", "Data Shards", "Data", 0.5f, 220, 1, 1, new Color32(96, 224, 208, 255)),
            new ItemSpec("MedKits", "Med Kits", "Med", 1f, 90, 1, 1, new Color32(124, 226, 158, 255))
        };

        private const int PickupItemCount = 3;

        private static CargoItemDefinition[] EnsureItems()
        {
            SceneSetupUtility.EnsureFolder(DataFolder);

            var pickupItems = new CargoItemDefinition[PickupItemCount];
            for (int i = 0; i < Items.Length; ++i)
            {
                CargoItemDefinition item = EnsureItem(Items[i]);
                if (i < PickupItemCount)
                    pickupItems[i] = item;
            }

            return pickupItems;
        }

        private static CargoItemDefinition EnsureItem(in ItemSpec spec)
        {
            string path = $"{DataFolder}/{spec.FileName}.asset";
            var item = AssetDatabase.LoadAssetAtPath<CargoItemDefinition>(path);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<CargoItemDefinition>();
                var created = new SerializedObject(item);
                created.FindProperty("displayName").stringValue = spec.DisplayName;
                created.FindProperty("unitMass").floatValue = spec.UnitMass;
                created.FindProperty("unitValue").intValue = spec.UnitValue;
                WriteGridLook(created, spec);
                created.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(item, path);
                return item;
            }

            // Items made before the hold grid have no short name. They get their grid size and colour once, so later tuning stays.
            var serialized = new SerializedObject(item);
            if (string.IsNullOrEmpty(serialized.FindProperty("shortName").stringValue))
            {
                WriteGridLook(serialized, spec);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(item);
            }

            return item;
        }

        private static void WriteGridLook(SerializedObject item, in ItemSpec spec)
        {
            item.FindProperty("shortName").stringValue = spec.ShortName;
            item.FindProperty("size").vector2IntValue = spec.Size;
            item.FindProperty("blockColor").colorValue = spec.BlockColor;
        }

        private readonly struct ItemSpec
        {
            public ItemSpec(string fileName, string displayName, string shortName, float unitMass, int unitValue, int width, int height,
                Color blockColor)
            {
                FileName = fileName;
                DisplayName = displayName;
                ShortName = shortName;
                UnitMass = unitMass;
                UnitValue = unitValue;
                Size = new Vector2Int(width, height);
                BlockColor = blockColor;
            }

            public string FileName { get; }
            public string DisplayName { get; }
            public string ShortName { get; }
            public float UnitMass { get; }
            public int UnitValue { get; }
            public Vector2Int Size { get; }
            public Color BlockColor { get; }
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

            var root = new GameObject(SceneSetupUtility.CargoPickupsRoot);
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
