using UnityEngine;
using CW.Common;

namespace SpaceGraphicsToolkit.Cloud
{
	/// <summary>This component generates an amortized depth texture via CPU raycasting to occlude precipitation particles against the environment.</summary>
	[ExecuteInEditMode]
	[AddComponentMenu("Space Graphics Toolkit/SGT Cloud Precipitation Depth")]
	public class SgtCloudPrecipitationDepth : MonoBehaviour
	{
		/// <summary>Which layer of the parent cloud to use (0..3).</summary>
		public int CloudLayerIndex { set { cloudLayerIndex = value; } get { return cloudLayerIndex; } } [SerializeField] [Range(0, 3)] private int cloudLayerIndex = 0;

		/// <summary>The layer mask used when raycasting to find the surface depth.</summary>
		public LayerMask DepthMask { set { depthMask = value; } get { return depthMask; } } [SerializeField] private LayerMask depthMask = -1;

		/// <summary>The width and height of the generated depth texture.</summary>
		public int DepthResolution { set { depthResolution = value; } get { return depthResolution; } } [SerializeField] private int depthResolution = 64;

		/// <summary>How many raycasts are performed per second to amortize the performance cost.</summary>
		public float DepthRaysPerSecond { set { depthRaysPerSecond = value; } get { return depthRaysPerSecond; } } [SerializeField] private float depthRaysPerSecond = 500.0f;

		/// <summary>If the camera moves further than this distance from the grid center, the grid will snap to the camera and rebuild.</summary>
		public float DepthRebaseDistance { set { depthRebaseDistance = value; } get { return depthRebaseDistance; } } [SerializeField] private float depthRebaseDistance = 5.0f;

		/// <summary>The maximum distance of the raycasts, matching the precipitation range.</summary>
		public float Range { set { range = value; } get { return range; } } [SerializeField] private float range = 10.0f;

		[System.NonSerialized] private Texture2D  depthTex;
		[System.NonSerialized] private Color[]    depthColors;
		[System.NonSerialized] private Vector3    gridLocalCenter;
		[System.NonSerialized] private Quaternion gridLocalRotation;
		[System.NonSerialized] private float      updateAccumulator;
		[System.NonSerialized] private int        updateIndex;
		[System.NonSerialized] private SgtCloud   cachedCloud;
		[System.NonSerialized] private Vector3    lastCameraPosition;

		private static readonly int _SGT_PrecipitationDepthTex    = Shader.PropertyToID("_SGT_PrecipitationDepthTex");
		private static readonly int _SGT_PrecipitationDepthMatrix = Shader.PropertyToID("_SGT_PrecipitationDepthMatrix");
		private static readonly int _SGT_PrecipitationExists      = Shader.PropertyToID("_SGT_PrecipitationExists");

		private float GetCloudOuterRadius()
		{
			if (cachedCloud == null) return 0.0f;
			var layerMid = 0.5f;
			if (cachedCloud.CloudLayers != null && cloudLayerIndex >= 0 && cloudLayerIndex < cachedCloud.CloudLayers.Count)
			{
				var layer = cachedCloud.CloudLayers[cloudLayerIndex];
				layerMid = (layer.Bottom + layer.Top) * 0.5f;
			}
			return Mathf.Lerp(cachedCloud.InnerRadius, cachedCloud.OuterRadius, layerMid);
		}

		protected virtual void OnEnable()
		{
			cachedCloud = GetComponentInParent<SgtCloud>();
			Camera.onPreCull += HandleCameraPreRender;
			UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += HandleCameraPreRender;
		}

		protected virtual void OnDisable()
		{
			Camera.onPreCull -= HandleCameraPreRender;
			UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= HandleCameraPreRender;

			if (depthTex != null)
			{
				DestroyImmediate(depthTex);
			}

			Shader.SetGlobalFloat(_SGT_PrecipitationExists, 0.0f);
		}

		private void HandleCameraPreRender(UnityEngine.Rendering.ScriptableRenderContext context, Camera camera)
		{
			HandleCameraPreRender(camera);
		}

		private void HandleCameraPreRender(Camera camera)
		{
			if (depthTex == null || depthTex.width != depthResolution)
			{
				if (depthTex != null) DestroyImmediate(depthTex);
				
				depthTex          = new Texture2D(depthResolution, depthResolution, TextureFormat.RFloat, false);
				depthTex.wrapMode = TextureWrapMode.Clamp;
				depthColors       = new Color[depthResolution * depthResolution];
				
				RebuildDepthGrid(camera);
			}
			else if (Vector3.Distance(camera.transform.position, lastCameraPosition) > depthRebaseDistance)
			{
				RebuildDepthGrid(camera);
			}

			UpdateDepthGrid();
			ApplyGlobalProperties();
		}

		private void UpdateDepthGrid()
		{
			updateAccumulator += Time.deltaTime * depthRaysPerSecond;
			var updatesThisFrame = Mathf.FloorToInt(updateAccumulator);

			if (updatesThisFrame > 0)
			{
				updateAccumulator -= updatesThisFrame;
				
				var size     = range * 2.0f;
				var worldPos = transform.TransformPoint(gridLocalCenter);
				var worldRot = transform.rotation * gridLocalRotation;

				var planet     = transform.position;
				var outerR     = GetCloudOuterRadius();
				var cameraDist = Vector3.Distance(lastCameraPosition, planet);
				var rayLength  = (outerR - cameraDist) + range;

				for (var i = 0; i < updatesThisFrame; i++)
				{
					var x = updateIndex % depthResolution;
					var y = updateIndex / depthResolution;

					depthColors[updateIndex].r = RaycastPixel(x, y, size, worldPos, worldRot, rayLength);

					updateIndex = (updateIndex + 1) % depthColors.Length;
				}

				depthTex.SetPixels(depthColors);
				depthTex.Apply();
			}
		}

		private void RebuildDepthGrid(Camera camera)
		{
			lastCameraPosition = camera.transform.position;
			var center = camera.transform.position;
			var planet = transform.position;
			var dir    = (planet - center).normalized;
			if (dir == Vector3.zero) dir = Vector3.down;

			var outerR      = GetCloudOuterRadius();
			var worldCenter = planet - dir * outerR;

			gridLocalCenter   = transform.InverseTransformPoint(worldCenter);
			gridLocalRotation = Quaternion.Inverse(transform.rotation) * Quaternion.LookRotation(dir);

			var size     = range * 2.0f;
			var step     = 4;
			var worldRot = transform.rotation * gridLocalRotation;

			var cameraDist = Vector3.Distance(center, planet);
			var rayLength  = (outerR - cameraDist) + range;

			for (var y = 0; y < depthResolution; y += step)
			{
				for (var x = 0; x < depthResolution; x += step)
				{
					var depth = RaycastPixel(x, y, size, worldCenter, worldRot, rayLength);

					for (var dy = 0; dy < step; dy++)
					{
						var wy = y + dy;
						if (wy >= depthResolution) continue;

						for (var dx = 0; dx < step; dx++)
						{
							var wx = x + dx;
							if (wx >= depthResolution) continue;

							depthColors[wy * depthResolution + wx].r = depth;
						}
					}
				}
			}

			depthTex.SetPixels(depthColors);
			depthTex.Apply();
			updateIndex = 0;
		}

		private float RaycastPixel(int x, int y, float size, Vector3 worldCenter, Quaternion worldRot, float rayLength)
		{
			var u        = x / (float)Mathf.Max(1, depthResolution - 1);
			var v        = y / (float)Mathf.Max(1, depthResolution - 1);
			var localPos = new Vector3((u - 0.5f) * size, (v - 0.5f) * size, 0.0f);
			
			var rayStart = worldCenter + worldRot * localPos;
			var rayDir   = worldRot * Vector3.forward;

			if (Physics.Raycast(rayStart, rayDir, out var hit, rayLength, depthMask))
			{
				return hit.distance;
			}
			
			return rayLength;
		}

		private void ApplyGlobalProperties()
		{
			var size        = range * 2.0f;
			var worldCenter = transform.TransformPoint(gridLocalCenter);
			var worldRot    = transform.rotation * gridLocalRotation;

			// Bake UV centering (XY) directly into the matrix. Z coordinate represents distance straight down from cloud layer top.
			var uvOffset   = Matrix4x4.Translate(new Vector3(0.5f, 0.5f, 0.0f));
			var uvScale    = Matrix4x4.Scale(new Vector3(1.0f / size, 1.0f / size, 1.0f));
			var worldToObj = Matrix4x4.TRS(worldCenter, worldRot, Vector3.one).inverse;

			var matrix = uvOffset * uvScale * worldToObj;

			Shader.SetGlobalTexture(_SGT_PrecipitationDepthTex, depthTex);
			Shader.SetGlobalMatrix(_SGT_PrecipitationDepthMatrix, matrix);
			Shader.SetGlobalFloat(_SGT_PrecipitationExists, 1.0f);
		}
	}
}

#if UNITY_EDITOR
namespace SpaceGraphicsToolkit.Cloud
{
	using UnityEditor;

	[CanEditMultipleObjects]
	[CustomEditor(typeof(SgtCloudPrecipitationDepth))]
	public class SgtCloudPrecipitationDepth_Editor : CwEditor
	{
		protected override void OnInspector()
		{
			SgtCloudPrecipitationDepth tgt; SgtCloudPrecipitationDepth[] tgts; GetTargets(out tgt, out tgts);

			Draw("cloudLayerIndex", "Which layer of the parent cloud to use (0..3).");
			Draw("depthMask", "The layer mask used when raycasting to find the surface depth.");
			Draw("depthResolution", "The width and height of the generated depth texture.");
			Draw("depthRaysPerSecond", "How many raycasts are performed per second to amortize the performance cost.");
			Draw("depthRebaseDistance", "If the camera moves further than this distance from the grid center, the grid will snap to the camera and rebuild.");
			Draw("range", "The maximum distance of the raycasts, matching the precipitation range.");
		}
	}
}
#endif