using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using UnityEngine;
using System.Collections.Generic;

namespace SpaceGraphicsToolkit.Landscape
{
	/// <summary>This component combines features from <b>SgtLandscapeColor</b> and multiple layers of <b>SgtLandscapeDetail</b>.</summary>
	[AddComponentMenu("Space Graphics Toolkit/SGT Landscape Biome")]
	public class SgtLandscapeBiome : SgtLandscapeFeature
	{
		public enum SpaceType
		{
			Global,
			Local
		}

		[System.Serializable]
		public class SgtLandscapeBiomeLayer
		{
			/// <summary>This allows you to enable or disable this specific biome layer.</summary>
			public bool Enabled { set { enabled = value; } get { return enabled; } } [SerializeField] private bool enabled = true;

			/// <summary>If you enable this, then this heightmap will modify the actual mesh geometry height values. If not, it will just be a visual effect like a normal map.</summary>
			public bool Displace { set { displace = value; } get { return displace; } } [SerializeField] private bool displace = true;

			/// <summary>This allows you to specify which height texture the detail uses.</summary>
			public Texture2D HeightTexture { set { heightTexture = value; } get { return heightTexture; } } [SerializeField] private Texture2D heightTexture;

			/// <summary>This allows you to define where in the heightmap the height is 0. For example, if you want this heightmap to go down into the existing terrain, then you must increase this to where the ground is supposed to be flat.</summary>
			public float HeightMidpoint { set { heightMidpoint = value; } get { return heightMidpoint; } } [SerializeField] [Range(0.0f, 1.0f)] private float heightMidpoint;

			/// <summary>This allows you define the difference in height between the lowest and highest points.</summary>
			public float HeightRange { set { heightRange = value; } get { return heightRange; } } [SerializeField] private float heightRange = 1.0f;

			/// <summary>This allows you to specify the size of this layer of detail, which is used to calculate the tiling.</summary>
			public float GlobalSize { set { globalSize = value; } get { return globalSize; } } [SerializeField] protected float globalSize = 1024;

			/// <summary>The detail will be tiled this many times around the landscape.</summary>
			public Vector2 LocalTiling { set { localTiling = value; } get { return localTiling; } } [SerializeField] protected Vector2 localTiling = new Vector2(1.0f, 1.0f);

			/// <summary>This allows you to adjust how deep the heightmap penetrates into the terrain texture when it gets colored.</summary>
			public float Strata { set { strata = value; } get { return strata; } } [SerializeField] private float strata = 1.0f;

			public Vector2 LocalScale { set { localScale = value; } get { return localScale; } } [System.NonSerialized] private Vector2 localScale;
			public double GlobalTiling { set { globalTiling = value; } get { return globalTiling; } } [System.NonSerialized] private double globalTiling;
			public int GlobalIndex { set { globalIndex = value; } get { return globalIndex; } } [System.NonSerialized] private int globalIndex;
			public int GlobalTile { set { globalTile = value; } get { return globalTile; } } [System.NonSerialized] private int globalTile;

			// OLD
			[HideInInspector] [SerializeField] private int heightIndex; public int HeightIndex { get { return heightIndex; } }
		}

		[System.Serializable]
		public class SgtLandscapeBiomeTexture
		{
			/// <summary>This allows you to enable or disable this specific biome layer.</summary>
			public bool Enabled { set { enabled = value; } get { return enabled; } } [SerializeField] private bool enabled = true;

			/// <summary>This allows you to specify which albedo texture is used.</summary>
			public Texture2D AlbedoTexture { set { albedoTexture = value; } get { return albedoTexture; } } [SerializeField] private Texture2D albedoTexture;

			/// <summary>This allows you to specify which normal texture is used.</summary>
			public Texture2D NormalTexture { set { normalTexture = value; } get { return normalTexture; } } [SerializeField] private Texture2D normalTexture;

			/// <summary>This allows you to specify the strength of the strata multiplier.</summary>
			public float StrataWeight { set { strataWeight = value; } get { return strataWeight; } } [SerializeField] private float strataWeight;

			/// <summary>Should the minimum height constraint be applied?</summary>
			public bool UseMinHeight { set { useMinHeight = value; } get { return useMinHeight; } } [SerializeField] private bool useMinHeight;

			/// <summary>The height at which objects start spawning (0% probability).</summary>
			public float MinHeight { set { minHeight = value; } get { return minHeight; } } [SerializeField] private float minHeight;

			/// <summary>The distance over which spawning probability fades in from 0 to 1.</summary>
			public float MinHeightFalloff { set { minHeightFalloff = value; } get { return minHeightFalloff; } } [SerializeField] private float minHeightFalloff;

			/// <summary>Should the maximum height constraint be applied?</summary>
			public bool UseMaxHeight { set { useMaxHeight = value; } get { return useMaxHeight; } } [SerializeField] private bool useMaxHeight;

			/// <summary>The height at which objects stop spawning (0% probability).</summary>
			public float MaxHeight { set { maxHeight = value; } get { return maxHeight; } } [SerializeField] private float maxHeight;

			/// <summary>The distance over which spawning probability fades out from 1 to 0.</summary>
			public float MaxHeightFalloff { set { maxHeightFalloff = value; } get { return maxHeightFalloff; } } [SerializeField] private float maxHeightFalloff;

			/// <summary>Should the minimum slope constraint be applied?</summary>
			public bool UseMinSlope { set { useMinSlope = value; } get { return useMinSlope; } } [SerializeField] private bool useMinSlope;

			/// <summary>The minimum slope angle (in degrees) required for spawning.</summary>
			public float MinSlope { set { minSlope = value; } get { return minSlope; } } [Range(0, 90)] [SerializeField] private float minSlope = 0.0f;

			/// <summary>The angular distance over which spawning probability fades in from 0 to 1.</summary>
			public float MinSlopeFalloff { set { minSlopeFalloff = value; } get { return minSlopeFalloff; } } [SerializeField] private float minSlopeFalloff = 5.0f;

			/// <summary>Should the maximum slope constraint be applied?</summary>
			public bool UseMaxSlope { set { useMaxSlope = value; } get { return useMaxSlope; } } [SerializeField] private bool useMaxSlope;

			/// <summary>The maximum slope angle (in degrees) allowed for spawning.</summary>
			public float MaxSlope { set { maxSlope = value; } get { return maxSlope; } } [Range(0, 90)] [SerializeField] private float maxSlope = 90.0f;

			/// <summary>The angular distance over which spawning probability fades out from 1 to 0.</summary>
			public float MaxSlopeFalloff { set { maxSlopeFalloff = value; } get { return maxSlopeFalloff; } } [SerializeField] private float maxSlopeFalloff = 5.0f;

			/// <summary>This allows you to specify the size of this layer of detail, which is used to calculate the tiling.</summary>
			public float GlobalSize { set { globalSize = value; } get { return globalSize; } } [SerializeField] protected float globalSize = 1024;

			/// <summary>The detail will be tiled this many times around the landscape.</summary>
			public Vector2 LocalTiling { set { localTiling = value; } get { return localTiling; } } [SerializeField] protected Vector2 localTiling = new Vector2(1.0f, 1.0f);

			public double GlobalTiling { set { globalTiling = value; } get { return globalTiling; } } [System.NonSerialized] private double globalTiling;
			public int GlobalIndex { set { globalIndex = value; } get { return globalIndex; } } [System.NonSerialized] private int globalIndex;
			public int GlobalTile { set { globalTile = value; } get { return globalTile; } } [System.NonSerialized] private int globalTile;
		}

		/// <summary>Where should the detail be applied?
		/// Global = The whole landscape.
		/// Local = A small section of the landscape defined by the <b>Transform</b> component's position, rotation, and scale.</summary>
		public SpaceType Space { set { space = value; } get { return space; } } [SerializeField] private SpaceType space;

		/// <summary>The <b>HeightRange</b> and <b>GlobalSize</b> settings will be multiplied by this amount. This is useful if you're changing your planet size and want your detail to match.</summary>
		public float SurfaceScale { set { surfaceScale = value; } get { return surfaceScale; } } [SerializeField] private float surfaceScale = 1.0f;

		/// <summary>If you enable this, then this biome will replace/override any previously written height data.</summary>
		public bool Replace { set { replace = value; } get { return replace; } } [SerializeField] protected bool replace = true;

		/// <summary>The mask texture used by this component.</summary>
		public Texture2D Mask { set { mask = value; } get { return mask; } } [SerializeField] protected Texture2D mask;

		/// <summary>This allows you to invert the mask.</summary>
		public bool MaskInvert { set { maskInvert = value; } get { return maskInvert; } } [SerializeField] private bool maskInvert;

		/// <summary>This allows you to set how sharp or smooth the mask transition is.</summary>
		public float MaskSharpness { set { maskSharpness = value; } get { return maskSharpness; } } [SerializeField] [Range(1.0f, 100.0f)] private float maskSharpness = 1.0f;

		/// <summary>This allows you to offset the mask based on the topology.</summary>
		public float MaskGlobalShift { set { maskGlobalShift = value; } get { return maskGlobalShift; } } [SerializeField] [Range(-1.0f, 1.0f)] private float maskGlobalShift;

		/// <summary>This allows you to enhance the detail around the edges of the mask.</summary>
		public Texture2D MaskDetail { set { maskDetail = value; } get { return maskDetail; } } [SerializeField] private Texture2D maskDetail;

		/// <summary>This allows you to adjust the mask detail texture tiling.</summary>
		public Vector2 MaskDetailTiling { set { maskDetailTiling = value; } get { return maskDetailTiling; } } [SerializeField] private Vector2 maskDetailTiling = new Vector2(10.0f, 10.0f);

		/// <summary>This allows you to maximum amount the mask can be shifted based on the <b>MaskDetail</b> texture in UV space.</summary>
		public float MaskDetailOffset { set { maskDetailOffset = value; } get { return maskDetailOffset; } } [SerializeField] [Range(0.0001f, 0.1f)] private float maskDetailOffset = 0.1f;

		/// <summary>Should this biome color the landscape?</summary>
		public bool Color { set { color = value; } get { return color; } } [SerializeField] private bool color;

		/// <summary>This allows you to specify which gradient texture is used.</summary>
		public Texture2D GradientTexture { set { gradientTexture = value; } get { return gradientTexture; } } [SerializeField] private Texture2D gradientTexture;

		/// <summary>This allows you to adjust which part of the <b>GradientTex</b> is used.</summary>
		public float Variation { set { variation = value; } get { return variation; } } [SerializeField] [Range(0.0f, 1.0f)] private float variation = 0.0f;

		/// <summary>This allows you to adjust which part of the <b>GradientTex</b> is used.</summary>
		public float Offset { set { offset = value; } get { return offset; } } [SerializeField] [Range(0.0f, 1.0f)] private float offset = 0.0f;

		/// <summary>This allows you to adjust the amount of ambient occlusion darkening applied to the slopes of terrain features.</summary>
		public float Occlusion { set { occlusion = value; } get { return occlusion; } } [SerializeField] [Range(0.0f, 10.0f)] private float occlusion = 1.0f;

		/// <summary>This allows you to adjust how many layers of strata appear in the terrain depending on its height.</summary>
		public float Strata { set { strata = value; } get { return strata; } } [SerializeField] private float strata = 1.0f;

		/// <summary>This allows you to smooth out the strata colors.</summary>
		public float Blur { set { blur = value; } get { return blur; } } [SerializeField] [Range(0.0f, 10.0f)] [UnityEngine.Serialization.FormerlySerializedAs("smooth")] private float blur;

		/// <summary>The smoothness will reach its maximum value when the gradient color is at this luminosity.</summary>
		public float SmoothnessMidpoint { set { smoothnessMidpoint = value; } get { return smoothnessMidpoint; } } [SerializeField] [Range(0.0f, 1.0f)] private float smoothnessMidpoint = 0.5f;

		/// <summary>This allows you to set the maximum smoothness value.</summary>
		public float SmoothnessStrength { set { smoothnessStrength = value; } get { return smoothnessStrength; } } [SerializeField] [Range(0.0f, 1.0f)] private float smoothnessStrength;

		/// <summary>This allows you to set the sharpness of the smoothness relative to how close the gradient color is to the <b>SmoothnessMidpoint</b>.</summary>
		public float SmoothnessPower { set { smoothnessPower = value; } get { return smoothnessPower; } } [SerializeField] [Range(0.01f, 100.0f)] private float smoothnessPower = 2.0f;

		/// <summary>The emission will reach its maximum value when the gradient color is at this luminosity.</summary>
		public float EmissionMidpoint { set { emissionMidpoint = value; } get { return emissionMidpoint; } } [SerializeField] [Range(0.0f, 1.0f)] private float emissionMidpoint = 0.5f;

		/// <summary>This allows you to set the maximum emission value.</summary>
		public float EmissionStrength { set { emissionStrength = value; } get { return emissionStrength; } } [SerializeField] [Range(0.0f, 1.0f)] private float emissionStrength;

		/// <summary>This allows you to set the sharpness of the emission relative to how close the gradient color is to the <b>EmissionMidpoint</b>.</summary>
		public float EmissionPower { set { emissionPower = value; } get { return emissionPower; } } [SerializeField] [Range(0.01f, 100.0f)] private float emissionPower = 2.0f;

		/// <summary>This allows you to adjust settings for each layer of detail in this biome.
		/// NOTE: There is a limit of 5 layers.</summary>
		public List<SgtLandscapeBiomeLayer> Layers { get { if (layers == null) layers = new List<SgtLandscapeBiomeLayer>(); return layers; } } [SerializeField] private List<SgtLandscapeBiomeLayer> layers;

		public List<SgtLandscapeBiomeTexture> TextureLayers { get { if (textureLayers == null) textureLayers = new List<SgtLandscapeBiomeTexture>(); return textureLayers; } } [SerializeField] private List<SgtLandscapeBiomeTexture> textureLayers;

		[System.NonSerialized] private double4x4 matrix;

		// OLD
		[SerializeField] private int maskIndex; public int MaskIndex { get { return maskIndex; } }
		[SerializeField] private int maskDetailIndex; public int MaskDetailIndex { get { return maskDetailIndex; } }
		[SerializeField] private int gradientIndex; public int GradientIndex { get { return gradientIndex; } }

		public static SgtLandscapeBiome Create(int layer = 0, Transform parent = null)
		{
			return Create(layer, parent, Vector3.zero, Quaternion.identity, Vector3.one);
		}

		public static SgtLandscapeBiome Create(int layer, Transform parent, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
		{
			return CW.Common.CwHelper.CreateGameObject("Biome", layer, parent, localPosition, localRotation, localScale).AddComponent<SgtLandscapeBiome>();
		}

		public override void RegisterTextures()
		{
			if (layers != null)
			{
				foreach (var layer in layers)
				{
					if (layer != null && layer.Enabled == true)
					{
						SgtLandscapeTextures.RegisterHeight(layer.HeightTexture);
					}
				}
			}

			if (textureLayers != null)
			{
				foreach (var layer in textureLayers)
				{
					if (layer != null && layer.Enabled == true)
					{
						SgtLandscapeTextures.RegisterAlbedo(layer.AlbedoTexture);
						SgtLandscapeTextures.RegisterNormal(layer.NormalTexture);
					}
				}
			}

			SgtLandscapeTextures.RegisterMask(mask);
			SgtLandscapeTextures.RegisterDetail(maskDetail);
			SgtLandscapeTextures.RegisterGradient(gradientTexture);
		}

		public override void Prepare()
		{
			matrix = CalculateMatrix(transform.localPosition, transform.localRotation, transform.localScale);

			if (layers != null)
			{
				foreach (var layer in layers)
				{
					if (layer != null)
					{
						layer.LocalScale = CalculateScale(layer.LocalTiling, layer.HeightRange);

						var index  = default(int);
						var tiling = default(int);

						if (cachedLandscape.GetTilingLayer(layer.GlobalSize * surfaceScale, ref index, ref tiling) == true)
						{
							layer.GlobalTiling = cachedLandscape.GlobalTiling[index] * tiling;
							layer.GlobalIndex  = index;
							layer.GlobalTile   = tiling;
						}
						else
						{
							layer.GlobalTiling = 0.0;
							layer.GlobalIndex  = 0;
							layer.GlobalTile   = 0;
						}
					}
				}
			}

			if (textureLayers != null)
			{
				foreach (var layer in textureLayers)
				{
					if (layer != null)
					{
						var index  = default(int);
						var tiling = default(int);

						if (cachedLandscape.GetTilingLayer(layer.GlobalSize * surfaceScale, ref index, ref tiling) == true)
						{
							layer.GlobalTiling = cachedLandscape.GlobalTiling[index] * tiling;
							layer.GlobalIndex  = index;
							layer.GlobalTile   = tiling;
						}
						else
						{
							layer.GlobalTiling = 0.0;
							layer.GlobalIndex  = 0;
							layer.GlobalTile   = 0;
						}
					}
				}
			}
		}

		public override void Dispose()
		{
		}

		private void ScheduleCpu(SgtLandscape.PendingPoints pending, SgtLandscape.HeightData maskData, Texture2D heightTexture, int globalTile, float heightRange, float heightMidpoint, double2 globalTiling, Vector2 localTiling, float replace)
		{
			var heightData = SgtLandscapeTextures.GetHeightData(heightTexture);

			if (space == SpaceType.Global)
			{
				if (globalTile > 0)
				{
					var job = new SgtLandscapeDetail.GlobalJob();

					job.Coords  = pending.Coords;
					job.DataA   = pending.DataA;
					job.DataB   = pending.DataB;
					job.Heights = pending.Heights;

					job.HeightData08 = heightData.Data08;
					job.HeightData16 = heightData.Data16;
					job.HeightSize   = heightData.Size;
					job.HeightRange  = new float2(-heightRange * heightMidpoint, heightRange) * surfaceScale;

					job.MaskData08 = maskData.Data08;
					job.MaskData16 = maskData.Data16;
					job.MaskSize   = maskData.Size;
					job.MaskShift  = maskGlobalShift;
					job.MaskInvert = maskInvert == true ? 1.0f : 0.0f;

					job.Tiling  = globalTiling;
					job.Replace = replace;

					pending.Handle = job.Schedule(pending.Handle);
				}
			}
			else
			{
				var job = new SgtLandscapeDetail.LocalJob();

				job.Points  = pending.Points;
				job.Heights = pending.Heights;

				job.HeightData08 = heightData.Data08;
				job.HeightData16 = heightData.Data16;
				job.HeightSize   = heightData.Size;
				job.HeightRange  = new float2(-heightRange * heightMidpoint, heightRange) * surfaceScale;

				job.MaskData08 = maskData.Data08;
				job.MaskData16 = maskData.Data16;
				job.MaskSize   = maskData.Size;
				job.MaskInvert = maskInvert == true ? 1.0f : 0.0f;

				job.Matrix  = matrix;
				job.Tiling  = (float2)localTiling;
				job.Replace = replace;

				pending.Handle = job.Schedule(pending.Handle);
			}
		}

		public override void ScheduleCpu(SgtLandscape.PendingPoints pending)
		{
			var maskData = SgtLandscapeTextures.GetMaskData(mask == true ? mask : null);
			var count    = 0;

			if (layers != null)
			{
				var rep = replace == true ? 1.0f : 0.0f;

				foreach (var layer in layers)
				{
					if (layer != null && layer.Enabled == true && layer.Displace == true)
					{
						ScheduleCpu(pending, maskData, layer.HeightTexture, layer.GlobalTile, layer.HeightRange, layer.HeightMidpoint, layer.GlobalTiling, layer.LocalTiling, rep);

						rep    = 0.0f;
						count += 1;
					}
				}
			}

			if (replace == true && count == 0)
			{
				ScheduleCpu(pending, maskData, null, 1, 0.0f, 0.0f, 1.0, Vector2.one, 1.0f);
			}
		}      

#if UNITY_EDITOR
		protected virtual void OnDrawGizmos()
		{
			if (space == SpaceType.Local)
			{
				var m = transform.localToWorldMatrix;

				Gizmos.matrix = m;

				Gizmos.DrawWireCube(Vector3.zero, new Vector3(1.0f, 1.0f, 0.0f));
				Gizmos.DrawWireCube(Vector3.zero, new Vector3(1.0f, 1.0f, 1.0f));

				if (mask == true && mask != null)
				{
					for (var i = 0; i < 16; i++)
					{
						var subMatrix = m * Matrix4x4.Translate(new Vector3(0.0f, 0.0f, Mathf.Lerp(-0.5f, 0.5f, i / 15.0f)));
						CW.Common.CwHelper.DrawShapeOutline(mask, 0, subMatrix);
					}
				}
			}
		}
#endif
	}
}

#if UNITY_EDITOR
namespace SpaceGraphicsToolkit.Landscape
{
	using UnityEngine;
	using UnityEditor;

	[CustomPropertyDrawer(typeof(SgtLandscapeBiome.SgtLandscapeBiomeTexture))]
	public class SgtLandscapeBiomeTextureDrawer : PropertyDrawer
	{
		private static Rect               r;
		private static SerializedProperty p;

		private static SerializedProperty Draw(string name, bool indent = false)
		{
			var c = p.FindPropertyRelative(name); var r2 = r; if (indent == true) r2.xMin += 10; EditorGUI.PropertyField(r2, c); Advance(); return c;
		}

		private static void Advance()
		{
			r.y += EditorGUIUtility.singleLineHeight + 2;
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			EditorGUI.BeginProperty(position, label, property);

			r = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
			p = property;

			Draw("enabled");
			Draw("albedoTexture");
			Draw("normalTexture");
			Draw("strataWeight");

			if (Draw("useMinHeight").boolValue == true)
			{
				Draw("minHeight", true);
				Draw("minHeightFalloff", true);
			}

			if (Draw("useMaxHeight").boolValue == true)
			{
				Draw("maxHeight", true);
				Draw("maxHeightFalloff", true);
			}

			if (Draw("useMinSlope").boolValue == true)
			{
				Draw("minSlope", true);
				Draw("minSlopeFalloff", true);
			}

			if (Draw("useMaxSlope").boolValue == true)
			{
				Draw("maxSlope", true);
				Draw("maxSlopeFalloff", true);
			}

			EditorGUI.EndProperty();
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			r.y = 0.0f;

			Advance(); // enabled
			Advance(); // albedoTexture
			Advance(); // normalTexture
			Advance(); // strataWeight
			Advance(); // useMinHeight
			if (property.FindPropertyRelative("useMinHeight").boolValue) Advance();
			if (property.FindPropertyRelative("useMinHeight").boolValue) Advance();
			Advance(); // useMaxHeight
			if (property.FindPropertyRelative("useMaxHeight").boolValue) Advance();
			if (property.FindPropertyRelative("useMaxHeight").boolValue) Advance();
			Advance(); // useMinSlope
			if (property.FindPropertyRelative("useMinSlope").boolValue) Advance();
			if (property.FindPropertyRelative("useMinSlope").boolValue) Advance();
			Advance(); // useMaxSlope
			if (property.FindPropertyRelative("useMaxSlope").boolValue) Advance();
			if (property.FindPropertyRelative("useMaxSlope").boolValue) Advance();

			return r.y + 10;
		}
	}

	[CanEditMultipleObjects]
	[CustomEditor(typeof(SgtLandscapeBiome))]
	public class SgtLandscapeBiome_Editor : SgtLandscapeFeature_Editor
	{
		protected override void OnInspector()
		{
			SgtLandscapeBiome tgt; SgtLandscapeBiome[] tgts; GetTargets(out tgt, out tgts);

			var markForRebuild = false;

			Draw("space", ref markForRebuild, "Where should the detail be applied?\n\nGlobal = The whole landscape.\n\nLocal = A small section of the landscape defined by the <b>Transform</b> component's position, rotation, and scale.");
			BeginError(Any(tgts, t => t.SurfaceScale <= 0.0f));
				Draw("surfaceScale", ref markForRebuild, "The <b>HeightRange</b> and <b>GlobalSize</b> settings will be multiplied by this amount. This is useful if you're changing your planet size and want your detail to match.");
			EndError();
			Draw("replace", ref markForRebuild, "If you enable this, then this biome will replace/override any previously written height data.");
			Draw("mask", ref markForRebuild, "The mask texture used by this component.");

			if (Any(tgts, t => t.Mask != null))
			{
				BeginIndent();
					Draw("maskInvert", ref markForRebuild, "This allows you to invert the mask.");
					Draw("maskSharpness", ref markForRebuild, "This allows you to set how sharp or smooth the mask transition is.");
					if (Any(tgts, t => t.Space == SgtLandscapeBiome.SpaceType.Global))
					{
						Draw("maskGlobalShift", ref markForRebuild, "This allows you to offset the mask based on the topology.");
					}
					Draw("maskDetail", ref markForRebuild, "This allows you to enhance the detail around the edges of the mask.");

					if (Any(tgts, t => t.MaskDetail == true))
					{
						Draw("maskDetailTiling", ref markForRebuild, "This allows you to adjust the mask detail texture tiling.");
						Draw("maskDetailOffset", ref markForRebuild, "This allows you to maximum amount the mask can be shifted based on the <b>MaskDetail</b> texture in UV space.");
					}
				EndIndent();
			}

			Separator();

			Draw("color", ref markForRebuild, "Should this biome color the landscape?");

			if (Any(tgts, t => t.Color == true))
			{
				BeginIndent();
					Draw("gradientTexture", ref markForRebuild, "This allows you to specify which gradient texture is used.");
					Draw("variation", ref markForRebuild, "This allows you to adjust which part of the <b>GradientTex</b> is used.");
					Draw("offset", ref markForRebuild, "This allows you to adjust which part of the <b>GradientTex</b> is used.");
					Draw("occlusion", ref markForRebuild, "This allows you to adjust the amount of ambient occlusion darkening applied to the slopes of terrain features.");
					Draw("strata", ref markForRebuild, "This allows you to adjust how many layers of strata appear in the terrain depending on its height.");
					Draw("blur", ref markForRebuild, "This allows you to smooth out the strata colors.");
					Draw("smoothnessMidpoint", ref markForRebuild, "The smoothness will reach its maximum value when the gradient color is at this luminosity.");
					Draw("smoothnessStrength", ref markForRebuild, "This allows you to set the maximum smoothness value.");
					Draw("smoothnessPower", ref markForRebuild, "This allows you to set the sharpness of the smoothness relative to how close the gradient color is to the <b>SmoothnessMidpoint</b>.");
					Draw("emissionMidpoint", ref markForRebuild, "The emission will reach its maximum value when the gradient color is at this luminosity.");
					Draw("emissionStrength", ref markForRebuild, "This allows you to set the maximum emission value.");
					Draw("emissionPower", ref markForRebuild, "This allows you to set the sharpness of the emission relative to how close the gradient color is to the <b>EmissionMidpoint</b>.");
				EndIndent();
			}

			Separator();

			Draw("layers", ref markForRebuild);

			Separator();

			Draw("textureLayers", ref markForRebuild);

			Separator();

			if (Any(tgts, t => t.Mask == true) && Button("Randomize MaskTexture") == true)
			{
				Each(tgts, t => t.Mask = SgtLandscape_Editor.Randomize(t.Mask, ref markForRebuild), true);
			}

			if (Any(tgts, t => t.Color == true) && Button("Randomize GradientTexture") == true)
			{
				Each(tgts, t => t.GradientTexture = SgtLandscape_Editor.Randomize(t.GradientTexture, ref markForRebuild), true);
			}

			if (Any(tgts, t => t.Color == true) && Button("Randomize Variation") == true)
			{
				Each(tgts, t => t.Variation = SgtLandscape_Editor.Randomize01(t.Variation, ref markForRebuild), true);
			}

			for (var i = 0; i < 5; i++)
			{
				if (Any(tgts, t => t.Layers.Count > i) && Button("Randomize HeightTexture " + i) == true)
				{
					Each(tgts, t => { if (t.Layers.Count > i) t.Layers[i].HeightTexture = SgtLandscape_Editor.Randomize(t.Layers[i].HeightTexture, ref markForRebuild); }, true);
				}
			}

			if (markForRebuild == true)
			{
				Each(tgts, t => t.MarkForRebuild());
			}
		}

		protected override void OnTransformChanged()
		{
			var tgt = (SgtLandscapeBiome)target;

			if (tgt.Space == SgtLandscapeBiome.SpaceType.Local)
			{
				tgt.MarkForRebuild();
			}
		}
	}
}
#endif