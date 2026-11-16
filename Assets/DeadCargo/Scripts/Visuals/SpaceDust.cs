using DeadCargo.Sectors;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadCargo.Visuals
{
    /// <summary>
    /// Space dust: faint motes around the camera that streak past as it moves, the main cue for speed and scale in
    /// open space. One static mesh is drawn for each camera, and the DeadCargo/Space Dust shader wraps it around that
    /// camera, so the field is endless and costs nothing per frame beyond one draw. Shows in edit mode, without streaks.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class SpaceDust : MonoBehaviour
    {
        // Four vertices per mote must fit a 16-bit index buffer.
        private const int MaxMotes = 16000;

        // The shader moves every mote to the camera, so the mesh's bounds must contain anywhere a camera can be.
        private const float BoundsSize = 1e6f;

        private static readonly int VelocityId = Shader.PropertyToID("_Velocity");

        [Tooltip("Material using the DeadCargo/Space Dust shader. The look is set there.")]
        [SerializeField] private Material material;

        [Tooltip("Motes in the field around the camera, at most. The material's Amount sets how many of them show.")]
        [SerializeField, Range(100, MaxMotes)] private int moteCount = 4000;

        [Tooltip("Seed for where the motes sit.")]
        [SerializeField] private int seed = 1;

        [Tooltip("How quickly streaks follow changes in the camera's velocity, per second.")]
        [SerializeField, Min(0.1f)] private float velocityResponse = 10f;

        [Tooltip("A camera move longer than this in one frame, in metres, is a jump, not flight, and draws no streak.")]
        [SerializeField, Min(1f)] private float jumpDistance = 500f;

        private Mesh mesh;
        private MaterialPropertyBlock properties;
        private Camera trackedCamera;
        private Vector3 lastPosition;
        private bool hasLastPosition;
        private Vector3 velocity;
        private bool rebuildPending;

        /// <summary>The tracked camera's smoothed velocity, in metres per second. It streaks the dust.</summary>
        public Vector3 Velocity => velocity;

        private void OnEnable()
        {
            if (properties == null)
                properties = new MaterialPropertyBlock();

            BuildMesh();
            hasLastPosition = false;
            velocity = Vector3.zero;
            Camera.onPreCull += Draw;
        }

        private void OnDisable()
        {
            Camera.onPreCull -= Draw;
            DestroyMesh();
        }

        // Meshes cannot be destroyed inside OnValidate, so the rebuild waits for the next update.
        private void OnValidate()
        {
            rebuildPending = true;
        }

        private void Update()
        {
            if (rebuildPending && isActiveAndEnabled)
                BuildMesh();
        }

        private void LateUpdate()
        {
            if (Application.isPlaying)
                TrackCamera();
        }

        // Velocity is measured against unscaled time, so a paused world draws no streaks and slow motion draws short ones.
        private void TrackCamera()
        {
            Camera current = Camera.main;
            if (current != trackedCamera)
            {
                trackedCamera = current;
                hasLastPosition = false;
            }

            if (trackedCamera == null)
            {
                velocity = Vector3.zero;
                return;
            }

            Vector3 position = trackedCamera.transform.position;
            float deltaTime = Time.unscaledDeltaTime;
            if (!hasLastPosition || (position - lastPosition).sqrMagnitude > jumpDistance * jumpDistance)
            {
                velocity = Vector3.zero;
            }
            else if (deltaTime > 0f)
            {
                Vector3 measured = (position - lastPosition) / deltaTime;
                velocity = Vector3.Lerp(velocity, measured, 1f - Mathf.Exp(-velocityResponse * deltaTime));
            }

            lastPosition = position;
            hasLastPosition = true;
        }

        private void Draw(Camera camera)
        {
            if (material == null || mesh == null || (camera.cullingMask & (1 << gameObject.layer)) == 0 ||
                camera.cameraType == CameraType.Preview || camera.cameraType == CameraType.Reflection)
                return;

            // Only the tracked camera streaks the dust. Others, such as the Scene view, see still motes.
            properties.SetVector(VelocityId, camera == trackedCamera ? velocity : Vector3.zero);
            Graphics.DrawMesh(mesh, Matrix4x4.identity, material, gameObject.layer, camera, 0, properties,
                ShadowCastingMode.Off, false, null, LightProbeUsage.Off, null);
        }

        // Each mote is a quad whose four corners share its spot in a unit cube; the shader does the rest.
        private void BuildMesh()
        {
            rebuildPending = false;
            DestroyMesh();

            int count = Mathf.Clamp(moteCount, 1, MaxMotes);
            var spots = new Vector3[count * 4];
            var corners = new Vector2[count * 4];
            var randoms = new Vector3[count * 4];
            var indices = new int[count * 6];
            var random = new SectorRandom(SectorHash.Mix(unchecked((uint)seed)));

            for (int i = 0; i < count; ++i)
            {
                var spot = new Vector3(random.NextFloat(), random.NextFloat(), random.NextFloat());
                // Size, brightness, and the rank the material's Amount is compared against.
                var shade = new Vector3(random.NextFloat(), random.NextFloat(), random.NextFloat());
                int vertex = i * 4;
                for (int k = 0; k < 4; ++k)
                {
                    spots[vertex + k] = spot;
                    randoms[vertex + k] = shade;
                }

                corners[vertex] = new Vector2(0f, -1f);
                corners[vertex + 1] = new Vector2(0f, 1f);
                corners[vertex + 2] = new Vector2(1f, 1f);
                corners[vertex + 3] = new Vector2(1f, -1f);

                int index = i * 6;
                indices[index] = vertex;
                indices[index + 1] = vertex + 1;
                indices[index + 2] = vertex + 2;
                indices[index + 3] = vertex;
                indices[index + 4] = vertex + 2;
                indices[index + 5] = vertex + 3;
            }

            mesh = new Mesh { name = "Space Dust", hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(spots);
            mesh.SetUVs(0, corners);
            mesh.SetUVs(1, randoms);
            mesh.SetTriangles(indices, 0, false);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * BoundsSize);
            mesh.UploadMeshData(true);
        }

        private void DestroyMesh()
        {
            if (mesh == null)
                return;

            if (Application.isPlaying)
                Destroy(mesh);
            else
                DestroyImmediate(mesh);

            mesh = null;
        }
    }
}
