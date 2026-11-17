#if __URP__ && UNITY_6000_0_OR_NEWER
	#define __HAS_RENDER_GRAPH__
#endif

#if __URP__
using UnityEngine.Rendering.Universal;
#elif __HDRP__
using UnityEngine.Rendering.HighDefinition;
#endif
#if __HAS_RENDER_GRAPH__
using UnityEngine.Rendering.RenderGraphModule;
#endif
using UnityEngine;
using UnityEngine.Rendering;

namespace SpaceGraphicsToolkit.Volumetrics
{
	/// <summary>This component can be added alongside your scene camera to render volumetric effects.</summary>
	[ExecuteInEditMode]
	[RequireComponent(typeof(Camera))]
	[AddComponentMenu("Space Graphics Toolkit/SGT Volume Camera")]
	public class SgtVolumeCamera : MonoBehaviour
	{
		public Camera CachedCamera { get { return cachedCamera; } } [System.NonSerialized] private Camera cachedCamera;

		public RenderTexture WholeColor { get { return wholeColor; } } [System.NonSerialized] private RenderTexture wholeColor;

		public RenderTexture WholeDepth { get { return wholeDepth; } } [System.NonSerialized] private RenderTexture wholeDepth;

		public RenderTexture OceanDepth { get { return oceanDepth; } } [System.NonSerialized] private RenderTexture oceanDepth;

		public RenderTexture CavityDepth { get { return cavityDepth; } } [System.NonSerialized] private RenderTexture cavityDepth;

		public RenderTexture SceneDepth { get { return sceneDepth; } } [System.NonSerialized] private RenderTexture sceneDepth;

		public Material FilterMaterial { get { return filterMaterial; } } [System.NonSerialized] private Material filterMaterial;

		public int PassTAA { get { return passTAA; } } [System.NonSerialized] private int passTAA;

		public int PassTAA2 { get { return passTAA2; } } [System.NonSerialized] private int passTAA2;

		// TAA History Buffers
		public RenderTexture OldColor { get { return oldColor; } } [System.NonSerialized] private RenderTexture oldColor;
		public RenderTexture OldDepth { get { return oldDepth; } } [System.NonSerialized] private RenderTexture oldDepth;

		public RenderTexture NewColor { get { return newColor; } } [System.NonSerialized] private RenderTexture newColor;

		public RenderTexture NewDepth { get { return newDepth; } } [System.NonSerialized] private RenderTexture newDepth;

		public Matrix4x4 PrevVP = Matrix4x4.identity;

		private static int _SGT_Volumetrics_NewSize      = Shader.PropertyToID("_SGT_Volumetrics_NewSize");
		private static int _SGT_Volumetrics_ColorTex_New = Shader.PropertyToID("_SGT_Volumetrics_ColorTex_New");
		private static int _SGT_Volumetrics_DepthTex_New = Shader.PropertyToID("_SGT_Volumetrics_DepthTex_New");
		private static int _SGT_Volumetrics_ColorTex_Old = Shader.PropertyToID("_SGT_Volumetrics_ColorTex_Old");
		private static int _SGT_Volumetrics_DepthTex_Old = Shader.PropertyToID("_SGT_Volumetrics_DepthTex_Old");

		public bool PrepareBuffers(int downscale, bool highPrecision, bool taa, int checkerboard)
		{
			if (CachedCamera == null) return false;

			var cb     = Mathf.Clamp(checkerboard, 1, 8);
			var highW  = Mathf.Max(1, CachedCamera.pixelWidth / downscale);
			var highH  = Mathf.Max(1, CachedCamera.pixelHeight / downscale);
			var lowW   = Mathf.Max(1, Mathf.CeilToInt(highW / (float)cb));
			var lowH   = Mathf.Max(1, Mathf.CeilToInt(highH / (float)cb));
			var format = highPrecision ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32;

			if (wholeColor != null && (wholeColor.width != highW || wholeColor.height != highH || wholeColor.format != format))
			{
				ReleaseBuffers();
			}

			if (wholeColor == null)
			{
				wholeColor  = RenderTexture.GetTemporary(highW, highH, 0, format);
				wholeDepth  = RenderTexture.GetTemporary(highW, highH, 0, RenderTextureFormat.RFloat);
				sceneDepth  = RenderTexture.GetTemporary(highW, highH, 0, RenderTextureFormat.RFloat);
				oceanDepth  = RenderTexture.GetTemporary(CachedCamera.pixelWidth, CachedCamera.pixelHeight, 24, RenderTextureFormat.ARGBHalf);
				cavityDepth = RenderTexture.GetTemporary(highW, highH, 0, RenderTextureFormat.RGHalf);
			}

			if (filterMaterial == null || filterMaterial.shader == null)
			{
				if (filterMaterial != null) DestroyImmediate(filterMaterial);

				filterMaterial = CW.Common.CwHelper.CreateTempMaterial("Filter Mat", "Hidden/SgtVolumeFilter");
				passTAA        = filterMaterial.FindPass("TAA");
				passTAA2       = filterMaterial.FindPass("TAA2");
			}

			// Enable history buffers if either TAA or Checkerboard is active
			if (taa == true || cb > 1)
			{
				if (oldColor == null || oldColor.width != highW || oldColor.height != highH || oldColor.format != format || newColor == null || newColor.width != lowW)
				{
					if (oldColor != null) RenderTexture.ReleaseTemporary(oldColor);
					if (oldDepth != null) RenderTexture.ReleaseTemporary(oldDepth);
					if (newColor != null) RenderTexture.ReleaseTemporary(newColor);
					if (newDepth != null) RenderTexture.ReleaseTemporary(newDepth);

					// History buffers remain full size
					oldColor = RenderTexture.GetTemporary(highW, highH, 0, format);
					oldDepth = RenderTexture.GetTemporary(highW, highH, 0, RenderTextureFormat.RFloat);

					// Temp buffers are downscaled for the checkerboard
					newColor = RenderTexture.GetTemporary(lowW, lowH, 0, format);
					newDepth = RenderTexture.GetTemporary(lowW, lowH, 0, RenderTextureFormat.RFloat);
            
					Graphics.Blit(Texture2D.blackTexture, oldColor);
					Graphics.Blit(Texture2D.blackTexture, oldDepth);
				}

				filterMaterial.SetVector(_SGT_Volumetrics_NewSize, new Vector4(lowW, lowH, 1.0f / lowW, 1.0f / lowH));
				filterMaterial.SetTexture(_SGT_Volumetrics_ColorTex_New, newColor);
				filterMaterial.SetTexture(_SGT_Volumetrics_DepthTex_New, newDepth);
				filterMaterial.SetTexture(_SGT_Volumetrics_ColorTex_Old, oldColor);
				filterMaterial.SetTexture(_SGT_Volumetrics_DepthTex_Old, oldDepth);
			}
			else
			{
				if (oldColor != null) { RenderTexture.ReleaseTemporary(oldColor); oldColor = null; }
				if (oldDepth != null) { RenderTexture.ReleaseTemporary(oldDepth); oldDepth = null; }
				if (newColor != null) { RenderTexture.ReleaseTemporary(newColor); newColor = null; }
				if (newDepth != null) { RenderTexture.ReleaseTemporary(newDepth); newDepth = null; }
			}

			return true;
		}

		public void ReleaseBuffers()
		{
			if (wholeColor  != null) { RenderTexture.ReleaseTemporary(wholeColor);  wholeColor  = null; }
			if (wholeDepth  != null) { RenderTexture.ReleaseTemporary(wholeDepth);  wholeDepth  = null; }
			if (sceneDepth  != null) { RenderTexture.ReleaseTemporary(sceneDepth);  sceneDepth  = null; }
			if (oceanDepth  != null) { RenderTexture.ReleaseTemporary(oceanDepth);  oceanDepth  = null; }
			if (cavityDepth != null) { RenderTexture.ReleaseTemporary(cavityDepth); cavityDepth = null; }
			if (oldColor    != null) { RenderTexture.ReleaseTemporary(oldColor);    oldColor    = null; }
			if (oldDepth    != null) { RenderTexture.ReleaseTemporary(oldDepth);    oldDepth    = null; }
			if (newColor    != null) { RenderTexture.ReleaseTemporary(newColor);    newColor    = null; }
			if (newDepth    != null) { RenderTexture.ReleaseTemporary(newDepth);    newDepth    = null; }
		}

#if __HDRP__
		private CustomPassVolume customPassVolume;

		private CustomDepthPass customDepthPass;

		private static RenderTargetIdentifier[] identifiers1 = new RenderTargetIdentifier[1];
		private static RenderTargetIdentifier[] identifiers2 = new RenderTargetIdentifier[2];

		public static void AddMRT(RenderBuffer a, RenderBuffer depth)
		{
			identifiers1[0] = a;

			CustomDepthPass.CurrentContext.cmd.SetRenderTarget(identifiers1, depth);
		}

		public static void AddMRT(RenderBuffer a, RenderBuffer depth, Color defaultColor)
		{
			identifiers1[0] = a;

			CustomDepthPass.CurrentContext.cmd.SetRenderTarget(identifiers1, depth);
			CustomDepthPass.CurrentContext.cmd.ClearRenderTarget(true, true, defaultColor);
		}

		public static void AddMRT(RenderBuffer a, RenderBuffer b, RenderBuffer depth)
		{
			identifiers2[0] = a;
			identifiers2[1] = b;

			CustomDepthPass.CurrentContext.cmd.SetRenderTarget(identifiers2, depth);
		}

		public static void AddMRT(RenderBuffer a, RenderBuffer b, RenderBuffer depth, Color defaultColor)
		{
			identifiers2[0] = a;
			identifiers2[1] = b;

			CustomDepthPass.CurrentContext.cmd.SetRenderTarget(identifiers2, depth);
			CustomDepthPass.CurrentContext.cmd.ClearRenderTarget(true, true, defaultColor);
		}

		public static void AddDrawMesh(Mesh mesh, int submesh, Matrix4x4 matrix, Material material, int pass)
		{
			CustomDepthPass.CurrentContext.cmd.DrawMesh(mesh, matrix, material, submesh, pass);
		}

		public static void AddDrawMesh(Mesh mesh, int submesh, Matrix4x4 matrix, Material material, int pass, MaterialPropertyBlock properties)
		{
			CustomDepthPass.CurrentContext.cmd.DrawMesh(mesh, matrix, material, submesh, pass, properties);
		}

		public static void AddDrawMeshInstancedProcedural(Mesh mesh, int submesh, Material material, int pass, int count, MaterialPropertyBlock properties)
		{
			CustomDepthPass.CurrentContext.cmd.DrawMeshInstancedProcedural(mesh, submesh, material, pass, count, properties);
		}

		public virtual void OnEnable()
		{
			if (customPassVolume == null)
			{
				customPassVolume = gameObject.GetComponent<CustomPassVolume>();
			}

			if (customPassVolume == null)
			{
				customPassVolume = gameObject.AddComponent<CustomPassVolume>();

				customPassVolume.hideFlags      = HideFlags.DontSave;
				customPassVolume.injectionPoint = CustomPassInjectionPoint.AfterOpaqueDepthAndNormal;
			}

			if (customDepthPass == null)
			{
				customDepthPass = customPassVolume.customPasses.Find(p => p is CustomDepthPass) as CustomDepthPass;
			}

			if (customDepthPass == null)
			{
				customDepthPass = (CustomDepthPass)customPassVolume.AddPassOfType<CustomDepthPass>();
			}

			customPassVolume.enabled      = true;
			customPassVolume.isGlobal     = false;
			customPassVolume.targetCamera = GetComponent<Camera>();

			customDepthPass.parent = this;

			cachedCamera = GetComponent<Camera>();
			cachedCamera.depthTextureMode |= DepthTextureMode.Depth;
		}

		public virtual void OnDisable()
		{
			customPassVolume.enabled = false;

			ReleaseBuffers();
		}
#elif __URP__
		#if __HAS_RENDER_GRAPH__
			private static RenderTargetIdentifier[] identifiers1 = new RenderTargetIdentifier[1];
			private static RenderTargetIdentifier[] identifiers2 = new RenderTargetIdentifier[2];

			public static void AddMRT(RenderBuffer a, RenderBuffer depth)
			{
				identifiers1[0] = a;

				CustomDepthPass.CurrentContext.cmd.SetRenderTarget(identifiers1, depth);
			}

			public static void AddMRT(RenderBuffer a, RenderBuffer depth, Color defaultColor)
			{
				identifiers1[0] = a;

				CustomDepthPass.CurrentContext.cmd.SetRenderTarget(identifiers1, depth);
				CustomDepthPass.CurrentContext.cmd.ClearRenderTarget(true, true, defaultColor);
			}

			public static void AddMRT(RenderBuffer a, RenderBuffer b, RenderBuffer depth)
			{
				identifiers2[0] = a;
				identifiers2[1] = b;

				CustomDepthPass.CurrentContext.cmd.SetRenderTarget(identifiers2, depth);
			}

			public static void AddMRT(RenderBuffer a, RenderBuffer b, RenderBuffer depth, Color defaultColor)
			{
				identifiers2[0] = a;
				identifiers2[1] = b;

				CustomDepthPass.CurrentContext.cmd.SetRenderTarget(identifiers2, depth);
				CustomDepthPass.CurrentContext.cmd.ClearRenderTarget(true, true, defaultColor);
			}

			public static void AddDrawMesh(Mesh mesh, int submesh, Matrix4x4 matrix, Material material, int pass)
			{
				CustomDepthPass.CurrentContext.cmd.DrawMesh(mesh, matrix, material, submesh, pass);
			}

			public static void AddDrawMesh(Mesh mesh, int submesh, Matrix4x4 matrix, Material material, int pass, MaterialPropertyBlock properties)
			{
				CustomDepthPass.CurrentContext.cmd.DrawMesh(mesh, matrix, material, submesh, pass, properties);
			}

			public static void AddDrawMeshInstancedProcedural(Mesh mesh, int submesh, Material material, int pass, int count, MaterialPropertyBlock properties)
			{
				CustomDepthPass.CurrentContext.cmd.DrawMeshInstancedProcedural(mesh, submesh, material, pass, count, properties);
			}
		#else
			private static RenderTargetIdentifier[] identifiers1 = new RenderTargetIdentifier[1];
			private static RenderTargetIdentifier[] identifiers2 = new RenderTargetIdentifier[2];

			public static void AddMRT(RenderBuffer a, RenderBuffer depth)
			{
				identifiers1[0] = a;

				CustomDepthPass.CurrentCommands.SetRenderTarget(identifiers1, depth);
			}

			public static void AddMRT(RenderBuffer a, RenderBuffer depth, Color defaultColor)
			{
				identifiers1[0] = a;

				CustomDepthPass.CurrentCommands.SetRenderTarget(identifiers1, depth);
				CustomDepthPass.CurrentCommands.ClearRenderTarget(true, true, defaultColor);
			}

			public static void AddMRT(RenderBuffer a, RenderBuffer b, RenderBuffer depth)
			{
				identifiers2[0] = a;
				identifiers2[1] = b;

				CustomDepthPass.CurrentCommands.SetRenderTarget(identifiers2, depth);
			}

			public static void AddMRT(RenderBuffer a, RenderBuffer b, RenderBuffer depth, Color defaultColor)
			{
				identifiers2[0] = a;
				identifiers2[1] = b;

				CustomDepthPass.CurrentCommands.SetRenderTarget(identifiers2, depth);
				CustomDepthPass.CurrentCommands.ClearRenderTarget(true, true, defaultColor);
			}

			public static void AddDrawMesh(Mesh mesh, int submesh, Matrix4x4 matrix, Material material, int pass)
			{
				CustomDepthPass.CurrentCommands.DrawMesh(mesh, matrix, material, submesh, pass);
			}

			public static void AddDrawMesh(Mesh mesh, int submesh, Matrix4x4 matrix, Material material, int pass, MaterialPropertyBlock properties)
			{
				CustomDepthPass.CurrentCommands.DrawMesh(mesh, matrix, material, submesh, pass, properties);
			}

			public static void AddDrawMeshInstancedProcedural(Mesh mesh, int submesh, Material material, int pass, int count, MaterialPropertyBlock properties)
			{
				CustomDepthPass.CurrentCommands.DrawMeshInstancedProcedural(mesh, submesh, material, pass, count, properties);
			}
		#endif

		private CustomDepthPass pass;

		public virtual void OnEnable()
		{
			RenderPipelineManager.beginCameraRendering += HandleCameraRendering;

			cachedCamera = GetComponent<Camera>();
			cachedCamera.depthTextureMode |= DepthTextureMode.Depth;

			pass = new CustomDepthPass();
			pass.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
			pass.parent          = this;
		}

		public virtual void OnDisable()
		{
			RenderPipelineManager.beginCameraRendering -= HandleCameraRendering;

			ReleaseBuffers();
		}

		private void HandleCameraRendering(ScriptableRenderContext context, Camera camera)
		{
			if (camera.gameObject == gameObject)
			{
				var data = camera.GetComponent<UniversalAdditionalCameraData>();

				if (data == null)
				{
					data = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
				}

				data.requiresDepthTexture = true;

				data.scriptableRenderer.EnqueuePass(pass);
			}
		}
#else // BiRP
		private static RenderTargetIdentifier[] buffers1 = new RenderTargetIdentifier[1];
		private static RenderTargetIdentifier[] buffers2 = new RenderTargetIdentifier[2];

		private static System.Collections.Generic.LinkedList<SgtVolumeCamera> instances = new System.Collections.Generic.LinkedList<SgtVolumeCamera>();

		private System.Collections.Generic.LinkedListNode<SgtVolumeCamera> node;

		private CommandBuffer commandBuffer;

		public static void AddMRT(RenderBuffer a, RenderBuffer depth)
		{
			buffers1[0] = a;

			foreach (var instance in instances)
			{
				instance.commandBuffer.SetRenderTarget(buffers1, depth);
			}
		}

		public static void AddMRT(RenderBuffer a, RenderBuffer depth, Color defaultColor)
		{
			buffers1[0] = a;

			foreach (var instance in instances)
			{
				instance.commandBuffer.SetRenderTarget(buffers1, depth);
				instance.commandBuffer.ClearRenderTarget(true, true, defaultColor);
			}
		}

		public static void AddMRT(RenderBuffer a, RenderBuffer b, RenderBuffer depth)
		{
			buffers2[0] = a;
			buffers2[1] = b;

			foreach (var instance in instances)
			{
				instance.commandBuffer.SetRenderTarget(buffers2, depth);
			}
		}

		public static void AddMRT(RenderBuffer a, RenderBuffer b, RenderBuffer depth, Color defaultColor)
		{
			buffers2[0] = a;
			buffers2[1] = b;

			foreach (var instance in instances)
			{
				instance.commandBuffer.SetRenderTarget(buffers2, depth);
				instance.commandBuffer.ClearRenderTarget(true, true, defaultColor);
			}
		}

		public static void AddDrawMesh(Mesh mesh, int submesh, Matrix4x4 matrix, Material material, int pass)
		{
			foreach (var instance in instances)
			{
				instance.commandBuffer.DrawMesh(mesh, matrix, material, submesh, pass);
			}
		}

		public static void AddDrawMesh(Mesh mesh, int submesh, Matrix4x4 matrix, Material material, int pass, MaterialPropertyBlock properties)
		{
			foreach (var instance in instances)
			{
				instance.commandBuffer.DrawMesh(mesh, matrix, material, submesh, pass, properties);
			}
		}

		public static void AddDrawMeshInstancedProcedural(Mesh mesh, int submesh, Material material, int pass, int count, MaterialPropertyBlock properties)
		{
			foreach (var instance in instances)
			{
				instance.commandBuffer.DrawMeshInstancedProcedural(mesh, submesh, material, pass, count, properties);
			}
		}

		public virtual void OnEnable()
		{
			node = instances.AddLast(this);

			commandBuffer = new CommandBuffer();

			cachedCamera = GetComponent<Camera>();
			cachedCamera.AddCommandBuffer(CameraEvent.AfterImageEffectsOpaque, commandBuffer);
			cachedCamera.depthTextureMode |= DepthTextureMode.Depth;
		}

		public virtual void OnDisable()
		{
			instances.Remove(node); node = null;

			cachedCamera.RemoveCommandBuffer(CameraEvent.AfterImageEffectsOpaque, commandBuffer);

			commandBuffer.Release();

			ReleaseBuffers();
		}

		[ImageEffectOpaque]
		private void OnRenderImage(RenderTexture src, RenderTexture dest)
		{
			commandBuffer.Clear();

			Graphics.Blit(src, dest);

			if (SgtVolumeManager.Instances.Count > 0)
			{
				var vm = SgtVolumeManager.Instances.First.Value;
				var vc = this;

				if (vc != null && PrepareBuffers(vm.Downscale, vm.HighPrecision, vm.Taa > 0.0f, vm.Checkerboard) == true && vm.PrepareEffects(this) == true)
				{
					commandBuffer.Blit(default(Texture), vc.SceneDepth, vm.DepthMaterial, vm.PassSceneDepth);

					vm.UpdateEffects(this);
				}
			}

			RenderTexture.active = dest;
		}
#endif
	}

#if __URP__
	[System.Serializable]
	public class CustomDepthPass : ScriptableRenderPass
	{
		public SgtVolumeCamera parent;

		#if __HAS_RENDER_GRAPH__
			public static UnsafeGraphContext CurrentContext;


			private class PassData
			{
				internal SgtVolumeCamera vc;
			}

			public override void RecordRenderGraph(UnityEngine.Rendering.RenderGraphModule.RenderGraph renderGraph, ContextContainer frameContext)
			{
				using (var builder = renderGraph.AddUnsafePass<PassData>("Draw normals", out var passData))
				{
					ConfigureInput(ScriptableRenderPassInput.Depth);

					UniversalResourceData resourceData = frameContext.Get<UniversalResourceData>();

					passData.vc = parent;

					builder.UseTexture(resourceData.cameraDepthTexture);

					builder.AllowPassCulling(false);

					builder.SetRenderFunc((PassData data, UnsafeGraphContext context) => ExecutePass(data, context));
				}

				static void ExecutePass(PassData passData, UnsafeGraphContext context)
				{
					CurrentContext = context;

					CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

					if (SgtVolumeManager.Instances.Count > 0)
					{
						var vm = SgtVolumeManager.Instances.First.Value;
						var vc = passData.vc;

						if (vc != null && vc.PrepareBuffers(vm.Downscale, vm.HighPrecision, vm.Taa > 0.0f, vm.Checkerboard) == true && vm.PrepareEffects(vc) == true)
						{
							cmd.SetRenderTarget(vc.SceneDepth.colorBuffer, vc.SceneDepth.depthBuffer);
							cmd.DrawMesh(CW.Common.CwHelper.GetQuadMesh(), Matrix4x4.identity, vm.DepthMaterial, 0, vm.PassSceneDepth);

							vm.UpdateEffects(vc);
						}
					}
				}
			}
		#else
			public static CommandBuffer CurrentCommands;

			public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
			{
				if (SgtVolumeManager.Instances.Count > 0)
				{
					var vm = SgtVolumeManager.Instances.First.Value;
					var vc = parent;

					if (vc != null && vc.PrepareBuffers(vm.Downscale, vm.HighPrecision, vm.Taa > 0.0f, vm.Checkerboard) == true && vm.PrepareEffects(vc) == true)
					{
						CurrentCommands = CommandBufferPool.Get("SgtVolumeCamera");

						CurrentCommands.Blit(default(Texture), vc.SceneDepth, vm.DepthMaterial, vm.PassSceneDepth);

						vm.UpdateEffects(vc);

						context.ExecuteCommandBuffer(CurrentCommands);
						CommandBufferPool.Release(CurrentCommands);
					}
				}
			}
		#endif
	}
#elif __HDRP__
	[System.Serializable]
	public class CustomDepthPass : CustomPass
	{
		public SgtVolumeCamera parent;

		public static CustomPassContext CurrentContext;

		protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
		{
		}

		protected override void Execute(CustomPassContext ctx)
		{
			CurrentContext = ctx;

			if (parent != null)
			{
				var thisCamera = ctx.hdCamera.camera;

				if (parent.gameObject == thisCamera.gameObject)
				{
					if (SgtVolumeManager.Instances.Count > 0)
					{
						var vm = SgtVolumeManager.Instances.First.Value;

						if (parent.PrepareBuffers(vm.Downscale, vm.HighPrecision, vm.Taa > 0.0f, vm.Checkerboard) == true && vm.PrepareEffects(parent) == true)
						{
							ctx.cmd.Blit(ctx.cameraDepthBuffer, parent.SceneDepth, vm.DepthMaterial, vm.PassSceneDepth);

							vm.UpdateEffects(parent);
						}
					}
				}
			}
		}

		protected override void Cleanup()
		{
		}
	}
#endif
}

#if UNITY_EDITOR
namespace SpaceGraphicsToolkit.Volumetrics
{
	using CW.Common;

	[UnityEditor.CanEditMultipleObjects]
	[UnityEditor.CustomEditor(typeof(SgtVolumeCamera))]
	public class SgtVolumeCamera_Editor : CwEditor
	{
		public static void Require()
		{
			var found = false;

			foreach (var camera in Camera.allCameras)
			{
				if (camera.GetComponent<SgtVolumeCamera>() != null)
				{
					found = true; break;
				}
			}

			if (found == false)
			{
				Separator();

				if (Camera.main != null)
				{
					if (Camera.main.GetComponent<SgtVolumeCamera>() == null)
					{
						if (HelpButton("This component requires your camera to have the " + typeof(SgtVolumeCamera).Name + " component, but it doesn't.", UnityEditor.MessageType.Error, "Fix", 50.0f) == true)
						{
							CwHelper.GetOrAddComponent<SgtVolumeCamera>(Camera.main.gameObject);

							CW.Common.CwHelper.SelectAndPing(Camera.main);
						}
					}
				}
				else
				{
					Error("This component requires the main camera in your scene to have the " + typeof(SgtVolumeCamera).Name + " component, but it doesn't, nor is any camera tagged as MainCamera.");
				}
			}
		}

		private static bool debug;

		protected override void OnInspector()
		{
			SgtVolumeCamera tgt; SgtVolumeCamera[] tgts; GetTargets(out tgt, out tgts);

			Info("This component will render volumetric effects to this camera.");

			debug = UnityEditor.EditorGUILayout.Toggle("Debug", debug);

			if (debug == true)
			{
				BeginDisabled();
					UnityEditor.EditorGUILayout.ObjectField("Color", tgt.WholeColor, typeof(RenderTexture), true);
					UnityEditor.EditorGUILayout.ObjectField("Depth", tgt.WholeDepth, typeof(RenderTexture), true);

					Separator();

					UnityEditor.EditorGUILayout.ObjectField("SceneDepth", tgt.SceneDepth, typeof(RenderTexture), true);

					Separator();

					UnityEditor.EditorGUILayout.ObjectField("Ocean", tgt.OceanDepth, typeof(RenderTexture), true);
					UnityEditor.EditorGUILayout.ObjectField("Cavity", tgt.CavityDepth, typeof(RenderTexture), true);

					Separator();

					UnityEditor.EditorGUILayout.ObjectField("NewColor", tgt.NewColor, typeof(RenderTexture), true);
					UnityEditor.EditorGUILayout.ObjectField("NewDepth", tgt.NewDepth, typeof(RenderTexture), true);
				EndDisabled();
			}
		}
	}
}
#endif