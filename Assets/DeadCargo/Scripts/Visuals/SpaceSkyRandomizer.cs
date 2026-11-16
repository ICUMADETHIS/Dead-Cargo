using System;
using DeadCargo.Sectors;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadCargo.Visuals
{
    /// <summary>
    /// Gives the sky a new look every time the scene loads: a freshly baked nebula, new stars, and a new tilt for the
    /// galactic band and its far and dust layers. The scene's sky material is never changed; the run uses a copy.
    /// The bake takes a fraction of a second while the scene loads, behind the loading screen. Play Mode only, so the
    /// editor always shows the authored sky.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpaceSkyRandomizer : MonoBehaviour
    {
        [Tooltip("0 gives a new sky every load. Any other value always gives the same sky, to bring back a look seen before.")]
        [SerializeField] private int fixedSeed;

        [Tooltip("Bake a new nebula shape. The colours and steps stay as authored.")]
        [SerializeField] private bool newNebula = true;

        [Tooltip("Place the stars and galaxies anew.")]
        [SerializeField] private bool newStars = true;

        [Tooltip("Turn the galactic band, the far layer and the dust lanes to new angles.")]
        [SerializeField] private bool newOrientation = true;

        [Tooltip("Largest tilt of the galactic band from level, in degrees.")]
        [SerializeField, Range(0f, 90f)] private float maxTilt = 40f;

        private Material authoredSky;
        private Material runSky;
        private Texture2D runMap;

        /// <summary>The seed of this load's sky. Set it as the fixed seed to see the same sky again.</summary>
        public int Seed { get; private set; }

        private void Awake()
        {
            authoredSky = RenderSettings.skybox;
            if (authoredSky == null || authoredSky.shader == null || authoredSky.shader.name != SpaceSkyProperties.ShaderName)
                return;

            Seed = fixedSeed != 0 ? fixedSeed : unchecked((int)SectorHash.Mix((uint)DateTime.UtcNow.Ticks ^ (uint)Environment.TickCount));
            var random = new SectorRandom(SectorHash.Mix(unchecked((uint)Seed)));
            int nebulaSeed = random.Range(0, SpaceSkyProperties.MaxSeed + 1);
            int starSeed = random.Range(0, SpaceSkyProperties.MaxSeed + 1);

            runSky = new Material(authoredSky) { name = authoredSky.name + " (Run)" };
            if (newStars)
                runSky.SetFloat(SpaceSkyProperties.StarSeed, starSeed);

            if (newOrientation)
            {
                runSky.SetVector(SpaceSkyProperties.Rotation, new Vector4(random.Range(-maxTilt, maxTilt), random.Range(0f, 360f), random.Range(-maxTilt, maxTilt), 0f));
                runSky.SetVector(SpaceSkyProperties.FarNebulaRotation, RandomAngles(ref random));
                runSky.SetVector(SpaceSkyProperties.DustRotation, RandomAngles(ref random));
            }

            if (newNebula)
            {
                NebulaShape shape = NebulaMap.ReadShape(authoredSky).WithSeed(nebulaSeed);
                runMap = NebulaMap.Create(shape, "Nebula (Run)", false);
                runSky.SetTexture(SpaceSkyProperties.NebulaMap, runMap);
            }

            RenderSettings.skybox = runSky;
        }

        private void OnDestroy()
        {
            if (runSky == null)
                return;

            // The scene may outlive this component, so it gets its authored sky back.
            if (RenderSettings.skybox == runSky)
                RenderSettings.skybox = authoredSky;

            Object.Destroy(runSky);
            if (runMap != null)
                Object.Destroy(runMap);
        }

        private static Vector4 RandomAngles(ref SectorRandom random)
        {
            return new Vector4(random.Range(0f, 360f), random.Range(0f, 360f), random.Range(0f, 360f), 0f);
        }
    }
}
