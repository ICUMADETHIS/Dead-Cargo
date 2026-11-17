using Unity.Mathematics;
using UnityEngine;

namespace SpaceGraphicsToolkit.Landscape
{
	/// <summary>This component can be added to a child GameObject of your landscape. It will texture the landscape based on its height and slope data using a set of textures.</summary>
	[AddComponentMenu("Space Graphics Toolkit/SGT Landscape Texture")]
	public class SgtLandscapeTexture : SgtLandscapeFeature
	{
		public enum SpaceType
		{
			Global,
			Local
		}

		/// <summary>Where should the color be applied?
		/// Global = The whole landscape.
		/// Local = A small section of the landscape defined by the <b>Transform</b> component's position, rotation, and scale.</summary>
		public SpaceType Space { set { space = value; } get { return space; } } [SerializeField] private SpaceType space;

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

		/// <summary>This allows you to adjust the mask detail texture tiling.</summary>
		public float MaskDetailOffset { set { maskDetailOffset = value; } get { return maskDetailOffset; } } [SerializeField] [Range(0.0001f, 0.1f)] private float maskDetailOffset = 0.1f;

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

		/// <summary>The detail will be tiled this many times around the landscape.</summary>
		public Vector2 LocalTiling { set { localTiling = value; } get { return localTiling; } } [SerializeField] protected Vector2 localTiling = new Vector2(1.0f, 1.0f);

		/// <summary>This allows you to specify the size of this layer of detail, which is used to calculate the tiling.</summary>
		public float GlobalSize { set { globalSize = value; } get { return globalSize; } } [SerializeField] protected float globalSize = 1024;

		// OLD
		[SerializeField] private int maskIndex; public int MaskIndex { get { return maskIndex; } }
		[SerializeField] private int maskDetailIndex; public int MaskDetailIndex { get { return maskDetailIndex; } }
		[SerializeField] private int gradientIndex; public int GradientIndex { get { return gradientIndex; } }

		public static SgtLandscapeTexture Create(int layer = 0, Transform parent = null)
		{
			return Create(layer, parent, Vector3.zero, Quaternion.identity, Vector3.one);
		}

		public static SgtLandscapeTexture Create(int layer, Transform parent, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
		{
			return CW.Common.CwHelper.CreateGameObject("SgtLandscapeTexture", layer, parent, localPosition, localRotation, localScale).AddComponent<SgtLandscapeTexture>();
		}

		public override void RegisterTextures()
		{
			SgtLandscapeTextures.RegisterMask(mask);
			SgtLandscapeTextures.RegisterDetail(maskDetail);
			SgtLandscapeTextures.RegisterAlbedo(albedoTexture);
			SgtLandscapeTextures.RegisterNormal(normalTexture);
		}

		public override void Prepare()
		{
		}

		public override void Dispose()
		{
		}

		public override void ScheduleCpu(SgtLandscape.PendingPoints pending)
		{
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

				if (mask != null)
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
	[UnityEditor.CanEditMultipleObjects]
	[UnityEditor.CustomEditor(typeof(SgtLandscapeTexture))]
	public class SgtLandscapeTexture_Editor : SgtLandscapeFeature_Editor
	{
		protected override void OnInspector()
		{
			SgtLandscapeTexture tgt; SgtLandscapeTexture[] tgts; GetTargets(out tgt, out tgts);

			var markForRebuild = false;

			Draw("space", ref markForRebuild, "Where should the color be applied?\n\nGlobal = The whole landscape.\n\nLocal = A small section of the landscape defined by the <b>Transform</b> component's position, rotation, and scale.");
			Draw("mask", ref markForRebuild, "The mask texture used by this component.");

			if (Any(tgts, t => t.Mask != null))
			{
				BeginIndent();
					Draw("maskInvert", ref markForRebuild, "This allows you to invert the mask.");
					Draw("maskSharpness", ref markForRebuild, "This allows you to set how sharp or smooth the mask transition is.");
					if (Any(tgts, t => t.Space == SgtLandscapeTexture.SpaceType.Global))
					{
						Draw("maskGlobalShift", ref markForRebuild, "This allows you to offset the mask based on the topology.");
					}
					Draw("maskDetail", ref markForRebuild, "This allows you to enhance the detail around the edges of the mask.");

					if (Any(tgts, t => t.MaskDetail != null))
					{
						BeginIndent();
							Draw("maskDetailTiling", ref markForRebuild, "This allows you to adjust the mask detail texture tiling.");
							Draw("maskDetailOffset", ref markForRebuild, "This allows you to maximum amount the mask can be shifted based on the <b>MaskDetail</b> texture in UV space.");
						EndIndent();
					}
				EndIndent();
			}

			Separator();

			BeginError(Any(tgts, t => t.AlbedoTexture == null));
				Draw("albedoTexture", ref markForRebuild, "This allows you to specify which albedo texture is used.");
			EndError();
			BeginError(Any(tgts, t => t.NormalTexture == null));
				Draw("normalTexture", ref markForRebuild, "This allows you to specify which normal texture is used.");
			EndError();
			Draw("strataWeight", ref markForRebuild, "This allows you to specify the strength of the strata multiplier.");
			Draw("useMinHeight", ref markForRebuild, "Should the minimum height constraint be applied?");
			if (Any(tgts, t => t.UseMinHeight))
			{
				BeginIndent();
					Draw("minHeight", ref markForRebuild, "The height at which objects start spawning (0% probability).");
					Draw("minHeightFalloff", ref markForRebuild, "The distance over which spawning probability fades in from 0 to 1.");
				EndIndent();
			}

			Draw("useMaxHeight", ref markForRebuild, "Should the maximum height constraint be applied?");
			if (Any(tgts, t => t.UseMaxHeight))
			{
				BeginIndent();
					Draw("maxHeight", ref markForRebuild, "The height at which objects stop spawning (0% probability).");
					Draw("maxHeightFalloff", ref markForRebuild, "The distance over which spawning probability fades out from 1 to 0.");
				EndIndent();
			}

			Separator();

			Draw("useMinSlope", ref markForRebuild, "Should the minimum slope constraint be applied?");
			if (Any(tgts, t => t.UseMinSlope))
			{
				BeginIndent();
					Draw("minSlope", ref markForRebuild, "The minimum slope angle (in degrees) required for spawning.");
					Draw("minSlopeFalloff", ref markForRebuild, "The angular distance over which spawning probability fades in from 0 to 1.");
				EndIndent();
			}

			Draw("useMaxSlope", ref markForRebuild, "Should the maximum slope constraint be applied?");
			if (Any(tgts, t => t.UseMaxSlope))
			{
				BeginIndent();
					Draw("maxSlope", ref markForRebuild, "The maximum slope angle (in degrees) allowed for spawning.");
					Draw("maxSlopeFalloff", ref markForRebuild, "The angular distance over which spawning probability fades out from 1 to 0.");
				EndIndent();
			}

			Separator();

			if (Any(tgts, t => t.Space == SgtLandscapeTexture.SpaceType.Global))
			{
				Draw("globalSize", ref markForRebuild, "This allows you to specify the size of this layer of detail, which is used to calculate the tiling.");
			}
			if (Any(tgts, t => t.Space == SgtLandscapeTexture.SpaceType.Local))
			{
				Draw("localTiling", ref markForRebuild, "The detail will be tiled this many times around the landscape.");
			}

			Separator();

			if (Any(tgts, t => t.Mask == true) && Button("Randomize MaskTexture") == true)
			{
				Each(tgts, t => t.Mask = SgtLandscape_Editor.Randomize(t.Mask, ref markForRebuild), true);
			}

			if (Button("Randomize AlbedoTexture") == true)
			{
				Each(tgts, t => t.AlbedoTexture = SgtLandscape_Editor.Randomize(t.AlbedoTexture, ref markForRebuild), true);
			}
			
			if (Button("Randomize NormalTexture") == true)
			{
				Each(tgts, t => t.NormalTexture = SgtLandscape_Editor.Randomize(t.NormalTexture, ref markForRebuild), true);
			}

			if (markForRebuild == true)
			{
				Each(tgts, t => t.MarkForRebuild());
			}
		}

		protected override void OnTransformChanged()
		{
			var tgt = (SgtLandscapeTexture)target;

			if (tgt.Space == SgtLandscapeTexture.SpaceType.Local)
			{
				tgt.MarkForRebuild();
			}
		}
	}
}
#endif