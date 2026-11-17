using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using System.Collections.Generic;

namespace SpaceGraphicsToolkit.Landscape
{
	/// <summary>For efficient generation, all textures used by active landscapes are combined into a global texture bundle automatically.
	/// This handles registration and conversions dynamically based on the scenes active Landscape features.</summary>
	public static class SgtLandscapeTextures
	{
		public static List<Texture2D> HeightTextures   = new List<Texture2D>();
		public static List<Texture2D> MaskTextures     = new List<Texture2D>();
		public static List<Texture2D> GradientTextures = new List<Texture2D>();
		public static List<Texture2D> DetailTextures   = new List<Texture2D>();
		public static List<Texture2D> FlowTextures     = new List<Texture2D>();
		public static List<Texture2D> AlbedoTextures   = new List<Texture2D>();
		public static List<Texture2D> NormalTextures   = new List<Texture2D>();

		private static Dictionary<Texture2D, int> heightIndexMap   = new Dictionary<Texture2D, int>();
		private static Dictionary<Texture2D, int> maskIndexMap     = new Dictionary<Texture2D, int>();
		private static Dictionary<Texture2D, int> gradientIndexMap = new Dictionary<Texture2D, int>();
		private static Dictionary<Texture2D, int> detailIndexMap   = new Dictionary<Texture2D, int>();
		private static Dictionary<Texture2D, int> flowIndexMap     = new Dictionary<Texture2D, int>();
		private static Dictionary<Texture2D, int> albedoIndexMap   = new Dictionary<Texture2D, int>();
		private static Dictionary<Texture2D, int> normalIndexMap   = new Dictionary<Texture2D, int>();

		private static Dictionary<Texture2D, SgtLandscape.HeightData> heightDataCache = new Dictionary<Texture2D, SgtLandscape.HeightData>();
		private static Dictionary<Texture2D, SgtLandscape.HeightData> maskDataCache   = new Dictionary<Texture2D, SgtLandscape.HeightData>();

		private static RenderTexture heightTopologyAtlas;
		private static RenderTexture maskTopologyAtlas;
		private static RenderTexture gradientAtlas;
		private static RenderTexture detailAtlas;
		private static RenderTexture flowAtlas;
		private static RenderTexture albedoAtlas;
		private static RenderTexture normalAtlas;

		private static Vector4 heightTopologyAtlasSize;
		private static Vector4 maskTopologyAtlasSize;
		private static Vector4 gradientAtlasSize;
		private static Vector4 detailAtlasSize;
		private static Vector4 flowAtlasSize;
		private static Vector4 albedoAtlasSize;
		private static Vector4 normalAtlasSize;
		
		private static bool                    defaultAllocated;
		private static SgtLandscape.HeightData defaultHeight;

		private static readonly int _CwHeightTopologyAtlas     = Shader.PropertyToID("_CwHeightTopologyAtlas");
		private static readonly int _CwHeightTopologyAtlasSize = Shader.PropertyToID("_CwHeightTopologyAtlasSize");
		private static readonly int _CwMaskTopologyAtlas       = Shader.PropertyToID("_CwMaskTopologyAtlas");
		private static readonly int _CwMaskTopologyAtlasSize   = Shader.PropertyToID("_CwMaskTopologyAtlasSize");
		private static readonly int _CwGradientAtlas           = Shader.PropertyToID("_CwGradientAtlas");
		private static readonly int _CwGradientAtlasSize       = Shader.PropertyToID("_CwGradientAtlasSize");
		private static readonly int _CwDetailAtlas             = Shader.PropertyToID("_CwDetailAtlas");
		private static readonly int _CwDetailAtlasSize         = Shader.PropertyToID("_CwDetailAtlasSize");
		private static readonly int _CwAlbedoAtlas             = Shader.PropertyToID("_CwAlbedoAtlas");
		private static readonly int _CwAlbedoAtlasSize         = Shader.PropertyToID("_CwAlbedoAtlasSize");
		private static readonly int _CwNormalAtlas             = Shader.PropertyToID("_CwNormalAtlas");
		private static readonly int _CwNormalAtlasSize         = Shader.PropertyToID("_CwNormalAtlasSize");

		private static List<Texture2D> oldHeightKeys = new List<Texture2D>();
		private static List<Texture2D> oldMaskKeys   = new List<Texture2D>();

		private static Material blitMaterial;

		public static void Write(Material material)
		{
			material.SetTexture(_CwHeightTopologyAtlas, heightTopologyAtlas);
			material.SetVector(_CwHeightTopologyAtlasSize, heightTopologyAtlasSize);
			material.SetTexture(_CwMaskTopologyAtlas, maskTopologyAtlas);
			material.SetVector(_CwMaskTopologyAtlasSize, maskTopologyAtlasSize);
			material.SetTexture(_CwGradientAtlas, gradientAtlas);
			material.SetVector(_CwGradientAtlasSize, gradientAtlasSize);
			material.SetTexture(_CwDetailAtlas, detailAtlas);
			material.SetVector(_CwDetailAtlasSize, detailAtlasSize);
			material.SetTexture(_CwAlbedoAtlas, albedoAtlas);
			material.SetVector(_CwAlbedoAtlasSize, albedoAtlasSize);
			material.SetTexture(_CwNormalAtlas, normalAtlas);
			material.SetVector(_CwNormalAtlasSize, normalAtlasSize);
		}

		public static void ClearLists()
		{
			HeightTextures.Clear();   heightIndexMap.Clear();
			MaskTextures.Clear();     maskIndexMap.Clear();
			GradientTextures.Clear(); gradientIndexMap.Clear();
			DetailTextures.Clear();   detailIndexMap.Clear();
			FlowTextures.Clear();     flowIndexMap.Clear();
			AlbedoTextures.Clear();   albedoIndexMap.Clear();
			NormalTextures.Clear();   normalIndexMap.Clear();
		}

		public static void RebuildAll()
		{
			foreach (var landscape in SgtLandscape.AllLandscapes)
			{
				if (landscape != null)
				{
					landscape.CompleteAllPendingJobs();
				}
			}

			CleanupCache(heightDataCache, HeightTextures, oldHeightKeys);
			CleanupCache(maskDataCache,   MaskTextures,   oldMaskKeys);

			ClearLists();

			foreach (var landscape in SgtLandscape.AllLandscapes)
			{
				if (landscape != null && landscape.isActiveAndEnabled == true)
				{
					landscape.RegisterTextures();
				}
			}

			if (HeightTextures.Count + MaskTextures.Count + GradientTextures.Count + DetailTextures.Count + FlowTextures.Count + AlbedoTextures.Count + NormalTextures.Count > 0)
			{
				TryCreateDefaults();

				var oldActive = RenderTexture.active;
				var oldWrite  = GL.sRGBWrite;

				GL.sRGBWrite = false;

				GenerateAtlas(HeightTextures,   ref heightTopologyAtlas, ref heightTopologyAtlasSize, RenderTextureFormat.ARGB64,   false, true, heightDataCache);
				GenerateAtlas(MaskTextures,     ref maskTopologyAtlas,   ref maskTopologyAtlasSize,   RenderTextureFormat.ARGBHalf, false, true, maskDataCache);
				GenerateAtlas(GradientTextures, ref gradientAtlas,       ref gradientAtlasSize,       RenderTextureFormat.ARGB32,   false, true);
				GenerateAtlas(DetailTextures,   ref detailAtlas,         ref detailAtlasSize,         RenderTextureFormat.R8,       false, true);
				GenerateAtlas(FlowTextures,     ref flowAtlas,           ref flowAtlasSize,           RenderTextureFormat.ARGB32,   false, true);
				GenerateAtlas(AlbedoTextures,   ref albedoAtlas,         ref albedoAtlasSize,         RenderTextureFormat.ARGB32,   true,  true);
				GenerateAtlas(NormalTextures,   ref normalAtlas,         ref normalAtlasSize,         RenderTextureFormat.ARGB32,   false, true);

				RenderTexture.active = oldActive;
				GL.sRGBWrite         = oldWrite;
			}
			else if (defaultAllocated == true)
			{
				defaultHeight.Dispose();

				defaultAllocated = false;
			}

			foreach (var landscape in SgtLandscape.AllLandscapes)
			{
				if (landscape != null && landscape.isActiveAndEnabled == true)
				{
					landscape.RefreshFeatureData();
				}
			}
		}

		private static void BlitToSlice(Texture source, RenderTexture destArray, int sliceIndex, bool sRGB)
		{
			if (blitMaterial == null)
			{
				blitMaterial = new Material(Shader.Find("Hidden/BlitCopy"));
				if (blitMaterial == null)
					blitMaterial = new Material(Shader.Find("Unlit/Texture"));
				blitMaterial.hideFlags = HideFlags.DontSave;
			}

			GL.sRGBWrite = sRGB;
			Graphics.SetRenderTarget(destArray, 0, CubemapFace.Unknown, sliceIndex);
			GL.PushMatrix();
			GL.LoadOrtho();
			blitMaterial.SetTexture("_MainTex", source);
			blitMaterial.SetPass(0);
			GL.Begin(GL.QUADS);
			GL.TexCoord2(0, 0); GL.Vertex3(0, 0, 0);
			GL.TexCoord2(1, 0); GL.Vertex3(1, 0, 0);
			GL.TexCoord2(1, 1); GL.Vertex3(1, 1, 0);
			GL.TexCoord2(0, 1); GL.Vertex3(0, 1, 0);
			GL.End();
			GL.PopMatrix();
		}

		private static void GenerateAtlas(List<Texture2D> textures, ref RenderTexture atlas, ref Vector4 atlasSize, RenderTextureFormat format, bool sRGB, bool useMips, Dictionary<Texture2D, SgtLandscape.HeightData> cache = null)
		{
			if (atlas != null) { Object.DestroyImmediate(atlas); atlas = null; }

			var size     = default(int2);
			var format2d = default(TextureFormat);
			var depth    = 0;

			if (TryGetTextureData(textures, ref size, ref format2d, ref depth))
			{
				var mips = useMips ? Mathf.FloorToInt(Mathf.Log(Mathf.Max(size.x, size.y), 2)) + 1 : 0;
				var desc = new RenderTextureDescriptor(size.x, size.y, format, 0, mips) { sRGB = sRGB };

				atlas                   = new RenderTexture(desc);
				atlas.hideFlags         = HideFlags.DontSave;
				atlas.dimension         = UnityEngine.Rendering.TextureDimension.Tex2DArray;
				atlas.volumeDepth       = depth;
				atlas.enableRandomWrite = true;
				atlas.wrapMode          = TextureWrapMode.Repeat;
				atlas.useMipMap         = useMips;
				atlas.autoGenerateMips  = false;
				atlas.Create();

				atlasSize = new Vector4(atlas.width, atlas.height, 1.0f / atlas.width, 1.0f / atlas.height);

				GL.sRGBWrite = sRGB;

				for (int i = 0; i < textures.Count; i++)
				{
					var tex = textures[i];
					if (tex == null) continue;

					if (cache != null)
					{
						var topo = default(SgtLandscape.TopologyData);
						topo.Create(tex, 1, 1, 1, 1);
						BlitToSlice(topo.Texture, atlas, i, sRGB);
						topo.Dispose();

						if (cache.ContainsKey(tex) == false)
						{
							var hData = default(SgtLandscape.HeightData);
							hData.Create(tex, 1, 1);
							cache.Add(tex, hData);
						}
					}
					else
					{
						BlitToSlice(tex, atlas, i, sRGB);
					}
				}

				if (useMips) atlas.GenerateMips();
			}
		}

		private static void CleanupCache(Dictionary<Texture2D, SgtLandscape.HeightData> cache, List<Texture2D> newList, List<Texture2D> oldKeys)
		{
			foreach (var tex in oldKeys)
			{
				if (tex != null && newList.Contains(tex) == false)
				{
					if (cache.TryGetValue(tex, out var data) == true)
					{
						data.Dispose();
						cache.Remove(tex);
					}
				}
			}

			oldKeys.Clear();
			oldKeys.AddRange(newList);
		}

		private static void RegisterTexture(Texture2D tex, List<Texture2D> list, Dictionary<Texture2D, int> map)
		{
			if (tex != null && map.ContainsKey(tex) == false)
			{
				map.Add(tex, list.Count);

				list.Add(tex);
			}
		}

		public static void RegisterHeight(Texture2D tex)   { RegisterTexture(tex, HeightTextures,   heightIndexMap);   }
		public static void RegisterMask(Texture2D tex)     { RegisterTexture(tex, MaskTextures,     maskIndexMap);     }
		public static void RegisterGradient(Texture2D tex) { RegisterTexture(tex, GradientTextures, gradientIndexMap); }
		public static void RegisterDetail(Texture2D tex)   { RegisterTexture(tex, DetailTextures,   detailIndexMap);   }
		public static void RegisterFlow(Texture2D tex)     { RegisterTexture(tex, FlowTextures,     flowIndexMap);     }
		public static void RegisterAlbedo(Texture2D tex)   { RegisterTexture(tex, AlbedoTextures,   albedoIndexMap);   }
		public static void RegisterNormal(Texture2D tex)   { RegisterTexture(tex, NormalTextures,   normalIndexMap);   }

		public static int GetHeightIndex(Texture2D tex)   { return tex != null && heightIndexMap.TryGetValue(tex,   out var index) ? index : -1; }
		public static int GetMaskIndex(Texture2D tex)     { return tex != null && maskIndexMap.TryGetValue(tex,     out var index) ? index : -1; }
		public static int GetGradientIndex(Texture2D tex) { return tex != null && gradientIndexMap.TryGetValue(tex, out var index) ? index : -1; }
		public static int GetDetailIndex(Texture2D tex)   { return tex != null && detailIndexMap.TryGetValue(tex,   out var index) ? index : -1; }
		public static int GetFlowIndex(Texture2D tex)     { return tex != null && flowIndexMap.TryGetValue(tex,     out var index) ? index : -1; }
		public static int GetAlbedoIndex(Texture2D tex)   { return tex != null && albedoIndexMap.TryGetValue(tex,   out var index) ? index : -1; }
		public static int GetNormalIndex(Texture2D tex)   { return tex != null && normalIndexMap.TryGetValue(tex,   out var index) ? index : -1; }

		private static void TryCreateDefaults()
		{
			if (defaultAllocated == false)
			{
				defaultHeight.Data08    = new NativeArray<byte  >(0, Allocator.Persistent, NativeArrayOptions.ClearMemory);
				defaultHeight.Data16    = new NativeArray<ushort>(0, Allocator.Persistent, NativeArrayOptions.ClearMemory);
				defaultHeight.Size      = new int2(0, 0);
				defaultHeight.Created08 = true;
				defaultHeight.Created16 = true;

				defaultAllocated = true;
			}
		}

		public static SgtLandscape.HeightData GetHeightData(Texture2D tex)
		{
			if (tex != null && heightDataCache.TryGetValue(tex, out var data)) return data;
			TryCreateDefaults();
			return defaultHeight;
		}

		public static SgtLandscape.HeightData GetMaskData(Texture2D tex)
		{
			if (tex != null && maskDataCache.TryGetValue(tex, out var data)) return data;
			TryCreateDefaults();
			return defaultHeight;
		}

		private static bool TryGetTextureData(List<Texture2D> textures, ref int2 size, ref TextureFormat format, ref int count)
		{
			count = 0;

			if (textures != null)
			{
				foreach (var texture in textures)
				{
					if (texture != null)
					{
						size.x = texture.width;
						size.y = texture.height;
						format = texture.format;
						count += 1;
					}
				}
			}

			return count > 0;
		}

#if UNITY_EDITOR
		public static void DrawInspector(CW.Common.CwEditor editor, ref bool markForRebuild)
		{
			UnityEditor.EditorGUILayout.LabelField("SIZES", UnityEditor.EditorStyles.boldLabel);

			DrawSizeInfo("Height", HeightTextures, 4 * 4);
			DrawSizeInfo("Mask", MaskTextures, 4 * 4);
			DrawSizeInfo("Gradient", GradientTextures, 1 * 4);
			DrawSizeInfo("Detail", DetailTextures, 1 * 1);
			DrawSizeInfo("Flow", FlowTextures, 1 * 4);
			DrawSizeInfo("Albedo", AlbedoTextures, 1 * 4);
			DrawSizeInfo("Normal", NormalTextures, 1 * 4);
		}

		private static void DrawSizeInfo(string name, List<Texture2D> textures, int bytesPerPixel)
		{
			var size = default(Vector2Int);
			var format = default(TextureFormat);

			if (TryGetData(textures, ref size, ref format))
			{
				var megs = CalculateMegabytes(textures, size, bytesPerPixel);
				UnityEditor.EditorGUILayout.HelpBox($"{name} Textures: Count = {textures.Count}, Size = {size.x}x{size.y}, Format = {format}, Megabytes = {megs}", UnityEditor.MessageType.Info);
			}
		}

		private static bool TryGetData(List<Texture2D> textures, ref Vector2Int size, ref TextureFormat format)
		{
			if (textures != null)
			{
				for (var i = 0; i < textures.Count; i++)
				{
					var t = textures[i];
					if (t != null)
					{
						size.x = t.width;
						size.y = t.height;
						format = t.format;
						return true;
					}
				}
			}
			return false;
		}

		private static int CalculateMegabytes(List<Texture2D> textures, Vector2Int size, int bytesPerPixel)
		{
			var total = 0;

			if (textures != null)
			{
				for (var i = 0; i < textures.Count; i++)
				{
					if (textures[i] != null) total += size.x * size.y * bytesPerPixel;
				}
			}

			return (int)math.ceil(total / 1024.0 / 1024.0);
		}
#endif
	}
}