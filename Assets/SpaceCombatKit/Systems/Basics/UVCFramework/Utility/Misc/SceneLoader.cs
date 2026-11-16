using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VSX.UniversalVehicleCombat
{
    /// <summary>
    /// Load a scene using a name or a build index.
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {

        [SerializeField]
        protected string sceneName;

        /// <summary>
        /// Load a scene with the name set in the inspector.
        /// </summary>
        public void LoadScene()
        {
            LoadNamedScene(sceneName);
        }

        /// <summary>
        /// Load a scene with a specified name.
        /// </summary>
        /// <param name="sceneName">The name of the scene.</param>
        public void LoadScene(string sceneName)
        {
            LoadNamedScene(sceneName);
        }

        internal static void LoadNamedScene(string sceneName)
        {
#if UNITY_EDITOR
            // Keep the disabled kit scenes navigable when they are opened for editor testing.
            foreach (var scene in UnityEditor.EditorBuildSettings.scenes)
            {
                if (scene.enabled || System.IO.Path.GetFileNameWithoutExtension(scene.path) != sceneName)
                    continue;

                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                    scene.path, new LoadSceneParameters(LoadSceneMode.Single));
                return;
            }
#endif
            SceneManager.LoadScene(sceneName);
        }

        /// <summary>
        /// Load a scene with a specified build index.
        /// </summary>
        /// <param name="sceneBuildIndex">The build index of the scene to load.</param>
        public void LoadScene(int sceneBuildIndex)
        {
            SceneManager.LoadScene(sceneBuildIndex);
        }


        /// <summary>
        /// Reload the current scene.
        /// </summary>
        public void ReloadActiveScene()
        {
            LoadNamedScene(SceneManager.GetActiveScene().name);
        }

        /// <summary>
        /// Quits the application.
        /// </summary>
        public void QuitApplication()
        {
            Application.Quit();
        }
    }

}
