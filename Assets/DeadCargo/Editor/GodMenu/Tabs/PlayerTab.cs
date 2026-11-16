using UnityEditor;
using UnityEngine;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.EditorTools.GodMenu
{
    /// <summary>Status and cheats for the vehicle the player controls.</summary>
    internal sealed class PlayerTab : GodMenuTab
    {
        private const int FreezeButton = 0;
        private const int StopButton = 1;
        private const int TeleportButton = 2;

        private static readonly string[] HealthLabels = { "Restore Health", "Destroy Vehicle" };
        private static readonly string[] MovementLabels = { "Freeze", "Stop", "Teleport To Scene View" };
        private static readonly string[] FrozenMovementLabels = { "Unfreeze", "Stop", "Teleport To Scene View" };

        private bool invulnerable;
        private Vehicle invulnerableVehicle;
        private bool frozen;
        private Vehicle frozenVehicle;
        private bool frozenBodyHeld;
        private RigidbodyConstraints frozenBodyConstraints;

        public override string Title => "Player";
        public override GodMenuGroup Group => GodMenuGroup.Run;
        public override int Order => 0;

        public override bool IsAvailable => GodMenuContext.Player != null;

        public override string Summary
        {
            get
            {
                Vehicle vehicle = GodMenuContext.PlayerVehicle;
                if (vehicle == null)
                    return null;

                string flags = (frozen ? "  ·  frozen" : "") + (invulnerable ? "  ·  invulnerable" : "");
                return vehicle.Label + flags;
            }
        }

        public override void OnUpdate()
        {
            if (!GodMenuContext.IsPlaying)
            {
                invulnerable = false;
                invulnerableVehicle = null;
                frozen = false;
                frozenVehicle = null;
                frozenBodyHeld = false;
                return;
            }

            // Both cheats follow the player between vehicles, so they survive boarding and exiting.
            Vehicle vehicle = GodMenuContext.PlayerVehicle;
            if (invulnerable && vehicle != invulnerableVehicle)
                ApplyInvulnerability(vehicle);
            if (frozen && vehicle != frozenVehicle)
                ApplyFreeze(vehicle);
        }

        public override void OnGUI()
        {
            Vehicle vehicle = GodMenuContext.PlayerVehicle;
            using (new EditorGUI.DisabledScope(vehicle == null))
            {
                DrawStatus(vehicle);
                DrawHealth(vehicle);
                DrawMovement(vehicle);
            }
        }

        private static void DrawStatus(Vehicle vehicle)
        {
            using (var section = new GodMenuGUI.SectionScope("Status"))
            {
                if (!section.Expanded)
                    return;

                if (vehicle == null)
                {
                    GodMenuGUI.Field("Vehicle", "None");
                    return;
                }

                Rigidbody body = vehicle.CachedRigidbody;
                GodMenuGUI.Field("Vehicle", vehicle.Label);
                GodMenuGUI.Field("Speed", body != null ? $"{body.linearVelocity.magnitude:0.0} m/s" : "n/a");
                GodMenuGUI.Field("Mass", body != null ? $"{body.mass:0.00}" : "n/a");
            }
        }

        private void DrawHealth(Vehicle vehicle)
        {
            using (var section = new GodMenuGUI.SectionScope("Health"))
            {
                if (!section.Expanded)
                    return;

                Health health = vehicle != null ? vehicle.GetComponentInChildren<Health>() : null;
                if (health != null)
                {
                    foreach (Damageable damageable in health.Damageables)
                    {
                        if (damageable == null || damageable.HealthType == null)
                            continue;

                        float fraction = damageable.HealthCapacity > 0f ? damageable.CurrentHealth / damageable.HealthCapacity : 0f;
                        string label = $"{damageable.HealthType.name}   {damageable.CurrentHealth:0} / {damageable.HealthCapacity:0}";
                        GodMenuGUI.Bar(fraction, label, Color.Lerp(GodMenuGUI.Negative, GodMenuGUI.Positive, fraction));
                    }
                }

                bool wantInvulnerable = EditorGUILayout.Toggle("Invulnerable", invulnerable);
                if (wantInvulnerable != invulnerable)
                {
                    invulnerable = wantInvulnerable;
                    ApplyInvulnerability(invulnerable ? vehicle : null);
                }

                int clicked = GodMenuGUI.ButtonRow(HealthLabels);
                if (health == null)
                    return;

                if (clicked == 0)
                    health.ResetHealth();
                else if (clicked == 1)
                    health.DestroyAllDamageables();
            }
        }

        private void DrawMovement(Vehicle vehicle)
        {
            using (var section = new GodMenuGUI.SectionScope("Movement"))
            {
                if (!section.Expanded)
                    return;

                SceneView sceneView = SceneView.lastActiveSceneView;
                bool[] enabled = { true, true, sceneView != null };

                // The freeze button is tinted while the cheat is on.
                string[] labels = frozen ? FrozenMovementLabels : MovementLabels;
                int clicked = GodMenuGUI.ButtonRow(labels, enabled, frozen ? FreezeButton : -1, GodMenuGUI.Accent);
                if (vehicle == null || clicked < 0)
                    return;

                if (clicked == FreezeButton)
                {
                    frozen = !frozen;
                    ApplyFreeze(frozen ? vehicle : null);
                    return;
                }

                StopVehicle(vehicle);
                if (clicked == TeleportButton)
                {
                    Transform camera = sceneView.camera.transform;
                    vehicle.transform.SetPositionAndRotation(camera.position, camera.rotation);
                }
            }
        }

        // Freezes the given vehicle and releases the previous one. Pass null to switch the cheat off.
        private void ApplyFreeze(Vehicle vehicle)
        {
            if (frozenVehicle != null)
                ReleaseVehicle(frozenVehicle);

            frozenVehicle = vehicle;
            if (vehicle != null)
                HoldVehicle(vehicle);
        }

        // Ships move through physics forces, so their body is locked. The on-foot character moves through
        // its own controller, so that controller is paused; looking around still works.
        private void HoldVehicle(Vehicle vehicle)
        {
            Rigidbody body = vehicle.CachedRigidbody;
            frozenBodyHeld = body != null && !body.isKinematic;
            if (frozenBodyHeld)
            {
                frozenBodyConstraints = body.constraints;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.constraints = RigidbodyConstraints.FreezeAll;
            }

            if (vehicle.TryGetComponent(out FirstPersonCharacterController character))
                character.enabled = false;
        }

        private void ReleaseVehicle(Vehicle vehicle)
        {
            Rigidbody body = vehicle.CachedRigidbody;
            if (frozenBodyHeld && body != null)
                body.constraints = frozenBodyConstraints;

            frozenBodyHeld = false;
            if (vehicle.TryGetComponent(out FirstPersonCharacterController character))
                character.enabled = true;
        }

        private static void StopVehicle(Vehicle vehicle)
        {
            Rigidbody body = vehicle.CachedRigidbody;
            if (body == null || body.isKinematic)
                return;

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        // Makes the given vehicle invulnerable and restores the previous one. Pass null to switch the cheat off.
        private void ApplyInvulnerability(Vehicle vehicle)
        {
            SetDamageable(invulnerableVehicle, true);
            SetDamageable(vehicle, false);
            invulnerableVehicle = vehicle;
        }

        private static void SetDamageable(Vehicle vehicle, bool isDamageable)
        {
            if (vehicle == null)
                return;

            foreach (Damageable damageable in vehicle.GetComponentsInChildren<Damageable>(true))
                damageable.SetDamageable(isDamageable);
        }
    }
}
