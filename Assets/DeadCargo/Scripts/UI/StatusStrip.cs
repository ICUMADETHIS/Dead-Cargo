using System.Collections.Generic;
using DeadCargo.Flight;
using UnityEngine;
using UnityEngine.UI;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.UI
{
    /// <summary>
    /// The player's ship status in one comic strip at the bottom centre (<see cref="StatusStripView"/>): shield and hull,
    /// speed and throttle, and both weapons with their rounds. While the autopilot cruises it shows the cruise speed in
    /// kilometres a second. It replaces the kit's health bars, weapon boxes and speed arcs, which Set Up Sector hides.
    /// Follows whichever ship the player flies.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas), typeof(CanvasScaler))]
    public sealed class StatusStrip : MonoBehaviour
    {
        private const int SortingOrder = 9;

        [Tooltip("The kit's health type for shields. Set by Set Up Sector from the kit's shield bar.")]
        [SerializeField] private HealthType shieldType;

        [Tooltip("The kit's health type for the hull. Set by Set Up Sector from the kit's armour bar.")]
        [SerializeField] private HealthType hullType;

        private readonly List<ModuleMount> weaponMounts = new List<ModuleMount>();

        private StatusStripView view;
        private GameAgentManager agents;
        private Vehicle vehicle;
        private Health health;
        private Rigidbody body;
        private VehicleEngines3D engines;
        private CruiseDrive cruise;

        /// <summary>True when both health types are set.</summary>
        public bool HasHealthTypes => shieldType != null && hullType != null;

        /// <summary>The strip of the loaded scene, or null.</summary>
        public static StatusStrip Active { get; private set; }

        /// <summary>How much of the screen's bottom the strip covers now, in reference pixels: 0 while it is hidden.</summary>
        public float CoveredHeight => view != null && view.Visible ? StatusStripView.CoveredHeight : 0f;

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

        private void Awake()
        {
            HudFactory.ConfigureCanvas(gameObject, SortingOrder);
            view = new StatusStripView((RectTransform)transform);
            view.SetVisible(false);
        }

        private void Start()
        {
            agents = GameAgentManager.Instance;
            if (agents == null)
                return;

            agents.onFocusedVehicleChanged.AddListener(OnFocusedVehicleChanged);
            GameAgent player = agents.FocusedGameAgent;
            OnFocusedVehicleChanged(player != null ? player.Vehicle : null);
        }

        private void OnDestroy()
        {
            if (agents != null)
                agents.onFocusedVehicleChanged.RemoveListener(OnFocusedVehicleChanged);

            Unbind();
        }

        private void Update()
        {
            if (vehicle == null)
                return;

            if (health != null)
                view.SetHealth(Share(shieldType), Share(hullType));
            else
                view.SetHealth(-1f, -1f);

            bool cruising = cruise != null && cruise.State == CruiseState.Cruising;
            double speed = cruising ? cruise.Speed : body != null ? body.linearVelocity.magnitude : 0.0;
            float throttle = engines != null ? Mathf.Clamp01(engines.MovementInputs.z) : 0f;
            bool boosting = engines != null && engines.BoostInputs.z > 0.5f;
            view.SetSpeed(speed, cruising, throttle, boosting);

            for (int slot = 0; slot < StatusStripView.WeaponSlots; ++slot)
                ShowWeapon(slot);
        }

        private float Share(HealthType type)
        {
            return type != null ? Mathf.Clamp01(health.GetCurrentHealthFractionByType(type)) : -1f;
        }

        // The weapon in a slot, its name and its rounds; a slot with no weapon is left empty.
        private void ShowWeapon(int slot)
        {
            Weapon weapon = null;
            Module module = null;
            if (slot < weaponMounts.Count && weaponMounts[slot] != null)
            {
                module = weaponMounts[slot].MountedModule();
                weapon = module != null ? module.GetComponent<Weapon>() : null;
            }

            if (weapon == null)
            {
                view.SetWeapon(slot, false, null, 0, 0);
                return;
            }

            ResourceContainerBase rounds = Rounds(weapon);
            view.SetWeapon(slot, true, module.Label.ToUpperInvariant(), rounds != null ? rounds.CurrentAmountInteger : 0,
                rounds != null ? rounds.CapacityInteger : 0);
        }

        private static ResourceContainerBase Rounds(Weapon weapon)
        {
            foreach (ResourceHandler handler in weapon.ResourceHandlers)
            {
                if (handler != null && handler.resourceContainer != null)
                    return handler.resourceContainer;
            }

            return null;
        }

        private void OnFocusedVehicleChanged(Vehicle current)
        {
            GameAgent player = agents.FocusedGameAgent;
            if (player == null || !player.IsPlayer || current == null || current.Destroyed)
                current = null;

            if (current == vehicle)
                return;

            Unbind();
            vehicle = current;
            view.SetVisible(vehicle != null);
            if (vehicle == null)
                return;

            vehicle.onDestroyed.AddListener(OnVehicleDestroyed);
            health = vehicle.GetComponentInChildren<Health>();
            body = vehicle.GetComponent<Rigidbody>();
            engines = vehicle.GetComponentInChildren<VehicleEngines3D>();
            cruise = vehicle.GetComponent<CruiseDrive>();

            // Mounts holding weapons when the ship is entered, in the ship's order: the first is key 1, the next key 2.
            foreach (ModuleMount mount in vehicle.ModuleMounts)
            {
                Module module = mount != null ? mount.MountedModule() : null;
                if (module != null && module.GetComponent<Weapon>() != null)
                    weaponMounts.Add(mount);
            }
        }

        private void OnVehicleDestroyed()
        {
            Unbind();
            vehicle = null;
            view.SetVisible(false);
        }

        private void Unbind()
        {
            if (vehicle != null)
                vehicle.onDestroyed.RemoveListener(OnVehicleDestroyed);

            weaponMounts.Clear();
            health = null;
            body = null;
            engines = null;
            cruise = null;
        }
    }
}
