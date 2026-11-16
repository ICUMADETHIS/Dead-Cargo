using UnityEngine;

namespace DeadCargo.Destruction.Voxels
{
    /// <summary>
    /// Places a voxel asteroid in a scene: builds the rock on its <see cref="VoxelBody"/> when play starts.
    /// Asteroids spawned by code go through <see cref="DestructionSystem.SpawnAsteroid"/> instead.
    /// </summary>
    [RequireComponent(typeof(VoxelBody))]
    public sealed class VoxelAsteroid : MonoBehaviour
    {
        [SerializeField] private AsteroidDefinition definition;

        [Tooltip("Mean diameter, in metres.")]
        [SerializeField, Min(2f)] private float diameter = 40f;

        [Tooltip("The same seed always builds the same rock. 0 builds a new rock every run.")]
        [SerializeField] private uint seed;

        public AsteroidDefinition Definition => definition;
        public float Diameter => diameter;

        private void Start()
        {
            if (definition == null)
            {
                Debug.LogWarning($"'{name}' has no asteroid type, so no rock was built.", this);
                return;
            }

            // Runs only build the rock; the scene keeps no voxel data, so scale is fixed at 1 to keep cuts in metres.
            transform.localScale = Vector3.one;
            uint rockSeed = seed != 0 ? seed : (uint)Random.Range(1, int.MaxValue);
            GetComponent<VoxelBody>().Generate(definition, diameter, rockSeed);
        }
    }
}
