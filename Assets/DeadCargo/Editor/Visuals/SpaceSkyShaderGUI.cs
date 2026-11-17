using DeadCargo.Visuals;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadCargo.EditorTools.Visuals
{
    /// <summary>
    /// Inspector for Space Sky materials. Settings are grouped by part of the sky, packed vectors are shown as
    /// labelled sliders, and the nebula is re-baked on its own shortly after a shape setting stops changing.
    /// </summary>
    internal sealed class SpaceSkyShaderGUI : ShaderGUI
    {
        // Seconds after the last shape change before baking, so dragging a slider bakes once, not every frame.
        private const double BakeDelay = 0.35;

        private const float SeedButtonWidth = 44f;
        private const string SectionKeyPrefix = "DeadCargo.SpaceSky.";

        private static readonly System.Random SeedPicker = new System.Random();

        private static Material pendingMaterial;
        private static int pendingHash;
        private static double pendingTime;
        private static string lastBakeReport;

        private MaterialEditor editor;
        private MaterialProperty[] properties;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] materialProperties)
        {
            editor = materialEditor;
            properties = materialProperties;
            Material material = editor.targets.Length == 1 ? editor.target as Material : null;

            if (GUILayout.Button(new GUIContent("Reset Look to Defaults",
                    "Every setting back to the shader's defaults, except the star and nebula seeds. Ctrl+Z undoes it.")))
            {
                foreach (Object target in editor.targets)
                {
                    if (target is Material sky)
                        ResetLook(sky);
                }
            }

            EditorGUILayout.Space(4f);

            if (Section("Sky"))
            {
                DrawVector3(SpaceSkyProperties.Rotation, "Rotation",
                    "Turns the whole sky, in degrees. Unrotated, the galactic plane lies along the horizon.");
                DrawVector3(SpaceSkyProperties.LightDirection, "Light Direction",
                    "World direction the nebula is lit from when it does not follow the sun (Lights the Clouds below 1).");
                Draw(SpaceSkyProperties.Exposure, "Exposure", "Multiplies the whole sky. The sun keeps its own brightness.");
            }

            if (Section("Sun"))
            {
                EditorGUILayout.HelpBox("The sun is drawn where the scene's sun light comes from: the Sun Source in the Lighting " +
                                        "window, or else the brightest directional light. Turn the light to move it.", MessageType.None);
                Draw(SpaceSkyProperties.SunSize, "Size", "Width of the star, in degrees. The sun seen from Earth is half a degree; larger reads as closer.");
                SubHeading("Surface");
                Draw(SpaceSkyProperties.SunCoreColor, "Hot", "Colour of the brightest gas and the veins.");
                Draw(SpaceSkyProperties.SunSurfaceColor, "Surface", "Colour of most of the surface.");
                Draw(SpaceSkyProperties.SunSpotColor, "Cool", "Colour of the coolest gas and of the limb.");
                Draw(SpaceSkyProperties.SunBrightness, "Brightness", "Multiplies the surface colours.");
                Draw(SpaceSkyProperties.SunSteps, "Tone Steps", "Flat tones the surface is painted in.");
                Draw(SpaceSkyProperties.SunSoftness, "Softness", "0 gives crisp comic tones; 1 blends them smoothly, as the nebula's Softness does.");
                Draw(SpaceSkyProperties.SunGranules, "Granules", "How many cells of gas cross the star. Higher gives finer cells.");
                Draw(SpaceSkyProperties.SunVeins, "Veins", "Bright threads of hot gas between the cells.");
                Draw(SpaceSkyProperties.SunLimbDarkening, "Limb Darkening", "How much the surface darkens towards its edge, which makes it read as a sphere.");
                SubHeading("Corona");
                Draw(SpaceSkyProperties.SunCoronaColor, "Colour", null);
                Draw(SpaceSkyProperties.SunCorona, "Strength", "Brightness of the light round the star. 0 leaves the bare sphere.");
                Draw(SpaceSkyProperties.SunCoronaSize, "Size", "How far the corona reaches past the limb, in degrees. Its streamers reach unevenly round it.");
                Draw(SpaceSkyProperties.SunFlares, "Flares", "Tongues of flame rising off the limb.");
                SubHeading("Light");
                Draw(SpaceSkyProperties.SunLightTint, "Light Colour", "How far the star's colours take on the sun light's colour.");
                Draw(SpaceSkyProperties.SunLightsClouds, "Lights the Clouds",
                    "1 lights the nebula from the sun, so sky and ships agree. 0 uses the Light Direction above.");
            }

            if (Section("Motion"))
            {
                Draw(SpaceSkyProperties.NebulaFlow, "Flow", "How far cloud edges creep as the gas drifts. 0 holds them still.");
                Draw(SpaceSkyProperties.NebulaFlowSpeed, "Flow Speed", "Flow cycles per second.");
                Draw(SpaceSkyProperties.FarDrift, "Far Drift", "Degrees per second the far layer turns against the near clouds, which makes the sky read as deep.");
                Draw(SpaceSkyProperties.StarTwinkle, "Twinkle", "How much stars dim and brighten. Glints pulse with it.");
                Draw(SpaceSkyProperties.TwinkleSpeed, "Twinkle Speed", "Average twinkle speed, in radians per second.");
                Draw(SpaceSkyProperties.SunBoil, "Sun Boil", "How fast the sun's surface churns and its flames rise. 0 holds them still.");
                Draw(SpaceSkyProperties.SunSpin, "Sun Spin", "Degrees per second the sun turns about its own axis.");
            }

            if (Section("Backdrop"))
            {
                Draw(SpaceSkyProperties.VoidColor, "Void", "Colour far from the galactic plane.");
                Draw(SpaceSkyProperties.PlaneColor, "Galactic Plane", "Colour of the band nearest the plane.");
                Draw(SpaceSkyProperties.BackdropBands, "Bands", "Flat colour steps between the void and the plane.");
                Draw(SpaceSkyProperties.BackdropWidth, "Width", "How far the bands reach from the plane. Stars thicken over the same width.");
                Draw(SpaceSkyProperties.BackdropWobble, "Wobble", "How much the hue field bends the band edges.");
            }

            if (Section("Depth"))
            {
                SubHeading("Far Layer");
                Draw(SpaceSkyProperties.FarNebula, "Strength", "A faint, flat nebula behind the main one. It gives the sky depth.");
                Draw(SpaceSkyProperties.FarNebulaColor, "Colour", null);
                Draw(SpaceSkyProperties.FarNebulaThreshold, "Step", "Density where the far layer starts. Lower covers more sky.");
                DrawVector3(SpaceSkyProperties.FarNebulaRotation, "Turn", "How the far layer is turned against the near clouds, in degrees.");
                SubHeading("Dust Lanes");
                Draw(SpaceSkyProperties.DustLanes, "Strength", "Dark filaments in front of the gas and stars along the galactic plane.");
                Draw(SpaceSkyProperties.DustThreshold, "Step", "Density where the lanes start. Lower gives more dust.");
                DrawVector3(SpaceSkyProperties.DustRotation, "Turn", "How the lanes are turned against the clouds, in degrees.");
                SubHeading("Galaxies");
                Draw(SpaceSkyProperties.GalaxyColor, "Colour", null);
                DrawLayer(SpaceSkyProperties.Galaxies, "Galaxies", 2f, 20f);
            }

            if (Section("Nebula"))
            {
                Draw(SpaceSkyProperties.NebulaOpacity, "Opacity", "How strongly the nebula covers the backdrop.");
                Draw(SpaceSkyProperties.NebulaSoftness, "Softness", "0 gives crisp cel steps; higher blends them into soft gas.");
                DrawThresholds();
                Draw(SpaceSkyProperties.NebulaHueSplit, "Hue Split", "Where the hue field switches from palette A to palette B.");
                Draw(SpaceSkyProperties.NebulaStarDimming, "Star Dimming", "How much the nebula hides the stars behind it.");
                SubHeading("Palette A");
                Draw(SpaceSkyProperties.NebulaColorA0, "Faint", null);
                Draw(SpaceSkyProperties.NebulaColorA1, "Mid", null);
                Draw(SpaceSkyProperties.NebulaColorA2, "Core", null);
                SubHeading("Palette B");
                Draw(SpaceSkyProperties.NebulaColorB0, "Faint", null);
                Draw(SpaceSkyProperties.NebulaColorB1, "Mid", null);
                Draw(SpaceSkyProperties.NebulaColorB2, "Core", null);
            }

            if (Section("Comic"))
            {
                Draw(SpaceSkyProperties.InkColor, "Ink", null);
                Draw(SpaceSkyProperties.InkWidth, "Ink Width", "Outline width in pixels at 1080p. 0 removes the ink.");
                Draw(SpaceSkyProperties.CloudLighting, "Cloud Lighting", "Lit and shadowed sides on the clouds, so they read as solid shapes. 0 gives flat clouds.");
                Draw(SpaceSkyProperties.CloudRelief, "Relief", "How broad the lit and shadowed areas are. Higher gives bigger, calmer shapes.");
                Draw(SpaceSkyProperties.ShadowTint, "Shadow Tint", "Multiplies cloud colours on the side away from the light.");
                Draw(SpaceSkyProperties.HighlightColor, "Highlight", "Colour mixed into the edges facing the light.");
                Draw(SpaceSkyProperties.HighlightLevel, "Highlight Level", "How strongly an edge must face the light to be highlighted. Also sets where hatching reaches full strength.");
                Draw(SpaceSkyProperties.Hatching, "Hatching", "Ink strokes in the shadows. 0 removes them.");
                Draw(SpaceSkyProperties.HatchLines, "Hatch Lines", "Strokes around the whole sky. Higher packs them closer.");
                Draw(SpaceSkyProperties.HatchWidth, "Hatch Width", "Stroke width in pixels at 1080p.");
            }

            if (Section("Stars"))
            {
                DrawSeed(SpaceSkyProperties.StarSeed, "Moves every star and galaxy. They are drawn live, so this needs no bake.");
                Draw(SpaceSkyProperties.StarPlaneBoost, "Plane Boost", "Stars are this many times denser along the galactic plane, plus one.");
                Draw(SpaceSkyProperties.StarSparkle, "Sparkle", "Four-pointed glints on the large stars.");
                DrawTints();
                DrawLayer(SpaceSkyProperties.StarSmall, "Small Stars", 0.5f, 6f);
                DrawLayer(SpaceSkyProperties.StarMedium, "Medium Stars", 0.5f, 6f);
                DrawLayer(SpaceSkyProperties.StarLarge, "Large Stars", 0.5f, 6f);
            }

            if (Section("Nebula Shape"))
            {
                DrawSeed(SpaceSkyProperties.NebulaSeed, "Moves every cloud. Changing it re-bakes the nebula.");
                Draw(SpaceSkyProperties.NebulaScale, "Scale", "Higher gives more, smaller clouds.");
                Draw(SpaceSkyProperties.NebulaOctaves, "Detail", "How much fine detail the cloud edges carry.");
                Draw(SpaceSkyProperties.NebulaSwirl, "Swirl", "How strongly the clouds are twisted.");
                Draw(SpaceSkyProperties.NebulaBandWidth, "Band Width", "How thick the band the clouds gather in is.");
                Draw(SpaceSkyProperties.NebulaBandFocus, "Band Focus", "0 spreads clouds over the whole sky; 1 keeps them to the band.");
                Draw(SpaceSkyProperties.NebulaHueScale, "Hue Scale", "Size of the regions that switch palettes.");
                DrawBake(material);
            }

            if (material != null)
                WatchShape(material);
        }

        // A reset keeps what makes this sky its own layout: its seeds, and the map baked from them.
        private static readonly string[] KeptOnReset =
        {
            SpaceSkyProperties.StarSeed, SpaceSkyProperties.NebulaSeed, SpaceSkyProperties.NebulaMap, SpaceSkyProperties.NebulaBakeHash
        };

        private static void ResetLook(Material material)
        {
            Undo.RecordObject(material, "Reset Sky Look");
            Shader shader = material.shader;
            for (int i = 0; i < shader.GetPropertyCount(); ++i)
            {
                string name = shader.GetPropertyName(i);
                if (System.Array.IndexOf(KeptOnReset, name) >= 0)
                    continue;

                switch (shader.GetPropertyType(i))
                {
                    case ShaderPropertyType.Color:
                        material.SetColor(name, shader.GetPropertyDefaultVectorValue(i));
                        break;
                    case ShaderPropertyType.Vector:
                        material.SetVector(name, shader.GetPropertyDefaultVectorValue(i));
                        break;
                    case ShaderPropertyType.Float:
                    case ShaderPropertyType.Range:
                        material.SetFloat(name, shader.GetPropertyDefaultFloatValue(i));
                        break;
                    case ShaderPropertyType.Int:
                        material.SetInteger(name, shader.GetPropertyDefaultIntValue(i));
                        break;
                }
            }

            // A changed shape is re-baked by the inspector shortly after.
            EditorUtility.SetDirty(material);
        }

        private MaterialProperty Find(string name)
        {
            return FindProperty(name, properties);
        }

        private void Draw(string name, string label, string tooltip)
        {
            editor.ShaderProperty(Find(name), new GUIContent(label, tooltip));
        }

        private static bool Section(string title)
        {
            string key = SectionKeyPrefix + title;
            bool expanded = SessionState.GetBool(key, true);
            bool next = EditorGUILayout.BeginFoldoutHeaderGroup(expanded, title);
            EditorGUILayout.EndFoldoutHeaderGroup();
            if (next != expanded)
                SessionState.SetBool(key, next);

            return next;
        }

        private static void SubHeading(string label)
        {
            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
        }

        private void DrawVector3(string name, string label, string tooltip)
        {
            MaterialProperty property = Find(name);
            EditorGUI.showMixedValue = property.hasMixedValue;
            EditorGUI.BeginChangeCheck();
            Vector3 value = EditorGUILayout.Vector3Field(new GUIContent(label, tooltip), property.vectorValue);
            if (EditorGUI.EndChangeCheck())
                Apply(property, value, label);

            EditorGUI.showMixedValue = false;
        }

        private void DrawThresholds()
        {
            MaterialProperty property = Find(SpaceSkyProperties.NebulaThresholds);
            Vector4 value = property.vectorValue;
            EditorGUI.showMixedValue = property.hasMixedValue;
            EditorGUI.BeginChangeCheck();
            value.x = EditorGUILayout.Slider(new GUIContent("Faint Step", "Density where the faint colour starts. Lower covers more sky."), value.x, 0f, 1f);
            value.y = EditorGUILayout.Slider(new GUIContent("Mid Step", "Density where the mid colour starts."), value.y, 0f, 1f);
            value.z = EditorGUILayout.Slider(new GUIContent("Core Step", "Density where the core colour starts."), value.z, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                // Each step sits inside the one before it.
                value.y = Mathf.Max(value.y, value.x);
                value.z = Mathf.Max(value.z, value.y);
                Apply(property, value, "Nebula Steps");
            }

            EditorGUI.showMixedValue = false;
        }

        private void DrawTints()
        {
            Draw(SpaceSkyProperties.StarCoolColor, "Cool Colour", null);
            Draw(SpaceSkyProperties.StarWarmColor, "Warm Colour", null);

            MaterialProperty property = Find(SpaceSkyProperties.StarTints);
            Vector4 value = property.vectorValue;
            EditorGUI.showMixedValue = property.hasMixedValue;
            EditorGUI.BeginChangeCheck();
            value.x = EditorGUILayout.Slider(new GUIContent("Cool Chance", "Share of stars in the cool colour."), value.x, 0f, 1f);
            value.y = EditorGUILayout.Slider(new GUIContent("Warm Chance", "Share of stars in the warm colour. The rest are white."), value.y, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                value.y = Mathf.Min(value.y, 1f - value.x);
                Apply(property, value, "Star Tints");
            }

            EditorGUI.showMixedValue = false;
        }

        // A packed layer vector: cells per face edge, chance per cell, radius in pixels, brightness.
        private void DrawLayer(string name, string label, float minRadius, float maxRadius)
        {
            MaterialProperty property = Find(name);
            Vector4 value = property.vectorValue;
            SubHeading(label);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUI.showMixedValue = property.hasMixedValue;
                EditorGUI.BeginChangeCheck();
                value.x = EditorGUILayout.IntSlider(
                    new GUIContent("Density", "Cells along each edge of a cube face. Each cell holds at most one."),
                    Mathf.RoundToInt(value.x), 4, 400);
                value.y = EditorGUILayout.Slider(new GUIContent("Chance", "Chance that a cell holds one, before the galactic plane's effect."), value.y, 0f, 1f);
                value.z = EditorGUILayout.Slider(new GUIContent("Radius", "Radius in pixels at 1080p. Scales with screen height."), value.z, minRadius, maxRadius);
                value.w = EditorGUILayout.Slider(new GUIContent("Brightness", "Above 1 glows where bloom is used."), value.w, 0f, 4f);
                if (EditorGUI.EndChangeCheck())
                    Apply(property, value, label);

                EditorGUI.showMixedValue = false;
            }
        }

        private void DrawSeed(string name, string tooltip)
        {
            MaterialProperty property = Find(name);
            EditorGUI.showMixedValue = property.hasMixedValue;
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            int seed = EditorGUILayout.IntField(new GUIContent("Seed", tooltip), Mathf.RoundToInt(property.floatValue));
            if (GUILayout.Button(new GUIContent("New", "Pick a random seed."), EditorStyles.miniButton, GUILayout.Width(SeedButtonWidth)))
                seed = SeedPicker.Next(0, SpaceSkyProperties.MaxSeed + 1);

            if (EditorGUI.EndChangeCheck())
            {
                editor.RegisterPropertyChangeUndo("Seed");
                property.floatValue = Mathf.Clamp(seed, 0, SpaceSkyProperties.MaxSeed);
            }

            EditorGUILayout.EndHorizontal();
            EditorGUI.showMixedValue = false;
        }

        private void DrawBake(Material material)
        {
            EditorGUILayout.Space(4f);
            if (material == null)
            {
                EditorGUILayout.HelpBox("Select a single material to bake its nebula.", MessageType.None);
                return;
            }

            if (SpaceSkyBaker.MapPath(material) == null)
            {
                EditorGUILayout.HelpBox("Save the material as an asset to bake its nebula.", MessageType.Info);
                return;
            }

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("Nebula Map", material.GetTexture(SpaceSkyProperties.NebulaMap), typeof(Texture2D), false);

            bool current = SpaceSkyBaker.IsCurrent(material);
            string status = current
                ? lastBakeReport ?? $"Baked, {NebulaBaker.MapSize} x {NebulaBaker.MapSize}."
                : "The shape has changed. Re-baking in a moment.";
            EditorGUILayout.HelpBox(status, current ? MessageType.None : MessageType.Info);

            if (GUILayout.Button("Bake Now"))
            {
                CancelPending();
                BakeAndReport(material);
            }
        }

        // Re-bakes once the shape has been still for BakeDelay. Also catches undo, which changes values without this GUI.
        private static void WatchShape(Material material)
        {
            if (SpaceSkyBaker.MapPath(material) == null)
                return;

            if (SpaceSkyBaker.IsCurrent(material))
            {
                if (pendingMaterial == material)
                    CancelPending();
                return;
            }

            int hash = SpaceSkyBaker.ReadShape(material).ComputeHash(NebulaBaker.MapSize);
            if (pendingMaterial == material && pendingHash == hash)
                return;

            pendingMaterial = material;
            pendingHash = hash;
            pendingTime = EditorApplication.timeSinceStartup + BakeDelay;
            EditorApplication.update -= BakeWhenDue;
            EditorApplication.update += BakeWhenDue;
        }

        private static void BakeWhenDue()
        {
            if (pendingMaterial == null)
            {
                CancelPending();
                return;
            }

            if (EditorApplication.timeSinceStartup < pendingTime)
                return;

            Material material = pendingMaterial;
            CancelPending();
            if (!SpaceSkyBaker.IsCurrent(material))
                BakeAndReport(material);
        }

        private static void CancelPending()
        {
            pendingMaterial = null;
            EditorApplication.update -= BakeWhenDue;
        }

        private static void BakeAndReport(Material material)
        {
            long milliseconds = SpaceSkyBaker.Bake(material);
            lastBakeReport = $"Baked, {NebulaBaker.MapSize} x {NebulaBaker.MapSize}, in {milliseconds} ms.";
            InternalEditorUtility.RepaintAllViews();
        }

        private void Apply(MaterialProperty property, Vector4 value, string undoName)
        {
            editor.RegisterPropertyChangeUndo(undoName);
            property.vectorValue = value;
        }
    }
}
