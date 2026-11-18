using System.Collections.Generic;
using DeadCargo.Cargo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.EditorTools
{
    /// <summary>
    /// Phase 6.6 step 4b, in the open scene: the player's ship starts the run in flight. The sector and the test range had
    /// the kit's ship lander start the ship Landed. That holds its body kinematic until a launch, so the run began parked
    /// and the arrival could not move the ship. This sets the lander to Launched and the body to dynamic, as the kit's
    /// fighter prefab has them. It also switches on and off what a launch would (the lander's launch events), such as
    /// hiding the bubble that marks where to board a landed ship. Safe to run more than once; every change can be undone.
    /// </summary>
    internal static class FlightStartSetup
    {
        private const string MenuPath = "Dead Cargo/Setup/Flight - Start In Flight (Open Scene)";
        private const string DialogTitle = "Start In Flight";
        private const string UndoName = "Start Ship In Flight";
        private const string StateProperty = "currentState";
        private const string SetActiveMethod = "SetActive";

        // The lander's events in the order a launch raises them; a later call wins.
        private static readonly string[] LaunchEvents = { "onShipLaunching", "onShipLaunched" };

        /// <summary>True when every player ship in the scene (one with a cargo hold) starts launched and dynamic.</summary>
        public static bool IsInstalled(Scene scene)
        {
            foreach (CargoHold hold in SceneSetupUtility.FindInScene<CargoHold>(scene))
            {
                if (!StartsInFlight(hold.gameObject))
                    return false;
            }

            return true;
        }

        [MenuItem(MenuPath, priority = 217)]
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

            if (StartsInFlight(ship.gameObject))
                return true;

            Undo.SetCurrentGroupName(UndoName);
            int undoGroup = Undo.GetCurrentGroup();

            if (ship.TryGetComponent(out ShipLander lander))
            {
                if (lander.CurrentState != ShipLander.ShipLanderState.Launched)
                {
                    using (var serialized = new SerializedObject(lander))
                    {
                        serialized.FindProperty(StateProperty).enumValueIndex = (int)ShipLander.ShipLanderState.Launched;
                        serialized.ApplyModifiedProperties();
                    }
                }

                foreach (KeyValuePair<GameObject, bool> change in LaunchActivations(lander))
                {
                    if (change.Key.activeSelf == change.Value)
                        continue;

                    Undo.RecordObject(change.Key, UndoName);
                    change.Key.SetActive(change.Value);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(change.Key);
                }
            }

            if (ship.TryGetComponent(out Rigidbody body) && body.isKinematic)
            {
                Undo.RecordObject(body, UndoName);
                body.isKinematic = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(body);
            }

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"'{ship.name}' now starts the run in flight in scene '{scene.name}'. Save the scene to keep it.", ship);
            return true;
        }

        private static bool StartsInFlight(GameObject ship)
        {
            if (ship.TryGetComponent(out ShipLander lander))
            {
                if (lander.CurrentState != ShipLander.ShipLanderState.Launched)
                    return false;

                foreach (KeyValuePair<GameObject, bool> change in LaunchActivations(lander))
                {
                    if (change.Key.activeSelf != change.Value)
                        return false;
                }
            }

            return !ship.TryGetComponent(out Rigidbody body) || !body.isKinematic;
        }

        // The objects a launch switches on or off through the lander's events, with the state it leaves each in.
        private static List<KeyValuePair<GameObject, bool>> LaunchActivations(ShipLander lander)
        {
            var activations = new List<KeyValuePair<GameObject, bool>>();
            using (var serialized = new SerializedObject(lander))
            {
                foreach (string eventName in LaunchEvents)
                {
                    SerializedProperty calls = serialized.FindProperty(eventName + ".m_PersistentCalls.m_Calls");
                    if (calls == null)
                        continue;

                    for (int i = 0; i < calls.arraySize; ++i)
                    {
                        SerializedProperty call = calls.GetArrayElementAtIndex(i);
                        if (call.FindPropertyRelative("m_CallState").intValue == (int)UnityEventCallState.Off
                            || call.FindPropertyRelative("m_MethodName").stringValue != SetActiveMethod
                            || !(call.FindPropertyRelative("m_Target").objectReferenceValue is GameObject target))
                            continue;

                        bool active = call.FindPropertyRelative("m_Arguments.m_BoolArgument").boolValue;
                        activations.RemoveAll(entry => entry.Key == target);
                        activations.Add(new KeyValuePair<GameObject, bool>(target, active));
                    }
                }
            }

            return activations;
        }

        private static bool Fail(string message)
        {
            EditorUtility.DisplayDialog(DialogTitle, message, "OK");
            return false;
        }
    }
}
