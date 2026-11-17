using UnityEngine;
using CW.Common;
using System.Collections.Generic;
using SpaceGraphicsToolkit.Sky;
using SpaceGraphicsToolkit.Volumetrics;
using Unity.Mathematics;

namespace SpaceGraphicsToolkit.Cloud
{
	/// <summary>This component renders a layer of volumetric clouds around a planet.</summary>
	[ExecuteInEditMode]
	[AddComponentMenu("Space Graphics Toolkit/SGT Cloud")]
	public class SgtCloud : SgtVolumeEffect
	{
		public enum ChannelType
		{
			Red,
			Green,
			Blue,
			Alpha
		}

		[System.Serializable]
		public struct CloudLayerType
		{
			[Range(0.0f, 1.0f)] public float Bottom;
			[Range(0.0f, 1.0f)] public float Top;
			[Range(0.0f, 1.0f)] public float Density;
			[Range(0.0f, 1.0f)] public float Softness;
			[Range(-1.0f, 1.0f)] public float Bias;
			[Range(0.0f, 2.0f)] public float BrightnessBottom;
			[Range(0.0f, 2.0f)] public float BrightnessTop;
			public ChannelType SampleChannel;
			[Range(0.0f, 1.0f)] public float SampleThreshold;
			public ChannelType ErodeChannel;
			[Range(0.0f, 1.0f)] public float ErodeStrength;
			public ChannelType CarveChannel;
		}

		[System.Serializable]
		public class CoverageLayerType
		{
			public int Visual;

			public float Height;

			public float Thickness;

			public float Offset;

			public float Speed;

			public int Tiling;

			public float Opacity;

			public Vector4 Layers;
		}

		/// <summary>The Material used to render the cloud.
		/// NOTE: This must use the <b>SGT / Cloud</b> shader.</summary>
		public Material Material { set { material = value; } get { return material; } } [SerializeField] private Material material;

		/// <summary>This texture defines where clouds appear around the planet.
		/// NOTE: This texture should use the equirectangular/cylindrical projection.</summary>
		public Texture CoverageTex { set { coverageTex = value; } get { return coverageTex; } } [SerializeField] private Texture coverageTex;

		/// <summary>The Transform the temperature will be based on (e.g. your planet).</summary>
		public Transform TemperatureRoot { set { temperatureRoot = value; } get { return temperatureRoot; } } [SerializeField] private Transform temperatureRoot;

		/// <summary>Equirectangular texture for temperature. White = warm (rain), Black = cold (snow).</summary>
		public Texture TemperatureTex { set { temperatureTex = value; } get { return temperatureTex; } } [SerializeField] private Texture temperatureTex;

		/// <summary>Invert the TemperatureTex data?</summary>
		public bool TemperatureInvert { set { temperatureInvert = value; } get { return temperatureInvert; } } [SerializeField] private bool temperatureInvert;

		/// <summary>The outer radius of the clouds in local space.</summary>
		public float OuterRadius { set { outerRadius = value; } get { return outerRadius; } } [SerializeField] private float outerRadius = 500.0f;

		/// <summary>The inner radius of the clouds in local space.</summary>
		public float InnerRadius { set { innerRadius = value; } get { return innerRadius; } } [SerializeField] private float innerRadius = 490.0f;

		/// <summary>This allows you to control how much the clouds fade out beyond the horizon. This is useful because the clouds are rendered separately from the atmosphere, and in some scenarios clouds can unexpectedly be seen through the atmosphere.</summary>
		public float CullStrength { set { cullStrength = value; } get { return cullStrength; } } [SerializeField] private float cullStrength = 128;

		/// <summary>Enable this if your clouds are on a gas giant with no solid core.</summary>
		public bool CullInside { set { cullInside = value; } get { return cullInside; } } [SerializeField] private bool cullInside;

		/// <summary>The volumetric sorting offset, to make sure it renders in the correct order relative to the sky.</summary>
		public float SortOffset { set { sortOffset = value; } get { return sortOffset; } } [SerializeField] private float sortOffset = 0.01f;

		/// <summary>The erosion texture is tiled this many times around the planet.
		/// NOTE: This requires the <b>Material</b> setting to have <b>ERODE</b> enabled.</summary>
		public float ErodeTiling { set { erodeTiling = value; } get { return erodeTiling; } } [SerializeField] private float erodeTiling = 70.0f;

		/// <summary>This allows you to restrict erosion to parts of the cloud that are below this level of density.</summary>
		public float ErodeCutoff { set { erodeCutoff = value; } get { return erodeCutoff; } } [SerializeField] [Range(0.01f, 1.0f)] private float erodeCutoff = 0.5f;

		/// <summary>The erosion will begin when the camera is within this distance of the clouds.
		/// NOTE: This requires the <b>Material</b> setting to have <b>ERODE</b> enabled.</summary>
		public float ErodeRange { set { erodeRange = value; } get { return erodeRange; } } [SerializeField] private float erodeRange = 3.0f;

		/// <summary>The erosion texture will cut away this much from the cloud cover.
		/// NOTE: This requires the <b>Material</b> setting to have <b>ERODE</b> enabled.</summary>
		public float ErodeStrength { set { erodeStrength = value; } get { return erodeStrength; } } [SerializeField] private float erodeStrength = 2.0f;

		/// <summary>This works like <b>ErodeRange</b>, but adds an additional layer of erosion up close.</summary>
		public float ErodeBoostRange { set { erodeBoostRange = value; } get { return erodeBoostRange; } } [SerializeField] private float erodeBoostRange = 0.1f;

		/// <summary>This works like <b>ErodeStrength</b>, but adds an additional layer of erosion up close.</summary>
		public float ErodeBoostStrength { set { erodeBoostStrength = value; } get { return erodeBoostStrength; } } [SerializeField] private float erodeBoostStrength;

		/// <summary>The overall tiling frequency scale of the Carve layer.</summary>
		public float CarveTiling { set { carveTiling = value; } get { return carveTiling; } } [SerializeField] private float carveTiling = 70.0f;

		/// <summary>The distance range fade cutoff for the Carve pass.</summary>
		public float CarveRange { set { carveRange = value; } get { return carveRange; } } [SerializeField] private float carveRange = 3.0f;

		/// <summary>The intensity value of the second erosion layer carving.</summary>
		public float CarveStrength { set { carveStrength = value; } get { return carveStrength; } } [SerializeField] private float carveStrength = 2.0f;

		/// <summary>If you want atmospheric effects to apply to this cloud, drag and drop it into here.</summary>
		public SgtSky Sky
		{
			set
			{
				if (sky != value)
				{
					if (sky != null) sky.OnPreDraw -= HandlePreDraw;
					sky = value;
					if (sky != null) sky.OnPreDraw += HandlePreDraw;
				}
			}
			get { return sky; }
		}
		[SerializeField] private SgtSky sky;

		/// <summary>How submerged the observer/camera is in the ocean (0 = on/above water surface, 1 = fully underwater).</summary>
		public float UnderwaterRatio { set { underwaterRatio = Mathf.Clamp01(value); } get { return underwaterRatio; } } [SerializeField] private float underwaterRatio;

		/// <summary>The maximum density of clouds.</summary>
		public float Density { set { density = value; } get { return density; } } [SerializeField] private float density = 5000.0f;

		/// <summary>The height of the generated cloud texture. The width will be set to double this.</summary>
		public int Resolution { set { resolution = value; } get { return resolution; } } [SerializeField] private int resolution = 1024;

		/// <summary>Use a high precision cloud texture for smoother results?</summary>
		public bool HighPrecision { set { highPrecision = value; } get { return highPrecision; } } [SerializeField] private bool highPrecision = true;

		/// <summary>The color of the clouds.</summary>
		public Color Color { set { color = value; } get { return color; } } [SerializeField] private Color color = Color.white;

		/// <summary>The color values will be multiplied by this.</summary>
		public Color Color2 { set { color2 = value; } get { return color2; } } [SerializeField] private Color color2 = Color.white;

		/// <summary>The scattering applied to the edges of clouds will be given this color.</summary>
		public Color ScatteringColor { set { scatteringColor = value; } get { return scatteringColor; } } [SerializeField] private Color scatteringColor = Color.white;

		/// <summary>The color rgb values will be multiplied by this.</summary>
		public float Brightness { set { brightness = value; } get { return brightness; } } [SerializeField] private float brightness = 1.0f;

		/// <summary>If you use the <b>SgtSkyLighter</b> to apply ambient like to the dark side of the planet surface, this allows you to tint it.</summary>
		public Color AmbientTint { set { ambientTint = value; } get { return ambientTint; } } [SerializeField] private Color ambientTint = new Color(1.0f, 1.0f, 1.0f, 0.15f);

		/// <summary>How much the shadows darken the planet or ocean.</summary>
		public float ShadowOpacity { set { shadowOpacity = value; } get { return shadowOpacity; } } [SerializeField] [Range(0.0f, 10.0f)] private float shadowOpacity = 1.0f;

		/// <summary>The clouds will be shadowed using this distance away from the light source.</summary>
		public float ShadowOffset { set { shadowOffset = value; } get { return shadowOffset; } } [SerializeField] [Range(0.001f, 1.0f)] private float shadowOffset = 0.6f;

		/// <summary>The clouds will be shadowed using this LOD level, a higher value is smoother.</summary>
		public int ShadowLod { set { shadowLod = value; } get { return shadowLod; } } [SerializeField] [Range(0.0f, 10.0f)] private int shadowLod;

		/// <summary>The clouds will be shadowed using this detail multiplier.</summary>
		public float ShadowStrength { set { shadowStrength = value; } get { return shadowStrength; } } [SerializeField] [Range(0.0f, 1.0f)] private float shadowStrength = 0.1f;

		/// <summary>The strength of the AO like powder coating effect to show cloud detail.</summary>
		public float ShadowPowder { set { shadowPowder = value; } get { return shadowPowder; } } [SerializeField] [Range(0.0f, 1.0f)] private float shadowPowder;

		/// <summary>The cloud height that will cast self shadows on the clouds. This should usually match the center height of the highest cloud layer.</summary>
		public float LongShadowTargetHeight { set { longShadowTargetHeight = value; } get { return longShadowTargetHeight; } } [SerializeField] [Range(0.0f, 1.0f)] private float longShadowTargetHeight;

		/// <summary>The thickness of the shadows that will be cast.</summary>
		public float LongShadowFadeDistance { set { longShadowFadeDistance = value; } get { return longShadowFadeDistance; } } [SerializeField] [Range(0.0f, 1.0f)] private float longShadowFadeDistance;

		/// <summary>The texture channel used to mask or detail the lightning flash inside the cloud volume.</summary>
		public ChannelType LightningChannel { set { lightningChannel = value; } get { return lightningChannel; } } [SerializeField] private ChannelType lightningChannel = ChannelType.Alpha;

		/// <summary>The tiling scale parameter for the lightning volume noise layout pattern.</summary>
		public float LightningTiling { set { lightningTiling = value; } get { return lightningTiling; } } [SerializeField] private float lightningTiling = 1.0f;

		/// <summary>The intensity/strength modifier for the cloud lightning detail.</summary>
		public float LightningDetail { set { lightningDetail = Mathf.Clamp01(value); } get { return lightningDetail; } } [SerializeField] [Range(0.0f, 1.0f)] private float lightningDetail = 0.5f;

		/// <summary>The near detail of the clouds when ray marching.</summary>
		public float NearDetail { set { nearDetail = value; } get { return nearDetail; } } [SerializeField] [Range(0.0f, 1.0f)] private float nearDetail = 0.07f;

		/// <summary>The distant detail of the clouds when ray marching.</summary>
		public float FarDetail { set { farDetail = value; } get { return farDetail; } } [SerializeField] [Range(0.0f, 1.0f)] private float farDetail = 0.0f;

		/// <summary>The cloud detail position will be offset by this vector.</summary>
		public Vector3 WindOffset { set { windOffset = value; } get { return windOffset; } } [SerializeField] private Vector3 windOffset;

		/// <summary>The <b>CoverageTex</b> has to up 4 texture channels. This allows you to specify up to 4 layers of height/density/shape of each cloud for each layer.
		/// None/null = One layer spanning the whole height range using the Red channel from the coverage texture.</summary>
		public List<CloudLayerType> CloudLayers { get { if (cloudLayers == null) cloudLayers = new List<CloudLayerType>(); return cloudLayers; } } [SerializeField] private List<CloudLayerType> cloudLayers;

		/// <summary>Generate a coverage texture for the cloud rendering?</summary>
		public bool Coverage { set { coverage = value; } get { return coverage; } } [SerializeField] private bool coverage;

		/// <summary>The texture bundle used to generate the coverage texture.</summary>
		public SgtCloudBundle CoverageBundle { set { coverageBundle = value; } get { return coverageBundle; } } [SerializeField] private SgtCloudBundle coverageBundle;

		/// <summary>This allows you to add bands of texture from the <b>CoverageBundle</b>.</summary>
		public List<CoverageLayerType> CoverageLayers { get { if (coverageLayers == null) coverageLayers = new List<CoverageLayerType>(); return coverageLayers; } } [SerializeField] [UnityEngine.Serialization.FormerlySerializedAs("albedoLayers")] private List<CoverageLayerType> coverageLayers;

		/// <summary>Event that fires after the cloud has updated, allowing weather child objects to process.</summary>
		public event System.Action<Bounds, Camera> OnUpdateWeather;

		/// <summary>If the <b>SgtCloudDetail</b> components have changed, calling this will force them to update.</summary>
		public void MarkDetailDirty()
		{
			dirtyDetail = true;
		}

		[System.NonSerialized] private static Mesh sphereMesh;

		[System.NonSerialized] private MaterialPropertyBlock properties;

		[System.NonSerialized] private bool dirtyDetail = true;

		[System.NonSerialized] private RenderTexture generatedCoverageTexture;

		[System.NonSerialized] private List<SgtCloudDetail> detailLayers = new List<SgtCloudDetail>();

		[System.NonSerialized] private Material coverageMaterial;

		[System.NonSerialized] private Vector4[] lightningPositions = new Vector4[16];
		[System.NonSerialized] private Color[]   lightningColors    = new Color[16];
		[System.NonSerialized] private int       lightningIndex     = 0;

		private static Vector4[] tempActivePositions = new Vector4[16];
		private static Vector4[] tempActiveColors    = new Vector4[16];

		private static readonly int _SGT_CoverageTex          = Shader.PropertyToID("_SGT_CoverageTex");
		private static readonly int _SGT_CoverageTex_Size     = Shader.PropertyToID("_SGT_CoverageTex_Size");
		private static readonly int _SGT_Size                 = Shader.PropertyToID("_SGT_Size");
		private static readonly int _SGT_CullStrength         = Shader.PropertyToID("_SGT_CullStrength");
		private static readonly int _SGT_WindOffset           = Shader.PropertyToID("_SGT_WindOffset");
		private static readonly int _SGT_CoverageSize         = Shader.PropertyToID("_SGT_CoverageSize");
		private static readonly int _SGT_Density              = Shader.PropertyToID("_SGT_Density");
		private static readonly int _SGT_Color                = Shader.PropertyToID("_SGT_Color");
		private static readonly int _SGT_Color2               = Shader.PropertyToID("_SGT_Color2");
		private static readonly int _SGT_CloudAmbientTint     = Shader.PropertyToID("_SGT_CloudAmbientTint");
		private static readonly int _SGT_ShadowData           = Shader.PropertyToID("_SGT_ShadowData");
		private static readonly int _SGT_LongShadowParams     = Shader.PropertyToID("_SGT_LongShadowParams");
		private static readonly int _SGT_ScatteringColor      = Shader.PropertyToID("_SGT_ScatteringColor");
		private static readonly int _SGT_RectCount            = Shader.PropertyToID("_SGT_RectCount");
		private static readonly int _SGT_RectDataA            = Shader.PropertyToID("_SGT_RectDataA");
		private static readonly int _SGT_RectDataB            = Shader.PropertyToID("_SGT_RectDataB");
		private static readonly int _SGT_RectDataC            = Shader.PropertyToID("_SGT_RectDataC");
		private static readonly int _SGT_WorldToCloud         = Shader.PropertyToID("_SGT_WorldToCloud");
		private static readonly int _SGT_CloudToWorld         = Shader.PropertyToID("_SGT_CloudToWorld");
		private static readonly int _SGT_Detail               = Shader.PropertyToID("_SGT_Detail");
		private static readonly int _SGT_CloudShadowTex       = Shader.PropertyToID("_SGT_CloudShadowTex");
		private static readonly int _SGT_CloudShadowMatrix    = Shader.PropertyToID("_SGT_CloudShadowMatrix");
		private static readonly int _SGT_CloudShadowDirection = Shader.PropertyToID("_SGT_CloudShadowDirection");
		private static readonly int _SGT_CloudShadowOpacity   = Shader.PropertyToID("_SGT_CloudShadowOpacity");
		private static readonly int _SGT_ErodeData            = Shader.PropertyToID("_SGT_ErodeData");
		private static readonly int _SGT_ErodeTiling          = Shader.PropertyToID("_SGT_ErodeTiling");
		private static readonly int _SGT_ErodeStrength        = Shader.PropertyToID("_SGT_ErodeStrength");
		private static readonly int _SGT_CarveTiling          = Shader.PropertyToID("_SGT_CarveTiling");
		private static readonly int _SGT_CarveStrength        = Shader.PropertyToID("_SGT_CarveStrength");
		private static readonly int _SGT_CarveData            = Shader.PropertyToID("_SGT_CarveData");
		private static readonly int _SGT_LayerSampleChannels  = Shader.PropertyToID("_SGT_LayerSampleChannels");
		private static readonly int _SGT_LayerErodeChannels   = Shader.PropertyToID("_SGT_LayerErodeChannels");
		private static readonly int _SGT_LayerCarveChannels   = Shader.PropertyToID("_SGT_LayerCarveChannels");
		private static readonly int _SGT_LayerBiasK           = Shader.PropertyToID("_SGT_LayerBiasK");
		private static readonly int _SGT_LayerRise0           = Shader.PropertyToID("_SGT_LayerRise0");
		private static readonly int _SGT_LayerRise1           = Shader.PropertyToID("_SGT_LayerRise1");
		private static readonly int _SGT_LayerFall0           = Shader.PropertyToID("_SGT_LayerFall0");
		private static readonly int _SGT_LayerFall1           = Shader.PropertyToID("_SGT_LayerFall1");
		private static readonly int _SGT_LayerDensities       = Shader.PropertyToID("_SGT_LayerDensities");
		private static readonly int _SGT_LayerBounds0         = Shader.PropertyToID("_SGT_LayerBounds0");
		private static readonly int _SGT_LayerBounds1         = Shader.PropertyToID("_SGT_LayerBounds1");
		private static readonly int _SGT_LayerBrightnesses0   = Shader.PropertyToID("_SGT_LayerBrightnesses0");
		private static readonly int _SGT_LayerBrightnesses1   = Shader.PropertyToID("_SGT_LayerBrightnesses1");
		private static readonly int _SGT_LayerSamples0        = Shader.PropertyToID("_SGT_LayerSamples0");
		private static readonly int _SGT_LayerSamples1        = Shader.PropertyToID("_SGT_LayerSamples1");
		private static readonly int _SGT_ErodeNoiseTex        = Shader.PropertyToID("_SGT_ErodeNoiseTex");
		private static readonly int _SGT_RenderSize           = Shader.PropertyToID("_SGT_RenderSize");
		private static readonly int _SGT_FullSize             = Shader.PropertyToID("_SGT_FullSize");
		private static readonly int _SGT_LightningPositions   = Shader.PropertyToID("_SGT_LightningPositions");
		private static readonly int _SGT_LightningColors      = Shader.PropertyToID("_SGT_LightningColors");
		private static readonly int _SGT_LightningMask        = Shader.PropertyToID("_SGT_LightningMask");
		private static readonly int _SGT_LightningData        = Shader.PropertyToID("_SGT_LightningData");

		public RenderTexture GeneratedCoverageTexture
		{
			get
			{
				return generatedCoverageTexture;
			}
		}

		public static SgtCloud Create(int layer = 0, Transform parent = null)
		{
			return Create(layer, parent, Vector3.zero, Quaternion.identity, Vector3.one);
		}

		public static SgtCloud Create(int layer, Transform parent, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
		{
			return CwHelper.CreateGameObject("Cloud", layer, parent, localPosition, localRotation, localScale).AddComponent<SgtCloud>();
		}

		/// <summary>Call this to illuminate the clouds with a lightning flash at a specific local position.</summary>
		public void AddLightningLocal(Vector3 localPos, float radius, Color color)
		{
			if (lightningIndex >= 16) return; // Cap at the shader's maximum array size

			var outerRecip = CwHelper.Reciprocal(outerRadius);
    
			localPos *= outerRecip;
			radius   *= outerRecip;

			lightningPositions[lightningIndex] = new Vector4(localPos.x, localPos.y, localPos.z, CwHelper.Reciprocal(radius));
			lightningColors[lightningIndex]    = color;
    
			lightningIndex++;
		}

		public override void CalculateSortDistance(Vector3 worldPoint)
		{
			var localPoint = transform.InverseTransformPoint(worldPoint);
			var distFromCenter = localPoint.magnitude;
			var midRadius = (innerRadius + outerRadius) * 0.5f;

			// Clamp to shell bounds
			var clampedDist = Mathf.Clamp(distFromCenter, innerRadius, outerRadius);
			var closestPoint = localPoint.normalized * clampedDist;

			var worldClosestPoint = transform.TransformPoint(closestPoint);

			sortDistance = Vector3.Distance(worldPoint, worldClosestPoint) - sortOffset;
		}

		public override void RenderBuffers(SgtVolumeManager manager, Camera finalCamera, int frame, Vector2Int renderSize, Vector2Int fullSize)
		{
			if (material != null)
			{
				if (sky != null)
				{
					sky.ApplySettings(properties, finalCamera);
				}

				if (generatedCoverageTexture != null)
				{
					properties.SetTexture(_SGT_CoverageTex, generatedCoverageTexture);
					properties.SetVector(_SGT_CoverageTex_Size, new Vector4(generatedCoverageTexture.width, generatedCoverageTexture.height));
				}
				else if (coverageTex != null)
				{
					properties.SetTexture(_SGT_CoverageTex, coverageTex);
					properties.SetVector(_SGT_CoverageTex_Size, new Vector4(coverageTex.width, coverageTex.height));
				}
				
				var inflate = 1.02f;
				var radO = 1.0f;
				var radI = radO * (innerRadius / outerRadius);
				var radM = (radI + radO) * 0.5f;
				var radT = radO - radI;

				var cloudToWorld = transform.localToWorldMatrix * Matrix4x4.Scale(Vector3.one * outerRadius);
				var worldToCloud = cloudToWorld.inverse;

				var scale      = outerRadius * 2.0f;
				var worldToSky = Matrix4x4.Scale(new Vector3(scale, scale, scale)) * transform.worldToLocalMatrix;
				var detailX    = radT / radO / Mathf.Lerp(5, 100, nearDetail);
				var detailY    = Mathf.Lerp(1.05f, 1.0f, farDetail);

				properties.SetMatrix(_SGT_WorldToCloud, worldToCloud);
				properties.SetMatrix(_SGT_CloudToWorld, cloudToWorld);

				properties.SetFloat(_SGT_Density, density);
				properties.SetColor(_SGT_Color, color * brightness);
				properties.SetColor(_SGT_Color2, color2 * brightness);
				properties.SetColor(_SGT_CloudAmbientTint, ambientTint * ambientTint.a);
				properties.SetColor(_SGT_ScatteringColor, scatteringColor * scatteringColor.a);
				properties.SetVector(_SGT_Detail, new Vector4(detailX, detailY));
				properties.SetVector(_SGT_Size, new Vector4(radM, 1.0f / radT, radO, radI));
				properties.SetVector(_SGT_CullStrength, new Vector2(cullInside == true ? cullStrength : 0.0f, cullStrength));
				properties.SetVector(_SGT_WindOffset, windOffset);
				properties.SetVector(_SGT_RenderSize, new Vector4(renderSize.x, renderSize.y, 1.0f / renderSize.x, 1.0f / renderSize.y));
				properties.SetVector(_SGT_FullSize, new Vector4(fullSize.x, fullSize.y, 1.0f / fullSize.x, 1.0f / fullSize.y));
				properties.SetVector(_SGT_ShadowData, new Vector4(shadowOffset, shadowLod, shadowStrength, shadowPowder));
				properties.SetVector(_SGT_LongShadowParams, new Vector4(Mathf.Lerp(radI, radO, longShadowTargetHeight), longShadowFadeDistance, 0.0f, 0.0f));

				var activeCount = lightningIndex;

				for (var i = 0; i < activeCount; i++)
				{
					tempActivePositions[i] = lightningPositions[i];
					var c = lightningColors[i];
					tempActiveColors[i]    = new Vector4(c.r, c.g, c.b, c.a);
				}

				lightningIndex = 0;

				var lightningMask = Vector4.zero;

				lightningMask[(int)lightningChannel] = 1.0f;

				properties.SetVector(_SGT_LightningMask, lightningMask);
				properties.SetVector(_SGT_LightningData, new Vector4(activeCount, lightningTiling, lightningDetail));

				if (activeCount > 0)
				{
					properties.SetVectorArray(_SGT_LightningPositions, tempActivePositions);
					properties.SetVectorArray(_SGT_LightningColors, tempActiveColors);
				}

				WriteCloudLayers();

				// Erode
				var finalErodeStrength = erodeStrength;

				if (erodeBoostRange > 0.0f && ErodeBoostStrength > 0.0f)
				{
					var cloudCam = worldToCloud.MultiplyPoint(finalCamera.transform.position);
					var cloudDst = cloudCam.magnitude - radM;

					finalErodeStrength += erodeBoostStrength * Mathf.Pow(Mathf.InverseLerp(erodeBoostRange, 0.0f, cloudDst), 2.0f);
				}

				properties.SetVector(_SGT_ErodeData, new Vector4(0.0f, 0.0f, erodeRange, 1.0f / Mathf.Max(0.0001f, erodeRange)));
				properties.SetFloat(_SGT_ErodeTiling, erodeTiling);
				properties.SetFloat(_SGT_ErodeStrength, finalErodeStrength);

				properties.SetFloat(_SGT_CarveTiling, carveTiling / Mathf.Max(0.0001f, erodeTiling));
				properties.SetFloat(_SGT_CarveStrength, finalErodeStrength * carveStrength);
				properties.SetVector(_SGT_CarveData, new Vector4(0.0f, 0.0f, carveRange, 1.0f / Mathf.Max(0.0001f, carveRange)));

				SgtVolumeCamera.AddDrawMesh(sphereMesh, 0, transform.localToWorldMatrix * Matrix4x4.Scale(Vector3.one * outerRadius * inflate * 2.0f), material, 0, properties);
			}
		}

		private Vector2 SphereTest(Vector3 localPos, Vector3 localDir, float radius)
		{
			var b = Vector3.Dot(localPos, localDir);
			var c = localPos.sqrMagnitude - radius * radius;
			var h = b * b - c;

			if (h < 0.0f) return new Vector2(-1.0f, -1.0f);

			h = Mathf.Sqrt(h);
			return new Vector2(-b - h, -b + h);
		}

		public static Vector2 DirectionToEquirectangular(Vector3 dir)
		{
			var d = dir.normalized;
			var u = Mathf.Atan2(d.z, d.x) / (2.0f * Mathf.PI) + 0.5f;
			var v = Mathf.Asin(d.y) / Mathf.PI + 0.5f;

			return new Vector2(u, v);
		}

		private float GetShellThickness(Vector3 localPos, Vector3 localDir, float rInner, float rOuter, CloudLayerType layer, int lod)
		{
			var hitOuter = SphereTest(localPos, localDir, rOuter);
			if (hitOuter.y <= 0.0f) return 0.0f;

			var hitInner = SphereTest(localPos, localDir, rInner);
			var tMinO    = Mathf.Max(0.0f, hitOuter.x);
			var tMaxO    = hitOuter.y;
			var thickness = 0.0f;
			var midPoint  = Vector3.zero;

			if (hitInner.y <= 0.0f)
			{
				thickness = Mathf.Max(0.0f, tMaxO - tMinO);
				midPoint  = localPos + localDir * (tMinO + thickness * 0.5f);
			}
			else
			{
				var tMinI      = Mathf.Max(0.0f, hitInner.x);
				var tMaxI      = hitInner.y;
				var entryThick = Mathf.Max(0.0f, tMinI - tMinO);
				var exitThick  = Mathf.Max(0.0f, tMaxO - tMaxI);

				thickness = entryThick + exitThick;
				midPoint  = localPos + localDir * (entryThick > exitThick ? (tMinO + entryThick * 0.5f) : (tMaxI + exitThick * 0.5f));
			}

			if (thickness <= 0.001f) return 0.0f;

			var coverageFactor = 0.0f;

			if (coverageTex is Texture2D tex2D && tex2D.isReadable == true)
			{
				var uv       = DirectionToEquirectangular(midPoint);
				var texColor = tex2D.GetPixelBilinear(uv.x, uv.y, math.min(lod, tex2D.mipmapCount - 1));
				var rawValue = 0.0f;

				switch (layer.SampleChannel)
				{
					case ChannelType.Red:   rawValue = texColor.r; break;
					case ChannelType.Green: rawValue = texColor.g; break;
					case ChannelType.Blue:  rawValue = texColor.b; break;
					case ChannelType.Alpha: rawValue = texColor.a; break;
				}

				coverageFactor = Mathf.Clamp01((rawValue - layer.SampleThreshold) / Mathf.Max(0.01f, 1.0f - layer.SampleThreshold));
			}

			return thickness * coverageFactor;
		}

		public float CalculateApproximateOpacity(Vector3 worldPos, Vector3 worldDir, float weight, int lod)
		{
			var localPos = transform.InverseTransformPoint(worldPos);
			var localDir = transform.InverseTransformDirection(worldDir).normalized;
			var radT     = outerRadius - innerRadius;
			var count    = cloudLayers != null && cloudLayers.Count > 0 ? cloudLayers.Count : 1;
			var total    = 0.0f;

			for (var i = 0; i < count; i++)
			{
				var layer = cloudLayers != null && cloudLayers.Count > 0 ? cloudLayers[i] : new CloudLayerType { Bottom = 0.0f, Top = 1.0f, Density = 1.0f };
				var rI    = innerRadius + Mathf.Min(layer.Bottom, layer.Top) * radT;
				var rO    = innerRadius + Mathf.Max(layer.Bottom, layer.Top) * radT;
				var thick = GetShellThickness(localPos, localDir, rI, rO, layer, lod);

				total += thick * layer.Density * density;
			}

			return (float)(1.0 - math.exp(-total * 0.0001 * weight));
		}

		public void RandomizeColorHue()
		{
			var h = 0.0f;
			var s = 0.0f;
			var v = 0.0f;

			Color.RGBToHSV(color, out h, out s, out v);

			h = UnityEngine.Random.value;

			color = Color.HSVToRGB(h, s, v);
		}

		public void RandomizeColor2Hue()
		{
			var h = 0.0f;
			var s = 0.0f;
			var v = 0.0f;

			Color.RGBToHSV(color2, out h, out s, out v);

			h = UnityEngine.Random.value;

			color2 = Color.HSVToRGB(h, s, v);
		}

		public void RandomizeCoverageLayers()
		{
			if (coverageLayers != null)
			{
				foreach (var coverageLayer in coverageLayers)
				{
					if (coverageLayer != null)
					{
						var maxVisual = coverageBundle != null ? coverageBundle.Slices.Count : int.MaxValue;

						coverageLayer.Visual    = UnityEngine.Random.Range(0, maxVisual);
						coverageLayer.Height    = UnityEngine.Random.Range(0.1f, 0.9f);
						coverageLayer.Thickness = UnityEngine.Random.Range(0.02f, 0.2f);
						coverageLayer.Offset    = UnityEngine.Random.Range(0.0f, 1.0f);
						coverageLayer.Speed     = UnityEngine.Random.Range(-0.001f, 0.001f);
						coverageLayer.Tiling    = UnityEngine.Random.Range(1, 5);
						coverageLayer.Opacity   = UnityEngine.Random.Range(0.5f, 1.0f);
						coverageLayer.Layers    = new Vector4(UnityEngine.Random.Range(0.5f, 1.0f), UnityEngine.Random.Range(0.5f, 1.0f), UnityEngine.Random.Range(0.5f, 1.0f), UnityEngine.Random.Range(0.5f, 1.0f));
					}
				}
			}
		}

		public void ApplyShadowSettings(MaterialPropertyBlock properties)
		{
			var generatedMatrix    = Matrix4x4.Scale(Vector3.one * (1.0f / outerRadius)) * transform.worldToLocalMatrix;
			var generatedDirection = Vector3.up;

			if (sky != null)
			{
				generatedDirection = transform.InverseTransformDirection(sky.LightDirection);
			}

			if (coverage == true)
			{
				if (generatedCoverageTexture == null)
				{
					UpdateCoverage();
				}

				properties.SetTexture(_SGT_CloudShadowTex, generatedCoverageTexture);
				properties.SetMatrix(_SGT_CloudShadowMatrix, generatedMatrix);
				properties.SetVector(_SGT_CloudShadowDirection, generatedDirection);
				properties.SetVector(_SGT_CloudShadowOpacity, new Vector4(shadowOpacity, 0.0f, 0.0f, 0.0f));
			}
			else if (coverageTex != null)
			{
				properties.SetTexture(_SGT_CloudShadowTex, coverageTex);
				properties.SetMatrix(_SGT_CloudShadowMatrix, generatedMatrix);
				properties.SetVector(_SGT_CloudShadowDirection, generatedDirection);
				properties.SetVector(_SGT_CloudShadowOpacity, new Vector4(shadowOpacity, 0.0f, 0.0f, 0.0f));
			}
			else
			{
				ClearShadowSettings(properties);
			}
		}

		public static void ClearShadowSettings(MaterialPropertyBlock properties)
		{
			properties.SetVector(_SGT_CloudShadowOpacity, new Vector4(0.0f, 0.0f, 0.0f, 0.0f));
		}

		protected override void OnEnable()
		{
			base.OnEnable();

			if (sphereMesh == null)
			{
				var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);

				go.hideFlags = HideFlags.DontSave;

				sphereMesh = go.GetComponent<MeshFilter>().sharedMesh;

				DestroyImmediate(go);
			}

			if (properties == null)
			{
				properties = new MaterialPropertyBlock();
			}

			if (sky != null)
			{
				sky.OnPreDraw += HandlePreDraw;
			}

			properties.SetTexture(_SGT_ErodeNoiseTex, SgtVolumeManager.NoiseTexture);
		}

		protected override void OnDisable()
		{
			base.OnDisable();

			if (sky != null)
			{
				sky.OnPreDraw -= HandlePreDraw;
			}

			if (generatedCoverageTexture != null)
			{
				generatedCoverageTexture.Release();

				DestroyImmediate(generatedCoverageTexture);
			}

			DestroyImmediate(coverageMaterial);
		}

		private void HandlePreDraw(ref Material drawMaterial, MaterialPropertyBlock drawProperties, Bounds bounds, Camera finalCamera)
		{
			if (OnUpdateWeather != null) OnUpdateWeather.Invoke(bounds, finalCamera);
		}

		private void CheckGeneratedCoverageTexture()
		{
			var format = highPrecision == true ? RenderTextureFormat.ARGBFloat : RenderTextureFormat.ARGB32;
			var width  = resolution * 2;
			var height = resolution;

			if (coverage == false || resolution <= 0)
			{
				DestroyImmediate(generatedCoverageTexture);
			}

			if (generatedCoverageTexture != null)
			{
				if (generatedCoverageTexture.width != width || generatedCoverageTexture.height != height || generatedCoverageTexture.format != format)
				{
					generatedCoverageTexture.Release();

					generatedCoverageTexture.format = format;
					generatedCoverageTexture.width  = width;
					generatedCoverageTexture.height = height;

					generatedCoverageTexture.Create();
				}
			}
			else if (coverage == true && resolution > 0)
			{
				var mips = Mathf.FloorToInt(Mathf.Log(Mathf.Max(width, height), 2)) + 1;
				var desc = new RenderTextureDescriptor(width, height, format, 0, mips);

				desc.sRGB = false;

				generatedCoverageTexture = new RenderTexture(desc);
				generatedCoverageTexture.hideFlags         = HideFlags.DontSave;
				generatedCoverageTexture.enableRandomWrite = true;
				generatedCoverageTexture.wrapMode          = TextureWrapMode.Clamp;
				generatedCoverageTexture.useMipMap         = true;
				generatedCoverageTexture.autoGenerateMips  = false;

				generatedCoverageTexture.Create();
			}
		}

		protected virtual void Update()
		{
			if (coverageBundle != null)
			{
				coverageBundle.HandleUpdate();
			}
		}

		protected virtual void LateUpdate()
		{
			if (SgtVolumeManager.Instances.Count > 0)
			{
				var cloudToWorld = transform.localToWorldMatrix * Matrix4x4.Scale(Vector3.one * outerRadius);
				var worldToCloud = cloudToWorld.inverse;

				windOffset += worldToCloud.MultiplyVector(SgtVolumeManager.Instances.First.Value.WindVelocity * Time.deltaTime);
			}

			UpdateCoverage();
		}

		public void WriteCloudLayers()
		{
			var biasK      = Vector4.zero;
			var rise0      = Vector4.zero;
			var rise1      = Vector4.zero;
			var fall0      = Vector4.zero;
			var fall1      = Vector4.zero;
			var densities  = Vector4.zero;
			var bounds0    = Vector4.zero; 
			var bounds1    = Vector4.zero;
			var bright0    = Vector4.zero;
			var bright1    = Vector4.zero;
			var sample0    = Vector4.zero;
			var sample1    = Vector4.zero;
			var sampleChannels = Matrix4x4.zero;
			var erodeChannels  = Matrix4x4.zero;
			var carveChannels  = Matrix4x4.zero;

			// Update the default layer initialization to include brightness defaults (1.0f)
			var layer      = new CloudLayerType { Bottom = 0.0f, Top = 1.0f, Softness = 0.5f, Bias = -0.5f, Density = 1.0f, BrightnessBottom = 0.5f, BrightnessTop = 1.0f, SampleChannel = ChannelType.Red, SampleThreshold = 0.0f, ErodeChannel = ChannelType.Red, ErodeStrength = 1.0f, CarveChannel = ChannelType.Red };
			var layerCount = (cloudLayers != null && cloudLayers.Count > 0) ? Mathf.Min(cloudLayers.Count, 4) : 1;

			for (var i = 0; i < layerCount; i++)
			{
				if (cloudLayers != null && cloudLayers.Count > 0)
				{
					layer = cloudLayers[i];
				}

				var b = Mathf.Min(layer.Bottom, layer.Top);
				var t = Mathf.Max(layer.Bottom, layer.Top); t = Mathf.Max(t, b + 0.0001f); // Guarantee valid span

				var bias01 = Mathf.Clamp(0.5f - (layer.Bias * 0.4999f), 0.0001f, 0.9999f);
				var k = (1.0f / bias01) - 2.0f;

				// Map authored endpoints into biased space
				var biasedB = b / (k * (1.0f - b) + 1.0f);
				var biasedT = t / (k * (1.0f - t) + 1.0f);

				var span = Mathf.Max(biasedT - biasedB, 0.0001f);
				var soft = Mathf.Max(span * layer.Softness, 0.0001f);

				biasK[i] = k;
				rise0[i] = biasedB;
				rise1[i] = Mathf.Min(biasedB + soft, biasedT);
				fall0[i] = biasedT;
				fall1[i] = Mathf.Max(biasedT - soft, biasedB);
				densities[i] = layer.Density;
				bounds0[i] = b;
				bounds1[i] = t;
				bright0[i] = (layer.BrightnessBottom == 0f && layer.BrightnessTop == 0f) ? 1.0f : layer.BrightnessBottom;
				bright1[i] = (layer.BrightnessBottom == 0f && layer.BrightnessTop == 0f) ? 1.0f : layer.BrightnessTop;
				sample0[i] = layer.SampleThreshold;
				sample1[i] = CwHelper.Reciprocal(1.0f - layer.SampleThreshold);

				var sampleChannel = Vector4.zero; sampleChannel[(int)layer.SampleChannel] = 1.0f;
				var erodeChannel  = Vector4.zero; erodeChannel[(int)layer.ErodeChannel] = layer.ErodeStrength;
				var carveChannel  = Vector4.zero; carveChannel[(int)layer.CarveChannel] = 1.0f;

				sampleChannels.SetRow(i, sampleChannel);
				erodeChannels.SetColumn(i, erodeChannel);
				carveChannels.SetColumn(i, carveChannel);
			}

			properties.SetVector(_SGT_LayerBiasK, biasK);
			properties.SetVector(_SGT_LayerRise0, rise0);
			properties.SetVector(_SGT_LayerRise1, rise1);
			properties.SetVector(_SGT_LayerFall0, fall0);
			properties.SetVector(_SGT_LayerFall1, fall1);
			properties.SetVector(_SGT_LayerDensities, densities);
			properties.SetVector(_SGT_LayerBounds0, bounds0);
			properties.SetVector(_SGT_LayerBounds1, bounds1);
			properties.SetVector(_SGT_LayerBrightnesses0, bright0);
			properties.SetVector(_SGT_LayerBrightnesses1, bright1);
			properties.SetVector(_SGT_LayerSamples0, sample0);
			properties.SetVector(_SGT_LayerSamples1, sample1);
			properties.SetMatrix(_SGT_LayerSampleChannels, sampleChannels);
			properties.SetMatrix(_SGT_LayerErodeChannels, erodeChannels);
			properties.SetMatrix(_SGT_LayerCarveChannels, carveChannels);
		}

		private void UpdateCoverage()
		{
			if (coverageBundle != null)
			{
				coverageBundle.HandleLateUpdate();
			}

			if (coverageMaterial == null) coverageMaterial = CwHelper.CreateTempMaterial("Blit Mat2", "Hidden/SgtCloud_Coverage");
			if (coverageMaterial == null) return;

			var oldWrite  = GL.sRGBWrite;
			var oldActive = RenderTexture.active;

			coverageMaterial.SetTexture(_SGT_CoverageTex, coverageTex);
			coverageMaterial.SetVector(_SGT_CoverageSize, coverageTex != null ? new Vector2(coverageTex.width, coverageTex.height) : Vector2.zero);

			if (dirtyDetail == true)
			{
				dirtyDetail = false;

				GetComponentsInChildren(detailLayers);
			}

			var detailLayerCount = 0;

			foreach (var cloudDetail in detailLayers)
			{
				if (cloudDetail != null && cloudDetail.enabled == true)
				{
					if (Application.isPlaying == true)
					{
						cloudDetail.Offset += cloudDetail.Speed * Time.deltaTime;
					}

					coverageMaterial.SetTexture("_SGT_DetailTex" + detailLayerCount, cloudDetail.CoverageTex);
					coverageMaterial.SetVector("_SGT_DetailData" + detailLayerCount, new Vector4(cloudDetail.CarveEdge, cloudDetail.CarveCore, cloudDetail.Scale, cloudDetail.Offset));
					coverageMaterial.SetVector("_SGT_DetailChannels" + detailLayerCount, cloudDetail.Channels);

					if (++detailLayerCount >= 3)
					{
						break;
					}
				}
			}

			CheckGeneratedCoverageTexture();

			coverageMaterial.DisableKeyword("_SGT_DETAIL0");
			coverageMaterial.DisableKeyword("_SGT_DETAIL1");
			coverageMaterial.DisableKeyword("_SGT_DETAIL2");
			coverageMaterial.DisableKeyword("_SGT_DETAIL3");
			coverageMaterial.EnableKeyword("_SGT_DETAIL" + detailLayerCount);

			GL.sRGBWrite = true;

			if (coverage == true)
			{
				var coverageLayerCount = 0;

				if (coverageBundle != null && coverageLayers != null && coverageBundle.GetTexture() != null)
				{
					coverageMaterial.EnableKeyword("_SGT_GENERATE_COVERAGE");

					var tex = coverageBundle.GetTexture();

					coverageMaterial.SetTexture(_SGT_CoverageTex, tex);
					coverageMaterial.SetVector(_SGT_CoverageSize, new Vector2(tex.width, tex.height));

					foreach (var coverageLayer in coverageLayers)
					{
						if (coverageLayer != null)
						{
							if (Application.isPlaying == true)
							{
								coverageLayer.Offset = (coverageLayer.Offset + coverageLayer.Speed * Time.deltaTime) % 1.0f;
							}

							var coords = coverageBundle.GetCoords(coverageLayer.Visual);

							rectDataA[coverageLayerCount] = new Vector4(coverageLayer.Height, coverageLayer.Thickness, coords.x, coords.y);
							rectDataB[coverageLayerCount] = new Vector4(coverageLayer.Offset, coverageLayer.Tiling, coverageLayer.Opacity, 0.0f);
							rectDataC[coverageLayerCount] = coverageLayer.Layers;

							coverageLayerCount += 1;

							if (coverageLayerCount >= SGT_MAX_RECTS)
							{
								break;
							}
						}
					}

					if (coverageLayerCount == 0)
					{
						rectDataA[coverageLayerCount] = new Vector4(0.5f, 0.5f, 0.0f, 1.0f);
						rectDataB[coverageLayerCount] = new Vector4(0.0f, 1.0f, 1.0f, 0.0f);
						rectDataC[coverageLayerCount] = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);

						coverageLayerCount += 1;
					}

					coverageMaterial.SetInt(_SGT_RectCount, coverageLayerCount);
					coverageMaterial.SetVectorArray(_SGT_RectDataA, rectDataA);
					coverageMaterial.SetVectorArray(_SGT_RectDataB, rectDataB);
					coverageMaterial.SetVectorArray(_SGT_RectDataC, rectDataC);
				}

				Graphics.Blit(default(Texture), generatedCoverageTexture, coverageMaterial, 0);

				generatedCoverageTexture.GenerateMips();
			}
			else
			{
				coverageMaterial.DisableKeyword("_SGT_GENERATE_COVERAGE");
			}

			GL.sRGBWrite = oldWrite;
			RenderTexture.active = oldActive;
		}

		private static readonly int SGT_MAX_RECTS = 64;

		private static Vector4[] rectDataA = new Vector4[SGT_MAX_RECTS];
		private static Vector4[] rectDataB = new Vector4[SGT_MAX_RECTS];
		private static Vector4[] rectDataC = new Vector4[SGT_MAX_RECTS];
	}
}

#if UNITY_EDITOR
namespace SpaceGraphicsToolkit.Cloud
{
	using UnityEditor;

	[CanEditMultipleObjects]
	[CustomEditor(typeof(SgtCloud))]
	public class SgtCloud_Editor : CwEditor
	{
		protected override void OnInspector()
		{
			SgtCloud tgt; SgtCloud[] tgts; GetTargets(out tgt, out tgts);

			if (Button("Add Detail") == true)
			{
				Each(tgts, t => { AddChildComponent<SgtCloudDetail>(t); }, true);
			}

			Separator();

			BeginError(Any(tgts, t => t.Material == null));
				Draw("material", "The Material used to render the cloud.\n\nNOTE: This must use the <b>SGT / Cloud</b> shader.");
			EndError();
			BeginError(Any(tgts, t => t.Sky == null));
				Draw("sky", "If you want atmospheric effects to apply to this cloud, drag and drop it into here.");
			EndError();
			Draw("outerRadius", "The outer radius of the clouds in local space.");
			Draw("innerRadius", "The inner radius of the clouds in local space.");
			Draw("cullStrength", "This allows you to control how much the clouds fade out beyond the horizon. This is useful because the clouds are rendered separately from the atmosphere, and in some scenarios clouds can unexpectedly be seen through the atmosphere.");
			Draw("cullInside", "Enable this if your clouds are on a gas giant with no solid core.");
			Draw("density", "The maximum density of clouds.");
			Draw("resolution", "The height of the generated cloud texture. The width will be set to double this.");
			Draw("highPrecision", "Use a high precision cloud texture for smoother results?");
			Draw("color", "The color of the clouds.");
			Draw("color2", "The color of the clouds.");
			Draw("scatteringColor", "The scattering applied to the edges of clouds will be given this color.");
			Draw("brightness", "The color values will be multiplied by this.");
			Draw("ambientTint", "If you use the <b>SgtSkyLighter</b> to apply ambient like to the dark side of the planet surface, this allows you to tint it.");
			Draw("underwaterRatio", "How submerged the observer/camera is in the ocean (0 = on/above water surface, 1 = fully underwater).");

			Separator();

			Draw("nearDetail", "The near detail of the clouds when ray marching.");
			Draw("farDetail", "The far detail of the clouds when ray marching.");
			Draw("windOffset", "The cloud detail position will be offset by this vector.");

			Separator();

			Draw("shadowOpacity", "How much the shadows darken the planet or ocean.");
			Draw("shadowOffset", "The clouds will be shadowed using this distance away from the light source.");
			//Draw("shadowLod", "The clouds will be shadowed using this LOD level, a higher value is smoother.");
			//Draw("shadowStrength", "The clouds will be shadowed using this detail multiplier.");
			Draw("shadowPowder", "The strength of the AO like powder coating effect to show cloud detail.");

			Separator();

			Draw("longShadowTargetHeight");
			Draw("longShadowFadeDistance");
			Draw("lightningChannel", "The texture channel used to mask or detail the lightning flash inside the cloud volume.");
			Draw("lightningTiling", "The tiling scale parameter for the lightning volume noise layout pattern.");
			Draw("lightningDetail", "The intensity/strength modifier for the cloud lightning detail.");

			Separator();

			Draw("coverageTex", "This texture defines where clouds appear around the planet.\n\nNOTE: This texture should use the equirectangular/cylindrical projection.");
			Draw("sortOffset", "The volumetric sorting offset, to make sure it renders in the correct order relative to the sky.");

			Separator();

			Draw("temperatureRoot", "The Transform the temperature will be based on (e.g. your planet).");
			Draw("temperatureTex", "Equirectangular texture for temperature. White = warm (rain), Black = cold (snow).");
			Draw("temperatureInvert", "Invert the TemperatureTex data?");

			Separator();

			Draw("erodeTiling", "The erosion texture is tiled this many times around the planet.\n\nNOTE: This requires the <b>Material</b> setting to have <b>ERODE</b> enabled.");
			Draw("erodeRange", "The erosion will begin when the camera is within this distance of the clouds.\n\nNOTE: This requires the <b>Material</b> setting to have <b>ERODE</b> enabled.");
			Draw("erodeStrength", "The erosion texture will cut away this much from the cloud cover.\n\nNOTE: This requires the <b>Material</b> setting to have <b>ERODE</b> enabled.");
			Draw("erodeBoostRange", "This works like <b>ErodeRange</b>, but adds an additional layer of erosion up close.");
			Draw("erodeBoostStrength", "This works like <b>ErodeStrength</b>, but adds an additional layer of erosion up close.");

			Separator();

			Draw("carveTiling", "The overall tiling frequency scale of the Carve layer.");
			Draw("carveRange", "The distance range fade cutoff for the Carve pass.");
			Draw("carveStrength", "The intensity value of the second erosion layer carving.");

			Separator();

			Draw("cloudLayers");

			Separator();

			Draw("coverage", "Generate a coverage texture for the cloud rendering?");

			if (Any(tgts, t => t.Coverage == true))
			{
				BeginIndent();
					Draw("coverageBundle", "The coverage texture will be generated from this texture bundle.", "Bundle");
					Draw("coverageLayers", "This allows you to add bands of texture from the <b>CoverageBundle</b>", "Bands");
					BeginDisabled();
						EditorGUILayout.ObjectField("GeneratedCoverageTexture", tgt.GeneratedCoverageTexture, typeof(Texture), true);
					EndDisabled();
				EndIndent();
			}

			Separator();

			Separator();

			if (Button("Randomize Color Hue") == true)
			{
				Each(tgts, t => { t.RandomizeColorHue(); t.RandomizeColor2Hue(); }, true, true);
			}

			if (Any(tgts, t => t.CoverageLayers.Count > 0) && Button("Randomize Coverage Layers") == true)
			{
				Each(tgts, t => { t.RandomizeCoverageLayers(); }, true);
			}

			//Separator();

			//BeginDisabled();
			//	UnityEditor.EditorGUILayout.ObjectField("GeneratedTexture", tgt.GeneratedTexture, typeof(RenderTexture), true);
			//EndDisabled();
		}

		public static Texture2D Randomize(Texture2D current)
		{
			if (current != null)
			{
				var currentPath = UnityEditor.AssetDatabase.GetAssetPath(current);
				var guids       = UnityEditor.AssetDatabase.FindAssets("t:Texture2D", new string[] { System.IO.Path.GetDirectoryName(currentPath) });

				if (guids.Length > 0)
				{
					var guid = guids[UnityEngine.Random.Range(0, guids.Length)];
					var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
					var next = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);

					if (current != next)
					{
						current = next;
					}
				}
			}

			return current;
		}

		private static T AddChildComponent<T>(SgtCloud tgt)
			where T : Component
		{
			var child = new GameObject(typeof(T).Name);

			child.transform.SetParent(tgt.transform, false);

			var component = child.AddComponent<T>();

			CW.Common.CwHelper.SelectAndPing(component);

			return component;
		}

		public static float Randomize01()
		{
			return UnityEngine.Random.Range(0.0f, 1.0f);
		}

		[UnityEditor.MenuItem("GameObject/CW/Space Graphics Toolkit/Cloud", false, 10)]
		public static void CreateMenuItem()
		{
			var parent   = CwHelper.GetSelectedParent();
			var instance = SgtCloud.Create(parent != null ? parent.gameObject.layer : 0, parent);

			CwHelper.SelectAndPing(instance);
		}
	}

	[CustomPropertyDrawer(typeof(SgtCloud.CoverageLayerType))]
	public class AlbedoLayerTypeDrawer : PropertyDrawer
	{
		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			// 1 line for label + 6 fields + 1 group of 4 sliders
			return EditorGUIUtility.singleLineHeight * 9 + 14f;
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			EditorGUI.BeginProperty(position, label, property);

			// Reset to defaults?
			var height    = property.FindPropertyRelative("Height");
			var thickness = property.FindPropertyRelative("Thickness");
			var tiling    = property.FindPropertyRelative("Tiling");
			var opacity   = property.FindPropertyRelative("Opacity");
			var layers    = property.FindPropertyRelative("Layers");

			if (height.floatValue == 0.0f && thickness.floatValue == 0.0f && tiling.intValue == 0 && opacity.floatValue == 0.0f)
			{
				height.floatValue = 0.5f;
				thickness.floatValue = 0.25f;
				tiling.intValue = 1;
				opacity.floatValue = 1.0f;
			}

			if (layers.vector4Value == Vector4.zero)
			{
				layers.vector4Value = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
			}

			var lineHeight = EditorGUIUtility.singleLineHeight;
			var spacing    = 2.0f;
			var rect       = new Rect(position.x, position.y, position.width, lineHeight);

			// Label
			EditorGUI.LabelField(rect, label);
			rect.y += lineHeight + spacing;

			// Visual (int)
			EditorGUI.PropertyField(rect, property.FindPropertyRelative("Visual"));
			rect.y += lineHeight + spacing;

			DrawFloatSlider(ref rect, property, "Height", 0f, 1f);
			DrawFloatSlider(ref rect, property, "Thickness", 0f, 1f);
			DrawFloatSlider(ref rect, property, "Offset", 0f, 1f);
			DrawFloatSlider(ref rect, property, "Speed", 0f, 1f);
			DrawIntSlider(ref rect, property, "Tiling", 1, 20);
			DrawFloatSlider(ref rect, property, "Opacity", 0f, 1f);

			Vector4 layersVec = layers.vector4Value;

			// Calculate total width available
			float labelWidth = EditorGUIUtility.labelWidth;
			float remainingWidth = position.width - labelWidth;
			float sliderWidth = (remainingWidth - 6f) / 4f; // 3 spacings between 4 sliders

			Rect labelRect = new Rect(position.x, rect.y, labelWidth, lineHeight);
			EditorGUI.LabelField(labelRect, "Layers");

			// Sliders
			float sliderX = position.x + labelWidth;
			Rect sliderRect = new Rect(sliderX, rect.y, sliderWidth, lineHeight);

			layersVec.x = GUI.HorizontalSlider(sliderRect, layersVec.x, 0f, 1f);

			sliderRect.x += sliderWidth + 2f;
			layersVec.y = GUI.HorizontalSlider(sliderRect, layersVec.y, 0f, 1f);

			sliderRect.x += sliderWidth + 2f;
			layersVec.z = GUI.HorizontalSlider(sliderRect, layersVec.z, 0f, 1f);

			sliderRect.x += sliderWidth + 2f;
			layersVec.w = GUI.HorizontalSlider(sliderRect, layersVec.w, 0f, 1f);

			// Apply changes
			layers.vector4Value = layersVec;

			// Move rect down
			rect.y += lineHeight + spacing;

			EditorGUI.EndProperty();
		}

		private void DrawFloatSlider(ref Rect rect, SerializedProperty parent, string name, float min, float max)
		{
			SerializedProperty prop = parent.FindPropertyRelative(name);
			prop.floatValue = EditorGUI.Slider(rect, ObjectNames.NicifyVariableName(name), prop.floatValue, min, max);
			rect.y += EditorGUIUtility.singleLineHeight + 2f;
		}

		private void DrawIntSlider(ref Rect rect, SerializedProperty parent, string name, int min, int max)
		{
			SerializedProperty prop = parent.FindPropertyRelative(name);
			prop.intValue = EditorGUI.IntSlider(rect, ObjectNames.NicifyVariableName(name), prop.intValue, min, max);
			rect.y += EditorGUIUtility.singleLineHeight + 2f;
		}
	}
}
#endif