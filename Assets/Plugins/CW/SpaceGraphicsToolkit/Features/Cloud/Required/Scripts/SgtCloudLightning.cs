using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;
using System.Collections.Generic;
using CW.Common;

namespace SpaceGraphicsToolkit.Cloud
{
	/// <summary>This component manages and renders procedural lightning strikes within a cloud layer using GPU instanced procedural rendering via standard arrays.</summary>
	[AddComponentMenu("Space Graphics Toolkit/SGT Cloud Lightning")]
	public class SgtCloudLightning : MonoBehaviour
	{
		public struct LightningData
		{
			public Vector3 Position;
			public float   Life;
			public int     Cell;
		}

		/// <summary>Which layer of the parent cloud to use (0..3).</summary>
		public int CloudLayerIndex { set { cloudLayerIndex = value; } get { return cloudLayerIndex; } } [SerializeField] [Range(0, 3)] private int cloudLayerIndex = 0;

		/// <summary>The material used to render the lightning.</summary>
		public Material Material { set { material = value; } get { return material; } } [SerializeField] private Material material;

		/// <summary>This allows you to offset the camera distance in world space when rendering the lightning, giving you fine control over the render order.</summary>
		public float CameraOffset { set { cameraOffset = value; } get { return cameraOffset; } } [SerializeField] private float cameraOffset = 0.1f;

		/// <summary>Coverage value below this will have no lightning.</summary>
		public float LightningThreshold { set { lightningThreshold = value; } get { return lightningThreshold; } } [SerializeField] [Range(0.0f, 1.0f)] private float lightningThreshold = 0.6f;

		/// <summary>The brightness multiplier for the lightning flash.</summary>
		public float LightningBrightness { set { lightningBrightness = value; } get { return lightningBrightness; } } [SerializeField] private float lightningBrightness = 5.0f;

		/// <summary>The width thickness of the lightning quad in world space.</summary>
		public float LightningThickness { set { lightningThickness = value; } get { return lightningThickness; } } [SerializeField] private float lightningThickness = 2.0f;

		/// <summary>The lifetime duration of a single lightning strike in seconds.</summary>
		public float LightningDuration { set { lightningDuration = value; } get { return lightningDuration; } } [SerializeField] private float lightningDuration = 0.3f;

		/// <summary>The normalized midpoint of the animation (0 to 1) separating the reveal phase from the return stroke phase.</summary>
		public float AnimationMidpoint { set { animationMidpoint = Mathf.Clamp01(value); } get { return animationMidpoint; } } [SerializeField] [Range(0.0f, 1.0f)] private float animationMidpoint = 0.1f;

		/// <summary>The brightness multiplier added during the return stroke phase.</summary>
		public float AnimationBrightness { set { animationBrightness = value; } get { return animationBrightness; } } [SerializeField] private float animationBrightness = 10.0f;

		/// <summary>The mean/average time interval between lightning strikes in seconds.</summary>
		public float LightningInterval { set { lightningInterval = value; } get { return lightningInterval; } } [SerializeField] private float lightningInterval = 4.0f;

		/// <summary>The standard deviation for the normal distribution curve.</summary>
		public float LightningIntervalStdDev { set { lightningIntervalStdDev = value; } get { return lightningIntervalStdDev; } } [SerializeField] private float lightningIntervalStdDev = 1.2f;

		/// <summary>Pinches the spawned random positions closer to the main camera direction line of sight (0 = default spread, 1 = heavily pinched).</summary>
		public float LightningBias { set { lightningBias = Mathf.Clamp01(value); } get { return lightningBias; } } [SerializeField] [Range(0.0f, 1.0f)] private float lightningBias = 0.0f;

		/// <summary>Prevents lightning from spawning too close to the camera (0 = allows hitting screen, 1 = pushed fully to horizon bounds).</summary>
		public float LightningMinDistance { set { lightningMinDistance = Mathf.Clamp01(value); } get { return lightningMinDistance; } } [SerializeField] [Range(0.0f, 1.0f)] private float lightningMinDistance = 0.0f;

		/// <summary>The color of the cloud illumination flash.</summary>
		public Color FlashColor { set { flashColor = value; } get { return flashColor; } } [SerializeField] private Color flashColor = Color.white;

		/// <summary>The brightness multiplier for the cloud illumination flash.</summary>
		public float FlashBrightness { set { flashBrightness = value; } get { return flashBrightness; } } [SerializeField] private float flashBrightness = 5.0f;

		/// <summary>The radius of the cloud illumination flash in local space.</summary>
		public float FlashRadius { set { flashRadius = value; } get { return flashRadius; } } [SerializeField] private float flashRadius = 50.0f;

		/// <summary>The lifetime duration of the cloud illumination flash in seconds.</summary>
		public float FlashDuration { set { flashDuration = value; } get { return flashDuration; } } [SerializeField] private float flashDuration = 0.3f;

		/// <summary>List of current active lightning parameters (xyz = dir, w = remaining life).</summary>
		public List<LightningData> ActiveLightnings = new List<LightningData>();

		[System.NonSerialized] private NativeList<float3> existingPoints;
		[System.NonSerialized] private Mesh               singleQuadMesh;
		[System.NonSerialized] private MaterialPropertyBlock properties;
		[System.NonSerialized] private float              strikeTimer;
		[System.NonSerialized] private float              mainCameraLightningDistance = -1.0f;

		[System.NonSerialized] private SgtCloud cachedCloud;

		[System.NonSerialized] private Vector4[] instancePositions = new Vector4[32];
		[System.NonSerialized] private Vector4[] instanceDatas     = new Vector4[32];

		[System.NonSerialized] private NativeList<float3> strikeCandidateResult;
		[System.NonSerialized] private NativeList<byte>   strikeFallbackCov;
		[System.NonSerialized] private JobHandle          strikeJobHandle;
		[System.NonSerialized] private bool               isStrikeJobRunning;

		private static int _SGT_RadiusData          = Shader.PropertyToID("_SGT_RadiusData");
		private static int _SGT_LightningBrightness = Shader.PropertyToID("_SGT_LightningBrightness");
		private static int _SGT_LightningThickness  = Shader.PropertyToID("_SGT_LightningThickness");
		private static int _SGT_AnimationMidpoint   = Shader.PropertyToID("_SGT_AnimationMidpoint");
		private static int _SGT_AnimationBrightness = Shader.PropertyToID("_SGT_AnimationBrightness");
		private static int _SGT_LightningPosition   = Shader.PropertyToID("_SGT_LightningPosition");
		private static int _SGT_LightningData       = Shader.PropertyToID("_SGT_LightningData");
		private static int _SGT_ObjectToWorld       = Shader.PropertyToID("_SGT_ObjectToWorld");
		private static int _SGT_WorldToObject       = Shader.PropertyToID("_SGT_WorldToObject");

		private bool isRegistered = false;

		public float MainCameraLightningDistance
		{
			get
			{
				return mainCameraLightningDistance;
			}
		}

		protected virtual void OnEnable()
		{
			if (properties == null) properties = new MaterialPropertyBlock();

			GenerateSingleQuadMesh();
			strikeTimer = CalculateNextInterval();

			if (!existingPoints.IsCreated)
			{
				existingPoints = new NativeList<float3>(Allocator.Persistent);
			}

			cachedCloud = GetComponentInParent<SgtCloud>();
			if (cachedCloud != null)
			{
				cachedCloud.OnUpdateWeather += HandleUpdateWeather;
			}

			RegisterCameraCallbacks();
		}

		protected virtual void OnDisable()
		{
			if (cachedCloud != null)
			{
				cachedCloud.OnUpdateWeather -= HandleUpdateWeather;
			}

			UnregisterCameraCallbacks();

			if (isStrikeJobRunning)
			{
				strikeJobHandle.Complete();
				if (strikeCandidateResult.IsCreated) strikeCandidateResult.Dispose();
				if (strikeFallbackCov.IsCreated)     strikeFallbackCov.Dispose();
				isStrikeJobRunning = false;
			}

			if (existingPoints.IsCreated)
			{
				existingPoints.Dispose();
			}

			if (singleQuadMesh != null)
			{
				DestroyImmediate(singleQuadMesh);
			}
		}

		private void RegisterCameraCallbacks()
		{
			if (isRegistered) return;

			isRegistered = true;
		}

		private void UnregisterCameraCallbacks()
		{
			if (!isRegistered) return;

			isRegistered = false;
		}

		protected virtual void LateUpdate()
		{
			if (cachedCloud == null) return;

			var innerR   = cachedCloud.Sky != null ? cachedCloud.Sky.InnerMeshRadius : cachedCloud.InnerRadius;
			var layerBot = 0.5f;
			var layerMid = 0.5f;
			var layerTop = 0.5f;
			var channel  = SgtCloud.ChannelType.Red;

			if (cachedCloud.CloudLayers != null && cloudLayerIndex >= 0 && cloudLayerIndex < cachedCloud.CloudLayers.Count)
			{
				var layer = cachedCloud.CloudLayers[cloudLayerIndex];

				layerBot = layer.Bottom;
				layerMid = (layer.Bottom + layer.Top) * 0.5f;
				layerTop = layer.Top;
				channel  = layer.SampleChannel;
			}
			
			var layerBottomRadius = Mathf.Lerp(cachedCloud.InnerRadius, cachedCloud.OuterRadius, layerBot);
			var layerTopRadius    = Mathf.Lerp(cachedCloud.InnerRadius, cachedCloud.OuterRadius, layerTop);
			var outerB            = Mathf.Lerp(cachedCloud.InnerRadius, cachedCloud.OuterRadius, layerBot);
			var outerR            = Mathf.Lerp(cachedCloud.InnerRadius, cachedCloud.OuterRadius, layerMid);

			var cam         = Camera.main;
			var worldCamPos = Vector3.zero;
			if (cam != null)
			{
				worldCamPos = cam.transform.position;
			}

			var localCamPos = cachedCloud.transform.InverseTransformPoint(worldCamPos);
			var camDist     = localCamPos.magnitude;
			var localCamDir = camDist > 0.0001f ? localCamPos / camDist : Vector3.up;
			var d           = math.max(camDist, math.max(innerR, 0.0001f));
			
			var root1       = math.sqrt(math.max(0.0f, d * d - innerR * innerR));
			var root2       = math.sqrt(math.max(0.0f, outerR * outerR - innerR * innerR));
			var cosTheta    = (innerR * innerR - root1 * root2) / (d * math.max(outerR, 0.0001f));
			var minCos      = math.clamp(cosTheta, 0.0f, 0.9999f);
			var clampDist   = math.clamp(camDist, layerBottomRadius, layerTopRadius);

			if (isStrikeJobRunning)
			{
				if (strikeJobHandle.IsCompleted)
				{
					strikeJobHandle.Complete();

					var nearestDistArray = new NativeArray<float>(1, Allocator.TempJob);

					var filterJob = new CombineAndFilterPointsJob
					{
						newPoints       = strikeCandidateResult.AsArray(),
						existingPoints  = existingPoints,
						localCamPos     = new float3(localCamPos.x, localCamPos.y, localCamPos.z),
						localToWorld    = cachedCloud.transform.localToWorldMatrix,
						camDir          = new float3(localCamDir.x, localCamDir.y, localCamDir.z),
						minCos          = minCos,
						minDistance     = lightningMinDistance,
						nearestDistance = nearestDistArray
					};

					filterJob.Run();

					mainCameraLightningDistance = nearestDistArray[0];

					nearestDistArray.Dispose();
					strikeCandidateResult.Dispose();
					if (strikeFallbackCov.IsCreated) strikeFallbackCov.Dispose();

					isStrikeJobRunning = false;
				}
			}

			if (!isStrikeJobRunning)
			{
				var coverageTex2D = cachedCloud.CoverageTex as Texture2D;
				var hasCov        = coverageTex2D != null && coverageTex2D.isReadable;
				var covWidth      = hasCov ? coverageTex2D.width : 1;
				var covHeight     = hasCov ? coverageTex2D.height : 1;
				var covBpp        = hasCov ? GetBytesPerPixel(coverageTex2D.format) : 0;

				var covData = default(NativeArray<byte>);
				if (hasCov)
				{
					covData = coverageTex2D.GetRawTextureData<byte>();
				}
				else
				{
					strikeFallbackCov = new NativeList<byte>(Allocator.TempJob);
					strikeFallbackCov.Add(255);
					covData = strikeFallbackCov.AsArray();
				}

				strikeCandidateResult = new NativeList<float3>(48, Allocator.TempJob);

				var job = new FindLightningStrikeJob
				{
					coverageData     = covData,
					coverageWidth    = covWidth,
					coverageHeight   = covHeight,
					hasCoverage      = hasCov,
					covBytesPerPixel = covBpp,
					channelIndex     = (int)channel,
					threshold        = lightningThreshold,
					random           = new Unity.Mathematics.Random((uint)UnityEngine.Random.Range(1, int.MaxValue)),
					camDir           = new float3(localCamDir.x, localCamDir.y, localCamDir.z),
					minCos           = minCos,
					bias             = lightningBias,
					minDistance      = lightningMinDistance,
					spawnDistance    = outerR,
					newPoints        = strikeCandidateResult
				};

				strikeJobHandle    = job.Schedule();
				isStrikeJobRunning = true;
			}
		}

		private void HandleUpdateWeather(Bounds bounds, Camera finalCamera)
		{
			if (cachedCloud == null) return;

			RegisterCameraCallbacks();

			var innerR   = cachedCloud.Sky != null ? cachedCloud.Sky.InnerMeshRadius : cachedCloud.InnerRadius;
			var layerBot = 0.5f;
			var layerMid = 0.5f;

			if (cachedCloud.CloudLayers != null && cloudLayerIndex >= 0 && cloudLayerIndex < cachedCloud.CloudLayers.Count)
			{
				var layer = cachedCloud.CloudLayers[cloudLayerIndex];

				layerBot = layer.Bottom;
				layerMid = (layer.Bottom + layer.Top) * 0.5f;
			}

			var outerB = Mathf.Lerp(cachedCloud.InnerRadius, cachedCloud.OuterRadius, layerBot);
			var outerR = Mathf.Lerp(cachedCloud.InnerRadius, cachedCloud.OuterRadius, layerMid);

			strikeTimer -= Time.deltaTime;

			if (strikeTimer <= 0.0f)
			{
				if (existingPoints.Length > 0)
				{
					var index = UnityEngine.Random.Range(0, existingPoints.Length);
					var dir   = existingPoints[index];
        
					existingPoints.RemoveAtSwapBack(index);

					var randAtlas = UnityEngine.Random.Range(0, 10000);

					ActiveLightnings.Add(new LightningData { Position = dir * outerB, Life = lightningDuration, Cell = randAtlas });
				}
    
				strikeTimer = CalculateNextInterval();
			}

			// Iterate backwards so we can safely remove dead strikes
			for (var i = ActiveLightnings.Count - 1; i >= 0; i--)
			{
				var state = ActiveLightnings[i];

				state.Life -= Time.deltaTime;

				if (state.Life <= 0.0f)
				{
					ActiveLightnings.RemoveAt(i);
				}
				else
				{
					ActiveLightnings[i] = state;

					// Calculate animation-matched brightness for SgtCloud volumetric pass
					var fade = state.Life / lightningDuration; // 1.0 (start) -> 0.0 (end)
					var normalizedTime = 1.0f - fade;       // 0.0 (start) -> 1.0 (end)
					var currentBrightness = 0.0f;

					if (normalizedTime < animationMidpoint)
					{
						// Reveal ramp-up phase
						currentBrightness = normalizedTime / Mathf.Max(0.0001f, animationMidpoint);
					}
					else
					{
						// Flash and fade-out phase (Return Stroke)
						var fadeOut = 1.0f - ((normalizedTime - animationMidpoint) / Mathf.Max(0.0001f, 1.0f - animationMidpoint));
						currentBrightness = fadeOut * animationBrightness;
					}

					cachedCloud.AddLightningLocal(state.Position, flashRadius, flashColor * flashColor.a * flashBrightness * currentBrightness);
				}
			}

			properties.SetVector(_SGT_RadiusData, new Vector4(innerR, outerR, 0.0f, 0.0f));
			properties.SetFloat(_SGT_LightningBrightness, lightningBrightness);
			properties.SetFloat(_SGT_LightningThickness, lightningThickness);
			properties.SetFloat(_SGT_AnimationMidpoint, animationMidpoint);
			properties.SetFloat(_SGT_AnimationBrightness, animationBrightness);
			
			if (singleQuadMesh == null) GenerateSingleQuadMesh();

			HandleDraw(bounds, finalCamera);
		}

		private float CalculateNextInterval()
		{
			var u1            = UnityEngine.Random.value;
			var u2            = UnityEngine.Random.value;
			var randStdNormal = Mathf.Sqrt(-2.0f * Mathf.Log(u1)) * Mathf.Sin(2.0f * Mathf.PI * u2);
			return Mathf.Max(0.1f, lightningInterval + lightningIntervalStdDev * randStdNormal);
		}

		private int GetBytesPerPixel(TextureFormat format)
		{
			switch (format)
			{
				case TextureFormat.Alpha8:
				case TextureFormat.R8:         return 1;
				case TextureFormat.RG16:
				case TextureFormat.R16:        return 2;
				case TextureFormat.RGB24:      return 3;
				case TextureFormat.RGBA32:
				case TextureFormat.ARGB32:
				case TextureFormat.BGRA32:     return 4;
				case TextureFormat.RGBAHalf:   return 8;
				case TextureFormat.RGBAFloat:  return 16;
				default:                       return 4;
			}
		}

		private void HandleDraw(Bounds bounds, Camera finalCamera)
		{
			if (singleQuadMesh == null || material == null || ActiveLightnings.Count == 0) return;

			for (var i = 0; i < 32; i++)
			{
				if (i < ActiveLightnings.Count)
				{
					var state = ActiveLightnings[i];

					instancePositions[i] = state.Position;
					instanceDatas[i] = new Vector4(state.Cell, Mathf.Clamp01(state.Life / lightningDuration));
				}
				else
				{
					instancePositions[i] = Vector3.zero;
					instanceDatas[i] = new Vector4(0, 0);
				}
			}

			properties.SetVectorArray(_SGT_LightningPosition, instancePositions);
			properties.SetVectorArray(_SGT_LightningData, instanceDatas);

			var center = cachedCloud != null ? cachedCloud.transform.position : transform.position;
			var matrix = Matrix4x4.TRS(center, transform.rotation, transform.lossyScale);
			var count  = Mathf.Min(ActiveLightnings.Count, 32);

			properties.SetMatrix(_SGT_ObjectToWorld, matrix);
			properties.SetMatrix(_SGT_WorldToObject, matrix.inverse);

			bounds.center += Vector3.Normalize(finalCamera.transform.position - bounds.center) * cameraOffset;

			Graphics.DrawMeshInstancedProcedural(singleQuadMesh, 0, material, bounds, count, properties, UnityEngine.Rendering.ShadowCastingMode.Off, false, gameObject.layer, finalCamera);
		}

		private void GenerateSingleQuadMesh()
		{
			var vertices  = new List<Vector3>(4);
			var uv        = new List<Vector4>(4);
			var triangles = new List<int>(6);

			vertices.Add(Vector3.zero);
			vertices.Add(Vector3.zero);
			vertices.Add(Vector3.zero);
			vertices.Add(Vector3.zero);

			uv.Add(new Vector4(-1.0f, -1.0f, 0.0f, 0.0f));
			uv.Add(new Vector4( 1.0f, -1.0f, 0.0f, 0.0f));
			uv.Add(new Vector4(-1.0f,  1.0f, 0.0f, 0.0f));
			uv.Add(new Vector4( 1.0f,  1.0f, 0.0f, 0.0f));

			triangles.Add(0); triangles.Add(1); triangles.Add(2);
			triangles.Add(3); triangles.Add(2); triangles.Add(1);

			singleQuadMesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
			singleQuadMesh.SetVertices(vertices);
			singleQuadMesh.SetUVs(0, uv);
			singleQuadMesh.SetTriangles(triangles, 0);
			singleQuadMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 50000.0f);
		}

		[BurstCompile]
		public struct FindLightningStrikeJob : IJob
		{
			[ReadOnly] public NativeArray<byte> coverageData;
		
			public int coverageWidth;
			public int coverageHeight;

			public bool hasCoverage;
			public int covBytesPerPixel;
			public int channelIndex;
		
			public float threshold;
			public Unity.Mathematics.Random random;

			public float3 camDir;
			public float minCos;
			public float bias;
			public float minDistance;
			public float spawnDistance;

			public NativeList<float3> newPoints;

			public void Execute()
			{
				var lookRot = quaternion.LookRotationSafe(camDir, new float3(0, 1, 0));

				for (var i = 0; i < 48; i++)
				{
					var t = random.NextFloat(0.0f, 1.0f);
				
					if (bias > 0.0f)
					{
						t = 1.0f - math.pow(1.0f - t, 1.0f + bias * 4.0f);
					}

					var allowedMaxZ = math.lerp(1.0f, minCos, minDistance);
					var z           = math.lerp(minCos, allowedMaxZ, t);
				
					var phi    = random.NextFloat(0.0f, 2.0f * math.PI);
					var radius = math.sqrt(math.max(0.0f, 1.0f - z * z));
				
					var localDir = new float3(radius * math.cos(phi), radius * math.sin(phi), z);
					var dir      = math.mul(lookRot, localDir);

					var lat = math.asin(math.clamp(dir.y, -1.0f, 1.0f));
					var lon = math.atan2(dir.z, dir.x);
				
					var u = lon / (2.0f * math.PI) + 0.5f;
					var v = lat / math.PI + 0.5f;

					var coverage = 1.0f;
				
					if (hasCoverage)
					{
						var cx            = math.clamp((int)(u * coverageWidth), 0, coverageWidth - 1);
						var cy            = math.clamp((int)(v * coverageHeight), 0, coverageHeight - 1);
						var pixelIndex    = cy * coverageWidth + cx;
						var byteIndex     = pixelIndex * covBytesPerPixel;
						var channelOffset = math.min(channelIndex, covBytesPerPixel - 1); 
						coverage          = coverageData[byteIndex + channelOffset] / 255f;
					}

					if (coverage > threshold)
					{
						var weatherMask = math.saturate((coverage - threshold) / math.max(0.0001f, 1.0f - threshold));

						if (random.NextFloat() < weatherMask)
						{
							newPoints.Add(dir * spawnDistance);
						}
					}
				}
			}
		}

		[BurstCompile]
		public struct CombineAndFilterPointsJob : IJob
		{
			[ReadOnly] public NativeArray<float3> newPoints;
			public NativeList<float3>             existingPoints;

			public float3 localCamPos;
			public float4x4 localToWorld;

			public float3 camDir;
			public float  minCos;
			public float  minDistance;

			public NativeArray<float> nearestDistance;

			public void Execute()
			{
				// Filter out older existing points that no longer meet the current frame's range criteria
				var allowedMaxZ = math.lerp(1.0f, minCos, minDistance);
				for (var i = existingPoints.Length - 1; i >= 0; i--)
				{
					var dir = math.normalize(existingPoints[i]);
					var dot = math.dot(dir, camDir);
					if (dot < minCos || dot > allowedMaxZ)
					{
						existingPoints.RemoveAtSwapBack(i);
					}
				}

				for (var i = 0; i < newPoints.Length && existingPoints.Length < 1024; i++)
				{
					existingPoints.Add(newPoints[i]);
				}

				var minDist     = float.MaxValue;
				var worldCamPos = math.transform(localToWorld, localCamPos);

				for (int i = 0; i < existingPoints.Length; i++)
				{
					var worldPos  = math.transform(localToWorld, existingPoints[i]);
					var worldDist = math.distance(worldCamPos, worldPos);

					minDist = math.min(minDist, worldDist);
				}

				nearestDistance[0] = (existingPoints.Length > 0) ? minDist : -1.0f;
			}
		}
	}
}

#if UNITY_EDITOR
namespace SpaceGraphicsToolkit.Cloud
{
	[UnityEditor.CanEditMultipleObjects]
	[UnityEditor.CustomEditor(typeof(SgtCloudLightning))]
	public class SgtCloudLightning_Editor : CwEditor
	{
		protected override void OnInspector()
		{
			SgtCloudLightning tgt; SgtCloudLightning[] tgts; GetTargets(out tgt, out tgts);

			Draw("cloudLayerIndex", "Which layer of the parent cloud to use (0..3).");
			Draw("material", "The instanced material used to render the lightning.");
			Draw("cameraOffset", "This allows you to offset the camera distance in world space when rendering the lightning, giving you fine control over the render order.");

			Separator();

			Draw("lightningThreshold", "Coverage value below this will have no lightning.");

			Separator();

			Draw("lightningBrightness", "The brightness multiplier for the lightning flash.");
			Draw("lightningThickness", "The width thickness of the lightning quad in world space.");
			Draw("lightningDuration", "The lifetime duration of a single lightning strike in seconds.");
			Draw("animationMidpoint", "The normalized midpoint of the animation (0 to 1) separating the reveal phase from the return stroke phase.");
			Draw("animationBrightness", "The brightness multiplier added during the return stroke phase.");
			Draw("lightningInterval", "The mean/average time interval between lightning strikes in seconds.");
			Draw("lightningIntervalStdDev", "The standard deviation for the normal distribution curve.");
			Draw("lightningBias", "Pinches the spawned random positions closer to the main camera direction line of sight (0 = default spread, 1 = heavily pinched).");
			Draw("lightningMinDistance", "Prevents lightning from spawning too close to the camera (0 = allows hitting screen, 1 = pushed fully to horizon bounds).");

			Separator();

			Draw("flashColor", "The color of the cloud illumination flash.");
			Draw("flashBrightness", "The brightness multiplier for the cloud illumination flash.");
			Draw("flashRadius", "The radius of the cloud illumination flash in local space.");
			Draw("flashDuration", "The lifetime duration of the cloud illumination flash in seconds.");

			Separator();

			BeginDisabled();
				UnityEditor.EditorGUILayout.FloatField("Main Cam Lightning Distance", tgt.MainCameraLightningDistance);
			EndDisabled();
		}
	}
}
#endif