using DeadCargo.Cargo;
using DeadCargo.Flow;
using UnityEditor;
using UnityEngine;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.EditorTools.GodMenu
{
    /// <summary>Spawn and clear things in the running scene.</summary>
    internal sealed class WorldTab : GodMenuTab
    {
        private static readonly string[] CrateLabels = { "Spawn Crate", "Clear All Crates" };

        private int crateItemIndex;
        private int crateQuantity = 1;
        private float crateDistance = 40f;

        public override string Title => "World";
        public override GodMenuGroup Group => GodMenuGroup.Run;
        public override int Order => 50;

        // Sector tools only: the mothership has no world to spawn into.
        public override bool IsAvailable => GodMenuContext.IsPlaying && !SceneFlow.InMothership;

        public override void OnGUI()
        {
            DrawEnemies();
            DrawCargo();
        }

        private static void DrawEnemies()
        {
            using (var section = new GodMenuGUI.SectionScope("Enemies"))
            {
                if (!section.Expanded)
                    return;

                PilotedVehicleSpawn[] spawns = GodMenuContext.IsPlaying
                    ? Object.FindObjectsByType<PilotedVehicleSpawn>(FindObjectsSortMode.None)
                    : new PilotedVehicleSpawn[0];

                if (spawns.Length == 0)
                {
                    GUILayout.Label(GodMenuContext.IsPlaying ? "No spawn points in the scene." : "Listed in Play Mode.", EditorStyles.miniLabel);
                    return;
                }

                foreach (PilotedVehicleSpawn spawn in spawns)
                {
                    bool alive = spawn.Spawned && !spawn.Destroyed;

                    GodMenuGUI.BeginItemRow(spawn.name, alive ? "Active" : "Ready");
                    bool clicked;
                    using (new EditorGUI.DisabledScope(alive))
                        clicked = GodMenuGUI.MiniButton("Spawn", 60f);
                    GodMenuGUI.EndItemRow();

                    if (clicked)
                    {
                        spawn.Spawn();
                        spawn.Vehicle.transform.SetPositionAndRotation(spawn.transform.position, spawn.transform.rotation);
                    }
                }
            }
        }

        private void DrawCargo()
        {
            using (var section = new GodMenuGUI.SectionScope("Loose Cargo"))
            {
                if (!section.Expanded)
                    return;

                var cratePrefab = AssetDatabase.LoadAssetAtPath<CargoPickup>(GodMenuContext.CratePrefabPath);
                if (cratePrefab == null || GodMenuContext.CargoItems.Count == 0)
                {
                    EditorGUILayout.HelpBox("Crate prefab or cargo items missing. Run the Phase 1 and 2 setups from the Project tab.", MessageType.Warning);
                    return;
                }

                crateItemIndex = Mathf.Clamp(crateItemIndex, 0, GodMenuContext.CargoItems.Count - 1);
                crateItemIndex = EditorGUILayout.Popup("Item", crateItemIndex, GodMenuContext.CargoItemNames);
                crateQuantity = Mathf.Max(1, EditorGUILayout.IntField("Quantity", crateQuantity));
                crateDistance = EditorGUILayout.Slider("Distance", crateDistance, 10f, 200f);

                Vehicle vehicle = GodMenuContext.PlayerVehicle;
                bool[] enabled = { vehicle != null, true };

                switch (GodMenuGUI.ButtonRow(CrateLabels, enabled))
                {
                    case 0:
                        // Dark matter spawns as its glowing sphere, everything else as a crate.
                        CargoItemDefinition item = GodMenuContext.CargoItems[crateItemIndex];
                        Vector3 position = vehicle.transform.position + vehicle.transform.forward * crateDistance;
                        CargoPickup pickup = Object.Instantiate(GodMenuContext.LoadPickupPrefab(item), position, Quaternion.identity);
                        pickup.Initialize(item, crateQuantity);
                        break;
                    case 1:
                        GodMenuContext.ClearLooseCargo();
                        break;
                }
            }
        }
    }
}
