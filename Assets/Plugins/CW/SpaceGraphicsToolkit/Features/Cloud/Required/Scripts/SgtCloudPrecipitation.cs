using UnityEngine;
using CW.Common;
using Unity.Mathematics;

namespace SpaceGraphicsToolkit.Cloud
{
	/// <summary>This component renders infinite precipitation effects around the camera, driven by a cloud coverage texture and a temperature texture.</summary>
	[AddComponentMenu("Space Graphics Toolkit/SGT Cloud Precipitation")]
	public class SgtCloudPrecipitation : MonoBehaviour
	{
		/// <summary>Which layer of the parent cloud to use (0..3).</summary>
		public int CloudLayerIndex { set { cloudLayerIndex = value; } get { return cloudLayerIndex; } } [SerializeField] [Range(0, 3)] private int cloudLayerIndex = 0;

		/// <summary>The material used to render the weather. NOTE: This must use the <b>SGT / CloudPrecipitation</b> shader.</summary>
		public Material Material { set { material = value; } get { return material; } } [SerializeField] private Material material;

		/// <summary>The amount of weather particles used to render the effect.</summary>
		public int Count { set { count = value; } get { return count; } } [SerializeField] private int count = 5000;

		/// <summary>The maximum camera distance particles can appear in world space.</summary>
		public float Range { set { range = value; } get { return range; } } [SerializeField] private float range = 10.0f;

		/// <summary>Coverage value below this will have no weather, above will be 0..1 precipitation.</summary>
		public float CoverageThreshold { set { coverageThreshold = value; } get { return coverageThreshold; } } [SerializeField] [Range(0.0f, 1.0f)] private float coverageThreshold = 0.5f;

		/// <summary>The texture used for individual rain droplets.</summary>
		public Texture2D RainTex { set { rainTex = value; } get { return rainTex; } } [SerializeField] private Texture2D rainTex;

		/// <summary>Multiplies the overall amount of rain.</summary>
		public float RainAmount { set { rainAmount = value; } get { return rainAmount; } } [SerializeField] [Range(0.0f, 10.0f)] private float rainAmount = 1.0f;

		/// <summary>The speed at which the rain falls.</summary>
		public float RainSpeed { set { rainSpeed = value; } get { return rainSpeed; } } [SerializeField] private float rainSpeed = 0.5f;

		/// <summary>The rain particle brightness will be multiplied by this.</summary>
		public float RainBrightness { set { rainBrightness = value; } get { return rainBrightness; } } [SerializeField] private float rainBrightness = 1.0f;

		/// <summary>The thickness of rain particles in world space.</summary>
		public float RainThickness { set { rainThickness = value; } get { return rainThickness; } } [SerializeField] private float rainThickness = 0.02f;

		/// <summary>The length of rain particles in world space.</summary>
		public float RainLength { set { rainLength = value; } get { return rainLength; } } [SerializeField] private float rainLength = 0.2f;

		/// <summary>The texture used for individual snow flakes.</summary>
		public Texture2D SnowTex { set { snowTex = value; } get { return snowTex; } } [SerializeField] private Texture2D snowTex;

		/// <summary>Multiplies the overall amount of snow.</summary>
		public float SnowAmount { set { snowAmount = value; } get { return snowAmount; } } [SerializeField] [Range(0.0f, 10.0f)] private float snowAmount = 1.0f;

		/// <summary>The speed at which the snow falls.</summary>
		public float SnowSpeed { set { snowSpeed = value; } get { return snowSpeed; } } [SerializeField] private float snowSpeed = 0.05f;

		/// <summary>The snow particle brightness will be multiplied by this.</summary>
		public float SnowBrightness { set { snowBrightness = value; } get { return snowBrightness; } } [SerializeField] private float snowBrightness = 1.0f;

		/// <summary>The uniform size of snow particles in world space.</summary>
		public float SnowSize { set { snowSize = value; } get { return snowSize; } } [SerializeField] private float snowSize = 0.03f;

		/// <summary>The frequency of the horizontal snow sway.</summary>
		public float SnowSwayFreq { set { snowSwayFreq = value; } get { return snowSwayFreq; } } [SerializeField] private float snowSwayFreq = 0.5f;

		/// <summary>The amplitude of the horizontal snow sway.</summary>
		public float SnowSwayAmp { set { snowSwayAmp = value; } get { return snowSwayAmp; } } [SerializeField] private float snowSwayAmp = 2.0f;

		/// <summary>The world space height above the InnerRadius where precipitation can be snow. Below this, snow turns to rain.</summary>
		public float SnowMinHeight { set { snowMinHeight = value; } get { return snowMinHeight; } } [SerializeField] private float snowMinHeight = 0.0f;

		/// <summary>Should the weather cast shadows?</summary>
		public bool CastShadows { set { castShadows = value; } get { return castShadows; } } [SerializeField] private bool castShadows;

		/// <summary>The multiplier applied to the camera velocity when calculating particle stretching.</summary>
		public float CameraVelocityMultiplier { set { cameraVelocityMultiplier = value; } get { return cameraVelocityMultiplier; } } [SerializeField] private float cameraVelocityMultiplier = 0.01f;

		/// <summary>The maximum velocity speed cap for camera-induced particle stretching.</summary>
		public float CameraVelocityCap { set { cameraVelocityCap = value; } get { return cameraVelocityCap; } } [SerializeField] private float cameraVelocityCap = 25.0f;

		/// <summary>This allows you to control how much the particles will fade out when the camera moves fast. If you set this to 0 then there will be no fading, and the particles won't fade when stretching, and it will look like you're travelling at warp speed.</summary>
		public float CameraVelocityFade { set { cameraVelocityFade = value; } get { return cameraVelocityFade; } } [SerializeField] [Range(0.0f, 1.0f)] private float cameraVelocityFade = 0.25f;

		[System.NonSerialized] private Mesh generatedMesh;

		[System.NonSerialized] private MaterialPropertyBlock properties;

		[System.NonSerialized] private Vector3 offset;

		[System.NonSerialized] private Vector3 expectedPosition;

		[System.NonSerialized] private SgtCloud cachedCloud;

		private static int _SGT_StretchDirection  = Shader.PropertyToID("_SGT_StretchDirection");
		private static int _SGT_WrapSize          = Shader.PropertyToID("_SGT_WrapSize");
		private static int _SGT_WorldToLocal      = Shader.PropertyToID("_SGT_WorldToLocal");
		private static int _SGT_WorldToPlanet     = Shader.PropertyToID("_SGT_WorldToPlanet");
		private static int _SGT_Offset            = Shader.PropertyToID("_SGT_Offset");
		private static int _SGT_RadiusData        = Shader.PropertyToID("_SGT_RadiusData");
		private static int _SGT_Brightness        = Shader.PropertyToID("_SGT_Brightness");
		
		private static int _SGT_CoverageTex       = Shader.PropertyToID("_SGT_CoverageTex");
		private static int _SGT_TemperatureTex    = Shader.PropertyToID("_SGT_TemperatureTex");
		private static int _SGT_TemperatureInvert = Shader.PropertyToID("_SGT_TemperatureInvert");
		private static int _SGT_CoverageChannel   = Shader.PropertyToID("_SGT_CoverageChannel");
		private static int _SGT_CoverageThreshold = Shader.PropertyToID("_SGT_CoverageThreshold");

		private static int _SGT_RainTex           = Shader.PropertyToID("_SGT_RainTex");
		private static int _SGT_RainData          = Shader.PropertyToID("_SGT_RainData");
		private static int _SGT_RainAmount        = Shader.PropertyToID("_SGT_RainAmount");

		private static int _SGT_SnowTex           = Shader.PropertyToID("_SGT_SnowTex");
		private static int _SGT_SnowData          = Shader.PropertyToID("_SGT_SnowData");
		private static int _SGT_SnowSway          = Shader.PropertyToID("_SGT_SnowSway");
		private static int _SGT_SnowMinHeight     = Shader.PropertyToID("_SGT_SnowMinHeight");

		private static int _SGT_CameraVelocityData = Shader.PropertyToID("_SGT_CameraVelocityData");

		public void GetWeatherIntensity(Vector3 worldPos, out float outRain, out float outSnow)
		{
			outRain = 0.0f;
			outSnow = 0.0f;

			if (cachedCloud == null) return;

			var coverageTex2D    = cachedCloud.CoverageTex as Texture2D;
			var temperatureTex2D = cachedCloud.TemperatureTex as Texture2D;

			if (coverageTex2D == null) return;

			var cloudPos = cachedCloud.transform.InverseTransformPoint(worldPos);
			var coverage   = 0.0f;

			if (coverageTex2D != null && coverageTex2D.isReadable == true)
			{
				var channel    = SgtCloud.ChannelType.Red;
				var cloudCoord = SgtCloud.DirectionToEquirectangular(cloudPos);
				var pixel      = coverageTex2D.GetPixelBilinear(cloudCoord.x, cloudCoord.y);

				if (cachedCloud.CloudLayers != null && cloudLayerIndex >= 0 && cloudLayerIndex < cachedCloud.CloudLayers.Count)
				{
					channel = cachedCloud.CloudLayers[cloudLayerIndex].SampleChannel;
				}

				switch (channel)
				{
					case SgtCloud.ChannelType.Red:   coverage = pixel.r; break;
					case SgtCloud.ChannelType.Green: coverage = pixel.g; break;
					case SgtCloud.ChannelType.Blue:  coverage = pixel.b; break;
					case SgtCloud.ChannelType.Alpha: coverage = pixel.a; break;
				}
			}

			var weatherMask = Mathf.Clamp01((coverage - coverageThreshold) / Mathf.Max(0.0001f, 1.0f - coverageThreshold));
			var temperature = 1.0f; // Default warm

			if (temperatureTex2D != null && temperatureTex2D.isReadable == true)
			{
				var root      = cachedCloud.TemperatureRoot != null ? cachedCloud.TemperatureRoot : transform;
				var rootPos   = root.InverseTransformPoint(worldPos);
				var rootCoord = SgtCloud.DirectionToEquirectangular(rootPos);

				temperature = temperatureTex2D.GetPixelBilinear(rootCoord.x, rootCoord.y).r; // Sample red channel
				temperature = cachedCloud.TemperatureInvert == true ? 1.0f - temperature : temperature;
			}

			var innerR = cachedCloud.Sky != null ? cachedCloud.Sky.InnerMeshRadius : cachedCloud.InnerRadius;

			float snowFraction = 1.0f - temperature;
			float rainFraction = temperature;

			float heightAboveInner = cloudPos.magnitude - innerR;
			float snowWeight       = Mathf.Clamp01(heightAboveInner / Mathf.Max(0.0001f, snowMinHeight));
			snowWeight             = snowWeight * snowWeight; // Squared weight emulation

			// Redistribute the transition difference to rain intensities
			rainFraction += snowFraction * (1.0f - snowWeight);
			snowFraction *= snowWeight;

			// --- ALTITUDE (RADIAL FADE) MATCHING ---
			var layerMid = 0.5f;

			if (cachedCloud.CloudLayers != null && cloudLayerIndex >= 0 && cloudLayerIndex < cachedCloud.CloudLayers.Count)
			{
				var layer = cachedCloud.CloudLayers[cloudLayerIndex];
				layerMid = (layer.Bottom + layer.Top) * 0.5f;
			}

			var outerR = Mathf.Lerp(cachedCloud.InnerRadius, cachedCloud.OuterRadius, layerMid);

			float distFromCenter = cloudPos.magnitude;
			float radiusRange    = Mathf.Max(0.0001f, outerR - innerR);
			float fadeMargin     = radiusRange * 0.1f;

			// Exact HLSL smoothstep emulation
			float innerFade = ShaderSmoothStep(innerR - fadeMargin, innerR, distFromCenter);
			float outerFade = 1.0f - ShaderSmoothStep(outerR, outerR + fadeMargin, distFromCenter);
			float radialFade = innerFade * outerFade;

			// Apply the altitude fade to match the vertex shader output
			outRain = weatherMask * rainFraction * rainAmount * radialFade;
			outSnow = weatherMask * snowFraction * snowAmount * radialFade;
		}

		// Helper method to emulate HLSL's smoothstep (Unity's Mathf.SmoothStep uses a different parameter layout)
		private float ShaderSmoothStep(float edge0, float edge1, float x)
		{
			float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
			return t * t * (3.0f - 2.0f * t);
		}

		public void MarkMeshDirty()
		{
			DestroyImmediate(generatedMesh);
		}

		protected virtual void OnEnable()
		{
			if (properties == null)
			{
				properties = new MaterialPropertyBlock();
			}

			cachedCloud = GetComponentInParent<SgtCloud>();
			if (cachedCloud != null)
			{
				cachedCloud.OnUpdateWeather += HandleUpdateWeather;
			}

			GenerateMesh();

			Camera.onPreCull += HandleCameraPreRender;
			UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += HandleCameraPreRender;
		}

		protected virtual void OnDisable()
		{
			if (cachedCloud != null)
			{
				cachedCloud.OnUpdateWeather -= HandleUpdateWeather;
			}

			Camera.onPreCull -= HandleCameraPreRender;
			UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= HandleCameraPreRender;

			DestroyImmediate(generatedMesh);
		}

		private void HandleUpdateWeather(Bounds bounds, Camera finalCamera)
		{
			if (cachedCloud == null) return;

			CheckPosition();

			var innerR = cachedCloud.Sky != null ? cachedCloud.Sky.InnerMeshRadius : cachedCloud.InnerRadius;
			var layerMid = 0.5f;
			var channel = SgtCloud.ChannelType.Red;

			if (cachedCloud.CloudLayers != null && cloudLayerIndex >= 0 && cloudLayerIndex < cachedCloud.CloudLayers.Count)
			{
				var layer = cachedCloud.CloudLayers[cloudLayerIndex];
				layerMid = (layer.Bottom + layer.Top) * 0.5f;
				channel = layer.SampleChannel;
			}

			var outerR = Mathf.Lerp(cachedCloud.InnerRadius, cachedCloud.OuterRadius, layerMid);

			var center     = (double3)(float3)transform.position;
			var delta      = (double3)(float3)finalCamera.transform.position - center;
			var cameraN    = math.normalize(delta);
			var brightness = CalculateBrightness(cameraN);
			var root       = cachedCloud.TemperatureRoot != null ? cachedCloud.TemperatureRoot : transform;

			properties.SetFloat(_SGT_Brightness, brightness);

			properties.SetVector(_SGT_WrapSize, new Vector4(range * 2.0f, CwHelper.Reciprocal(range * 2.0f), 0.0f, 0.0f));
			properties.SetMatrix(_SGT_WorldToLocal, transform.worldToLocalMatrix);
			properties.SetMatrix(_SGT_WorldToPlanet, root.transform.worldToLocalMatrix);
			properties.SetVector(_SGT_Offset, offset);
			properties.SetVector(_SGT_RadiusData, new Vector4(innerR, outerR, 0.0f, 0.0f));

			properties.SetTexture(_SGT_CoverageTex, cachedCloud.CoverageTex != null ? cachedCloud.CoverageTex : Texture2D.whiteTexture);
			properties.SetTexture(_SGT_TemperatureTex, cachedCloud.TemperatureTex != null ? cachedCloud.TemperatureTex : Texture2D.whiteTexture);
			properties.SetFloat(_SGT_TemperatureInvert, cachedCloud.TemperatureInvert == true ? 1.0f : 0.0f);
			properties.SetVector(_SGT_CoverageChannel, GetCoverageChannelVector(channel));
			properties.SetFloat(_SGT_CoverageThreshold, coverageThreshold);

			properties.SetTexture(_SGT_RainTex, rainTex != null ? rainTex : Texture2D.whiteTexture);
			properties.SetVector(_SGT_RainData, new Vector4(rainSpeed, rainBrightness, rainLength, rainThickness));
			properties.SetFloat(_SGT_RainAmount, rainAmount);

			properties.SetTexture(_SGT_SnowTex, snowTex != null ? snowTex : Texture2D.whiteTexture);
			properties.SetVector(_SGT_SnowData, new Vector4(snowSpeed, snowBrightness, snowSize, snowAmount));
			properties.SetVector(_SGT_SnowSway, new Vector4(snowSwayFreq, snowSwayAmp, 0.0f, 0.0f));
			properties.SetFloat(_SGT_SnowMinHeight, snowMinHeight);

			properties.SetVector(_SGT_CameraVelocityData, new Vector4(cameraVelocityMultiplier, cameraVelocityCap, cameraVelocityFade));

			if (generatedMesh == null)
			{
				GenerateMesh();
			}
		}

		private float CalculateBrightness(double3 camNormal)
		{
			var brightness = 0.0f;
			var direction  = (float3)camNormal;
			
			if (cachedCloud != null && cachedCloud.Sky != null)
			{
				var lightDot = math.smoothstep(0.0f, 1.0f, math.saturate(math.dot(direction, cachedCloud.Sky.LightDirection) * 5.0f + 0.5f));
				brightness += lightDot * cachedCloud.Sky.LightIntensity;
			}

			return brightness;
		}

		private Vector4 GetCoverageChannelVector(SgtCloud.ChannelType channel)
		{
			switch (channel)
			{
				case SgtCloud.ChannelType.Red:   return new Vector4(1.0f, 0.0f, 0.0f, 0.0f);
				case SgtCloud.ChannelType.Green: return new Vector4(0.0f, 1.0f, 0.0f, 0.0f);
				case SgtCloud.ChannelType.Blue:  return new Vector4(0.0f, 0.0f, 1.0f, 0.0f);
				case SgtCloud.ChannelType.Alpha: return new Vector4(0.0f, 0.0f, 0.0f, 1.0f);
			}
			return new Vector4(1.0f, 0.0f, 0.0f, 0.0f);
		}

		private void CheckPosition()
		{
			var currentPosition = transform.position;

			if (expectedPosition != currentPosition)
			{
				offset += currentPosition - expectedPosition;

				offset.x = Mathf.Repeat(offset.x, range * 2.0f);
				offset.y = Mathf.Repeat(offset.y, range * 2.0f);
				offset.z = Mathf.Repeat(offset.z, range * 2.0f);

				expectedPosition = currentPosition;
			}
		}

		private void HandleCameraPreRender(UnityEngine.Rendering.ScriptableRenderContext context, Camera camera)
		{
			HandleCameraPreRender(camera);
		}

		private void HandleCameraPreRender(Camera camera)
		{
			if (generatedMesh != null && material != null)
			{
				Vector3 gravityDown = Vector3.Normalize(transform.position - camera.transform.position);
				if (gravityDown.sqrMagnitude == 0.0f) gravityDown = Vector3.down;
				
				properties.SetVector(_SGT_StretchDirection, gravityDown);

				Graphics.DrawMesh(generatedMesh, Matrix4x4.Translate(camera.transform.position), material, gameObject.layer, camera, 0, properties, castShadows, false);
			}
		}

		private void GenerateMesh()
		{
			var vertices  = new Vector3[count * 4];
			var uv        = new Vector4[count * 4];
			var triangles = new int[count * 6];

			for (int i = 0; i < count; i++)
			{
				var position = new Vector3(UnityEngine.Random.Range(-0.5f, 0.5f), UnityEngine.Random.Range(-0.5f, 0.5f), UnityEngine.Random.Range(-0.5f, 0.5f));
				var seed     = UnityEngine.Random.value;

				vertices[i * 4 + 0] = position;
				vertices[i * 4 + 1] = position;
				vertices[i * 4 + 2] = position;
				vertices[i * 4 + 3] = position;

				uv[i * 4 + 0] = new Vector4(-1.0f,  1.0f,  0.5f, seed);
				uv[i * 4 + 1] = new Vector4( 1.0f,  1.0f, -0.5f, seed);
				uv[i * 4 + 2] = new Vector4(-1.0f, -1.0f,  0.5f, seed);
				uv[i * 4 + 3] = new Vector4( 1.0f, -1.0f, -0.5f, seed);

				triangles[i * 6 + 0] = i * 4 + 0;
				triangles[i * 6 + 1] = i * 4 + 1;
				triangles[i * 6 + 2] = i * 4 + 2;
				triangles[i * 6 + 3] = i * 4 + 3;
				triangles[i * 6 + 4] = i * 4 + 2;
				triangles[i * 6 + 5] = i * 4 + 1;
			}

			generatedMesh = new Mesh();
			generatedMesh.hideFlags   = HideFlags.HideAndDontSave;
			generatedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
			generatedMesh.bounds      = new Bounds(Vector3.zero, Vector3.one * 100);

			generatedMesh.SetVertices(vertices);
			generatedMesh.SetUVs(0, uv);
			generatedMesh.SetTriangles(triangles, 0);
		}
	}
}

#if UNITY_EDITOR
namespace SpaceGraphicsToolkit.Cloud
{
	[UnityEditor.CanEditMultipleObjects]
	[UnityEditor.CustomEditor(typeof(SgtCloudPrecipitation))]
	public class SgtCloudPrecipitation_Editor : CwEditor
	{
		protected override void OnInspector()
		{
			base.OnInspector();

			Separator();

			SgtCloudPrecipitation tgt; SgtCloudPrecipitation[] tgts; GetTargets(out tgt, out tgts);

			var markMeshDirty = false;

			Draw("cloudLayerIndex", "Which layer of the parent cloud to use (0..3).");
			Draw("material", "The material used to render the weather.\n\nNOTE: This must use the <b>SGT / CloudRain</b> shader.");
			Draw("count", ref markMeshDirty, "The amount of particles used to render the effect.");
			Draw("range", "The maximum camera distance particles can appear in world space.");

			Separator();

			Draw("coverageThreshold", "Coverage value below this will have no weather, above will be 0..1 precipitation.");
			
			Separator();

			Draw("rainTex", "The texture used for individual rain droplets.");
			Draw("rainAmount", "Multiplies the overall amount of rain.");
			Draw("rainSpeed", "The speed at which the rain falls.");
			Draw("rainBrightness", "The rain particle brightness will be multiplied by this.");
			Draw("rainThickness", "The thickness of rain particles in world space.");
			Draw("rainLength", "The length of rain particles in world space.");

			Separator();

			Draw("snowTex", "The texture used for individual snow flakes.");
			Draw("snowAmount", "Multiplies the overall amount of snow.");
			Draw("snowSpeed", "The speed at which the snow falls.");
			Draw("snowBrightness", "The snow particle brightness will be multiplied by this.");
			Draw("snowSize", "The uniform size of snow particles in world space.");
			Draw("snowSwayFreq", "The frequency of the horizontal snow sway.");
			Draw("snowSwayAmp", "The amplitude of the horizontal snow sway.");
			Draw("snowMinHeight", "The world space height above the SgtCloud InnerRadius where the precipitation can be snow.");

			Separator();

			Draw("castShadows", "Should the weather cast shadows?");
			Draw("cameraVelocityMultiplier", "The multiplier applied to the camera velocity when calculating particle stretching.");
			Draw("cameraVelocityCap", "The maximum velocity speed cap for camera-induced particle stretching.");
			Draw("cameraVelocityFade", "This allows you to control how much the particles will fade out when the camera moves fast. If you set this to 0 then there will be no fading, and the particles won't fade when stretching, and it will look like you're travelling at warp speed.");

			Separator();

			BeginDisabled();
				DrawIntensity(tgt);
			EndDisabled();

			if (markMeshDirty == true)
			{
				Each(tgts, t => t.MarkMeshDirty(), true, true);
			}
		}

		private void DrawIntensity(SgtCloudPrecipitation tgt)
		{
			var cameraRainAmountStr = "N/A";
			var cameraSnowAmountStr = "N/A";
			
			if (Camera.main != null)
			{
				var cachedCloud = tgt.GetComponentInParent<SgtCloud>();
				if (cachedCloud != null && cachedCloud.CoverageTex != null)
				{
					var covTex2D = cachedCloud.CoverageTex as Texture2D;
					var tempTex2D = cachedCloud.TemperatureTex as Texture2D;
					if (covTex2D != null && covTex2D.isReadable && (tempTex2D == null || tempTex2D.isReadable))
					{
						float outRain, outSnow;
						tgt.GetWeatherIntensity(Camera.main.transform.position, out outRain, out outSnow);
						cameraRainAmountStr = outRain.ToString("F3");
						cameraSnowAmountStr = outSnow.ToString("F3");
					}
					else
					{
						cameraRainAmountStr = "Texture(s) Not Readable";
						cameraSnowAmountStr = "Texture(s) Not Readable";
					}
				}
				else
				{
					cameraRainAmountStr = "No Coverage Texture / Cloud";
					cameraSnowAmountStr = "No Coverage Texture / Cloud";
				}
			}
			else
			{
				cameraRainAmountStr = "No Main Camera Found";
				cameraSnowAmountStr = "No Main Camera Found";
			}
			
			UnityEditor.EditorGUILayout.TextField("Main Cam Rain Intensity", cameraRainAmountStr);
			UnityEditor.EditorGUILayout.TextField("Main Cam Snow Intensity", cameraSnowAmountStr);
		}
	}
}
#endif