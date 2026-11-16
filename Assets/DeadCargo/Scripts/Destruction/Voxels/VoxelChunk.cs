using UnityEngine;
using UnityEngine.Rendering;

namespace DeadCargo.Destruction.Voxels
{
    /// <summary>
    /// The scene side of one chunk of a voxel body: a child object at the body's origin with its renderer, collider and
    /// weapon receiver. Rendering and collision may briefly use different meshes: a new shape renders at once and the
    /// collider switches to it once it has been cooked off the main thread.
    /// <para>
    /// When the chunk changes mesh detail, its old mesh stays on a second renderer, the ghost, and the two cross-fade
    /// through a fine dither: the new mesh takes over pixel by pixel, so the switch never pops.
    /// </para>
    /// </summary>
    internal sealed class VoxelChunk
    {
        private static readonly int TriplanarOffsetId = Shader.PropertyToID("_TriplanarOffset");
        private static readonly int DitherFadeId = Shader.PropertyToID("_DitherFade");
        private static MaterialPropertyBlock block;

        public readonly GameObject GameObject;
        public readonly MeshFilter Filter;
        public readonly MeshRenderer Renderer;
        public readonly MeshCollider Collider;
        public readonly DestructionDamageReceiver Receiver;

        public Mesh RenderMesh;
        public Mesh ColliderMesh;
        public int Triangles;

        /// <summary>Samples per cell edge the render mesh was built at: 1 for full detail, 2 for half, 4 for quarter.</summary>
        public int MeshStride = 1;

        /// <summary>The mesh fading out during a detail cross-fade, shown by the ghost. Null otherwise.</summary>
        public Mesh FadeMesh;

        /// <summary>Share of pixels the new mesh shows during a cross-fade, 0 to 1.</summary>
        public float FadeProgress = 1f;

        public float FadeStart;

        private Material baseMaterial;
        private MeshFilter ghostFilter;
        private MeshRenderer ghostRenderer;

        public VoxelChunk(VoxelBody body, Material material)
        {
            // Every chunk sits at the body's origin, so all of them share one transform and one shade variation.
            GameObject = new GameObject("Chunk") { layer = body.gameObject.layer };
            GameObject.transform.SetParent(body.transform, false);

            Filter = GameObject.AddComponent<MeshFilter>();
            Renderer = GameObject.AddComponent<MeshRenderer>();
            Configure(Renderer);
            Renderer.enabled = false;
            SetMaterial(material);

            // The collider exists before the receiver wakes, so the kit's receiver finds it.
            Collider = GameObject.AddComponent<MeshCollider>();
            Collider.cookingOptions = ColliderBakeJob.CookingOptions;
            Collider.enabled = false;

            Receiver = GameObject.AddComponent<DestructionDamageReceiver>();
            Receiver.Bind(body);
        }

        public bool IsAlive => GameObject != null;

        public bool IsFading => FadeMesh != null;

        public void SetMaterial(Material material)
        {
            baseMaterial = material;
            Renderer.sharedMaterial = material;
        }

        /// <summary>
        /// Starts a cross-fade: the ghost keeps showing the outgoing mesh while the chunk's own renderer, which already
        /// shows the new one, fades in over it.
        /// </summary>
        /// <param name="fadeMaterial">The chunk's material with the dither fade on.</param>
        public void BeginFade(Mesh outgoing, Material fadeMaterial, float time)
        {
            if (ghostRenderer == null)
            {
                var ghost = new GameObject("Chunk (fading out)") { layer = GameObject.layer };
                ghost.transform.SetParent(GameObject.transform, false);
                ghostFilter = ghost.AddComponent<MeshFilter>();
                ghostRenderer = ghost.AddComponent<MeshRenderer>();
                Configure(ghostRenderer);
            }

            FadeMesh = outgoing;
            FadeProgress = 0f;
            FadeStart = time;
            ghostFilter.sharedMesh = outgoing;
            ghostRenderer.sharedMaterial = fadeMaterial;
            ghostRenderer.enabled = Renderer.enabled;
            Renderer.sharedMaterial = fadeMaterial;
        }

        /// <summary>Ends a cross-fade, if one runs, and returns the outgoing mesh for the caller to release.</summary>
        public Mesh EndFade()
        {
            Mesh outgoing = FadeMesh;
            FadeMesh = null;
            FadeProgress = 1f;
            if (ghostRenderer != null)
            {
                ghostFilter.sharedMesh = null;
                ghostRenderer.enabled = false;
            }

            if (IsAlive)
                Renderer.sharedMaterial = baseMaterial;

            return outgoing;
        }

        public void SetGhostVisible(bool visible)
        {
            if (ghostRenderer != null)
                ghostRenderer.enabled = visible;
        }

        /// <summary>Sets the per-renderer look: the body's texture offset and, during a cross-fade, how far it has got.</summary>
        public void ApplyLook(Vector4 textureOffset)
        {
            bool hasOffset = textureOffset != Vector4.zero;
            if (!IsFading && !hasOffset)
            {
                Renderer.SetPropertyBlock(null);
                return;
            }

            block ??= new MaterialPropertyBlock();
            block.Clear();
            if (hasOffset)
                block.SetVector(TriplanarOffsetId, textureOffset);

            if (!IsFading)
            {
                Renderer.SetPropertyBlock(block);
                return;
            }

            // The incoming mesh keeps the pixels below the progress, the outgoing one the rest.
            block.SetVector(DitherFadeId, new Vector4(FadeProgress, 1f, 0f, 0f));
            Renderer.SetPropertyBlock(block);
            block.SetVector(DitherFadeId, new Vector4(FadeProgress, -1f, 0f, 0f));
            ghostRenderer.SetPropertyBlock(block);
        }

        private static void Configure(MeshRenderer renderer)
        {
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
    }
}
