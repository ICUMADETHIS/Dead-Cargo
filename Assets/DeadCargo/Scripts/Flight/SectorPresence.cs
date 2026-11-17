using System;
using System.Collections.Generic;
using DeadCargo.Sectors.Field;
using DeadCargo.Visuals;
using UnityEngine;
using VSX.UniversalVehicleCombat;
using Object = UnityEngine.Object;

namespace DeadCargo.Flight
{
    /// <summary>
    /// The sector as a place the player can leave. While they are away in cruise, everything that belongs to the place
    /// sleeps where it stands: its asteroid field and field dust stop, and every root object that is part of the place is
    /// switched off (extraction points, pickups, voxel rocks, loose cargo, Authority ships). The player's ship, the player,
    /// the cameras, the kit's managers and the scene's folders stay awake, so heat, the run and the jump drive go on. On
    /// return it wakes in exactly the state it slept. Lives on the Sector Space object.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SectorPresence : MonoBehaviour
    {
        // The scene folders (see the editor's SceneHierarchy) hold systems, UI, cameras and visuals, which stay awake.
        private static readonly string[] AwakeFolders = { "Systems", "UI", "Cameras", "Environment" };

        private readonly List<GameObject> sleeping = new List<GameObject>();
        private readonly List<Behaviour> paused = new List<Behaviour>();

        /// <summary>The sector presence of the loaded scene, or null.</summary>
        public static SectorPresence Active { get; private set; }

        /// <summary>True while the player is away and the sector sleeps.</summary>
        public bool IsAsleep { get; private set; }

        /// <summary>Objects and components asleep now.</summary>
        public int SleepingCount => sleeping.Count + paused.Count;

        /// <summary>Raised when the sector falls asleep (true) or wakes (false).</summary>
        public event Action<bool> Changed;

        /// <summary>Puts the sector to sleep. Does nothing when it already sleeps.</summary>
        public void Sleep()
        {
            if (IsAsleep)
                return;

            IsAsleep = true;
            GameAgentManager agents = GameAgentManager.Instance;
            GameAgent player = agents != null ? agents.FocusedGameAgent : null;
            Transform playerRoot = player != null ? player.transform.root : null;
            Transform shipRoot = player != null && player.Vehicle != null ? player.Vehicle.transform.root : null;

            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                if (!root.activeSelf || StaysAwake(root, playerRoot, shipRoot))
                    continue;

                root.SetActive(false);
                sleeping.Add(root);
            }

            Pause<AsteroidField>();
            Pause<FieldDust>();
            Changed?.Invoke(true);
        }

        /// <summary>Wakes the sector as it slept. Does nothing when it is awake.</summary>
        public void Wake()
        {
            if (!IsAsleep)
                return;

            IsAsleep = false;
            foreach (GameObject root in sleeping)
            {
                if (root != null)
                    root.SetActive(true);
            }

            foreach (Behaviour behaviour in paused)
            {
                if (behaviour != null)
                    behaviour.enabled = true;
            }

            sleeping.Clear();
            paused.Clear();
            Changed?.Invoke(false);
        }

        // Statics survive play sessions when domain reload is off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Active = null;
        }

        private void OnEnable()
        {
            Active = this;
        }

        private void OnDisable()
        {
            if (Active == this)
                Active = null;
        }

        private static bool StaysAwake(GameObject root, Transform playerRoot, Transform shipRoot)
        {
            Transform transform = root.transform;
            return transform == playerRoot || transform == shipRoot ||
                   Array.IndexOf(AwakeFolders, root.name) >= 0 ||
                   root.GetComponentInChildren<Camera>(true) != null ||
                   root.GetComponentInChildren<GameAgentManager>(true) != null;
        }

        private void Pause<T>() where T : Behaviour
        {
            foreach (T behaviour in Object.FindObjectsByType<T>(FindObjectsSortMode.None))
            {
                if (behaviour.enabled && behaviour.gameObject.scene == gameObject.scene)
                {
                    behaviour.enabled = false;
                    paused.Add(behaviour);
                }
            }
        }
    }
}
