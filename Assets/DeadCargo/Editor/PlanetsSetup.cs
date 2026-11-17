using System.Collections.Generic;
using DeadCargo.EditorTools.Visuals;
using DeadCargo.Sectors.Far;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadCargo.EditorTools
{
    /// <summary>
    /// Adds planets to the open scene: the Far Space layer, a Far Space object in the Environment folder that draws the far
    /// world behind the main camera and builds the run's planets, and the planet catalogue, copied from Planet Forge the
    /// first time. The scene's sun is left as it is: the far space lights its planets with a copy of it. Safe to run more
    /// than once; scene changes can be undone, the new layer and the catalogue are kept.
    /// </summary>
    internal static class PlanetsSetup
    {
        public const string MenuPath = "Dead Cargo/Setup/Phase 6.9 - Planets (Open Scene)";

        private const string DialogTitle = "Phase 6.9 - Planets";
        private const string UndoName = "Add Planets";
        private const string ObjectName = "Far Space";
        private const string LayerProperty = "layer";
        private const string ShaderProperty = "backdropShader";
        private const string CatalogProperty = "catalog";

        /// <summary>The scene's far space, or null when it has none.</summary>
        public static FarSpace Find(Scene scene)
        {
            List<FarSpace> found = SceneSetupUtility.FindInScene<FarSpace>(scene);
            return found.Count > 0 ? found[0] : null;
        }

        /// <summary>True when the scene's far space has its layer, its backdrop shader and a filled catalogue, and the scene has a sun.</summary>
        public static bool IsInstalled(Scene scene)
        {
            FarSpace far = Find(scene);
            if (far == null || far.Layer <= 0 || far.Layer >= 32 || LayerMask.LayerToName(far.Layer) != FarSpace.LayerName)
                return false;

            using (var serializedFar = new SerializedObject(far))
            {
                if (serializedFar.FindProperty(ShaderProperty).objectReferenceValue == null)
                    return false;
            }

            var bodies = far.GetComponent<BackdropBodies>();
            if (bodies == null || bodies.Catalog == null || bodies.Catalog.Count == 0)
                return false;

            return SunSetup.Find(scene) != null;
        }

        [MenuItem(MenuPath, priority = 212)]
        private static void Run()
        {
            Apply();
        }

        [MenuItem(MenuPath, true)]
        private static bool CanRun()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        /// <summary>Adds planets to the open scene. Returns false when it cannot, after a dialog explains why.</summary>
        internal static bool Apply()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (SunSetup.Find(scene) == null)
                return Fail("Planets are lit by the scene's sun, and the open scene has no directional light. Add one and press Set Up Sun, then run this again.");

            PlanetCatalog catalog = PlanetCatalogBuilder.Find();
            if (catalog == null || catalog.Count == 0)
            {
                if (!EditorUtility.DisplayDialog(DialogTitle,
                        "Planets come from Planet Forge. Copy its 26 planets into Dead Cargo now?\n\nEach planet's demo scene opens " +
                        "beside this one for a moment and closes unsaved. It takes a minute or two.", "Copy Planets", "Cancel"))
                    return false;

                catalog = PlanetCatalogBuilder.Build();
                if (catalog == null || catalog.Count == 0)
                    return Fail("No planets were copied. The Console says why.");
            }

            Shader backdropShader = Shader.Find(FarSpace.BackdropShaderName);
            if (backdropShader == null)
                return Fail($"The '{FarSpace.BackdropShaderName}' shader was not found. Check the Console for shader errors.");

            int layer = ProjectLayers.Ensure(FarSpace.LayerName);
            if (layer < 0)
                return Fail($"Every layer from {ProjectLayers.FirstFree} to {ProjectLayers.LastFree} is in use, so far space has no layer. Free one in " +
                            "Project Settings > Tags and Layers, then run this again.");

            Undo.SetCurrentGroupName(UndoName);
            int undoGroup = Undo.GetCurrentGroup();

            FarSpace far = SceneHierarchy.EnsureObject<FarSpace>(scene, ObjectName, SceneHierarchy.Environment);
            var serializedFar = new SerializedObject(far);
            serializedFar.FindProperty(LayerProperty).intValue = layer;
            serializedFar.FindProperty(ShaderProperty).objectReferenceValue = backdropShader;
            serializedFar.ApplyModifiedProperties();

            BackdropBodies bodies = far.GetComponent<BackdropBodies>();
            if (bodies == null)
                bodies = Undo.AddComponent<BackdropBodies>(far.gameObject);

            var serializedBodies = new SerializedObject(bodies);
            serializedBodies.FindProperty(CatalogProperty).objectReferenceValue = catalog;
            serializedBodies.ApplyModifiedProperties();

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = far.gameObject;
            Debug.Log($"Planets added to '{scene.name}': {catalog.Count} in the catalogue, drawn on layer {layer} ({FarSpace.LayerName}). Save the scene.", far);
            return true;
        }

        private static bool Fail(string message)
        {
            EditorUtility.DisplayDialog(DialogTitle, message, "OK");
            return false;
        }
    }
}
