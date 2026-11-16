using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeadCargo.Profile
{
    /// <summary>
    /// The player's saved progress: credits, the mothership stash, the service record and the last run.
    /// Changed only through <see cref="ProfileStore"/>, which saves after every change.
    /// </summary>
    [Serializable]
    public sealed class PlayerProfile
    {
        /// <summary>Save format version. Raise it and migrate in <see cref="ProfileStore"/> when the format changes.</summary>
        public const int CurrentVersion = 1;

        [SerializeField] private int version = CurrentVersion;
        [SerializeField] private int credits;
        [SerializeField] private List<StashEntry> stash = new List<StashEntry>();
        [SerializeField] private int runsExtracted;
        [SerializeField] private int runsLost;
        [SerializeField] private bool hasLastRun;
        [SerializeField] private RunSummary lastRun;

        public int Version => version;

        /// <summary>Credits held, in credits.</summary>
        public int Credits => credits;

        public IReadOnlyList<StashEntry> Stash => stash;
        public int RunsExtracted => runsExtracted;
        public int RunsLost => runsLost;
        public bool HasLastRun => hasLastRun;
        public RunSummary LastRun => lastRun;

        internal void AddCredits(int amount)
        {
            credits = Mathf.Max(0, credits + amount);
        }

        internal void RecordRun(RunSummary summary)
        {
            if (summary.Extracted)
                runsExtracted++;
            else
                runsLost++;

            lastRun = summary;
            hasLastRun = true;
        }

        internal int GetStashQuantity(string itemId)
        {
            int index = IndexOf(itemId);
            return index >= 0 ? stash[index].Quantity : 0;
        }

        internal void AddToStash(string itemId, int quantity)
        {
            if (string.IsNullOrEmpty(itemId) || quantity <= 0)
                return;

            int index = IndexOf(itemId);
            if (index < 0)
                stash.Add(new StashEntry(itemId, quantity));
            else
                stash[index] = new StashEntry(itemId, stash[index].Quantity + quantity);
        }

        internal void RemoveFromStash(string itemId)
        {
            int index = IndexOf(itemId);
            if (index >= 0)
                stash.RemoveAt(index);
        }

        internal void ClearStash()
        {
            stash.Clear();
        }

        // Repairs data a hand-edited or older save may hold.
        internal void Sanitise()
        {
            if (stash == null)
                stash = new List<StashEntry>();

            stash.RemoveAll(entry => string.IsNullOrEmpty(entry.ItemId) || entry.Quantity <= 0);
            credits = Mathf.Max(0, credits);
            version = CurrentVersion;
        }

        private int IndexOf(string itemId)
        {
            for (int i = 0; i < stash.Count; ++i)
            {
                if (stash[i].ItemId == itemId)
                    return i;
            }

            return -1;
        }
    }
}
