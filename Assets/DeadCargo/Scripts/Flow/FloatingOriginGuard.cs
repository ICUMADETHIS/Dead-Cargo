using UnityEngine;
using UnityEngine.SceneManagement;
using VSX.FloatingOriginSystem;

namespace DeadCargo.Flow
{
    /// <summary>
    /// Keeps the kit's floating origin off in every scene. The kit recentres the camera each time it strays a kilometre
    /// from the origin, but only shifts objects that carry its own <see cref="FloatingOriginObject"/>. Dead Cargo's
    /// asteroids, trails, cargo, extraction points and generated content do not, so they would stay put while the ship
    /// jumps back towards the start.
    /// <para>
    /// A sector fits well within floating-point precision: Polaris spans ±20 km, where positions are good to about
    /// 2 mm. When sectors outgrow that, every system must follow <c>SectorSpace.OriginOffset</c>, and this guard goes.
    /// </para>
    /// </summary>
    public static class FloatingOriginGuard
    {
        // Runs after the first scene's objects have woken, before any of them updates.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Disable();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Disable();
        }

        private static void Disable()
        {
            FloatingOriginManager origin = FloatingOriginManager.Instance;
            if (origin != null && origin.Activated)
                origin.Activated = false;
        }
    }
}
