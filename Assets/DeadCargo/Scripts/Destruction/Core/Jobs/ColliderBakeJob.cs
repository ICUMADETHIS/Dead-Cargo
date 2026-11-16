using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace DeadCargo.Destruction.Voxels
{
    /// <summary>
    /// Cooks mesh colliders on worker threads. Assigning a baked mesh to a collider with the same cooking options then
    /// costs almost nothing on the main thread. Not Burst compiled: baking is an engine call.
    /// </summary>
    public struct ColliderBakeJob : IJobParallelFor
    {
        /// <summary>
        /// Options every voxel collider uses; the collider must match them for the bake to be reused. Voxel meshes share
        /// their vertices already and are rebuilt often, so welding and slow, simulation-tuned cooking are left out: they
        /// more than double the cooking time for no gain.
        /// </summary>
        public const MeshColliderCookingOptions CookingOptions = MeshColliderCookingOptions.EnableMeshCleaning |
            MeshColliderCookingOptions.UseFastMidphase;

        [ReadOnly] public NativeArray<EntityId> MeshIds;

        public void Execute(int i)
        {
            Physics.BakeMesh(MeshIds[i], false, CookingOptions);
        }
    }
}
