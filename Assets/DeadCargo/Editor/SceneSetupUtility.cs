using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.EditorTools
{
    /// <summary>Shared helpers for the phase setup tools.</summary>
    internal static class SceneSetupUtility
    {
        public const int IgnoreRaycastLayer = 2;
        public const int UILayer = 5;

        /// <summary>Returns the selected ship, or the only ship in the scene when nothing valid is selected.</summary>
        public static VehicleEngines3D FindShip(Scene scene)
        {
            GameObject selected = Selection.activeGameObject;
            if (selected != null && selected.scene == scene)
            {
                var selectedShip = selected.GetComponentInParent<VehicleEngines3D>();
                if (selectedShip != null)
                    return selectedShip;
            }

            List<VehicleEngines3D> ships = FindInScene<VehicleEngines3D>(scene);
            return ships.Count == 1 ? ships[0] : null;
        }

        /// <summary>Returns the player's game agent in the scene, or null.</summary>
        public static GameAgent FindPlayer(Scene scene)
        {
            foreach (GameAgent agent in FindInScene<GameAgent>(scene))
            {
                if (agent.IsPlayer)
                    return agent;
            }

            return null;
        }

        /// <summary>Finds components in a scene, including on inactive objects.</summary>
        public static List<T> FindInScene<T>(Scene scene) where T : Component
        {
            var results = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
                results.AddRange(root.GetComponentsInChildren<T>(true));

            return results;
        }

        /// <summary>Creates every missing folder in an asset path.</summary>
        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int split = path.LastIndexOf('/');
            string parent = path.Substring(0, split);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(split + 1));
        }
    }
}
