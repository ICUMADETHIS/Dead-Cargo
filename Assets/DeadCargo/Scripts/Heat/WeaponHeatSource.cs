using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.Heat
{
    /// <summary>Raises heat when the ship fires its weapons.</summary>
    [DisallowMultipleComponent]
    public sealed class WeaponHeatSource : MonoBehaviour
    {
        [Tooltip("Heat added per shot, and per activation of a continuous weapon such as a beam.")]
        [SerializeField, Min(0f)] private float heatPerShot = 0.15f;

        private readonly List<Triggerable> boundWeapons = new List<Triggerable>();

        /// <summary>Re-scans the ship for weapons. Call after the loadout changes.</summary>
        public void Rebind()
        {
            Unbind();
            GetComponentsInChildren(true, boundWeapons);

            foreach (Triggerable weapon in boundWeapons)
            {
                // Continuous weapons never raise onAction, so they are counted when switched on.
                if (weapon.TriggerModeSetting == Triggerable.TriggerMode.OnOff)
                    weapon.onStartTriggering.AddListener(OnFired);
                else
                    weapon.onAction.AddListener(OnFired);
            }
        }

        private IEnumerator Start()
        {
            // Module mounts load their weapons during startup, so bind a frame later.
            yield return null;
            Rebind();
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void Unbind()
        {
            foreach (Triggerable weapon in boundWeapons)
            {
                if (weapon == null)
                    continue;

                weapon.onStartTriggering.RemoveListener(OnFired);
                weapon.onAction.RemoveListener(OnFired);
            }

            boundWeapons.Clear();
        }

        private void OnFired()
        {
            if (HeatTracker.Active != null)
                HeatTracker.Active.Add(heatPerShot);
        }
    }
}
