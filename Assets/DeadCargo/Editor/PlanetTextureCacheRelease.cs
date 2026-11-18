using DeadCargo.Sectors.Far;
using UnityEditor;

namespace DeadCargo.EditorTools
{
    /// <summary>
    /// Frees Planet Forge's cached landscape data as the editor leaves Play Mode, once every play-mode planet is gone, so
    /// nothing is left for the next domain reload to report as a leak (<see cref="PlanetTextureCache"/>, Backlog #7).
    /// </summary>
    [InitializeOnLoad]
    internal static class PlanetTextureCacheRelease
    {
        static PlanetTextureCacheRelease()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredEditMode)
                PlanetTextureCache.ReleaseUnused();
        }
    }
}
