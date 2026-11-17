using System.Collections.Generic;
using DeadCargo.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadCargo.EditorTools
{
    /// <summary>
    /// Makes the open scene's directional light its sun: the Sun Source in the Lighting settings, so every system and
    /// the sky agree on it, with no lens flare, because the space sky draws the star itself and a flat flare on top
    /// reads as fake, and with Space Lighting, which turns it each run and sets how dark space is. Safe to run more than
    /// once; every change can be undone.
    /// </summary>
    internal static class SunSetup
    {
        private const string UndoName = "Set Up Sun";

        /// <summary>The scene's sun light: its Sun Source, or else its brightest directional light. Null when it has none.</summary>
        public static Light Find(Scene scene)
        {
            if (scene == SceneManager.GetActiveScene() && RenderSettings.sun != null && RenderSettings.sun.gameObject.scene == scene)
                return RenderSettings.sun;

            Light brightest = null;
            List<Light> lights = SceneSetupUtility.FindInScene<Light>(scene);
            foreach (Light light in lights)
            {
                if (light.type == LightType.Directional && (brightest == null || light.intensity > brightest.intensity))
                    brightest = light;
            }

            return brightest;
        }

        /// <summary>True when the scene's sun is its Sun Source, has no lens flare, and carries Space Lighting.</summary>
        public static bool IsInstalled(Scene scene)
        {
            Light sun = Find(scene);
            return sun != null && sun.flare == null && sun.GetComponent<SpaceLighting>() != null &&
                   scene == SceneManager.GetActiveScene() && RenderSettings.sun == sun;
        }

        /// <summary>Sets up the open scene. Returns false when it has no directional light, after a dialog says so.</summary>
        internal static bool Apply()
        {
            Scene scene = SceneManager.GetActiveScene();
            Light sun = Find(scene);
            if (sun == null)
            {
                EditorUtility.DisplayDialog(UndoName, "The open scene has no directional light to be its sun. Add one, then run this again.", "OK");
                return false;
            }

            Undo.SetCurrentGroupName(UndoName);
            int undoGroup = Undo.GetCurrentGroup();

            if (sun.flare != null)
            {
                Undo.RecordObject(sun, UndoName);
                sun.flare = null;
            }

            if (RenderSettings.sun != sun)
            {
                SpaceSkySetup.RecordRenderSettings(UndoName);
                RenderSettings.sun = sun;
            }

            if (sun.GetComponent<SpaceLighting>() == null)
                Undo.AddComponent<SpaceLighting>(sun.gameObject);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"'{sun.name}' is the sun of '{scene.name}'.", sun);
            return true;
        }
    }
}
