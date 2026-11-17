using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace SpaceGraphicsToolkit.Landscape
{
	/// <summary>This is the base class for all landscape types.</summary>
	[ExecuteInEditMode]
	[DefaultExecutionOrder(1000)]
	public abstract partial class SgtLandscape : MonoBehaviour
	{
		public enum DeformType
		{
			Default,
			Sphere
		}

		public struct TriangleHash : System.IEquatable<TriangleHash>
		{
			private double3x3 data;

			public TriangleHash(double3 a, double3 b, double3 c)
			{
				data.c0 = a;
				data.c1 = b;
				data.c2 = c;

				if (CompareVectors(data.c0, data.c1) > 0) Swap(ref data.c0, ref data.c1);
				if (CompareVectors(data.c0, data.c2) > 0) Swap(ref data.c0, ref data.c2);
				if (CompareVectors(data.c1, data.c2) > 0) Swap(ref data.c1, ref data.c2);
			}

			private static int CompareVectors(double3 a, double3 b)
			{
				if (a.x != b.x) return a.x < b.x ? -1 : 1;
				if (a.y != b.y) return a.y < b.y ? -1 : 1;
				if (a.z != b.z) return a.z < b.z ? -1 : 1;
				return 0;
			}

			private static void Swap(ref double3 a, ref double3 b)
			{
				var temp = a;
				a = b;
				b = temp;
			}

			public bool Equals(TriangleHash other)
			{
				return data.Equals(other.data);
			}

			public override bool Equals(object obj)
			{
				return obj is TriangleHash other && Equals(other);
			}

			public override int GetHashCode()
			{
				return data.GetHashCode();
			}
		}

		public struct Triangle
		{
			public double3 PositionA;
			public double3 PositionB;
			public double3 PositionC;

			public int          Depth;
			public bool         Split;
			public bool         Fixer;
			public TriangleHash Hash;

			public Triangle(double3 a, double3 b, double3 c, int depth, bool split, bool fixer)
			{
				PositionA  = a;
				PositionB  = b;
				PositionC  = c;
				Depth      = depth;
				Split      = split;
				Fixer      = fixer;
				Hash       = new TriangleHash(a, b, c);
			}

			public double3 Pivot1
			{
				get
				{
					var a = math.min(PositionA, PositionB);
					var b = math.max(PositionA, PositionB);
					return (a + b) * 0.5f;
				}
			}

			public double3 Pivot2
			{
				get
				{
					var a = math.min(PositionB, PositionC);
					var b = math.max(PositionB, PositionC);
					return (a + b) * 0.5f;
				}
			}

			public double3 Pivot3
			{
				get
				{
					var a = math.min(PositionC, PositionA);
					var b = math.max(PositionC, PositionA);
					return (a + b) * 0.5f;
				}
			}

			public double Size1
			{
				get
				{
					var a = math.min(PositionA, PositionB);
					var b = math.max(PositionA, PositionB);
					return math.length(a.xyz - b.xyz);
				}
			}

			public double Size2
			{
				get
				{
					var a = math.min(PositionB, PositionC);
					var b = math.max(PositionB, PositionC);
					return math.length(a.xyz - b.xyz);
				}
			}

			public double Size3
			{
				get
				{
					var a = math.min(PositionC, PositionA);
					var b = math.max(PositionC, PositionA);
					return math.length(a.xyz - b.xyz);
				}
			}
		}

		/// <summary>This allows you to control the LOD detail while in edit mode.</summary>
		public float EditorDetail { set { editorDetail = value; } get { return editorDetail; } } [SerializeField] [Range(1.0f, 20.0f)] private float editorDetail = 1.0f;

		/// <summary>When entering play mode or running the actual game, the landscape will initialize with this level of detail.
		/// NOTE: 0 means it will initialize with the minimum amount of triangles, and will generate very quickly.
		/// Higher values will make the planet immediately look better, but may cause the scene to freeze until it's generated.</summary>
		public float InitDetail { set { initDetail = value; } get { return initDetail; } } [SerializeField] [Range(0.0f, 20.0f)] private float initDetail;

		/// <summary>The overall detail of the landscape relative to the camera distance. The higher you set this, the more triangles it will have.</summary>
		public float Detail { set { detail = value; } get { return detail; } } [SerializeField] [Range(1.0f, 20.0f)] private float detail = 5.0f;

		/// <summary>This allows you to boost the detail of lower LOD stages, so the landscape detail doesn't go too low.</summary>
		public float BoostMultiplier { set { boostMultiplier = value; } get { return boostMultiplier; } } [SerializeField] [Range(1.0f, 3.0f)] private float boostMultiplier = 1.75f;

		/// <summary>The maximum LOD depth this landscape can have.</summary>
		public float MinimumTriangleSize { set { minimumTriangleSize = value; } get { return minimumTriangleSize; } } [SerializeField] private float minimumTriangleSize = 0.1f;

		/// <summary>The maximum amount of seconds we can budget for LOD.</summary>
		public float LodBudget { set { lodBudget = value; } get { return lodBudget; } } [SerializeField] private float lodBudget = 0.001f;

		/// <summary>The maximum LOD chunks that can be generated between each LOD change. For example, if your camera suddenly travels to the planet surface then many LOD chunks will need to be generated. If you set LOD Steps to a high value, then it will take a while for any changes to appear, whereas a low value means the landscape will constantly be updating to the final LOD state.</summary>
		public int LodSteps { set { lodSteps = value; } get { return lodSteps; } } [SerializeField] protected int lodSteps = 30;

		/// <summary>If you spawn objects on a massive planet, their positions can degrade from floating point precision. If you enable this, then they will spawn on a separate GameObject in the scene root.</summary>
		public bool UseSpawnAnchor { set { useSpawnAnchor = value; } get { return useSpawnAnchor; } } [SerializeField] private bool useSpawnAnchor;

		/// <summary>The landscape will be rendered using this Material.
		/// NOTE: This material must use a shader based on the SGT/Landscape shader.</summary>
		public Material Material { set { material = value; } get { return material; } } [SerializeField] private Material material;

		/// <summary>To support massive planets, the global detail/biome UV sizes must be baked into the mesh. This allows you to set up to 4 different tile sizes.</summary>
		public Vector4 GlobalSizes { set { globalSizes = value; } get { return globalSizes; } } [SerializeField] protected Vector4 globalSizes = new Vector4(1.0f, 100.0f, 10000.0f, 1000000.0f);

		public Vector4 GlobalSizesNormalized { get { return globalSizesNormalized; } } [SerializeField] protected Vector4 globalSizesNormalized;

		public Vector4 GlobalTiling { get { return globalTiling; } } [SerializeField] protected Vector4 globalTiling;

		public Vector4 GlobalTilingNormalized { get { return globalTilingNormalized; } } [SerializeField] protected Vector4 globalTilingNormalized;

		/// <summary>The landscape LOD will be based on these transform positions.
		/// None/null = The GameObject with the <b>MainCamera</b> tag will be used.</summary>
		public List<Transform> Observers { get { if (observers == null) observers = new List<Transform>(); return observers; } } [SerializeField] private List<Transform> observers;

		/// <summary>Should the landscape cast Unity built-in shadows?</summary>
		public bool CastShadows { set { castShadows = value; } get { return castShadows; } } [SerializeField] private bool castShadows = true;

		/// <summary>Should the landscape receive Unity's built-in shadows?</summary>
		public bool ReceiveShadows { set { receiveShadows = value; } get { return receiveShadows; } } [SerializeField] private bool receiveShadows = true;

		/// <summary>Scale the render bounds to be within range of the camera?</summary>
		public bool CompressBounds { set { compressBounds = value; } get { return compressBounds; } } [SerializeField] private bool compressBounds;

		[System.NonSerialized]
		private static readonly int PIXEL_WIDTH = 136 * 3;

		[System.NonSerialized]
		private static readonly int PIXEL_HEIGHT = 136;

		private static readonly int ATLAS_COLUMNS = 5;

		private static readonly int ATLAS_ROWS = 7; // 15

		private static readonly int FEATURE_CAPACITY = 256;
		private static readonly int HOLES_CAPACITY   = 64;

		[System.NonSerialized] protected NativeList<Triangle> topology;
		[System.NonSerialized] protected NativeList<double3>  cameraPositions;
		[System.NonSerialized] protected NativeList<Triangle> triangles;

		public int ActiveHoleCount { get { return activeHoleCount; } } [System.NonSerialized] private int activeHoleCount;

		public Vector4[] ActiveHoleDatas { get { return activeHoleDatas; } } [System.NonSerialized] private Vector4[] activeHoleDatas;

		public Matrix4x4[] ActiveHoleMatrices { get { return activeHoleMatrices; } } [System.NonSerialized] private Matrix4x4[] activeHoleMatrices;
		
		public NativeArray<float4> ActiveHoleDatasNA { get { return activeHoleDatasNA; } } [System.NonSerialized] private NativeArray<float4> activeHoleDatasNA;

		public NativeArray<double4x4> ActiveHoleMatricesNA { get { return activeHoleMatricesNA; } } [System.NonSerialized] private NativeArray<double4x4> activeHoleMatricesNA;

		[System.NonSerialized] protected NativeList<Triangle> createDiffs;
		[System.NonSerialized] protected NativeList<Triangle> deleteDiffs;
		[System.NonSerialized] protected NativeList<Triangle> statusDiffs;

		[System.NonSerialized] protected Material blitMaterial;

		public static LinkedList<SgtLandscape> AllLandscapes = new LinkedList<SgtLandscape>();

		private LinkedListNode<SgtLandscape> node;

		[System.NonSerialized] private List<SgtLandscapeCave> caves = new List<SgtLandscapeCave>();

		[System.NonSerialized] private bool markForRebuild;

		[System.NonSerialized] private System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();

		[System.NonSerialized] protected PendingUpdate pendingUpdate = new PendingUpdate();

		[System.NonSerialized] private List<PendingTriangle> pendingTriangles = new List<PendingTriangle>();

		[System.NonSerialized] protected PendingPoints cameraPoints;

		[System.NonSerialized] private Transform spawnAnchor;

		public double3 SpawnOffset { get { return spawnOffset; } } [System.NonSerialized] private double3 spawnOffset;

		[System.NonSerialized] protected List<SgtLandscapeFeature> features = new List<SgtLandscapeFeature>();

		public event System.Action<Visual, PendingTriangle> OnAddVisual;

		public event System.Action<Visual> OnRemoveVisual;

		public event System.Action<Visual> OnShowVisual;

		public event System.Action<Visual> OnHideVisual;

		public event System.Action OnDispose;

		public event System.Action OnSpawnOffsetChanged;

		private struct FeatureData
		{
			public Vector4 A, B, C, D, E, Layer;
			public Matrix4x4 Matrix;
		}

		[System.NonSerialized] private List<FeatureData> tempGlobalDetails = new List<FeatureData>();
		[System.NonSerialized] private List<FeatureData> tempLocalDetails = new List<FeatureData>();
		[System.NonSerialized] private List<FeatureData> tempGlobalFlattens = new List<FeatureData>();
		[System.NonSerialized] private List<FeatureData> tempLocalFlattens = new List<FeatureData>();
		[System.NonSerialized] private List<FeatureData> tempGlobalColors = new List<FeatureData>();
		[System.NonSerialized] private List<FeatureData> tempLocalColors = new List<FeatureData>();
		[System.NonSerialized] private List<FeatureData> tempGlobalTextures = new List<FeatureData>();
		[System.NonSerialized] private List<FeatureData> tempLocalTextures = new List<FeatureData>();

		[System.NonSerialized] private Vector4[] featureDataA  = new Vector4[FEATURE_CAPACITY];
		[System.NonSerialized] private Vector4[] featureDataB  = new Vector4[FEATURE_CAPACITY];
		[System.NonSerialized] private Vector4[] featureDataC  = new Vector4[FEATURE_CAPACITY];
		[System.NonSerialized] private Vector4[] featureDataD  = new Vector4[FEATURE_CAPACITY];
		[System.NonSerialized] private Vector4[] featureDataE  = new Vector4[FEATURE_CAPACITY];
		[System.NonSerialized] private Vector4[] featureLayer  = new Vector4[FEATURE_CAPACITY];
		[System.NonSerialized] private Matrix4x4[] featureMatrix = new Matrix4x4[FEATURE_CAPACITY];

		[System.NonSerialized] private Vector4 featureRangeDetail;
		[System.NonSerialized] private Vector4 featureRangeFlatten;
		[System.NonSerialized] private Vector4 featureRangeColor;
		[System.NonSerialized] private Vector4 featureRangeTexture;

		protected static readonly int _CwMatrix        = Shader.PropertyToID("_CwMatrix");
		protected static readonly int _CwPositionA     = Shader.PropertyToID("_CwPositionA");
		protected static readonly int _CwPositionB     = Shader.PropertyToID("_CwPositionB");
		protected static readonly int _CwPositionC     = Shader.PropertyToID("_CwPositionC");
		protected static readonly int _CwCoordX        = Shader.PropertyToID("_CwCoordX");
		protected static readonly int _CwCoordY        = Shader.PropertyToID("_CwCoordY");
		protected static readonly int _CwCoordZ        = Shader.PropertyToID("_CwCoordZ");
		protected static readonly int _CwCoordW        = Shader.PropertyToID("_CwCoordW");
		protected static readonly int _CwGlobalTiling  = Shader.PropertyToID("_CwGlobalTiling");
		protected static readonly int _CwDepth         = Shader.PropertyToID("_CwDepth");
		protected static readonly int _CwRadius        = Shader.PropertyToID("_CwRadius");
		protected static readonly int _CwSquareSize    = Shader.PropertyToID("_CwSquareSize");
		protected static readonly int _CwAlbedo        = Shader.PropertyToID("_CwAlbedo");
		protected static readonly int _CwAlbedoSize    = Shader.PropertyToID("_CwAlbedoSize");
		protected static readonly int _CwTopology      = Shader.PropertyToID("_CwTopology");
		protected static readonly int _CwTopologySize  = Shader.PropertyToID("_CwTopologySize");
		protected static readonly int _CwTopologyData  = Shader.PropertyToID("_CwTopologyData");
		protected static readonly int _CwTopologyRange = Shader.PropertyToID("_CwTopologyRange");

		protected static readonly int _SGT_OceanFade           = Shader.PropertyToID("_SGT_OceanFade");
		protected static readonly int _SGT_OceanDensity        = Shader.PropertyToID("_SGT_OceanDensity");
		protected static readonly int _SGT_OceanHeight         = Shader.PropertyToID("_SGT_OceanHeight");
		protected static readonly int _SGT_OceanLightDirection = Shader.PropertyToID("_SGT_OceanLightDirection");
		protected static readonly int _SGT_OceanLightColor     = Shader.PropertyToID("_SGT_OceanLightColor");
		protected static readonly int _SGT_OceanColor          = Shader.PropertyToID("_SGT_OceanColor");
		protected static readonly int _SGT_OceanSmoothness     = Shader.PropertyToID("_SGT_OceanSmoothness");
		protected static readonly int _SGT_OceanRadius         = Shader.PropertyToID("_SGT_OceanRadius");

		protected static readonly int _CwHoleCount    = Shader.PropertyToID("_CwHoleCount");
		protected static readonly int _CwHoleDatas    = Shader.PropertyToID("_CwHoleDatas");
		protected static readonly int _CwHoleMatrices = Shader.PropertyToID("_CwHoleMatrices");

		protected static readonly int _CwNoiseTex         = Shader.PropertyToID("_CwNoiseTex");
		protected static readonly int _CwBufferP          = Shader.PropertyToID("_CwBufferP");
		protected static readonly int _CwBufferSize       = Shader.PropertyToID("_CwBufferSize");
		protected static readonly int _CwWeights          = Shader.PropertyToID("_CwWeights");
		protected static readonly int _SGT_WeightTex      = Shader.PropertyToID("_SGT_WeightTex");
		protected static readonly int _CwCoords           = Shader.PropertyToID("_CwCoords");
		protected static readonly int _CwPixelSize        = Shader.PropertyToID("_CwPixelSize");
		protected static readonly int _CwVertexResolution = Shader.PropertyToID("_CwVertexResolution");

		protected static readonly int _CwFeatureRangeDetail  = Shader.PropertyToID("_CwFeatureRangeDetail");
		protected static readonly int _CwFeatureRangeFlatten = Shader.PropertyToID("_CwFeatureRangeFlatten");
		protected static readonly int _CwFeatureRangeColor   = Shader.PropertyToID("_CwFeatureRangeColor");
		protected static readonly int _CwFeatureRangeTexture = Shader.PropertyToID("_CwFeatureRangeTexture");

		protected static readonly int _CwFeatureDataA  = Shader.PropertyToID("_CwFeatureDataA");
		protected static readonly int _CwFeatureDataB  = Shader.PropertyToID("_CwFeatureDataB");
		protected static readonly int _CwFeatureDataC  = Shader.PropertyToID("_CwFeatureDataC");
		protected static readonly int _CwFeatureDataD  = Shader.PropertyToID("_CwFeatureDataD");
		protected static readonly int _CwFeatureDataE  = Shader.PropertyToID("_CwFeatureDataE");
		protected static readonly int _CwFeatureLayer  = Shader.PropertyToID("_CwFeatureLayer");
		protected static readonly int _CwFeatureMatrix = Shader.PropertyToID("_CwFeatureMatrix");

		protected static readonly int _CwOrigins       = Shader.PropertyToID("_CwOrigins");
		protected static readonly int _CwPositionsA    = Shader.PropertyToID("_CwPositionsA");
		protected static readonly int _CwPositionsB    = Shader.PropertyToID("_CwPositionsB");
		protected static readonly int _CwPositionsC    = Shader.PropertyToID("_CwPositionsC");
		protected static readonly int _CwOffset        = Shader.PropertyToID("_CwOffset");
		protected static readonly int _CwObjectToWorld = Shader.PropertyToID("_CwObjectToWorld");
		protected static readonly int _CwWorldToObject = Shader.PropertyToID("_CwWorldToObject");

		protected void PrepareBlitMaterial(string title)
		{
			blitMaterial = CW.Common.CwHelper.CreateTempMaterial(title, "Hidden/" + title);

			if (PIXEL_WEIGHTS == null) GenerateTex();

			if (noiseTex == null) GenerateNoiseTex();
			
			blitMaterial.SetTexture(_CwNoiseTex, noiseTex);
			blitMaterial.SetTexture(_SGT_WeightTex, PIXEL_WEIGHTS);
			blitMaterial.SetVectorArray(_CwWeights, VERTEX_BARY);
			blitMaterial.SetVectorArray(_CwCoords, VERTEX_COORDS);
			blitMaterial.SetVector(_CwPixelSize, new Vector2(PIXEL_WIDTH, PIXEL_HEIGHT));
			blitMaterial.SetFloat(_CwVertexResolution, VERTEX_RESOLUTION);

			ApplyBlitFeatureData();
		}

		private void ApplyBlitFeatureData()
		{
			if (blitMaterial == null) return;

			SgtLandscapeTextures.Write(blitMaterial);

			blitMaterial.SetVector(_CwFeatureRangeDetail, featureRangeDetail);
			blitMaterial.SetVector(_CwFeatureRangeFlatten, featureRangeFlatten);
			blitMaterial.SetVector(_CwFeatureRangeColor, featureRangeColor);
			blitMaterial.SetVector(_CwFeatureRangeTexture, featureRangeTexture);

			blitMaterial.SetVectorArray(_CwFeatureDataA, featureDataA);
			blitMaterial.SetVectorArray(_CwFeatureDataB, featureDataB);
			blitMaterial.SetVectorArray(_CwFeatureDataC, featureDataC);
			blitMaterial.SetVectorArray(_CwFeatureDataD, featureDataD);
			blitMaterial.SetVectorArray(_CwFeatureDataE, featureDataE);
			blitMaterial.SetVectorArray(_CwFeatureLayer, featureLayer);
			blitMaterial.SetMatrixArray(_CwFeatureMatrix, featureMatrix);
		}

		/// <summary>This allows you to add a child GameObject to your planet with high precision positions, regardless of the planet size.</summary>
		public void AddChild(Transform root, double3 localPos, quaternion localRot, float3 localScale)
		{
			root.SetParent(GetSpawnAnchor(), false);
			root.localPosition = (Vector3)(float3)(localPos - spawnOffset);
			root.localRotation = localRot;
			root.localScale    = (Vector3)localScale;
		}

		public Transform GetSpawnAnchor()
		{
			var parent = transform;

			if (useSpawnAnchor == true)
			{
				if (spawnAnchor == null)
				{
					var anchorName = "SpawnAnchor (" + gameObject.name + ")";
					var existing = GameObject.Find(anchorName);
					
					if (existing != null)
					{
						spawnAnchor = existing.transform;
					}
					else
					{
						spawnAnchor = new GameObject(anchorName).transform;
						spawnAnchor.gameObject.hideFlags = HideFlags.DontSave;
						spawnOffset = double3.zero;
						spawnAnchor.SetPositionAndRotation(transform.position, transform.rotation);
					}
				}

				UpdateAnchor();

				parent = spawnAnchor;
			}

			return parent;
		}

		private void DisposeAnchor()
		{
			if (spawnAnchor != null)
			{
				DestroyImmediate(spawnAnchor.gameObject);
			}

			spawnAnchor = null;
		}

		private void UpdateAnchor()
		{
			if (spawnAnchor == null) return;

			var planetMatrix     = new double4x4(transform.localToWorldMatrix);
			spawnAnchor.rotation = transform.rotation;
			spawnAnchor.position = (Vector3)(float3)math.transform(planetMatrix, spawnOffset);

			var camPos = default(Vector3);
			if (TryGetFirstObserverWorldPosition(ref camPos) == false) return;

			var snapInterval   = 100.0;
			var snapIntervalSq = snapInterval * snapInterval * 4.0;
			var invMatrix      = math.inverse(planetMatrix);
			var camLocal       = math.mul(invMatrix, new double4(camPos.x, camPos.y, camPos.z, 1.0)).xyz;
			var localDelta     = camLocal - spawnOffset;

			if (math.lengthsq(localDelta) >= snapIntervalSq)
			{
				var snapped = math.round(math.floor(camLocal / snapInterval)) * snapInterval;
				var delta   = snapped - spawnOffset;
				var offset  = (Vector3)(float3)(-delta);

				for (var i = 0; i < spawnAnchor.childCount; i++)
				{
					spawnAnchor.GetChild(i).localPosition += offset;
				}

				spawnOffset = snapped;

				spawnAnchor.position = (Vector3)(float3)math.transform(planetMatrix, spawnOffset);

				if (OnSpawnOffsetChanged != null) OnSpawnOffsetChanged();
			}
		}

		public bool GetTilingLayer(float size, ref int index, ref int tiling)
		{
			if (size > 0.0f)
			{
				if (size <= globalSizesNormalized.x)
				{
					index  = 0;
					tiling = (int)math.round(globalSizesNormalized.x / size);

					return true;
				}

				if (size <= globalSizesNormalized.y)
				{
					index  = 1;
					tiling = (int)math.round(globalSizesNormalized.y / size);

					return true;
				}

				if (size <= globalSizesNormalized.z)
				{
					index  = 2;
					tiling = (int)math.round(globalSizesNormalized.z / size);

					return true;
				}

				if (size <= globalSizesNormalized.w)
				{
					index  = 3;
					tiling = (int)math.round(globalSizesNormalized.w / size);

					return true;
				}
			}

			return false;
		}

		public int GetTriangleDepth(int triangleIndex)
		{
			return triangles[triangleIndex].Depth;
		}

		/// <summary>This will call <b>Rebuild</b> the next time this landscape updates.</summary>
		public void MarkForRebuild()
		{
			markForRebuild = true;
		}

		/// <summary>This method will completely reset the landscape and rebuild it from scratch. You can call this if you've modified a setting or feature that causes the landscape to change.</summary>
		[ContextMenu("Rebuild")]
		public void Rebuild()
		{
			if (triangles.IsCreated == true)
			{
				Dispose();
			}

			TryGenerateMeshData();

			Prepare();

#if UNITY_EDITOR
			if (Application.isPlaying == true)
			{
				UpdateLod(initDetail, 1.0f, int.MaxValue);
			}
			else
			{
				UpdateLod(editorDetail, 1.0f, int.MaxValue);
			}
#else
			UpdateLod(initDetail, 1.0f, int.MaxValue);
#endif
		}

		[System.NonSerialized] private PendingTriangle rootPending;

		public abstract int CalculateLodDepth(float triangleSize);

		public abstract double3 GetLocalPoint(double3 localPoint);

		public abstract PendingPoints SchedulePoints(NativeList<double3> localPoints);

		public bool IsActivated
		{
			get
			{
				return triangles.IsCreated == true;
			}
		}

		public double3 GetWorldPoint(double3 worldPoint) // TODO: Implement this with doubles
		{
			var localPoint = transform.InverseTransformPoint((float3)worldPoint);

			if (triangles.IsCreated == true)
			{
				localPoint = (float3)GetLocalPoint((double3)(float3)localPoint);
			}

			return (float3)transform.TransformPoint(localPoint);
		}

		public abstract double3 GetLocalDirection(double3 localPoint);

		public abstract double3 GetWorldDirection(double3 worldPoint);

		public abstract double3 GetWorldPivot(double3 worldPoint);

		private static double3 GetNormal(double3 vectorA, double3 vectorB, double length)
		{
			var smallsq = length * 0.1; smallsq *= smallsq;

			if (math.lengthsq(vectorA) < smallsq) return vectorB;
			if (math.lengthsq(vectorB) < smallsq) return vectorA;

			return -math.cross(vectorA, vectorB);
		}

		public double3 GetWorldNormal(double3 worldPoint, double3 worldRight, double3 worldForward)
		{
			var sampledPointL = GetWorldPoint(worldPoint - worldRight  );
			var sampledPointR = GetWorldPoint(worldPoint + worldRight  );
			var sampledPointB = GetWorldPoint(worldPoint - worldForward);
			var sampledPointF = GetWorldPoint(worldPoint + worldForward);

			var vectorA = sampledPointR - sampledPointL;
			var vectorB = sampledPointF - sampledPointB;

			var sampledNormal = math.normalize(GetNormal(vectorA, vectorB, math.length(worldRight)));

			if (math.dot(sampledNormal, sampledPointL - (float3)transform.position) < 0.0f)
			{
				sampledNormal = -sampledNormal;
			}

			return sampledNormal;
		}

		public double3 GetLocalNormal(double3 localPoint, double3 localRight, double3 localForward)
		{
			var sampledPointL = GetLocalPoint(localPoint - localRight  );
			var sampledPointR = GetLocalPoint(localPoint + localRight  );
			var sampledPointB = GetLocalPoint(localPoint - localForward);
			var sampledPointF = GetLocalPoint(localPoint + localForward);

			var vectorA = sampledPointR - sampledPointL;
			var vectorB = sampledPointF - sampledPointB;

			var sampledNormal = math.normalize(GetNormal(vectorA, vectorB, math.length(localRight)));

			if (math.dot(sampledNormal, sampledPointL - (float3)transform.position) < 0.0f)
			{
				sampledNormal = -sampledNormal;
			}

			return sampledNormal;
		}

		/// <summary>This method returns the nearest <b>SgtLandscape</b> to the specified world point.</summary>
		public static SgtLandscape FindNearest(Vector3 worldPosition)
		{
			var bestDistance  = float.PositiveInfinity;
			var bestLandscape = default(SgtLandscape);

			foreach (var landscape in AllLandscapes)
			{
				var distance = landscape.GetApproximateWorldDistance(worldPosition);

				if (distance < bestDistance)
				{
					bestDistance = distance;
					bestLandscape  = landscape;
				}
			}

			return bestLandscape;
		}

		public abstract float GetApproximateWorldDistance(Vector3 worldPoint);

		protected virtual void OnEnable()
		{
			node = AllLandscapes.AddLast(this);
		}

		protected virtual void OnDisable()
		{
			AllLandscapes.Remove(node); node = null;

			if (triangles.IsCreated == true)
			{
				Dispose();
			}

			if (AllLandscapes.Count == 0)
			{
				TryDisposeMeshData();
			}

			SgtLandscapeTextures.RebuildAll();
		}

		public void RegisterTextures()
		{
			foreach (var feature in features)
			{
				if (feature != null && feature.isActiveAndEnabled == true)
				{
					feature.RegisterTextures();
				}
			}
		}

		protected virtual void Prepare()
		{
			markForRebuild = false;

			topology             = new NativeList<Triangle>(Allocator.Persistent);
			cameraPositions      = new NativeList<double3>(Allocator.Persistent);
			triangles            = new NativeList<Triangle>(4096, Allocator.Persistent);
			createDiffs          = new NativeList<Triangle>(4096, Allocator.Persistent);
			deleteDiffs          = new NativeList<Triangle>(4096, Allocator.Persistent);
			statusDiffs          = new NativeList<Triangle>(4096, Allocator.Persistent);
			cameraPoints         = new PendingPoints(1);
			activeHoleDatas      = new Vector4[HOLES_CAPACITY];
			activeHoleMatrices   = new Matrix4x4[HOLES_CAPACITY];
			activeHoleDatasNA    = new NativeArray<float4>(HOLES_CAPACITY, Allocator.Persistent);
			activeHoleMatricesNA = new NativeArray<double4x4>(HOLES_CAPACITY, Allocator.Persistent);

			features.Clear();

			GetComponentsInChildren(features);

			features.RemoveAll(f =>f.isActiveAndEnabled == false ||  f.transform.parent != transform);

			foreach (var feature in features)
			{
				feature.Prepare();
			}

			SgtLandscapeTextures.RebuildAll();
		}

		public void RefreshFeatureData()
		{
			tempGlobalDetails.Clear();
			tempLocalDetails.Clear();
			tempGlobalFlattens.Clear();
			tempLocalFlattens.Clear();
			tempGlobalColors.Clear();
			tempLocalColors.Clear();
			tempGlobalTextures.Clear();
			tempLocalTextures.Clear();

			foreach (var feature in features)
			{
				if (feature == null) continue;

				if (feature is SgtLandscapeBiome)
				{
					var f       = (SgtLandscapeBiome)feature;
					var matrix  = f.Space == SgtLandscapeBiome.SpaceType.Local ? f.CalculateMatrix() : Matrix4x4.identity;
					var replace = f.Replace ? 1.0f : 0.0f;
					var count   = 0;

					foreach (var l in f.Layers)
					{
						if (l.Enabled == true)
						{
							var d = new FeatureData {
								B = new float4(l.Strata, SgtLandscapeTextures.GetHeightIndex(l.HeightTexture), -l.HeightRange * l.HeightMidpoint, l.HeightRange),
								C = new float4(SgtLandscapeTextures.GetMaskIndex(f.Mask), f.MaskSharpness, f.MaskGlobalShift, f.MaskInvert ? 1 : 0),
								D = new float4(SgtLandscapeTextures.GetDetailIndex(f.MaskDetail), f.MaskDetailTiling.x, f.MaskDetailTiling.y, f.MaskDetailOffset),
								E = new float4(replace, 0.0f, 0.0f, 0.0f)
							};

							if (f.Space == SgtLandscapeBiome.SpaceType.Local)
							{
								d.A      = new float4(l.LocalTiling.x, l.LocalTiling.y, l.LocalScale.x, l.LocalScale.y);
								d.Matrix = matrix;
								tempLocalDetails.Add(d);
							}
							else if (l.GlobalTile > 0)
							{
								var vector = Vector4.zero; vector[l.GlobalIndex] = 1.0f;
								d.A        = new float4(l.HeightRange * globalTilingNormalized[l.GlobalIndex] * l.GlobalTile, l.GlobalTile, 0.0f, 0.0f);
								d.Layer    = vector;
								tempGlobalDetails.Add(d);
							}

							replace = 0.0f;
							count  += 1;
						}
					}

					if (f.Replace == true && count == 0)
					{
						var d = new FeatureData {
							B = new float4(f.Strata, 0, 0, 0),
							C = new float4(SgtLandscapeTextures.GetMaskIndex(f.Mask), f.MaskSharpness, f.MaskGlobalShift, f.MaskInvert ? 1 : 0),
							D = new float4(SgtLandscapeTextures.GetDetailIndex(f.MaskDetail), f.MaskDetailTiling.x, f.MaskDetailTiling.y, f.MaskDetailOffset),
							E = new float4(1.0f, 0.0f, 0.0f, 0.0f)
						};

						if (f.Space == SgtLandscapeBiome.SpaceType.Local)
						{
							d.A      = new float4(1.0f, 1.0f, 0.0f, 0.0f);
							d.Matrix = matrix;
							tempLocalDetails.Add(d);
						}
						else
						{
							var vector = Vector4.zero; vector[0] = 1.0f;
							d.A        = new float4(0.0f, 1.0f, 0.0f, 0.0f);
							d.Layer    = vector;
							tempGlobalDetails.Add(d);
						}
					}

					foreach (var l in f.TextureLayers)
					{
						if (l.Enabled == true)
						{
							var d = new FeatureData {
								A = new Vector4(SgtLandscapeTextures.GetAlbedoIndex(l.AlbedoTexture), l.UseMinHeight ? l.MinHeight : -1.0f, l.MinHeightFalloff, l.UseMaxHeight ? l.MaxHeight : -1.0f),
								B = new Vector4(l.MaxHeightFalloff, l.UseMinSlope ? l.MinSlope : -1.0f, l.MinSlopeFalloff, l.UseMaxSlope ? l.MaxSlope : -1.0f),
								C = new Vector4(l.MaxSlopeFalloff, SgtLandscapeTextures.GetMaskIndex(f.Mask), f.MaskSharpness, f.MaskInvert ? 1 : 0),
								D = new Vector4(SgtLandscapeTextures.GetDetailIndex(f.MaskDetail), f.MaskDetailTiling.x, f.MaskDetailTiling.y, f.MaskDetailOffset),
								E = new Vector4(f.MaskGlobalShift, SgtLandscapeTextures.GetNormalIndex(l.NormalTexture), l.StrataWeight, 0)
							};

							if (f.Space == SgtLandscapeBiome.SpaceType.Local) { d.Matrix = matrix; tempLocalTextures.Add(d); } else { tempGlobalTextures.Add(d); }
						}
					}

					if (f.Color == true)
					{
						var d = new FeatureData {
							A = new float4(f.Variation, f.Occlusion, f.Strata, SgtLandscapeTextures.GetGradientIndex(f.GradientTexture)),
							B = new float4(f.Blur, f.Offset, f.SmoothnessStrength, f.EmissionStrength),
							C = new float4(SgtLandscapeTextures.GetMaskIndex(f.Mask), f.MaskSharpness, f.MaskGlobalShift, f.MaskInvert ? 1 : 0),
							D = new float4(SgtLandscapeTextures.GetDetailIndex(f.MaskDetail), f.MaskDetailTiling.x, f.MaskDetailTiling.y, f.MaskDetailOffset),
							E = new float4(f.SmoothnessMidpoint, f.SmoothnessPower, f.EmissionMidpoint, f.EmissionPower)
						};

						if (f.Space == SgtLandscapeBiome.SpaceType.Local) { d.Matrix = matrix; tempLocalColors.Add(d); } else { tempGlobalColors.Add(d); }
					}
				}
				else if (feature is SgtLandscapeDetail)
				{
					var f    = (SgtLandscapeDetail)feature;
					var replaceValue = f.Replace ? 0.0f : 1.0f;

					var d = new FeatureData {
						B = new float4(f.Strata, SgtLandscapeTextures.GetHeightIndex(f.HeightTexture), -f.HeightRange * f.HeightMidpoint, f.HeightRange),
						C = new float4(SgtLandscapeTextures.GetMaskIndex(f.Mask), f.MaskSharpness, f.MaskGlobalShift, f.MaskInvert ? 1 : 0),
						D = new float4(SgtLandscapeTextures.GetDetailIndex(f.MaskDetail), f.MaskDetailTiling.x, f.MaskDetailTiling.y, f.MaskDetailOffset),
						E = new float4(replaceValue, 0.0f, 0.0f, 0.0f)
					};

					if (f.Space == SgtLandscapeDetail.SpaceType.Local)
					{
						d.A      = new float4(f.LocalTiling.x, f.LocalTiling.y, f.LocalScale.x, f.LocalScale.y);
						d.Matrix = f.CalculateMatrix();
						tempLocalDetails.Add(d);
					}
					else if (f.GlobalTile > 0)
					{
						var vector = Vector4.zero; vector[f.GlobalIndex] = 1.0f;
						d.A        = new float4(f.HeightRange * globalTilingNormalized[f.GlobalIndex] * f.GlobalTile, f.GlobalTile, 0.0f, 0.0f);
						d.Layer    = vector;
						tempGlobalDetails.Add(d);
					}
				}
				else if (feature is SgtLandscapeFlatten)
				{
					var f = (SgtLandscapeFlatten)feature;
					var d = new FeatureData {
						A = new float4(f.TargetHeight, f.TargetStrata, f.FlattenHeight, f.FlattenStrata),
						C = new float4(SgtLandscapeTextures.GetMaskIndex(f.MaskTexture), 0, 0, f.MaskInvert ? 1 : 0)
					};

					if (f.Space == SgtLandscapeFlatten.SpaceType.Local) { d.Matrix = f.CalculateMatrix(); tempLocalFlattens.Add(d); } else if (f.Space == SgtLandscapeFlatten.SpaceType.Global) { tempGlobalFlattens.Add(d); }
				}
				else if (feature is SgtLandscapeTexture)
				{
					var f = (SgtLandscapeTexture)feature;
					var d = new FeatureData {
						A = new Vector4(SgtLandscapeTextures.GetAlbedoIndex(f.AlbedoTexture), f.UseMinHeight ? f.MinHeight : -1.0f, f.MinHeightFalloff, f.UseMaxHeight ? f.MaxHeight : -1.0f),
						B = new Vector4(f.MaxHeightFalloff, f.UseMinSlope ? f.MinSlope : -1.0f, f.MinSlopeFalloff, f.UseMaxSlope ? f.MaxSlope : -1.0f),
						C = new Vector4(f.MaxSlopeFalloff, SgtLandscapeTextures.GetMaskIndex(f.Mask), f.MaskSharpness, f.MaskInvert ? 1 : 0),
						D = new Vector4(SgtLandscapeTextures.GetDetailIndex(f.MaskDetail), f.MaskDetailTiling.x, f.MaskDetailTiling.y, f.MaskDetailOffset),
						E = new Vector4(f.MaskGlobalShift, SgtLandscapeTextures.GetNormalIndex(f.NormalTexture), f.StrataWeight, 0)
					};

					if (f.Space == SgtLandscapeTexture.SpaceType.Local) { d.Matrix = f.CalculateMatrix(); tempLocalTextures.Add(d); } else if (f.Space == SgtLandscapeTexture.SpaceType.Global) { tempGlobalTextures.Add(d); }
				}
				else if (feature is SgtLandscapeColor)
				{
					var f = (SgtLandscapeColor)feature;
					var d = new FeatureData {
						A = new float4(f.Variation, f.Occlusion, f.Strata, SgtLandscapeTextures.GetGradientIndex(f.GradientTexture)),
						B = new float4(f.Blur, f.Offset, f.SmoothnessStrength, f.EmissionStrength),
						C = new float4(SgtLandscapeTextures.GetMaskIndex(f.Mask), f.MaskSharpness, f.MaskGlobalShift, f.MaskInvert ? 1 : 0),
						D = new float4(SgtLandscapeTextures.GetDetailIndex(f.MaskDetail), f.MaskDetailTiling.x, f.MaskDetailTiling.y, f.MaskDetailOffset),
						E = new float4(f.SmoothnessMidpoint, f.SmoothnessPower, f.EmissionMidpoint, f.EmissionPower)
					};

					if (f.Space == SgtLandscapeColor.SpaceType.Local) { d.Matrix = f.CalculateMatrix(); tempLocalColors.Add(d); } else { tempGlobalColors.Add(d); }
				}
			}

			// Copy pooled features to Shader Arrays
			int index = 0;
			
			featureRangeDetail.x = index; index = CopyFeatures(tempGlobalDetails, index);
			featureRangeDetail.y = index; featureRangeDetail.z = index;
			index = CopyFeatures(tempLocalDetails, index); featureRangeDetail.w = index;

			featureRangeFlatten.x = index; index = CopyFeatures(tempGlobalFlattens, index);
			featureRangeFlatten.y = index; featureRangeFlatten.z = index;
			index = CopyFeatures(tempLocalFlattens, index); featureRangeFlatten.w = index;

			featureRangeColor.x = index; index = CopyFeatures(tempGlobalColors, index);
			featureRangeColor.y = index; featureRangeColor.z = index;
			index = CopyFeatures(tempLocalColors, index); featureRangeColor.w = index;

			featureRangeTexture.x = index; index = CopyFeatures(tempGlobalTextures, index);
			featureRangeTexture.y = index; featureRangeTexture.z = index;
			index = CopyFeatures(tempLocalTextures, index); featureRangeTexture.w = index;

			ApplyBlitFeatureData();
		}

		private int CopyFeatures(List<FeatureData> source, int startIndex)
		{
			for (int i = 0; i < source.Count; i++)
			{
				if (startIndex < FEATURE_CAPACITY)
				{
					featureDataA[startIndex]  = source[i].A;
					featureDataB[startIndex]  = source[i].B;
					featureDataC[startIndex]  = source[i].C;
					featureDataD[startIndex]  = source[i].D;
					featureDataE[startIndex]  = source[i].E;
					featureLayer[startIndex]  = source[i].Layer;
					featureMatrix[startIndex] = source[i].Matrix;
					startIndex++;
				}
			}
			return startIndex;
		}

		protected virtual void Dispose()
		{
			if (OnDispose != null) OnDispose.Invoke();

			foreach (var pending in PendingTriangle.Pool) { pending.Dispose(); } PendingTriangle.Pool.Clear();

			foreach (var batch in batches) { batch.Dispose(); } batches.Clear();

			foreach (var storage in storages) { storage.Dispose(); } storages.Clear();

			foreach (var pending in pendingTriangles) { pending.Handle.Complete(); pending.Dispose(); } pendingTriangles.Clear();

			if (OnRemoveVisual != null)
			{
				foreach (var pair in visuals)
				{
					OnRemoveVisual.Invoke(pair.Value);
				}
			}

			visuals.Clear();

			Visual.Pool.Clear();

			if (rootPending != null) rootPending.Dispose(); rootPending = null;

			foreach (var feature in features)
			{
				feature.Dispose();
			}

			pendingUpdate.Complete();

			topology.Dispose();

			cameraPositions.Dispose();

			cameraPoints.Dispose();

			triangles.Dispose();

			createDiffs.Dispose();
			deleteDiffs.Dispose();
			statusDiffs.Dispose();

			activeHoleDatas = null;
			activeHoleMatrices = null;
			activeHoleDatasNA.Dispose();
			activeHoleMatricesNA.Dispose();
		}

		public bool TryGetFirstObserverWorldPosition(ref Vector3 worldPosition)
		{
			if (observers != null)
			{
				foreach (var observer in observers)
				{
					if (observer != null)
					{
						worldPosition = observer.position; return true;
					}
				}
			}

			var mainCamera = Camera.main;

			if (mainCamera != null)
			{
				worldPosition = mainCamera.transform.position; return true;
			}

			return false;
		}

		private void UpdateCameraPositions()
		{
			cameraPositions.Clear();

			if (observers != null)
			{
				foreach (var observer in observers)
				{
					if (observer != null)
					{
						cameraPositions.Add((float3)transform.InverseTransformPoint(observer.position));
					}
				}
			}

			if (cameraPositions.Length == 0 && Camera.main != null)
			{
				cameraPositions.Add((float3)transform.InverseTransformPoint(Camera.main.transform.position));
			}

			if (cameraPoints.Count != cameraPositions.Length)
			{
				cameraPoints.Dispose();

				cameraPoints = new PendingPoints(cameraPositions.Length);
			}

			cameraPoints.Points.CopyFrom(cameraPositions.AsArray());
		}

		private bool UpdateDiffs(float budget)
		{
			var maxTicks = long.MaxValue;

			if (budget > 0.0f)
			{
				maxTicks = (long)(System.Diagnostics.Stopwatch.Frequency * budget);
			}

			stopwatch.Restart();

			for (var i = createDiffs.Length - 1; i >= 0; i--)
			{
				var triangle = createDiffs[i]; createDiffs.RemoveAt(i);

				var pendingTriangle = Schedule(triangle);

				pendingTriangles.Add(pendingTriangle);

				if (stopwatch.ElapsedTicks >= maxTicks)
				{
					return false;
				}
			}

			for (var i = pendingTriangles.Count - 1; i >= 0; i--)
			{
				var pendingTriangle = pendingTriangles[i];
				
				if (pendingTriangle.Handle.IsCompleted == true || budget < 0.0f)
				{
					pendingTriangles.RemoveAt(i);

					CompleteAndAddVisual(pendingTriangle);

					if (pendingTriangle.Triangle.Split == false)
					{
						statusDiffs.Add(pendingTriangle.Triangle);
					}

					if (stopwatch.ElapsedTicks >= maxTicks)
					{
						return false;
					}
				}
			}

			// Finished? Swap!
			if (createDiffs.Length == 0 && pendingTriangles.Count == 0)
			{
				foreach (var triangle in deleteDiffs)
				{
					HideVisual(triangle.Hash);

					RemoveVisual(triangle.Hash);
				}

				foreach (var triangle in statusDiffs)
				{
					if (triangle.Split == true)
					{
						HideVisual(triangle.Hash);
					}
					else
					{
						ShowVisual(triangle.Hash);
					}
				}

				deleteDiffs.Clear();
				statusDiffs.Clear();

				return true;
			}

			return false;
		}

		/// <summary>This method will force the LOD to update now, rather than slowly over time.</summary>
		[ContextMenu("Force Update LOD")]
		public void ForceUpdateLOD()
		{
#if UNITY_EDITOR
			if (Application.isPlaying == true)
			{
				UpdateLod(detail, boostMultiplier, lodSteps);
			}
			else
			{
				UpdateLod(editorDetail, boostMultiplier, int.MaxValue);
			}
#else
			UpdateLod(detail, boostMultiplier, int.MaxValue);
#endif
		}

		private void UpdateLod(float detail, float detailBoost, int maxSteps, float budget = -1.0f)
		{
			if (IsActivated == true)
			{
				if (pendingUpdate.Running == false)
				{
					if (UpdateDiffs(budget) == true)
					{
						UpdateCameraPositions();

						UpdateHoleData();

						pendingUpdate.Schedule(ScheduleUpdateTriangles(detail, boostMultiplier, maxSteps));
					}
				}

				if (pendingUpdate.Running == true)
				{
					if (pendingUpdate.Handle.IsCompleted == true || budget < 0.0f)
					{
						pendingUpdate.Complete();

						UpdateDiffs(budget);
					}
				}
			}
		}

#if UNITY_EDITOR
		private int childrenHash;

		private void UpdateChildrenHash()
		{
			var oldHash = childrenHash;

			childrenHash = 0;

			UpdateChildrenHash(transform);

			if (childrenHash != oldHash)
			{
				MarkForRebuild();
			}
		}

		private void UpdateChildrenHash(Transform t)
		{
			for (var i = 0; i < t.childCount; i++)
			{
				var child   = t.GetChild(i);
				var feature = child.GetComponent<SgtLandscapeFeature>();

				if (feature != null)
				{
					childrenHash = childrenHash * 31 + child.localPosition.GetHashCode();
					childrenHash = childrenHash * 31 + child.localRotation.GetHashCode();
					childrenHash = childrenHash * 31 + child.localScale   .GetHashCode();
				}

				if (child.GetComponent<SgtLandscapePrefabSpawner>() == null && child.name != "Colliders")
				{
					UpdateChildrenHash(child);
				}
			}
		}
#endif

		protected virtual void Start()
		{
			if (triangles.IsCreated == true)
			{
				Dispose();
			}

			TryGenerateMeshData();
		}

		protected virtual void OnDestroy()
		{
			DisposeAnchor();
		}

		public void RegisterCave(SgtLandscapeCave cave)
		{
			caves.Add(cave);
		}

		public void UnregisterCave(SgtLandscapeCave cave)
		{
			caves.Remove(cave);
		}

		public void CompleteAllPendingJobs()
		{
			foreach (var pending in pendingTriangles)
			{
				pending.Handle.Complete();
			}

			pendingUpdate.Complete();
		}

		private void UpdateHoleData()
		{
			activeHoleCount = 0;

			foreach (var cave in caves)
			{
				if (cave.isActiveAndEnabled == true && cave.SpawnedInstance != null)
				{
					foreach (var entrance in cave.SpawnedInstance.Entrances)
					{
						if (entrance != null)
						{
							var data   = new Vector4((int)entrance.Shape, 0.0f, 0.0f, 0.0f);
							var matrix = entrance.EntranceMatrix;

							activeHoleDatas[activeHoleCount] = data;
							activeHoleMatrices[activeHoleCount] = (float4x4)matrix;
							activeHoleDatasNA[activeHoleCount] = data;
							activeHoleMatricesNA[activeHoleCount] = matrix;

							activeHoleCount += 1;
						}
					}
				}
			}
		}

		protected virtual void LateUpdate()
		{
#if UNITY_EDITOR
			UpdateChildrenHash();
#endif
			if (markForRebuild == true || triangles.IsCreated == false)
			{
				Rebuild();
			}

			UpdateHoleData();

			var finalLodSteps = lodSteps;

#if UNITY_EDITOR
			if (Application.isPlaying == true)
			{
				UpdateLod(detail, boostMultiplier, finalLodSteps, lodBudget);
			}
#else
			UpdateLod(detail, boostMultiplier, finalLodSteps, lodBudget);
#endif
			
			DrawTriangles();

			if (useSpawnAnchor == true)
			{
				UpdateAnchor();
			}
		}

		protected abstract Bounds GetWorldBounds();

		public static Bounds ScaleToFarClipPlane(Bounds bounds, Camera camera)
		{
			var camPos          = camera.transform.position;
			var camForward      = camera.transform.forward;
			var farClip         = camera.farClipPlane;
			var farPointTarget  = camPos + camForward * 1e9f; 
			var farthestPoint   = bounds.ClosestPoint(farPointTarget);
			var toFarthestPoint = farthestPoint - camPos;
			var depth           = Vector3.Dot(toFarthestPoint, camForward);

			if (depth <= farClip || depth <= 0.0f)
			{
				return bounds;
			}

			var scale     = farClip / depth;
			var newCenter = camPos + (bounds.center - camPos) * scale;
			var newSize   = bounds.size * scale;

			return new Bounds(newCenter, newSize);
		}

		private void DrawTriangles()
		{
			var o2w   = RemoveTranslation(transform.localToWorldMatrix);
			var w2o   = RemoveTranslation(transform.worldToLocalMatrix);
			var rot   = Quaternion.Inverse(transform.rotation) * transform.position;

			var localOffset = transform.InverseTransformVector(transform.position);

			var settings = new RenderParams(material);

			settings.worldBounds       = GetWorldBounds();
			settings.layer             = gameObject.layer;
			settings.camera            = null;
			settings.shadowCastingMode = castShadows == true ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
			settings.receiveShadows    = receiveShadows;

			if (compressBounds == true)
			{
				var mainCamera = Camera.main;

				if (mainCamera != null)
				{
					settings.worldBounds = ScaleToFarClipPlane(settings.worldBounds, mainCamera);
				}
			}

			for (var i = batches.Count - 1; i >= 0; i--)
			{
				var batch = batches[i];

				if (batch.IsDirty == true)
				{
					if (batch.Count > 0)
					{
						batch.Properties.SetVectorArray(_CwOrigins, batch.Origins);
						batch.Properties.SetVectorArray(_CwPositionsA, batch.PositionsA);
						batch.Properties.SetVectorArray(_CwPositionsB, batch.PositionsB);
						batch.Properties.SetVectorArray(_CwPositionsC, batch.PositionsC);

						//batch.MR.SetPropertyBlock(batch.Properties);
						//batch.MF.sharedMesh = batchMeshes[batch.Count - 1];

						batch.IsDirty = false;
					}
					else
					{
						batches.RemoveAt(i);

						batch.Dispose();

						continue;
					}

					batch.IsDirty = false;
				}

				batch.Properties.SetMatrix(_CwObjectToWorld, o2w);
				batch.Properties.SetMatrix(_CwWorldToObject, w2o);
				batch.Properties.SetVector(_CwOffset, localOffset);

				UpdateBatchBeforeRender(batch);

				settings.matProps = batch.Properties;

				Graphics.RenderMeshPrimitives(settings, visualBlitMesh, 0, batch.Count);
			}
		}

		protected abstract void UpdateBatchBeforeRender(Batch batch);

		private Matrix4x4 RemoveTranslation(Matrix4x4 matrix)
		{
			matrix.m03 = 0;
			matrix.m13 = 0;
			matrix.m23 = 0;

			return matrix;
		}

		/*
		protected virtual void OnGUI()
		{
			if (batches.Count > 0)
			{
				var w = batches[0].Atlas.DataA.width;
				var h = batches[0].Atlas.DataA.height;

				GUI.DrawTexture(new Rect(10,10,w,h), batches[0].Atlas.DataN, ScaleMode.StretchToFill, false);
			}
		}
		*/
#if UNITY_EDITOR
	[System.NonSerialized] protected List<float3x3> gizmoTriangles = new List<float3x3>();
	[System.NonSerialized] protected List<float3>   gizmoObservers = new List<float3>();

	protected virtual void OnDrawGizmosSelected()
	{
		Gizmos.matrix = transform.localToWorldMatrix;

		if (IsActivated == true)
		{
			if (pendingUpdate.Running == false)
			{
				gizmoTriangles.Clear();
				gizmoObservers.Clear();

				foreach (var triangle in triangles)
				{
					if (triangle.Split == false)
					{
						var gizmoTriangle = default(float3x3);

						gizmoTriangle.c0 = (float3)triangle.PositionA;
						gizmoTriangle.c1 = (float3)triangle.PositionB;
						gizmoTriangle.c2 = (float3)triangle.PositionC;

						gizmoTriangles.Add(gizmoTriangle);
					}
				}

				foreach (var cameraPosition in cameraPositions)
				{
					gizmoObservers.Add((float3)cameraPosition);
				}

				UpdateGizmoTriangles();
			}

			foreach (var gizmoTriangle in gizmoTriangles)
			{
				Gizmos.DrawLine(gizmoTriangle.c0, gizmoTriangle.c1);
				Gizmos.DrawLine(gizmoTriangle.c1, gizmoTriangle.c2);
				Gizmos.DrawLine(gizmoTriangle.c2, gizmoTriangle.c0);
			}

			foreach (var cameraPosition in gizmoObservers)
			{
				Gizmos.DrawWireSphere(cameraPosition, 0.1f);
			}
		}
	}

	protected virtual void UpdateGizmoTriangles()
	{
	}
#endif
	}
}

#if UNITY_EDITOR
namespace SpaceGraphicsToolkit.Landscape
{
	using CW.Common;

	class SgtAssetPostprocessor : UnityEditor.AssetPostprocessor
	{
		static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
		{
			foreach (var landscape in SgtLandscape.AllLandscapes)
			{
				landscape.MarkForRebuild();
			}
		}
	}

	public class SgtLandscape_Editor : CwEditor
	{
		private static bool expandBundle;

		protected override void OnInspector()
		{
			SgtLandscape tgt; SgtLandscape[] tgts; GetTargets(out tgt, out tgts);

			var markForRebuild = false;

			if (Any(tgts, t => t.GetComponent<SgtLandscapeCollider>() == null))
			{
				if (Button("Add Collider") == true)
				{
					Each(tgts, t => { if (t.GetComponent<SgtLandscapeCollider>() == null) t.gameObject.AddComponent<SgtLandscapeCollider>(); }, true);
				}
			}

			if (Button("Add Detail") == true)
			{
				Each(tgts, t => { AddChildComponent<SgtLandscapeDetail>(t); }, true);
			}

			if (Button("Add Flatten") == true)
			{
				Each(tgts, t => { AddChildComponent<SgtLandscapeFlatten>(t); }, true);
			}

			if (Button("Add Color") == true)
			{
				Each(tgts, t => { AddChildComponent<SgtLandscapeColor>(t); }, true);
			}

			if (Button("Add Texture") == true)
			{
				Each(tgts, t => { AddChildComponent<SgtLandscapeTexture>(t); }, true);
			}

			if (Button("Add Biome") == true)
			{
				Each(tgts, t => { AddChildComponent<SgtLandscapeBiome>(t); }, true);
			}

			if (Button("Add Prefab Spawner") == true)
			{
				Each(tgts, t => { AddChildComponent<SgtLandscapePrefabSpawner>(t); }, true);
			}

			if (Button("Add Static Spawner") == true)
			{
				Each(tgts, t => { AddChildComponent<SgtLandscapeStaticSpawner>(t); }, true);
			}

			if (Button("Force Update LOD") == true)
			{
				Each(tgts, t => t.ForceUpdateLOD(), true);
			}

			Separator();

			Draw("editorDetail", "This allows you to control the LOD detail while in edit mode.");
			BeginError(Any(tgts, t => t.InitDetail > t.Detail));
				Draw("initDetail", "When entering play mode or running the actual game, the landscape will initialize with this level of detail.\n\nNOTE: 0 means it will initialize with the minimum amount of triangles, and will generate very quickly.\n\nHigher values will make the planet immediately look better, but may cause the scene to freeze until it's generated.");
			EndError();
			BeginError(Any(tgts, t => t.Detail <= 0.0f));
				Draw("detail", "The overall detail of the landscape relative to the camera distance. The higher you set this, the more triangles it will have.");
			EndError();
			Draw("boostMultiplier", "This allows you to boost the detail of lower LOD stages, so the landscape detail doesn't go too low.");
			BeginError(Any(tgts, t => t.MinimumTriangleSize <= 0.0f));
				Draw("minimumTriangleSize", "The maximum LOD depth this landscape can have.");
			EndError();
			Draw("lodBudget", "The maximum amount of seconds we can budget for LOD.");
			Draw("lodSteps", "The maximum LOD chunks that can be generated between each LOD change. For example, if your camera suddenly travels to the planet surface then many LOD chunks will need to be generated. If you set LOD Steps to a high value, then it will take a while for any changes to appear, whereas a low value means the landscape will constantly be updating to the final LOD state.");
			Draw("useSpawnAnchor", "If you spawn objects on a massive planet, their positions can degrade from floating point precision. If you enable this, then they will spawn on a separate GameObject in the scene root.");
			Draw("observers", "The landscape LOD will be based on these transform positions.\n\nNone/null = The GameObject with the <b>MainCamera</b> tag will be used.");

			Separator();

			BeginError(Any(tgts, t => t.Material == null));
				Draw("material", "The landscape will be rendered using this Material.\n\nNOTE: This material must use a shader based on the SGT/Landscape shader.");
			EndError();
			if (DrawVector4("globalSizes", "To support massive planets, the global detail/biome UV sizes must be baked into the mesh. This allows you to set up to 4 different tile sizes.") == true)
			{
				markForRebuild = true;
			}
			Draw("castShadows", "Should the landscape cast Unity built-in shadows?");
			Draw("receiveShadows", "Should the landscape receive Unity built-in shadows?");
			Draw("compressBounds", "Scale the render bounds to be within range of the camera?");

			Separator();
			
			expandBundle = UnityEditor.EditorGUILayout.Foldout(expandBundle, "Global Texture Bundle");
			if (expandBundle)
			{
				BeginIndent();
				SgtLandscapeTextures.DrawInspector(this, ref markForRebuild);
				EndIndent();
			}

			if (markForRebuild == true)
			{
				Each(tgts, t => t.MarkForRebuild());
			}
		}

		public static Texture2D Randomize(Texture2D current, ref bool markForRebuild)
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
						current        = next;
						markForRebuild = true;
					}
				}
			}

			return current;
		}

		private static T AddChildComponent<T>(SgtLandscape tgt)
			where T : Component
		{
			var child = new GameObject(typeof(T).Name);

			child.transform.SetParent(tgt.transform, false);

			var component = child.AddComponent<T>();

			CW.Common.CwHelper.SelectAndPing(component);

			return component;
		}

		public static float Randomize01(float current, ref bool markForRebuild)
		{
			current        = UnityEngine.Random.Range(0.0f, 1.0f);
			markForRebuild = true;

			return current;
		}
	}
}
#endif