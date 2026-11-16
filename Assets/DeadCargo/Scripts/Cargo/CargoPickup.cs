using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DeadCargo.Cargo
{
    /// <summary>Loose cargo that is collected by any ship with a hold that touches it.</summary>
    [RequireComponent(typeof(Collider))]
    public sealed class CargoPickup : MonoBehaviour
    {
        private static readonly List<CargoPickup> pickups = new List<CargoPickup>();

        [SerializeField] private CargoItemDefinition item;
        [SerializeField, Min(1)] private int quantity = 1;

        private Collider trigger;

        /// <summary>Every enabled pickup in the loaded scenes, for HUD markers.</summary>
        public static IReadOnlyList<CargoPickup> All => pickups;

        public CargoItemDefinition Item => item;
        public int Quantity => quantity;

        // Statics survive play sessions when domain reload is off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            pickups.Clear();
        }

        private void OnEnable()
        {
            pickups.Add(this);
        }

        private void OnDisable()
        {
            pickups.Remove(this);
        }

        public void Initialize(CargoItemDefinition newItem, int newQuantity)
        {
            item = newItem;
            quantity = Mathf.Max(1, newQuantity);
        }

        /// <summary>Blocks collection for a time, so a ship does not re-collect what it just dropped.</summary>
        public void DelayCollection(float seconds)
        {
            if (seconds > 0f)
                StartCoroutine(DelayCollectionRoutine(seconds));
        }

        private void Awake()
        {
            trigger = GetComponent<Collider>();
        }

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            Rigidbody otherBody = other.attachedRigidbody;
            if (item == null || otherBody == null || !otherBody.TryGetComponent(out CargoHold hold))
                return;

            // Whatever does not fit stays in space for a later pass.
            quantity -= hold.Add(item, quantity);
            if (quantity <= 0)
                Destroy(gameObject);
        }

        private IEnumerator DelayCollectionRoutine(float seconds)
        {
            // Re-enabling the trigger raises OnTriggerEnter for a ship that is still overlapping.
            trigger.enabled = false;
            yield return new WaitForSeconds(seconds);
            trigger.enabled = true;
        }
    }
}
