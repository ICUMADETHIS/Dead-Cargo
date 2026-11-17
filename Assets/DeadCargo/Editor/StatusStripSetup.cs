using System.Collections.Generic;
using DeadCargo.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.EditorTools
{
    /// <summary>
    /// Adds the status strip to the open scene, in the UI folder, with the shield and hull health types taken from the
    /// kit's own health bars, and hides the kit HUD parts it replaces on the player's ship: the reticle arcs, the health
    /// bars, the speed and weapon-power fills and the weapon boxes. They are hidden by a CanvasGroup at zero alpha, so the
    /// kit is untouched and any part comes back by raising its alpha. Safe to run more than once; every change can be undone.
    /// </summary>
    internal static class StatusStripSetup
    {
        public const string MenuPath = "Dead Cargo/Setup/HUD - Status Strip (Open Scene)";

        private const string DialogTitle = "Status Strip";
        private const string UndoName = "Set Up Status Strip";
        private const string StripName = "Status Strip";
        private const string KitGroupName = "Cockpit";
        private const string ShieldBarName = "ShieldHealthBar";
        private const string HullBarName = "ArmorHealthBar";
        private const string ShieldProperty = "shieldType";
        private const string HullProperty = "hullType";

        // The kit HUD parts the strip replaces, under the ship's HUD group of the same name.
        private static readonly string[] ReplacedParts = { "HUDFrame_Left", "HUDFrame_Right", "Health", "Speed", "WeaponsInfo", "WeaponsPower" };

        /// <summary>True when the strip has its health types and every replaced kit part is hidden.</summary>
        public static bool IsInstalled(Scene scene)
        {
            List<StatusStrip> strips = SceneSetupUtility.FindInScene<StatusStrip>(scene);
            if (strips.Count == 0 || !strips[0].HasHealthTypes)
                return false;

            Transform group = KitGroup(scene);
            if (group == null)
                return true;

            foreach (string part in ReplacedParts)
            {
                Transform child = group.Find(part);
                if (child != null && (!child.TryGetComponent(out CanvasGroup hidden) || hidden.alpha > 0f))
                    return false;
            }

            return true;
        }

        [MenuItem(MenuPath, priority = 220)]
        private static void Run()
        {
            Apply();
        }

        [MenuItem(MenuPath, true)]
        private static bool CanRun()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        /// <summary>Sets up the open scene. Returns false when it cannot, after a dialog explains why.</summary>
        internal static bool Apply()
        {
            Scene scene = SceneManager.GetActiveScene();
            VehicleEngines3D ship = SceneSetupUtility.FindShip(scene);
            if (ship == null)
                return Fail("Select the player's ship in the Hierarchy, then run this again.");

            Transform group = KitGroup(scene);
            HealthType shield = BarType(group, ShieldBarName);
            HealthType hull = BarType(group, HullBarName);
            if (shield == null || hull == null)
                return Fail("The player's ship has no kit shield and armour bars to take the health types from. Set Shield Type " +
                            "and Hull Type on the Status Strip by hand.");

            Undo.SetCurrentGroupName(UndoName);
            int undoGroup = Undo.GetCurrentGroup();

            StatusStrip strip = SceneHierarchy.EnsureObject<StatusStrip>(scene, StripName, SceneHierarchy.UI, true);
            var serializedStrip = new SerializedObject(strip);
            serializedStrip.FindProperty(ShieldProperty).objectReferenceValue = shield;
            serializedStrip.FindProperty(HullProperty).objectReferenceValue = hull;
            serializedStrip.ApplyModifiedProperties();

            int hidden = 0;
            foreach (string part in ReplacedParts)
            {
                Transform child = group.Find(part);
                if (child == null)
                    continue;

                if (!child.TryGetComponent(out CanvasGroup canvasGroup))
                    canvasGroup = Undo.AddComponent<CanvasGroup>(child.gameObject);

                Undo.RecordObject(canvasGroup, UndoName);
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
                ++hidden;
            }

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"Status strip set up in '{scene.name}': {hidden} kit HUD parts hidden on '{ship.name}'. Save the scene.", strip);
            return true;
        }

        // The kit HUD group holding the health bars, speed and weapon boxes on the player's ship: the HUD component the
        // health bars sit in. Found from the bars, as the kit keeps its HUD manager on the ship's root, not on the HUD.
        private static Transform KitGroup(Scene scene)
        {
            VehicleEngines3D ship = SceneSetupUtility.FindShip(scene);
            if (ship == null)
                return null;

            foreach (HealthFillBarController bar in ship.GetComponentsInChildren<HealthFillBarController>(true))
            {
                HUDComponent group = bar.GetComponentInParent<HUDComponent>(true);
                if (group != null && group.name == KitGroupName)
                    return group.transform;
            }

            return null;
        }

        private static HealthType BarType(Transform group, string barName)
        {
            if (group == null)
                return null;

            foreach (HealthFillBarController bar in group.GetComponentsInChildren<HealthFillBarController>(true))
            {
                if (bar.name == barName)
                    return bar.HealthType;
            }

            return null;
        }

        private static bool Fail(string message)
        {
            EditorUtility.DisplayDialog(DialogTitle, message, "OK");
            return false;
        }
    }
}
