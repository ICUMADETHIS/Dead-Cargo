using DeadCargo.Cargo;
using DeadCargo.Flow;
using DeadCargo.Heat;
using DeadCargo.UI;
using UnityEngine;
using UnityEngine.UI;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.DevTesting
{
    [DisallowMultipleComponent]
    public sealed class DevTestRange : MonoBehaviour
    {
        [SerializeField] private Vehicle character;
        [SerializeField] private Vehicle fighter;
        [SerializeField] private PilotedVehicleSpawn enemySpawn;

        internal bool CanSpawnEnemy => enemySpawn != null && (!enemySpawn.Spawned || enemySpawn.Destroyed);

        private void Awake()
        {
            // The panel opens when play starts in the range, not when a run arrives from the mothership.
            DevTestConsole.StartSession(!SceneFlow.IsLoading);
            EnsureHeatSystems();
            ExtractionHud.Install(gameObject.scene);
        }

        private void EnsureHeatSystems()
        {
            bool hasTracker = false;
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<HeatTracker>(true) != null)
                {
                    hasTracker = true;
                    break;
                }
            }

            if (!hasTracker)
                gameObject.AddComponent<HeatTracker>();

            // Reuse the phase components when the range already has a tuned setup.
            if (fighter == null)
                return;
            if (!fighter.TryGetComponent<WeaponHeatSource>(out _))
                fighter.gameObject.AddComponent<WeaponHeatSource>();
            if (fighter.TryGetComponent<CargoHold>(out _) && !fighter.TryGetComponent<CargoHeatSource>(out _))
                fighter.gameObject.AddComponent<CargoHeatSource>();
        }

        private void Start()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                foreach (var button in root.GetComponentsInChildren<Button>(true))
                {
                    var click = button.onClick;
                    if (button.name != "QuitButton" || click.GetPersistentEventCount() != 1 ||
                        !(click.GetPersistentTarget(0) is SceneLoader) ||
                        click.GetPersistentMethodName(0) != nameof(SceneLoader.LoadScene))
                        continue;

                    // The range's kit menus originally quit back to the demo main menu.
                    button.onClick = new Button.ButtonClickedEvent();
                    button.onClick.AddListener(QuitRange);
                }
            }
        }

        private static void QuitRange()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void RestartRange()
        {
            DevTestConsole.RestartRange();
        }

        internal bool CanBoardFighter(GameAgent player)
        {
            return player != null && !player.IsDead && player.Vehicle == character && fighter != null &&
                !fighter.Destroyed && fighter.Occupants.Count == 0;
        }

        internal void BoardFighter(GameAgent player)
        {
            if (!CanBoardFighter(player))
                return;

            var child = character.GetComponentInChildren<VehicleEnterExitManager>();
            var parent = fighter.GetComponentInChildren<VehicleEnterExitManager>();
            if (child == null || parent == null || !child.CanEnter(parent))
                return;

            var alreadyInRange = child.EnterableVehicles.Contains(parent);
            child.AddEnterableVehicle(parent);
            var index = child.EnterableVehicles.IndexOf(parent);
            if (index < 0)
                return;

            child.EnterParent(index);
            if (!alreadyInRange)
                child.RemoveEnterableVehicle(parent);

            player.EnterVehicle(fighter);
        }

        internal void SpawnEnemy()
        {
            if (!CanSpawnEnemy)
                return;

            enemySpawn.Spawn();
            enemySpawn.Vehicle.transform.SetPositionAndRotation(
                enemySpawn.transform.position, enemySpawn.transform.rotation);
        }
    }
}
