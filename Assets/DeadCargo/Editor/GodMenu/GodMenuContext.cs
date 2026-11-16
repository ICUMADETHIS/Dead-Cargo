using System.Collections.Generic;
using DeadCargo.Cargo;
using UnityEditor;
using UnityEngine;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.EditorTools.GodMenu
{
    /// <summary>Live game state and project assets shared by the God Menu tabs.</summary>
    internal static class GodMenuContext
    {
        public const string CratePrefabPath = "Assets/DeadCargo/Prefabs/Cargo/CargoCrate.prefab";

        private static readonly List<CargoItemDefinition> cargoItems = new List<CargoItemDefinition>();
        private static string[] cargoItemNames = new string[0];

        public static bool IsPlaying => EditorApplication.isPlaying;
        public static IReadOnlyList<CargoItemDefinition> CargoItems => cargoItems;
        public static string[] CargoItemNames => cargoItemNames;

        /// <summary>The player's game agent, or null outside play mode.</summary>
        public static GameAgent Player
        {
            get
            {
                GameAgentManager manager = GameAgentManager.Instance;
                return IsPlaying && manager != null ? manager.FocusedGameAgent : null;
            }
        }

        /// <summary>The vehicle the player currently controls, or null.</summary>
        public static Vehicle PlayerVehicle
        {
            get
            {
                GameAgent player = Player;
                return player != null ? player.Vehicle : null;
            }
        }

        /// <summary>The hold on the player's vehicle, or the first hold in the scene when the player is on foot.</summary>
        public static CargoHold Hold
        {
            get
            {
                if (!IsPlaying)
                    return null;

                Vehicle vehicle = PlayerVehicle;
                CargoHold hold = vehicle != null ? vehicle.GetComponent<CargoHold>() : null;
                return hold != null ? hold : Object.FindFirstObjectByType<CargoHold>();
            }
        }

        /// <summary>Reloads the cargo item assets from the project.</summary>
        public static void RefreshAssets()
        {
            cargoItems.Clear();
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(CargoItemDefinition)))
            {
                var item = AssetDatabase.LoadAssetAtPath<CargoItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (item != null)
                    cargoItems.Add(item);
            }

            cargoItems.Sort((a, b) => string.CompareOrdinal(a.DisplayName, b.DisplayName));

            cargoItemNames = new string[cargoItems.Count];
            for (int i = 0; i < cargoItems.Count; ++i)
                cargoItemNames[i] = cargoItems[i].DisplayName;
        }

        /// <summary>Destroys every loose cargo crate in the scene.</summary>
        public static void ClearLooseCargo()
        {
            foreach (CargoPickup pickup in Object.FindObjectsByType<CargoPickup>(FindObjectsSortMode.None))
                Object.Destroy(pickup.gameObject);
        }
    }
}
