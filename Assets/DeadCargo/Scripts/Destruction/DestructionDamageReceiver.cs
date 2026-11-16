using UnityEngine;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.Destruction
{
    /// <summary>
    /// Bridges the kit's weapons to Dead Cargo destruction. Kit projectiles, beams and hitscan guns look for a
    /// <see cref="DamageReceiver"/> on the collider they hit; this one turns each hit into a <see cref="DamageEvent"/>
    /// for the destructible it is bound to. Sits beside the collider, with no health of its own.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DestructionDamageReceiver : DamageReceiver
    {
        private IDestructible target;

        /// <summary>The destructible that receives this collider's hits.</summary>
        public IDestructible Target => target;

        public void Bind(IDestructible destructible)
        {
            target = destructible;
        }

        protected override void Awake()
        {
            // Added from code, the kit's events are left unset, and the kit invokes them unchecked on every hit.
            onDamaged ??= new OnDamageReceiverDamagedEventHandler();
            onHealed ??= new OnDamageReceiverHealedEventHandler();
            onCollision ??= new OnCollisionEnterEventHandler();
            onActivated ??= new OnDamageReceiverActivatedEventHandler();
            onDeactivated ??= new OnDamageReceiverDeactivatedEventHandler();
            base.Awake();
        }

        public override void Damage(float damage, Vector3 hitPoint, HealthModifierType healthModifierType, Transform damageSourceRootTransform)
        {
            base.Damage(damage, hitPoint, healthModifierType, damageSourceRootTransform);
            if (target == null || damage <= 0f)
                return;

            Vector3 direction = Vector3.zero;
            if (damageSourceRootTransform != null)
            {
                Vector3 travel = hitPoint - damageSourceRootTransform.position;
                float length = travel.magnitude;
                if (length > 1e-3f)
                    direction = travel / length;
            }

            target.ApplyDamage(new DamageEvent(damage, hitPoint, direction, DamageKind.Weapon, 0f, damageSourceRootTransform));
        }
    }
}
