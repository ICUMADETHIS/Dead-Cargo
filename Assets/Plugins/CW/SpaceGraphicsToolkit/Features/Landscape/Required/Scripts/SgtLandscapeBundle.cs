using UnityEngine;
using System.Collections.Generic;

namespace SpaceGraphicsToolkit.Landscape
{
	public class SgtLandscapeBundle : MonoBehaviour
	{
		public List<Texture2D> heightTextures;
		public List<Texture2D> maskTextures;
		public List<Texture2D> gradientTextures;
		public List<Texture2D> detailTextures;
	}
}

#if UNITY_EDITOR
namespace SpaceGraphicsToolkit.Landscape
{
	using UnityEditor;

	[CustomEditor(typeof(SgtLandscapeBundle))]
	public class SgtLandscapeBundle_Editor : CW.Common.CwEditor
	{
		protected override void OnInspector()
		{
			DrawDefaultInspector();

			var tgt = (SgtLandscapeBundle)target;

			if (GUILayout.Button("Upgrade from SgtLandscapeBundle"))
			{
				Upgrade(tgt);

				tgt.GetComponent<SgtLandscape>().Rebuild();

				DestroyImmediate(tgt);
			}
		}

		private void Upgrade(SgtLandscapeBundle newBundle)
		{
			var oldTextures = newBundle.GetComponent<SgtLandscapeBundle>();

			if (oldTextures == null)
			{
				Debug.LogError("Could not find SgtLandscapeBundle component on this GameObject.");
				return;
			}

			Undo.RecordObjects(newBundle.GetComponentsInChildren<SgtLandscapeFeature>(), "Upgrade Landscape Textures");

			var features = newBundle.GetComponentsInChildren<SgtLandscapeFeature>();

			foreach (var feature in features)
			{
				if (feature is SgtLandscapeDetail)
				{
					var detail = (SgtLandscapeDetail)feature;

					detail.HeightTexture     = GetAtIndex(oldTextures.heightTextures, detail.HeightIndex);
					detail.Mask       = GetAtIndex(oldTextures.maskTextures, detail.MaskIndex);
					detail.MaskDetail = GetAtIndex(oldTextures.maskTextures, detail.MaskDetailIndex);
				}
				else if (feature is SgtLandscapeColor)
				{
					var color = (SgtLandscapeColor)feature;

					color.Mask       = GetAtIndex(oldTextures.maskTextures, color.MaskIndex);
					color.MaskDetail = GetAtIndex(oldTextures.maskTextures, color.MaskDetailIndex);
					color.GradientTexture   = GetAtIndex(oldTextures.gradientTextures, color.GradientIndex);
				}
				else if (feature is SgtLandscapeFlatten)
				{
					var flatten = (SgtLandscapeFlatten)feature;

					flatten.MaskTexture = GetAtIndex(oldTextures.maskTextures, flatten.MaskIndex);
				}
				else if (feature is SgtLandscapeBiome)
				{
					var biome = (SgtLandscapeBiome)feature;

					foreach (var layer in biome.Layers)
					{
						layer.HeightTexture = GetAtIndex(oldTextures.heightTextures, layer.HeightIndex);
					}

					biome.Mask       = GetAtIndex(oldTextures.maskTextures, biome.MaskIndex);
					biome.MaskDetail = GetAtIndex(oldTextures.maskTextures, biome.MaskDetailIndex);
					biome.GradientTexture   = GetAtIndex(oldTextures.gradientTextures, biome.GradientIndex);
				}
			
				EditorUtility.SetDirty(feature);
			}

			EditorUtility.SetDirty(newBundle);
		
			Debug.Log("Upgrade Complete: Indices have been converted to direct references.");
		}

		private Texture2D GetAtIndex(List<Texture2D> list, int index)
		{
			if (list != null && index >= 0 && index < list.Count)
			{
				return list[index];
			}
			return null;
		}
	}
}
#endif