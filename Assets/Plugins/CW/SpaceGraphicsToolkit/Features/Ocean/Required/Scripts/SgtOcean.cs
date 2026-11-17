using UnityEngine;
using CW.Common;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using SpaceGraphicsToolkit.Landscape;
using SpaceGraphicsToolkit.Sky;
using SpaceGraphicsToolkit.Cloud;
using SpaceGraphicsToolkit.Volumetrics;
using SpaceGraphicsToolkit.LightAndShadow;

namespace SpaceGraphicsToolkit.Ocean
{
	/// <summary>This component can be used to render oceans for your planets.</summary>
	[ExecuteInEditMode]
	[AddComponentMenu("Space Graphics Toolkit/SGT Ocean")]
	public partial class SgtOcean : SgtVolumeEffect, CwChild.IHasChildren
	{
		/// <summary>The material used to render the fluid surface.
		/// NOTE: This must use the <b>SGT/Ocean</b> shader.</summary>
		public Material Material { set { material = value; } get { return material; } } [SerializeField] private Material material;

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

		/// <summary>The radius of the ocean.</summary>
		public float Radius { set { radius = value; } get { return radius; } } [SerializeField] private float radius = 50.0f;

		/// <summary>The overall detail of the ocean relative to the camera distance. The higher you set this, the more triangles it will have.</summary>
		public float Detail { set { detail = value; } get { return detail; } } [SerializeField] [Range(0.01f, 5.0f)] private float detail = 1.2f;

		/// <summary>The density of the fluid used for buoyancy in kg/m^3 (seawater = 1025).</summary>
		public float FluidDensity { set { fluidDensity = value; } get { return fluidDensity; } } [SerializeField] private float fluidDensity = 1025.0f;

		/// <summary>If you want cloud shadows to appear on the surface of the ocean, specify them here.</summary>
		public SgtCloud Cloud { set { cloud = value; } get { return cloud; } } [SerializeField] [UnityEngine.Serialization.FormerlySerializedAs("cloudShadow")] protected SgtCloud cloud;

		public Camera Observer { set { observer = value; } get { return observer; } } [SerializeField] protected Camera observer;

		/// <summary>This allows you to offset the camera distance in world space when rendering the ocean, giving you fine control over the render order.</summary>
		public float CameraOffset { set { cameraOffset = value; } get { return cameraOffset; } } [SerializeField] private float cameraOffset = 0.1f;

		/// <summary>The color of the fluid when viewed from above the surface.</summary>
		public Color SurfaceColor { set { surfaceColor = value; } get { return surfaceColor; } } [SerializeField] private Color surfaceColor = new Color(0.007843138f, 0.3647059f, 0.5019608f);

		/// <summary>The color of the sub surface scattering. Alpha controls strength.</summary>
		public Color SurfaceScattering { set { surfaceScattering = value; } get { return surfaceScattering; } } [SerializeField] private Color surfaceScattering = new Color(0.19f, 0.84f, 0.78f, 0.5f);

		/// <summary>The density of the water when viewed from above the surface.</summary>
		public float SurfaceDensity { set { surfaceDensity = value; } get { return surfaceDensity; } } [SerializeField] private float surfaceDensity = 0.5f;

		/// <summary>The PBR smoothness of the surface material.</summary>
		public float SurfaceSmoothness { set { surfaceSmoothness = value; } get { return surfaceSmoothness; } } [SerializeField] [Range(0.0f, 1.0f)] private float surfaceSmoothness = 0.9f;

		/// <summary>This allows you to control how much the smoothness value can be reduced by clouds in the way.</summary>
		public float SurfaceSmoothnessOcclusion { set { surfaceSmoothnessOcclusion = value; } get { return surfaceSmoothnessOcclusion; } } [SerializeField] [Range(0.0f, 1.0f)] private float surfaceSmoothnessOcclusion;

		/// <summary>This allows you to control the LOD used by <b>SurfaceSmoothnessOcclusion</b>.</summary>
		public int SurfaceSmoothnessLod { set { surfaceSmoothnessLod = value; } get { return surfaceSmoothnessLod; } } [SerializeField] [Range(0, 8)] private int surfaceSmoothnessLod;

		/// <summary>The inner radius threshold below which the camera is fully submerged underwater.</summary>
		public float UnderwaterRadius { set { underwaterRadius = value; } get { return underwaterRadius; } } [SerializeField] private float underwaterRadius = 0.5f;

		/// <summary>The distance from the camera the cloud shadow calculations will use. This should approximately be the underwater fog distance (4.6 * density).</summary>
		public float UnderwaterShadowRange { set { underwaterShadowRange = value; } get { return underwaterShadowRange; } } [SerializeField] private float underwaterShadowRange = 10.0f;

		/// <summary>The maximum +- height displacement of the waves in meters.</summary>
		public float WavesAmplitude { set { wavesAmplitude = value; } get { return wavesAmplitude; } } [SerializeField] [Range(0.0f, 20.0f)] private float wavesAmplitude = 0.2f;

		/// <summary>How choppy the waves are.</summary>
		public float WavesSteepness { set { wavesSteepness = value; } get { return wavesSteepness; } } [SerializeField] [Range(0.01f, 0.5f)] private float wavesSteepness = 0.05f;

		/// <summary>How fast the waves animate.</summary>
		public float WavesSpeed { set { wavesSpeed = value; } get { return wavesSpeed; } } [SerializeField] [Range(0.0f, 1.0f)] private float wavesSpeed = 0.3f;

		/// <summary>The amount of combined wave layers.</summary>
		public int WavesQuality { set { wavesQuality = value; } get { return wavesQuality; } } [SerializeField] [Range(1, 3)] private int wavesQuality = 3;

		/// <summary>The direction of the wind in world space.</summary>
		public Vector3 WindDirection { set { windDirection = value; } get { return windDirection; } } [SerializeField] private Vector3 windDirection = Vector3.forward;

		/// <summary>The speed of the wind in meters.</summary>
		public float WindSpeed { set { windSpeed = value; } get { return windSpeed; } } [SerializeField] private float windSpeed;

		/// <summary>The size of the shore texture in world space.</summary>
		public float ShoreScale { set { shoreScale = value; } get { return shoreScale; } } [SerializeField] private float shoreScale = 1.0f;

		/// <summary>The animation speed of the shore.</summary>
		public float ShoreSpeed { set { shoreSpeed = value; } get { return shoreSpeed; } } [SerializeField] [Range(0.0f, 2.0f)] private float shoreSpeed = 0.75f;

		/// <summary>Apply small details to the ocean surface?</summary>
		public bool Micro { set { micro = value; } get { return micro; } } [SerializeField] [UnityEngine.Serialization.FormerlySerializedAs("ripples")] private bool micro = true;

		/// <summary>The dual normal map texture for detail.
		/// RG = 0..1 NormalA.xy
		/// BA = 0..1 NormalB.xy</summary>
		public Texture2D MicroTexture { set { microTexture = value; } get { return microTexture; } } [SerializeField] [UnityEngine.Serialization.FormerlySerializedAs("ripplesTexture")] private Texture2D microTexture;

		/// <summary>The size of the detail texture in world space.</summary>
		public float MicroScale { set { microScale = value; } get { return microScale; } } [SerializeField] [UnityEngine.Serialization.FormerlySerializedAs("ripplesScale")] private float microScale = 1.0f;

		/// <summary>The animation speed of the detail.</summary>
		public float MicroSpeed { set { microSpeed = value; } get { return microSpeed; } } [SerializeField] [UnityEngine.Serialization.FormerlySerializedAs("ripplesSpeed")] [Range(0.0f, 2.0f)] private float microSpeed = 0.75f;

		/// <summary>The detail texture strength will be multiplied by this.</summary>
		public float MicroStrength { set { microStrength = value; } get { return microStrength; } } [SerializeField] [Range(0.01f, 1.0f)] [UnityEngine.Serialization.FormerlySerializedAs("ripplesStrength")] private float microStrength = 0.5f;

		/// <summary>Apply large details to the ocean surface?</summary>
		public bool Macro { set { macro = value; } get { return macro; } } [SerializeField] private bool macro;

		/// <summary>The dual normal map texture for detail.
		/// RG = 0..1 NormalA.xy
		/// BA = 0..1 NormalB.xy</summary>
		public Texture2D MacroTexture { set { macroTexture = value; } get { return macroTexture; } } [SerializeField] private Texture2D macroTexture;

		/// <summary>The size of the macro texture in world space.</summary>
		public float MacroScale { set { macroScale = value; } get { return macroScale; } } [SerializeField] private float macroScale = 100.0f;

		/// <summary>The animation speed of the macro ripples.</summary>
		public float MacroSpeed { set { macroSpeed = value; } get { return macroSpeed; } } [SerializeField] [Range(0.0f, 2.0f)] private float macroSpeed = 0.1f;

		/// <summary>The detail texture strength will be multiplied by this.</summary>
		public float MacroStrength { set { macroStrength = value; } get { return macroStrength; } } [SerializeField] [Range(0.01f, 1.0f)] private float macroStrength = 0.5f;

		/// <summary>Ocean rendering can look bat at extreme distances, so fade it out.
		/// NOTE: If you enable this, your landscape should use the <b>OceanFade</b> setting.</summary>
		public bool Fade { set { fade = value; } get { return fade; } } [SerializeField] private bool fade;

		/// <summary>The ocean will completely disappear at this distance in world space.</summary>
		public float FadeDistance { set { fadeDistance = value; } get { return fadeDistance; } } [SerializeField] private float fadeDistance = 200.0f;

		/// <summary>The deep ocean surface effect will be fully revealed at this distance in world space.</summary>
		public float FadeDeepDistance { set { fadeDeepDistance = value; } get { return fadeDeepDistance; } } [SerializeField] private float fadeDeepDistance = 10000.0f;

		/// <summary>The deep ocean surface effect will have this density.</summary>
		public float FadeDeepDensity { set { fadeDeepDensity = value; } get { return fadeDeepDensity; } } [SerializeField] private float fadeDeepDensity = 0.1f;

		/// <summary>The light source for the caustics.</summary>
		public SgtLight CausticsLight { set { causticsLight = value; } get { return causticsLight; } } [SerializeField] private SgtLight causticsLight;

		/// <summary>The size of the caustics texture in world space.</summary>
		public float CausticsScale { set { causticsScale = value; } get { return causticsScale; } } [SerializeField] private float causticsScale = 1.0f;

		/// <summary>The animation speed of the caustics.</summary>
		public float CausticsSpeed { set { causticsSpeed = value; } get { return causticsSpeed; } } [SerializeField] [Range(0.0f, 2.0f)] private float causticsSpeed = 0.05f;

		[System.NonSerialized] private static Texture3D noiseTex;
		[System.NonSerialized] private static bool      noiseTexCreated;

		[System.NonSerialized]
		private MaterialPropertyBlock properties;

		[System.NonSerialized]
		private float landscapeOpacity = -0.01f;

		[System.NonSerialized] private float currentSurfaceSmoothness;

		[System.NonSerialized]
		private Material blitMaterial;

		[System.NonSerialized]
		private NativeList<double3> cameraPositions;

		[System.NonSerialized]
		private NativeList<Triangle> topology;

		[System.NonSerialized] private NativeList<Triangle> triangles;
		[System.NonSerialized] private NativeList<Triangle> createDiffs;
		[System.NonSerialized] private NativeList<Triangle> deleteDiffs;
		[System.NonSerialized] private NativeList<Triangle> statusDiffs;

		[System.NonSerialized]
		private JobHandle updateHandle;

		[System.NonSerialized]
		private bool updateRunning;

		[System.NonSerialized]
		private List<Batch> batches = new List<Batch>();

		[System.NonSerialized] private Matrix4x4 shoreMatrixA;
		[System.NonSerialized] private Matrix4x4 shoreMatrixB;
		[System.NonSerialized] private float     shoreBlend = -1.0f;

		[System.NonSerialized] private Matrix4x4 microMatrixA;
		[System.NonSerialized] private Matrix4x4 microMatrixB;
		[System.NonSerialized] private float     microBlend = -1.0f;
		[System.NonSerialized] private float     microPosition;

		[System.NonSerialized] private Matrix4x4 macroMatrixA;
		[System.NonSerialized] private Matrix4x4 macroMatrixB;
		[System.NonSerialized] private float     macroBlend = -1.0f;
		[System.NonSerialized] private float     macroPosition;

		[System.NonSerialized] private Matrix4x4 causticsMatrixA;
		[System.NonSerialized] private Matrix4x4 causticsMatrixB;
		[System.NonSerialized] private float     causticsBlend = -1.0f;
		[System.NonSerialized] private float     causticsPosition;

		[System.NonSerialized]
		private Dictionary<TriangleHash, Batch> triangleBatches = new Dictionary<TriangleHash, Batch>();

		private static LinkedList<SgtOcean> AllOceans = new LinkedList<SgtOcean>();

		[System.NonSerialized] private LinkedListNode<SgtOcean> node;

		private static readonly int _SGT_CausticsDirection  = Shader.PropertyToID("_SGT_CausticsDirection");
		private static readonly int _SGT_FadeOpacity        = Shader.PropertyToID("_SGT_FadeOpacity");
		private static readonly int _SGT_FadeDensity        = Shader.PropertyToID("_SGT_FadeDensity");
		private static readonly int _SGT_UnderwaterDistance = Shader.PropertyToID("_SGT_UnderwaterDistance");
		private static readonly int _SGT_WaveInvAmplitude   = Shader.PropertyToID("_SGT_WaveInvAmplitude");

		private static readonly int _SGT_SurfaceColor             = Shader.PropertyToID("_SGT_SurfaceColor");
		private static readonly int _SGT_SurfaceScattering        = Shader.PropertyToID("_SGT_SurfaceScattering");
		private static readonly int _SGT_SurfaceDensity           = Shader.PropertyToID("_SGT_SurfaceDensity");
		private static readonly int _SGT_SurfaceSmoothness        = Shader.PropertyToID("_SGT_SurfaceSmoothness");
		
		private static readonly int _SGT_MicroTexture  = Shader.PropertyToID("_SGT_MicroTexture");
		private static readonly int _SGT_MicroMatrixA  = Shader.PropertyToID("_SGT_MicroMatrixA");
		private static readonly int _SGT_MicroMatrixB  = Shader.PropertyToID("_SGT_MicroMatrixB");
		private static readonly int _SGT_MicroBlend    = Shader.PropertyToID("_SGT_MicroBlend");
		private static readonly int _SGT_MicroData     = Shader.PropertyToID("_SGT_MicroData");
		
		private static readonly int _SGT_MacroTexture = Shader.PropertyToID("_SGT_MacroTexture");
		private static readonly int _SGT_MacroMatrixA = Shader.PropertyToID("_SGT_MacroMatrixA");
		private static readonly int _SGT_MacroMatrixB = Shader.PropertyToID("_SGT_MacroMatrixB");
		private static readonly int _SGT_MacroBlend   = Shader.PropertyToID("_SGT_MacroBlend");
		private static readonly int _SGT_MacroData    = Shader.PropertyToID("_SGT_MacroData");

		private static readonly int _SGT_ShoreMatrixA   = Shader.PropertyToID("_SGT_ShoreMatrixA");
		private static readonly int _SGT_ShoreMatrixB   = Shader.PropertyToID("_SGT_ShoreMatrixB");
		private static readonly int _SGT_ShoreBlend     = Shader.PropertyToID("_SGT_ShoreBlend");

		private static readonly int _SGT_CausticsMatrixA   = Shader.PropertyToID("_SGT_CausticsMatrixA");
		private static readonly int _SGT_CausticsMatrixB   = Shader.PropertyToID("_SGT_CausticsMatrixB");
		private static readonly int _SGT_CausticsBlend     = Shader.PropertyToID("_SGT_CausticsBlend");

		private static readonly int _SGT_WavesMatrixA   = Shader.PropertyToID("_SGT_WavesMatrixA");
		private static readonly int _SGT_WavesMatrixB   = Shader.PropertyToID("_SGT_WavesMatrixB");
		private static readonly int _SGT_WavesBlend     = Shader.PropertyToID("_SGT_WavesBlend");

		private static readonly int _SGT_SphereData   = Shader.PropertyToID("_SGT_SphereData");
		private static readonly int _SGT_PlaneData    = Shader.PropertyToID("_SGT_PlaneData");

		protected static readonly int _SGT_CloudTex     = Shader.PropertyToID("_SGT_CloudTex");
		protected static readonly int _SGT_CloudMatrix  = Shader.PropertyToID("_SGT_CloudMatrix");
		protected static readonly int _SGT_CloudOpacity = Shader.PropertyToID("_SGT_CloudOpacity");
		protected static readonly int _SGT_UnderwaterShadowRange = Shader.PropertyToID("_SGT_UnderwaterShadowRange");

		private static readonly int _SGT_ObjectToWorld         = Shader.PropertyToID("_SGT_ObjectToWorld");
		private static readonly int _SGT_WorldToObject         = Shader.PropertyToID("_SGT_WorldToObject");

		protected static readonly int _SGT_OceanDistance       = Shader.PropertyToID("_SGT_OceanDistance");
		protected static readonly int _SGT_OceanDensity        = Shader.PropertyToID("_SGT_OceanDensity");
		protected static readonly int _SGT_OceanHeight         = Shader.PropertyToID("_SGT_OceanHeight");
		protected static readonly int _SGT_OceanLightDirection = Shader.PropertyToID("_SGT_OceanLightDirection");
		protected static readonly int _SGT_OceanLightColor     = Shader.PropertyToID("_SGT_OceanLightColor");
		protected static readonly int _SGT_OceanColor          = Shader.PropertyToID("_SGT_OceanColor");
		protected static readonly int _SGT_OceanMinimum        = Shader.PropertyToID("_SGT_OceanMinimum");
		protected static readonly int _SGT_OceanSmoothness     = Shader.PropertyToID("_SGT_OceanSmoothness");
		protected static readonly int _SGT_OceanRadius         = Shader.PropertyToID("_SGT_OceanRadius");

		
		private static readonly int _SGT_NoiseTex       = Shader.PropertyToID("_SGT_NoiseTex");

		private static readonly int _SGT_Offset       = Shader.PropertyToID("_SGT_Offset");
		private static readonly int _SGT_Radius       = Shader.PropertyToID("_SGT_Radius");

		private static readonly int _SGT_Origins        = Shader.PropertyToID("_SGT_Origins");
		private static readonly int _SGT_PositionsA     = Shader.PropertyToID("_SGT_PositionsA");
		private static readonly int _SGT_PositionsB     = Shader.PropertyToID("_SGT_PositionsB");
		private static readonly int _SGT_PositionsC     = Shader.PropertyToID("_SGT_PositionsC");

		private static readonly int _SGT_World2View           = Shader.PropertyToID("_SGT_World2View");
		private static readonly int _SGT_WCam                 = Shader.PropertyToID("_SGT_WCam");

		public Texture3D NoiseTex
		{
			get
			{
				if (noiseTexCreated == false)
				{
					noiseTex = new Texture3D(16, 16, 16, TextureFormat.R8, false);
					noiseTex.hideFlags = HideFlags.DontSave;
					noiseTex.wrapMode  = TextureWrapMode.Repeat;

					var pixels = new NativeArray<byte>(noiseTex.width * noiseTex.height * noiseTex.depth, Allocator.Temp);
					var rng    = new Unity.Mathematics.Random(1337);

					for (var i = 0; i < pixels.Length; i++)
					{
						pixels[i] = (byte)rng.NextInt(0, 256);
					}

					noiseTex.SetPixelData(pixels, 0);
					noiseTex.Apply(false, true);

					noiseTexCreated = true;
				}

				return noiseTex;
			}
		}

		public float LandscapeOpacity
		{
			get
			{
				return landscapeOpacity;
			}
		}

		public float CurrentSurfaceSmoothness
		{
			get
			{
				return currentSurfaceSmoothness;
			}
		}

		/// <summary>This method returns the nearest <b>SgtOcean</b> to the specified world point.</summary>
		public static SgtOcean FindNearest(Vector3 worldPosition)
		{
			var bestDistance = float.PositiveInfinity;
			var bestInstance = default(SgtOcean);

			foreach (var ocean in AllOceans)
			{
				var distance = math.abs(ocean.CalculateWorldAltitude(worldPosition));

				if (distance < bestDistance)
				{
					bestDistance = distance;
					bestInstance = ocean;
				}
			}

			return bestInstance;
		}

		public bool HasChild(CwChild child)
		{
			return false;
		}

		public void RandomizeHue()
		{
			var h = 0.0f;
			var s = 0.0f;
			var v = 0.0f;

			Color.RGBToHSV(surfaceColor, out h, out s, out v);

			var newH = UnityEngine.Random.value;

			surfaceColor = Color.HSVToRGB(newH, s, v);
		}

		public void ApplyCloudShadowSettings(MaterialPropertyBlock properties)
		{
			if (cloud != null)
			{
				cloud.ApplyShadowSettings(properties);

				properties.SetFloat(_SGT_UnderwaterShadowRange, underwaterShadowRange);
			}
		}

		/// <summary>This will tell you the +- altitude of the input worldPoint relative to the surface of the ocean without displacement.</summary>
		public float CalculateWorldAltitude(Vector3 worldPoint)
		{
			var localPoint   = transform.InverseTransformPoint(worldPoint);
			var localSurface = localPoint.normalized * radius;
			var worldSurface = transform.TransformPoint(localSurface);

			return Vector3.Distance(worldPoint, worldSurface) * Mathf.Sign(localPoint.magnitude - radius);
		}

		public static SgtOcean Create(int layer = 0, Transform parent = null)
		{
			return Create(layer, parent, Vector3.zero, Quaternion.identity, Vector3.one);
		}

		public static SgtOcean Create(int layer, Transform parent, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
		{
			return CwHelper.CreateGameObject("Ocean", layer, parent, localPosition, localRotation, localScale).AddComponent<SgtOcean>();
		}

		protected override void OnEnable()
		{
			node = AllOceans.AddLast(this);

			base.OnEnable();

			topology            = new NativeList<Triangle>(8, AllocatorManager.Persistent);
			cameraPositions     = new NativeList<double3>(1, AllocatorManager.Persistent);

			triangles   = new NativeList<Triangle>(1024, AllocatorManager.Persistent);
			createDiffs = new NativeList<Triangle>(1024, AllocatorManager.Persistent);
			deleteDiffs = new NativeList<Triangle>(1024, AllocatorManager.Persistent);
			statusDiffs = new NativeList<Triangle>(1024, AllocatorManager.Persistent);

			if (properties == null)
			{
				properties = new MaterialPropertyBlock();
			}

			if (sky != null)
			{
				sky.OnPreDraw += HandlePreDraw;
			}

			InitializeWaves();
		}

		protected override void OnDisable()
		{
			base.OnDisable();

			if (sky != null)
			{
				sky.OnPreDraw -= HandlePreDraw;
			}

			if (updateRunning == true)
			{
				updateHandle.Complete();
				updateRunning = false;
			}

			topology.Dispose();

			cameraPositions.Dispose();

			triangles.Dispose();
			
			createDiffs.Dispose();
			deleteDiffs.Dispose();
			statusDiffs.Dispose();

			foreach (var batch in batches) { Batch.Pool.Push(batch); } batches.Clear();

			triangleBatches.Clear();

			DisposeWaves();

			AllOceans.Remove(node); node = null;
		}

		[System.NonSerialized]
		private Vector3 expectedPosition;

		private void UpdatePosition()
		{
			var newPosition = transform.position;

			if (newPosition != expectedPosition)
			{
				var deltaPos = expectedPosition - newPosition;
				var deltaMat = Matrix4x4.Translate(deltaPos);

				expectedPosition = newPosition;

				microMatrixA *= deltaMat;
				microMatrixB *= deltaMat;

				shoreMatrixA *= deltaMat;
				shoreMatrixB *= deltaMat;

				macroMatrixA *= deltaMat;
				macroMatrixB *= deltaMat;

				causticsMatrixA *= deltaMat;
				causticsMatrixB *= deltaMat;

				wavePendingDelta -= deltaPos;
			}
		}

		private void ScheduleUpdate(float detail)
		{
			cameraPositions.Clear();

			if (observer != null)
			{
				cameraPositions.Add((float3)transform.InverseTransformPoint(observer.transform.position));
			}

			if (cameraPositions.Length == 0 && Camera.main != null)
			{
				cameraPositions.Add((float3)transform.InverseTransformPoint(Camera.main.transform.position));
			}

			var topologyJob = new CreateTopologyJob_Sphere();

			topologyJob.Radius   = radius;
			topologyJob.Topology = topology;

			updateHandle = topologyJob.Schedule();

			var trianglesJob = new UpdateTrianglesJob();

			trianglesJob.Topology        = topology;
			trianglesJob.CameraPositions = cameraPositions;
			trianglesJob.CameraDetailSq  = 1.0f / (detail * detail);
			trianglesJob.Deform          = DeformType.Sphere;
			trianglesJob.Radius          = radius;
			trianglesJob.MaxDepth        = 60;
			trianglesJob.MaxSteps        = 100;

			trianglesJob.Triangles   = triangles;
			trianglesJob.CreateDiffs = createDiffs;
			trianglesJob.DeleteDiffs = deleteDiffs;
			trianglesJob.StatusDiffs = statusDiffs;

			updateHandle  = trianglesJob.Schedule(updateHandle);
			updateRunning = true;
		}

		private void CompleteAndRebuildBatches()
		{
			updateHandle.Complete();

			updateRunning = false;

			BuildBatches(batches, triangleBatches);
		}

		private void BuildBatches(List<Batch> batches, Dictionary<TriangleHash, Batch> triangleBatches)
		{
			foreach (var triangle in deleteDiffs)
			{
				HideVisual(triangle);
			}

			deleteDiffs.Clear();

			foreach (var triangle in createDiffs)
			{
				if (triangle.Split == false)
				{
					ShowVisual(triangle);
				}
			}

			createDiffs.Clear();

			foreach (var triangle in statusDiffs)
			{
				if (triangle.Split == true)
				{
					HideVisual(triangle);
				}
				else
				{
					ShowVisual(triangle);
				}
			}

			statusDiffs.Clear();
		}

		private void ShowVisual(Triangle triangle)
		{
			var batch = GetBatch(batches);

			batch.AddTriangle(triangle, radius);

			triangleBatches.Add(triangle.Hash, batch);
		}

		private void HideVisual(Triangle triangle)
		{
			var batch = default(Batch);

			if (triangleBatches.Remove(triangle.Hash, out batch) == true)
			{
				batch.RemoveTriangle(triangle.Hash);

				if (batch.Count == 0)
				{
					batches.Remove(batch);
				}
			}
		}

		private static Batch GetBatch(List<Batch> batches)
		{
			foreach (var batch in batches)
			{
				if (batch.Count < 128)
				{
					return batch;
				}
			}

			var newBatch = new Batch();

			batches.Add(newBatch);

			return newBatch;
		}

		private Matrix4x4 RemoveTranslation(Matrix4x4 matrix)
		{
			matrix.m03 = 0;
			matrix.m13 = 0;
			matrix.m23 = 0;

			return matrix;
		}

		private Matrix4x4 GenerateWaterMatrix(Camera finalCamera, float scale, float degrees)
		{
			return Matrix4x4.Rotate(Quaternion.Euler(0.0f, 0.0f, degrees)) * Matrix4x4.LookAt(transform.position, finalCamera.transform.position, Vector3.up).inverse * Matrix4x4.Scale(Vector3.one / scale);
		}

		private Vector2 DirectionToEquirectangular(Vector3 dir)
		{
			var d = dir.normalized;
			var u = Mathf.Atan2(d.z, d.x) / (2.0f * Mathf.PI) + 0.5f;
			var v = Mathf.Asin(d.y) / Mathf.PI + 0.5f;

			return new Vector2(u, v);
		}

		private void HandlePreDraw(ref Material drawMaterial, MaterialPropertyBlock drawProperties, Bounds bounds, Camera finalCamera)
		{
			if (material == null) return;
			if (fade == true && landscapeOpacity >= 1.0f) return; // Ocean is fully faded out

			//var finalCamera = observer != null ? observer : Camera.main;
			//if (finalCamera == null) return;

			// Override the material with the ocean material
			drawMaterial = material;

			// Apply ocean properties directly to the sky's MaterialPropertyBlock
			var altitude    = CalculateWorldAltitude(finalCamera.transform.position);
			var fadeOpacity = fade == true ? Mathf.InverseLerp(fadeDistance, 0.0f, altitude) : 1.0f;

			var o2w   = RemoveTranslation(transform.localToWorldMatrix);
			var w2o   = RemoveTranslation(transform.worldToLocalMatrix);

			var center     = (double3)(float3)transform.position;
			var delta      = (double3)(float3)finalCamera.transform.position - center;
			var cameraN    = math.normalize(delta);
			var cameraW    = -math.dot(cameraN, center + cameraN * radius);

			drawProperties.SetMatrix(_SGT_ObjectToWorld, o2w);
			drawProperties.SetMatrix(_SGT_WorldToObject, w2o);

			drawProperties.SetFloat(_SGT_SurfaceSmoothness, currentSurfaceSmoothness);
			drawProperties.SetColor(_SGT_SurfaceColor, surfaceColor);
			drawProperties.SetColor(_SGT_SurfaceScattering, surfaceScattering * surfaceScattering.a);
			drawProperties.SetFloat(_SGT_SurfaceDensity, surfaceDensity);

			drawProperties.SetVector(_SGT_FadeOpacity, new Vector2(fadeOpacity, 1.0f - landscapeOpacity));
			drawProperties.SetFloat(_SGT_FadeDensity, fadeDeepDensity);
			drawProperties.SetFloat(_SGT_UnderwaterDistance, 5.0f / math.max(surfaceDensity, 0.1f));
			drawProperties.SetFloat(_SGT_WaveInvAmplitude, 1.0f / math.max(wavesAmplitude, 1.0f));

			WriteWaves(drawProperties);
			drawProperties.SetTexture(_SGT_NoiseTex, NoiseTex);

			drawProperties.SetMatrix(_SGT_ShoreMatrixA, shoreMatrixA);
			drawProperties.SetMatrix(_SGT_ShoreMatrixB, shoreMatrixB);
			drawProperties.SetFloat(_SGT_ShoreBlend, shoreBlend);

			drawProperties.SetMatrix(_SGT_CausticsMatrixA, causticsMatrixA);
			drawProperties.SetMatrix(_SGT_CausticsMatrixB, causticsMatrixB);
			drawProperties.SetFloat(_SGT_CausticsBlend, causticsBlend);
			drawProperties.SetVector(_SGT_CausticsDirection, GetLightDirection());

			drawProperties.SetVector(_SGT_SphereData, new Vector4((float)center.x, (float)center.y, (float)center.z, radius));
			drawProperties.SetVector(_SGT_PlaneData, new Vector4((float)cameraN.x, (float)cameraN.y, (float)cameraN.z, (float)cameraW));

			if (cloud != null)
			{
				var waves           = CalculateWaves(finalCamera.transform.position);
				var submersionRatio = 0.0f;

				if (waves.HeightAboveWater < 0.0f)
				{
					var currentDepthBelowWaves = -waves.HeightAboveWater;

					submersionRatio = Mathf.Clamp01(currentDepthBelowWaves / math.max(0.001f, underwaterRadius));
				}

				cloud.UnderwaterRatio = submersionRatio;
			}
		}

		protected virtual void LateUpdate()
		{
			UpdatePosition();

			UpdateWaves();

			if (batches.Count > 0)
			{
				if (updateRunning == false)
				{
					ScheduleUpdate(detail);
				}

				if (updateHandle.IsCompleted == true)
				{
					CompleteAndRebuildBatches();
				}
			}
			else
			{
				ScheduleUpdate(0.01f);
				CompleteAndRebuildBatches();
			}

			microPosition += microSpeed * Time.deltaTime;

			macroPosition += macroSpeed * Time.deltaTime;

			causticsPosition += causticsSpeed * Time.deltaTime;

			var finalCamera = observer != null ? observer : Camera.main;

			if (sky != null && finalCamera != null)
			{
				var altitude    = CalculateWorldAltitude(finalCamera.transform.position);
				var targetLO    = fade == true && altitude >= fadeDistance ? 1.0f : 0.0f;

				if (fade == true && landscapeOpacity >= 0.0f)
				{
					landscapeOpacity = Mathf.MoveTowards(landscapeOpacity, targetLO, Time.deltaTime);
				}
				else
				{
					landscapeOpacity = targetLO;
				}

				var center     = (double3)(float3)transform.position;
				var delta      = (double3)(float3)finalCamera.transform.position - center;
				var cameraN    = math.normalize(delta);
				
				if (microBlend >= 0.0f)
				{
					microBlend += Time.deltaTime * microSpeed;

					if (microBlend > 1.0f)
					{
						microMatrixA = microMatrixB;
						microMatrixB = GenerateWaterMatrix(finalCamera, microScale, UnityEngine.Random.value * 360.0f);
						microBlend  %= 1.0f;
					}
				}
				else
				{
					microMatrixA = GenerateWaterMatrix(finalCamera, microScale, UnityEngine.Random.value * 360.0f);
					microMatrixB = GenerateWaterMatrix(finalCamera, microScale, UnityEngine.Random.value * 360.0f);
					microBlend   = 0.0f;
				}

				if (macroBlend >= 0.0f)
				{
					macroBlend += Time.deltaTime * macroSpeed;

					if (macroBlend > 1.0f)
					{
						macroMatrixA = macroMatrixB;
						macroMatrixB = GenerateWaterMatrix(finalCamera, macroScale, UnityEngine.Random.value * 360.0f);
						macroBlend  %= 1.0f;
					}
				}
				else
				{
					macroMatrixA = GenerateWaterMatrix(finalCamera, macroScale, UnityEngine.Random.value * 360.0f);
					macroMatrixB = GenerateWaterMatrix(finalCamera, macroScale, UnityEngine.Random.value * 360.0f);
					macroBlend   = 0.0f;
				}

				if (shoreBlend >= 0.0f)
				{
					shoreBlend += Time.deltaTime * shoreSpeed;

					if (shoreBlend > 1.0f)
					{
						shoreMatrixA = shoreMatrixB;
						shoreMatrixB = GenerateWaterMatrix(finalCamera, shoreScale, UnityEngine.Random.value * 360.0f);
						shoreBlend  %= 1.0f;
					}
				}
				else
				{
					shoreMatrixA = GenerateWaterMatrix(finalCamera, shoreScale, UnityEngine.Random.value * 360.0f);
					shoreMatrixB = GenerateWaterMatrix(finalCamera, shoreScale, UnityEngine.Random.value * 360.0f);
					shoreBlend   = 0.0f;
				}

				if (causticsBlend >= 0.0f)
				{
					causticsBlend += Time.deltaTime;

					if (causticsBlend > 1.0f)
					{
						causticsMatrixA = causticsMatrixB;
						causticsMatrixB = GenerateWaterMatrix(finalCamera, causticsScale, UnityEngine.Random.value * 360.0f);
						causticsBlend  %= 1.0f;
					}
				}
				else
				{
					causticsMatrixA = GenerateWaterMatrix(finalCamera, causticsScale, UnityEngine.Random.value * 360.0f);
					causticsMatrixB = GenerateWaterMatrix(finalCamera, causticsScale, UnityEngine.Random.value * 360.0f);
					causticsBlend   = 0.0f;
				}

				currentSurfaceSmoothness = surfaceSmoothness;

				if (cloud != null && surfaceSmoothnessOcclusion > 0.0f)
				{
					// Direction from the surface toward the sun
					var sunDir       = sky.LightDirection.normalized;
					var cloudOpacity = cloud.CalculateApproximateOpacity(finalCamera.transform.position, sunDir, surfaceSmoothnessOcclusion, surfaceSmoothnessLod);
    
					// Dim the PBR smoothness based on the accumulated cloud opacity
					currentSurfaceSmoothness = Mathf.Lerp(surfaceSmoothness, 0.0f, cloudOpacity);
				}
			}
		}

		public override void CalculateSortDistance(Vector3 worldPoint)
		{
			sortDistance = Vector3.Distance(worldPoint, transform.position);
		}

		public override void RenderOceanBuffer(SgtVolumeManager manager, Camera finalCamera, int frame, Vector2Int renderSize)
		{
			if (blitMaterial == null)
			{
				blitMaterial = CwHelper.CreateTempMaterial("SgtOcean", "Hidden/SgtOcean");
			}

			blitMaterial.SetVector(_SGT_WCam, finalCamera.transform.position);

			blitMaterial.EnableKeyword("_SGT_SHAPE_SPHERE");

			if (wavesAmplitude > 0.0f)
			{
				blitMaterial.EnableKeyword("_SGT_DISPLACEMENT_ON");
			}
			else
			{
				blitMaterial.DisableKeyword("_SGT_DISPLACEMENT_ON");
			}

			if (wavesQuality == 1)
			{
				blitMaterial.EnableKeyword("_SGT_WAVES_1");
				blitMaterial.DisableKeyword("_SGT_WAVES_2");
				blitMaterial.DisableKeyword("_SGT_WAVES_3");
			}
			else if (wavesQuality == 2)
			{
				blitMaterial.DisableKeyword("_SGT_WAVES_1");
				blitMaterial.EnableKeyword("_SGT_WAVES_2");
				blitMaterial.DisableKeyword("_SGT_WAVES_3");
			}
			else if (wavesQuality == 3)
			{
				blitMaterial.DisableKeyword("_SGT_WAVES_1");
				blitMaterial.DisableKeyword("_SGT_WAVES_2");
				blitMaterial.EnableKeyword("_SGT_WAVES_3");
			}

			if (micro == true && microTexture != null)
			{
				blitMaterial.SetTexture(_SGT_MicroTexture, microTexture);
				blitMaterial.SetMatrix(_SGT_MicroMatrixA, microMatrixA);
				blitMaterial.SetMatrix(_SGT_MicroMatrixB, microMatrixB);
				blitMaterial.SetFloat(_SGT_MicroBlend, microBlend);
				blitMaterial.SetVector(_SGT_MicroData, new Vector4(microStrength, microPosition * Mathf.PI, 0.0f, 0.0f));
				blitMaterial.SetTexture(_SGT_NoiseTex, NoiseTex);

				blitMaterial.EnableKeyword("_SGT_MICRO_ON");
			}
			else
			{
				blitMaterial.DisableKeyword("_SGT_MMICRO_ON");
			}

			if (macro == true && macroTexture != null)
			{
				blitMaterial.SetTexture(_SGT_MacroTexture, macroTexture);
				blitMaterial.SetMatrix(_SGT_MacroMatrixA, macroMatrixA);
				blitMaterial.SetMatrix(_SGT_MacroMatrixB, macroMatrixB);
				blitMaterial.SetFloat(_SGT_MacroBlend, macroBlend);
				blitMaterial.SetVector(_SGT_MacroData, new Vector4(macroStrength, macroPosition * Mathf.PI, 0.0f, 0.0f));
				blitMaterial.SetTexture(_SGT_NoiseTex, NoiseTex);

				blitMaterial.EnableKeyword("_SGT_MACRO_ON");
			}
			else
			{
				blitMaterial.DisableKeyword("_SGT_MACRO_ON");
			}

			var distance = Vector3.Distance(finalCamera.transform.position, transform.position);
			var altitude = distance - radius;

			if (Mathf.Abs(altitude) > radius * 0.001f)
			{
				blitMaterial.EnableKeyword("_SGT_SNAP_ON");
			}
			else
			{
				blitMaterial.DisableKeyword("_SGT_SNAP_ON");
			}
			
			blitMaterial.enableInstancing = true;

			var o2w = RemoveTranslation(transform.localToWorldMatrix);
			var w2o = RemoveTranslation(transform.worldToLocalMatrix);
			var rot = Quaternion.Inverse(transform.rotation) * transform.position;

			properties.SetMatrix(_SGT_ObjectToWorld, o2w);
			properties.SetMatrix(_SGT_WorldToObject, w2o);
			properties.SetVector(_SGT_Offset, rot);
			properties.SetFloat(_SGT_Radius, radius);
			properties.SetVector(_SGT_FadeOpacity, new Vector2(1.0f, landscapeOpacity));

			WriteWaves(properties);

			foreach (var batch in batches)
			{
				properties.SetVectorArray(_SGT_Origins, batch.PositionsO);
				properties.SetVectorArray(_SGT_PositionsA, batch.PositionsA);
				properties.SetVectorArray(_SGT_PositionsB, batch.PositionsB);
				properties.SetVectorArray(_SGT_PositionsC, batch.PositionsC);

				SgtVolumeCamera.AddDrawMeshInstancedProcedural(GetMesh(), 0, blitMaterial, 0, batch.Count, properties);
			}
		}

		private Vector3 GetLightDirection()
		{
			var finalLight = causticsLight;

			if (finalLight == null)
			{
				var lights = SgtLight.Find(-1, transform.position);

				if (lights.Count > 0)
				{
					finalLight = lights[0];
				}
			}

			if (finalLight != null && finalLight.isActiveAndEnabled == true)
			{
				if (finalLight.CachedLight.type == LightType.Point || (finalLight.CachedLight.type == LightType.Directional && finalLight.TreatAsPoint == true))
				{
					return Vector3.Normalize(finalLight.CachedTransform.position - transform.position);
				}
				else
				{
					return finalLight.CachedTransform.forward;
				}
			}

			return Vector3.forward;
		}
	}
}

#if UNITY_EDITOR
namespace SpaceGraphicsToolkit.Ocean
{
	[UnityEditor.CanEditMultipleObjects]
	[UnityEditor.CustomEditor(typeof(SgtOcean))]
	public class SgtOcean_Editor : CwEditor
	{
		protected override void OnInspector()
		{
			SgtOcean tgt; SgtOcean[] tgts; GetTargets(out tgt, out tgts);

			var markForRebuild = false;
			var markParentForRebuild = false;

			BeginError(Any(tgts, t => t.Material == null));
				Draw("material", "The material used to render the fluid surface.\n\nNOTE: This must use the <b>SGT/Ocean</b> shader.");
			EndError();
			BeginError(Any(tgts, t => t.Sky == null));
				Draw("sky");
			EndError();
			Draw("radius", "The radius of the ocean.");
			Draw("detail", "The overall detail of the ocean relative to the camera distance. The higher you set this, the more triangles it will have.");
			Draw("fluidDensity", "The density of the fluid used for buoyancy in kg/m^3 (seawater = 1025).");
			Draw("cloud", "If you want cloud shadows to appear on the surface of the ocean, specify them here.");

			Separator();
			
			Draw("observer", "The camera rendering this ocean.\n\nNone/null = The GameObject with the <b>MainCamera</b> tag will be used.");
			Draw("cameraOffset", "This allows you to offset the camera distance in world space when rendering the ocean, giving you fine control over the render order.");
			BeginDisabled();
				var finalCamera = tgt.Observer != null ? tgt.Observer : Camera.main;
				if (finalCamera != null)
				{
					UnityEditor.EditorGUILayout.FloatField("Height Above Water", tgt.CalculateWaves(finalCamera.transform.position).HeightAboveWater);
				}
			EndDisabled();

			Separator();

			Draw("surfaceColor", "The color of the fluid when viewed from above the surface.");
			Draw("surfaceScattering", "The color of the sub surface scattering.");
			Draw("surfaceDensity", "The density of the water when viewed from above the surface.");
			Draw("surfaceSmoothness", "The PBR smoothness of the surface material.");
			BeginIndent();
				Draw("surfaceSmoothnessOcclusion", "This allows you to control how much the smoothness value can be reduced by clouds in the way.", "Occlusion");
				Draw("surfaceSmoothnessLod", "This allows you to control the LOD used by <b>SurfaceSmoothnessOcclusion</b>.", "LOD");
			EndIndent();

			Separator();

			Draw("underwaterRadius", "The inner radius threshold below which the camera is fully submerged underwater.");
			Draw("underwaterShadowRange", "The distance from the camera the cloud shadow calculations will use. This should approximately be the underwater fog distance (4.6 * density).");

			Separator();

			Draw("wavesAmplitude", "How high the waves can reach in meters.");
			Draw("wavesSteepness", "How choppy the waves are.");
			Draw("wavesSpeed", "How fast the waves animate.");
			Draw("wavesQuality", "The amount of combined wave layers.");

			Separator();

			Draw("windDirection", "The direction of the wind in world space.");
			Draw("windSpeed", "The speed of the wind in world space.");

			Separator();

			Draw("shoreScale", "The size of the shore texture in world space.");
			Draw("shoreSpeed", "The animation speed of the shore.");

			Separator();

			Draw("micro", "Apply small details to the ocean surface?");
			if (Any(tgts, t => t.Micro == true))
			{
				BeginIndent();
					BeginError(Any(tgts, t => t.MicroTexture == null));
						Draw("microTexture", "The dual normal map texture for detail.\n\nRG = 0..1 NormalA.xy\n\nBA = 0..1 NormalB.xy", "Texture");
					EndError();
					Draw("microSpeed", "The animation speed of the detail.", "Speed");
					Draw("microScale", "The size of the detail texture in world space.", "Scale");
					Draw("microStrength", "The detail texture strength will be multiplied by this.", "Strength");
				EndIndent();
			}

			Separator();

			Draw("macro", "Apply large details to the ocean surface?");
			if (Any(tgts, t => t.Macro == true))
			{
				BeginIndent();
					BeginError(Any(tgts, t => t.MacroTexture == null));
						Draw("macroTexture", "The dual normal map texture for detail.\n\nRG = 0..1 NormalA.xy\n\nBA = 0..1 NormalB.xy", "Texture");
					EndError();
					Draw("macroSpeed", "The animation speed of the detail.", "Speed");
					Draw("macroScale", "The size of the detail texture in world space.", "Scale");
					Draw("macroStrength", "The detail texture strength will be multiplied by this.", "Strength");
				EndIndent();
			}

			Separator();

			Draw("fade", "Ocean rendering can look bat at extreme distances, so fade it out.\n\nNOTE: If you enable this, your landscape should use the <b>OceanFade</b> setting.");
			if (Any(tgts, t => t.Fade == true))
			{
				BeginIndent();
					Draw("fadeDistance", "The ocean will completely disappear at this distance in world space.");
					Draw("fadeDeepDistance", "The deep ocean surface effect will be fully revealed at this distance in world space.", "Deep Distance");
					Draw("fadeDeepDensity", ref markParentForRebuild, "The deep ocean surface effect will have this density.", "Deep Density");
				EndIndent();
			}

			Separator();

			Draw("causticsLight", "The light source for the caustics.");
			Draw("causticsScale", "The size of the caustics texture in world space.");
			Draw("causticsSpeed", "The animation speed of the caustics.");

			if (markForRebuild == true)
			{
				//Each(tgts, t => t.reb);
			}

			if (markParentForRebuild == true)
			{
				foreach (var landscape in SgtLandscape.AllLandscapes)
				{
					if (landscape is SgtSphereLandscape)
					{
						var sphereLandscape = (SgtSphereLandscape)landscape;

						if (sphereLandscape.OceanFade != null && System.Array.IndexOf(tgts, sphereLandscape.OceanFade) >= 0)
						{
							sphereLandscape.MarkForRebuild();
						}
					}
				}
			}

			Separator();

			if (Button("Randomize Hue") == true)
			{
				Each(tgts, t => t.RandomizeHue(), true, true);
			}
		}

		[UnityEditor.MenuItem("GameObject/CW/Space Graphics Toolkit/Ocean", false, 10)]
		public static void CreateMenuItem()
		{
			var parent   = CwHelper.GetSelectedParent();
			var instance = SgtOcean.Create(parent != null ? parent.gameObject.layer : 0, parent);

			CwHelper.SelectAndPing(instance);
		}
	}
}
#endif