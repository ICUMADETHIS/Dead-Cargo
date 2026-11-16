using DeadCargo.Cargo;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadCargo.EditorTools
{
    /// <summary>
    /// Creates the dark matter item, its glowing pickup prefab and, in a scene with none, a spread of pickups
    /// ahead of the ship. Part of the extraction setup. Safe to run more than once.
    /// </summary>
    internal static class DarkMatterSetup
    {
        public const string ItemPath = DataFolder + "/DarkMatter.asset";
        public const string PickupPrefabPath = PrefabFolder + "/DarkMatterPickup.prefab";

        private const string DataFolder = "Assets/DeadCargo/Data/Cargo";
        private const string PrefabFolder = "Assets/DeadCargo/Prefabs/Cargo";
        private const string MaterialPath = PrefabFolder + "/DarkMatter.mat";
        private const string ItemId = "DarkMatter";
        private const int UnitValue = 15;
        private const int UnitsPerPickup = 25;
        private const int PickupCount = 6;
        private const float PickupScale = 2.5f;
        private const float PickupTriggerRadius = 0.8f;

        private static readonly Color BaseColor = new Color(0.22f, 0.1f, 0.42f);
        private static readonly Color GlowColor = new Color(0.62f, 0.4f, 1f) * 2f;

        public static void Apply(Scene scene, Transform ship)
        {
            CargoItemDefinition item = EnsureItem();
            CargoPickup prefab = EnsurePickupPrefab(item);
            EnsurePickups(scene, ship, item, prefab);
        }

        /// <summary>True when the scene has at least one dark matter pickup to collect.</summary>
        public static bool HasPickups(Scene scene)
        {
            foreach (CargoPickup pickup in SceneSetupUtility.FindInScene<CargoPickup>(scene))
            {
                if (pickup.Item != null && pickup.Item.Slot == CargoSlot.DarkMatter)
                    return true;
            }

            return false;
        }

        private static CargoItemDefinition EnsureItem()
        {
            var item = AssetDatabase.LoadAssetAtPath<CargoItemDefinition>(ItemPath);
            if (item != null)
                return item;

            SceneSetupUtility.EnsureFolder(DataFolder);
            item = ScriptableObject.CreateInstance<CargoItemDefinition>();
            var serialized = new SerializedObject(item);
            serialized.FindProperty("id").stringValue = ItemId;
            serialized.FindProperty("displayName").stringValue = "Dark Matter";
            serialized.FindProperty("slot").enumValueIndex = (int)CargoSlot.DarkMatter;
            serialized.FindProperty("unitValue").intValue = UnitValue;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(item, ItemPath);
            return item;
        }

        private static CargoPickup EnsurePickupPrefab(CargoItemDefinition item)
        {
            var existing = AssetDatabase.LoadAssetAtPath<CargoPickup>(PickupPrefabPath);
            if (existing != null)
                return existing;

            SceneSetupUtility.EnsureFolder(PrefabFolder);

            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "DarkMatterPickup";
            sphere.transform.localScale = Vector3.one * PickupScale;

            // Keeps weapon and targeting raycasts from hitting the pickup volume.
            sphere.layer = SceneSetupUtility.IgnoreRaycastLayer;

            var trigger = sphere.GetComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = PickupTriggerRadius;
            sphere.GetComponent<MeshRenderer>().sharedMaterial = EnsureMaterial();

            var pickup = sphere.AddComponent<CargoPickup>();
            pickup.Initialize(item, UnitsPerPickup);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(sphere, PickupPrefabPath);
            Object.DestroyImmediate(sphere);
            return prefab.GetComponent<CargoPickup>();
        }

        private static Material EnsureMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null)
                return material;

            material = new Material(Shader.Find("Standard")) { color = BaseColor };
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", GlowColor);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        // Spread ahead of the ship, wider and further out than the phase 1 crates, under the same range content root.
        private static void EnsurePickups(Scene scene, Transform ship, CargoItemDefinition item, CargoPickup prefab)
        {
            if (HasPickups(scene))
                return;

            Transform root = FindOrCreateRoot(scene);
            for (int i = 0; i < PickupCount; ++i)
            {
                float side = (i % 2 == 0 ? -1f : 1f) * (35f + 10f * (i / 2));
                Vector3 position = ship.position + ship.forward * (90f + i * 40f) + ship.right * side + Vector3.up * (20f + 10f * (i % 3));

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, scene);
                Undo.RegisterCreatedObjectUndo(instance, "Create Dark Matter Pickup");
                Undo.SetTransformParent(instance.transform, root, "Parent Dark Matter Pickup");
                instance.transform.position = position;
                instance.name = $"Pickup - {item.DisplayName} x{UnitsPerPickup}";
            }
        }

        private static Transform FindOrCreateRoot(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == SceneSetupUtility.CargoPickupsRoot)
                    return root.transform;
            }

            var created = new GameObject(SceneSetupUtility.CargoPickupsRoot);
            Undo.RegisterCreatedObjectUndo(created, "Create Cargo Pickups");
            if (created.scene != scene)
                SceneManager.MoveGameObjectToScene(created, scene);

            return created.transform;
        }
    }
}
