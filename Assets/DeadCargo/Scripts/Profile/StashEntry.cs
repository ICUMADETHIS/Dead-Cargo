using System;
using UnityEngine;

namespace DeadCargo.Profile
{
    /// <summary>A quantity of one cargo item in the mothership stash, keyed by item ID.</summary>
    [Serializable]
    public struct StashEntry
    {
        [SerializeField] private string itemId;
        [SerializeField] private int quantity;

        public StashEntry(string itemId, int quantity)
        {
            this.itemId = itemId;
            this.quantity = quantity;
        }

        public string ItemId => itemId;
        public int Quantity => quantity;
    }
}
