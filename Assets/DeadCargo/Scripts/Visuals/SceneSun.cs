using UnityEngine;

namespace DeadCargo.Visuals
{
    /// <summary>
    /// The scene's sun light, found the way Unity finds it for the sky: the Sun Source in the Lighting settings, or else
    /// the brightest enabled directional light. The space sky draws its sun from the same light, so anything that asks
    /// here agrees with what the player sees. The light is remembered, and searched for again only when it goes away.
    /// </summary>
    public static class SceneSun
    {
        // When a scene has no directional light, look again at most this often, in seconds.
        private const float RetryInterval = 1f;

        private static Light cached;
        private static float nextSearch;

        /// <summary>The sun light, or null when the scene has none.</summary>
        public static Light Light
        {
            get
            {
                Light assigned = RenderSettings.sun;
                if (IsUsable(assigned))
                    return assigned;

                if (IsUsable(cached))
                    return cached;

                float now = Time.realtimeSinceStartup;
                if (now < nextSearch)
                    return null;

                cached = FindBrightest();
                nextSearch = cached != null ? 0f : now + RetryInterval;
                return cached;
            }
        }

        /// <summary>World direction towards the sun, or straight up when the scene has no sun light.</summary>
        public static Vector3 Direction
        {
            get
            {
                Light sun = Light;
                return sun != null ? -sun.transform.forward : Vector3.up;
            }
        }

        // Statics survive play sessions when domain reload is off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            cached = null;
            nextSearch = 0f;
        }

        private static bool IsUsable(Light light)
        {
            return light != null && light.type == LightType.Directional && light.isActiveAndEnabled;
        }

        private static Light FindBrightest()
        {
            Light brightest = null;
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (IsUsable(light) && (brightest == null || light.intensity > brightest.intensity))
                    brightest = light;
            }

            return brightest;
        }
    }
}
