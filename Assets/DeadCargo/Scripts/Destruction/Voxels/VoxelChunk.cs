using UnityEngine;
using UnityEngine.Rendering;

namespace DeadCargo.Destruction.Voxels
{
    /// <summary>
    /// The scene side of one chunk of a voxel body: a child object at the body's origin with its renderer, collider and
    /// weapon receiver. Rendering and collision may briefly use different meshes: a new shape renders at once and the
    /// collider switches to it once it has been cooked off the main thread.
    /// </summary>
    internal sealed class VoxelChunk
    {
        public readonly GameObject GameObject;
        public readonly MeshFilter Filter;
        public readonly MeshRenderer Renderer;
        public readonly MeshCollider Collider;
        public readonly DestructionDamageReceiver Receiver;

        public Mesh RenderMesh;
        public Mesh ColliderMesh;
        public int Triangles;

        public VoxelChunk(VoxelBody body, Material material)
        {
            // Every chunk sits at the body's origin, so all of them share one transform and one shade variation.
            GameObject = new GameObject("Chunk") { layer = body.gameObject.layer };
            GameObject.transform.SetParent(body.transform, false);

            Filter = GameObject.AddComponent<MeshFilter>();
            Renderer = GameObject.AddComponent<MeshRenderer>();
            Renderer.sharedMaterial = material;
            Renderer.shadowCastingMode = ShadowCastingMode.On;
            Renderer.receiveShadows = true;
            Renderer.lightProbeUsage = LightProbeUsage.Off;
            Renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            Renderer.enabled = false;

            // The collider exists before the receiver wakes, so the kit's receiver finds it.
            Collider = GameObject.AddComponent<MeshCollider>();
            Collider.cookingOptions = ColliderBakeJob.CookingOptions;
            Collider.enabled = false;

            Receiver = GameObject.AddComponent<DestructionDamageReceiver>();
            Receiver.Bind(body);
        }

        public bool IsAlive => GameObject != null;

        public void SetMaterial(Material material)
        {
            Renderer.sharedMaterial = material;
        }
    }
}
