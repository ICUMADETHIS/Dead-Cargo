using System.Collections.Generic;
using DeadCargo.AI;
using DeadCargo.Authority;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.EditorTools
{
    /// <summary>
    /// Gives the open scene's Authority Dead Cargo's own pilots (phase 6.14, steps 1 to 3).
    /// <list type="bullet">
    /// <item>Builds the Authority Pilot prefab if it is missing, starting from the kit's enemy pilot (its team and
    /// avoidance are copied; the kit's prefab is only read), and brings an older one up to date part by part.</item>
    /// <item>Creates the Rookie, Regular and Veteran pilot skills, and gives each heat tier that has none its skill:
    /// NOTICED Rookie, WANTED Regular, HUNTED Veteran.</item>
    /// </list>
    /// Existing parts, tuning and chosen skills are kept. Safe to run more than once.
    /// </summary>
    internal static class AuthorityPilotSetup
    {
        public const string PrefabPath = PrefabFolder + "/AuthorityPilot.prefab";

        private const string MenuPath = "Dead Cargo/Setup/Phase 6.14 - Authority Pilot (Open Scene)";
        private const string DialogTitle = "Phase 6.14 - Authority Pilot";
        private const string PrefabFolder = "Assets/DeadCargo/Prefabs/Authority";
        private const string SkillFolder = "Assets/DeadCargo/Data/AI";
        private const string KitPilotPath = "Assets/SpaceCombatKit/SpaceCombat/Demos/Prefabs/EnemyPilot_SpaceFighter.prefab";
        private const string KitShieldPath = "Assets/SpaceCombatKit/SpaceCombat/Demos/HealthTypes/Shield.asset";
        private const string KitHullPath = "Assets/SpaceCombatKit/SpaceCombat/Demos/HealthTypes/Armor.asset";
        private const string PilotLabel = "AUTHORITY";

        private const string AvoidancePart = "Avoidance";
        private const string FlightPart = "Flight";
        private const string AttackPart = "Attack Run";
        private const string BreakPart = "Break Away";
        private const string FlyToPart = "Fly To";
        private const string DogfightPart = "Dogfight";

        [MenuItem(MenuPath, priority = 215)]
        private static void Run()
        {
            Apply();
        }

        [MenuItem(MenuPath, true)]
        private static bool CanRun()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        /// <summary>True when the scene has an Authority Response, every one sends an up-to-date pilot, and every tier that sends ships has a skill.</summary>
        public static bool IsInstalled(Scene scene)
        {
            List<AuthorityResponse> responses = SceneSetupUtility.FindInScene<AuthorityResponse>(scene);
            foreach (AuthorityResponse response in responses)
            {
                var serialized = new SerializedObject(response);
                if (!IsCurrent(serialized.FindProperty("pilotPrefab").objectReferenceValue as GameAgent) || HasTierWithoutSkill(serialized))
                    return false;
            }

            return responses.Count > 0;
        }

        /// <summary>Sets up the open scene. Returns false when it cannot, after a dialog explains why.</summary>
        internal static bool Apply()
        {
            Scene scene = SceneManager.GetActiveScene();
            List<AuthorityResponse> responses = SceneSetupUtility.FindInScene<AuthorityResponse>(scene);
            if (responses.Count == 0)
            {
                EditorUtility.DisplayDialog(DialogTitle, "The open scene has no Authority Response. Run the Phase 4 setup first.", "OK");
                return false;
            }

            GameAgent prefab = EnsurePrefab();
            if (prefab == null)
            {
                EditorUtility.DisplayDialog(DialogTitle,
                    $"The kit's enemy pilot was not found at\n{KitPilotPath}\nwith its avoidance and attack behaviours, " +
                    $"or its health types at\n{KitShieldPath}\n{KitHullPath}\n\nThe Authority Pilot prefab is built from them.", "OK");
                return false;
            }

            PilotSkillDefinition[] skillsByTier = EnsureSkills();

            Undo.SetCurrentGroupName("Set Up Authority Pilot");
            int undoGroup = Undo.GetCurrentGroup();

            foreach (AuthorityResponse response in responses)
            {
                var serialized = new SerializedObject(response);
                serialized.FindProperty("pilotPrefab").objectReferenceValue = prefab;
                AssignTierSkills(serialized, skillsByTier);
                serialized.ApplyModifiedProperties();
            }

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"Authority Pilot set up in scene '{scene.name}'.", prefab);
            return true;
        }

        // Current means every part of the latest step is there, the kit's attack behaviour is gone, and the pilot can read its health.
        private static bool IsCurrent(GameAgent pilot)
        {
            if (pilot == null)
                return false;

            var brain = pilot.GetComponentInChildren<PilotBrain>(true);
            return brain != null &&
                new SerializedObject(brain).FindProperty("shieldType").objectReferenceValue != null &&
                pilot.GetComponentInChildren<PilotFlight>(true) != null &&
                pilot.GetComponentInChildren<AttackRunBehaviour>(true) != null &&
                pilot.GetComponentInChildren<SpaceshipAttackBehaviour>(true) == null;
        }

        // The first tier sends no ships, so only the tiers after it need a skill.
        private static bool HasTierWithoutSkill(SerializedObject response)
        {
            SerializedProperty tiers = response.FindProperty("tierResponses");
            for (int i = 1; i < tiers.arraySize; ++i)
            {
                SerializedProperty tier = tiers.GetArrayElementAtIndex(i);
                if (tier.FindPropertyRelative("maxShips").intValue > 0 && tier.FindPropertyRelative("pilotSkill").objectReferenceValue == null)
                    return true;
            }

            return false;
        }

        // Tier 1 (NOTICED) gets the first skill, tier 2 the second, and every tier above the last. Chosen skills are kept.
        private static void AssignTierSkills(SerializedObject response, PilotSkillDefinition[] skillsByTier)
        {
            SerializedProperty tiers = response.FindProperty("tierResponses");
            for (int i = 1; i < tiers.arraySize; ++i)
            {
                SerializedProperty skill = tiers.GetArrayElementAtIndex(i).FindPropertyRelative("pilotSkill");
                if (skill.objectReferenceValue == null)
                    skill.objectReferenceValue = skillsByTier[Mathf.Min(i - 1, skillsByTier.Length - 1)];
            }
        }

        private static PilotSkillDefinition[] EnsureSkills()
        {
            return new[]
            {
                EnsureSkill("RookiePilot", "Rookie", PilotSkill.Rookie),
                EnsureSkill("RegularPilot", "Regular", PilotSkill.Regular),
                EnsureSkill("VeteranPilot", "Veteran", PilotSkill.Veteran)
            };
        }

        // An existing asset, and any tuning on it, is kept.
        private static PilotSkillDefinition EnsureSkill(string fileName, string displayName, PilotSkill preset)
        {
            string path = $"{SkillFolder}/{fileName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<PilotSkillDefinition>(path);
            if (existing != null)
                return existing;

            SceneSetupUtility.EnsureFolder(SkillFolder);
            var skill = ScriptableObject.CreateInstance<PilotSkillDefinition>();
            skill.Configure(displayName, preset);
            AssetDatabase.CreateAsset(skill, path);
            AssetDatabase.SaveAssets();
            return skill;
        }

        private static GameAgent EnsurePrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameAgent>(PrefabPath);
            if (IsCurrent(existing))
                return existing;

            var kitPilot = AssetDatabase.LoadAssetAtPath<GameAgent>(KitPilotPath);
            var kitAvoidance = kitPilot != null ? kitPilot.GetComponentInChildren<ObstacleAvoidanceBehaviour>(true) : null;
            var kitShield = AssetDatabase.LoadAssetAtPath<HealthType>(KitShieldPath);
            var kitHull = AssetDatabase.LoadAssetAtPath<HealthType>(KitHullPath);
            if (kitPilot == null || kitAvoidance == null || kitShield == null || kitHull == null)
                return null;

            if (existing == null)
                CreateShell(kitPilot);

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                EnsureParts(root, kitAvoidance, kitShield, kitHull);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return AssetDatabase.LoadAssetAtPath<GameAgent>(PrefabPath);
        }

        // The game agent keeps the kit pilot's team, so the Authority hunts the player and nothing else.
        private static void CreateShell(GameAgent kitPilot)
        {
            SceneSetupUtility.EnsureFolder(PrefabFolder);
            var root = new GameObject("AuthorityPilot");
            try
            {
                var agent = root.AddComponent<GameAgent>();
                EditorUtility.CopySerialized(kitPilot, agent);
                SetString(agent, "label", PilotLabel);
                root.AddComponent<PilotBrain>();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // Adds whatever the pilot lacks, then wires every part to the others. Parts already there are left as they are.
        private static void EnsureParts(GameObject root, ObstacleAvoidanceBehaviour kitAvoidance, HealthType kitShield, HealthType kitHull)
        {
            PilotBrain brain = root.GetComponent<PilotBrain>();
            if (brain == null)
                brain = root.AddComponent<PilotBrain>();

            var avoidance = EnsurePart<ObstacleAvoidanceBehaviour>(root, AvoidancePart, added => EditorUtility.CopySerialized(kitAvoidance, added));
            var pilotFlight = EnsurePart<PilotFlight>(root, FlightPart, null);
            AttackRunBehaviour attack = EnsureAttackRun(root);
            var breakAway = EnsurePart<BreakAwayBehaviour>(root, BreakPart, null);
            var flyTo = EnsurePart<FlyToBehaviour>(root, FlyToPart, null);
            var dogfight = EnsurePart<DogfightBehaviour>(root, DogfightPart, null);

            SetReferences(pilotFlight, ("avoidance", avoidance));
            SetReferences(attack, ("pilotFlight", pilotFlight));
            SetReferences(breakAway, ("pilotFlight", pilotFlight));
            SetReferences(flyTo, ("pilotFlight", pilotFlight));
            SetReferences(dogfight, ("attack", attack), ("breakAway", breakAway));
            SetReferences(brain, ("pilotFlight", pilotFlight), ("dogfight", dogfight), ("flyTo", flyTo));
            SetIfEmpty(brain, "shieldType", kitShield);
            SetIfEmpty(brain, "hullType", kitHull);
        }

        // Behaviours sit on children of the pilot, as in the kit's pilots, so each shows on its own in the hierarchy.
        private static T EnsurePart<T>(GameObject root, string name, System.Action<T> onAdded) where T : Component
        {
            Transform child = root.transform.Find(name);
            if (child == null)
            {
                child = new GameObject(name).transform;
                child.SetParent(root.transform, false);
            }

            var part = child.GetComponent<T>();
            if (part != null)
                return part;

            part = child.gameObject.AddComponent<T>();
            onAdded?.Invoke(part);
            return part;
        }

        // Dead Cargo's attack run replaces the kit's attack behaviour; its timing now comes from the pilot's skill.
        private static AttackRunBehaviour EnsureAttackRun(GameObject root)
        {
            var attack = EnsurePart<AttackRunBehaviour>(root, AttackPart, null);
            var kitAttack = attack.GetComponent<SpaceshipAttackBehaviour>();
            if (kitAttack != null)
                Object.DestroyImmediate(kitAttack);

            return attack;
        }

        private static void SetReferences(Object target, params (string property, Object value)[] references)
        {
            var serialized = new SerializedObject(target);
            foreach ((string property, Object value) in references)
                serialized.FindProperty(property).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetIfEmpty(Object target, string property, Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty field = serialized.FindProperty(property);
            if (field.objectReferenceValue != null)
                return;

            field.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetString(Object target, string property, string value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty field = serialized.FindProperty(property);
            if (field == null)
                return;

            field.stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
