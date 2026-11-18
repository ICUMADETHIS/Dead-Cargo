using SpaceGraphicsToolkit.Landscape;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadCargo.Sectors.Far
{
    /// <summary>
    /// Frees what Planet Forge's landscapes leave behind once the last of them is gone (Backlog #7). SGT keeps each
    /// landscape texture's height data in a static cache (<c>SgtLandscapeTextures</c>) and frees an entry only at a later
    /// rebuild, judged against the textures in use one rebuild before. The rebuild run as the last landscape goes still
    /// sees their textures in use, so their height data outlived every planet: it held memory between runs, and the editor
    /// reported it as a leak at the next domain reload. One more rebuild with no landscape left frees it all. SGT is never
    /// edited, so this runs from outside: after a scene unloads, before Unity unloads the textures, and as the editor
    /// leaves Play Mode (<c>PlanetTextureCacheRelease</c>).
    /// </summary>
    public static class PlanetTextureCache
    {
        /// <summary>Frees SGT's cached landscape data when no landscape remains. Does nothing while any does.</summary>
        public static void ReleaseUnused()
        {
            if (SgtLandscape.AllLandscapes.Count > 0)
                return;

            SgtLandscapeTextures.RebuildAll();
        }

        // Each play session, and once only when domain reload is off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Subscribe()
        {
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            ReleaseUnused();
        }
    }
}
