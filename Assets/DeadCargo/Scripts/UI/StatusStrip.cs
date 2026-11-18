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
    /// It also draws the comic reticle (<see cref="HudReticle"/>) and steering cursor (<see cref="HudSteeringCursor"/>) in
    /// place of the kit's: the kit's cursor still steers, its canvas only made invisible while the player flies the ship.
    /// Follows whichever ship the player flies.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas), typeof(CanvasScaler))]
    public sealed class StatusStrip : MonoBehaviour
    {
        private const int SortingOrder = 9;
        private const float ReticleSize = 160f;

        // The kit cursor's field holding the canvas of its cursor, the cursor's line and its centre dot.
        private static readonly System.Reflection.FieldInfo KitCursorCanvasField = typeof(HUDCursor).GetField("canvas",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        [Tooltip("The kit's health type for shields. Set by Set Up Sector from the kit's shield bar.")]
        [SerializeField] private HealthType shieldType;

        [Tooltip("The kit's health type for the hull. Set by Set Up Sector from the kit's armour bar.")]
        [SerializeField] private HealthType hullType;

        private readonly List<ModuleMount> weaponMounts = new List<ModuleMount>();

        private StatusStripView view;
        private HudReticle reticle;
        private HudSteeringCursor steeringCursor;
        private GameAgentManager agents;
        private Vehicle vehicle;
        private Health health;
        private Rigidbody body;
        private VehicleEngines3D engines;
        private CruiseDrive cruise;
        private HUDCursor kitCursor;
        private GameObject kitCursorCanvas;
        private CanvasGroup kitCursorHider;
        private bool addedKitCursorHider;
        private float kitCursorAlpha = 1f;

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
            var canvasRect = (RectTransform)transform;
            reticle = HudFactory.Anchored("Reticle", canvasRect, new Vector2(0.5f, 0.5f), ReticleSize, ReticleSize)
                .gameObject.AddComponent<HudReticle>();
            steeringCursor = HudFactory.Anchored("Steering Cursor", canvasRect, new Vector2(0.5f, 0.5f), ReticleSize, ReticleSize)
                .gameObject.AddComponent<HudSteeringCursor>();
            view = new StatusStripView(canvasRect);
            view.SetVisible(false);
            SetReticleVisible(false);
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

            UpdateReticle();
        }

        // Shown while the kit would show its cursor, or always when the ship has none; the dot follows the kit's cursor.
        private void UpdateReticle()
        {
            bool shown = kitCursor == null || (kitCursorCanvas != null ? kitCursorCanvas.activeInHierarchy : kitCursor.isActiveAndEnabled);
            SetReticleVisible(shown);
            if (!shown)
                return;

            // The kit reports the bottom-left corner while it has no camera yet; the dot waits at the centre.
            Vector3 viewport = kitCursor != null ? kitCursor.ViewportPosition : new Vector3(0.5f, 0.5f, 1f);
            if (viewport.x <= 0f && viewport.y <= 0f)
                viewport = new Vector3(0.5f, 0.5f, 1f);

            Vector2 canvasSize = ((RectTransform)transform).rect.size;
            steeringCursor.SetOffset(Vector2.Scale(new Vector2(viewport.x - 0.5f, viewport.y - 0.5f), canvasSize));
        }

        private void SetReticleVisible(bool visible)
        {
            if (reticle.gameObject.activeSelf != visible)
                reticle.gameObject.SetActive(visible);
            if (steeringCursor.gameObject.activeSelf != visible)
                steeringCursor.gameObject.SetActive(visible);
        }

        // The kit's cursor keeps steering and aiming; only its look is hidden, on the canvas that holds the cursor, its line
        // and the centre dot, and nothing else. The kit moves that canvas onto the camera when the view changes and gives
        // no public handle on it, so it is read from the kit cursor's own serialized field.
        private void HideKitCursor()
        {
            kitCursor = vehicle.GetComponentInChildren<HUDCursor>(true);
            if (kitCursor == null)
                return;

            var kitCanvas = KitCursorCanvasField?.GetValue(kitCursor) as Canvas;
            if (kitCanvas == null)
            {
                Debug.LogWarning($"Status Strip found no canvas on the kit cursor of '{vehicle.name}', so the kit's cursor shows " +
                                 "beside the comic reticle.", vehicle);
                return;
            }

            kitCursorCanvas = kitCanvas.gameObject;

            addedKitCursorHider = !kitCursorCanvas.TryGetComponent(out kitCursorHider);
            if (addedKitCursorHider)
                kitCursorHider = kitCursorCanvas.AddComponent<CanvasGroup>();

            kitCursorAlpha = kitCursorHider.alpha;
            kitCursorHider.alpha = 0f;
        }

        private void RestoreKitCursor()
        {
            if (kitCursorHider != null)
            {
                if (addedKitCursorHider)
                    Destroy(kitCursorHider);
                else
                    kitCursorHider.alpha = kitCursorAlpha;
            }

            kitCursor = null;
            kitCursorCanvas = null;
            kitCursorHider = null;
            addedKitCursorHider = false;
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
            SetReticleVisible(vehicle != null);
            if (vehicle == null)
                return;

            vehicle.onDestroyed.AddListener(OnVehicleDestroyed);
            HideKitCursor();
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
            SetReticleVisible(false);
        }

        private void Unbind()
        {
            if (vehicle != null)
                vehicle.onDestroyed.RemoveListener(OnVehicleDestroyed);

            RestoreKitCursor();
            weaponMounts.Clear();
            health = null;
            body = null;
            engines = null;
            cruise = null;
        }
    }
}
