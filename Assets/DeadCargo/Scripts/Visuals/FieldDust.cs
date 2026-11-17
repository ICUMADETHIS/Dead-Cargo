using DeadCargo.Sectors;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadCargo.Visuals
{
    /// <summary>
    /// Field dust: soft banks of dust through the asteroid fields, so rocks sit in something and depth inside a field
    /// reads at a glance. One static mesh is drawn for each camera, and the DeadCargo/Field Dust shader places its
    /// banks around that camera and thins them by the asteroid field's density map, so dust lies where the rock is
    /// and costs nothing per frame beyond one draw. Without an asteroid field running, nothing shows.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FieldDust : MonoBehaviour
    {
        // Four vertices per bank must fit a 16-bit index buffer.
        private const int MaxBanks = 8000;

        // The shader moves every bank to the camera, so the mesh's bounds must contain anywhere a camera can be.
        private const float BoundsSize = 1e6f;

        private static readonly int SunDirectionId = Shader.PropertyToID("_SunDirection");

        [Tooltip("Material using the DeadCargo/Field Dust shader. The look is set there.")]
        [SerializeField] private Material material;

        [Tooltip("Banks in the space around the camera, at most. Only those inside a field are drawn. More banks give a " +
                 "fuller haze and cost more to draw; the material's Amount shows a share of them.")]
        [SerializeField, Range(100, MaxBanks)] private int bankCount = 1200;

        [Tooltip("Seed for where the banks sit.")]
        [SerializeField] private int seed = 7;

        private Mesh mesh;
        private MaterialPropertyBlock properties;
        private int builtCount;
        private int builtSeed;

        /// <summary>The material the dust is drawn with.</summary>
        public Material Material => material;

        private void OnEnable()
        {
            properties ??= new MaterialPropertyBlock();
            BuildMesh();
            Camera.onPreCull += Draw;
        }

        private void OnDisable()
        {
            Camera.onPreCull -= Draw;
            DestroyMesh();
        }

        private void Update()
        {
            if (builtCount != bankCount || builtSeed != seed)
                BuildMesh();
        }

        private void Draw(Camera camera)
        {
            if (material == null || mesh == null || (camera.cullingMask & (1 << gameObject.layer)) == 0 ||
                camera.cameraType == CameraType.Preview || camera.cameraType == CameraType.Reflection)
                return;

            Light sun = RenderSettings.sun;
            properties.SetVector(SunDirectionId, sun != null ? -sun.transform.forward : Vector3.up);
            Graphics.DrawMesh(mesh, Matrix4x4.identity, material, gameObject.layer, camera, 0, properties,
                ShadowCastingMode.Off, false, null, LightProbeUsage.Off, null);
        }

        // Each bank is a quad whose four corners share its spot in a unit cube; the shader does the rest.
        private void BuildMesh()
        {
            DestroyMesh();
            builtCount = bankCount;
            builtSeed = seed;

            int count = Mathf.Clamp(bankCount, 1, MaxBanks);
            var spots = new Vector3[count * 4];
            var corners = new Vector2[count * 4];
            var randoms = new Vector3[count * 4];
            var indices = new int[count * 6];
            var random = new SectorRandom(SectorHash.Mix(unchecked((uint)seed)));

            for (int i = 0; i < count; ++i)
            {
                var spot = new Vector3(random.NextFloat(), random.NextFloat(), random.NextFloat());

                // Size, strength, and the rank the material's Amount is compared against.
                var shade = new Vector3(random.NextFloat(), random.NextFloat(), random.NextFloat());
                int vertex = i * 4;
                for (int k = 0; k < 4; ++k)
                {
                    spots[vertex + k] = spot;
                    randoms[vertex + k] = shade;
                }

                corners[vertex] = new Vector2(-1f, -1f);
                corners[vertex + 1] = new Vector2(-1f, 1f);
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

            mesh = new Mesh { name = "Field Dust", hideFlags = HideFlags.HideAndDontSave };
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

            Destroy(mesh);
            mesh = null;
        }
    }
}
