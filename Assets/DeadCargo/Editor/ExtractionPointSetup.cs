using System.Collections.Generic;
using DeadCargo.Extraction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.EditorTools
{
    /// <summary>
    /// Adds the phase 6.3 extraction point to the open scene: builds the prefab if it is missing, places one point
    /// ahead of the ship, and adds the uplink input to the player. Safe to run more than once.
    /// </summary>
    internal static class ExtractionPointSetup
    {
        /// <summary>Root object holding the scene's extraction points. Test range content, kept at the root.</summary>
        public const string RootName = "Extraction Points";
        public const string PrefabPath = PrefabFolder + "/ExtractionPoint.prefab";

        private const string MenuPath = "Dead Cargo/Setup/Phase 6.3 - Extraction Point (Open Scene)";
        private const string DialogTitle = "Phase 6.3 - Extraction Point";
        private const string PrefabFolder = "Assets/DeadCargo/Prefabs/Extraction";
        private const string MaterialPath = PrefabFolder + "/ExtractionPoint.mat";
        private const string ControlsName = "UplinkControls";

        // Matches the extraction point's default entry radius, so the ring shows the area that counts.
        private const float RingRadius = 25f;
        private const int RingSegments = 16;
        private const float SegmentThickness = 2f;
        private const float BeaconSize = 5f;
        private const float BeamHeight = 400f;
        private const float BeamWidth = 2.5f;
        private const float LightRange = 120f;
        private const float LightIntensity = 4f;

        // Where the first point goes, relative to the ship: right, up, forward. In metres.
        private static readonly Vector3 PlacementOffset = new Vector3(150f, 40f, 700f);

        [MenuItem(MenuPath, priority = 206)]
        private static void Run()
        {
            Apply();
        }

        [MenuItem(MenuPath, true)]
        private static bool CanRun()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        /// <summary>True when the scene has an extraction point and the uplink input.</summary>
        public static bool IsInstalled(Scene scene)
        {
            return SceneSetupUtility.FindInScene<ExtractionPoint>(scene).Count > 0 &&
                SceneSetupUtility.FindInScene<UplinkInput>(scene).Count > 0;
        }

        /// <summary>Sets up the open scene. Returns false when it cannot, after a dialog explains why.</summary>
        internal static bool Apply()
        {
            Scene scene = SceneManager.GetActiveScene();

            VehicleEngines3D ship = SceneSetupUtility.FindShip(scene);
            if (ship == null)
            {
                EditorUtility.DisplayDialog(DialogTitle, "Select the player's ship in the Hierarchy, then run this again.", "OK");
                return false;
            }

            GameAgent player = SceneSetupUtility.FindPlayer(scene);
            if (player == null)
            {
                EditorUtility.DisplayDialog(DialogTitle, "No player game agent was found in the open scene.", "OK");
                return false;
            }

            Undo.SetCurrentGroupName("Set Up Extraction Point");
            int undoGroup = Undo.GetCurrentGroup();

            ExtractionPoint prefab = EnsurePrefab();
            EnsurePoint(scene, ship.transform, prefab);
            EnsureInput(scene, player);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"Extraction point set up in scene '{scene.name}'.");
            return true;
        }

        private static ExtractionPoint EnsurePrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ExtractionPoint>(PrefabPath);
            if (existing != null)
                return existing;

            SceneSetupUtility.EnsureFolder(PrefabFolder);
            Material material = EnsureMaterial();
            var renderers = new List<Renderer>();

            var root = new GameObject("ExtractionPoint");
            var point = root.AddComponent<ExtractionPoint>();

            // A vertical ring facing the point's forward axis, so it reads as a gate to fly through.
            var ring = new GameObject("Ring").transform;
            ring.SetParent(root.transform, false);
            float segmentLength = 2f * Mathf.PI * RingRadius / RingSegments * 0.9f;
            for (int i = 0; i < RingSegments; ++i)
            {
                float angle = 360f * i / RingSegments;
                float radians = angle * Mathf.Deg2Rad;
                Transform segment = CreatePart(PrimitiveType.Cube, $"Segment {i}", ring, material, renderers);
                segment.localPosition = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f) * RingRadius;
                segment.localRotation = Quaternion.Euler(0f, 0f, angle + 90f);
                segment.localScale = new Vector3(segmentLength, SegmentThickness, SegmentThickness);
            }

            Transform beacon = CreatePart(PrimitiveType.Sphere, "Beacon", root.transform, material, renderers);
            beacon.localScale = Vector3.one * BeaconSize;

            // The cylinder primitive is two units tall, so half the height is its scale.
            Transform beam = CreatePart(PrimitiveType.Cylinder, "Open Beam", root.transform, material, renderers);
            beam.localPosition = Vector3.up * (BeamHeight * 0.5f);
            beam.localScale = new Vector3(BeamWidth, BeamHeight * 0.5f, BeamWidth);
            beam.gameObject.SetActive(false);

            var lightObject = new GameObject("Beacon Light");
            lightObject.transform.SetParent(root.transform, false);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = LightRange;
            light.intensity = LightIntensity;

            var serialized = new SerializedObject(point);
            SerializedProperty list = serialized.FindProperty("stateRenderers");
            list.arraySize = renderers.Count;
            for (int i = 0; i < renderers.Count; ++i)
                list.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
            serialized.FindProperty("openBeam").objectReferenceValue = beam.gameObject;
            serialized.FindProperty("beacon").objectReferenceValue = light;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<ExtractionPoint>();
        }

        // Visual only: colliders are removed so ships fly through and weapons ignore it.
        private static Transform CreatePart(PrimitiveType type, string name, Transform parent, Material material, List<Renderer> renderers)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);

            var renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderers.Add(renderer);
            return part.transform;
        }

        // Emission is enabled on the material; the point sets the actual colours per renderer at runtime.
        private static Material EnsureMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null)
                return material;

            material = new Material(Shader.Find("Standard")) { color = new Color(0.15f, 0.2f, 0.25f) };
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(0.3f, 0.4f, 0.45f));
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        private static void EnsurePoint(Scene scene, Transform ship, ExtractionPoint prefab)
        {
            if (SceneSetupUtility.FindInScene<ExtractionPoint>(scene).Count > 0)
                return;

            Transform root = FindOrCreateRoot(scene);
            Vector3 position = ship.position + ship.right * PlacementOffset.x + Vector3.up * PlacementOffset.y + ship.forward * PlacementOffset.z;

            // Yaw only, so the ring stands upright and faces back towards the ship.
            Vector3 toShip = ship.position - position;
            toShip.y = 0f;
            Quaternion facing = toShip.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toShip) : Quaternion.identity;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, scene);
            Undo.RegisterCreatedObjectUndo(instance, "Create Extraction Point");
            Undo.SetTransformParent(instance.transform, root, "Parent Extraction Point");
            instance.transform.SetPositionAndRotation(position, facing);
            instance.name = "Extraction Point";
        }

        private static Transform FindOrCreateRoot(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == RootName)
                    return root.transform;
            }

            var created = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(created, "Create Extraction Points");
            if (created.scene != scene)
                SceneManager.MoveGameObjectToScene(created, scene);

            return created.transform;
        }

        // The kit's game agent collects input scripts from its children when play starts.
        private static void EnsureInput(Scene scene, GameAgent player)
        {
            if (SceneSetupUtility.FindInScene<UplinkInput>(scene).Count > 0)
                return;

            var controls = new GameObject(ControlsName, typeof(UplinkInput));
            Undo.RegisterCreatedObjectUndo(controls, "Create Uplink Controls");
            Undo.SetTransformParent(controls.transform, player.transform, "Parent Uplink Controls");
            controls.transform.localPosition = Vector3.zero;
        }
    }
}
