using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeadCargo.DevTesting
{
    internal static class DevTestMenu
    {
        private const string OpenMenu = "Dead Cargo/Open Dev Test Range";
        private const string PlayMenu = "Dead Cargo/Play Dev Test Range";

        [MenuItem(OpenMenu, priority = 100)]
        private static void OpenRange()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(DevTestConsole.RangePath);
        }

        [MenuItem(PlayMenu, priority = 101)]
        private static void PlayRange()
        {
            var range = AssetDatabase.LoadAssetAtPath<SceneAsset>(DevTestConsole.RangePath);
            if (range == null)
            {
                Debug.LogError("Cannot find the Dead Cargo Dev Test Range scene.");
                return;
            }

            var previousStartScene = EditorSceneManager.playModeStartScene;
            EditorSceneManager.playModeStartScene = range;
            SessionState.SetString(PreviousPlaySceneKey, AssetDatabase.GetAssetPath(previousStartScene));
            SessionState.SetBool(RestorePlaySceneKey, true);
            EditorApplication.isPlaying = true;
        }

        private const string RestorePlaySceneKey = "DeadCargo.DevTest.RestorePlayScene";
        private const string PreviousPlaySceneKey = "DeadCargo.DevTest.PreviousPlayScene";

        [InitializeOnLoadMethod]
        private static void InitializePlaySceneRestoration()
        {
            EditorApplication.playModeStateChanged += RestorePlayScene;
        }

        private static void RestorePlayScene(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(RestorePlaySceneKey, false))
                return;

            var previousPath = SessionState.GetString(PreviousPlaySceneKey, "");
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(previousPath);
            SessionState.EraseBool(RestorePlaySceneKey);
            SessionState.EraseString(PreviousPlaySceneKey);
        }

        [MenuItem(OpenMenu, true)]
        [MenuItem(PlayMenu, true)]
        private static bool CanOpenRange()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }
    }
}