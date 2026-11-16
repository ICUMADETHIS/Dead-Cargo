using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.EditorTools
{
    /// <summary>
    /// Gives near objects the comic look. Converts the player ship's Flat Kit materials into Dead Cargo comic copies,
    /// keeping their colours and textures, and assigns them to the ship in the open scene; kit files are never touched.
    /// Also owns the comic rock material the asteroid field draws with, and a test rock to judge it by.
    /// Every step is safe to run more than once, and existing comic materials are never overwritten.
    /// </summary>
    internal static class ComicLookSetup
    {
        public const string ShipMenuPath = "Dead Cargo/Setup/Phase 6.9 - Comic Ship (Open Scene)";
        public const string RockMaterialPath = MaterialFolder + "/ComicRock.mat";
        public const string TestRockName = "Test Rock";

        private const string ShaderName = "DeadCargo/Comic Surface";
        private const string FlatKitShaderName = "FlatKit/Stylized Surface";
        private const string MaterialFolder = "Assets/DeadCargo/Materials/Comic";
        private const string ComicSuffix = " Comic";
        private const string DialogTitle = "Phase 6.9 - Comic Look";

        private const string KitAsteroidModel = "Assets/SpaceCombatKit/SpaceCombat/Demos/Content/Asteroid/Asteroid.fbx";
        private const string KitAsteroidAlbedo = "Assets/SpaceCombatKit/SpaceCombat/Demos/Content/Asteroid/Asteroid_Albedo.png";
        private const string KitAsteroidNormal = "Assets/SpaceCombatKit/SpaceCombat/Demos/Content/Asteroid/Asteroid_Normal.png";
        private const string NormalMapProperty = "_BumpMap";

        // Flat Kit properties the comic shader shares, copied across when a material is converted.
        private const string LitColorProperty = "_Color";
        private static readonly string[] SharedFloats = { "_TextureImpact", "_SelfShadingSize", "_LightContribution" };
        private const string AlbedoProperty = "_MainTex";

        // A converted material's shadow is its lit colour times this, so every part shades into the same cool charcoal
        // as the rocks, rather than into a plain darker version of itself.
        private static readonly Color ShadowTint = new Color(0.4f, 0.39f, 0.47f);
        private const float CelSoftness = 0.03f;

        [MenuItem(ShipMenuPath, priority = 210)]
        private static void RunShip()
        {
            ApplyToShip();
        }

        [MenuItem(ShipMenuPath, true)]
        private static bool CanRun()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        /// <summary>True when the ship in the scene has no Flat Kit materials left.</summary>
        public static bool IsShipComic(Scene scene)
        {
            GameObject ship = FindShipRoot(scene);
            if (ship == null)
                return false;

            foreach (Renderer renderer in ship.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (IsFlatKit(material))
                        return false;
                }
            }

            return true;
        }

        /// <summary>The ship's first comic material, for editing its look. Null when the ship has none.</summary>
        public static Material FindShipComicMaterial(Scene scene)
        {
            GameObject ship = FindShipRoot(scene);
            if (ship == null)
                return null;

            foreach (Renderer renderer in ship.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null && material.shader != null && material.shader.name == ShaderName)
                        return material;
                }
            }

            return null;
        }

        /// <summary>Converts the ship's Flat Kit materials to comic copies. Returns false when it cannot, after a dialog explains why.</summary>
        internal static bool ApplyToShip()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject ship = FindShipRoot(scene);
            if (ship == null)
            {
                EditorUtility.DisplayDialog(DialogTitle, "No ship found in the open scene. Select the ship and try again.", "OK");
                return false;
            }

            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                EditorUtility.DisplayDialog(DialogTitle, $"The '{ShaderName}' shader was not found. Check the Console for shader errors.", "OK");
                return false;
            }

            Undo.SetCurrentGroupName("Comic Ship");
            int undoGroup = Undo.GetCurrentGroup();
            var converted = new Dictionary<Material, Material>();
            foreach (Renderer renderer in ship.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; ++i)
                {
                    if (!IsFlatKit(materials[i]))
                        continue;

                    if (!converted.TryGetValue(materials[i], out Material comic))
                    {
                        comic = EnsureComicCopy(materials[i], shader);
                        converted.Add(materials[i], comic);
                    }

                    materials[i] = comic;
                    changed = true;
                }

                if (changed)
                {
                    Undo.RecordObject(renderer, "Comic Ship");
                    renderer.sharedMaterials = materials;
                }
            }

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"'{ship.name}' uses the comic look: {converted.Count} materials converted, in '{MaterialFolder}'.", ship);
            return true;
        }

        /// <summary>The comic rock material, created the first time. Null when the shader is not available.</summary>
        public static Material EnsureRockMaterial()
        {
            var rock = AssetDatabase.LoadAssetAtPath<Material>(RockMaterialPath);
            if (rock != null)
                return rock;

            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
                return null;

            // Dusty warm stone against the cold void, with violet shadows. The kit's rock texture and normal map give
            // it grit and relief, lit in three soft tones.
            rock = new Material(shader) { name = "ComicRock", enableInstancing = true };
            rock.SetTexture(AlbedoProperty, AssetDatabase.LoadAssetAtPath<Texture>(KitAsteroidAlbedo));
            rock.SetTexture(NormalMapProperty, AssetDatabase.LoadAssetAtPath<Texture>(KitAsteroidNormal));
            rock.SetFloat("_TextureImpact", 0.75f);
            rock.SetFloat("_BumpScale", 0.8f);
            rock.SetColor("_Color", new Color(0.66f, 0.54f, 0.42f));
            rock.SetColor("_ColorDim", new Color(0.16f, 0.15f, 0.2f));
            rock.SetFloat("_CelSoftness", CelSoftness);
            rock.SetFloat("_InstanceVariation", 0.15f);
            rock.SetColor("_HighlightColor", new Color(0.95f, 0.85f, 0.7f));
            rock.SetFloat("_HighlightStrength", 0.35f);
            rock.SetFloat("_SpecularStrength", 0f);
            rock.SetColor("_RimColor", new Color(0.45f, 0.55f, 0.75f));
            rock.SetFloat("_RimStrength", 0.3f);
            rock.SetFloat("_InkWidth", 2.2f);
            rock.SetFloat("_InkGapFill", 1f);
            rock.SetVector("_InkFade", new Vector4(300f, 2500f, 0f, 0f));
            rock.SetFloat("_HatchSpacing", 2f);

            SceneSetupUtility.EnsureFolder(MaterialFolder);
            AssetDatabase.CreateAsset(rock, RockMaterialPath);
            return rock;
        }

        /// <summary>The scene's test rock, or null.</summary>
        public static GameObject FindTestRock(Scene scene)
        {
            foreach (MeshRenderer renderer in SceneSetupUtility.FindInScene<MeshRenderer>(scene))
            {
                if (renderer.name == TestRockName)
                    return renderer.gameObject;
            }

            return null;
        }

        /// <summary>
        /// Places one kit asteroid with the comic rock material in front of the ship, to judge the look before the
        /// asteroid field is built. Returns false when it cannot, after a dialog explains why.
        /// </summary>
        internal static bool PlaceTestRock()
        {
            Scene scene = SceneManager.GetActiveScene();
            Material rock = EnsureRockMaterial();
            Mesh mesh = LoadKitAsteroidMesh();
            if (rock == null || mesh == null)
            {
                EditorUtility.DisplayDialog(DialogTitle, rock == null
                    ? $"The '{ShaderName}' shader was not found. Check the Console for shader errors."
                    : $"The kit asteroid was not found at '{KitAsteroidModel}'.", "OK");
                return false;
            }

            // In front of the ship, at three times the rock's size, so the whole rock is in view.
            GameObject ship = FindShipRoot(scene);
            Transform anchor = ship != null ? ship.transform : null;
            float radius = mesh.bounds.extents.magnitude;
            Vector3 position = anchor != null ? anchor.position + anchor.forward * (radius * 3f + 50f) : Vector3.forward * (radius * 3f);

            var testRock = new GameObject(TestRockName, typeof(MeshFilter), typeof(MeshRenderer));
            Undo.RegisterCreatedObjectUndo(testRock, "Place Test Rock");
            if (testRock.scene != scene)
                SceneManager.MoveGameObjectToScene(testRock, scene);

            testRock.transform.SetPositionAndRotation(position, Quaternion.Euler(20f, 35f, 10f));
            testRock.GetComponent<MeshFilter>().sharedMesh = mesh;
            testRock.GetComponent<MeshRenderer>().sharedMaterial = rock;
            SceneHierarchy.Place(testRock, SceneHierarchy.Environment);

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = testRock;
            Debug.Log($"Test rock placed {Vector3.Distance(position, anchor != null ? anchor.position : Vector3.zero):N0} m ahead, " +
                      $"about {radius * 2f:N0} m across. Its look is set on '{RockMaterialPath}'.", testRock);
            return true;
        }

        /// <summary>The kit asteroid's most detailed mesh.</summary>
        public static Mesh LoadKitAsteroidMesh()
        {
            Mesh best = null;
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(KitAsteroidModel))
            {
                if (asset is Mesh mesh && (best == null || mesh.vertexCount > best.vertexCount))
                    best = mesh;
            }

            return best;
        }

        private static GameObject FindShipRoot(Scene scene)
        {
            VehicleEngines3D ship = SceneSetupUtility.FindShip(scene);
            return ship != null ? ship.transform.root.gameObject : null;
        }

        private static bool IsFlatKit(Material material)
        {
            return material != null && material.shader != null && material.shader.name == FlatKitShaderName;
        }

        // Reuses an existing copy, so a comic material tuned by hand is never overwritten.
        private static Material EnsureComicCopy(Material flatKit, Shader shader)
        {
            string path = $"{MaterialFolder}/{flatKit.name}{ComicSuffix}.mat";
            var comic = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (comic != null)
                return comic;

            comic = new Material(shader) { name = flatKit.name + ComicSuffix, enableInstancing = true };
            if (flatKit.HasProperty(LitColorProperty))
            {
                Color lit = flatKit.GetColor(LitColorProperty);
                comic.SetColor(LitColorProperty, lit);
                comic.SetColor("_ColorDim", new Color(lit.r * ShadowTint.r, lit.g * ShadowTint.g, lit.b * ShadowTint.b, lit.a));
            }

            comic.SetFloat("_CelSoftness", CelSoftness);

            foreach (string property in SharedFloats)
            {
                if (flatKit.HasProperty(property))
                    comic.SetFloat(property, flatKit.GetFloat(property));
            }

            if (flatKit.HasProperty(AlbedoProperty))
            {
                comic.SetTexture(AlbedoProperty, flatKit.GetTexture(AlbedoProperty));
                comic.SetTextureScale(AlbedoProperty, flatKit.GetTextureScale(AlbedoProperty));
                comic.SetTextureOffset(AlbedoProperty, flatKit.GetTextureOffset(AlbedoProperty));
            }

            // Ship-sized ink and hatching: fine strokes, inked out to a few hundred metres.
            comic.SetFloat("_HatchSpacing", 0.18f);
            comic.SetVector("_InkFade", new Vector4(80f, 700f, 0f, 0f));

            SceneSetupUtility.EnsureFolder(MaterialFolder);
            AssetDatabase.CreateAsset(comic, path);
            return comic;
        }
    }
}
