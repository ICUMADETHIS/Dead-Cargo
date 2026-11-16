using System.Diagnostics;
using DeadCargo.Visuals;
using UnityEditor;
using UnityEngine;

namespace DeadCargo.EditorTools.Visuals
{
    /// <summary>
    /// Bakes a Space Sky material's nebula shape into its nebula map, saved beside the material as
    /// "&lt;material&gt;_Nebula.asset". Re-baking updates the same asset, so references never break.
    /// </summary>
    internal static class SpaceSkyBaker
    {
        private const string MapSuffix = "_Nebula.asset";

        /// <summary>The nebula shape set on a material.</summary>
        public static NebulaShape ReadShape(Material material)
        {
            return NebulaMap.ReadShape(material);
        }

        /// <summary>True when the material has a nebula map baked from its current shape.</summary>
        public static bool IsCurrent(Material material)
        {
            return material.GetTexture(SpaceSkyProperties.NebulaMap) != null &&
                   material.GetInteger(SpaceSkyProperties.NebulaBakeHash) == ReadShape(material).ComputeHash(NebulaBaker.MapSize);
        }

        /// <summary>Where a material's nebula map is saved. Null for a material that is not an asset.</summary>
        public static string MapPath(Material material)
        {
            string materialPath = AssetDatabase.GetAssetPath(material);
            if (string.IsNullOrEmpty(materialPath))
                return null;

            return materialPath.Substring(0, materialPath.Length - System.IO.Path.GetExtension(materialPath).Length) + MapSuffix;
        }

        /// <summary>Bakes and assigns the nebula map. Returns the time taken in milliseconds, or -1 when the material is not an asset.</summary>
        public static long Bake(Material material)
        {
            string mapPath = MapPath(material);
            if (mapPath == null)
                return -1;

            Stopwatch stopwatch = Stopwatch.StartNew();
            NebulaShape shape = ReadShape(material);

            // Kept readable, so its pixels are written into the asset.
            Texture2D baked = NebulaMap.Create(shape, System.IO.Path.GetFileNameWithoutExtension(mapPath), true);

            var map = AssetDatabase.LoadAssetAtPath<Texture2D>(mapPath);
            if (map == null)
            {
                AssetDatabase.CreateAsset(baked, mapPath);
                map = baked;
            }
            else
            {
                EditorUtility.CopySerialized(baked, map);
                Object.DestroyImmediate(baked);
                EditorUtility.SetDirty(map);
            }

            material.SetTexture(SpaceSkyProperties.NebulaMap, map);
            material.SetInteger(SpaceSkyProperties.NebulaBakeHash, shape.ComputeHash(NebulaBaker.MapSize));
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(map);
            AssetDatabase.SaveAssetIfDirty(material);
            return stopwatch.ElapsedMilliseconds;
        }
    }
}
