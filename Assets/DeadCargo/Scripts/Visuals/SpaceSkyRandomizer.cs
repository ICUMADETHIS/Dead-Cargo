using System;
using System.Threading.Tasks;
using DeadCargo.Flow;
using DeadCargo.Sectors;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadCargo.Visuals
{
    /// <summary>
    /// Gives the sky a new look every time the scene loads: a freshly baked nebula, new stars, and a new tilt for the
    /// galactic band and its far and dust layers. In a generated sector the look comes from the sector's seed, so a
    /// pinned sector seed brings its sky back too, and a seed changed in Play Mode bakes a new sky. The scene's sky
    /// material is never changed; the run uses a copy. Play Mode only, so the editor always shows the authored sky.
    /// The nebula takes about a third of a second to bake. Behind the loading screen, and for a seed changed in play, it
    /// bakes on worker threads while the frames run on, and the loading screen waits for it (phase 6.6). Behind the
    /// loading screen it starts once the scene's start-up frame is over, so it does not slow that frame down. In a scene
    /// the game starts in, with no loading screen to hide it, it bakes before the first frame instead.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpaceSkyRandomizer : MonoBehaviour
    {
        // Separates the sky from every other use of the sector seed.
        private const uint SkyStream = 0x5C1E5u;

        private const string LoadLabel = "Sky";
        private const string PaintTaskLabel = "Painting sky";
        private const string MapName = "Nebula (Run)";

        private enum NebulaTiming
        {
            // On the main thread, on every core, before the first frame is drawn.
            BeforeFirstFrame,

            // On workers from now.
            OnWorkers,

            // On workers from the next frame, once the scene's objects have started.
            AfterStartUp
        }

        [Tooltip("0 follows the sector's seed, or gives a new sky every load outside a generated sector. Any other value " +
                 "always gives the same sky, to bring back a look seen before.")]
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
        private SectorSpace space;
        private Task<ushort[]> nebulaBake;
        private LoadTask paintTask;
        private NebulaShape waitingShape;
        private int waitingFrame = -1;

        /// <summary>The seed of this load's sky. Set it as the fixed seed to see the same sky again.</summary>
        public int Seed { get; private set; }

        /// <summary>True while a new nebula is waiting or baking on worker threads; the sky shows the last one until it is in.</summary>
        public bool IsBaking => paintTask != null;

        // Behind the loading screen, after the start-up frame: all but two cores, for the main thread and the field's jobs.
        private static int HiddenThreads => Mathf.Max(1, SystemInfo.processorCount - 2);

        // In play: half the cores, so the frames the player sees run on.
        private static int PlayThreads => Mathf.Max(1, SystemInfo.processorCount / 2);

        // Start, not Awake, so the sector space has drawn the run's seed whatever order objects wake in.
        private void Start()
        {
            authoredSky = RenderSettings.skybox;
            if (authoredSky == null || authoredSky.shader == null || authoredSky.shader.name != SpaceSkyProperties.ShaderName)
                return;

            space = SectorSpace.Active;
            if (space != null)
                space.SeedChanged += OnSeedChanged;

            Bake(SceneFlow.IsEntering ? NebulaTiming.AfterStartUp : NebulaTiming.BeforeFirstFrame);
        }

        private void Update()
        {
            // Start and the first Update share the scene's start-up frame; the bake waits for the frame after.
            if (waitingFrame >= 0 && Time.frameCount > waitingFrame)
            {
                waitingFrame = -1;
                LaunchNebula(waitingShape, HiddenThreads);
            }

            if (nebulaBake != null && nebulaBake.IsCompleted)
                FinishNebula();
        }

        private void OnDestroy()
        {
            if (space != null)
                space.SeedChanged -= OnSeedChanged;

            // A bake still running finishes on its worker, and its texels are dropped.
            DropNebula();

            if (runSky == null)
                return;

            // The scene may outlive this component, so it gets its authored sky back.
            if (RenderSettings.skybox == runSky)
                RenderSettings.skybox = authoredSky;

            Object.Destroy(runSky);
            ReplaceMap(null);
            runSky = null;
        }

        private void OnSeedChanged()
        {
            Bake(NebulaTiming.OnWorkers);
        }

        // The material, stars and tilts change at once. The nebula bakes on workers, or here before the first frame.
        private void Bake(NebulaTiming timing)
        {
            using LoadScope measure = LoadReport.Measure(LoadLabel);
            Seed = ChooseSeed();
            var random = new SectorRandom(SectorHash.Mix(unchecked((uint)Seed)));
            int nebulaSeed = random.Range(0, SpaceSkyProperties.MaxSeed + 1);
            int starSeed = random.Range(0, SpaceSkyProperties.MaxSeed + 1);

            Material previousSky = runSky;
            runSky = new Material(authoredSky) { name = authoredSky.name + " (Run)" };
            if (newStars)
                runSky.SetFloat(SpaceSkyProperties.StarSeed, starSeed);

            if (newOrientation)
            {
                runSky.SetVector(SpaceSkyProperties.Rotation, new Vector4(random.Range(-maxTilt, maxTilt), random.Range(0f, 360f), random.Range(-maxTilt, maxTilt), 0f));
                runSky.SetVector(SpaceSkyProperties.FarNebulaRotation, RandomAngles(ref random));
                runSky.SetVector(SpaceSkyProperties.DustRotation, RandomAngles(ref random));
            }

            DropNebula();
            if (!newNebula)
            {
                ReplaceMap(null);
            }
            else
            {
                NebulaShape shape = NebulaMap.ReadShape(authoredSky).WithSeed(nebulaSeed);
                if (timing == NebulaTiming.BeforeFirstFrame)
                {
                    ReplaceMap(NebulaMap.Create(shape, MapName, false));
                }
                else
                {
                    // The last run nebula stays up until the new one is in; at load the authored one does, hidden.
                    if (runMap != null)
                        runSky.SetTexture(SpaceSkyProperties.NebulaMap, runMap);

                    StartNebula(shape, timing == NebulaTiming.AfterStartUp);
                }
            }

            RenderSettings.skybox = runSky;
            if (previousSky != null)
                Object.Destroy(previousSky);
        }

        // The load task starts now, so the loading screen waits for a bake that has not launched yet.
        private void StartNebula(NebulaShape shape, bool afterStartUp)
        {
            paintTask = LoadGate.Begin(PaintTaskLabel);
            if (!afterStartUp)
            {
                LaunchNebula(shape, SceneFlow.IsEntering ? HiddenThreads : PlayThreads);
                return;
            }

            waitingShape = shape;
            waitingFrame = Time.frameCount;
        }

        private void LaunchNebula(NebulaShape shape, int threads)
        {
            nebulaBake = Task.Run(() => NebulaMap.BakeTexels(shape, threads));
            LoadReport.StartSpan(LoadLabel);
        }

        private void FinishNebula()
        {
            Task<ushort[]> bake = nebulaBake;
            nebulaBake = null;
            LoadReport.EndSpan(LoadLabel);

            using (LoadReport.Measure(LoadLabel))
            {
                if (bake.Status == TaskStatus.RanToCompletion)
                    ReplaceMap(NebulaMap.Create(bake.Result, MapName, false));
                else
                    Debug.LogError($"The sky's nebula did not bake: {bake.Exception?.GetBaseException().Message}", this);
            }

            paintTask.Complete();
            paintTask = null;
        }

        // Stops waiting for a bake that is waiting or in flight; one in flight runs to its end on its worker and is thrown away.
        private void DropNebula()
        {
            waitingFrame = -1;
            if (nebulaBake != null)
            {
                nebulaBake = null;
                LoadReport.EndSpan(LoadLabel);
            }

            paintTask?.Complete();
            paintTask = null;
        }

        // Puts a new run map on the run sky, or none, and frees the one it replaces.
        private void ReplaceMap(Texture2D map)
        {
            if (runMap != null && runMap != map)
                Object.Destroy(runMap);

            runMap = map;
            if (map != null && runSky != null)
                runSky.SetTexture(SpaceSkyProperties.NebulaMap, map);
        }

        private int ChooseSeed()
        {
            if (fixedSeed != 0)
                return fixedSeed;

            if (space != null)
                return unchecked((int)SectorHash.Combine(space.Seed, SkyStream));

            return unchecked((int)SectorHash.Mix((uint)DateTime.UtcNow.Ticks ^ (uint)Environment.TickCount));
        }

        private static Vector4 RandomAngles(ref SectorRandom random)
        {
            return new Vector4(random.Range(0f, 360f), random.Range(0f, 360f), random.Range(0f, 360f), 0f);
        }
    }
}
