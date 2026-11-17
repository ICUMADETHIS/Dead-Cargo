using System.Collections.Generic;
using DeadCargo.Cargo;
using DeadCargo.Extraction;
using DeadCargo.Sectors.Placement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadCargo.EditorTools
{
    /// <summary>
    /// Lets the run place the open scene's content: every extraction point and every pickup gets a
    /// <see cref="SectorPlacement"/> with its kind's rule, and the rules are created when missing. Safe to run more than
    /// once; existing rules and placements are never changed, so tuned values are kept.
    /// </summary>
    internal static class SectorPlacementSetup
    {
        public const string MenuPath = "Dead Cargo/Setup/Phase 6.8 - Content Placement (Open Scene)";

        public const string RuleFolder = "Assets/DeadCargo/Data/Sectors/Placement";
        public const string ExtractionRulePath = RuleFolder + "/Extraction Points.asset";
        public const string PickupRulePath = RuleFolder + "/Pickups.asset";

        private const string RuleProperty = "rule";

        // Extraction points stand off in open space or at a field's thin edge, far enough out that reaching one is a
        // journey, facing the way the player comes, with rock cleared round the ring.
        private static readonly PlacementSettings ExtractionDefaults = new PlacementSettings
        {
            MinDistance = 4000f,
            MaxDistance = 9000f,
            MaxHeight = 600f,
            MinDensity = 0f,
            MaxDensity = 0.3f,
            Spacing = 3000f,
            KeepClear = 400f,
            FaceArrival = true
        };

        // Pickups are scattered anywhere within reach, fields included, apart from each other and the extraction points,
        // each in a small pocket no rock reaches into, so none is ever buried.
        private static readonly PlacementSettings PickupDefaults = new PlacementSettings
        {
            MinDistance = 1200f,
            MaxDistance = 8000f,
            MaxHeight = 800f,
            MinDensity = 0f,
            MaxDensity = 1f,
            Spacing = 400f,
            KeepClear = 60f,
            FaceArrival = false
        };

        private const int ExtractionOrder = 0;
        private const int PickupOrder = 10;

        [MenuItem(MenuPath, priority = 210)]
        private static void Run()
        {
            Apply();
        }

        [MenuItem(MenuPath, true)]
        private static bool CanRun()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        /// <summary>True when every extraction point and pickup in the scene is placed by a rule.</summary>
        public static bool IsInstalled(Scene scene)
        {
            return AllPlaced(SceneSetupUtility.FindInScene<ExtractionPoint>(scene)) &&
                   AllPlaced(SceneSetupUtility.FindInScene<CargoPickup>(scene));
        }

        /// <summary>Sets up the open scene. Always succeeds; a scene with no content to place is left as it is.</summary>
        internal static bool Apply()
        {
            Scene scene = SceneManager.GetActiveScene();

            Undo.SetCurrentGroupName("Set Up Content Placement");
            int undoGroup = Undo.GetCurrentGroup();

            SectorPlacementRule extractionRule = EnsureRule(ExtractionRulePath, ExtractionOrder, ExtractionDefaults);
            SectorPlacementRule pickupRule = EnsureRule(PickupRulePath, PickupOrder, PickupDefaults);

            int added = AddPlacements(SceneSetupUtility.FindInScene<ExtractionPoint>(scene), extractionRule) +
                        AddPlacements(SceneSetupUtility.FindInScene<CargoPickup>(scene), pickupRule);

            Undo.CollapseUndoOperations(undoGroup);
            if (added > 0)
                EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log($"Content placement set up in '{scene.name}': {added} object(s) are now placed by the run.");
            return true;
        }

        private static bool AllPlaced<T>(List<T> content) where T : Component
        {
            foreach (T item in content)
            {
                if (!item.TryGetComponent(out SectorPlacement placement) || placement.Rule == null)
                    return false;
            }

            return true;
        }

        private static int AddPlacements<T>(List<T> content, SectorPlacementRule rule) where T : Component
        {
            int added = 0;
            foreach (T item in content)
            {
                if (!item.TryGetComponent(out SectorPlacement placement))
                {
                    placement = Undo.AddComponent<SectorPlacement>(item.gameObject);
                    ++added;
                }

                if (placement.Rule != null)
                    continue;

                var serialized = new SerializedObject(placement);
                serialized.FindProperty(RuleProperty).objectReferenceValue = rule;
                serialized.ApplyModifiedProperties();
            }

            return added;
        }

        private static SectorPlacementRule EnsureRule(string path, int order, in PlacementSettings defaults)
        {
            var rule = AssetDatabase.LoadAssetAtPath<SectorPlacementRule>(path);
            if (rule != null)
                return rule;

            rule = ScriptableObject.CreateInstance<SectorPlacementRule>();
            rule.Configure(order, defaults);
            SceneSetupUtility.EnsureFolder(RuleFolder);
            AssetDatabase.CreateAsset(rule, path);
            AssetDatabase.SaveAssets();
            return rule;
        }
    }
}
