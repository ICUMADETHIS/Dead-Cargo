using UnityEngine;
using CW.Common;
using System.Collections.Generic;
using SpaceGraphicsToolkit.LightAndShadow;

namespace SpaceGraphicsToolkit.Volumetrics
{
	/// <summary>This component must be added to your scene if you want to render volumetric effects like <b>SgtSky</b> or <b>SgtCloud</b>.</summary>
	[ExecuteInEditMode]
	[AddComponentMenu("Space Graphics Toolkit/SGT Volume Manager")]
	public class SgtVolumeManager : MonoBehaviour
	{
		public static LinkedList<SgtVolumeManager> Instances = new LinkedList<SgtVolumeManager>();

		/// <summary>This allows you to specify how the width/height of the volumetric effects are divided. For example, a value of 1 means no downscaling, and 5 means 1/5 width and height or 1/25 of the total pixels.</summary>
		public int Downscale { set { downscale = value; } get { return downscale; } } [SerializeField] [Range(1, 10)] private int downscale = 2;

		/// <summary>Use a higher precision color buffer for smoother volumetrics?</summary>
		public bool HighPrecision { set { highPrecision = value; } get { return highPrecision; } } [SerializeField] private bool highPrecision = true;

		/// <summary>Global wind velocity for cloud motion vectors to prevent TAA ghosting.</summary>
		public Vector3 WindVelocity { set { windVelocity = value; } get { return windVelocity; } } [SerializeField] private Vector3 windVelocity;

		/// <summary>Smooth volumetric effects over time?</summary>
		public float Taa { set { taa = value; } get { return taa; } } [SerializeField] [Range(0.0f, 1.0f)] private float taa;

		/// <summary>Renders volumetrics at a lower resolution and temporally reconstructs them over multiple frames to drastically improve performance.</summary>
		public int Checkerboard { set { checkerboard = value; } get { return checkerboard; } } [SerializeField] [Range(1, 8)] private int checkerboard = 1;

		/// <summary>This allows volumetric effects to render even if they're blocked by a nearby foreground object. This allows screen space reflections to work better in some scenarios.</summary>
		public float NearClip { set { nearClip = value; } get { return nearClip; } } [SerializeField] private float nearClip;

		[System.NonSerialized] private static Material  depthMaterial;
		[System.NonSerialized] private static int       passSceneDepth;
		[System.NonSerialized] private static Texture2D blueNoiseTex;
		[System.NonSerialized] private static Texture2D clearTexture;
		[System.NonSerialized] private static Texture3D noiseTexture;
		[System.NonSerialized] private static bool      noiseTextureCreated;

		[System.NonSerialized] private int checkerboardIndex;
		[System.NonSerialized] private int frameIndex;

		[System.NonSerialized] private Vector3 snapDelta;
		[System.NonSerialized] private bool    snapDeltaUsed;

		[System.NonSerialized] private LinkedListNode<SgtVolumeManager> node;

		[System.NonSerialized] private Dictionary<Camera, Vector3> lastCameraPositions = new Dictionary<Camera, Vector3>();

		private static List<SgtVolumeEffect> tempVolumeEffects = new List<SgtVolumeEffect>();

		private static int _SGT_Volumetrics_ColorTex  = Shader.PropertyToID("_SGT_Volumetrics_ColorTex");
		private static int _SGT_Volumetrics_ColorSize = Shader.PropertyToID("_SGT_Volumetrics_ColorSize");
		private static int _SGT_Volumetrics_DepthTex  = Shader.PropertyToID("_SGT_Volumetrics_DepthTex");
		private static int _SGT_Volumetrics_DepthSize = Shader.PropertyToID("_SGT_Volumetrics_DepthSize");
		private static int _SGT_Volumetrics_OceanTex  = Shader.PropertyToID("_SGT_Volumetrics_OceanTex");
		private static int _SGT_Volumetrics_OceanSize = Shader.PropertyToID("_SGT_Volumetrics_OceanSize");
		private static int _SGT_Volumetrics_CavityTex = Shader.PropertyToID("_SGT_Volumetrics_CavityTex");
		private static int _SGT_Volumetrics_Downscale = Shader.PropertyToID("_SGT_Volumetrics_Downscale");
		private static int _SGT_Volumetrics_NearClip  = Shader.PropertyToID("_SGT_Volumetrics_NearClip");
		private static int _SGT_SceneDepthTexture     = Shader.PropertyToID("_SGT_SceneDepthTexture");
		private static int _SGT_BlueNoiseTex          = Shader.PropertyToID("_SGT_BlueNoiseTex");
		private static int _SGT_Frame                 = Shader.PropertyToID("_SGT_Frame");
		private static int _SGT_TAA                   = Shader.PropertyToID("_SGT_TAA");
		private static int _SGT_PrevViewProj          = Shader.PropertyToID("_SGT_PrevViewProj");
		private static int _SGT_InvViewProj           = Shader.PropertyToID("_SGT_InvViewProj");
		private static int _SGT_WindVelocity          = Shader.PropertyToID("_SGT_WindVelocity");
		private static int _SGT_Jitter                = Shader.PropertyToID("_SGT_Jitter");
		private static int _SGT_Checkerboard          = Shader.PropertyToID("_SGT_Checkerboard");
		private static int _SGT_CameraVelocity        = Shader.PropertyToID("_SGT_CameraVelocity");

		public static Texture3D NoiseTexture
		{
			get
			{
				if (noiseTextureCreated == false)
				{
					Unity.Mathematics.Random rng = new Unity.Mathematics.Random(1337);

					noiseTexture        = new Texture3D(32, 32, 32, TextureFormat.R8, false);
					noiseTextureCreated = true;

					noiseTexture.hideFlags  = HideFlags.DontSave;
					noiseTexture.wrapMode   = TextureWrapMode.Repeat;
					noiseTexture.filterMode = FilterMode.Bilinear;

					var data = noiseTexture.GetPixelData<byte>(0);

					for (int i = 0; i < data.Length; i++)
					{
						data[i] = (byte)(rng.NextFloat() * 255f);
					}

					noiseTexture.Apply(false);

				}

				return noiseTexture;
			}
		}

		public static Texture2D ClearTexture
		{
			get
			{
				if (clearTexture == null)
				{
					clearTexture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
					clearTexture.SetPixel(0, 0, Color.clear);
					clearTexture.Apply();
				}

				return clearTexture;
			}
		}

		public Material DepthMaterial
		{
			get
			{
				return depthMaterial;
			}
		}

		public int PassSceneDepth
		{
			get
			{
				return passSceneDepth;
			}
		}

		public static Texture2D BlueNoiseTex
		{
			get
			{
				if (blueNoiseTex == null)
				{
					blueNoiseTex = new Texture2D(64, 64, TextureFormat.R8, false, true);

					blueNoiseTex.wrapMode   = TextureWrapMode.Repeat;
					blueNoiseTex.filterMode = FilterMode.Point;

					blueNoiseTex.LoadImage(System.Convert.FromBase64String(
						"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"));
				}

				return blueNoiseTex;
			}
		}

		public void OnSnap(Vector3 delta)
		{
			snapDelta = delta;
		}

		protected virtual void OnEnable()
		{
			node = Instances.AddLast(this);

			UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += HandleCameraStart;
			Camera.onPreCull += HandleCameraStartBiRP;
		}

		protected virtual void OnDisable()
		{
			Instances.Remove(node); node = null;

			UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= HandleCameraStart;
			Camera.onPreCull -= HandleCameraStartBiRP;

			lastCameraPositions.Clear();
		}

		protected virtual void Update()
		{
			if (snapDeltaUsed == true)
			{
				snapDelta     = Vector3.zero;
				snapDeltaUsed = false;
			}
		}

		protected virtual void LateUpdate()
		{
			checkerboard = Mathf.Clamp(checkerboard, 1, 8);

			if (checkerboard > 1)
			{
				checkerboardIndex += 1;

				if (checkerboardIndex >= checkerboard * checkerboard)
				{
					checkerboardIndex %= checkerboard * checkerboard;

					frameIndex += 1;
				}
			}
			else
			{
				frameIndex += 1;
			}

			Shader.SetGlobalTexture(_SGT_BlueNoiseTex, BlueNoiseTex);
			Shader.SetGlobalFloat(_SGT_Frame, frameIndex);
		}

		private void HandleCameraStartBiRP(Camera camera)
		{
			HandleCameraStart(default, camera);
		}

		private void HandleCameraStart(UnityEngine.Rendering.ScriptableRenderContext ctx, Camera camera)
		{
			if (camera.cameraType == CameraType.SceneView)
			{
				Shader.SetGlobalTexture(_SGT_Volumetrics_ColorTex, ClearTexture);
				Shader.SetGlobalTexture(_SGT_Volumetrics_OceanTex, ClearTexture);
				Shader.SetGlobalTexture(_SGT_Volumetrics_CavityTex, ClearTexture);
			}
		}

		public bool PrepareEffects(SgtVolumeCamera volumeCamera)
		{
			if (depthMaterial == null)
			{
				depthMaterial  = CwHelper.CreateTempMaterial("Depth Mat", "Hidden/SgtVolumeDepth");
				passSceneDepth = depthMaterial.FindPass("SceneDepth");
			}

			var cb = Mathf.Clamp(checkerboard, 1, 8);

			if (cb > 1)
			{
				var cx = checkerboardIndex % cb;
				var cy = checkerboardIndex / cb;

				var jitterX = (cx * 2.0f) / volumeCamera.WholeColor.width;
				var jitterY = (cy * 2.0f) / volumeCamera.WholeColor.height;

				Shader.SetGlobalVector(_SGT_Jitter, new Vector4(-jitterX, jitterY, cb, 0));
				Shader.SetGlobalVector(_SGT_Checkerboard, new Vector4(cx, cy, cb, 0));
			}
			else
			{
				Shader.SetGlobalVector(_SGT_Jitter, Vector4.zero);
				Shader.SetGlobalVector(_SGT_Checkerboard, new Vector4(0, 0, 1, 0));
			}

			snapDeltaUsed = true;

			Shader.SetGlobalVector(_SGT_WindVelocity, windVelocity * Time.deltaTime - snapDelta);
			Shader.SetGlobalFloat(_SGT_Volumetrics_Downscale, downscale);
			Shader.SetGlobalFloat(_SGT_Volumetrics_NearClip, nearClip);
			Shader.SetGlobalTexture(_SGT_SceneDepthTexture, volumeCamera.SceneDepth);
			Shader.SetGlobalTexture(_SGT_Volumetrics_ColorTex, volumeCamera.WholeColor);
			Shader.SetGlobalVector(_SGT_Volumetrics_ColorSize, new Vector4(volumeCamera.WholeColor.width, volumeCamera.WholeColor.height, 1.0f / volumeCamera.WholeColor.width, 1.0f / volumeCamera.WholeColor.height));
			Shader.SetGlobalTexture(_SGT_Volumetrics_DepthTex, volumeCamera.WholeDepth);
			Shader.SetGlobalVector(_SGT_Volumetrics_DepthSize, new Vector4(volumeCamera.WholeDepth.width, volumeCamera.WholeDepth.height, 1.0f / volumeCamera.WholeDepth.width, 1.0f / volumeCamera.WholeDepth.height));
			Shader.SetGlobalTexture(_SGT_Volumetrics_OceanTex, volumeCamera.OceanDepth);
			Shader.SetGlobalVector(_SGT_Volumetrics_OceanSize, new Vector4(volumeCamera.OceanDepth.width, volumeCamera.OceanDepth.height, 1.0f / volumeCamera.OceanDepth.width, 1.0f / volumeCamera.OceanDepth.height));
			Shader.SetGlobalTexture(_SGT_Volumetrics_CavityTex, volumeCamera.CavityDepth);

			return true;
		}

		public void UpdateEffects(SgtVolumeCamera vc)
		{
			var currentCam = vc.CachedCamera;
			if (currentCam == null) return;

			var cameraVelocity = Vector3.zero;
			var dt             = Time.deltaTime;

			if (dt > 0.0f)
			{
				if (lastCameraPositions.TryGetValue(currentCam, out var lastPos) == true)
				{
					// Adding snapDelta compensates for the coordinate system shift
					cameraVelocity = (currentCam.transform.position - lastPos - snapDelta) / dt;
				}

				lastCameraPositions[currentCam] = currentCam.transform.position;
			}

			Shader.SetGlobalVector(_SGT_CameraVelocity, cameraVelocity);

			var cb        = Mathf.Clamp(checkerboard, 1, 8);
			var fullSize  = new Vector2Int(vc.WholeColor.width, vc.WholeColor.height);
			var hasCavity = false;

			tempVolumeEffects.Clear();

			foreach (var volumeEffect in SgtVolumeEffect.Instances)
			{
				volumeEffect.CalculateSortDistance(currentCam.transform.position);

				tempVolumeEffects.Add(volumeEffect);

				if (volumeEffect.IsCavity == true)
				{
					hasCavity = true;
				}
			}

			tempVolumeEffects.Sort((a, b) => b.SortDistance.CompareTo(a.SortDistance));

			// Render Ocean
			SgtVolumeCamera.AddMRT(vc.OceanDepth.colorBuffer, vc.OceanDepth.colorBuffer, Color.clear);
			foreach (var volumeEffect in tempVolumeEffects)
			{
				volumeEffect.RenderOceanBuffer(this, currentCam, 0, fullSize);
			}

			if (hasCavity == true)
			{
				SgtVolumeCamera.AddMRT(vc.CavityDepth.colorBuffer, vc.CavityDepth.colorBuffer, new Color(99999999.0f, 0f, 0f, 0f));

				// Pass 0: Front Faces -> Writes to Red channel
				foreach (var volumeEffect in tempVolumeEffects)
				{
					if (volumeEffect.IsCavity) volumeEffect.RenderCavityBuffer(this, currentCam, 0);
				}

				// Pass 1: Back Faces -> Writes to Green channel
				foreach (var volumeEffect in tempVolumeEffects)
				{
					if (volumeEffect.IsCavity) volumeEffect.RenderCavityBuffer(this, currentCam, 1);
				}
    
				// Globally share the single packed buffer to your project shaders
				Shader.SetGlobalTexture("_SGT_CavityDepthTex", vc.CavityDepth);
			}

			var v = currentCam.worldToCameraMatrix;
			var p = GL.GetGPUProjectionMatrix(currentCam.projectionMatrix, false);
			p = Matrix4x4.Perspective(currentCam.fieldOfView, currentCam.aspect, 1.0f, 100.0f);
			var vp = p * v;

			if (taa > 0.0f || cb > 1)
			{
				vc.FilterMaterial.SetFloat(_SGT_TAA, Mathf.Lerp(0.01f, 1.0f, taa));
				vc.FilterMaterial.SetMatrix(_SGT_InvViewProj, vp.inverse);
				vc.FilterMaterial.SetMatrix(_SGT_PrevViewProj, vc.PrevVP);

				var renderSize = new Vector2Int(vc.NewColor.width, vc.NewColor.height);

				// Render clouds to temp
				SgtVolumeCamera.AddMRT(vc.NewColor.colorBuffer, vc.NewDepth.colorBuffer, vc.NewDepth.depthBuffer, Color.clear);
				foreach (var volumeEffect in tempVolumeEffects)
				{
					volumeEffect.RenderBuffers(this, currentCam, 0, renderSize, fullSize);
				}

				// Combine old + temp to whole
				SgtVolumeCamera.AddMRT(vc.WholeColor.colorBuffer, vc.WholeDepth.colorBuffer, vc.WholeDepth.depthBuffer, Color.clear);
				SgtVolumeCamera.AddDrawMesh(CwHelper.GetQuadMesh(), 0, Matrix4x4.identity, vc.FilterMaterial, vc.PassTAA);

				// Copy whole to old
				SgtVolumeCamera.AddMRT(vc.OldColor.colorBuffer, vc.OldDepth.colorBuffer, vc.OldDepth.depthBuffer, Color.clear);
				SgtVolumeCamera.AddDrawMesh(CwHelper.GetQuadMesh(), 0, Matrix4x4.identity, vc.FilterMaterial, vc.PassTAA2);

				// TODO: Find out why this needs to be set to wholeColor/wholeDepth at the end
				SgtVolumeCamera.AddMRT(vc.WholeColor.colorBuffer, vc.WholeDepth.colorBuffer, vc.WholeDepth.depthBuffer);
			}
			else
			{
				SgtVolumeCamera.AddMRT(vc.WholeColor.colorBuffer, vc.WholeDepth.colorBuffer, vc.WholeDepth.depthBuffer, Color.clear);
				foreach (var volumeEffect in tempVolumeEffects)
				{
					volumeEffect.RenderBuffers(this, currentCam, 0, fullSize, fullSize);
				}
			}

			vc.PrevVP = vp;
		}
	}
}

#if UNITY_EDITOR
namespace SpaceGraphicsToolkit.Volumetrics
{
	class SgtAssetPostprocessor : UnityEditor.AssetPostprocessor
	{
		static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
		{
			/*
			foreach (var landscape in SgtVolumeManager.Instances)
			{
				landscape.MarkForRebuild();
			}
			*/
		}
	}

	[UnityEditor.CanEditMultipleObjects]
	[UnityEditor.CustomEditor(typeof(SgtVolumeManager))]
	public class SgtVolumeManager_Editor : CwEditor
	{
		public static void Require()
		{
			if (SgtVolumeManager.Instances.Count == 0)
			{
				Separator();

				if (CwEditor.HelpButton("This component requires your scene to have the " + typeof(SgtVolumeManager).Name + " component, but it doesn't.", UnityEditor.MessageType.Error, "Fix", 50.0f) == true)
				{
					var vm = new GameObject("SgtVolumeManager").AddComponent<SgtVolumeManager>();

					CwHelper.SelectAndPing(vm);
				}
			}
		}

		protected override void OnInspector()
		{
			SgtVolumeManager tgt; SgtVolumeManager[] tgts; GetTargets(out tgt, out tgts);

			Draw("downscale", "This allows you to specify how the width/height of the volumetric effects are divided. For example, a value of 1 means no downscaling, and 5 means 1/5 width and height or 1/25 of the total pixels.");
			Draw("highPrecision", "Use a higher precision color buffer for smoother volumetrics?");
			Draw("windVelocity", "Global wind velocity for cloud motion vectors to prevent TAA ghosting.");
			Draw("taa", "Smooth volumetric effects over time?");
			Draw("checkerboard", "Renders volumetrics at a lower resolution and temporally reconstructs them over multiple frames. 2 = 1/4th pixels, 3 = 1/9th pixels.");
			Draw("nearClip", "This allows volumetric effects to render even if they're blocked by a nearby foreground object. This allows screen space reflections to work better in some scenarios.");
		}
	}
}
#endif